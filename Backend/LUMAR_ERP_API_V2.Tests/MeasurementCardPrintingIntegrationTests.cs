using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Inventory;
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
        var employeeId = seed.EmployeeId;

        try
        {
        var first = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(1, first.CopyNumber);
        Assert.Equal("Reserved", first.PrintStatus);
        Assert.Null(first.FinancialTransactionId);
        var firstCompleted = await repository.CompleteAsync(first.PrintHistoryId, null, seed.User, CancellationToken.None);
        Assert.Equal("Completed", firstCompleted.PrintStatus);

        var damagedCard = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedCard", null, null, "اختبار بطاقة تالفة", null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(2, damagedCard.CopyNumber);
        await repository.CompleteAsync(damagedCard.PrintHistoryId, null, seed.User, CancellationToken.None);

        var lostCard = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "LostCard", null, null, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(3, lostCard.CopyNumber);
        await repository.CompleteAsync(lostCard.PrintHistoryId, null, seed.User, CancellationToken.None);

        var originalSourceOperationId = await SeedOfficialOriginalFabricConsumptionAsync(connectionString, seed);
        var damagedPiece = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedPiece", "تمزق أثناء التجهيز", employeeId, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(4, damagedPiece.CopyNumber);
        await repository.CompleteAsync(damagedPiece.PrintHistoryId, null, seed.User, CancellationToken.None);

        var soldPiece = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "PieceSold", null, null, "بيع مع بطاقة بديلة", 15000m, "Cash", null), seed.User, CancellationToken.None);
        Assert.Equal(5, soldPiece.CopyNumber);
        Assert.StartsWith("MC-PIECE-SALE-", soldPiece.FinancialTransactionReference);
        var soldCompleted = await repository.CompleteAsync(soldPiece.PrintHistoryId, seed.CashAccountId, seed.User, CancellationToken.None);
        var idempotentCompleted = await repository.CompleteAsync(soldPiece.PrintHistoryId, seed.CashAccountId, seed.User, CancellationToken.None);

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
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task SuccessfulPrint_UpdatesOfficialProductionStage_AndDoesNotCreatePieceWage()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var prepared = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
            var completed = await repository.CompleteAsync(prepared.PrintHistoryId, null, seed.User, CancellationToken.None);

            Assert.Equal("Completed", completed.PrintStatus);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var pieceStatus = await ReadScalarAsync(connection, "SELECT PieceStatus FROM dbo.Pieces WHERE PieceID = @pieceId", seed.PieceId);
            var printingEvents = await CountAsync(connection, "SELECT COUNT(*) FROM dbo.TrackingEvents WHERE PieceID = @pieceId AND Stage = N'Printing' AND Status = N'Printing'", seed.PieceId);
            var printWages = await CountAsync(connection, "SELECT COUNT(*) FROM dbo.PieceWageRecords WHERE PieceID = @pieceId AND Stage = N'Printing'", seed.PieceId);

            Assert.Equal("Printing", pieceStatus);
            Assert.Equal(1, printingEvents);
            Assert.Equal(0, printWages);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FailedPrint_DoesNotBecomeOfficialCopy_AndDoesNotCreateRevenue()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
        var prepared = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        var failed = await repository.FailAsync(prepared.PrintHistoryId, "ألغى المستخدم نافذة الطباعة", seed.User, CancellationToken.None);

        Assert.NotNull(failed);
        Assert.Equal("Failed", failed!.PrintStatus);
        Assert.Null(failed.PrintedAtUtc);
        Assert.Null(failed.FinancialTransactionId);

        var retry = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        Assert.Equal(1, retry.CopyNumber);
        await repository.CompleteAsync(retry.PrintHistoryId, null, seed.User, CancellationToken.None);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.MeasurementCardPrintHistory WHERE PieceId = @pieceId AND PrintStatus = N'Completed'", seed.PieceId));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber LIKE @reference", $"MC-PIECE-SALE-{prepared.PrintHistoryId:D8}%"));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task DamagedCard_And_DamagedPiece_Reprints_DoNotAdvanceOfficialProductionStage()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var first = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
            var firstCompleted = await repository.CompleteAsync(first.PrintHistoryId, null, seed.User, CancellationToken.None);
            Assert.Equal("Completed", firstCompleted.PrintStatus);

            var damagedCard = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedCard", null, null, "بطاقة تالفة", null, null, null), seed.User, CancellationToken.None);
            await repository.CompleteAsync(damagedCard.PrintHistoryId, null, seed.User, CancellationToken.None);

            var originalSourceOperationId = await SeedOfficialOriginalFabricConsumptionAsync(connectionString, seed);
            var damagedPiece = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedPiece", "تمزق خلال التجهيز", seed.EmployeeId, null, null, null, null), seed.User, CancellationToken.None);
            await repository.CompleteAsync(damagedPiece.PrintHistoryId, null, seed.User, CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var pieceStatus = await ReadScalarAsync(connection, "SELECT PieceStatus FROM dbo.Pieces WHERE PieceID = @pieceId", seed.PieceId);
            var printingEvents = await CountAsync(connection, "SELECT COUNT(*) FROM dbo.TrackingEvents WHERE PieceID = @pieceId AND Stage = N'Printing' AND Status = N'Printing'", seed.PieceId);
            var printingWages = await CountAsync(connection, "SELECT COUNT(*) FROM dbo.PieceWageRecords WHERE PieceID = @pieceId AND Stage = N'Printing'", seed.PieceId);

            Assert.Equal("Printing", pieceStatus);
            Assert.Equal(1, printingEvents);
            Assert.Equal(0, printingWages);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task DamagedPiece_ConsumesReplacementFabric_WithoutAdvancingOfficialStage()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var originalSourceOperationId = await SeedOfficialOriginalFabricConsumptionAsync(connectionString, seed);

            var first = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
            await repository.CompleteAsync(first.PrintHistoryId, null, seed.User, CancellationToken.None);

            var damaged = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedPiece", "تمزق أثناء تجهيز القطعة", seed.EmployeeId, null, null, null, null), seed.User, CancellationToken.None);
            await repository.CompleteAsync(damaged.PrintHistoryId, null, seed.User, CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var originalSources = await ReadFabricConsumptionSourcesAsync(connection, seed.OrderItemId, seed.PieceId, originalSourceOperationId);
            var allSources = await ReadAllFabricConsumptionSourcesAsync(connection, seed.OrderItemId, seed.PieceId);
            var replacementSources = allSources.Where(item => item.SourceOperationId != originalSourceOperationId).ToList();
            Assert.Equal(1, originalSources.Count);
            Assert.Single(replacementSources);
            Assert.NotEqual(originalSources[0].SourceOperationId, replacementSources[0].SourceOperationId);
            Assert.Equal(originalSources[0].ConsumedQuantity, replacementSources[0].ConsumedQuantity);
            Assert.True(
                string.Equals(originalSources[0].FabricCode, replacementSources[0].FabricCode, StringComparison.OrdinalIgnoreCase),
                $"Expected replacement FabricCode '{originalSources[0].FabricCode}' but found '{replacementSources[0].FabricCode}'.");
            Assert.Equal(2, allSources.Count);
            Assert.Equal(928m, await ReadDecimalAsync(connection, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", seed.InventoryItemId));
            Assert.Equal(72m, await ReadDecimalAsync(connection, "SELECT ConsumedQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", seed.InventoryItemId));

            var idempotent = await repository.CompleteAsync(damaged.PrintHistoryId, null, seed.User, CancellationToken.None);
            Assert.Equal("Completed", idempotent.PrintStatus);
            Assert.Equal(2, await CountAsync(connection,
                @"SELECT COUNT(*)
                  FROM dbo.FabricConsumptionSources s
                  WHERE s.OrderItemId = @orderItemId AND s.PieceId = @pieceId AND s.ConsumedQuantity > 0",
                ("@orderItemId", seed.OrderItemId),
                ("@pieceId", seed.PieceId))); 

            var pieceStatus = await ReadScalarAsync(connection, "SELECT PieceStatus FROM dbo.Pieces WHERE PieceID = @pieceId", seed.PieceId);
            var officialPrintingEvents = await CountAsync(connection, "SELECT COUNT(*) FROM dbo.TrackingEvents WHERE PieceID = @pieceId AND Stage = N'Printing' AND Status = N'Printing'", seed.PieceId);
            var reworkEvents = await CountAsync(connection, "SELECT COUNT(*) FROM dbo.TrackingEvents WHERE PieceID = @pieceId AND Stage = N'Rework' AND Status = N'Rework'", seed.PieceId);

            Assert.Equal("Printing", pieceStatus);
            Assert.Equal(1, officialPrintingEvents);
            Assert.Equal(1, reworkEvents);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task DamagedPiece_RequiresOfficialFabricQuantityBeforeRework()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var first = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
            await repository.CompleteAsync(first.PrintHistoryId, null, seed.User, CancellationToken.None);

            var damaged = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, "DamagedPiece", "تمزق أثناء تجهيز القطعة", seed.EmployeeId, null, null, null, null), seed.User, CancellationToken.None);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CompleteAsync(damaged.PrintHistoryId, null, seed.User, CancellationToken.None));
            Assert.Contains("كمية رسمية", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task PieceSold_Donation_IsBlockedWithoutCreatingFinancialRecords()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedTailoringPieceAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
        var first = await repository.PrepareAsync(seed.PieceId, false, new PrepareMeasurementCardPrintDto(null, null, null, null, null, null, null, null), seed.User, CancellationToken.None);
        await repository.CompleteAsync(first.PrintHistoryId, null, seed.User, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PrepareAsync(
            seed.PieceId,
            false,
            new PrepareMeasurementCardPrintDto(null, "PieceSold", null, null, null, 5000m, "Donation", null),
            seed.User,
            CancellationToken.None));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.MeasurementCardPrintHistory WHERE PieceId = @pieceId AND ReprintReasonCode = N'PieceSold'", seed.PieceId));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE Description LIKE N'%CustomerBalanceWaiver%'", DBNull.Value));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    private static IPrintingRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new PrintingRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must target the validation database.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measurement card printing tests are restricted to LUMAR_ERP_ES_VALIDATION.");
        return builder.ConnectionString;
    }

    private static async Task<Seed> SeedTailoringPieceAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var fabricCode = $"TEST-FABRIC-{suffix}";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var customerId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, IsActive)
                OUTPUT INSERTED.CustomerID
                VALUES (@code, N'عميل اختبار بطاقة قياس', @phone, 0, 0, 0, 1);",
                ("@code", $"MCP-{suffix}"), ("@phone", $"013{suffix[..9]}"));
            var departmentId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.Departments (DepartmentCode, DepartmentName, IsActive, CreatedAt)
                OUTPUT INSERTED.DepartmentId
                VALUES (@code, N'قسم اختبار بطاقة قياس', 1, SYSUTCDATETIME());", ("@code", $"MCP-{suffix}"));
            var userId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.Users (Username, UserPassword, FullName, UserRole, IsActive, CreatedDate, SecurityStamp)
                OUTPUT INSERTED.UserID
                VALUES (@username, N'fixture-password', N'مستخدم اختبار بطاقة قياس', N'Admin', 1, SYSUTCDATETIME(), NEWID());", ("@username", $"mcp-{suffix}"));
            var employeeId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.Employees
                    (EmployeeCode, EmployeeName, FullName, DepartmentId, PhoneNumber, BasicSalary, PieceWageRate,
                     OvertimeHourlyRate, HireDate, Status, IsActive, CreatedAt)
                OUTPUT INSERTED.EmployeeID
                VALUES (@code, N'موظف اختبار بطاقة قياس', N'موظف اختبار بطاقة قياس', @departmentId, @phone,
                        0, 0, 0, SYSUTCDATETIME(), N'Active', 1, SYSUTCDATETIME());",
                ("@code", $"MCP-{suffix}"), ("@departmentId", departmentId), ("@phone", $"014{suffix[..9]}"));
            var cashAccountId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.CashAccounts
                    (AccountName, CurrentBalance, IsActive, CreatedAt, CashAccountType, CurrencyCode, LedgerControlAccountId, AllowsReceipts)
                OUTPUT INSERTED.CashAccountId
                VALUES (N'صندوق اختبار بطاقة قياس', 0, 1, SYSUTCDATETIME(), 1, 'YER', 7, 1);");
            var inventoryItemId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.InventoryItems (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.InventoryItemID
                VALUES (@fabricCode, N'قماش اختبار بطاقة قياس', N'Foundation', N'Inch', 1000, 1000, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());",
                ("@fabricCode", fabricCode));
            await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.InventoryItemFoundation (InventoryItemId, InventoryClassId, UnitId, CurrencyCode, OriginalQuantity, AvailableQuantity, ConsumedQuantity, OperationalValue, OfficialUnitCost, CreatedAt, UpdatedAt)
                VALUES (@inventoryItemId, 1, 2, N'YER', 1000, 1000, 0, 500, 0.5, SYSUTCDATETIME(), SYSUTCDATETIME());",
                ("@inventoryItemId", inventoryItemId));
            var orderId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.Orders
                    (OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount, PaidAmount,
                     RemainingAmount, UrgencyStatus, OrderStatus, Notes, CreatedDate, SaleCategory,
                     RevenueRecognized, RevenueRecognizedAt, RevenueReversalCreated)
                OUTPUT INSERTED.OrderID
                VALUES
                    (@orderNumber, @customerId, SYSUTCDATETIME(), NULL, 1000, 0, 0, 1000, N'Normal', N'New',
                     N'Measurement card reprint integration test', SYSUTCDATETIME(), N'Custom', 0, NULL, 0);",
                ("@orderNumber", $"ORD-MCP-{suffix}"), ("@customerId", customerId));
            var trackingCode = $"TRK-MCP-{suffix}";
            var orderItemId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.OrderItems
                    (OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor, MeasurementSnapshot,
                     TrackingCode, PieceStatus, CreatedDate, ProductTypeId)
                OUTPUT INSERTED.OrderItemID
                VALUES (@orderId, N'قميص', 1, @fabricCode, N'قماش', N'أبيض', N'{}', @trackingCode, N'New', SYSUTCDATETIME(), 4);",
                ("@orderId", orderId), ("@trackingCode", trackingCode), ("@fabricCode", fabricCode));
            await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.OrderItemFabrics (OrderItemID, InventoryItemID, FabricCode, FabricType, FabricColor, Quantity, Unit, UnitCost, TotalCost, ConsumedQuantity, CreatedDate)
                VALUES (@orderItemId, @inventoryItemId, @fabricCode, N'قماش', N'أبيض', 36, N'Inch', 0.5, 18, 0, SYSUTCDATETIME());",
                ("@orderItemId", orderItemId), ("@inventoryItemId", inventoryItemId), ("@fabricCode", fabricCode));
            var pieceId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.Pieces (OrderItemID, TrackingCode, PieceStatus, PieceNumber, CreatedDate)
                OUTPUT INSERTED.PieceID
                VALUES (@orderItemId, @trackingCode, N'New', 1, SYSUTCDATETIME());",
                ("@orderItemId", orderItemId), ("@trackingCode", trackingCode));
            await transaction.CommitAsync();
            return new Seed(orderId, orderItemId, pieceId, inventoryItemId, customerId, userId, employeeId, departmentId, cashAccountId, trackingCode, fabricCode, await ReadUserAsync(connectionString, userId));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(string connectionString, Seed seed)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            await using var command = new SqlCommand(@"
                SET XACT_ABORT ON;

                DECLARE @historyIds TABLE (PrintHistoryId int PRIMARY KEY);
                DECLARE @paymentIds TABLE (PaymentId int PRIMARY KEY);
                DECLARE @eventIds TABLE (AccountingEventId bigint PRIMARY KEY);
                DECLARE @fabricSourceIds TABLE (FabricConsumptionSourceId int PRIMARY KEY);
                DECLARE @journalEntryIds TABLE (JournalEntryId int PRIMARY KEY);
                DECLARE @financialTransactionIds TABLE (FinancialTransactionId int PRIMARY KEY);
                DECLARE @inventoryTransactionIds TABLE (TransactionId int PRIMARY KEY);

                INSERT INTO @historyIds
                SELECT h.PrintHistoryId
                FROM dbo.MeasurementCardPrintHistory AS h
                WHERE h.PieceId = @pieceId OR h.OrderItemId = @orderItemId OR h.OrderId = @orderId;

                INSERT INTO @paymentIds
                SELECT p.PaymentID
                FROM dbo.Payments AS p
                WHERE p.PaymentID IN (
                        SELECT p2.PaymentId
                        FROM dbo.MeasurementCardPrintHistory AS h2
                        INNER JOIN dbo.Payments AS p2 ON p2.PrintHistoryId = h2.PrintHistoryId
                        WHERE h2.PieceId = @pieceId OR h2.OrderItemId = @orderItemId OR h2.OrderId = @orderId
                    )
                   OR p.PrintHistoryId IN (SELECT PrintHistoryId FROM @historyIds)
                   OR p.OrderID = @orderId;

                INSERT INTO @fabricSourceIds
                SELECT s.FabricConsumptionSourceId
                FROM dbo.FabricConsumptionSources AS s
                WHERE s.PieceId = @pieceId OR s.OrderItemId = @orderItemId;

                INSERT INTO @eventIds
                SELECT e.AccountingEventId
                FROM dbo.AccountingEvents AS e
                WHERE e.MeasurementCardPrintHistoryId IN (SELECT PrintHistoryId FROM @historyIds)
                   OR e.PaymentId IN (SELECT PaymentId FROM @paymentIds)
                   OR e.FabricConsumptionSourceId IN (SELECT FabricConsumptionSourceId FROM @fabricSourceIds);

                INSERT INTO @journalEntryIds
                SELECT j.JournalEntryId
                FROM dbo.JournalEntries AS j
                WHERE j.AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);

                INSERT INTO @financialTransactionIds
                SELECT f.FinancialTransactionId
                FROM dbo.FinancialTransactions AS f
                WHERE f.AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);

                INSERT INTO @inventoryTransactionIds
                SELECT s.InventoryTransactionId
                FROM dbo.FabricConsumptionSources AS s
                WHERE s.FabricConsumptionSourceId IN (SELECT FabricConsumptionSourceId FROM @fabricSourceIds)
                  AND s.InventoryTransactionId IS NOT NULL;

                UPDATE dbo.Payments
                SET PrintHistoryId = NULL
                WHERE PaymentID IN (SELECT PaymentId FROM @paymentIds);

                UPDATE dbo.MeasurementCardPrintHistory
                SET FinancialTransactionId = NULL,
                    JournalEntryId = NULL,
                    CustomerLedgerEntryId = NULL,
                    PaymentId = NULL,
                    PaymentFinancialTransactionId = NULL,
                    PaymentJournalEntryId = NULL,
                    PaymentCustomerLedgerEntryId = NULL,
                    WaiverFinancialTransactionId = NULL,
                    WaiverJournalEntryId = NULL,
                    WaiverCustomerLedgerEntryId = NULL
                WHERE PrintHistoryId IN (SELECT PrintHistoryId FROM @historyIds);

                DELETE FROM dbo.JournalEntryLines
                WHERE JournalEntryId IN (SELECT JournalEntryId FROM @journalEntryIds);

                DELETE FROM dbo.JournalEntries
                WHERE JournalEntryId IN (SELECT JournalEntryId FROM @journalEntryIds);

                DELETE FROM dbo.CashMovements
                WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);

                DELETE FROM dbo.FinancialTransactions
                WHERE FinancialTransactionId IN (SELECT FinancialTransactionId FROM @financialTransactionIds)
                   OR AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);

                DELETE FROM dbo.AccountingEvents
                WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);

                DELETE FROM dbo.FabricConsumptionSources
                WHERE FabricConsumptionSourceId IN (SELECT FabricConsumptionSourceId FROM @fabricSourceIds);

                DELETE FROM dbo.InventoryTransactions
                WHERE TransactionID IN (SELECT TransactionId FROM @inventoryTransactionIds);

                DELETE FROM dbo.Payments
                WHERE PaymentID IN (SELECT PaymentId FROM @paymentIds);

                DELETE FROM dbo.CustomerLedgerEntries
                WHERE CustomerID = @customerId;

                DELETE FROM dbo.TrackingEvents
                WHERE PieceID = @pieceId OR OrderItemID = @orderItemId OR OrderID = @orderId;

                DELETE FROM dbo.OrderItemFabrics
                WHERE OrderItemID = @orderItemId;

                DELETE FROM dbo.PieceWageRecords
                WHERE PieceID = @pieceId OR OrderItemID = @orderItemId;

                DELETE FROM dbo.MeasurementCardPrintHistory
                WHERE PrintHistoryId IN (SELECT PrintHistoryId FROM @historyIds);

                DELETE FROM dbo.Pieces
                WHERE PieceID = @pieceId;

                DELETE FROM dbo.OrderItems
                WHERE OrderItemID = @orderItemId;

                DELETE FROM dbo.Orders
                WHERE OrderID = @orderId;

                DELETE FROM dbo.InventoryItemFoundation
                WHERE InventoryItemId = @inventoryItemId;

                DELETE FROM dbo.InventoryItems
                WHERE InventoryItemID = @inventoryItemId;

                DELETE FROM dbo.CashAccounts
                WHERE CashAccountId = @cashAccountId;

                DELETE FROM dbo.Employees
                WHERE EmployeeID = @employeeId;

                DELETE FROM dbo.Users
                WHERE UserID = @userId;

                DELETE FROM dbo.Departments
                WHERE DepartmentId = @departmentId;

                DELETE FROM dbo.Customers
                WHERE CustomerID = @customerId;", connection, transaction);
            command.Parameters.AddWithValue("@pieceId", seed.PieceId);
            command.Parameters.AddWithValue("@orderItemId", seed.OrderItemId);
            command.Parameters.AddWithValue("@orderId", seed.OrderId);
            command.Parameters.AddWithValue("@customerId", seed.CustomerId);
            command.Parameters.AddWithValue("@cashAccountId", seed.CashAccountId);
            command.Parameters.AddWithValue("@employeeId", seed.EmployeeId);
            command.Parameters.AddWithValue("@userId", seed.UserId);
            command.Parameters.AddWithValue("@departmentId", seed.DepartmentId);
            command.Parameters.AddWithValue("@inventoryItemId", seed.InventoryItemId);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> InsertIntAsync(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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

    private static async Task<int> CountAsync(SqlConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ReadScalarAsync(SqlConnection connection, string sql, int pieceId)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var value = await command.ExecuteScalarAsync();
        return value is null ? string.Empty : Convert.ToString(value)!;
    }

    private static async Task<decimal> ReadDecimalAsync(SqlConnection connection, string sql, int id)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull ? 0m : Convert.ToDecimal(value);
    }

    private static async Task<Guid> SeedOfficialOriginalFabricConsumptionAsync(string connectionString, Seed seed)
    {
        var sourceOperationId = InventoryRepository.CreateFabricConsumptionOperationId(seed.OrderItemId, false);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
        try
        {
            var original = await InventoryRepository.ConsumeFabricCodeInventoryAsync(connection, transaction,
                new ConsumeFabricCodeInventoryDto
                {
                    FabricCode = seed.FabricCode,
                    OrderItemId = seed.OrderItemId,
                    PieceId = seed.PieceId,
                    QuantityInches = 36m,
                    ConfirmedByUserId = seed.UserId,
                    SourceOperationId = sourceOperationId
                }, CancellationToken.None);

            Assert.False(original.IsExisting);
            Assert.Equal(36m, original.Quantity);
            Assert.True(
                string.Equals(seed.FabricCode, original.ItemCode, StringComparison.OrdinalIgnoreCase),
                $"Expected FabricCode '{seed.FabricCode}' but found '{original.ItemCode}'.");

            await transaction.CommitAsync();
            return sourceOperationId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<(decimal Debit, decimal Credit)> ReadJournalTotalsAsync(SqlConnection connection, int journalEntryId)
    {
        await using var command = new SqlCommand("SELECT COALESCE(SUM(DebitAmount), 0), COALESCE(SUM(CreditAmount), 0) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId", connection);
        command.Parameters.AddWithValue("@journalEntryId", journalEntryId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private static async Task<List<(Guid SourceOperationId, decimal ConsumedQuantity, string FabricCode)>> ReadAllFabricConsumptionSourcesAsync(SqlConnection connection, int orderItemId, int pieceId)
    {
        var rows = new List<(Guid SourceOperationId, decimal ConsumedQuantity, string FabricCode)>();
        await using var command = new SqlCommand(@"
            SELECT s.SourceOperationId, s.ConsumedQuantity, i.ItemCode
            FROM dbo.FabricConsumptionSources s
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = s.InventoryItemId
            WHERE s.OrderItemId = @orderItemId AND s.PieceId = @pieceId
            ORDER BY s.FabricConsumptionSourceId;", connection);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2)));
        }
        return rows;
    }

    private static async Task<List<(Guid SourceOperationId, decimal ConsumedQuantity, string FabricCode)>> ReadFabricConsumptionSourcesAsync(SqlConnection connection, int orderItemId, int pieceId, Guid? sourceOperationId)
    {
        string sql = @"
            SELECT s.SourceOperationId, s.ConsumedQuantity, i.ItemCode
            FROM dbo.FabricConsumptionSources s
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = s.InventoryItemId
            WHERE s.OrderItemId = @orderItemId AND s.PieceId = @pieceId";

        if (sourceOperationId.HasValue)
            sql += " AND s.SourceOperationId = @sourceOperationId";

        sql += " ORDER BY s.FabricConsumptionSourceId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        if (sourceOperationId.HasValue)
            command.Parameters.AddWithValue("@sourceOperationId", sourceOperationId.Value);

        var rows = new List<(Guid SourceOperationId, decimal ConsumedQuantity, string FabricCode)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2)));
        }

        return rows;
    }

    private sealed record Seed(int OrderId, int OrderItemId, int PieceId, int InventoryItemId, int CustomerId, int UserId, int EmployeeId, int DepartmentId, int CashAccountId, string TrackingCode, string FabricCode, CurrentUserDto User);
}
