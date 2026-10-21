using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class CustomerRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : ICustomerRepository
{
    public Task<IReadOnlyList<CustomerListDto>> GetListAsync(CancellationToken cancellationToken) => QueryAsync("SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, RelationshipType FROM dbo.Customers ORDER BY CustomerName, CustomerID", reader => new CustomerListDto(reader.GetInt32(0), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.NullableDecimal("TotalPoints"), reader.NullableInt32("TotalPieces"), reader.NullableDecimal("TotalDebts"), reader.NullableString("RelationshipType")), null, cancellationToken);

    public async Task<CustomerDetailsDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken)
    {
        var results = await QueryAsync("SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber, p.CustomerCode AS ParentCustomerCode, c.ParentCustomerId, p.CustomerName AS ParentCustomerName, c.Address, c.Notes, c.IsActive, c.TotalPoints, c.TotalPieces, c.TotalDebts, c.RelationshipType FROM dbo.Customers c LEFT JOIN dbo.Customers p ON p.CustomerID = c.ParentCustomerId WHERE c.CustomerID = @customerId", MapDetails, customerId, cancellationToken);
        return results.SingleOrDefault();
    }

    public Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurementsAsync(int customerId, CancellationToken cancellationToken) => QueryAsync("SELECT Id, CustomerId, PieceType, MeasurementName, MeasurementValue, CreatedAtUtc, RevisionNumber FROM dbo.CustomerMeasurements WHERE CustomerId = @customerId ORDER BY CreatedAtUtc DESC, Id DESC", reader => new CustomerMeasurementDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDateTime(5), reader.GetInt32(6)), customerId, cancellationToken);

    public async Task<IReadOnlyList<CustomerMeasurementDto>?> UpsertMeasurementsAsync(int customerId, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;
        try
        {
            await using (var customerCommand = new SqlCommand("SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE CustomerID=@customerId", connection, transaction))
            {
                customerCommand.Parameters.AddWithValue("@customerId", customerId);
                if (await customerCommand.ExecuteScalarAsync(cancellationToken) is null) return null;
            }

            const string sql = "DECLARE @revision int = ISNULL((SELECT MAX(RevisionNumber) FROM dbo.CustomerMeasurements WITH (UPDLOCK, HOLDLOCK) WHERE CustomerId=@customerId AND PieceType=@pieceType AND MeasurementName=@name), 0) + 1; INSERT INTO dbo.CustomerMeasurements (CustomerId, PieceType, MeasurementName, MeasurementValue, RevisionNumber) OUTPUT INSERTED.Id, INSERTED.CustomerId, INSERTED.PieceType, INSERTED.MeasurementName, INSERTED.MeasurementValue, INSERTED.CreatedAtUtc, INSERTED.RevisionNumber VALUES (@customerId, @pieceType, @name, @value, @revision);";
            var saved = new List<CustomerMeasurementDto>(measurements.Measurements.Count);
            foreach (var measurement in measurements.Measurements)
            {
                await using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@pieceType", measurements.PieceType!.Trim());
                command.Parameters.AddWithValue("@name", measurement.MeasurementName!.Trim());
                command.Parameters.AddWithValue("@value", measurement.MeasurementValue);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                saved.Add(new CustomerMeasurementDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDateTime(5), reader.GetInt32(6)));
            }
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return saved;
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken cancellationToken) => QueryAsync("SELECT CustomerLedgerEntryId, CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt FROM dbo.CustomerLedgerEntries WHERE CustomerID = @customerId ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC", reader => new CustomerLedgerEntryDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDateTime(6)), customerId, cancellationToken);

    public async Task<CustomerLoyaltyDto?> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT la.LoyaltyAccountId, la.CustomerId, la.CurrentPoints, la.LifetimeEarnedPoints, la.LifetimeRedeemedPoints, la.PendingExpirePoints, la.VipLevelId, vl.Code AS VipLevelCode, vl.DisplayName AS VipLevelDisplayName, la.CreatedAt, la.UpdatedAt, la.LastActivityAt, la.LoyaltyAccountStatus, la.WarningStartedAtUtc, la.FrozenAtUtc, la.ReactivatedAtUtc, la.FreezeReason, la.LastQualifyingActivityAtUtc FROM dbo.LoyaltyAccounts la LEFT JOIN dbo.VipLevels vl ON vl.VipLevelId = la.VipLevelId WHERE la.CustomerId = @customerId";
        var results = await QueryAsync(sql, reader => new CustomerLoyaltyDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.NullableInt32("VipLevelId"), reader.NullableString("VipLevelCode"), reader.NullableString("VipLevelDisplayName"), reader.GetDateTime(9), reader.GetDateTime(10), reader.NullableDateTime("LastActivityAt"), reader.GetString(12), reader.NullableDateTime("WarningStartedAtUtc"), reader.NullableDateTime("FrozenAtUtc"), reader.NullableDateTime("ReactivatedAtUtc"), reader.NullableString("FreezeReason"), reader.NullableDateTime("LastQualifyingActivityAtUtc")), customerId, cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<CustomerReferralDto?> GetReferralsAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT ra.ReferralAccountId, ra.CustomerId, ra.ReferralCodeId, rc.Code AS ReferralCode, rc.IsActive AS ReferralCodeIsActive, ra.TotalReferrals, ra.SuccessfulReferrals, ra.TotalRewardsAmount, ra.TotalRewardPoints, ra.CreatedAt, ra.UpdatedAt, rc.CreatedAt AS ReferralCodeCreatedAt, rc.LastUsedAt AS ReferralCodeLastUsedAt FROM dbo.ReferralAccounts ra LEFT JOIN dbo.ReferralCodes rc ON rc.ReferralCodeId = ra.ReferralCodeId WHERE ra.CustomerId = @customerId";
        var results = await QueryAsync(sql, reader => new CustomerReferralDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableInt32("ReferralCodeId"), reader.NullableString("ReferralCode"), reader.NullableBoolean("ReferralCodeIsActive"), reader.GetInt32(5), reader.GetInt32(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDateTime(9), reader.GetDateTime(10), reader.NullableDateTime("ReferralCodeCreatedAt"), reader.NullableDateTime("ReferralCodeLastUsedAt")), customerId, cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<CustomerDetailsDto> CreateAsync(CreateCustomerDto customer, CancellationToken cancellationToken)
    {
        var parentId = await ResolveParentIdAsync(customer.ParentCustomerId, customer.ReferralCode, cancellationToken);
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;
        try
        {
            var name = NormalizeCustomerName(customer.CustomerName!);
            var phone = NormalizeCustomerPhone(customer.PhoneNumber);
            await EnsureNoDuplicateCustomerAsync(name, phone, connection, (SqlTransaction)transaction, cancellationToken);
            const string sql = "INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, ParentCustomerId, RelationshipType) OUTPUT INSERTED.CustomerID VALUES (@code, @name, @phone, @address, @notes, @isActive, @parentId, @relationshipType)";
            await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@code", customer.CustomerCode!.Trim());
            command.Parameters.AddWithValue("@name", name);
            AddNullable(command, "@phone", phone);
            AddNullable(command, "@address", customer.Address);
            AddNullable(command, "@notes", customer.Notes);
            command.Parameters.AddWithValue("@isActive", customer.IsActive);
            AddNullable(command, "@parentId", parentId);
            AddNullable(command, "@relationshipType", customer.RelationshipType);
            var customerId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return (await GetByIdAsync(customerId, cancellationToken))!;
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public async Task<CustomerCreationResultDto> CreateWithReferralAsync(CreateCustomerWithReferralDto customer, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;
        try
        {
            var created = await CreateWithReferralInTransactionAsync(connection, transaction, customer, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return created;
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public async Task<CustomerCreationResultDto> CreateWithReferralInTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CreateCustomerWithReferralDto customer,
        CancellationToken cancellationToken)
    {
        var phone = NormalizeCustomerPhone(customer.PhoneNumber)!;
        var name = NormalizeCustomerName(customer.CustomerName!);
        var relationship = customer.RelationshipType?.Trim();

        await EnsureNoDuplicateCustomerAsync(name, phone, connection, transaction, cancellationToken);
        CustomerReferralCandidateDto? referrer = null;
        ReferralCodeRow? referrerCode = null;
        if (customer.ReferrerCustomerId.HasValue)
        {
            referrer = await GetReferralCandidateAsync(customer.ReferrerCustomerId.Value, connection, transaction, cancellationToken)
                ?? throw new ArgumentException("المحيل المحدد غير موجود أو غير فعال.");
            referrerCode = await EnsureReferralCodeAsync(referrer.CustomerId, connection, transaction, cancellationToken);
            referrer = referrer with { ReferralCode = referrerCode.Code };
        }

        var customerPrefix = await SystemCodeGenerator.ResolvePrefixAsync(
            connection,
            transaction,
            "CustomerCodePrefix",
            "C",
            cancellationToken);
        var customerNumber = await SystemCodeGenerator.GetNextNumberAsync(
            connection,
            transaction,
            "dbo.Customers",
            "CustomerCode",
            "CustomerCodePrefix",
            "C",
            cancellationToken);
        var customerCode = customerPrefix + customerNumber.ToString(CultureInfo.InvariantCulture);

        const string customerSql = @"INSERT INTO dbo.Customers
                (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, ParentCustomerId, RelationshipType)
                OUTPUT INSERTED.CustomerID
                VALUES (@customerCode, @customerName, @phoneNumber, @address, @notes, 1, @parentCustomerId, @relationshipType);";
        await using var customerCommand = new SqlCommand(customerSql, connection, transaction);
        customerCommand.Parameters.AddWithValue("@customerCode", customerCode);
        customerCommand.Parameters.AddWithValue("@customerName", name);
        customerCommand.Parameters.AddWithValue("@phoneNumber", phone);
        AddNullable(customerCommand, "@address", customer.Address?.Trim());
        AddNullable(customerCommand, "@notes", customer.Notes?.Trim());
        AddNullable(customerCommand, "@parentCustomerId", customer.ReferrerCustomerId);
        AddNullable(customerCommand, "@relationshipType", relationship);
        var customerId = Convert.ToInt32(await customerCommand.ExecuteScalarAsync(cancellationToken));

        var customerReferralCode = await EnsureReferralCodeAsync(customerId, connection, transaction, cancellationToken);
        await InsertReferralAccountAsync(customerId, customerReferralCode.ReferralCodeId, connection, transaction, cancellationToken);

        long? registrationTransactionId = null;
        if (referrer is not null && referrerCode is not null)
        {
            var registrationNotes = string.IsNullOrWhiteSpace(relationship)
                ? "تسجيل عميل جديد."
                : $"تسجيل عميل جديد؛ صلة القرابة: {relationship}";
            registrationTransactionId = await InsertRegistrationAsync(
                referrer.CustomerId,
                customerId,
                referrerCode.ReferralCodeId,
                registrationNotes,
                connection,
                transaction,
                cancellationToken);
            await IncrementReferralAccountAsync(
                referrer.CustomerId,
                referrerCode.ReferralCodeId,
                connection,
                transaction,
                cancellationToken);
            await UpdateReferralCodeLastUsedAsync(
                referrerCode.ReferralCodeId,
                connection,
                transaction,
                cancellationToken);
        }

        return await ReadCreationResultAsync(
            customerId,
            registrationTransactionId,
            connection,
            transaction,
            cancellationToken);
    }

    public async Task<CustomerDetailsDto?> UpdateAsync(int customerId, UpdateCustomerDto customer, CancellationToken cancellationToken)
    {
        if (await GetByIdAsync(customerId, cancellationToken) is null) return null;
        if (customer.ParentCustomerId == customerId) throw new ArgumentException("A customer cannot refer to itself.");
        if (customer.ParentCustomerId.HasValue && await WouldCreateCycleAsync(customerId, customer.ParentCustomerId.Value, cancellationToken)) throw new ArgumentException("The referral parent would create a cycle.");
        if (customer.ParentCustomerId.HasValue && await GetByIdAsync(customer.ParentCustomerId.Value, cancellationToken) is null) throw new ArgumentException("The referral parent does not exist.");
        const string sql = "UPDATE dbo.Customers SET CustomerCode=@code, CustomerName=@name, PhoneNumber=@phone, Address=@address, Notes=@notes, IsActive=@isActive, ParentCustomerId=@parentId, RelationshipType=@relationshipType WHERE CustomerID=@id";
        await using var connection = operationalConnections.Create(); await connection.OpenAsync(cancellationToken); await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", customerId); command.Parameters.AddWithValue("@code", customer.CustomerCode!.Trim()); command.Parameters.AddWithValue("@name", customer.CustomerName!.Trim()); AddNullable(command, "@phone", customer.PhoneNumber); AddNullable(command, "@address", customer.Address); AddNullable(command, "@notes", customer.Notes); command.Parameters.AddWithValue("@isActive", customer.IsActive); AddNullable(command, "@parentId", customer.ParentCustomerId); AddNullable(command, "@relationshipType", customer.RelationshipType);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetByIdAsync(customerId, cancellationToken);
    }

    public Task<IReadOnlyList<CustomerListDto>> SearchAsync(string term, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber,
                   la.CurrentPoints AS TotalPoints, c.TotalPieces, ledger.BalanceAfterTransaction AS TotalDebts, c.RelationshipType
            FROM dbo.Customers c
            LEFT JOIN dbo.Customers p ON p.CustomerID = c.ParentCustomerId
            LEFT JOIN dbo.LoyaltyAccounts la ON la.CustomerId = c.CustomerID
            OUTER APPLY (
                SELECT TOP (1) cle.BalanceAfterTransaction
                FROM dbo.CustomerLedgerEntries cle
                WHERE cle.CustomerID = c.CustomerID
                ORDER BY cle.CreatedAt DESC, cle.CustomerLedgerEntryId DESC
            ) ledger
            OUTER APPLY (
                SELECT TOP (1) rc.Code AS ReferralCode
                FROM dbo.ReferralCodes rc
                WHERE rc.CustomerId = c.CustomerID
                ORDER BY rc.IsActive DESC, rc.CreatedAt DESC, rc.ReferralCodeId DESC
            ) referral
            CROSS APPLY (VALUES (
                LOWER(COALESCE(c.CustomerCode, N'')),
                LOWER(COALESCE(c.CustomerName, N'')),
                LOWER(COALESCE(c.PhoneNumber, N'')),
                LOWER(COALESCE(p.CustomerCode, N'')),
                LOWER(COALESCE(referral.ReferralCode, N''))
            )) searchFields(CustomerCode, CustomerName, PhoneNumber, ParentCustomerCode, ReferralCode)
            WHERE searchFields.CustomerCode LIKE @term
               OR searchFields.CustomerName LIKE @term
               OR searchFields.PhoneNumber LIKE @term
               OR searchFields.ParentCustomerCode LIKE @term
               OR searchFields.ReferralCode LIKE @term
               OR EXISTS (
                    SELECT 1
                    FROM STRING_SPLIT(@normalizedTerm, N' ') words
                    WHERE NULLIF(LTRIM(RTRIM(words.value)), N'') IS NOT NULL
                      AND (
                           searchFields.CustomerCode LIKE N'%' + LOWER(words.value) + N'%'
                        OR searchFields.CustomerName LIKE N'%' + LOWER(words.value) + N'%'
                        OR searchFields.PhoneNumber LIKE N'%' + LOWER(words.value) + N'%'
                        OR searchFields.ParentCustomerCode LIKE N'%' + LOWER(words.value) + N'%'
                        OR searchFields.ReferralCode LIKE N'%' + LOWER(words.value) + N'%'
                      )
               )
            ORDER BY
                CASE
                    WHEN searchFields.CustomerCode = @normalizedTerm
                      OR searchFields.CustomerName = @normalizedTerm
                      OR searchFields.PhoneNumber = @normalizedTerm
                      OR searchFields.ParentCustomerCode = @normalizedTerm
                      OR searchFields.ReferralCode = @normalizedTerm THEN 0
                    WHEN searchFields.CustomerCode LIKE @prefixTerm
                      OR searchFields.CustomerName LIKE @prefixTerm
                      OR searchFields.PhoneNumber LIKE @prefixTerm
                      OR searchFields.ParentCustomerCode LIKE @prefixTerm
                      OR searchFields.ReferralCode LIKE @prefixTerm THEN 1
                    WHEN NOT EXISTS (
                        SELECT 1
                        FROM STRING_SPLIT(@normalizedTerm, N' ') words
                        WHERE NULLIF(LTRIM(RTRIM(words.value)), N'') IS NOT NULL
                          AND NOT (
                               searchFields.CustomerCode LIKE N'%' + LOWER(words.value) + N'%'
                            OR searchFields.CustomerName LIKE N'%' + LOWER(words.value) + N'%'
                            OR searchFields.PhoneNumber LIKE N'%' + LOWER(words.value) + N'%'
                            OR searchFields.ParentCustomerCode LIKE N'%' + LOWER(words.value) + N'%'
                            OR searchFields.ReferralCode LIKE N'%' + LOWER(words.value) + N'%'
                          )
                    ) THEN 2
                    ELSE 3
                END,
                c.CustomerName,
                c.CustomerID;
            """;

        return QuerySearchAsync(sql, term, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerReferralCandidateDto>> SearchReferralCandidatesAsync(string term, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber, referral.Code AS ReferralCode
            FROM dbo.Customers c
            OUTER APPLY (
                SELECT TOP (1) rc.Code
                FROM dbo.ReferralCodes rc
                WHERE rc.CustomerId = c.CustomerID AND rc.IsActive = 1
                ORDER BY rc.CreatedAt DESC, rc.ReferralCodeId DESC
            ) referral
            WHERE c.IsActive = 1
              AND (c.CustomerCode LIKE @term OR c.CustomerName LIKE @term OR c.PhoneNumber LIKE @term OR referral.Code LIKE @term)
            ORDER BY c.CustomerName, c.CustomerID;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@term", $"%{term.Trim()}%");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<CustomerReferralCandidateDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CustomerReferralCandidateDto(
                reader.GetInt32(0),
                reader.NullableString("CustomerCode"),
                reader.NullableString("CustomerName"),
                reader.NullableString("PhoneNumber"),
                reader.NullableString("ReferralCode")));
        }

        return results;
    }

    public async Task<IReadOnlyList<LinkedCustomerAccessDto>> GetLinkedCustomersAsync(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
            return [];

        const string sql = @"
            SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.RelationshipType,
                   CASE WHEN ma.CustomerId = c.CustomerID THEN N'Primary' ELSE N'Managed' END AS AccessRole,
                   c.IsActive
            FROM dbo.MobileAccountCustomerAccess mac
            JOIN dbo.MobileAccounts ma ON ma.MobileAccountId = mac.MobileAccountId
            JOIN dbo.Customers c ON c.CustomerID = mac.CustomerId
            WHERE (ma.Username = @username OR ma.NormalizedUsername = @username)
              AND mac.AccessRole = CASE WHEN ma.CustomerId = c.CustomerID THEN N'Primary' ELSE N'Managed' END
              AND mac.IsActive = 1
              AND (mac.RevokedAtUtc IS NULL OR mac.RevokedAtUtc > SYSUTCDATETIME())
            ORDER BY CASE WHEN ma.CustomerId = c.CustomerID THEN 0 ELSE 1 END, c.CustomerName;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username.Trim());

        var results = new List<LinkedCustomerAccessDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new LinkedCustomerAccessDto(
                reader.GetInt32(0),
                reader.NullableString("CustomerCode"),
                reader.NullableString("CustomerName"),
                reader.NullableString("RelationshipType"),
                reader.GetString(4),
                reader.GetBoolean(5)));
        }

        return results;
    }

    public async Task<LinkedCustomerAccessDto> CreateManagedCustomerAsync(string username, CreateManagedCustomerDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("المستخدم الحالي غير مصرح له بإضافة فرد العائلة.");

        if (request is null)
            throw new ArgumentException("بيانات الفرد غير صالحة.");

        if (string.IsNullOrWhiteSpace(request.CustomerName) && string.IsNullOrWhiteSpace(request.CustomerCode))
            throw new ArgumentException("اسم الفرد أو كود العميل مطلوب.");

        if (string.IsNullOrWhiteSpace(request.PhoneNumber) && string.IsNullOrWhiteSpace(request.CustomerCode))
            throw new ArgumentException("رقم التواصل أو كود العميل مطلوب.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            var normalizedUsername = username.Trim();
            await using (var accountCommand = new SqlCommand(
                "SELECT TOP (1) MobileAccountId, AccountType, CustomerId FROM dbo.MobileAccounts WITH (UPDLOCK, HOLDLOCK) WHERE (Username = @username OR NormalizedUsername = @username OR NormalizedUsername = @normalizedUsername) AND IsActive = 1 ORDER BY MobileAccountId",
                connection,
                transaction))
            {
                accountCommand.Parameters.AddWithValue("@username", normalizedUsername);
                accountCommand.Parameters.AddWithValue("@normalizedUsername", normalizedUsername.ToUpperInvariant());
                await using var accountReader = await accountCommand.ExecuteReaderAsync(cancellationToken);
                if (!await accountReader.ReadAsync(cancellationToken))
                    throw new UnauthorizedAccessException("لا توجد صلاحية لإنشاء فرد العائلة.");

                var accountType = accountReader.IsDBNull(1) ? null : accountReader.GetString(1);
                if (!string.Equals(accountType, "Customer", StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("لا توجد صلاحية لإنشاء فرد العائلة.");

                var currentUser = new { MobileAccountId = accountReader.GetInt32(0), CustomerId = accountReader.IsDBNull(2) ? (int?)null : accountReader.GetInt32(2) };
                await accountReader.CloseAsync();
                if (currentUser.CustomerId is null)
                    throw new UnauthorizedAccessException("لا توجد صلاحية لإنشاء فرد العائلة.");

                var customerName = NormalizeCustomerName(request.CustomerName);
                var phoneNumber = NormalizeCustomerPhone(request.PhoneNumber);
                var relationshipType = string.IsNullOrWhiteSpace(request.RelationshipType) ? "Family" : request.RelationshipType.Trim();

                var existingCustomer = await ResolveExistingCustomerForFamilyAsync(connection, transaction, request.CustomerCode, customerName, phoneNumber, cancellationToken);
                if (existingCustomer is not null)
                {
                    var accessExists = await ExistsManagedAccessAsync(connection, transaction, currentUser.MobileAccountId, existingCustomer.CustomerId, cancellationToken);
                    if (accessExists)
                    {
                        var currentAccess = await GetExistingManagedAccessAsync(connection, transaction, currentUser.MobileAccountId, existingCustomer.CustomerId, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        committed = true;
                        return new LinkedCustomerAccessDto(existingCustomer.CustomerId, existingCustomer.CustomerCode, existingCustomer.CustomerName, currentAccess?.RelationshipType ?? relationshipType, "Managed", currentAccess?.IsActive ?? true);
                    }

                    await using (var insertAccess = new SqlCommand(@"INSERT INTO dbo.MobileAccountCustomerAccess (MobileAccountId, CustomerId, AccessRole, IsActive, RelationshipType, CreatedAtUtc) VALUES (@mobileAccountId, @customerId, N'Managed', 1, @relationshipType, SYSUTCDATETIME());", connection, transaction))
                    {
                        insertAccess.Parameters.AddWithValue("@mobileAccountId", currentUser.MobileAccountId);
                        insertAccess.Parameters.AddWithValue("@customerId", existingCustomer.CustomerId);
                        insertAccess.Parameters.AddWithValue("@relationshipType", relationshipType);
                        await insertAccess.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    committed = true;
                    return new LinkedCustomerAccessDto(existingCustomer.CustomerId, existingCustomer.CustomerCode, existingCustomer.CustomerName, relationshipType, "Managed", true);
                }

                var customerPrefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "CustomerCodePrefix", "C", cancellationToken);
                var customerNumber = await SystemCodeGenerator.GetNextNumberAsync(connection, transaction, "dbo.Customers", "CustomerCode", "CustomerCodePrefix", "C", cancellationToken);
                var customerCode = customerPrefix + customerNumber.ToString(CultureInfo.InvariantCulture);

                const string insertCustomerSql = @"INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, ParentCustomerId, RelationshipType)
                    OUTPUT INSERTED.CustomerID, INSERTED.CustomerCode, INSERTED.CustomerName, INSERTED.PhoneNumber, INSERTED.RelationshipType
                    VALUES (@customerCode, @customerName, @phoneNumber, @address, @notes, 1, @parentCustomerId, @relationshipType);";

                await using var createCustomer = new SqlCommand(insertCustomerSql, connection, transaction);
                createCustomer.Parameters.AddWithValue("@customerCode", customerCode);
                createCustomer.Parameters.AddWithValue("@customerName", customerName);
                AddNullable(createCustomer, "@phoneNumber", phoneNumber);
                AddNullable(createCustomer, "@address", string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim());
                AddNullable(createCustomer, "@notes", string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());
                createCustomer.Parameters.AddWithValue("@parentCustomerId", DBNull.Value);
                createCustomer.Parameters.AddWithValue("@relationshipType", relationshipType);

                await using var reader = await createCustomer.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("تعذر إنشاء العميل الرسمي للفرد.");

                var customerId = reader.GetInt32(0);
                var createdCode = reader.IsDBNull(1) ? null : reader.GetString(1);
                var createdName = reader.IsDBNull(2) ? null : reader.GetString(2);
                await reader.CloseAsync();

                var primaryCustomerId = currentUser.CustomerId.Value;
                var referrerCode = await EnsureReferralCodeAsync(primaryCustomerId, connection, transaction, cancellationToken);
                await InsertReferralAccountAsync(customerId, referrerCode.ReferralCodeId, connection, transaction, cancellationToken);
                var registrationId = await InsertRegistrationAsync(primaryCustomerId, customerId, referrerCode.ReferralCodeId, $"إضافة فرد عائلة جديد؛ صلة القرابة: {relationshipType}", connection, transaction, cancellationToken);
                await IncrementReferralAccountAsync(primaryCustomerId, referrerCode.ReferralCodeId, connection, transaction, cancellationToken);
                await UpdateReferralCodeLastUsedAsync(referrerCode.ReferralCodeId, connection, transaction, cancellationToken);

                await using (var insertAccess = new SqlCommand(@"INSERT INTO dbo.MobileAccountCustomerAccess (MobileAccountId, CustomerId, AccessRole, IsActive, RelationshipType, CreatedAtUtc) VALUES (@mobileAccountId, @customerId, N'Managed', 1, @relationshipType, SYSUTCDATETIME());", connection, transaction))
                {
                    insertAccess.Parameters.AddWithValue("@mobileAccountId", currentUser.MobileAccountId);
                    insertAccess.Parameters.AddWithValue("@customerId", customerId);
                    insertAccess.Parameters.AddWithValue("@relationshipType", relationshipType);
                    await insertAccess.ExecuteNonQueryAsync(cancellationToken);
                }

                await accountReader.CloseAsync();
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return new LinkedCustomerAccessDto(customerId, createdCode, createdName, relationshipType, "Managed", true);
            }
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public async Task<ReferrerLookupResultDto?> ResolveReferrerAsync(ReferrerLookupRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return null;

        if (string.IsNullOrWhiteSpace(request.CustomerCode) && (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber)))
            return null;

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            const string sql = @"SELECT CustomerID, CustomerCode, CustomerName FROM dbo.Customers WHERE CustomerCode = @customerCode AND IsActive = 1;";
            await using var codeCommand = new SqlCommand(sql, connection);
            codeCommand.Parameters.AddWithValue("@customerCode", request.CustomerCode.Trim());
            await using var codeReader = await codeCommand.ExecuteReaderAsync(cancellationToken);
            if (!await codeReader.ReadAsync(cancellationToken))
                return null;

            return new ReferrerLookupResultDto(codeReader.GetInt32(0), codeReader.GetString(1), codeReader.GetString(2));
        }

        var normalizedName = NormalizeCustomerName(request.CustomerName!);
        var normalizedPhone = NormalizeCustomerPhone(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(normalizedPhone))
            return null;

        const string namePhoneSql = @"SELECT CustomerID, CustomerCode, CustomerName FROM dbo.Customers WHERE CustomerName = @customerName AND PhoneNumber = @phoneNumber AND IsActive = 1 ORDER BY CustomerID;";
        await using var namePhoneCommand = new SqlCommand(namePhoneSql, connection);
        namePhoneCommand.Parameters.AddWithValue("@customerName", normalizedName);
        namePhoneCommand.Parameters.AddWithValue("@phoneNumber", normalizedPhone);
        await using var namePhoneReader = await namePhoneCommand.ExecuteReaderAsync(cancellationToken);

        var matches = new List<ReferrerLookupResultDto>();
        while (await namePhoneReader.ReadAsync(cancellationToken))
        {
            matches.Add(new ReferrerLookupResultDto(namePhoneReader.GetInt32(0), namePhoneReader.GetString(1), namePhoneReader.GetString(2)));
        }

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException("Multiple active customers matched the supplied name and phone number. Use CustomerCode.")
        };
    }

    private static async Task<CustomerDetailsDto?> ResolveExistingCustomerForFamilyAsync(SqlConnection connection, SqlTransaction transaction, string? customerCode, string? customerName, string? phoneNumber, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(customerCode))
        {
            const string sql = "SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, Address, Notes, RelationshipType FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE CustomerCode = @customerCode AND IsActive = 1;";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@customerCode", customerCode.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new CustomerDetailsDto(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.NullableString("PhoneNumber"),
                    null,
                    null,
                    null,
                    reader.NullableString("Address"),
                    reader.NullableString("Notes"),
                    true,
                    null,
                    null,
                    null,
                    reader.NullableString("RelationshipType"));
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(customerName) && !string.IsNullOrWhiteSpace(phoneNumber))
        {
            const string sql = "SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, Address, Notes, RelationshipType FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE CustomerName = @customerName AND PhoneNumber = @phoneNumber AND IsActive = 1 ORDER BY CustomerID;";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@customerName", customerName);
            command.Parameters.AddWithValue("@phoneNumber", phoneNumber);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var matches = new List<CustomerDetailsDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(new CustomerDetailsDto(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.NullableString("PhoneNumber"),
                    null,
                    null,
                    null,
                    reader.NullableString("Address"),
                    reader.NullableString("Notes"),
                    true,
                    null,
                    null,
                    null,
                    reader.NullableString("RelationshipType")));
            }

            if (matches.Count > 1)
                throw new InvalidOperationException("تعدد العملاء المطابقين، استخدم الكود الرسمي لتحديد العميل." );

            return matches.Count == 1 ? matches[0] : null;
        }

        return null;
    }

    private static async Task<bool> ExistsManagedAccessAsync(SqlConnection connection, SqlTransaction transaction, int mobileAccountId, int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.MobileAccountCustomerAccess WITH (UPDLOCK, HOLDLOCK) WHERE MobileAccountId = @mobileAccountId AND CustomerId = @customerId AND AccessRole = N'Managed' AND IsActive = 1 AND (RevokedAtUtc IS NULL OR RevokedAtUtc > SYSUTCDATETIME())) THEN 1 ELSE 0 END;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        command.Parameters.AddWithValue("@customerId", customerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<(string? RelationshipType, bool IsActive)?> GetExistingManagedAccessAsync(SqlConnection connection, SqlTransaction transaction, int mobileAccountId, int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT RelationshipType, IsActive FROM dbo.MobileAccountCustomerAccess WITH (UPDLOCK, HOLDLOCK) WHERE MobileAccountId = @mobileAccountId AND CustomerId = @customerId AND AccessRole = N'Managed' ORDER BY CreatedAtUtc DESC;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return (reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetBoolean(1));
    }

    private static string? NormalizeCustomerPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = Regex.Replace(value.Trim(), @"[\s\-()]+", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return null;

        if (digits.StartsWith("00967", StringComparison.Ordinal))
            return digits.Substring(5);

        if (digits.StartsWith("967", StringComparison.Ordinal))
            return digits.Substring(3);

        if (digits.StartsWith("+967", StringComparison.Ordinal))
            return digits.Substring(4);

        return digits;
    }

    public Task<IReadOnlyList<CustomerDetailsDto>> GetAncestorsAsync(int customerId, CancellationToken cancellationToken) => QueryAsync("WITH Ancestors AS (SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, ParentCustomerId, Address, Notes, IsActive, TotalPoints, TotalPieces, TotalDebts, RelationshipType, 0 AS Depth FROM dbo.Customers WHERE CustomerID=@customerId UNION ALL SELECT p.CustomerID,p.CustomerCode,p.CustomerName,p.PhoneNumber,p.ParentCustomerId,p.Address,p.Notes,p.IsActive,p.TotalPoints,p.TotalPieces,p.TotalDebts,p.RelationshipType,a.Depth+1 FROM dbo.Customers p JOIN Ancestors a ON a.ParentCustomerId=p.CustomerID) SELECT a.CustomerID,a.CustomerCode,a.CustomerName,a.PhoneNumber,p.CustomerCode AS ParentCustomerCode,a.ParentCustomerId,p.CustomerName AS ParentCustomerName,a.Address,a.Notes,a.IsActive,a.TotalPoints,a.TotalPieces,a.TotalDebts,a.RelationshipType FROM Ancestors a LEFT JOIN dbo.Customers p ON p.CustomerID=a.ParentCustomerId WHERE a.Depth>0 ORDER BY a.Depth OPTION (MAXRECURSION 100)", MapDetails, customerId, cancellationToken);

    public async Task<IReadOnlyList<ReferralTreeNodeDto>> GetDescendantsAsync(int customerId, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync("WITH Descendants AS (SELECT CustomerID,CustomerCode,CustomerName,ParentCustomerId FROM dbo.Customers WHERE CustomerID=@customerId UNION ALL SELECT c.CustomerID,c.CustomerCode,c.CustomerName,c.ParentCustomerId FROM dbo.Customers c JOIN Descendants d ON c.ParentCustomerId=d.CustomerID) SELECT CustomerID,CustomerCode,CustomerName,ParentCustomerId FROM Descendants OPTION (MAXRECURSION 100)", reader => new ReferralTreeNodeDto(reader.GetInt32(0), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableInt32("ParentCustomerId"), []), customerId, cancellationToken);
        var lookup = rows.ToDictionary(row => row.CustomerId, row => new ReferralTreeNodeDto(row.CustomerId, row.CustomerCode, row.CustomerName, row.ParentCustomerId, []));
        var children = rows.ToDictionary(row => row.CustomerId, _ => new List<ReferralTreeNodeDto>());
        foreach (var row in rows.Where(row => row.ParentCustomerId.HasValue && lookup.ContainsKey(row.ParentCustomerId.Value))) children[row.ParentCustomerId!.Value].Add(lookup[row.CustomerId]);
        ReferralTreeNodeDto Build(int id) => lookup[id] with { Children = children[id].OrderBy(child => child.CustomerName).Select(child => Build(child.CustomerId)).ToList() };
        return [Build(customerId)];
    }

    public async Task<int?> ResolveReferralCodeAsync(string referralCode, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CustomerId FROM dbo.ReferralCodes WHERE Code=@code AND IsActive=1";
        await using var connection = connections.Create(); await connection.OpenAsync(cancellationToken); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@code", referralCode.Trim()); return (await command.ExecuteScalarAsync(cancellationToken)) as int?;
    }

    public async Task<bool> WouldCreateCycleAsync(int customerId, int parentCustomerId, CancellationToken cancellationToken)
    {
        const string sql = "WITH Ancestors AS (SELECT CustomerID, ParentCustomerId FROM dbo.Customers WHERE CustomerID=@parentId UNION ALL SELECT c.CustomerID,c.ParentCustomerId FROM dbo.Customers c JOIN Ancestors a ON c.CustomerID=a.ParentCustomerId) SELECT CASE WHEN EXISTS (SELECT 1 FROM Ancestors WHERE CustomerID=@customerId) THEN 1 ELSE 0 END OPTION (MAXRECURSION 100)";
        await using var connection = connections.Create(); await connection.OpenAsync(cancellationToken); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@customerId", customerId); command.Parameters.AddWithValue("@parentId", parentCustomerId); return (int)(await command.ExecuteScalarAsync(cancellationToken))! == 1;
    }

    private async Task<int?> ResolveParentIdAsync(int? parentCustomerId, string? referralCode, CancellationToken cancellationToken)
    {
        if (parentCustomerId.HasValue && !string.IsNullOrWhiteSpace(referralCode)) throw new ArgumentException("Provide either ParentCustomerId or ReferralCode, not both.");
        var parentId = parentCustomerId ?? (string.IsNullOrWhiteSpace(referralCode) ? null : await ResolveReferralCodeAsync(referralCode, cancellationToken));
        if (!string.IsNullOrWhiteSpace(referralCode) && parentId is null) throw new ArgumentException("The referral code does not resolve to an active customer.");
        if (parentId.HasValue && await GetByIdAsync(parentId.Value, cancellationToken) is null) throw new ArgumentException("The referral parent does not exist.");
        return parentId;
    }

    private Task<IReadOnlyList<CustomerListDto>> QuerySearchAsync(string sql, string term, CancellationToken cancellationToken)
    {
        return QueryWithTextAsync(sql, reader => new CustomerListDto(reader.GetInt32(0), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.NullableDecimal("TotalPoints"), reader.NullableInt32("TotalPieces"), reader.NullableDecimal("TotalDebts"), reader.NullableString("RelationshipType")), term, cancellationToken);
    }

    public static bool ShouldRollbackAfterFailure(bool committed, bool transactionUsable)
        => !committed && transactionUsable;

    private static async Task<CustomerReferralCandidateDto?> GetReferralCandidateAsync(int customerId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber, referral.Code AS ReferralCode
            FROM dbo.Customers c
            OUTER APPLY (
                SELECT TOP (1) rc.Code
                FROM dbo.ReferralCodes rc
                WHERE rc.CustomerId = c.CustomerID AND rc.IsActive = 1
                ORDER BY rc.CreatedAt DESC, rc.ReferralCodeId DESC
            ) referral
            WHERE c.CustomerID = @customerId AND c.IsActive = 1;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new CustomerReferralCandidateDto(
            reader.GetInt32(0),
            reader.NullableString("CustomerCode"),
            reader.NullableString("CustomerName"),
            reader.NullableString("PhoneNumber"),
            reader.NullableString("ReferralCode"));
    }

    private static async Task<ReferralCodeRow> EnsureReferralCodeAsync(int customerId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string existingSql = @"SELECT TOP (1) ReferralCodeId, Code
            FROM dbo.ReferralCodes WITH (UPDLOCK, HOLDLOCK)
            WHERE CustomerId = @customerId AND IsActive = 1
            ORDER BY CreatedAt DESC, ReferralCodeId DESC;";
        await using (var existingCommand = new SqlCommand(existingSql, connection, transaction))
        {
            existingCommand.Parameters.AddWithValue("@customerId", customerId);
            await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                return new ReferralCodeRow(reader.GetInt32(0), reader.GetString(1));
        }

        var code = $"REF-{customerId}-{DateTime.UtcNow:yyMMddHHmmssfff}";
        const string insertSql = @"INSERT INTO dbo.ReferralCodes (CustomerId, Code, IsActive, CreatedAt)
            OUTPUT INSERTED.ReferralCodeId, INSERTED.Code
            VALUES (@customerId, @code, 1, SYSUTCDATETIME());";
        await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("@customerId", customerId);
        insertCommand.Parameters.AddWithValue("@code", code);
        await using var insertReader = await insertCommand.ExecuteReaderAsync(cancellationToken);
        if (!await insertReader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("تعذر إنشاء كود الإحالة.");
        return new ReferralCodeRow(insertReader.GetInt32(0), insertReader.GetString(1));
    }

    private static async Task InsertReferralAccountAsync(int customerId, int referralCodeId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO dbo.ReferralAccounts
            (CustomerId, ReferralCodeId, TotalReferrals, SuccessfulReferrals, TotalRewardsAmount, TotalRewardPoints, CreatedAt, UpdatedAt)
            VALUES (@customerId, @referralCodeId, 0, 0, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@referralCodeId", referralCodeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task IncrementReferralAccountAsync(int customerId, int referralCodeId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string updateSql = @"UPDATE dbo.ReferralAccounts
            SET ReferralCodeId = COALESCE(ReferralCodeId, @referralCodeId),
                TotalReferrals = TotalReferrals + 1,
                UpdatedAt = SYSUTCDATETIME()
            WHERE CustomerId = @customerId;";
        await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@customerId", customerId);
        updateCommand.Parameters.AddWithValue("@referralCodeId", referralCodeId);
        if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0) return;
        await InsertReferralAccountAsync(customerId, referralCodeId, connection, transaction, cancellationToken);
    }

    private static async Task<long> InsertRegistrationAsync(int referrerCustomerId, int referredCustomerId, int referralCodeId, string notes, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO dbo.ReferralTransactions
            (ReferrerCustomerId, ReferredCustomerId, ReferralCodeId, TransactionType, FixedRewardAmount, LoyaltyPoints, Notes, CreatedAt)
            OUTPUT INSERTED.ReferralTransactionId
            VALUES (@referrerCustomerId, @referredCustomerId, @referralCodeId, 'Registration', 0, 0, @notes, SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referrerCustomerId", referrerCustomerId);
        command.Parameters.AddWithValue("@referredCustomerId", referredCustomerId);
        command.Parameters.AddWithValue("@referralCodeId", referralCodeId);
        command.Parameters.AddWithValue("@notes", notes);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task UpdateReferralCodeLastUsedAsync(int referralCodeId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.ReferralCodes SET LastUsedAt = SYSUTCDATETIME() WHERE ReferralCodeId = @referralCodeId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referralCodeId", referralCodeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<CustomerCreationResultDto> ReadCreationResultAsync(int customerId, long? registrationTransactionId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber, c.Address, c.Notes,
                c.RelationshipType, c.TotalPoints, c.TotalDebts,
                COALESCE(ra.TotalReferrals, 0) AS ReferralCount,
                COALESCE(ra.TotalRewardsAmount, 0) AS ReferralRewardsAmount,
                COALESCE(ra.TotalRewardPoints, 0) AS ReferralRewardPoints,
                parent.CustomerID AS ReferrerCustomerId,
                parent.CustomerCode AS ReferrerCustomerCode,
                parent.CustomerName AS ReferrerCustomerName,
                parent.PhoneNumber AS ReferrerPhoneNumber,
                parentCode.Code AS ReferrerReferralCode
            FROM dbo.Customers c
            LEFT JOIN dbo.Customers parent ON parent.CustomerID = c.ParentCustomerId
            OUTER APPLY (
                SELECT TOP (1) rc.Code
                FROM dbo.ReferralCodes rc
                WHERE rc.CustomerId = parent.CustomerID AND rc.IsActive = 1
                ORDER BY rc.CreatedAt DESC, rc.ReferralCodeId DESC
            ) parentCode
            LEFT JOIN dbo.ReferralAccounts ra ON ra.CustomerId = c.CustomerID
            WHERE c.CustomerID = @customerId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("تعذر قراءة العميل الذي تم إنشاؤه.");
        return new CustomerCreationResultDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.NullableString("Address"),
            reader.NullableString("Notes"),
            reader.NullableString("RelationshipType"),
            reader.NullableDecimal("TotalPoints"),
            reader.NullableDecimal("TotalDebts"),
            reader.GetInt32(9),
            reader.GetDecimal(10),
            reader.GetDecimal(11),
            registrationTransactionId,
            reader.NullableInt32("ReferrerCustomerId"),
            reader.NullableString("ReferrerCustomerCode"),
            reader.NullableString("ReferrerCustomerName"),
            reader.NullableString("ReferrerPhoneNumber"),
            reader.NullableString("ReferrerReferralCode"));
    }

    private sealed record ReferralCodeRow(int ReferralCodeId, string Code);

    private static async Task EnsureNoDuplicateCustomerAsync(
        string name,
        string? phone,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;

        const string sql = @"SELECT TOP (1) CustomerID
            FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
            WHERE CustomerName = @name AND PhoneNumber = @phone;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@phone", phone);
        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
            throw new ArgumentException("هذا العميل مسجل من قبل بنفس الاسم ورقم الهاتف.");
    }

    private static string NormalizeCustomerName(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static CustomerDetailsDto MapDetails(SqlDataReader reader) => new(reader.GetInt32(0), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.NullableString("ParentCustomerCode"), reader.NullableInt32("ParentCustomerId"), reader.NullableString("ParentCustomerName"), reader.NullableString("Address"), reader.NullableString("Notes"), reader.GetBoolean(9), reader.NullableDecimal("TotalPoints"), reader.NullableInt32("TotalPieces"), reader.NullableDecimal("TotalDebts"), reader.NullableString("RelationshipType"));
    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private async Task<IReadOnlyList<T>> QueryWithTextAsync<T>(string sql, Func<SqlDataReader, T> map, string term, CancellationToken cancellationToken)
    {
        var normalizedTerm = NormalizeSearchTerm(term);
        try
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@term", $"%{normalizedTerm}%");
            command.Parameters.AddWithValue("@normalizedTerm", normalizedTerm);
            command.Parameters.AddWithValue("@prefixTerm", $"{normalizedTerm}%");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<T>();
            while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
            return results;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Array.Empty<T>();
        }
        catch (SqlException exception)
            when (cancellationToken.IsCancellationRequested && IsExpectedSearchCancellation(exception))
        {
            return Array.Empty<T>();
        }
    }

    private static string NormalizeSearchTerm(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();

    private static bool IsExpectedSearchCancellation(SqlException exception) =>
        exception.Message.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase) ||
        exception.Errors.Cast<SqlError>().Any(error =>
            error.Message.Contains("Operation canceled by user", StringComparison.OrdinalIgnoreCase) ||
            error.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int? customerId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (customerId is not null) command.Parameters.AddWithValue("@customerId", customerId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
        return results;
    }
}