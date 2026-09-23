using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class VipLevelEvaluationRepository(
    ReadOnlySqlConnectionFactory connections,
    OperationalSqlConnectionFactory operationalConnections) : IVipLevelEvaluationRepository
{
    public async Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    v.VipLevelId,
    v.Code,
    v.DisplayName,
    v.Priority,
    c.MinimumDirectReferrals,
    c.MinimumOwnOrders,
    c.MinimumNetworkOrders,
    c.MinimumNetworkSize,
    c.MinimumScore,
    c.DirectReferralWeight,
    c.OwnOrderWeight,
    c.NetworkOrderWeight,
    c.NetworkSizeWeight,
    v.IsActive
FROM dbo.VipLevelEvaluationCriteria c
INNER JOIN dbo.VipLevels v ON v.VipLevelId = c.VipLevelId
ORDER BY v.Priority, v.VipLevelId;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<VipLevelEvaluationCriteriaDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new VipLevelEvaluationCriteriaDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                reader.GetBoolean(13)));
        }

        return results;
    }

    public async Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    c.CustomerID,
    c.CustomerCode,
    c.CustomerName,
    la.VipLevelId,
    v.Code,
    v.DisplayName,
    v.Priority,
    ISNULL(la.VipLevelScore, 0),
    ISNULL(la.VipDirectReferralCount, 0),
    ISNULL(la.VipOwnOrderCount, 0),
    ISNULL(la.VipNetworkOrderCount, 0),
    ISNULL(la.VipNetworkSize, 0),
    ISNULL(la.VipNetworkMaxDepth, 0),
    la.VipLevelEvaluatedAtUtc
FROM dbo.LoyaltyAccounts la
INNER JOIN dbo.Customers c ON c.CustomerID = la.CustomerId
INNER JOIN dbo.VipLevels v ON v.VipLevelId = la.VipLevelId
WHERE la.VipLevelId IS NOT NULL
  AND la.VipLevelEvaluatedAtUtc IS NOT NULL
ORDER BY v.Priority DESC,
         ISNULL(la.VipLevelScore, 0) DESC,
         ISNULL(la.VipNetworkSize, 0) DESC,
         c.CustomerName,
         c.CustomerID;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<VipCustomerListItemDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new VipCustomerListItemDto(
                reader.GetInt32(0),
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetDecimal(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetDateTime(13)));
        }

        return results;
    }

    public async Task<IReadOnlyList<VipLevelEvaluationSourceDto>> GetSourceRowsAsync(int? customerId, CancellationToken cancellationToken)
    {
        const string sql = @"
;WITH RegistrationCandidates AS
(
    SELECT
        ReferrerCustomerId,
        ReferredCustomerId,
        ROW_NUMBER() OVER (PARTITION BY ReferredCustomerId ORDER BY CreatedAt, ReferralTransactionId) AS RegistrationRank
    FROM dbo.ReferralTransactions
    WHERE TransactionType = N'Registration'
      AND ReferredCustomerId IS NOT NULL
),
Edges AS
(
    SELECT ReferrerCustomerId, ReferredCustomerId
    FROM RegistrationCandidates
    WHERE RegistrationRank = 1
),
ReferralTree AS
(
    SELECT la.CustomerId AS RootCustomerId, la.CustomerId, 0 AS Depth
    FROM dbo.LoyaltyAccounts la
    WHERE @customerId IS NULL OR la.CustomerId = @customerId

    UNION ALL

    SELECT tree.RootCustomerId, edges.ReferredCustomerId, tree.Depth + 1
    FROM ReferralTree tree
    INNER JOIN Edges edges ON edges.ReferrerCustomerId = tree.CustomerId
    WHERE tree.Depth < 100
),
TreeMetrics AS
(
    SELECT
        RootCustomerId,
        SUM(CASE WHEN Depth = 1 THEN 1 ELSE 0 END) AS DirectReferralCount,
        SUM(CASE WHEN Depth = 1 THEN 1 ELSE 0 END) AS Level1ReferralCount,
        SUM(CASE WHEN Depth = 2 THEN 1 ELSE 0 END) AS Level2ReferralCount,
        SUM(CASE WHEN Depth = 3 THEN 1 ELSE 0 END) AS Level3ReferralCount,
        SUM(CASE WHEN Depth = 4 THEN 1 ELSE 0 END) AS Level4ReferralCount,
        SUM(CASE WHEN Depth >= 1 THEN 1 ELSE 0 END) AS NetworkSize,
        MAX(Depth) AS NetworkMaxDepth
    FROM ReferralTree
    GROUP BY RootCustomerId
),
OwnOrders AS
(
    SELECT CustomerID, COUNT(*) AS OwnOrderCount
    FROM dbo.Orders
    WHERE ISNULL(OrderStatus, N'') <> N'Cancelled'
    GROUP BY CustomerID
),
NetworkOrders AS
(
    SELECT tree.RootCustomerId, COUNT(orderRow.OrderID) AS NetworkOrderCount
    FROM ReferralTree tree
    INNER JOIN dbo.Orders orderRow ON orderRow.CustomerID = tree.CustomerId
        AND tree.Depth >= 1
        AND ISNULL(orderRow.OrderStatus, N'') <> N'Cancelled'
    GROUP BY tree.RootCustomerId
)
SELECT
    la.CustomerId,
    c.CustomerCode,
    c.CustomerName,
    la.VipLevelId,
    currentLevel.Code,
    currentLevel.DisplayName,
    la.VipLevelEvaluatedAtUtc,
    la.VipLevelReason,
    ISNULL(metrics.DirectReferralCount, 0),
    ISNULL(metrics.Level1ReferralCount, 0),
    ISNULL(metrics.Level2ReferralCount, 0),
    ISNULL(metrics.Level3ReferralCount, 0),
    ISNULL(metrics.Level4ReferralCount, 0),
    ISNULL(metrics.NetworkSize, 0),
    ISNULL(metrics.NetworkMaxDepth, 0),
    ISNULL(ownOrders.OwnOrderCount, 0),
    ISNULL(networkOrders.NetworkOrderCount, 0)
FROM dbo.LoyaltyAccounts la
INNER JOIN dbo.Customers c ON c.CustomerID = la.CustomerId
LEFT JOIN dbo.VipLevels currentLevel ON currentLevel.VipLevelId = la.VipLevelId
LEFT JOIN TreeMetrics metrics ON metrics.RootCustomerId = la.CustomerId
LEFT JOIN OwnOrders ownOrders ON ownOrders.CustomerID = la.CustomerId
LEFT JOIN NetworkOrders networkOrders ON networkOrders.RootCustomerId = la.CustomerId
WHERE @customerId IS NULL OR la.CustomerId = @customerId
ORDER BY la.CustomerId
OPTION (MAXRECURSION 100);";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@customerId", System.Data.SqlDbType.Int)
        {
            Value = (object?)customerId ?? DBNull.Value,
        });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<VipLevelEvaluationSourceDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new VipLevelEvaluationSourceDto(
                reader.GetInt32(0),
                reader[1] is DBNull ? null : reader.GetString(1),
                reader[2] is DBNull ? null : reader.GetString(2),
                reader[3] is DBNull ? null : reader.GetInt32(3),
                reader[4] is DBNull ? null : reader.GetString(4),
                reader[5] is DBNull ? null : reader.GetString(5),
                reader[6] is DBNull ? null : reader.GetDateTime(6),
                reader[7] is DBNull ? null : reader.GetString(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                reader.GetInt32(16)));
        }

        return results;
    }

    public async Task<IReadOnlyList<int>> GetAffectedCustomerIdsAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = @"
;WITH RegistrationCandidates AS
(
    SELECT
        ReferrerCustomerId,
        ReferredCustomerId,
        ROW_NUMBER() OVER (PARTITION BY ReferredCustomerId ORDER BY CreatedAt, ReferralTransactionId) AS RegistrationRank
    FROM dbo.ReferralTransactions
    WHERE TransactionType = N'Registration'
      AND ReferredCustomerId IS NOT NULL
),
Edges AS
(
    SELECT ReferrerCustomerId, ReferredCustomerId
    FROM RegistrationCandidates
    WHERE RegistrationRank = 1
),
Affected AS
(
    SELECT @customerId AS CustomerId
    UNION ALL
    SELECT edges.ReferrerCustomerId
    FROM Edges edges
    INNER JOIN Affected affected ON affected.CustomerId = edges.ReferredCustomerId
)
SELECT DISTINCT CustomerId FROM Affected OPTION (MAXRECURSION 100);";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<int>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(reader.GetInt32(0));
        return results;
    }

    public async Task SaveEvaluationAsync(
        int customerId,
        int vipLevelId,
        int directReferralCount,
        int networkSize,
        int networkMaxDepth,
        int ownOrderCount,
        int networkOrderCount,
        decimal score,
        string reason,
        DateTime evaluatedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE dbo.LoyaltyAccounts
SET VipLevelId = @vipLevelId,
    VipDirectReferralCount = @directReferralCount,
    VipOwnOrderCount = @ownOrderCount,
    VipNetworkOrderCount = @networkOrderCount,
    VipNetworkSize = @networkSize,
    VipNetworkMaxDepth = @networkMaxDepth,
    VipLevelScore = @score,
    VipLevelReason = @reason,
    VipLevelEvaluatedAtUtc = @evaluatedAtUtc,
    UpdatedAt = @evaluatedAtUtc
WHERE CustomerId = @customerId;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@vipLevelId", vipLevelId);
        command.Parameters.AddWithValue("@directReferralCount", directReferralCount);
        command.Parameters.AddWithValue("@ownOrderCount", ownOrderCount);
        command.Parameters.AddWithValue("@networkOrderCount", networkOrderCount);
        command.Parameters.AddWithValue("@networkSize", networkSize);
        command.Parameters.AddWithValue("@networkMaxDepth", networkMaxDepth);
        command.Parameters.AddWithValue("@score", score);
        command.Parameters.AddWithValue("@reason", reason);
        command.Parameters.AddWithValue("@evaluatedAtUtc", evaluatedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
