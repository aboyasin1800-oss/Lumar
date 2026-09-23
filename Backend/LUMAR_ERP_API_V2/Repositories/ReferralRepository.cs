using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Referral;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ReferralRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IReferralRepository
{
    public async Task<ReferralDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM dbo.ReferralTransactions WHERE TransactionType = 'Registration'),
                (SELECT COUNT(*) FROM (
                    SELECT ReferrerCustomerId AS CustomerId FROM dbo.ReferralTransactions
                    UNION
                    SELECT ReferredCustomerId FROM dbo.ReferralTransactions WHERE ReferredCustomerId IS NOT NULL
                ) participants),
                (SELECT COUNT(*) FROM dbo.ReferralTransactions WHERE TransactionType = 'RewardGranted'),
                (SELECT COUNT(*) FROM dbo.ReferralTransactions WHERE TransactionType = 'RewardReversal'),
                COALESCE((SELECT SUM(LoyaltyPoints) FROM dbo.ReferralTransactions WHERE TransactionType = 'RewardGranted'), 0);

            SELECT TOP (5) t.ReferrerCustomerId, c.CustomerCode, c.CustomerName, COUNT(*) AS ReferralCount
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE t.TransactionType = 'Registration'
            GROUP BY t.ReferrerCustomerId, c.CustomerCode, c.CustomerName
            ORDER BY COUNT(*) DESC, t.ReferrerCustomerId;

            SELECT TOP (5) rc.ReferralCodeId, rc.Code, rc.CustomerId, c.CustomerName, COUNT(t.ReferralTransactionId) AS UsageCount
            FROM dbo.ReferralCodes rc
            LEFT JOIN dbo.Customers c ON c.CustomerID = rc.CustomerId
            LEFT JOIN dbo.ReferralTransactions t ON t.ReferralCodeId = rc.ReferralCodeId AND t.TransactionType = 'Registration'
            GROUP BY rc.ReferralCodeId, rc.Code, rc.CustomerId, c.CustomerName
            ORDER BY COUNT(t.ReferralTransactionId) DESC, rc.ReferralCodeId;

            SELECT TOP (10)
                t.ReferralTransactionId, t.TransactionType, t.ReferrerCustomerId, referrer.CustomerName,
                t.ReferredCustomerId, referred.CustomerName, rc.Code, t.FixedRewardAmount, t.LoyaltyPoints, t.CreatedAt
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers referrer ON referrer.CustomerID = t.ReferrerCustomerId
            LEFT JOIN dbo.Customers referred ON referred.CustomerID = t.ReferredCustomerId
            LEFT JOIN dbo.ReferralCodes rc ON rc.ReferralCodeId = t.ReferralCodeId
            ORDER BY t.CreatedAt DESC, t.ReferralTransactionId DESC;

            SELECT TOP (5) t.ReferredCustomerId, c.CustomerCode, c.CustomerName, COUNT(*) AS ReferralCount
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferredCustomerId
            WHERE t.TransactionType = 'Registration' AND t.ReferredCustomerId IS NOT NULL
            GROUP BY t.ReferredCustomerId, c.CustomerCode, c.CustomerName
            ORDER BY COUNT(*) DESC, t.ReferredCustomerId;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Unable to read referral dashboard metrics.");
        }

        var totalRegistrations = reader.GetInt32(0);
        var participatingCustomers = reader.GetInt32(1);
        var rewardsGranted = reader.GetInt32(2);
        var rewardReversals = reader.GetInt32(3);
        var totalReferralPoints = reader.GetDecimal(4);

        await reader.NextResultAsync(cancellationToken);
        var topReferrers = new List<ReferralDashboardReferrerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topReferrers.Add(new ReferralDashboardReferrerDto(
                reader.GetInt32(0),
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader.GetInt32(3)));
        }

        await reader.NextResultAsync(cancellationToken);
        var topCodes = new List<ReferralDashboardCodeDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topCodes.Add(new ReferralDashboardCodeDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader[3] is DBNull ? null : reader.GetString(3),
                reader.GetInt32(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        var recentEvents = new List<ReferralDashboardEventDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            recentEvents.Add(new ReferralDashboardEventDto(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader[3] is DBNull ? null : reader.GetString(3),
                reader[4] is DBNull ? null : reader.GetInt32(4),
                reader[5] is DBNull ? null : reader.GetString(5),
                reader[6] is DBNull ? null : reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDateTime(9)));
        }

        await reader.NextResultAsync(cancellationToken);
        var topReceivers = new List<ReferralDashboardReceiverDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topReceivers.Add(new ReferralDashboardReceiverDto(
                reader.GetInt32(0),
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader.GetInt32(3)));
        }

        var roots = await GetRootsAsync(cancellationToken);
        var maxDepth = roots.Count == 0 ? 0 : roots.Max(root => root.MaxDepth);
        var maxDirectReferrals = 0;
        foreach (var root in roots)
        {
            var tree = await GetTreeAsync(root.CustomerId, cancellationToken);
            if (tree is null) continue;
            maxDirectReferrals = Math.Max(maxDirectReferrals, tree.DirectReferralsCount);
            maxDirectReferrals = Math.Max(maxDirectReferrals, MaxDirectReferrals(tree.Children));
        }

        return new ReferralDashboardDto(
            totalRegistrations,
            participatingCustomers,
            rewardsGranted,
            rewardReversals,
            totalReferralPoints,
            topReferrers,
            topCodes,
            recentEvents,
            topReceivers,
            new ReferralDashboardTreeSummaryDto(roots.Count, maxDepth, maxDirectReferrals));
    }

    public async Task<ReferralAnalyticsDto> GetAnalyticsAsync(
        string? search,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                COUNT(CASE WHEN t.TransactionType = 'Registration' THEN 1 END),
                COUNT(DISTINCT CASE WHEN t.TransactionType = 'Registration' THEN t.ReferredCustomerId END),
                COUNT(CASE WHEN t.TransactionType = 'RewardGranted' THEN 1 END),
                COALESCE(SUM(CASE WHEN t.TransactionType = 'RewardGranted' THEN t.LoyaltyPoints ELSE 0 END), 0),
                CAST(COUNT(CASE WHEN t.TransactionType = 'Registration' THEN 1 END) AS decimal(18,4)) /
                    NULLIF(COUNT(DISTINCT CASE WHEN t.TransactionType = 'Registration' THEN t.ReferrerCustomerId END), 0)
            FROM dbo.ReferralTransactions t
            WHERE t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR EXISTS (
                  SELECT 1 FROM dbo.Customers c
                  WHERE c.CustomerID = t.ReferrerCustomerId
                    AND (c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)
              ) OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc
                  WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search
              ));

            SELECT
                CAST(COUNT(CASE WHEN t.TransactionType = 'Registration' THEN 1 END) AS decimal(18,4)) /
                    NULLIF(COUNT(DISTINCT CASE WHEN t.TransactionType = 'Registration' THEN t.ReferralCodeId END), 0),
                CAST(COUNT(DISTINCT CASE WHEN t.TransactionType = 'Registration' THEN t.ReferredCustomerId END) AS decimal(18,4)) /
                    NULLIF(COUNT(CASE WHEN t.TransactionType = 'Registration' THEN 1 END), 0) * 100,
                CAST(COUNT(CASE WHEN t.TransactionType = 'RewardGranted' THEN 1 END) AS decimal(18,4)) /
                    NULLIF(COUNT(CASE WHEN t.TransactionType = 'Registration' THEN 1 END), 0) * 100,
                CAST(COUNT(CASE WHEN t.TransactionType = 'RewardReversal' THEN 1 END) AS decimal(18,4)) /
                    NULLIF(COUNT(CASE WHEN t.TransactionType = 'RewardGranted' THEN 1 END), 0) * 100,
                COUNT(CASE WHEN t.TransactionType = 'RewardReversal' THEN 1 END)
            FROM dbo.ReferralTransactions t
            WHERE t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR EXISTS (
                  SELECT 1 FROM dbo.Customers c
                  WHERE c.CustomerID = t.ReferrerCustomerId
                    AND (c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)
              ) OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc
                  WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search
              ));

            SELECT TOP (10) t.ReferrerCustomerId, c.CustomerCode, c.CustomerName, COUNT(*)
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE t.TransactionType = 'Registration' AND t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR c.CustomerName LIKE @search OR c.CustomerCode LIKE @search OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search))
            GROUP BY t.ReferrerCustomerId, c.CustomerCode, c.CustomerName
            ORDER BY COUNT(*) DESC, t.ReferrerCustomerId;

            SELECT TOP (10) rc.ReferralCodeId, rc.Code, rc.CustomerId, c.CustomerName, COUNT(t.ReferralTransactionId)
            FROM dbo.ReferralCodes rc
            LEFT JOIN dbo.Customers c ON c.CustomerID = rc.CustomerId
            LEFT JOIN dbo.ReferralTransactions t ON t.ReferralCodeId = rc.ReferralCodeId
                AND t.TransactionType = 'Registration' AND t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
            WHERE (@search IS NULL OR rc.Code LIKE @search OR c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)
            GROUP BY rc.ReferralCodeId, rc.Code, rc.CustomerId, c.CustomerName
            ORDER BY COUNT(t.ReferralTransactionId) DESC, rc.ReferralCodeId;

            SELECT TOP (10) t.ReferrerCustomerId, c.CustomerCode, c.CustomerName,
                SUM(t.LoyaltyPoints), COUNT(*)
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE t.TransactionType = 'RewardGranted' AND t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR c.CustomerName LIKE @search OR c.CustomerCode LIKE @search OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search))
            GROUP BY t.ReferrerCustomerId, c.CustomerCode, c.CustomerName
            ORDER BY SUM(t.LoyaltyPoints) DESC, t.ReferrerCustomerId;

            SELECT
                COUNT(CASE WHEN t.TransactionType = 'Registration' AND t.CreatedAt >= CONVERT(date, SYSUTCDATETIME()) THEN 1 END),
                COUNT(CASE WHEN t.TransactionType = 'Registration' AND t.CreatedAt >= DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME())) THEN 1 END),
                COUNT(CASE WHEN t.TransactionType = 'Registration' AND t.CreatedAt >= DATEADD(month, -1, CONVERT(date, SYSUTCDATETIME())) THEN 1 END)
            FROM dbo.ReferralTransactions t
            WHERE t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR EXISTS (
                  SELECT 1 FROM dbo.Customers c WHERE c.CustomerID = t.ReferrerCustomerId
                    AND (c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)
              ) OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search
              ));

            SELECT CAST(t.CreatedAt AS date), COUNT(*)
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE t.TransactionType = 'Registration' AND t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR c.CustomerName LIKE @search OR c.CustomerCode LIKE @search OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search))
            GROUP BY CAST(t.CreatedAt AS date)
            ORDER BY CAST(t.CreatedAt AS date);

            SELECT t.ReferrerCustomerId, t.ReferredCustomerId
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE t.TransactionType = 'Registration' AND t.ReferredCustomerId IS NOT NULL
              AND t.CreatedAt >= @from AND t.CreatedAt < @toExclusive
              AND (@search IS NULL OR c.CustomerName LIKE @search OR c.CustomerCode LIKE @search OR EXISTS (
                  SELECT 1 FROM dbo.ReferralCodes rc WHERE rc.ReferralCodeId = t.ReferralCodeId AND rc.Code LIKE @search));";

        var fromValue = from?.Date ?? new DateTime(1753, 1, 1);
        var toExclusive = to is null ? DateTime.MaxValue : to.Value.Date.AddDays(1);
        var searchValue = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        var edges = new List<ReferralRegistrationEdge>();
        ReferralAnalyticsDto result;

        await using (var connection = connections.Create())
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@from", fromValue);
            command.Parameters.AddWithValue("@toExclusive", toExclusive);
            command.Parameters.AddWithValue("@search", (object?)searchValue ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            await reader.ReadAsync(cancellationToken);
            var overview = new
            {
                Registrations = reader.GetInt32(0), Referred = reader.GetInt32(1),
                Rewards = reader.GetInt32(2), Points = reader.GetDecimal(3),
                Average = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
            };

            await reader.NextResultAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var quality = new ReferralAnalyticsQualityDto(
                reader.IsDBNull(0) ? 0 : reader.GetDecimal(0),
                reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                reader.IsDBNull(3) ? 0 : reader.GetDecimal(3), reader.GetInt32(4));

            await reader.NextResultAsync(cancellationToken);
            var referrers = new List<ReferralAnalyticsReferrerDto>();
            while (await reader.ReadAsync(cancellationToken))
                referrers.Add(new(reader.GetInt32(0), reader[1] as string, reader[2] as string, reader.GetInt32(3)));

            await reader.NextResultAsync(cancellationToken);
            var codes = new List<ReferralAnalyticsCodeDto>();
            while (await reader.ReadAsync(cancellationToken))
                codes.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader[3] as string, reader.GetInt32(4)));

            await reader.NextResultAsync(cancellationToken);
            var rewardCustomers = new List<ReferralAnalyticsRewardCustomerDto>();
            while (await reader.ReadAsync(cancellationToken))
                rewardCustomers.Add(new(reader.GetInt32(0), reader[1] as string, reader[2] as string, reader.GetDecimal(3), reader.GetInt32(4)));

            await reader.NextResultAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var activity = new ReferralAnalyticsActivityDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), []);

            await reader.NextResultAsync(cancellationToken);
            var daily = new List<ReferralAnalyticsPeriodDto>();
            while (await reader.ReadAsync(cancellationToken))
                daily.Add(new(reader.GetDateTime(0), reader.GetInt32(1)));
            activity = activity with { DailyRegistrations = daily };

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                edges.Add(new(reader.GetInt32(0), reader.GetInt32(1)));

            var tree = BuildAnalyticsTree(edges);
            var identities = await GetCustomerIdentityMapAsync(
                tree.LargestNetworkCustomerId is int id ? [id] : [], cancellationToken);
            result = new ReferralAnalyticsDto(
                overview.Registrations, overview.Referred, overview.Rewards, overview.Points,
                overview.Average, referrers, codes, rewardCustomers, quality, activity,
                tree with { LargestNetworkCustomerName = tree.LargestNetworkCustomerId is int largest && identities.TryGetValue(largest, out var identity) ? identity.CustomerName : null });
        }

        return result;
    }

    private static ReferralAnalyticsTreeDto BuildAnalyticsTree(IReadOnlyList<ReferralRegistrationEdge> edges)
    {
        var children = edges.GroupBy(edge => edge.ReferrerCustomerId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ReferredCustomerId).Distinct().ToArray());
        var roots = edges.Select(edge => edge.ReferrerCustomerId).ToHashSet()
            .Except(edges.Select(edge => edge.ReferredCustomerId).ToHashSet()).ToArray();
        var largestId = (int?)null;
        var largestSize = 0;
        var maxDepth = 0;
        foreach (var root in roots)
        {
            var visited = new HashSet<int> { root };
            var queue = new Queue<(int Id, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                maxDepth = Math.Max(maxDepth, current.Depth);
                if (!children.TryGetValue(current.Id, out var childIds)) continue;
                foreach (var child in childIds)
                    if (visited.Add(child)) queue.Enqueue((child, current.Depth + 1));
            }
            if (visited.Count > largestSize) { largestSize = visited.Count; largestId = root; }
        }
        return new ReferralAnalyticsTreeDto(roots.Length, maxDepth, largestSize, largestId, null);
    }

    private static int MaxDirectReferrals(IReadOnlyList<ReferralTreeNodeDto> nodes)
    {
        var maximum = 0;
        foreach (var node in nodes)
        {
            maximum = Math.Max(maximum, node.DirectChildrenCount);
            maximum = Math.Max(maximum, MaxDirectReferrals(node.Children));
        }

        return maximum;
    }

    public async Task<IReadOnlyList<ReferralDashboardSearchResultDto>> SearchDashboardAsync(string query, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT TOP (20) c.CustomerID, c.CustomerCode, c.CustomerName, rc.Code,
                CASE WHEN rc.ReferralCodeId IS NULL THEN 'customer' ELSE 'code' END AS ResultType
            FROM dbo.Customers c
            LEFT JOIN dbo.ReferralCodes rc ON rc.CustomerId = c.CustomerID AND rc.IsActive = 1
            WHERE c.CustomerName LIKE @query OR c.CustomerCode LIKE @query OR rc.Code LIKE @query
            ORDER BY c.CustomerName, c.CustomerID, rc.Code";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@query", $"%{query.Trim()}%");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ReferralDashboardSearchResultDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ReferralDashboardSearchResultDto(
                reader.GetInt32(0),
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader[3] is DBNull ? null : reader.GetString(3),
                reader.GetString(4)));
        }

        return results;
    }

    public async Task<ReferralRewardsScreenDto> GetRewardsScreenAsync(
        string? transactionType,
        DateTime? from,
        DateTime? to,
        int? customerId,
        string? search,
        CancellationToken cancellationToken)
    {
        var filters = new List<string>
        {
            "t.TransactionType IN ('RewardGranted', 'RewardReversal')"
        };

        if (!string.IsNullOrWhiteSpace(transactionType)) filters.Add("t.TransactionType = @transactionType");
        if (from.HasValue) filters.Add("t.CreatedAt >= @from");
        if (to.HasValue) filters.Add("t.CreatedAt < @toExclusive");
        if (customerId.HasValue) filters.Add("t.ReferrerCustomerId = @customerId");
        if (!string.IsNullOrWhiteSpace(search)) filters.Add("(c.CustomerName LIKE @search OR c.CustomerCode LIKE @search)");

        var where = string.Join(" AND ", filters);
        var grantedWhere = $"{where} AND t.TransactionType = 'RewardGranted'";
        var sql = $@"
            SELECT
                SUM(CASE WHEN t.TransactionType = 'RewardGranted' THEN 1 ELSE 0 END),
                SUM(CASE WHEN t.TransactionType = 'RewardReversal' THEN 1 ELSE 0 END),
                COALESCE(SUM(CASE WHEN t.TransactionType = 'RewardGranted' THEN t.LoyaltyPoints ELSE 0 END), 0),
                COUNT(DISTINCT CASE WHEN t.TransactionType = 'RewardGranted' THEN t.ReferrerCustomerId END)
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE {where};

            SELECT TOP (10) t.ReferrerCustomerId, c.CustomerCode, c.CustomerName,
                SUM(t.LoyaltyPoints) AS TotalPoints, COUNT(*) AS EventCount
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            WHERE {grantedWhere}
            GROUP BY t.ReferrerCustomerId, c.CustomerCode, c.CustomerName
            ORDER BY SUM(t.LoyaltyPoints) DESC, t.ReferrerCustomerId;

            SELECT t.ReferralTransactionId, t.ReferrerCustomerId, c.CustomerCode, c.CustomerName,
                t.ReferredCustomerId, referred.CustomerName, t.TransactionType, t.FixedRewardAmount,
                t.LoyaltyPoints, t.OrderId, t.Notes, t.CreatedAt
            FROM dbo.ReferralTransactions t
            LEFT JOIN dbo.Customers c ON c.CustomerID = t.ReferrerCustomerId
            LEFT JOIN dbo.Customers referred ON referred.CustomerID = t.ReferredCustomerId
            WHERE {where}
            ORDER BY t.CreatedAt DESC, t.ReferralTransactionId DESC;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(transactionType)) command.Parameters.AddWithValue("@transactionType", transactionType);
        if (from.HasValue) command.Parameters.AddWithValue("@from", from.Value);
        if (to.HasValue) command.Parameters.AddWithValue("@toExclusive", to.Value.Date.AddDays(1));
        if (customerId.HasValue) command.Parameters.AddWithValue("@customerId", customerId.Value);
        if (!string.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("@search", $"%{search.Trim()}%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to read referral rewards.");
        var grantedCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
        var reversalCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        var totalGrantedPoints = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
        var beneficiaryCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

        await reader.NextResultAsync(cancellationToken);
        var beneficiaries = new List<ReferralRewardBeneficiaryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            beneficiaries.Add(new ReferralRewardBeneficiaryDto(
                reader.GetInt32(0),
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetInt32(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        var events = new List<ReferralRewardEventDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ReferralRewardEventDto(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader[3] is DBNull ? null : reader.GetString(3),
                reader[4] is DBNull ? null : reader.GetInt32(4),
                reader[5] is DBNull ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader[9] is DBNull ? null : reader.GetInt32(9),
                reader[10] is DBNull ? null : reader.GetString(10),
                reader.GetDateTime(11)));
        }

        return new ReferralRewardsScreenDto(
            grantedCount,
            reversalCount,
            totalGrantedPoints,
            beneficiaryCount,
            beneficiaries,
            events);
    }

    public async Task<IReadOnlyList<ReferralCodeDto>> GetCodesByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT ReferralCodeId, CustomerId, Code, IsActive, CreatedAt, LastUsedAt FROM dbo.ReferralCodes WHERE CustomerId = @customerId ORDER BY CreatedAt DESC";
        return await QueryAsync(sql, reader => new ReferralCodeDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetDateTime(4),
            reader[5] is DBNull ? null : reader.GetDateTime(5)), customerId, cancellationToken);
    }

    public async Task<ReferralAccountSummaryDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT ra.ReferralAccountId, ra.CustomerId, ra.ReferralCodeId, rc.Code AS ReferralCode, rc.IsActive AS ReferralCodeIsActive,
                ra.TotalReferrals, ra.SuccessfulReferrals, ra.TotalRewardsAmount, ra.TotalRewardPoints, ra.CreatedAt, ra.UpdatedAt
            FROM dbo.ReferralAccounts ra
            LEFT JOIN dbo.ReferralCodes rc ON rc.ReferralCodeId = ra.ReferralCodeId
            WHERE ra.CustomerId = @customerId";

        var results = await QueryAsync(sql, reader => new ReferralAccountSummaryDto
        {
            CustomerId = reader.GetInt32(1),
            ReferralCodeId = reader[2] is DBNull ? null : reader.GetInt32(2),
            ReferralCode = reader[3] is DBNull ? null : reader.GetString(3),
            ReferralCodeIsActive = reader[4] is DBNull ? false : reader.GetBoolean(4),
            TotalReferrals = reader.GetInt32(5),
            SuccessfulReferrals = reader.GetInt32(6),
            TotalRewardsAmount = reader.GetDecimal(7),
            TotalRewardPoints = reader.GetDecimal(8),
            CreatedAt = reader.GetDateTime(9),
            UpdatedAt = reader.GetDateTime(10)
        }, customerId, cancellationToken);

        return results.SingleOrDefault();
    }

    public async Task<ReferralAccountSummaryDto> UpsertAccountAsync(ReferralAccountSummaryDto account, CancellationToken cancellationToken)
    {
        const string sql = @"IF EXISTS (SELECT 1 FROM dbo.ReferralAccounts WHERE CustomerId = @customerId)
            BEGIN
                UPDATE dbo.ReferralAccounts
                SET ReferralCodeId = @referralCodeId,
                    TotalReferrals = @totalReferrals,
                    SuccessfulReferrals = @successfulReferrals,
                    TotalRewardsAmount = COALESCE((SELECT SUM(FixedRewardAmount) FROM dbo.ReferralTransactions WHERE ReferrerCustomerId = @customerId AND TransactionType = 'RewardGranted'), 0),
                    TotalRewardPoints = COALESCE((SELECT SUM(LoyaltyPoints) FROM dbo.ReferralTransactions WHERE ReferrerCustomerId = @customerId AND TransactionType = 'RewardGranted'), 0),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE CustomerId = @customerId;
                SELECT ra.ReferralAccountId, ra.CustomerId, ra.ReferralCodeId, rc.Code AS ReferralCode, rc.IsActive AS ReferralCodeIsActive,
                    ra.TotalReferrals, ra.SuccessfulReferrals, ra.TotalRewardsAmount, ra.TotalRewardPoints, ra.CreatedAt, ra.UpdatedAt
                FROM dbo.ReferralAccounts ra
                LEFT JOIN dbo.ReferralCodes rc ON rc.ReferralCodeId = ra.ReferralCodeId
                WHERE ra.CustomerId = @customerId;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.ReferralAccounts (CustomerId, ReferralCodeId, TotalReferrals, SuccessfulReferrals, TotalRewardsAmount, TotalRewardPoints, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.ReferralAccountId, INSERTED.CustomerId, INSERTED.ReferralCodeId, NULL, 1, INSERTED.TotalReferrals, INSERTED.SuccessfulReferrals, INSERTED.TotalRewardsAmount, INSERTED.TotalRewardPoints, INSERTED.CreatedAt, INSERTED.UpdatedAt
                VALUES (@customerId, @referralCodeId, @totalReferrals, @successfulReferrals,
                    COALESCE((SELECT SUM(FixedRewardAmount) FROM dbo.ReferralTransactions WHERE ReferrerCustomerId = @customerId AND TransactionType = 'RewardGranted'), 0),
                    COALESCE((SELECT SUM(LoyaltyPoints) FROM dbo.ReferralTransactions WHERE ReferrerCustomerId = @customerId AND TransactionType = 'RewardGranted'), 0),
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            END";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", account.CustomerId);
        command.Parameters.AddWithValue("@referralCodeId", (object?)account.ReferralCodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@totalReferrals", account.TotalReferrals);
        command.Parameters.AddWithValue("@successfulReferrals", account.SuccessfulReferrals);
        command.Parameters.AddWithValue("@totalRewardsAmount", account.TotalRewardsAmount);
        command.Parameters.AddWithValue("@totalRewardPoints", account.TotalRewardPoints);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to upsert referral account.");

        return new ReferralAccountSummaryDto
        {
            CustomerId = reader.GetInt32(1),
            ReferralCodeId = reader[2] is DBNull ? null : reader.GetInt32(2),
            ReferralCode = reader[3] is DBNull ? null : reader.GetString(3),
            ReferralCodeIsActive = reader[4] is DBNull ? false : reader.GetBoolean(4),
            TotalReferrals = reader.GetInt32(5),
            SuccessfulReferrals = reader.GetInt32(6),
            TotalRewardsAmount = reader.GetDecimal(7),
            TotalRewardPoints = reader.GetDecimal(8),
            CreatedAt = reader.GetDateTime(9),
            UpdatedAt = reader.GetDateTime(10)
        };
    }

    public async Task<IReadOnlyList<ReferralTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT ReferralTransactionId, ReferrerCustomerId, ReferredCustomerId, ReferralCodeId, OrderId, ReferralRewardId,
                TransactionType, FixedRewardAmount, LoyaltyPoints, Notes, CreatedAt
            FROM dbo.ReferralTransactions
            WHERE ReferrerCustomerId = @customerId OR ReferredCustomerId = @customerId
            ORDER BY CreatedAt DESC";

        return await QueryAsync(sql, reader => new ReferralTransactionDto(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader[2] is DBNull ? null : reader.GetInt32(2),
            reader[3] is DBNull ? null : reader.GetInt32(3),
            reader[4] is DBNull ? null : reader.GetInt32(4),
            reader[5] is DBNull ? null : reader.GetInt32(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader[9] is DBNull ? null : reader.GetString(9),
            reader.GetDateTime(10)), customerId, cancellationToken);
    }

    public async Task<ReferralTransactionDto> CreateRewardGrantedAsync(int referrerCustomerId, int referredCustomerId, int? referralCodeId, int orderId, decimal fixedRewardAmount, decimal loyaltyPoints, string notes, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO dbo.ReferralTransactions (ReferrerCustomerId, ReferredCustomerId, ReferralCodeId, OrderId, TransactionType, FixedRewardAmount, LoyaltyPoints, Notes, CreatedAt)
            OUTPUT INSERTED.ReferralTransactionId, INSERTED.ReferrerCustomerId, INSERTED.ReferredCustomerId, INSERTED.ReferralCodeId, INSERTED.OrderId, INSERTED.ReferralRewardId, INSERTED.TransactionType, INSERTED.FixedRewardAmount, INSERTED.LoyaltyPoints, INSERTED.Notes, INSERTED.CreatedAt
            VALUES (@referrerCustomerId, @referredCustomerId, @referralCodeId, @orderId, 'RewardGranted', @fixedRewardAmount, @loyaltyPoints, @notes, SYSUTCDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@referrerCustomerId", referrerCustomerId);
        command.Parameters.AddWithValue("@referredCustomerId", referredCustomerId);
        command.Parameters.AddWithValue("@referralCodeId", (object?)referralCodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@fixedRewardAmount", fixedRewardAmount);
        command.Parameters.AddWithValue("@loyaltyPoints", loyaltyPoints);
        command.Parameters.AddWithValue("@notes", notes);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to create referral reward record.");

        return new ReferralTransactionDto(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader[2] is DBNull ? null : reader.GetInt32(2),
            reader[3] is DBNull ? null : reader.GetInt32(3),
            reader[4] is DBNull ? null : reader.GetInt32(4),
            reader[5] is DBNull ? null : reader.GetInt32(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader[9] is DBNull ? null : reader.GetString(9),
            reader.GetDateTime(10));
    }

    public async Task<ReferralCodeDto?> EnsureCodeAsync(int customerId, string? preferredCode, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(preferredCode) ? GenerateCode(customerId) : preferredCode.Trim();

        const string sql = @"IF EXISTS (SELECT 1 FROM dbo.ReferralCodes WHERE CustomerId = @customerId AND IsActive = 1)
            SELECT ReferralCodeId, CustomerId, Code, IsActive, CreatedAt, LastUsedAt FROM dbo.ReferralCodes WHERE CustomerId = @customerId AND IsActive = 1;
        ELSE
            BEGIN
                INSERT INTO dbo.ReferralCodes (CustomerId, Code, IsActive, CreatedAt) OUTPUT INSERTED.ReferralCodeId, INSERTED.CustomerId, INSERTED.Code, INSERTED.IsActive, INSERTED.CreatedAt, INSERTED.LastUsedAt VALUES (@customerId, @code, 1, SYSUTCDATETIME());
            END";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new ReferralCodeDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetDateTime(4),
            reader[5] is DBNull ? null : reader.GetDateTime(5));
    }

    public async Task<RegisterReferralResponse> RegisterAsync(RegisterReferralRequest request, CancellationToken cancellationToken)
    {
        if (request.ReferredCustomerId <= 0) throw new ArgumentException("ReferredCustomerId must be positive.");

        var codeId = await ResolveReferralCodeAsync(request.ReferralCode, cancellationToken);
        if (codeId is null) throw new ArgumentException("The referral code is invalid or inactive.");

        var referrerCustomerId = await GetCustomerIdFromReferralCodeAsync(codeId.Value, cancellationToken);
        if (referrerCustomerId is null) throw new ArgumentException("The referral code does not belong to a valid customer.");

        if (request.ReferredCustomerId == referrerCustomerId.Value) throw new ArgumentException("Self referral is not allowed.");

        var existing = await IsDuplicateRegistrationAsync(referrerCustomerId.Value, request.ReferredCustomerId, cancellationToken);
        if (existing) return new RegisterReferralResponse(0, referrerCustomerId.Value, request.ReferredCustomerId, codeId, "Registration", DateTime.UtcNow, true, "Duplicate registration skipped.");

        if (await WouldCreateCycleAsync(referrerCustomerId.Value, request.ReferredCustomerId, cancellationToken)) throw new ArgumentException("This referral would create a cycle.");

        const string insertSql = @"INSERT INTO dbo.ReferralTransactions (ReferrerCustomerId, ReferredCustomerId, ReferralCodeId, TransactionType, FixedRewardAmount, LoyaltyPoints, Notes, CreatedAt)
            OUTPUT INSERTED.ReferralTransactionId, INSERTED.ReferrerCustomerId, INSERTED.ReferredCustomerId, INSERTED.ReferralCodeId, INSERTED.TransactionType, INSERTED.CreatedAt
            VALUES (@referrerCustomerId, @referredCustomerId, @referralCodeId, 'Registration', 0, 0, @notes, SYSUTCDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(insertSql, connection);
        command.Parameters.AddWithValue("@referrerCustomerId", referrerCustomerId.Value);
        command.Parameters.AddWithValue("@referredCustomerId", request.ReferredCustomerId);
        command.Parameters.AddWithValue("@referralCodeId", codeId.Value);
        command.Parameters.AddWithValue("@notes", (object?)request.Notes ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Registration could not be saved.");

        var transactionId = reader.GetInt64(0);
        return new RegisterReferralResponse(
            transactionId,
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader[3] is DBNull ? null : reader.GetInt32(3),
            reader.GetString(4),
            reader.GetDateTime(5),
            false,
            "Registration created.");
    }

    public async Task<IReadOnlyList<ReferralRootDto>> GetRootsAsync(CancellationToken cancellationToken)
    {
        var registrationEdges = await GetRegistrationEdgesAsync(cancellationToken);
        var rootIds = ReferralTreeBuilder.DiscoverRootCustomerIds(registrationEdges);
        if (rootIds.Count == 0)
        {
            return [];
        }

        var customerMap = await GetCustomerIdentityMapAsync(rootIds.ToHashSet(), cancellationToken);
        var results = new List<ReferralRootDto>();

        foreach (var rootId in rootIds)
        {
            if (!customerMap.TryGetValue(rootId, out var identity))
            {
                continue;
            }

            var reachableCustomerIds = DiscoverReachableCustomerIds(rootId, registrationEdges);
            var relevantRegistrations = registrationEdges
                .Where(edge => reachableCustomerIds.Contains(edge.ReferrerCustomerId) && reachableCustomerIds.Contains(edge.ReferredCustomerId))
                .ToList();

            var tree = ReferralTreeBuilder.Build(rootId, relevantRegistrations, customerMap);
            var root = ToTreeNodeDto(tree.Root);

            results.Add(new ReferralRootDto(
                root.CustomerId,
                root.CustomerCode,
                root.CustomerName,
                root.DirectChildrenCount,
                root.TotalDescendantsCount,
                root.MaxDepth));
        }

        return results
            .OrderBy(x => x.CustomerId)
            .ToList();
    }

    public async Task<ReferralTreeDto?> GetTreeAsync(int customerId, CancellationToken cancellationToken)
    {
        var registrationEdges = await GetRegistrationEdgesAsync(cancellationToken);
        var reachableCustomerIds = DiscoverReachableCustomerIds(customerId, registrationEdges);
        var customerMap = await GetCustomerIdentityMapAsync(reachableCustomerIds, cancellationToken);

        if (!customerMap.ContainsKey(customerId))
        {
            return null;
        }

        var relevantRegistrations = registrationEdges
            .Where(edge => reachableCustomerIds.Contains(edge.ReferrerCustomerId) && reachableCustomerIds.Contains(edge.ReferredCustomerId))
            .ToList();

        var tree = ReferralTreeBuilder.Build(customerId, relevantRegistrations, customerMap);
        var root = ToTreeNodeDto(tree.Root);

        return new ReferralTreeDto(
            tree.RootCustomerId,
            root.CustomerCode,
            root.CustomerName,
            root.DirectChildrenCount,
            root.TotalDescendantsCount,
            root.MaxDepth,
            root.Children);
    }

    private static string GenerateCode(int customerId) => $"REF-{customerId}-{DateTime.UtcNow:yyMMddHHmmss}";

    private async Task<int?> ResolveReferralCodeAsync(string referralCode, CancellationToken cancellationToken)
    {
        const string sql = "SELECT ReferralCodeId FROM dbo.ReferralCodes WHERE Code = @code AND IsActive = 1";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", referralCode.Trim());
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value);
    }

    private async Task<int?> GetCustomerIdFromReferralCodeAsync(int referralCodeId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CustomerId FROM dbo.ReferralCodes WHERE ReferralCodeId = @referralCodeId";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@referralCodeId", referralCodeId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value);
    }

    private async Task<bool> IsDuplicateRegistrationAsync(int referrerCustomerId, int referredCustomerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.ReferralTransactions WHERE ReferrerCustomerId = @referrerCustomerId AND ReferredCustomerId = @referredCustomerId AND TransactionType = 'Registration') THEN 1 ELSE 0 END";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@referrerCustomerId", referrerCustomerId);
        command.Parameters.AddWithValue("@referredCustomerId", referredCustomerId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToInt32(result) == 1;
    }

    private async Task<bool> WouldCreateCycleAsync(int referrerCustomerId, int referredCustomerId, CancellationToken cancellationToken)
    {
        const string sql = @"WITH Ancestors AS (
                SELECT CustomerID, ParentCustomerId FROM dbo.Customers WHERE CustomerID = @referredCustomerId
                UNION ALL
                SELECT c.CustomerID, c.ParentCustomerId FROM dbo.Customers c INNER JOIN Ancestors a ON a.ParentCustomerId = c.CustomerID
            )
            SELECT CASE WHEN EXISTS (SELECT 1 FROM Ancestors WHERE CustomerID = @referrerCustomerId) THEN 1 ELSE 0 END";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@referrerCustomerId", referrerCustomerId);
        command.Parameters.AddWithValue("@referredCustomerId", referredCustomerId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToInt32(result) == 1;
    }

    private sealed record TreeRow(int CustomerId, string? CustomerCode, string? CustomerName, int? ParentCustomerId, int Level, int MaxDepth);

    private async Task<List<TreeRow>> GetExistingTreeRowsAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = @"WITH Tree AS (
                SELECT CustomerID, CustomerCode, CustomerName, ParentCustomerId, 0 AS Level
                FROM dbo.Customers
                WHERE CustomerID = @customerId
                UNION ALL
                SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.ParentCustomerId, Tree.Level + 1
                FROM dbo.Customers c
                INNER JOIN Tree ON c.ParentCustomerId = Tree.CustomerID
            )
            SELECT CustomerID, CustomerCode, CustomerName, ParentCustomerId, Level, Level AS MaxDepth FROM Tree ORDER BY Level, CustomerID";

        return await QueryAsync(sql, reader => new TreeRow(
            reader.GetInt32(0),
            reader[1] is DBNull ? null : reader.GetString(1),
            reader[2] is DBNull ? null : reader.GetString(2),
            reader[3] is DBNull ? null : reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5)), customerId, cancellationToken);
    }

    private static ReferralTreeNodeDto BuildTree(int customerId, IReadOnlyDictionary<int, TreeRow> rows)
    {
        var current = rows[customerId];
        var children = rows.Where(item => item.Value.ParentCustomerId == customerId)
            .OrderBy(item => item.Key)
            .Select(item => BuildTree(item.Key, rows))
            .ToList();

        var directChildren = children.Count;
        var totalDescendants = children.Count + children.Sum(child => CountDescendants(child));
        var maxDepth = children.Count == 0 ? current.Level : Math.Max(current.Level, children.Max(child => child.MaxDepth));

        return new ReferralTreeNodeDto(
            current.CustomerId,
            current.CustomerCode,
            current.CustomerName,
            current.ParentCustomerId,
            current.Level,
            children,
            directChildren,
            totalDescendants,
            maxDepth,
            null,
            true
        );
    }

    private static int CountDescendants(ReferralTreeNodeDto node)
    {
        return node.Children.Sum(child => 1 + CountDescendants(child));
    }

    private static ReferralTreeNodeDto ToTreeNodeDto(ReferralTreeNodeData node)
    {
        return new ReferralTreeNodeDto(
            node.CustomerId,
            node.CustomerCode,
            node.CustomerName,
            node.ParentCustomerId,
            node.Level,
            node.Children.Select(ToTreeNodeDto).ToList(),
            node.DirectChildrenCount,
            node.TotalDescendantsCount,
            node.MaxDepth,
            node.ReferralCode,
            node.IsActive);
    }

    private static HashSet<int> DiscoverReachableCustomerIds(int rootCustomerId, IReadOnlyCollection<ReferralRegistrationEdge> registrations)
    {
        var reachable = new HashSet<int> { rootCustomerId };
        var queue = new Queue<int>(new[] { rootCustomerId });

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in registrations)
            {
                if (edge.ReferrerCustomerId == current && reachable.Add(edge.ReferredCustomerId))
                {
                    queue.Enqueue(edge.ReferredCustomerId);
                }

                if (edge.ReferredCustomerId == current && reachable.Add(edge.ReferrerCustomerId))
                {
                    queue.Enqueue(edge.ReferrerCustomerId);
                }
            }
        }

        return reachable;
    }

    private async Task<IReadOnlyList<ReferralRegistrationEdge>> GetRegistrationEdgesAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT ReferrerCustomerId, ReferredCustomerId FROM dbo.ReferralTransactions WHERE TransactionType = 'Registration' ORDER BY CreatedAt, ReferralTransactionId";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ReferralRegistrationEdge>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ReferralRegistrationEdge(reader.GetInt32(0), reader.GetInt32(1)));
        }

        return results;
    }

    private async Task<IReadOnlyDictionary<int, CustomerIdentity>> GetCustomerIdentityMapAsync(IReadOnlyCollection<int> customerIds, CancellationToken cancellationToken)
    {
        var orderedIds = customerIds.Distinct().OrderBy(id => id).ToArray();
        if (orderedIds.Length == 0)
        {
            return new Dictionary<int, CustomerIdentity>();
        }

        var parameterNames = orderedIds.Select((_, index) => $"@p{index}").ToArray();
        var sql = $@"SELECT c.CustomerID, c.CustomerCode, c.CustomerName, c.IsActive, rc.Code AS ReferralCode
                    FROM dbo.Customers c
                    LEFT JOIN dbo.ReferralCodes rc ON rc.CustomerId = c.CustomerId AND rc.IsActive = 1
                    WHERE c.CustomerID IN ({string.Join(", ", parameterNames)})";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        for (var i = 0; i < orderedIds.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", orderedIds[i]);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new Dictionary<int, CustomerIdentity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var customerId = reader.GetInt32(0);
            results[customerId] = new CustomerIdentity(
                customerId,
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader.GetBoolean(3),
                reader[4] is DBNull ? null : reader.GetString(4));
        }

        return results;
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int customerId, CancellationToken cancellationToken)
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
