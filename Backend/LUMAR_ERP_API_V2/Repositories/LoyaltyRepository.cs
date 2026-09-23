using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class LoyaltyRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : ILoyaltyRepository
{
    public async Task<LoyaltyDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                COUNT(*),
                COALESCE(SUM(la.CurrentPoints), 0),
                COALESCE((SELECT SUM(ABS(Points)) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Earn'), 0),
                COALESCE((SELECT SUM(ABS(Points)) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Redeem'), 0),
                COALESCE((SELECT SUM(ABS(Points)) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Reversal'), 0),
                COALESCE((SELECT SUM(ABS(Points)) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Adjust'), 0),
                (SELECT COUNT(DISTINCT CustomerId) FROM dbo.LoyaltyTransactions)
            FROM dbo.LoyaltyAccounts la;
            SELECT TOP (10)
                la.CustomerId, c.CustomerCode, c.CustomerName, la.CurrentPoints,
                COUNT(lt.LoyaltyTransactionId)
            FROM dbo.LoyaltyAccounts la
            LEFT JOIN dbo.Customers c ON c.CustomerID = la.CustomerId
            LEFT JOIN dbo.LoyaltyTransactions lt ON lt.CustomerId = la.CustomerId
            GROUP BY la.CustomerId, c.CustomerCode, c.CustomerName, la.CurrentPoints
            ORDER BY la.CurrentPoints DESC, la.CustomerId;
            SELECT TOP (10)
                lt.CustomerId, c.CustomerCode, c.CustomerName,
                SUM(ABS(lt.Points)), COUNT(*)
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            WHERE lt.TransactionType = 'Earn'
            GROUP BY lt.CustomerId, c.CustomerCode, c.CustomerName
            ORDER BY SUM(ABS(lt.Points)) DESC, lt.CustomerId;
            SELECT vl.VipLevelId, vl.DisplayName, COUNT(la.LoyaltyAccountId), vl.MinimumPoints
            FROM dbo.VipLevels vl
            LEFT JOIN dbo.LoyaltyAccounts la ON la.VipLevelId = vl.VipLevelId
            GROUP BY vl.VipLevelId, vl.DisplayName, vl.MinimumPoints, vl.Priority
            ORDER BY vl.Priority, vl.MinimumPoints, vl.VipLevelId;
            SELECT TOP (10)
                lt.LoyaltyTransactionId, lt.CustomerId, c.CustomerCode, c.CustomerName,
                lt.TransactionType, lt.Points, lt.OrderId, lt.Notes, lt.CreatedAt
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            ORDER BY lt.CreatedAt DESC, lt.LoyaltyTransactionId DESC;
            SELECT
                (SELECT COUNT(*) FROM dbo.LoyaltyAccounts),
                (SELECT COUNT(*) FROM dbo.LoyaltyTransactions),
                (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Redeem'),
                (SELECT COALESCE(SUM(la.CurrentPoints), 0) FROM dbo.LoyaltyAccounts la),
                (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Earn'),
                (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Redeem'),
                (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Reversal'),
                (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE TransactionType = 'Adjust');";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);
        var accountCount = reader.GetInt32(0);
        var currentPoints = reader.GetDecimal(1);
        var earnedPoints = reader.GetDecimal(2);
        var redeemedPoints = reader.GetDecimal(3);
        var reversedPoints = reader.GetDecimal(4);
        var adjustedPoints = reader.GetDecimal(5);
        var activeCustomers = reader.GetInt32(6);

        await reader.NextResultAsync(cancellationToken);
        var topBalances = new List<LoyaltyTopCustomerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topBalances.Add(new(
                reader.GetInt32(0), reader[1] as string, reader[2] as string,
                reader.GetDecimal(3), reader.GetInt32(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        var topEarners = new List<LoyaltyTopCustomerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topEarners.Add(new(
                reader.GetInt32(0), reader[1] as string, reader[2] as string,
                reader.GetDecimal(3), reader.GetInt32(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        var vipLevels = new List<LoyaltyVipSummaryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            vipLevels.Add(new(
                reader.GetInt32(0), reader[1] as string, reader.GetInt32(2),
                reader.GetDecimal(3)));
        }

        await reader.NextResultAsync(cancellationToken);
        var recentActivities = new List<LoyaltyActivityDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            recentActivities.Add(new(
                reader.GetInt64(0), reader.GetInt32(1), reader[2] as string,
                reader[3] as string, reader.GetString(4), reader.GetDecimal(5),
                reader[6] is DBNull ? null : reader.GetInt32(6),
                reader[7] is DBNull ? null : reader.GetString(7),
                reader.GetDateTime(8)));
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var summary = new LoyaltyProgramSummaryDto(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.GetDecimal(3), reader.GetInt32(4), reader.GetInt32(5),
            reader.GetInt32(6), reader.GetInt32(7));

        return new LoyaltyDashboardDto(
            accountCount, currentPoints, earnedPoints, redeemedPoints,
            reversedPoints, adjustedPoints, activeCustomers, topBalances,
            topEarners, vipLevels, recentActivities, summary);
    }

    public async Task<LoyaltyTransactionsScreenDto> GetTransactionsScreenAsync(
        string? search,
        string? transactionType,
        DateTime? from,
        DateTime? to,
        int? customerId,
        CancellationToken cancellationToken)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            filters.Add("(c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)");
        if (!string.IsNullOrWhiteSpace(transactionType))
            filters.Add("lt.TransactionType = @transactionType");
        if (from.HasValue) filters.Add("lt.CreatedAt >= @from");
        if (to.HasValue) filters.Add("lt.CreatedAt < DATEADD(day, 1, @to)");
        if (customerId.HasValue) filters.Add("lt.CustomerId = @customerId");

        var where = filters.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", filters)}";
        var sql = $@"
            SELECT
                COUNT(*),
                COALESCE(SUM(CASE WHEN lt.TransactionType = 'Earn' THEN ABS(lt.Points) ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN lt.TransactionType = 'Redeem' THEN ABS(lt.Points) ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN lt.TransactionType = 'Reversal' THEN ABS(lt.Points) ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN lt.TransactionType = 'Adjust' THEN 1 ELSE 0 END), 0),
                COUNT(DISTINCT lt.CustomerId)
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where};
            SELECT lt.TransactionType, COUNT(*), COALESCE(SUM(ABS(lt.Points)), 0)
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where}
            GROUP BY lt.TransactionType
            ORDER BY lt.TransactionType;
            SELECT
                lt.LoyaltyTransactionId, lt.LoyaltyAccountId, lt.CustomerId,
                c.CustomerCode, c.CustomerName, lt.OrderId, lt.RewardId,
                lt.TransactionType, lt.Points, lt.BalanceBefore, lt.BalanceAfter,
                lt.Source, lt.Notes, lt.CreatedAt
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where}
            ORDER BY lt.CreatedAt DESC, lt.LoyaltyTransactionId DESC;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
        if (!string.IsNullOrWhiteSpace(transactionType)) command.Parameters.AddWithValue("@transactionType", transactionType.Trim());
        if (from.HasValue) command.Parameters.AddWithValue("@from", from.Value.Date);
        if (to.HasValue) command.Parameters.AddWithValue("@to", to.Value.Date);
        if (customerId.HasValue) command.Parameters.AddWithValue("@customerId", customerId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var summary = new LoyaltyTransactionsSummaryDto(
            reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2),
            reader.GetDecimal(3), reader.GetInt32(4), reader.GetInt32(5),
            []);

        await reader.NextResultAsync(cancellationToken);
        var types = new List<LoyaltyTransactionTypeSummaryDto>();
        while (await reader.ReadAsync(cancellationToken))
            types.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetDecimal(2)));

        await reader.NextResultAsync(cancellationToken);
        var transactions = new List<LoyaltyTransactionListItemDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            transactions.Add(new(
                reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2),
                reader[3] is DBNull ? null : reader.GetString(3),
                reader[4] is DBNull ? null : reader.GetString(4),
                reader[5] is DBNull ? null : reader.GetInt32(5),
                reader[6] is DBNull ? null : reader.GetInt32(6),
                reader.GetString(7), reader.GetDecimal(8), reader.GetDecimal(9),
                reader.GetDecimal(10), reader.GetString(11),
                reader[12] is DBNull ? null : reader.GetString(12),
                reader.GetDateTime(13)));
        }

        return new LoyaltyTransactionsScreenDto(summary with { Types = types }, transactions);
    }

    public async Task<LoyaltyRewardsScreenDto> GetRewardsScreenAsync(
        string? search,
        string? rewardType,
        DateTime? from,
        DateTime? to,
        int? customerId,
        CancellationToken cancellationToken)
    {
        var filters = new List<string>
        {
            "lt.TransactionType = N'Earn'",
            "lt.Points > 0"
        };
        if (!string.IsNullOrWhiteSpace(search))
            filters.Add("(c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)");
        if (!string.IsNullOrWhiteSpace(rewardType))
            filters.Add("lt.Source = @rewardType");
        if (from.HasValue) filters.Add("lt.CreatedAt >= @from");
        if (to.HasValue) filters.Add("lt.CreatedAt < DATEADD(day, 1, @to)");
        if (customerId.HasValue) filters.Add("lt.CustomerId = @customerId");

        var where = $"WHERE {string.Join(" AND ", filters)}";
        const string reversedExpression = @"CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.LoyaltyTransactions reversal
                    WHERE reversal.TransactionType = N'Reversal'
                      AND reversal.CustomerId = lt.CustomerId
                      AND reversal.OrderId = lt.OrderId
                      AND reversal.Points = lt.Points
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END";
        var sql = $@"
            SELECT
                COUNT(*),
                COALESCE(SUM(lt.Points), 0),
                COUNT(DISTINCT lt.CustomerId),
                COALESCE(AVG(lt.Points), 0),
                COALESCE(MAX(lt.Points), 0)
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where};
            SELECT TOP (10)
                lt.CustomerId, c.CustomerCode, c.CustomerName,
                SUM(lt.Points), COUNT(*)
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where}
            GROUP BY lt.CustomerId, c.CustomerCode, c.CustomerName
            ORDER BY SUM(lt.Points) DESC, lt.CustomerId;
            SELECT TOP (10)
                lt.LoyaltyTransactionId, lt.LoyaltyAccountId, lt.CustomerId,
                c.CustomerCode, c.CustomerName, lt.OrderId, lt.RewardId,
                lt.Points, lt.BalanceBefore, lt.BalanceAfter,
                lt.Source, lt.Notes, lt.CreatedAt, {reversedExpression}
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where}
            ORDER BY lt.Points DESC, lt.CreatedAt DESC, lt.LoyaltyTransactionId DESC;
            SELECT
                lt.LoyaltyTransactionId, lt.LoyaltyAccountId, lt.CustomerId,
                c.CustomerCode, c.CustomerName, lt.OrderId, lt.RewardId,
                lt.Points, lt.BalanceBefore, lt.BalanceAfter,
                lt.Source, lt.Notes, lt.CreatedAt, {reversedExpression}
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            {where}
            ORDER BY lt.CreatedAt DESC, lt.LoyaltyTransactionId DESC;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
        if (!string.IsNullOrWhiteSpace(rewardType)) command.Parameters.AddWithValue("@rewardType", rewardType.Trim());
        if (from.HasValue) command.Parameters.AddWithValue("@from", from.Value.Date);
        if (to.HasValue) command.Parameters.AddWithValue("@to", to.Value.Date);
        if (customerId.HasValue) command.Parameters.AddWithValue("@customerId", customerId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var summary = new LoyaltyRewardsSummaryDto(
            reader.GetInt32(0), reader.GetDecimal(1), reader.GetInt32(2),
            reader.GetDecimal(3), reader.GetDecimal(4));

        await reader.NextResultAsync(cancellationToken);
        var topCustomers = new List<LoyaltyRewardCustomerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topCustomers.Add(new(
                reader.GetInt32(0), reader[1] as string, reader[2] as string,
                reader.GetDecimal(3), reader.GetInt32(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        var largestRewards = new List<LoyaltyRewardItemDto>();
        while (await reader.ReadAsync(cancellationToken))
            largestRewards.Add(MapReward(reader));

        await reader.NextResultAsync(cancellationToken);
        var rewards = new List<LoyaltyRewardItemDto>();
        while (await reader.ReadAsync(cancellationToken))
            rewards.Add(MapReward(reader));

        return new LoyaltyRewardsScreenDto(summary, topCustomers, largestRewards, rewards);
    }

    private static LoyaltyRewardItemDto MapReward(SqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt32(1),
        reader.GetInt32(2),
        reader[3] is DBNull ? null : reader.GetString(3),
        reader[4] is DBNull ? null : reader.GetString(4),
        reader[5] is DBNull ? null : reader.GetInt32(5),
        reader[6] is DBNull ? null : reader.GetInt32(6),
        reader.GetDecimal(7),
        reader.GetDecimal(8),
        reader.GetDecimal(9),
        reader.GetString(10),
        reader[11] is DBNull ? null : reader.GetString(11),
        reader.GetDateTime(12),
        reader.GetBoolean(13));

    public async Task<LoyaltyAccountDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyAccountId, CustomerId, CurrentPoints, LifetimeEarnedPoints, LifetimeRedeemedPoints, PendingExpirePoints, VipLevelId, CreatedAt, UpdatedAt, LastActivityAt, LoyaltyAccountStatus, WarningStartedAtUtc, FrozenAtUtc, ReactivatedAtUtc, FreezeReason, LastQualifyingActivityAtUtc FROM dbo.LoyaltyAccounts WHERE CustomerId = @customerId";
        var rows = await QueryAsync(sql, customerId, MapAccount, cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<LoyaltyAccountDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken)
    {
        var existing = await GetAccountByCustomerAsync(customerId, cancellationToken);
        if (existing is not null) return existing;

        const string sql = @"INSERT INTO dbo.LoyaltyAccounts (CustomerId, CurrentPoints, LifetimeEarnedPoints, LifetimeRedeemedPoints, PendingExpirePoints, VipLevelId, CreatedAt, UpdatedAt, LastActivityAt, LoyaltyAccountStatus)
            OUTPUT INSERTED.LoyaltyAccountId, INSERTED.CustomerId, INSERTED.CurrentPoints, INSERTED.LifetimeEarnedPoints, INSERTED.LifetimeRedeemedPoints, INSERTED.PendingExpirePoints, INSERTED.VipLevelId, INSERTED.CreatedAt, INSERTED.UpdatedAt, INSERTED.LastActivityAt, INSERTED.LoyaltyAccountStatus, INSERTED.WarningStartedAtUtc, INSERTED.FrozenAtUtc, INSERTED.ReactivatedAtUtc, INSERTED.FreezeReason, INSERTED.LastQualifyingActivityAtUtc
            VALUES (@customerId, 0, 0, 0, 0, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME(), N'Active');";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to create loyalty account.");

        return MapAccount(reader);
    }

    public async Task<IReadOnlyList<LoyaltyAccountDto>> GetAllAccountsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyAccountId, CustomerId, CurrentPoints, LifetimeEarnedPoints, LifetimeRedeemedPoints, PendingExpirePoints, VipLevelId, CreatedAt, UpdatedAt, LastActivityAt, LoyaltyAccountStatus, WarningStartedAtUtc, FrozenAtUtc, ReactivatedAtUtc, FreezeReason, LastQualifyingActivityAtUtc FROM dbo.LoyaltyAccounts ORDER BY LoyaltyAccountId";
        return await QueryAsync(sql, 0, MapAccount, cancellationToken);
    }

    public async Task<LoyaltyAccountDto?> UpdateAccountLifecycleAsync(int customerId, string status, DateTime? warningStartedAtUtc, DateTime? frozenAtUtc, DateTime? reactivatedAtUtc, string? freezeReason, DateTime? lastQualifyingActivityAtUtc, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE dbo.LoyaltyAccounts
SET LoyaltyAccountStatus = @status,
    WarningStartedAtUtc = @warningStartedAtUtc,
    FrozenAtUtc = @frozenAtUtc,
    ReactivatedAtUtc = @reactivatedAtUtc,
    FreezeReason = @freezeReason,
    LastQualifyingActivityAtUtc = @lastQualifyingActivityAtUtc,
    UpdatedAt = SYSUTCDATETIME()
WHERE CustomerId = @customerId;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@warningStartedAtUtc", (object?)warningStartedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@frozenAtUtc", (object?)frozenAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@reactivatedAtUtc", (object?)reactivatedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@freezeReason", (object?)freezeReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastQualifyingActivityAtUtc", (object?)lastQualifyingActivityAtUtc ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 0
            ? null
            : await GetAccountByCustomerAsync(customerId, cancellationToken);
    }

    public async Task<IReadOnlyList<LoyaltyTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT LoyaltyTransactionId, LoyaltyAccountId, CustomerId, OrderId, RewardId, TransactionType, Points, BalanceBefore, BalanceAfter, Source, Notes, CreatedAt
            FROM dbo.LoyaltyTransactions WHERE CustomerId = @customerId ORDER BY CreatedAt DESC, LoyaltyTransactionId DESC";

        return await QueryAsync(sql, customerId, reader => new LoyaltyTransactionDto(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader[3] is DBNull ? null : reader.GetInt32(3),
            reader[4] is DBNull ? null : reader.GetInt32(4),
            reader.GetString(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            reader[10] is DBNull ? null : reader.GetString(10),
            reader.GetDateTime(11)), cancellationToken);
    }

    public async Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken)
    {
        var account = await EnsureAccountAsync(customerId, cancellationToken);
        const string sql = @"SELECT TOP (1) BalanceBefore, BalanceAfter FROM dbo.LoyaltyTransactions WHERE CustomerId = @customerId ORDER BY CreatedAt DESC, LoyaltyTransactionId DESC";

        var rows = await QueryAsync(sql, customerId, reader => new
        {
            BalanceBefore = reader.GetDecimal(0),
            BalanceAfter = reader.GetDecimal(1)
        }, cancellationToken);

        var last = rows.FirstOrDefault();
        var current = account.CurrentPoints;
        var balanceBefore = last?.BalanceBefore ?? current;
        var balanceAfter = last?.BalanceAfter ?? current;

        return new LoyaltyBalanceDto(
            account.LoyaltyAccountId,
            account.CustomerId,
            current,
            account.LifetimeEarnedPoints,
            account.LifetimeRedeemedPoints,
            account.PendingExpirePoints,
            balanceBefore,
            balanceAfter,
            account.VipLevelId,
            account.UpdatedAt,
            account.LastActivityAt,
            account.LoyaltyAccountStatus,
            account.WarningStartedAtUtc,
            account.FrozenAtUtc,
            account.ReactivatedAtUtc,
            account.FreezeReason,
            account.LastQualifyingActivityAtUtc);
    }

    public async Task<LoyaltyTransactionResultDto> CreateTransactionAsync(int customerId, string transactionType, decimal points, int? orderId, int? rewardId, string source, string? notes, CancellationToken cancellationToken, bool allowFrozenQualifyingPurchase = false)
    {
        var account = await EnsureAccountAsync(customerId, cancellationToken);
        var isQualifyingPurchase = allowFrozenQualifyingPurchase &&
            transactionType.Equals("Earn", StringComparison.OrdinalIgnoreCase) &&
            orderId.HasValue &&
            source.Equals("PiecePurchase", StringComparison.OrdinalIgnoreCase);
        if (account.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase) &&
            !isQualifyingPurchase &&
            (transactionType.Equals("Earn", StringComparison.OrdinalIgnoreCase) ||
             (transactionType.Equals("Adjust", StringComparison.OrdinalIgnoreCase) && points > 0m)))
            throw new InvalidOperationException("Loyalty account is frozen and cannot receive new rewards.");
        var result = LoyaltyAccountResolver.ApplyTransaction(account, transactionType, points, orderId, rewardId, source, notes);

        const string insertSql = @"INSERT INTO dbo.LoyaltyTransactions (LoyaltyAccountId, CustomerId, OrderId, RewardId, TransactionType, Points, BalanceBefore, BalanceAfter, Source, Notes, CreatedAt)
            OUTPUT INSERTED.LoyaltyTransactionId, INSERTED.LoyaltyAccountId, INSERTED.CustomerId, INSERTED.OrderId, INSERTED.RewardId, INSERTED.TransactionType, INSERTED.Points, INSERTED.BalanceBefore, INSERTED.BalanceAfter, INSERTED.Source, INSERTED.Notes, INSERTED.CreatedAt
            VALUES (@loyaltyAccountId, @customerId, @orderId, @rewardId, @transactionType, @points, @balanceBefore, @balanceAfter, @source, @notes, SYSUTCDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(insertSql, connection);
        command.Parameters.AddWithValue("@loyaltyAccountId", account.LoyaltyAccountId);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderId", (object?)orderId ?? DBNull.Value);
        command.Parameters.AddWithValue("@rewardId", (object?)rewardId ?? DBNull.Value);
        command.Parameters.AddWithValue("@transactionType", result.Transaction.TransactionType);
        command.Parameters.AddWithValue("@points", result.Transaction.Points);
        command.Parameters.AddWithValue("@balanceBefore", result.BalanceBefore);
        command.Parameters.AddWithValue("@balanceAfter", result.BalanceAfter);
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to store loyalty transaction.");

        var inserted = new LoyaltyTransactionDto(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader[3] is DBNull ? null : reader.GetInt32(3),
            reader[4] is DBNull ? null : reader.GetInt32(4),
            reader.GetString(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            reader[10] is DBNull ? null : reader.GetString(10),
            reader.GetDateTime(11));

        var updated = account with { CurrentPoints = result.BalanceAfter, LifetimeEarnedPoints = result.Account.LifetimeEarnedPoints, LifetimeRedeemedPoints = result.Account.LifetimeRedeemedPoints, PendingExpirePoints = result.Account.PendingExpirePoints, UpdatedAt = DateTime.UtcNow, LastActivityAt = DateTime.UtcNow };

        const string updateSql = @"UPDATE dbo.LoyaltyAccounts SET CurrentPoints = @currentPoints, LifetimeEarnedPoints = @lifetimeEarnedPoints, LifetimeRedeemedPoints = @lifetimeRedeemedPoints, PendingExpirePoints = @pendingExpirePoints, UpdatedAt = SYSUTCDATETIME(), LastActivityAt = SYSUTCDATETIME() WHERE LoyaltyAccountId = @loyaltyAccountId";
        await using var updateCommand = new SqlCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@currentPoints", updated.CurrentPoints);
        updateCommand.Parameters.AddWithValue("@lifetimeEarnedPoints", updated.LifetimeEarnedPoints);
        updateCommand.Parameters.AddWithValue("@lifetimeRedeemedPoints", updated.LifetimeRedeemedPoints);
        updateCommand.Parameters.AddWithValue("@pendingExpirePoints", updated.PendingExpirePoints);
        updateCommand.Parameters.AddWithValue("@loyaltyAccountId", account.LoyaltyAccountId);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);

        return new LoyaltyTransactionResultDto(inserted, updated, result.BalanceBefore, result.BalanceAfter);
    }

    private static LoyaltyAccountDto MapAccount(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetInt32(1),
        reader.GetDecimal(2),
        reader.GetDecimal(3),
        reader.GetDecimal(4),
        reader.GetDecimal(5),
        reader[6] is DBNull ? null : reader.GetInt32(6),
        reader.GetDateTime(7),
        reader.GetDateTime(8),
        reader[9] is DBNull ? null : reader.GetDateTime(9),
        reader[10] is DBNull ? "Active" : reader.GetString(10),
        reader[11] is DBNull ? null : reader.GetDateTime(11),
        reader[12] is DBNull ? null : reader.GetDateTime(12),
        reader[13] is DBNull ? null : reader.GetDateTime(13),
        reader[14] is DBNull ? null : reader.GetString(14),
        reader[15] is DBNull ? null : reader.GetDateTime(15));

    private async Task<List<T>> QueryAsync<T>(string sql, int customerId, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
        return results;
    }
}
