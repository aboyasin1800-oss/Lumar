using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Printing;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class MeasurementCardPrintingIntegrationTests
{
    [Fact]
    public async Task PrintingLifecycle_TracksCopies_Reasons_FinancialPosting_AndOriginalTrackingCode()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var employeeId = await ReadActiveEmployeeIdAsync(connectionString);

        var first = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(1, first.CopyNumber);
        Assert.Equal("Reserved", first.PrintStatus);
        Assert.Null(first.FinancialTransactionId);
        var firstCompleted = await repository.CompleteAsync(first.PrintHistoryId, seed.User, CancellationToken.None);
        Assert.Equal("Completed", firstCompleted.PrintStatus);

        var damagedCard = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedCard", null, null, "اختبار بطاقة تالفة", null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(2, damagedCard.CopyNumber);
        await repository.CompleteAsync(damagedCard.PrintHistoryId, seed.User, CancellationToken.None);

        var lostCard = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "LostCard", null, null, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(3, lostCard.CopyNumber);
        await repository.CompleteAsync(lostCard.PrintHistoryId, seed.User, CancellationToken.None);

        var damagedPiece = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedPiece", "تمزق أثناء التجهيز", employeeId, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(4, damagedPiece.CopyNumber);
        await repository.CompleteAsync(damagedPiece.PrintHistoryId, seed.User, CancellationToken.None);

        var soldPiece = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "PieceSold", null, null, "بيع مع بطاقة بديلة", 15000m, "Cash", null), seed.User, CancellationToken.None);
        Assert.Equal(5, soldPiece.CopyNumber);
        Assert.StartsWith("MC-PIECE-SALE-", soldPiece.FinancialTransactionReference);
        var soldCompleted = await repository.CompleteAsync(soldPiece.PrintHistoryId, seed.User, CancellationToken.None);
        var idempotentCompleted = await repository.CompleteAsync(soldPiece.PrintHistoryId, seed.User, CancellationToken.None);

        Assert.Equal(soldCompleted.PrintHistoryId, idempotentCompleted.PrintHistoryId);
        Assert.Equal("Completed", idempotentCompleted.PrintStatus);
        Assert.NotNull(idempotentCompleted.FinancialTransactionId);
        Assert.NotNull(idempotentCompleted.JournalEntryId);
        Assert.NotNull(idempotentCompleted.CustomerLedgerEntryId);
        Assert.NotNull(idempotentCompleted.PaymentId);
        Assert.NotNull(idempotentCompleted.PaymentFinancialTransactionId);
        Assert.NotNull(idempotentCompleted.PaymentJournalEntryId);
        Assert.NotNull(idempotentCompleted.PaymentCustomerLedgerEntryId);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var historyCommand = new SqlCommand(@"
            SELECT CopyNumber, PrintStatus, TrackingCode, ReprintReasonCode, DamageReason,
                   ResponsibleEmployeeId, FinancialTransactionId, JournalEntryId,
                   CustomerLedgerEntryId, PaymentId
            FROM dbo.MeasurementCardPrintHistory
            WHERE PieceId = @pieceId
            ORDER BY CopyNumber;", connection);
        historyCommand.Parameters.AddWithValue("@pieceId", seed.PieceId);
        await using var historyReader = await historyCommand.ExecuteReaderAsync();
        var copies = new List<(int Copy, string Status, string Tracking, string? Reason, string? Damage, int? Employee, int? Financial, int? Journal, int? Ledger, int? Payment)>();
        while (await historyReader.ReadAsync())
        {
            copies.Add((
                historyReader.GetInt32(0),
                historyReader.GetString(1),
                historyReader.GetString(2),
                historyReader.IsDBNull(3) ? null : historyReader.GetString(3),
                historyReader.IsDBNull(4) ? null : historyReader.GetString(4),
                historyReader.IsDBNull(5) ? null : historyReader.GetInt32(5),
                historyReader.IsDBNull(6) ? null : historyReader.GetInt32(6),
                historyReader.IsDBNull(7) ? null : historyReader.GetInt32(7),
                historyReader.IsDBNull(8) ? null : historyReader.GetInt32(8),
                historyReader.IsDBNull(9) ? null : historyReader.GetInt32(9)));
        }

        Assert.Equal([1, 2, 3, 4, 5], copies.Select(item => item.Copy).ToArray());
        Assert.All(copies, item => Assert.Equal("Completed", item.Status));
        Assert.All(copies, item => Assert.Equal(seed.TrackingCode, item.Tracking));
        Assert.Equal("DamagedPiece", copies[3].Reason);
        Assert.Equal("تمزق أثناء التجهيز", copies[3].Damage);
        Assert.Equal(employeeId, copies[3].Employee);
        Assert.NotNull(copies[4].Financial);
        Assert.NotNull(copies[4].Journal);
        Assert.NotNull(copies[4].Ledger);
        Assert.NotNull(copies[4].Payment);

        await historyReader.CloseAsync();
        var revenueReference = soldCompleted.FinancialTransactionReference!;
        var paymentReference = soldCompleted.PaymentReferenceNumber!;
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference AND TransactionType = N'RevenueRecognized'", revenueReference));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference AND TransactionType = N'CustomerPayment'", paymentReference));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.Payments WHERE PrintHistoryId = @printHistoryId", soldPiece.PrintHistoryId));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber = @reference", revenueReference));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber = @reference", paymentReference));

        var (debit, credit) = await ReadJournalTotalsAsync(connection, soldCompleted.JournalEntryId!.Value);
        Assert.Equal(debit, credit);
        Assert.Equal(15000m, debit);
    }

    [Fact]
    public async Task FailedPrint_DoesNotBecomeOfficialCopy_AndDoesNotCreateRevenue()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        var prepared = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        var failed = await repository.FailAsync(prepared.PrintHistoryId, "ألغى المستخدم نافذة الطباعة", seed.User, CancellationToken.None);

        Assert.NotNull(failed);
        Assert.Equal("Failed", failed!.PrintStatus);
        Assert.Null(failed.PrintedAtUtc);
        Assert.Null(failed.FinancialTransactionId);

        var retry = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(1, retry.CopyNumber);
        await repository.CompleteAsync(retry.PrintHistoryId, seed.User, CancellationToken.None);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.MeasurementCardPrintHistory WHERE PieceId = @pieceId AND PrintStatus = N'Completed'", seed.PieceId));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber LIKE @reference", $"MC-PIECE-SALE-{prepared.PrintHistoryId:D8}%"));
    }

    private static IPrintingRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new PrintingRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }

    private static async Task<Seed> SeedTailoringPieceAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @customerId int = (SELECT TOP (1) CustomerID FROM dbo.Customers ORDER BY CustomerID);
            DECLARE @userId int = (SELECT TOP (1) UserID FROM dbo.Users WHERE IsActive = 1 ORDER BY UserID);
            DECLARE @now datetime2 = SYSUTCDATETIME();
            INSERT INTO dbo.Orders
                (OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount, PaidAmount,
                 RemainingAmount, UrgencyStatus, OrderStatus, Notes, CreatedDate, SaleCategory,
                 RevenueRecognized, RevenueRecognizedAt, RevenueReversalCreated)
            OUTPUT INSERTED.OrderID, @customerId, @userId
            VALUES
                (@orderNumber, @customerId, @now, NULL, 1000, 0, 0, 1000, N'Normal', N'New',
                 N'Measurement card reprint integration test', @now, N'Custom', 0, NULL, 0);
            INSERT INTO dbo.OrderItems
                (OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor, MeasurementSnapshot,
                 TrackingCode, PieceStatus, CreatedDate)
            OUTPUT INSERTED.OrderItemID
            VALUES (SCOPE_IDENTITY(), N'قميص', 1, N'TEST-FABRIC', N'قماش', N'أبيض', N'{}', @trackingCode, N'New', @now);
            INSERT INTO dbo.Pieces (OrderItemID, TrackingCode, PieceStatus, PieceNumber, CreatedDate)
            OUTPUT INSERTED.PieceID
            VALUES (SCOPE_IDENTITY(), @trackingCode, N'New', 1, @now);", connection);
        command.Parameters.AddWithValue("@orderNumber", $"ORD-MCP-{suffix}");
        command.Parameters.AddWithValue("@trackingCode", $"TRK-MCP-{suffix}");
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("تعذر إنشاء بيانات اختبار بطاقة المقاسات.");
        var orderId = reader.GetInt32(0);
        var customerId = reader.GetInt32(1);
        var userId = reader.GetInt32(2);
        await reader.NextResultAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("تعذر إنشاء بند اختبار بطاقة المقاسات.");
        var orderItemId = reader.GetInt32(0);
        await reader.NextResultAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("تعذر إنشاء قطعة اختبار بطاقة المقاسات.");
        var pieceId = reader.GetInt32(0);
        return new Seed(
            orderId,
            orderItemId,
            pieceId,
            customerId,
            $"TRK-MCP-{suffix}",
            await ReadUserAsync(connectionString, userId));
    }

    private static async Task<CurrentUserDto> ReadUserAsync(string connectionString, int userId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT UserID, Username, FullName, IsActive, LastLoginUtc FROM dbo.Users WHERE UserID = @userId", connection);
        command.Parameters.AddWithValue("@userId", userId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("لا يوجد مستخدم اختبار فعال.");
        return new CurrentUserDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), null, reader.GetBoolean(3), reader.IsDBNull(4) ? null : reader.GetDateTime(4));
    }

    private static async Task<int> ReadActiveEmployeeIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP (1) EmployeeID FROM dbo.Employees WHERE IsActive = 1 OR Status = N'Active' ORDER BY EmployeeID", connection);
        var value = await command.ExecuteScalarAsync();
        return value is int employeeId ? employeeId : throw new InvalidOperationException("لا يوجد موظف اختبار فعال.");
    }

    private static async Task<int> CountAsync(SqlConnection connection, string sql, object value)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@reference", value is string ? value : DBNull.Value);
        command.Parameters.AddWithValue("@pieceId", value is int ? value : DBNull.Value);
        command.Parameters.AddWithValue("@printHistoryId", value is int ? value : DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<(decimal Debit, decimal Credit)> ReadJournalTotalsAsync(SqlConnection connection, int journalEntryId)
    {
        await using var command = new SqlCommand("SELECT COALESCE(SUM(DebitAmount), 0), COALESCE(SUM(CreditAmount), 0) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId", connection);
        command.Parameters.AddWithValue("@journalEntryId", journalEntryId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private sealed record Seed(int OrderId, int OrderItemId, int PieceId, int CustomerId, string TrackingCode, CurrentUserDto User);
}
