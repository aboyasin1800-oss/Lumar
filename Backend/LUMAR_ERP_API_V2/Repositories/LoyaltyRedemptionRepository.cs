using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class LoyaltyRedemptionRepository(
    ReadOnlySqlConnectionFactory readOnlyConnections,
    OperationalSqlConnectionFactory operationalConnections) : ILoyaltyRedemptionRepository
{
    public async Task<LoyaltyRedemptionHistoryDto> GetHistoryAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT lt.LoyaltyTransactionId, lt.TransactionType, lt.CustomerId,
                c.CustomerCode, c.CustomerName, lt.OrderId, o.OrderNumber, lt.Points,
                lt.BalanceBefore, lt.BalanceAfter, lt.Source, lt.Notes, lt.CreatedAt,
                lr.LoyaltyRedemptionId, lr.PointsRedeemed, lr.PointMonetaryValue, lr.CreditAmount,
                lr.ReferenceNumber, lr.ReversedAtUtc, lr.ReversalLoyaltyTransactionId,
                cle.CustomerLedgerEntryId, cle.ReferenceNumber
            FROM dbo.LoyaltyTransactions lt
            LEFT JOIN dbo.Customers c ON c.CustomerID = lt.CustomerId
            LEFT JOIN dbo.Orders o ON o.OrderID = lt.OrderId
            OUTER APPLY (
                SELECT TOP (1) r.LoyaltyRedemptionId, r.PointsRedeemed, r.PointMonetaryValue,
                    r.CreditAmount, r.ReferenceNumber, r.ReversedAtUtc, r.ReversalLoyaltyTransactionId
                FROM dbo.LoyaltyRedemptions r
                WHERE r.LoyaltyTransactionId = lt.LoyaltyTransactionId
                   OR r.ReversalLoyaltyTransactionId = lt.LoyaltyTransactionId
                ORDER BY r.LoyaltyRedemptionId DESC
            ) lr
            OUTER APPLY (
                SELECT TOP (1) e.CustomerLedgerEntryId, e.ReferenceNumber
                FROM dbo.CustomerLedgerEntries e
                WHERE lr.LoyaltyRedemptionId IS NOT NULL
                  AND e.ReferenceNumber LIKE CONCAT(N'LoyaltyCredit:', lr.LoyaltyRedemptionId, N':%')
                ORDER BY e.CreatedAt DESC, e.CustomerLedgerEntryId DESC
            ) cle
            WHERE lt.TransactionType IN (N'Redeem', N'Reversal')
              AND (@transactionType IS NULL OR lt.TransactionType = @transactionType)
              AND (@customerId IS NULL OR lt.CustomerId = @customerId)
              AND (@from IS NULL OR lt.CreatedAt >= CONVERT(date, @from))
              AND (@to IS NULL OR lt.CreatedAt < DATEADD(day, 1, CONVERT(date, @to)))
                AND (@search IS NULL OR c.CustomerName LIKE CONCAT(N'%', @search, N'%')
                   OR c.CustomerCode LIKE CONCAT(N'%', @search, N'%')
                    OR c.PhoneNumber LIKE CONCAT(N'%', @search, N'%')
                    OR o.OrderNumber LIKE CONCAT(N'%', @search, N'%')
                   OR CONVERT(nvarchar(30), lt.CustomerId) LIKE CONCAT(N'%', @search, N'%')
                   OR CONVERT(nvarchar(30), lt.OrderId) LIKE CONCAT(N'%', @search, N'%')
                   OR CONVERT(nvarchar(30), lt.LoyaltyTransactionId) LIKE CONCAT(N'%', @search, N'%')
                   OR lr.ReferenceNumber LIKE CONCAT(N'%', @search, N'%'))
            ORDER BY lt.CreatedAt DESC, lt.LoyaltyTransactionId DESC";

        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@transactionType", (object?)transactionType ?? DBNull.Value);
        command.Parameters.AddWithValue("@customerId", (object?)customerId ?? DBNull.Value);
        command.Parameters.AddWithValue("@from", (object?)from ?? DBNull.Value);
        command.Parameters.AddWithValue("@to", (object?)to ?? DBNull.Value);
        command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<LoyaltyRedemptionHistoryItemDto>();
        while (await reader.ReadAsync(cancellationToken)) items.Add(MapHistoryItem(reader));

        var summary = new LoyaltyRedemptionHistorySummaryDto(
            items.Count(item => item.TransactionType == "Redeem"),
            items.Where(item => item.TransactionType == "Redeem").Sum(item => Math.Abs(item.Points)),
            items.Where(item => item.TransactionType == "Redeem").Sum(item => item.CreditAmount ?? 0m),
            items.Count(item => item.TransactionType == "Reversal"),
            items.Select(item => item.CustomerId).Distinct().Count());
        return new LoyaltyRedemptionHistoryDto(summary, items);
    }

    public async Task<LoyaltyRedemptionDto?> GetByIdAsync(int redemptionId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyRedemptionId, CustomerId, OrderId, PointsRedeemed, PointMonetaryValue, CreditAmount, ReversedAtUtc, ReversalLoyaltyTransactionId, CreatedAtUtc FROM dbo.LoyaltyRedemptions WHERE LoyaltyRedemptionId = @id";
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", redemptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<LoyaltyRedemptionDto>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyRedemptionId, CustomerId, OrderId, PointsRedeemed, PointMonetaryValue, CreditAmount, ReversedAtUtc, ReversalLoyaltyTransactionId, CreatedAtUtc FROM dbo.LoyaltyRedemptions WHERE CustomerId = @customerId ORDER BY CreatedAtUtc DESC, LoyaltyRedemptionId DESC";
        return await QueryAsync(sql, customerId, cancellationToken);
    }

    public async Task<LoyaltyRedemptionResultDto> ApplyAtomicAsync(int customerId, int orderId, decimal pointsRedeemed, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            if (pointsRedeemed <= 0m) throw new InvalidOperationException("PointsRedeemed must be greater than zero.");

            const string settingSql = @"SELECT TOP (1) PointMonetaryValue, AllowRedemption, MinimumRedemptionPoints, MaximumRedemptionPoints
                FROM dbo.LoyaltyProgramSettings
                WHERE IsEnabled = 1 ORDER BY EffectiveFromUtc DESC, LoyaltyProgramSettingId DESC";
            await using var settingCommand = new SqlCommand(settingSql, connection, transaction);
            await using var settingReader = await settingCommand.ExecuteReaderAsync(cancellationToken);
            if (!await settingReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Loyalty program settings are not configured.");
            var pointMonetaryValue = settingReader.GetDecimal(0);
            var allowRedemption = settingReader.GetBoolean(1);
            var minimumRedemptionPoints = settingReader.GetDecimal(2);
            var maximumRedemptionPoints = settingReader.GetDecimal(3);
            await settingReader.DisposeAsync();
            if (!allowRedemption) throw new InvalidOperationException("Loyalty redemption is disabled.");
            if (pointMonetaryValue <= 0m) throw new InvalidOperationException("PointMonetaryValue must be greater than zero.");
            if (pointsRedeemed < minimumRedemptionPoints) throw new InvalidOperationException("PointsRedeemed is below the configured minimum.");
            if (maximumRedemptionPoints > 0m && pointsRedeemed > maximumRedemptionPoints) throw new InvalidOperationException("PointsRedeemed exceeds the configured maximum.");

            const string accountSql = @"SELECT LoyaltyAccountId, CurrentPoints, LifetimeEarnedPoints, LifetimeRedeemedPoints, PendingExpirePoints, LoyaltyAccountStatus
                FROM dbo.LoyaltyAccounts WITH (UPDLOCK, HOLDLOCK) WHERE CustomerId = @customerId";
            await using var accountCommand = new SqlCommand(accountSql, connection, transaction);
            accountCommand.Parameters.AddWithValue("@customerId", customerId);
            await using var accountReader = await accountCommand.ExecuteReaderAsync(cancellationToken);
            if (!await accountReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Loyalty account not found.");
            var accountId = accountReader.GetInt32(0);
            var currentPoints = accountReader.GetDecimal(1);
            var lifetimeEarned = accountReader.GetDecimal(2);
            var lifetimeRedeemed = accountReader.GetDecimal(3);
            var pendingExpire = accountReader.GetDecimal(4);
            var accountStatus = accountReader.GetString(5);
            await accountReader.DisposeAsync();
            if (accountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Loyalty account is frozen.");
            if (pointsRedeemed > currentPoints) throw new InvalidOperationException("PointsRedeemed exceeds current points balance.");

            const string orderSql = @"SELECT OrderNumber, CustomerID, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, OrderStatus
                FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE OrderID = @orderId";
            await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
            orderCommand.Parameters.AddWithValue("@orderId", orderId);
            await using var orderReader = await orderCommand.ExecuteReaderAsync(cancellationToken);
            if (!await orderReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Order not found.");
            var orderNumber = orderReader.GetString(0);
            var orderCustomerId = orderReader.GetInt32(1);
            var totalAmount = orderReader.GetDecimal(2);
            var discountAmount = orderReader.GetDecimal(3);
            var paidAmount = orderReader.GetDecimal(4);
            var remainingAmount = orderReader.GetDecimal(5);
            var orderStatus = orderReader.GetString(6);
            await orderReader.DisposeAsync();
            if (orderCustomerId != customerId) throw new InvalidOperationException("Order does not belong to customer.");
            if (string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(orderStatus, "Closed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Order is not eligible for loyalty credit.");

            var creditAmount = pointsRedeemed * pointMonetaryValue;
            if (creditAmount > remainingAmount) throw new InvalidOperationException("CreditAmount exceeds remaining order amount.");
            var referenceNumber = $"Redeem:{customerId}:{orderId}:{pointsRedeemed}:{pointMonetaryValue}";
            const string duplicateSql = "SELECT TOP (1) LoyaltyRedemptionId FROM dbo.LoyaltyRedemptions WITH (UPDLOCK, HOLDLOCK) WHERE ReferenceNumber = @reference";
            if (await ScalarLongAsync(duplicateSql, connection, transaction, referenceNumber, cancellationToken) is not null)
                throw new InvalidOperationException("An equivalent redemption already exists.");

            const string transactionSql = @"INSERT INTO dbo.LoyaltyTransactions
                (LoyaltyAccountId, CustomerId, OrderId, RewardId, TransactionType, Points, BalanceBefore, BalanceAfter, Source, Notes, CreatedAt)
                OUTPUT INSERTED.LoyaltyTransactionId, INSERTED.LoyaltyAccountId, INSERTED.CustomerId, INSERTED.OrderId,
                    INSERTED.RewardId, INSERTED.TransactionType, INSERTED.Points, INSERTED.BalanceBefore, INSERTED.BalanceAfter,
                    INSERTED.Source, INSERTED.Notes, INSERTED.CreatedAt
                VALUES (@accountId, @customerId, @orderId, NULL, N'Redeem', @points, @before, @after, N'LoyaltyRedemption', @notes, SYSUTCDATETIME())";
            await using var transactionCommand = new SqlCommand(transactionSql, connection, transaction);
            transactionCommand.Parameters.AddWithValue("@accountId", accountId);
            transactionCommand.Parameters.AddWithValue("@customerId", customerId);
            transactionCommand.Parameters.AddWithValue("@orderId", orderId);
            transactionCommand.Parameters.AddWithValue("@points", -pointsRedeemed);
            transactionCommand.Parameters.AddWithValue("@before", currentPoints);
            transactionCommand.Parameters.AddWithValue("@after", currentPoints - pointsRedeemed);
            transactionCommand.Parameters.AddWithValue("@notes", referenceNumber);
            await using var transactionReader = await transactionCommand.ExecuteReaderAsync(cancellationToken);
            if (!await transactionReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to store loyalty transaction.");
            var redeemTransaction = MapTransaction(transactionReader);
            await transactionReader.DisposeAsync();

            const string accountUpdateSql = @"UPDATE dbo.LoyaltyAccounts SET CurrentPoints = @currentPoints,
                LifetimeRedeemedPoints = @lifetimeRedeemed, UpdatedAt = SYSUTCDATETIME(), LastActivityAt = SYSUTCDATETIME()
                WHERE LoyaltyAccountId = @accountId";
            await using var accountUpdate = new SqlCommand(accountUpdateSql, connection, transaction);
            accountUpdate.Parameters.AddWithValue("@currentPoints", currentPoints - pointsRedeemed);
            accountUpdate.Parameters.AddWithValue("@lifetimeRedeemed", lifetimeRedeemed + pointsRedeemed);
            accountUpdate.Parameters.AddWithValue("@accountId", accountId);
            await accountUpdate.ExecuteNonQueryAsync(cancellationToken);

            const string redemptionSql = @"INSERT INTO dbo.LoyaltyRedemptions
                (LoyaltyTransactionId, CustomerId, OrderId, PointsRedeemed, CreditAmount, PointMonetaryValue, ReferenceNumber, CreatedAtUtc)
                OUTPUT INSERTED.LoyaltyRedemptionId, INSERTED.CustomerId, INSERTED.OrderId, INSERTED.PointsRedeemed,
                    INSERTED.PointMonetaryValue, INSERTED.CreditAmount, INSERTED.ReversedAtUtc,
                    INSERTED.ReversalLoyaltyTransactionId, INSERTED.CreatedAtUtc
                VALUES (@transactionId, @customerId, @orderId, @points, @credit, @value, @reference, SYSUTCDATETIME())";
            await using var redemptionCommand = new SqlCommand(redemptionSql, connection, transaction);
            redemptionCommand.Parameters.AddWithValue("@transactionId", redeemTransaction.LoyaltyTransactionId);
            redemptionCommand.Parameters.AddWithValue("@customerId", customerId);
            redemptionCommand.Parameters.AddWithValue("@orderId", orderId);
            redemptionCommand.Parameters.AddWithValue("@points", pointsRedeemed);
            redemptionCommand.Parameters.AddWithValue("@credit", creditAmount);
            redemptionCommand.Parameters.AddWithValue("@value", pointMonetaryValue);
            redemptionCommand.Parameters.AddWithValue("@reference", referenceNumber);
            await using var redemptionReader = await redemptionCommand.ExecuteReaderAsync(cancellationToken);
            if (!await redemptionReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to store loyalty redemption.");
            var redemption = Map(redemptionReader);
            await redemptionReader.DisposeAsync();

            var ledgerReference = $"LoyaltyCredit:{redemption.LoyaltyRedemptionId}:{orderNumber}:{referenceNumber}|رصيد ولاء مطبق على الطلب {orderNumber}";
            const string ledgerSql = @"DECLARE @balance decimal(18,2) = ISNULL((SELECT TOP (1) BalanceAfterTransaction
                FROM dbo.CustomerLedgerEntries WITH (UPDLOCK, HOLDLOCK) WHERE CustomerID = @customerId
                ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC), 0);
                INSERT INTO dbo.CustomerLedgerEntries (CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt)
                VALUES (@customerId, @reference, 0, @credit, @balance - @credit, SYSUTCDATETIME())";
            await using var ledgerCommand = new SqlCommand(ledgerSql, connection, transaction);
            ledgerCommand.Parameters.AddWithValue("@customerId", customerId);
            ledgerCommand.Parameters.AddWithValue("@reference", ledgerReference);
            ledgerCommand.Parameters.AddWithValue("@credit", creditAmount);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken);

            const string orderUpdateSql = @"UPDATE dbo.Orders SET RemainingAmount = @remaining, UpdatedDate = SYSUTCDATETIME()
                WHERE OrderID = @orderId AND RemainingAmount >= @credit";
            await using var orderUpdate = new SqlCommand(orderUpdateSql, connection, transaction);
            orderUpdate.Parameters.AddWithValue("@remaining", remainingAmount - creditAmount);
            orderUpdate.Parameters.AddWithValue("@credit", creditAmount);
            orderUpdate.Parameters.AddWithValue("@orderId", orderId);
            if (await orderUpdate.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Unable to apply loyalty credit to order.");

            await transaction.CommitAsync(cancellationToken);
            var updatedAccount = new LoyaltyAccountDto(accountId, customerId, currentPoints - pointsRedeemed, lifetimeEarned, lifetimeRedeemed + pointsRedeemed, pendingExpire, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, accountStatus);
            return new LoyaltyRedemptionResultDto(redemption, redeemTransaction, creditAmount, updatedAccount.CurrentPoints, remainingAmount, remainingAmount - creditAmount);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<LoyaltyRedemptionDto> CreateAsync(int customerId, int orderId, long loyaltyTransactionId, decimal pointsRedeemed, decimal pointMonetaryValue, decimal creditAmount, string referenceNumber, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO dbo.LoyaltyRedemptions
            (LoyaltyTransactionId, CustomerId, OrderId, PointsRedeemed, CreditAmount, PointMonetaryValue, ReferenceNumber, CreatedAtUtc)
            OUTPUT INSERTED.LoyaltyRedemptionId, INSERTED.CustomerId, INSERTED.OrderId,
                INSERTED.PointsRedeemed, INSERTED.PointMonetaryValue, INSERTED.CreditAmount,
                INSERTED.ReversedAtUtc, INSERTED.ReversalLoyaltyTransactionId, INSERTED.CreatedAtUtc
            VALUES (@transactionId, @customerId, @orderId, @points, @credit, @value, @reference, SYSUTCDATETIME())";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@transactionId", loyaltyTransactionId);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@points", pointsRedeemed);
        command.Parameters.AddWithValue("@credit", creditAmount);
        command.Parameters.AddWithValue("@value", pointMonetaryValue);
        command.Parameters.AddWithValue("@reference", referenceNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Unable to store loyalty redemption.");
        return Map(reader);
    }

    public async Task<LoyaltyCreditDto> CreateCreditAsync(int customerId, int orderId, int redemptionId, decimal creditAmount, decimal pointsRedeemed, string source, string? referenceNumber, CancellationToken cancellationToken)
        => new(0, customerId, orderId, redemptionId, creditAmount, pointsRedeemed, source, referenceNumber, DateTime.UtcNow, false, null);

    public async Task<LoyaltyRedemptionDto> ReverseAsync(int redemptionId, long reversalTransactionId, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE dbo.LoyaltyRedemptions
            SET ReversedAtUtc = COALESCE(ReversedAtUtc, SYSUTCDATETIME()),
                ReversalLoyaltyTransactionId = COALESCE(ReversalLoyaltyTransactionId, @reversalTransactionId)
            OUTPUT INSERTED.LoyaltyRedemptionId, INSERTED.CustomerId, INSERTED.OrderId,
                INSERTED.PointsRedeemed, INSERTED.PointMonetaryValue, INSERTED.CreditAmount,
                INSERTED.ReversedAtUtc, INSERTED.ReversalLoyaltyTransactionId, INSERTED.CreatedAtUtc
            WHERE LoyaltyRedemptionId = @id";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", redemptionId);
        command.Parameters.AddWithValue("@reversalTransactionId", reversalTransactionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Redemption not found.");
        return Map(reader);
    }

    private async Task<IReadOnlyList<LoyaltyRedemptionDto>> QueryAsync(string sql, int customerId, CancellationToken cancellationToken)
    {
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LoyaltyRedemptionDto>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(Map(reader));
        return result;
    }

    private static LoyaltyRedemptionDto Map(SqlDataReader reader) => new(
        Convert.ToInt32(reader.GetValue(0)), reader.GetInt32(1), reader.GetInt32(2),
        reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5),
        reader.IsDBNull(6) ? "Applied" : "Reversed",
        reader.GetDateTime(8), reader.IsDBNull(6) ? null : reader.GetDateTime(6),
        ReadNullableInt64(reader, 7));

    private static LoyaltyRedemptionHistoryItemDto MapHistoryItem(SqlDataReader reader) => new(
        Convert.ToInt64(reader.GetValue(0)), reader.GetString(1), reader.GetInt32(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetDecimal(7),
        reader.GetDecimal(8), reader.GetDecimal(9), reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetDateTime(12),
        ReadNullableInt64(reader, 13),
        reader.IsDBNull(14) ? null : reader.GetDecimal(14),
        reader.IsDBNull(15) ? null : reader.GetDecimal(15),
        reader.IsDBNull(16) ? null : reader.GetDecimal(16),
        reader.IsDBNull(17) ? null : reader.GetString(17),
        reader.IsDBNull(18) ? null : reader.GetDateTime(18),
        ReadNullableInt64(reader, 19),
        ReadNullableInt64(reader, 20),
        reader.IsDBNull(21) ? null : reader.GetString(21));

    private static long? ReadNullableInt64(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));

    private static LoyaltyTransactionDto MapTransaction(SqlDataReader reader) => new(
        reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2),
        reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetInt32(4),
        reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8),
        reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetDateTime(11));

    private static async Task<decimal?> ScalarDecimalAsync(string sql, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToDecimal(value);
    }

    private static async Task<long?> ScalarLongAsync(string sql, SqlConnection connection, SqlTransaction transaction, string reference, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }
}
