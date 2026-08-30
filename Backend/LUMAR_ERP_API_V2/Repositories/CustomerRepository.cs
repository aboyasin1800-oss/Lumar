using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Customers;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class CustomerRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : ICustomerRepository
{
    public Task<IReadOnlyList<CustomerListDto>> GetListAsync(CancellationToken cancellationToken) => QueryAsync("SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, RelationshipType FROM dbo.Customers ORDER BY CustomerName, CustomerID", reader => new CustomerListDto(reader.GetInt32(0), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.NullableDecimal("TotalPoints"), reader.NullableInt32("TotalPieces"), reader.NullableDecimal("TotalDebts"), reader.NullableString("RelationshipType")), null, cancellationToken);

    public async Task<CustomerDetailsDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken)
    {
        var results = await QueryAsync("SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber, c.ParentCustomerCode, c.ParentCustomerId, p.CustomerName AS ParentCustomerName, c.Address, c.Notes, c.IsActive, c.TotalPoints, c.TotalPieces, c.TotalDebts, c.RelationshipType FROM dbo.Customers c LEFT JOIN dbo.Customers p ON p.CustomerID = c.ParentCustomerId WHERE c.CustomerID = @customerId", MapDetails, customerId, cancellationToken);
        return results.SingleOrDefault();
    }

    public Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurementsAsync(int customerId, CancellationToken cancellationToken) => QueryAsync("SELECT Id, CustomerId, PieceType, MeasurementName, MeasurementValue, CreatedAtUtc, RevisionNumber FROM dbo.CustomerMeasurements WHERE CustomerId = @customerId ORDER BY CreatedAtUtc DESC, Id DESC", reader => new CustomerMeasurementDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDateTime(5), reader.GetInt32(6)), customerId, cancellationToken);

    public async Task<IReadOnlyList<CustomerMeasurementDto>?> UpsertMeasurementsAsync(int customerId, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
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
            return saved;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken cancellationToken) => QueryAsync("SELECT CustomerLedgerEntryId, CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt FROM dbo.CustomerLedgerEntries WHERE CustomerID = @customerId ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC", reader => new CustomerLedgerEntryDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDateTime(6)), customerId, cancellationToken);

    public async Task<CustomerLoyaltyDto?> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT la.LoyaltyAccountId, la.CustomerId, la.CurrentPoints, la.LifetimeEarnedPoints, la.LifetimeRedeemedPoints, la.PendingExpirePoints, la.VipLevelId, vl.Code AS VipLevelCode, vl.DisplayName AS VipLevelDisplayName, la.CreatedAt, la.UpdatedAt, la.LastActivityAt FROM dbo.LoyaltyAccounts la LEFT JOIN dbo.VipLevels vl ON vl.VipLevelId = la.VipLevelId WHERE la.CustomerId = @customerId";
        var results = await QueryAsync(sql, reader => new CustomerLoyaltyDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.NullableInt32("VipLevelId"), reader.NullableString("VipLevelCode"), reader.NullableString("VipLevelDisplayName"), reader.GetDateTime(9), reader.GetDateTime(10), reader.NullableDateTime("LastActivityAt")), customerId, cancellationToken);
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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string sql = "INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, ParentCustomerId, RelationshipType) OUTPUT INSERTED.CustomerID VALUES (@code, @name, @phone, @address, @notes, @isActive, @parentId, @relationshipType)";
            await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@code", customer.CustomerCode!.Trim());
            command.Parameters.AddWithValue("@name", customer.CustomerName!.Trim());
            AddNullable(command, "@phone", customer.PhoneNumber);
            AddNullable(command, "@address", customer.Address);
            AddNullable(command, "@notes", customer.Notes);
            command.Parameters.AddWithValue("@isActive", customer.IsActive);
            AddNullable(command, "@parentId", parentId);
            AddNullable(command, "@relationshipType", customer.RelationshipType);
            var customerId = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            await transaction.CommitAsync(cancellationToken);
            return (await GetByIdAsync(customerId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
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

    public Task<IReadOnlyList<CustomerListDto>> SearchAsync(string term, CancellationToken cancellationToken) => QuerySearchAsync("SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, RelationshipType FROM dbo.Customers WHERE CustomerCode LIKE @term OR CustomerName LIKE @term OR PhoneNumber LIKE @term OR ParentCustomerCode LIKE @term OR EXISTS (SELECT 1 FROM dbo.ReferralCodes rc WHERE rc.CustomerId = Customers.CustomerID AND rc.Code LIKE @term) ORDER BY CustomerName, CustomerID", term, cancellationToken);

    public Task<IReadOnlyList<CustomerDetailsDto>> GetAncestorsAsync(int customerId, CancellationToken cancellationToken) => QueryAsync("WITH Ancestors AS (SELECT CustomerID, CustomerCode, CustomerName, PhoneNumber, ParentCustomerCode, ParentCustomerId, Address, Notes, IsActive, TotalPoints, TotalPieces, TotalDebts, RelationshipType, 0 AS Depth FROM dbo.Customers WHERE CustomerID=@customerId UNION ALL SELECT p.CustomerID,p.CustomerCode,p.CustomerName,p.PhoneNumber,p.ParentCustomerCode,p.ParentCustomerId,p.Address,p.Notes,p.IsActive,p.TotalPoints,p.TotalPieces,p.TotalDebts,p.RelationshipType,a.Depth+1 FROM dbo.Customers p JOIN Ancestors a ON a.ParentCustomerId=p.CustomerID) SELECT a.CustomerID,a.CustomerCode,a.CustomerName,a.PhoneNumber,a.ParentCustomerCode,a.ParentCustomerId,p.CustomerName AS ParentCustomerName,a.Address,a.Notes,a.IsActive,a.TotalPoints,a.TotalPieces,a.TotalDebts,a.RelationshipType FROM Ancestors a LEFT JOIN dbo.Customers p ON p.CustomerID=a.ParentCustomerId WHERE a.Depth>0 ORDER BY a.Depth OPTION (MAXRECURSION 100)", MapDetails, customerId, cancellationToken);

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

    private static CustomerDetailsDto MapDetails(SqlDataReader reader) => new(reader.GetInt32(0), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.NullableString("ParentCustomerCode"), reader.NullableInt32("ParentCustomerId"), reader.NullableString("ParentCustomerName"), reader.NullableString("Address"), reader.NullableString("Notes"), reader.GetBoolean(9), reader.NullableDecimal("TotalPoints"), reader.NullableInt32("TotalPieces"), reader.NullableDecimal("TotalDebts"), reader.NullableString("RelationshipType"));
    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private async Task<IReadOnlyList<T>> QueryWithTextAsync<T>(string sql, Func<SqlDataReader, T> map, string term, CancellationToken cancellationToken) { await using var connection = connections.Create(); await connection.OpenAsync(cancellationToken); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@term", $"%{term.Trim()}%"); await using var reader = await command.ExecuteReaderAsync(cancellationToken); var results = new List<T>(); while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader)); return results; }

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