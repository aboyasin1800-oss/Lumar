using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ConsumptionRulesRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections, ILogger<ConsumptionRulesRepository> logger) : IConsumptionRulesRepository
{
    private sealed record ConsumptionRuleRangeCandidate(
        int RuleId,
        int ProductTypeId,
        decimal? FabricWidth,
        int? SizeClassId,
        string MeasurementKey,
        decimal? MinimumValue,
        decimal? MaximumValue,
        string Name,
        int Priority,
        string Status);

    public async Task<ConsumptionRulesDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        var productTypes = await ReadProductTypesAsync(connection, cancellationToken);
        var sizeClasses = await ReadSizeClassesAsync(connection, cancellationToken);
        var rules = await ReadRulesAsync(connection, cancellationToken);
        var profiles = await ReadMeasurementProfilesAsync(connection, cancellationToken);
        var fields = await ReadMeasurementFieldsAsync(connection, cancellationToken);

        return new ConsumptionRulesDashboardDto(
            productTypes.Count,
            sizeClasses.Count,
            rules.Count,
            rules.Count(x => x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && x.IsActive),
            productTypes,
            sizeClasses,
            rules,
            profiles,
            fields);
    }

    public async Task<ConsumptionRulesIntegrityReportDto> GetIntegrityReportAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        var productTypes = await ReadProductTypesAsync(connection, cancellationToken);
        var sizeClasses = await ReadSizeClassesAsync(connection, cancellationToken);
        var rules = await ReadRulesAsync(connection, cancellationToken);
        var profiles = await ReadMeasurementProfilesAsync(connection, cancellationToken);
        var fields = await ReadMeasurementFieldsAsync(connection, cancellationToken);

        var validCodesByProduct = fields
            .GroupBy(f => f.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Code.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var issues = new List<ConsumptionRuleIntegrityIssueDto>();
        var candidates = new List<ConsumptionRuleIntegrityCandidate>();
        var activeRangeCandidates = new Dictionary<int, ConsumptionRuleRangeCandidate>();
        foreach (var productTypeId in rules.Select(r => r.ProductTypeId).Distinct())
        {
            foreach (var candidate in await ReadActiveRuleRangeCandidatesAsync(connection, null, productTypeId, cancellationToken))
            {
                activeRangeCandidates[candidate.RuleId] = candidate;
            }
        }

        foreach (var rule in rules.Where(r => r.IsActive && string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase)))
        {
            activeRangeCandidates.TryGetValue(rule.ConsumptionRuleId, out var rangeCandidate);
            var measurementKey = rangeCandidate?.MeasurementKey ?? string.Empty;
            var range = rangeCandidate?.MinimumValue is null
                ? null
                : new RangeInfo(rangeCandidate.MinimumValue.Value, rangeCandidate.MaximumValue);
            var invalidTokens = ExtractFormulaTokens(rule.Formula)
                .Where(token => !string.Equals(token, "0", StringComparison.OrdinalIgnoreCase))
                .Where(token => !validCodesByProduct.TryGetValue(rule.ProductTypeId, out var codes) || !codes.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalidTokens.Count > 0)
            {
                foreach (var token in invalidTokens)
                {
                    issues.Add(new ConsumptionRuleIntegrityIssueDto("INVALID_MEASUREMENT", $"قياس غير صالح في الصيغة: {token}", rule.ProductTypeId, rule.ConsumptionRuleId, rule.ResultUnit, measurementKey));
                }
            }

            if (range is null)
            {
                issues.Add(new ConsumptionRuleIntegrityIssueDto("INCOMPLETE_RANGE", $"القاعدة {rule.Name} لا ترتبط بفئة مقاس ذات نطاق واضح.", rule.ProductTypeId, rule.ConsumptionRuleId, rule.ResultUnit, measurementKey));
            }

            candidates.Add(new ConsumptionRuleIntegrityCandidate(
                rule.ProductTypeId,
                rangeCandidate?.FabricWidth,
                rangeCandidate?.SizeClassId ?? rule.SizeClassId,
                measurementKey,
                rule.Priority,
                rangeCandidate?.MinimumValue,
                rangeCandidate?.MaximumValue,
                rule.Formula,
                rule.Status,
                rule.ConsumptionRuleId));
        }

        var validation = ConsumptionRuleIntegrityValidator.Validate(candidates);
        var mergedIssues = validation.Issues
            .Select(issue => new ConsumptionRuleIntegrityIssueDto(issue.Type, issue.Message, issue.ProductTypeId, issue.RuleId, issue.FabricWidth, issue.MeasurementKey))
            .Concat(issues)
            .ToList();

        return new ConsumptionRulesIntegrityReportDto(
            validation.TotalRules,
            validation.ValidRules,
            validation.OverlapCount,
            validation.DuplicateCount,
            validation.GapCount,
            validation.ProductTypesCoveringAllSizes,
            validation.ProductTypesNeedingCompletion,
            mergedIssues);
    }

    public async Task<IReadOnlyList<ConsumptionRuleDto>> SaveProductRulesBatchAsync(SaveProductRulesBatchDto request, CancellationToken cancellationToken)
    {
        if (request is null || request.ProductTypeId <= 0 || request.Rules is null || request.Rules.Count == 0)
        {
            return Array.Empty<ConsumptionRuleDto>();
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var productExists = await ExistsAsync(connection, transaction, "SELECT TOP (1) 1 FROM dbo.PricingProductTypes WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@id AND IsActive=1", new { id = request.ProductTypeId }, cancellationToken);
            if (!productExists)
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                return Array.Empty<ConsumptionRuleDto>();
            }

            var activeRangeCandidates = await ReadActiveRuleRangeCandidatesAsync(connection, transaction, request.ProductTypeId, cancellationToken);
            var allCandidates = new List<ConsumptionRuleIntegrityCandidate>();
            var savedRules = new List<ConsumptionRuleDto>();

            foreach (var rule in request.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.Formula) || string.IsNullOrWhiteSpace(rule.ResultUnit))
                {
                    throw new InvalidOperationException("يجب تعبئة الاسم والصيغة ووحدة القياس لكل قاعدة.");
                }

                if (rule.FabricWidth is <= 0)
                {
                    throw new InvalidOperationException("عرض القماش مطلوب لكل قاعدة.");
                }

                if (string.IsNullOrWhiteSpace(rule.Formula))
                {
                    throw new InvalidOperationException("صيغة القاعدة مطلوبة.");
                }

                var hasExactDuplicate = await HasExactRuleDuplicateAsync(connection, transaction, request.ProductTypeId, rule, cancellationToken);
                if (hasExactDuplicate)
                {
                    throw new InvalidOperationException("هذه القاعدة موجودة فعليًا لنفس القطعة بنفس المعطيات.");
                }

                var effectiveSizeClassId = rule.SizeClassId;
                var resolvedRule = rule;
                if (rule.SizeClassId is null)
                {
                    var createdSizeClass = await EnsureSizeClassAsync(connection, transaction, request.ProductTypeId, rule, cancellationToken);
                    effectiveSizeClassId = createdSizeClass.SizeClassId;
                }

                if (effectiveSizeClassId is not null)
                {
                    var sizeClass = await ReadSizeClassByIdAsync(connection, transaction, effectiveSizeClassId.Value, cancellationToken);
                    if (sizeClass is null)
                    {
                        throw new InvalidOperationException("فئة القاعدة المحددة غير موجودة أو غير مرخصة للاستخدام.");
                    }

                    resolvedRule = new CreateConsumptionRuleDto
                    {
                        ProductTypeId = rule.ProductTypeId,
                        SizeClassId = sizeClass.SizeClassId,
                        FabricWidth = rule.FabricWidth,
                        FabricWidthUnit = rule.FabricWidthUnit,
                        FirstMeasurementCode = rule.FirstMeasurementCode,
                        FirstFactor = rule.FirstFactor,
                        SecondMeasurementCode = rule.SecondMeasurementCode,
                        SecondFactor = rule.SecondFactor,
                        ConditionalMeasurementCode = rule.ConditionalMeasurementCode ?? sizeClass.MeasurementCode,
                        MinimumValue = rule.MinimumValue ?? sizeClass.MinimumValue,
                        MaximumValue = rule.MaximumValue ?? sizeClass.MaximumValue,
                        FixedIncrease = rule.FixedIncrease,
                        Name = rule.Name,
                        RuleType = rule.RuleType,
                        Formula = rule.Formula,
                        ResultUnit = rule.ResultUnit,
                        Priority = rule.Priority,
                        Status = rule.Status,
                    };
                }

                var validationError = await ValidateIntegrityAsync(connection, transaction, request.ProductTypeId, resolvedRule.FabricWidth, resolvedRule.Name, resolvedRule.Formula, resolvedRule.Priority, resolvedRule.Status, null, resolvedRule.MinimumValue, resolvedRule.MaximumValue, resolvedRule.ConditionalMeasurementCode ?? resolvedRule.FirstMeasurementCode, resolvedRule.SizeClassId, cancellationToken);
                if (validationError is not null)
                {
                    throw new InvalidOperationException(validationError);
                }

                var sizeClassInfo = await ResolveSizeClassContextAsync(connection, transaction, request.ProductTypeId, resolvedRule, cancellationToken);
                allCandidates.Add(new ConsumptionRuleIntegrityCandidate(
                    resolvedRule.ProductTypeId,
                    resolvedRule.FabricWidth,
                    sizeClassInfo?.SizeClassId,
                    sizeClassInfo?.MeasurementCode ?? resolvedRule.ConditionalMeasurementCode ?? resolvedRule.FirstMeasurementCode ?? string.Empty,
                    resolvedRule.Priority,
                    resolvedRule.MinimumValue ?? sizeClassInfo?.MinimumValue,
                    resolvedRule.MaximumValue ?? sizeClassInfo?.MaximumValue,
                    resolvedRule.Formula,
                    resolvedRule.Status));

                var inserted = await InsertRuleAsync(connection, transaction, resolvedRule, cancellationToken);
                if (inserted is null)
                {
                    throw new InvalidOperationException("تعذر حفظ قاعدة الاستهلاك في المعاملة الحالية.");
                }

                savedRules.Add(inserted);
            }

            var combined = activeRangeCandidates
                .Select(r => new ConsumptionRuleIntegrityCandidate(
                    r.ProductTypeId,
                    r.FabricWidth,
                    r.SizeClassId,
                    r.MeasurementKey,
                    r.Priority,
                    r.MinimumValue,
                    r.MaximumValue,
                    string.Empty,
                    r.Status,
                    r.RuleId))
                .Concat(allCandidates)
                .ToList();

            var integrity = ConsumptionRuleIntegrityValidator.Validate(combined);
            if (integrity.OverlapCount > 0 || integrity.DuplicateCount > 0 || integrity.GapCount > 0)
            {
                var issue = integrity.Issues.FirstOrDefault() ?? new ConsumptionRuleIntegrityIssue("GAP", "يوجد نطاق غير مغطى أو متداخل في القواعد النشطة.", request.ProductTypeId, null, request.Rules.First().FabricWidth?.ToString(System.Globalization.CultureInfo.InvariantCulture), request.Rules.First().Name);
                throw new InvalidOperationException(issue.Message);
            }

            await transaction.CommitAsync(cancellationToken);
            return savedRules;
        }
        catch
        {
            await SafeRollbackAsync(transaction, CancellationToken.None);
            throw;
        }
    }

    public async Task<ConsumptionRuleDto?> CreateAsync(CreateConsumptionRuleDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return null;
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var productExists = await ExistsAsync(connection, transaction, "SELECT TOP (1) 1 FROM dbo.PricingProductTypes WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@id AND IsActive=1", new { id = request.ProductTypeId }, cancellationToken);
            if (!productExists)
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                return null;
            }

            if (request.SizeClassId is not null)
            {
                var sizeExists = await ExistsAsync(connection, transaction, "SELECT TOP (1) 1 FROM dbo.PricingSizeClasses WITH (UPDLOCK,HOLDLOCK) WHERE SizeClassId=@id AND ProductTypeId=@productTypeId AND IsActive=1", new { id = request.SizeClassId, productTypeId = request.ProductTypeId }, cancellationToken);
                if (!sizeExists)
                {
                    await SafeRollbackAsync(transaction, cancellationToken);
                    return null;
                }
            }

            var effectiveSizeClassId = request.SizeClassId;
            var resolvedRequest = request;
            if (request.SizeClassId is null && (request.MinimumValue is not null || request.MaximumValue is not null || !string.IsNullOrWhiteSpace(request.ConditionalMeasurementCode)))
            {
                var createdSizeClass = await EnsureSizeClassAsync(connection, transaction, request.ProductTypeId, request, cancellationToken);
                effectiveSizeClassId = createdSizeClass.SizeClassId;
            }

            if (effectiveSizeClassId is not null)
            {
                var sizeClass = await ReadSizeClassByIdAsync(connection, transaction, effectiveSizeClassId.Value, cancellationToken);
                if (sizeClass is null)
                {
                    await SafeRollbackAsync(transaction, cancellationToken);
                    return null;
                }

                resolvedRequest = new CreateConsumptionRuleDto
                {
                    ProductTypeId = request.ProductTypeId,
                    SizeClassId = sizeClass.SizeClassId,
                    FabricWidth = request.FabricWidth,
                    FabricWidthUnit = request.FabricWidthUnit,
                    FirstMeasurementCode = request.FirstMeasurementCode,
                    FirstFactor = request.FirstFactor,
                    SecondMeasurementCode = request.SecondMeasurementCode,
                    SecondFactor = request.SecondFactor,
                    ConditionalMeasurementCode = request.ConditionalMeasurementCode ?? sizeClass.MeasurementCode,
                    MinimumValue = request.MinimumValue ?? sizeClass.MinimumValue,
                    MaximumValue = request.MaximumValue ?? sizeClass.MaximumValue,
                    FixedIncrease = request.FixedIncrease,
                    Name = request.Name,
                    RuleType = request.RuleType,
                    Formula = request.Formula,
                    ResultUnit = request.ResultUnit,
                    Priority = request.Priority,
                    Status = request.Status,
                };
            }

            logger.LogInformation(
                "Consumption rule create values. ProductTypeId={ProductTypeId}, Name={Name}, Formula={Formula}, SizeClassId={SizeClassId}, MeasurementCode={MeasurementCode}, MinimumValue={MinimumValue}, MaximumValue={MaximumValue}, FabricWidth={FabricWidth}, FabricWidthUnit={FabricWidthUnit}, ResultUnit={ResultUnit}, Priority={Priority}, Status={Status}",
                resolvedRequest.ProductTypeId, resolvedRequest.Name, resolvedRequest.Formula, effectiveSizeClassId, resolvedRequest.ConditionalMeasurementCode, resolvedRequest.MinimumValue, resolvedRequest.MaximumValue, resolvedRequest.FabricWidth, resolvedRequest.FabricWidthUnit, resolvedRequest.ResultUnit, resolvedRequest.Priority, resolvedRequest.Status);

            var hasExactDuplicate = await HasExactRuleDuplicateAsync(connection, transaction, request.ProductTypeId, resolvedRequest, cancellationToken);
            if (hasExactDuplicate)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("هذه القاعدة موجودة فعليًا لنفس القطعة بنفس المعطيات.");
            }

            var validationError = await ValidateIntegrityAsync(connection, transaction, resolvedRequest.ProductTypeId, resolvedRequest.FabricWidth, resolvedRequest.Name, resolvedRequest.Formula, resolvedRequest.Priority, resolvedRequest.Status, null, resolvedRequest.MinimumValue, resolvedRequest.MaximumValue, resolvedRequest.ConditionalMeasurementCode ?? resolvedRequest.FirstMeasurementCode, effectiveSizeClassId, cancellationToken);
            if (validationError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException(validationError);
            }

            var sql = @"
                INSERT INTO dbo.PricingConsumptionRules
                    (ProductTypeId, SizeClassId, FabricWidth, FabricWidthUnit, Name, RuleType, Formula, ResultUnit, Priority, Version, Status, EffectiveFrom, IsActive, ChangeReason, CreatedBy, CreatedAt)
                OUTPUT INSERTED.*
                VALUES (@ProductTypeId, @SizeClassId, @FabricWidth, @FabricWidthUnit, @Name, @RuleType, @Formula, @ResultUnit, @Priority, 1, @Status, SYSDATETIME(), 1, N'Created via consumption rules settings', N'LUMAR ERP', SYSDATETIME());";

            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@ProductTypeId", resolvedRequest.ProductTypeId);
            command.Parameters.AddWithValue("@SizeClassId", effectiveSizeClassId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FabricWidth", resolvedRequest.FabricWidth ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FabricWidthUnit", string.IsNullOrWhiteSpace(resolvedRequest.FabricWidthUnit) ? (object)DBNull.Value : resolvedRequest.FabricWidthUnit.Trim());
            command.Parameters.AddWithValue("@Name", resolvedRequest.Name.Trim());
            command.Parameters.AddWithValue("@RuleType", resolvedRequest.RuleType.Trim());
            command.Parameters.AddWithValue("@Formula", resolvedRequest.Formula.Trim());
            command.Parameters.AddWithValue("@ResultUnit", resolvedRequest.ResultUnit.Trim());
            command.Parameters.AddWithValue("@Priority", resolvedRequest.Priority);
            command.Parameters.AddWithValue("@Status", resolvedRequest.Status.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var result = MapRule(reader);
            await reader.CloseAsync();
            result = await ReadRuleByIdAsync(connection, transaction, result.ConsumptionRuleId, cancellationToken);
            if (result is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
            logger.LogInformation(
                "Consumption rule create completed. RuleId={RuleId}, ProductTypeId={ProductTypeId}, Name={Name}, Formula={Formula}, SizeClassId={SizeClassId}, FabricWidth={FabricWidth}, MeasurementCode={MeasurementCode}, MinimumValue={MinimumValue}, MaximumValue={MaximumValue}",
                result.ConsumptionRuleId, result.ProductTypeId, result.Name, result.Formula, result.SizeClassId, result.FabricWidth, result.MeasurementCode, result.MinimumValue, result.MaximumValue);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (transaction.Connection is not null && transaction.Connection.State == ConnectionState.Open)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Transaction may already be committed or completed; ignore for the final result.
                }
            }
            throw;
        }
    }

    public async Task<MeasurementTypeWriteResultDto?> CreateMeasurementTypeAsync(CreateMeasurementTypeDto request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.NameAr))
        {
            return null;
        }

        var normalizedNames = request.FieldNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedNames.Count == 0 && request.FieldCount > 0)
        {
            return null;
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var code = string.IsNullOrWhiteSpace(request.Code) ? await GenerateUniqueProductTypeCodeAsync(connection, transaction, request.NameAr, cancellationToken) : await EnsureUniqueProductTypeCodeAsync(connection, transaction, request.Code.Trim(), cancellationToken);
            var insertProductSql = @"
                INSERT INTO dbo.PricingProductTypes (Code, NameAr, NameEn, Category, Scope, IsActive, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.ProductTypeId
                VALUES (@Code, @NameAr, @NameEn, N'Garment', N'Both', 1, SYSDATETIME(), SYSDATETIME());";

            await using (var productCommand = new SqlCommand(insertProductSql, connection, transaction))
            {
                productCommand.Parameters.AddWithValue("@Code", code);
                productCommand.Parameters.AddWithValue("@NameAr", request.NameAr.Trim());
                productCommand.Parameters.AddWithValue("@NameEn", request.NameAr.Trim());

                var insertedProductId = await productCommand.ExecuteScalarAsync(cancellationToken) as int?;
                if (insertedProductId is null)
                {
                    await SafeRollbackAsync(transaction, cancellationToken);
                    return null;
                }

                var productTypeId = insertedProductId.Value;
                var profileName = $"{request.NameAr.Trim()} - قياسات";
                var profileSql = @"
                    INSERT INTO dbo.PricingMeasurementProfiles (ProductTypeId, Name, Version, Status, IsActive, EffectiveFrom, CreatedAt, UpdatedAt)
                    OUTPUT INSERTED.MeasurementProfileId
                    VALUES (@ProductTypeId, @Name, 1, N'Active', 1, SYSDATETIME(), SYSDATETIME(), SYSDATETIME());";

                await using (var profileCommand = new SqlCommand(profileSql, connection, transaction))
                {
                    profileCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                    profileCommand.Parameters.AddWithValue("@Name", profileName);

                    var insertedProfileId = await profileCommand.ExecuteScalarAsync(cancellationToken) as int?;
                    if (insertedProfileId is null)
                    {
                        await SafeRollbackAsync(transaction, cancellationToken);
                        return null;
                    }

                    var measurementProfileId = insertedProfileId.Value;
                    var fieldList = normalizedNames.Count > 0 ? normalizedNames : Enumerable.Range(1, request.FieldCount).Select(i => $"حقل {i}").ToList();

                    for (var index = 0; index < fieldList.Count; index++)
                    {
                        var fieldName = fieldList[index].Trim();
                        var fieldCode = await GenerateUniqueMeasurementFieldCodeAsync(connection, transaction, measurementProfileId, fieldName, index + 1, cancellationToken);
                        var fieldSql = @"
                            INSERT INTO dbo.PricingMeasurementFields (MeasurementProfileId, Code, NameAr, Unit, IsRequired, Sequence)
                            VALUES (@MeasurementProfileId, @Code, @NameAr, N'بوصة', 1, @Sequence);";

                        await using (var fieldCommand = new SqlCommand(fieldSql, connection, transaction))
                        {
                            fieldCommand.Parameters.AddWithValue("@MeasurementProfileId", measurementProfileId);
                            fieldCommand.Parameters.AddWithValue("@Code", fieldCode);
                            fieldCommand.Parameters.AddWithValue("@NameAr", fieldName);
                            fieldCommand.Parameters.AddWithValue("@Sequence", index + 1);
                            await fieldCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return new MeasurementTypeWriteResultDto(productTypeId, measurementProfileId, fieldList.Count, request.NameAr.Trim());
                }
            }
        }
        catch
        {
            await SafeRollbackAsync(transaction, CancellationToken.None);
            throw;
        }
    }

    public async Task<MeasurementTypeWriteResultDto?> UpdateMeasurementTypeAsync(int productTypeId, UpdateMeasurementTypeDto request, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0 || request is null || string.IsNullOrWhiteSpace(request.NameAr))
        {
            return null;
        }

        var fieldNames = request.FieldNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fieldNames.Count == 0)
        {
            return null;
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var productCheckSql = "SELECT TOP (1) ProductTypeId FROM dbo.PricingProductTypes WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@ProductTypeId AND IsActive=1;";
            await using (var checkCommand = new SqlCommand(productCheckSql, connection, transaction))
            {
                checkCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                await using var checkReader = await checkCommand.ExecuteReaderAsync(cancellationToken);
                if (!await checkReader.ReadAsync(cancellationToken))
                {
                    await SafeRollbackAsync(transaction, cancellationToken);
                    return null;
                }
            }

            var updateProductSql = @"UPDATE dbo.PricingProductTypes SET NameAr=@NameAr, NameEn=@NameAr, UpdatedAt=SYSDATETIME() WHERE ProductTypeId=@ProductTypeId AND IsActive=1;";
            await using (var updateCommand = new SqlCommand(updateProductSql, connection, transaction))
            {
                updateCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                updateCommand.Parameters.AddWithValue("@NameAr", request.NameAr.Trim());
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var profileSql = "SELECT TOP (1) MeasurementProfileId FROM dbo.PricingMeasurementProfiles WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@ProductTypeId AND IsActive=1 ORDER BY MeasurementProfileId DESC;";
            int? measurementProfileId = null;
            await using (var profileCommand = new SqlCommand(profileSql, connection, transaction))
            {
                profileCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                await using var profileReader = await profileCommand.ExecuteReaderAsync(cancellationToken);
                if (await profileReader.ReadAsync(cancellationToken))
                {
                    measurementProfileId = profileReader.GetInt32(0);
                }
            }

            if (measurementProfileId is null)
            {
                var newProfileSql = @"
                    INSERT INTO dbo.PricingMeasurementProfiles (ProductTypeId, Name, Version, Status, IsActive, EffectiveFrom, CreatedAt, UpdatedAt)
                    OUTPUT INSERTED.MeasurementProfileId
                    VALUES (@ProductTypeId, @Name, 1, N'Active', 1, SYSDATETIME(), SYSDATETIME(), SYSDATETIME());";

                await using (var newProfileCommand = new SqlCommand(newProfileSql, connection, transaction))
                {
                    newProfileCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                    newProfileCommand.Parameters.AddWithValue("@Name", $"{request.NameAr.Trim()} - قياسات");
                    await using var newProfileReader = await newProfileCommand.ExecuteReaderAsync(cancellationToken);
                    if (!await newProfileReader.ReadAsync(cancellationToken))
                    {
                        await SafeRollbackAsync(transaction, cancellationToken);
                        return null;
                    }

                    measurementProfileId = newProfileReader.GetInt32(0);
                }
            }

            var deleteFieldsSql = "UPDATE dbo.PricingMeasurementFields SET IsRequired=0 WHERE MeasurementProfileId=@MeasurementProfileId;";
            await using (var deleteCommand = new SqlCommand(deleteFieldsSql, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@MeasurementProfileId", measurementProfileId.Value);
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            for (var index = 0; index < fieldNames.Count; index++)
            {
                var fieldName = fieldNames[index];
                var fieldCode = await GenerateUniqueMeasurementFieldCodeAsync(connection, transaction, measurementProfileId.Value, fieldName, index + 1, cancellationToken);
                var insertFieldSql = @"
                    INSERT INTO dbo.PricingMeasurementFields (MeasurementProfileId, Code, NameAr, Unit, IsRequired, Sequence)
                    VALUES (@MeasurementProfileId, @Code, @NameAr, N'بوصة', 1, @Sequence);";

                await using (var fieldCommand = new SqlCommand(insertFieldSql, connection, transaction))
                {
                    fieldCommand.Parameters.AddWithValue("@MeasurementProfileId", measurementProfileId.Value);
                    fieldCommand.Parameters.AddWithValue("@Code", fieldCode);
                    fieldCommand.Parameters.AddWithValue("@NameAr", fieldName);
                    fieldCommand.Parameters.AddWithValue("@Sequence", index + 1);
                    await fieldCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new MeasurementTypeWriteResultDto(productTypeId, measurementProfileId.Value, fieldNames.Count, request.NameAr.Trim());
        }
        catch
        {
            await SafeRollbackAsync(transaction, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> DeleteMeasurementTypeAsync(int productTypeId, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0)
        {
            return false;
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var hasRules = await ExistsAsync(connection, transaction,
                "SELECT TOP (1) 1 FROM dbo.PricingConsumptionRules WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@ProductTypeId AND IsActive=1",
                new { ProductTypeId = productTypeId }, cancellationToken);

            if (hasRules)
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                return false;
            }

            await using (var profileCommand = new SqlCommand("SELECT MeasurementProfileId FROM dbo.PricingMeasurementProfiles WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@ProductTypeId AND IsActive=1", connection, transaction))
            {
                profileCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                await using var profileReader = await profileCommand.ExecuteReaderAsync(cancellationToken);
                while (await profileReader.ReadAsync(cancellationToken))
                {
                    var profileId = profileReader.GetInt32(0);
                    await using (var deleteFieldCommand = new SqlCommand("UPDATE dbo.PricingMeasurementFields SET IsRequired=0 WHERE MeasurementProfileId=@MeasurementProfileId;", connection, transaction))
                    {
                        deleteFieldCommand.Parameters.AddWithValue("@MeasurementProfileId", profileId);
                        await deleteFieldCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            await using (var profileUpdateCommand = new SqlCommand("UPDATE dbo.PricingMeasurementProfiles SET IsActive=0, Status=N'Inactive', UpdatedAt=SYSDATETIME() WHERE ProductTypeId=@ProductTypeId;", connection, transaction))
            {
                profileUpdateCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                await profileUpdateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var productCommand = new SqlCommand("UPDATE dbo.PricingProductTypes SET IsActive=0, UpdatedAt=SYSDATETIME() WHERE ProductTypeId=@ProductTypeId;", connection, transaction))
            {
                productCommand.Parameters.AddWithValue("@ProductTypeId", productTypeId);
                await productCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await SafeRollbackAsync(transaction, CancellationToken.None);
            throw;
        }
    }

    public async Task<ConsumptionRuleDto?> UpdateAsync(int consumptionRuleId, UpdateConsumptionRuleDto request, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var existingRule = await ReadRuleByIdAsync(connection, transaction, consumptionRuleId, cancellationToken);
            if (existingRule is null)
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                return null;
            }

            logger.LogInformation(
                "Consumption rule update started. RuleId={RuleId}, ProductTypeId={ProductTypeId}, OldName={OldName}, OldFormula={OldFormula}, OldSizeClassId={OldSizeClassId}, OldFabricWidth={OldFabricWidth}",
                consumptionRuleId, existingRule.ProductTypeId, existingRule.Name, existingRule.Formula, existingRule.SizeClassId, existingRule.FabricWidth);

            var requestedSizeClassId = request.SizeClassId ?? existingRule.SizeClassId;
            var hasRangeUpdate = request.SizeClassId is not null
                || request.MinimumValue is not null
                || request.MaximumValue is not null
                || request.ConditionalMeasurementCode is not null;
            var resolvedSizeClass = hasRangeUpdate
                ? await ResolveSizeClassContextAsync(connection, transaction, existingRule.ProductTypeId, new CreateConsumptionRuleDto
                {
                    ProductTypeId = existingRule.ProductTypeId,
                    SizeClassId = requestedSizeClassId,
                    FabricWidth = request.FabricWidth,
                    FabricWidthUnit = string.Empty,
                    FirstMeasurementCode = request.FirstMeasurementCode,
                    FirstFactor = request.FirstFactor,
                    SecondMeasurementCode = request.SecondMeasurementCode,
                    SecondFactor = request.SecondFactor,
                    ConditionalMeasurementCode = request.ConditionalMeasurementCode,
                    MinimumValue = request.MinimumValue,
                    MaximumValue = request.MaximumValue,
                    FixedIncrease = request.FixedIncrease,
                    Name = request.Name,
                    RuleType = "Formula",
                    Formula = request.Formula,
                    ResultUnit = request.ResultUnit,
                    Priority = request.Priority,
                    Status = request.Status,
                }, cancellationToken)
                : null;

            var effectiveSizeClassId = resolvedSizeClass?.SizeClassId ?? existingRule.SizeClassId;
            var fabricWidth = request.FabricWidth ?? (existingRule.ResultUnit.Contains("Inch", StringComparison.OrdinalIgnoreCase) ? 58m : null);
            var effectiveMinimumValue = resolvedSizeClass is null
                ? request.MinimumValue
                : request.MinimumValue ?? resolvedSizeClass.MinimumValue;
            var effectiveMaximumValue = request.MinimumValue is not null
                ? request.MaximumValue
                : resolvedSizeClass?.MaximumValue ?? request.MaximumValue;
            var effectiveMeasurementCode = request.ConditionalMeasurementCode ?? resolvedSizeClass?.MeasurementCode;
            logger.LogInformation(
                "Consumption rule update values. RuleId={RuleId}, Name={Name}, Formula={Formula}, SizeClassId={SizeClassId}, MeasurementCode={MeasurementCode}, MinimumValue={MinimumValue}, MaximumValue={MaximumValue}, FabricWidth={FabricWidth}, FabricWidthUnit={FabricWidthUnit}, ResultUnit={ResultUnit}, Priority={Priority}, Status={Status}",
                consumptionRuleId, request.Name, request.Formula, effectiveSizeClassId, effectiveMeasurementCode, effectiveMinimumValue, effectiveMaximumValue, request.FabricWidth, request.FabricWidthUnit, request.ResultUnit, request.Priority, request.Status);
            var validationError = await ValidateIntegrityAsync(connection, transaction, existingRule.ProductTypeId, fabricWidth, request.Name, request.Formula, request.Priority, request.Status, consumptionRuleId, effectiveMinimumValue, effectiveMaximumValue, effectiveMeasurementCode, effectiveSizeClassId, cancellationToken);
            if (validationError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException(validationError);
            }

            if (effectiveSizeClassId is not null && hasRangeUpdate)
            {
                await UpdateSizeClassValuesAsync(
                    connection,
                    transaction,
                    effectiveSizeClassId.Value,
                    request.Name,
                    effectiveMeasurementCode,
                    effectiveMinimumValue,
                    effectiveMaximumValue,
                    cancellationToken);
            }

            var sql = @"
                UPDATE dbo.PricingConsumptionRules
                SET Name=@Name,
                    Formula=@Formula,
                    ResultUnit=@ResultUnit,
                    Priority=@Priority,
                    Status=@Status,
                    SizeClassId=@SizeClassId,
                    FabricWidth=COALESCE(@FabricWidth, FabricWidth),
                    FabricWidthUnit=COALESCE(@FabricWidthUnit, FabricWidthUnit),
                    UpdatedAt=SYSDATETIME(),
                    ChangeReason=N'Updated via consumption rules settings'
                OUTPUT INSERTED.*
                WHERE ConsumptionRuleId=@ConsumptionRuleId AND IsActive=1;";

            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@ConsumptionRuleId", consumptionRuleId);
            command.Parameters.AddWithValue("@Name", request.Name.Trim());
            command.Parameters.AddWithValue("@Formula", request.Formula.Trim());
            command.Parameters.AddWithValue("@ResultUnit", request.ResultUnit.Trim());
            command.Parameters.AddWithValue("@Priority", request.Priority);
            command.Parameters.AddWithValue("@Status", request.Status.Trim());
            command.Parameters.AddWithValue("@SizeClassId", effectiveSizeClassId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FabricWidth", request.FabricWidth ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FabricWidthUnit", string.IsNullOrWhiteSpace(request.FabricWidthUnit) ? (object)DBNull.Value : request.FabricWidthUnit.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var result = MapRule(reader);
            await reader.CloseAsync();
            result = await ReadRuleByIdAsync(connection, transaction, result.ConsumptionRuleId, cancellationToken);
            if (result is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
            logger.LogInformation(
                "Consumption rule update completed. RuleId={RuleId}, ProductTypeId={ProductTypeId}, Name={Name}, Formula={Formula}, SizeClassId={SizeClassId}, FabricWidth={FabricWidth}, MeasurementCode={MeasurementCode}, MinimumValue={MinimumValue}, MaximumValue={MaximumValue}",
                result.ConsumptionRuleId, result.ProductTypeId, result.Name, result.Formula, result.SizeClassId, result.FabricWidth, result.MeasurementCode, result.MinimumValue, result.MaximumValue);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (transaction.Connection is not null && transaction.Connection.State == ConnectionState.Open)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Transaction may already be committed or completed; ignore for the final result.
                }
            }
            throw;
        }
    }

    private static async Task<List<ConsumptionRuleProductTypeDto>> ReadProductTypesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT ProductTypeId, Code, NameAr, Category, Scope, IsActive
            FROM dbo.PricingProductTypes WITH (NOLOCK)
            WHERE IsActive = 1
            ORDER BY NameAr, ProductTypeId;";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleProductTypeDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ConsumptionRuleProductTypeDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetBoolean(5)));
        }

        return items;
    }

    private static ConsumptionRuleSizeClassDto MapSizeClass(SqlDataReader reader)
    {
        var sizeClassId = reader.GetOrdinal("SizeClassId");
        var productTypeId = reader.GetOrdinal("ProductTypeId");
        var code = reader.GetOrdinal("Code");
        var nameAr = reader.GetOrdinal("NameAr");
        var measurementCode = reader.GetOrdinal("MeasurementCode");
        var minimumValue = reader.GetOrdinal("MinimumValue");
        var maximumValue = reader.GetOrdinal("MaximumValue");
        var sequence = reader.GetOrdinal("Sequence");
        var status = reader.GetOrdinal("Status");
        var isActive = reader.GetOrdinal("IsActive");

        return new ConsumptionRuleSizeClassDto(
            reader.GetInt32(sizeClassId),
            reader.GetInt32(productTypeId),
            reader.GetString(code),
            reader.GetString(nameAr),
            reader.GetString(measurementCode),
            reader.IsDBNull(minimumValue) ? null : reader.GetDecimal(minimumValue),
            reader.IsDBNull(maximumValue) ? null : reader.GetDecimal(maximumValue),
            reader.GetInt32(sequence),
            reader.GetString(status),
            reader.GetBoolean(isActive));
    }

    private static async Task<List<ConsumptionRuleSizeClassDto>> ReadSizeClassesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT SizeClassId, ProductTypeId, Code, NameAr, MeasurementCode, MinimumValue, MaximumValue, Sequence, Status, IsActive
            FROM dbo.PricingSizeClasses WITH (NOLOCK)
            WHERE IsActive = 1
            ORDER BY ProductTypeId, Sequence, SizeClassId;";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleSizeClassDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapSizeClass(reader));
        }

        return items;
    }

    private static async Task<List<ConsumptionRuleDto>> ReadRulesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT cr.ConsumptionRuleId,
                   cr.ProductTypeId,
                   cr.SizeClassId,
                   pt.NameAr AS ProductTypeName,
                   sc.NameAr AS SizeClassName,
                   cr.Name,
                   cr.RuleType,
                   cr.Formula,
                   cr.ResultUnit,
                   cr.Priority,
                   cr.Version,
                   cr.Status,
                   cr.IsActive,
                   cr.EffectiveFrom,
                   cr.EffectiveTo,
                   cr.FabricWidth,
                   cr.FabricWidthUnit,
                   sc.MeasurementCode,
                   sc.MinimumValue,
                   sc.MaximumValue
            FROM dbo.PricingConsumptionRules cr WITH (NOLOCK)
            LEFT JOIN dbo.PricingProductTypes pt WITH (NOLOCK) ON pt.ProductTypeId = cr.ProductTypeId
            LEFT JOIN dbo.PricingSizeClasses sc WITH (NOLOCK) ON sc.SizeClassId = cr.SizeClassId
            WHERE cr.IsActive = 1
            ORDER BY cr.ProductTypeId, cr.Priority, cr.ConsumptionRuleId;";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ConsumptionRuleDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(11),
                reader.GetBoolean(12),
                reader.GetDateTime(13),
                reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.IsDBNull(17) ? null : reader.GetString(17),
                reader.IsDBNull(18) ? null : reader.GetDecimal(18),
                reader.IsDBNull(19) ? null : reader.GetDecimal(19)));
        }

        return items;
    }

    private static async Task<List<ConsumptionRuleMeasurementProfileDto>> ReadMeasurementProfilesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT MeasurementProfileId, ProductTypeId, Name, Version, Status, IsActive, EffectiveFrom, EffectiveTo
            FROM dbo.PricingMeasurementProfiles WITH (NOLOCK)
            WHERE IsActive = 1
            ORDER BY ProductTypeId, MeasurementProfileId;";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleMeasurementProfileDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ConsumptionRuleMeasurementProfileDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetDateTime(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7)));
        }

        return items;
    }

    private static Task<List<ConsumptionRuleMeasurementFieldDto>> ReadMeasurementFieldsAsync(SqlConnection connection, CancellationToken cancellationToken)
        => ReadMeasurementFieldsAsync(connection, null, cancellationToken);

    private static async Task<List<ConsumptionRuleMeasurementFieldDto>> ReadMeasurementFieldsAsync(SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT mf.MeasurementFieldId, mf.MeasurementProfileId, mp.ProductTypeId, mf.Code, mf.NameAr, mf.Unit, mf.IsRequired, mf.Sequence
            FROM dbo.PricingMeasurementFields mf WITH (NOLOCK)
            LEFT JOIN dbo.PricingMeasurementProfiles mp WITH (NOLOCK) ON mp.MeasurementProfileId = mf.MeasurementProfileId
            WHERE mf.IsRequired = 1 OR mf.Sequence IS NOT NULL
            ORDER BY mp.ProductTypeId, mf.Sequence, mf.MeasurementFieldId;";

        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleMeasurementFieldDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ConsumptionRuleMeasurementFieldDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.GetInt32(7)));
        }

        return items;
    }

    private static async Task<bool> ExistsAsync(SqlConnection connection, SqlTransaction transaction, string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        if (parameters is not null)
        {
            foreach (var property in parameters.GetType().GetProperties())
            {
                command.Parameters.AddWithValue($"@{property.Name}", property.GetValue(parameters) ?? DBNull.Value);
            }
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private static async Task<bool> HasExactRuleDuplicateAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, CreateConsumptionRuleDto rule, CancellationToken cancellationToken)
    {
        if (rule is null)
        {
            return false;
        }

        const string sql = @"
            SELECT TOP (1) 1
            FROM dbo.PricingConsumptionRules WITH (UPDLOCK,HOLDLOCK)
            WHERE ProductTypeId = @ProductTypeId
              AND IsActive = 1
              AND LTRIM(RTRIM(Name)) = @Name
              AND LTRIM(RTRIM(Formula)) = @Formula
              AND LTRIM(RTRIM(ResultUnit)) = @ResultUnit;";

        return await ExistsAsync(connection, transaction, sql, new
        {
            ProductTypeId = productTypeId,
            Name = rule.Name?.Trim() ?? string.Empty,
            Formula = rule.Formula?.Trim() ?? string.Empty,
            ResultUnit = rule.ResultUnit?.Trim() ?? string.Empty,
        }, cancellationToken);
    }

    private static async Task<string?> ValidateIntegrityAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, decimal? fabricWidth, string name, string formula, int priority, string status, int? excludeRuleId, decimal? minimumValue, decimal? maximumValue, string? conditionalMeasurementCode, int? sizeClassId, CancellationToken cancellationToken)
    {
        var productFields = await ReadMeasurementFieldsAsync(connection, transaction, cancellationToken);
        var validCodes = productFields
            .Where(f => f.ProductTypeId == productTypeId)
            .Select(f => f.Code)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tokens = ExtractFormulaTokens(formula).Where(token => !string.IsNullOrWhiteSpace(token)).ToList();

        // Ensure the command stream stays single-reader per call by materializing the rule context
        // before the next SQL command executes on the same transaction-aware connection.
        var invalidTokens = tokens
            .Where(token => !validCodes.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invalidTokens.Count > 0)
        {
            return $"قياسات غير صالحة ضمن الصيغة: {string.Join(", ", invalidTokens)}.";
        }

        if (fabricWidth is <= 0)
        {
            return "عرض القماش يجب أن يكون رقماً موجباً.";
        }

        var hasRangeContext = sizeClassId is not null
            || minimumValue is not null
            || maximumValue is not null
            || !string.IsNullOrWhiteSpace(conditionalMeasurementCode);
        var effectiveMinimumValue = minimumValue;
        var effectiveMaximumValue = maximumValue;
        ConsumptionRuleSizeClassDto? resolvedSizeClass = null;
        if (sizeClassId is not null)
        {
            resolvedSizeClass = await ReadSizeClassByIdAsync(connection, transaction, sizeClassId.Value, cancellationToken);
            effectiveMinimumValue ??= resolvedSizeClass?.MinimumValue;
            effectiveMaximumValue ??= resolvedSizeClass?.MaximumValue;
        }

        if (hasRangeContext && !effectiveMinimumValue.HasValue)
        {
            return "قيمة من مطلوبة.";
        }

        if (effectiveMinimumValue.HasValue && effectiveMaximumValue.HasValue && effectiveMinimumValue.Value >= effectiveMaximumValue.Value)
        {
            return "يجب أن تكون قيمة إلى أكبر من قيمة من.";
        }

        var range = await ResolveStructuredRangeFromContext(connection, transaction, productTypeId, sizeClassId, minimumValue, maximumValue, conditionalMeasurementCode, name, cancellationToken);
        if (range is not null)
        {
            if (range.Maximum.HasValue && range.Minimum >= range.Maximum.Value)
            {
                return "يجب أن تكون قيمة إلى أكبر من قيمة من.";
            }
        }

        if (minimumValue is not null && maximumValue is not null && minimumValue >= maximumValue)
        {
            return "يجب أن تكون قيمة إلى أكبر من قيمة من.";
        }

        var structuredRange = range ?? TryParseRange(name);
        if (structuredRange is null)
        {
            if (!string.IsNullOrWhiteSpace(conditionalMeasurementCode) || sizeClassId is not null || minimumValue is not null || maximumValue is not null)
            {
                return "يجب تحديد القياس الشرطي ونطاقه لهذه القاعدة.";
            }

            return null;
        }

        var activeRules = await ReadActiveRuleRangeCandidatesAsync(connection, transaction, productTypeId, cancellationToken);
        var targetMeasurementKey = conditionalMeasurementCode?.Trim()
            ?? resolvedSizeClass?.MeasurementCode?.Trim()
            ?? string.Empty;
        foreach (var existing in activeRules
                     .Where(r => r.RuleId != excludeRuleId)
                 .Where(r => string.Equals(r.MeasurementKey, targetMeasurementKey, StringComparison.OrdinalIgnoreCase)))
        {
            if (!existing.MinimumValue.HasValue)
            {
                continue;
            }

            var overlaps = ConsumptionRuleRangePolicy.Overlaps(
                existing.MinimumValue.Value,
                existing.MaximumValue,
                structuredRange.Minimum,
                structuredRange.Maximum);
            if (overlaps)
            {
                return $"تداخل فعلي في النطاق مع قاعدة موجودة: {existing.Name}.";
            }

        }

        return null;
    }

    private static async Task<List<ConsumptionRuleRangeCandidate>> ReadActiveRuleRangeCandidatesAsync(SqlConnection connection, SqlTransaction? transaction, int productTypeId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT cr.ConsumptionRuleId,
                   cr.ProductTypeId,
                   cr.FabricWidth,
                   cr.SizeClassId,
                   sc.MeasurementCode,
                   sc.MinimumValue,
                   sc.MaximumValue,
                   cr.Name,
                   cr.Priority,
                   cr.Status
            FROM dbo.PricingConsumptionRules cr WITH (UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.PricingSizeClasses sc WITH (NOLOCK) ON sc.SizeClassId = cr.SizeClassId
            WHERE cr.ProductTypeId = @ProductTypeId AND cr.IsActive = 1 AND cr.Status = N'Active'
            ORDER BY cr.Priority, cr.ConsumptionRuleId;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ProductTypeId", productTypeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleRangeCandidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ConsumptionRuleRangeCandidate(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.GetString(7),
                reader.GetInt32(8),
                reader.GetString(9)));
        }

        return items;
    }

    private static async Task<List<ConsumptionRuleDto>> ReadActiveRulesForProductAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT cr.ConsumptionRuleId,
                   cr.ProductTypeId,
                   cr.SizeClassId,
                   pt.NameAr AS ProductTypeName,
                   sc.NameAr AS SizeClassName,
                   cr.Name,
                   cr.RuleType,
                   cr.Formula,
                   cr.ResultUnit,
                   cr.Priority,
                   cr.Version,
                   cr.Status,
                   cr.IsActive,
                   cr.EffectiveFrom,
                   cr.EffectiveTo,
                   cr.FabricWidth,
                   cr.FabricWidthUnit,
                   sc.MeasurementCode,
                   sc.MinimumValue,
                   sc.MaximumValue
            FROM dbo.PricingConsumptionRules cr WITH (UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.PricingProductTypes pt WITH (NOLOCK) ON pt.ProductTypeId = cr.ProductTypeId
            LEFT JOIN dbo.PricingSizeClasses sc WITH (NOLOCK) ON sc.SizeClassId = cr.SizeClassId
            WHERE cr.ProductTypeId = @ProductTypeId AND cr.IsActive = 1
            ORDER BY cr.Priority, cr.ConsumptionRuleId;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ProductTypeId", productTypeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConsumptionRuleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ConsumptionRuleDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(11),
                reader.GetBoolean(12),
                reader.GetDateTime(13),
                reader.IsDBNull(14) ? null : reader.GetDateTime(14)));
        }

        return items;
    }

    private static async Task<ConsumptionRuleDto?> ReadRuleByIdAsync(SqlConnection connection, SqlTransaction transaction, int consumptionRuleId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT cr.ConsumptionRuleId,
                   cr.ProductTypeId,
                   cr.SizeClassId,
                   pt.NameAr AS ProductTypeName,
                   sc.NameAr AS SizeClassName,
                   cr.Name,
                   cr.RuleType,
                   cr.Formula,
                   cr.ResultUnit,
                   cr.Priority,
                   cr.Version,
                   cr.Status,
                   cr.IsActive,
                   cr.EffectiveFrom,
                   cr.EffectiveTo
            FROM dbo.PricingConsumptionRules cr WITH (UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.PricingProductTypes pt WITH (NOLOCK) ON pt.ProductTypeId = cr.ProductTypeId
            LEFT JOIN dbo.PricingSizeClasses sc WITH (NOLOCK) ON sc.SizeClassId = cr.SizeClassId
            WHERE cr.ConsumptionRuleId = @ConsumptionRuleId AND cr.IsActive = 1;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ConsumptionRuleId", consumptionRuleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ConsumptionRuleDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetString(11),
            reader.GetBoolean(12),
            reader.GetDateTime(13),
            reader.IsDBNull(14) ? null : reader.GetDateTime(14),
            reader.IsDBNull(15) ? null : reader.GetDecimal(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetDecimal(18),
            reader.IsDBNull(19) ? null : reader.GetDecimal(19));
    }

    private static IReadOnlyList<string> ExtractFormulaTokens(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return Array.Empty<string>();
        }

        var matches = Regex.Matches(formula, "[A-Za-z_][A-Za-z0-9_]*");
        var tokens = new List<string>();
        foreach (Match match in matches)
        {
            var token = match.Value.Trim();
            if (!string.IsNullOrEmpty(token) && !token.Equals("plus", StringComparison.OrdinalIgnoreCase) && !token.Equals("minus", StringComparison.OrdinalIgnoreCase) && !token.Equals("multiply", StringComparison.OrdinalIgnoreCase) && !token.Equals("div", StringComparison.OrdinalIgnoreCase) && !token.Equals("by", StringComparison.OrdinalIgnoreCase) && !token.Equals("and", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static RangeInfo? TryParseRange(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var numbers = Regex.Matches(value, "-?\\d+(?:\\.\\d+)?");
        if (numbers.Count < 2)
        {
            return null;
        }

        var all = numbers
            .Select(match => decimal.Parse(match.Value, CultureInfo.InvariantCulture))
            .OrderBy(x => x)
            .ToList();

        return new RangeInfo(all.First(), all.Last());
    }

    private static async Task<RangeInfo?> ResolveStructuredRangeFromContext(SqlConnection connection, SqlTransaction transaction, int productTypeId, int? sizeClassId, decimal? minimumValue, decimal? maximumValue, string? conditionalMeasurementCode, string? name, CancellationToken cancellationToken)
    {
        if (sizeClassId is not null)
        {
            var sizeClass = await ReadSizeClassByIdAsync(connection, transaction, sizeClassId.Value, cancellationToken);
            if (sizeClass is not null)
            {
                var min = minimumValue ?? sizeClass.MinimumValue;
                var max = maximumValue ?? sizeClass.MaximumValue;
                if (min is not null)
                {
                    return new RangeInfo(min.Value, max);
                }
            }
        }

        if (minimumValue is not null)
        {
            return new RangeInfo(minimumValue.Value, maximumValue);
        }

        if (!string.IsNullOrWhiteSpace(conditionalMeasurementCode))
        {
            return TryParseRange(name ?? conditionalMeasurementCode);
        }

        return TryParseRange(name ?? string.Empty);
    }

    private static async Task<ConsumptionRuleSizeClassDto?> ReadSizeClassByIdAsync(SqlConnection connection, SqlTransaction transaction, int sizeClassId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT SizeClassId, ProductTypeId, Code, NameAr, MeasurementCode, MinimumValue, MaximumValue, Sequence, Status, IsActive
            FROM dbo.PricingSizeClasses WITH (UPDLOCK,HOLDLOCK)
            WHERE SizeClassId = @SizeClassId AND IsActive = 1;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SizeClassId", sizeClassId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapSizeClass(reader);
    }

    private static async Task UpdateSizeClassValuesAsync(SqlConnection connection, SqlTransaction transaction, int sizeClassId, string name, string? conditionalMeasurementCode, decimal? minimumValue, decimal? maximumValue, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE dbo.PricingSizeClasses
            SET NameAr = @NameAr,
                MeasurementCode = @MeasurementCode,
                MinimumValue = @MinimumValue,
                MaximumValue = @MaximumValue,
                UpdatedAt = SYSDATETIME(),
                ChangeReason = N'Updated via consumption rules settings'
            WHERE SizeClassId = @SizeClassId AND IsActive = 1;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SizeClassId", sizeClassId);
        command.Parameters.AddWithValue("@NameAr", string.IsNullOrWhiteSpace(name) ? "قاعدة عامة" : name.Trim());
        command.Parameters.AddWithValue("@MeasurementCode", string.IsNullOrWhiteSpace(conditionalMeasurementCode) ? (object)DBNull.Value : conditionalMeasurementCode.Trim());
        command.Parameters.AddWithValue("@MinimumValue", minimumValue is null ? (object)DBNull.Value : minimumValue.Value);
        command.Parameters.AddWithValue("@MaximumValue", maximumValue is null ? (object)DBNull.Value : maximumValue.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ConsumptionRuleSizeClassDto?> FindExistingSizeClassAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, string code, int version, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT SizeClassId, ProductTypeId, Code, NameAr, MeasurementCode, MinimumValue, MaximumValue, Sequence, Status, IsActive
            FROM dbo.PricingSizeClasses WITH (UPDLOCK,HOLDLOCK)
            WHERE ProductTypeId = @ProductTypeId
              AND Code = @Code
              AND Version = @Version
              AND IsActive = 1;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ProductTypeId", productTypeId);
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@Version", version);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapSizeClass(reader);
    }

    private static string BuildStableSizeClassCode(int productTypeId, string name, string measurementCode, decimal? minimumValue, decimal? maximumValue)
    {
        var signature = $"{productTypeId}|{name}|{measurementCode}|{minimumValue?.ToString(CultureInfo.InvariantCulture) ?? ""}|{maximumValue?.ToString(CultureInfo.InvariantCulture) ?? ""}";
        var hash = Math.Abs(StringComparer.Ordinal.GetHashCode(signature)) % 1000000;
        return $"SZ{productTypeId}{hash:D6}";
    }

    private static async Task<ConsumptionRuleSizeClassDto> EnsureSizeClassAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, CreateConsumptionRuleDto rule, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(rule.Name) ? "قاعدة عامة" : rule.Name.Trim();
        var measurementCode = string.IsNullOrWhiteSpace(rule.ConditionalMeasurementCode) ? "GENERAL" : rule.ConditionalMeasurementCode.Trim();
        var minimumValue = rule.MinimumValue ?? 0m;
        var maximumValue = rule.MaximumValue ?? 0m;
        var version = 1;
        var code = BuildStableSizeClassCode(productTypeId, name, measurementCode, minimumValue == 0m ? null : minimumValue, maximumValue == 0m ? null : maximumValue);

        var existing = await FindExistingSizeClassAsync(connection, transaction, productTypeId, code, version, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        const string sql = @"
            INSERT INTO dbo.PricingSizeClasses
                (ProductTypeId, Code, NameAr, MeasurementCode, MinimumValue, MaximumValue, Sequence, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt)
            OUTPUT
                INSERTED.SizeClassId,
                INSERTED.ProductTypeId,
                INSERTED.Code,
                INSERTED.NameAr,
                INSERTED.MeasurementCode,
                INSERTED.MinimumValue,
                INSERTED.MaximumValue,
                INSERTED.Sequence,
                INSERTED.Status,
                INSERTED.IsActive
            VALUES (@ProductTypeId, @Code, @NameAr, @MeasurementCode, @MinimumValue, @MaximumValue, @Sequence, @Version, @Status, SYSDATETIME(), NULL, @IsActive, N'Created via consumption rules settings', N'LUMAR ERP', SYSDATETIME(), SYSDATETIME());";

        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@ProductTypeId", productTypeId);
            command.Parameters.AddWithValue("@Code", code);
            command.Parameters.AddWithValue("@NameAr", name);
            command.Parameters.AddWithValue("@MeasurementCode", measurementCode);
            command.Parameters.AddWithValue("@MinimumValue", minimumValue == 0m ? (object)DBNull.Value : minimumValue);
            command.Parameters.AddWithValue("@MaximumValue", maximumValue == 0m ? (object)DBNull.Value : maximumValue);
            command.Parameters.AddWithValue("@Sequence", 0);
            command.Parameters.AddWithValue("@Version", version);
            command.Parameters.AddWithValue("@Status", "Active");
            command.Parameters.AddWithValue("@IsActive", true);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("تعذر إنشاء فئة القاعدة المرتبطة بالنطاق.");
            }

            return MapSizeClass(reader);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            var recovered = await FindExistingSizeClassAsync(connection, transaction, productTypeId, code, version, cancellationToken);
            if (recovered is not null)
            {
                return recovered;
            }

            throw;
        }
    }

    private static async Task<ConsumptionRuleSizeClassDto?> ResolveSizeClassContextAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, CreateConsumptionRuleDto rule, CancellationToken cancellationToken)
    {
        if (rule.SizeClassId is not null)
        {
            return await ReadSizeClassByIdAsync(connection, transaction, rule.SizeClassId.Value, cancellationToken);
        }

        if (rule.MinimumValue is null && rule.MaximumValue is null && string.IsNullOrWhiteSpace(rule.ConditionalMeasurementCode))
        {
            return null;
        }

        return await EnsureSizeClassAsync(connection, transaction, productTypeId, rule, cancellationToken);
    }

    private static async Task<ConsumptionRuleDto?> InsertRuleAsync(SqlConnection connection, SqlTransaction transaction, CreateConsumptionRuleDto request, CancellationToken cancellationToken)
    {
        var sql = @"
            INSERT INTO dbo.PricingConsumptionRules
                (ProductTypeId, SizeClassId, FabricWidth, FabricWidthUnit, Name, RuleType, Formula, ResultUnit, Priority, Version, Status, EffectiveFrom, IsActive, ChangeReason, CreatedBy, CreatedAt)
            OUTPUT INSERTED.*
            VALUES (@ProductTypeId, @SizeClassId, @FabricWidth, @FabricWidthUnit, @Name, @RuleType, @Formula, @ResultUnit, @Priority, 1, @Status, SYSDATETIME(), 1, N'Created via consumption rules settings', N'LUMAR ERP', SYSDATETIME());";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ProductTypeId", request.ProductTypeId);
        command.Parameters.AddWithValue("@SizeClassId", request.SizeClassId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FabricWidth", request.FabricWidth ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FabricWidthUnit", string.IsNullOrWhiteSpace(request.FabricWidthUnit) ? (object)DBNull.Value : request.FabricWidthUnit.Trim());
        command.Parameters.AddWithValue("@Name", request.Name.Trim());
        command.Parameters.AddWithValue("@RuleType", request.RuleType.Trim());
        command.Parameters.AddWithValue("@Formula", request.Formula.Trim());
        command.Parameters.AddWithValue("@ResultUnit", request.ResultUnit.Trim());
        command.Parameters.AddWithValue("@Priority", request.Priority);
        command.Parameters.AddWithValue("@Status", request.Status.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapRule(reader);
    }

    private static string GenerateProductTypeCode(string name)
    {
        var normalized = Regex.Replace(name ?? string.Empty, @"[^\p{L}\p{Nd}]+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "PT";
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = new StringBuilder();

        foreach (var token in tokens)
        {
            if (initials.Length >= 4)
            {
                break;
            }

            var firstChar = token.FirstOrDefault(char.IsLetterOrDigit);
            if (firstChar == '\0')
            {
                continue;
            }

            initials.Append(char.ToUpperInvariant(firstChar));
        }

        if (initials.Length == 0)
        {
            return "PT";
        }

        return $"PT{initials}";
    }

    private static async Task<string> GenerateUniqueProductTypeCodeAsync(SqlConnection connection, SqlTransaction transaction, string name, CancellationToken cancellationToken)
    {
        var baseCode = GenerateProductTypeCode(name);
        return await EnsureUniqueProductTypeCodeAsync(connection, transaction, baseCode, cancellationToken);
    }

    private static async Task<string> EnsureUniqueProductTypeCodeAsync(SqlConnection connection, SqlTransaction transaction, string requestedCode, CancellationToken cancellationToken)
    {
        var candidate = string.IsNullOrWhiteSpace(requestedCode) ? "PT" : requestedCode.Trim();
        var suffix = 2;

        while (await ExistsAsync(connection, transaction, "SELECT TOP (1) 1 FROM dbo.PricingProductTypes WITH (UPDLOCK,HOLDLOCK) WHERE Code = @Code", new { Code = candidate }, cancellationToken))
        {
            candidate = $"{requestedCode.Trim()}{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string BuildMeasurementCode(string fieldName, int sequence)
    {
        var normalized = Regex.Replace(fieldName ?? string.Empty, @"[^\p{L}\p{Nd}]+", " ").Trim();
        var firstToken = string.IsNullOrWhiteSpace(normalized) ? "M" : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "M";
        var codeSeed = new string(firstToken.Where(char.IsLetterOrDigit).Take(3).ToArray());
        var safeCode = string.IsNullOrWhiteSpace(codeSeed) ? "M" : codeSeed;
        return $"{safeCode}{sequence:D2}";
    }

    private static async Task<string> GenerateUniqueMeasurementFieldCodeAsync(SqlConnection connection, SqlTransaction transaction, int measurementProfileId, string fieldName, int sequence, CancellationToken cancellationToken)
    {
        var baseCode = BuildMeasurementCode(fieldName, sequence);
        var candidate = baseCode;
        var suffix = 2;

        while (await ExistsAsync(connection, transaction,
                   "SELECT TOP (1) 1 FROM dbo.PricingMeasurementFields WITH (UPDLOCK,HOLDLOCK) WHERE MeasurementProfileId = @MeasurementProfileId AND Code = @Code",
                   new { MeasurementProfileId = measurementProfileId, Code = candidate },
                   cancellationToken))
        {
            candidate = $"{baseCode}{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static async Task SafeRollbackAsync(SqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            return;
        }

        if (transaction.Connection is null || transaction.Connection.State != ConnectionState.Open)
        {
            return;
        }

        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The transaction may already be committed or completed; this is safe to ignore.
        }
        catch (SqlException)
        {
            // The transaction may already be completed or the connection may be closed; ignore safely.
        }
    }

    private sealed record RangeInfo(decimal Minimum, decimal? Maximum);

    private static int GetRequiredInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            throw new SqlNullValueException($"Column '{columnName}' cannot be null.");
        }

        return reader.GetInt32(ordinal);
    }

    private static string GetRequiredString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            throw new SqlNullValueException($"Column '{columnName}' cannot be null.");
        }

        return reader.GetString(ordinal);
    }

    private static bool? GetNullableBoolean(SqlDataReader reader, string columnName)
    {
        if (!TryGetOrdinal(reader, columnName, out var ordinal))
        {
            return null;
        }

        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    private static int? GetNullableInt32(SqlDataReader reader, string columnName)
    {
        if (!TryGetOrdinal(reader, columnName, out var ordinal))
        {
            return null;
        }

        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal? GetNullableDecimal(SqlDataReader reader, string columnName)
    {
        if (!TryGetOrdinal(reader, columnName, out var ordinal))
        {
            return null;
        }

        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        if (!TryGetOrdinal(reader, columnName, out var ordinal))
        {
            return null;
        }

        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
    {
        if (!TryGetOrdinal(reader, columnName, out var ordinal))
        {
            return null;
        }

        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static bool TryGetOrdinal(SqlDataReader reader, string columnName, out int ordinal)
    {
        try
        {
            ordinal = reader.GetOrdinal(columnName);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }

    private static ConsumptionRuleDto MapRule(SqlDataReader reader)
    {
        var productTypeName = GetNullableString(reader, "ProductTypeName") ?? string.Empty;
        var sizeClassName = GetNullableString(reader, "SizeClassName") ?? string.Empty;
        var sizeClassId = GetNullableInt32(reader, "SizeClassId");
        var name = GetRequiredString(reader, "Name");
        var ruleType = GetRequiredString(reader, "RuleType");
        var formula = GetRequiredString(reader, "Formula");
        var resultUnit = GetRequiredString(reader, "ResultUnit");
        var priority = GetRequiredInt32(reader, "Priority");
        var version = GetRequiredInt32(reader, "Version");
        var status = GetRequiredString(reader, "Status");
        var isActive = GetNullableBoolean(reader, "IsActive") ?? true;
        var effectiveFrom = GetRequiredDateTime(reader, "EffectiveFrom");
        var effectiveTo = GetNullableDateTime(reader, "EffectiveTo");
        var fabricWidth = GetNullableDecimal(reader, "FabricWidth");
        var fabricWidthUnit = GetNullableString(reader, "FabricWidthUnit");
        var measurementCode = GetNullableString(reader, "MeasurementCode");
        var minimumValue = GetNullableDecimal(reader, "MinimumValue");
        var maximumValue = GetNullableDecimal(reader, "MaximumValue");
        var consumptionRuleId = GetRequiredInt32(reader, "ConsumptionRuleId");
        var productTypeId = GetRequiredInt32(reader, "ProductTypeId");

        return new ConsumptionRuleDto(
            consumptionRuleId,
            productTypeId,
            sizeClassId,
            productTypeName,
            sizeClassName,
            name,
            ruleType,
            formula,
            resultUnit,
            priority,
            version,
            status,
            isActive,
            effectiveFrom,
            effectiveTo,
            fabricWidth,
            fabricWidthUnit,
            measurementCode,
            minimumValue,
            maximumValue);
    }

    private static DateTime GetRequiredDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            throw new SqlNullValueException($"Column '{columnName}' cannot be null.");
        }

        return reader.GetDateTime(ordinal);
    }
}
