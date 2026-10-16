using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FinancialPostingCallerIntegrationTests
{
    [Fact]
    public async Task CustomerAdvance_CreatesOneBalancedPosting_AndReplayedRequestDoesNotDuplicate()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var requestReference = $"TRKA-ADV-{suffix}";
        await FabricConsumablesFoundationIntegrationTests.WithFabricOrderAsync(requestReference, async (repository, template) =>
        {
            var request = new CreateOrderDto
            {
                CustomerId = template.CustomerId,
                TotalAmount = 100m,
                AdvancePayment = 25m,
                UrgencyStatus = "Normal",
                SaleCategory = "TailoringOrder",
                PaymentMethod = "Cash",
                CashAccountId = 1,
                RequestReference = requestReference,
                Items = template.Items
            };

            var created = await repository.CreateAsync(request, CancellationToken.None);
            Assert.NotNull(created);
            await AssertPostingAsync($"{created.OrderNumber}:Advance", "CustomerAdvance", 25m, "1000", "1160");

            var replayed = await repository.CreateAsync(request, CancellationToken.None);
            Assert.NotNull(replayed);
            Assert.Equal(created.OrderId, replayed!.OrderId);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference AND TransactionType = N'CustomerAdvance'", $"{created.OrderNumber}:Advance"));
        });
    }

    [Fact]
    public async Task CustomerAdvance_RollsBackOrderGraph_WhenPaymentMethodExceedsStorageLimit()
    {
        var requestReference = $"TRKA-ADV-ROLLBACK-{Guid.NewGuid():N}";
        var oversizedPaymentMethod = new string('X', 51);
        await FabricConsumablesFoundationIntegrationTests.WithFabricOrderAsync(requestReference, async (repository, template) =>
        {
            var request = new CreateOrderDto
            {
            CustomerId = template.CustomerId,
                TotalAmount = 100m,
                AdvancePayment = 25m,
                UrgencyStatus = "Normal",
                SaleCategory = "TailoringOrder",
                PaymentMethod = oversizedPaymentMethod,
                CashAccountId = 1,
                RequestReference = requestReference,
                Items = template.Items
            };

            await Assert.ThrowsAsync<SqlException>(() => repository.CreateAsync(request, CancellationToken.None));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference = @reference", requestReference));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.Payments WHERE PaymentMethod = @reference", oversizedPaymentMethod));
        });
    }

    [Fact]
    public async Task CustomerPayment_Succeeds_AndDeduplicatesByPaymentIdentity()
    {
        var repository = CreateOrderRepository();
        var success = await InsertOrderFixtureAsync("Delivered", revenueRecognized: true, 100m);
        var successReference = $"TRKA-PAY-{Guid.NewGuid():N}";

        try
        {
            var collected = await repository.CollectCustomerPaymentAsync(success.OrderId, 40m, "Cash", 1, successReference, null, CancellationToken.None);
            Assert.NotNull(collected);
            await AssertPostingAsync(successReference, "CustomerPayment", 40m, "1000", "1200");
            Assert.Null(await repository.CollectCustomerPaymentAsync(success.OrderId, 40m, "Cash", 1, successReference, null, CancellationToken.None));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", successReference));
        }
        finally
        {
            await DeleteOrderGraphAsync(success.OrderId, success.OrderNumber);
        }
    }

    [Fact]
    public async Task RevenueRecognized_Succeeds_AndDeduplicatesByOrderIdentity()
    {
        var repository = CreateOrderRepository();
        var success = await InsertOrderFixtureAsync("Delivered", revenueRecognized: false, 100m);

        try
        {
            var recognized = await repository.RecognizeDeliveryRevenueAsync(success.OrderId, CancellationToken.None);
            Assert.NotNull(recognized);
            await AssertPostingAsync($"{success.OrderNumber}:RevenueRecognized", "RevenueRecognized", 100m, "1200", "4200");
            await repository.RecognizeDeliveryRevenueAsync(success.OrderId, CancellationToken.None);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", $"{success.OrderNumber}:RevenueRecognized"));
        }
        finally
        {
            await DeleteOrderGraphAsync(success.OrderId, success.OrderNumber);
        }
    }

    [Fact]
    public async Task WipToFinishedGoods_Succeeds_AndDeduplicatesByInventoryProductIdentity()
    {
        var repository = CreateCancelledPieceRepository();
        var success = await InsertWipFixtureAsync();

        try
        {
            await repository.SaveDecisionAsync(success.PieceId, "ContinueToReadyInventory", "اختبار", "اختبار", CancellationToken.None);
            var transferred = await repository.ExecuteDecisionAsync(success.PieceId, CancellationToken.None);
            Assert.NotNull(transferred);
            await AssertPostingAsync(success.TrackingCode, "WipToFinishedGoods", 50m, "1110", "1130");
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.AccountingEvents ae INNER JOIN dbo.FinancialTransactions ft ON ft.AccountingEventId=ae.AccountingEventId WHERE ft.ReferenceNumber=@reference AND ae.Status=N'Posted'", success.TrackingCode));
            var replayed = await repository.ExecuteDecisionAsync(success.PieceId, CancellationToken.None);
            Assert.NotNull(replayed);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", success.TrackingCode));
        }
        finally
        {
            await DeleteWipGraphAsync(success);
        }
    }

    [Fact]
    public async Task ActiveOrderPieceDecision_CanBeSavedWithoutCancellingWholeOrder()
    {
        var repository = CreateCancelledPieceRepository();
        var order = await InsertOrderFixtureAsync("Delivered", revenueRecognized: false, 0m);
        var productTypeId = await ReadActiveProductTypeIdAsync();

        try
        {
            var trackingCode = $"TRKA-ACTIVE-{Guid.NewGuid():N}";
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();
            try
            {
                var orderItemId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,MeasurementSnapshot,TrackingCode,PieceStatus,CreatedDate,ProductTypeId)
                    OUTPUT INSERTED.OrderItemID VALUES (@orderId,N'قطعة فعالة',1,N'{}',@tracking,N'Ready',SYSUTCDATETIME(),@productTypeId);",
                    ("@orderId", order.OrderId),
                    ("@tracking", trackingCode),
                    ("@productTypeId", productTypeId));

                await using var pieceCommand = new SqlCommand(@"
                    INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                    OUTPUT INSERTED.PieceID
                    VALUES (@orderItemId,@tracking,N'Ready',1,SYSUTCDATETIME());", connection, transaction);
                pieceCommand.Parameters.AddWithValue("@orderItemId", orderItemId);
                pieceCommand.Parameters.AddWithValue("@tracking", trackingCode);
                var pieceId = Convert.ToInt32(await pieceCommand.ExecuteScalarAsync());

                await insertTrackingEventAsync(connection, transaction, order.OrderId, orderItemId, pieceId, trackingCode, "Assembly", "Completed");
                await transaction.CommitAsync();

                var saved = await repository.SaveDecisionAsync(pieceId, "StopAndHold", "Active order safety check", "tester", CancellationToken.None);
                Assert.Equal(pieceId, saved.PieceId);
                Assert.Equal("StopAndHold", saved.Decision);
                Assert.Equal("tester", saved.DecidedBy);
                Assert.Equal("Active order safety check", saved.Reason);

                var orderStatus = await ReadScalarStringAsync("SELECT OrderStatus FROM dbo.Orders WHERE OrderID=@orderId", order.OrderId);
                Assert.Equal("Delivered", orderStatus);

                var executed = await repository.ExecuteDecisionAsync(pieceId, CancellationToken.None);
                Assert.NotNull(executed);
                Assert.Equal("StopAndHold", executed.Decision);
                Assert.Equal("NotTransferred", executed.TransferStatus);
            }
            finally
            {
                if (transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
            await DeleteOrderGraphAsync(order.OrderId, order.OrderNumber);
        }
    }

    [Fact]
    public async Task DuplicateAndHistoricalRecords_ArePreservedForSinglePieceCancellation()
    {
        var repository = CreateCancelledPieceRepository();
        var order = await InsertOrderFixtureAsync("Active", revenueRecognized: false, 0m);
        var productTypeId = await ReadActiveProductTypeIdAsync();

        try
        {
            var trackingCode = $"TRKA-DUP-{Guid.NewGuid():N}";
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();
            try
            {
                var orderItemId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,MeasurementSnapshot,TrackingCode,PieceStatus,CreatedDate,ProductTypeId)
                    OUTPUT INSERTED.OrderItemID VALUES (@orderId,N'قطعة مكررة',1,N'{}',@tracking,N'Ready',SYSUTCDATETIME(),@productTypeId);",
                    ("@orderId", order.OrderId),
                    ("@tracking", trackingCode),
                    ("@productTypeId", productTypeId));
                var insertedPieceId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                    OUTPUT INSERTED.PieceID VALUES (@orderItemId,@tracking,N'Ready',1,SYSUTCDATETIME());",
                    ("@orderItemId", orderItemId),
                    ("@tracking", trackingCode));
                await insertTrackingEventAsync(connection, transaction, order.OrderId, orderItemId, insertedPieceId, trackingCode, "Assembly", "Completed");
                await insertPieceWageAsync(connection, transaction, order.OrderId, orderItemId, insertedPieceId, trackingCode, 80m);
                await transaction.CommitAsync();
            }
            finally
            {
                if (transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }

            var pieceId = await ReadIntAsync("SELECT PieceID FROM dbo.Pieces WHERE TrackingCode=@tracking", trackingCode);
            var beforeTracking = await CountByPieceIdAsync("SELECT COUNT(*) FROM dbo.TrackingEvents WHERE PieceID=@pieceId", pieceId);
            var beforeWages = await CountByPieceIdAsync("SELECT COUNT(*) FROM dbo.PieceWageRecords WHERE PieceID=@pieceId", pieceId);

            var first = await repository.SaveDecisionAsync(pieceId, "StopAndHold", "duplicate check", "tester", CancellationToken.None);
            var second = await repository.SaveDecisionAsync(first.PieceId, "StopAndHold", "duplicate check updated", "tester-2", CancellationToken.None);
            Assert.Equal(first.PieceId, second.PieceId);
            Assert.Equal(1, await CountByPieceIdAsync("SELECT COUNT(*) FROM dbo.CancelledPieceDisposition WHERE PieceId=@pieceId", pieceId));

            var afterTracking = await CountByPieceIdAsync("SELECT COUNT(*) FROM dbo.TrackingEvents WHERE PieceID=@pieceId", pieceId);
            var afterWages = await CountByPieceIdAsync("SELECT COUNT(*) FROM dbo.PieceWageRecords WHERE PieceID=@pieceId", pieceId);
            Assert.Equal(beforeTracking, afterTracking);
            Assert.Equal(beforeWages, afterWages);
        }
        finally
        {
            await DeleteOrderGraphAsync(order.OrderId, order.OrderNumber);
        }
    }

    [Fact]
    public async Task MissingPiece_AndActivePieceSave_StayFailSafe()
    {
        var repository = CreateCancelledPieceRepository();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveDecisionAsync(-1, "StopAndHold", "missing", "tester", CancellationToken.None));

        var order = await InsertOrderFixtureAsync("Active", revenueRecognized: false, 0m);
        var productTypeId = await ReadActiveProductTypeIdAsync();
        try
        {
            var trackingCode = $"TRKA-MISSING-{Guid.NewGuid():N}";
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();
            try
            {
                var orderItemId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,MeasurementSnapshot,TrackingCode,PieceStatus,CreatedDate,ProductTypeId)
                    OUTPUT INSERTED.OrderItemID VALUES (@orderId,N'قطعة فارغة',1,N'{}',@tracking,N'Ready',SYSUTCDATETIME(),@productTypeId);",
                    ("@orderId", order.OrderId),
                    ("@tracking", trackingCode),
                    ("@productTypeId", productTypeId));
                var pieceId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                    OUTPUT INSERTED.PieceID VALUES (@orderItemId,@tracking,N'Ready',1,SYSUTCDATETIME());",
                    ("@orderItemId", orderItemId),
                    ("@tracking", trackingCode));
                await transaction.CommitAsync();

                var saved = await repository.SaveDecisionAsync(pieceId, "StopAndHold", "active safe", "tester", CancellationToken.None);
                Assert.Equal(pieceId, saved.PieceId);
            }
            finally
            {
                if (transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
            await DeleteOrderGraphAsync(order.OrderId, order.OrderNumber);
        }
    }

    [Theory]
    [InlineData("Legacy")]
    [InlineData("Posted")]
    [InlineData("Reversed")]
    public async Task WipToFinishedGoods_ReplayPreservesStoredEventStatus(string existingStatus)
    {
        var repository = CreateCancelledPieceRepository();
        var fixture = await InsertWipFixtureAsync();

        try
        {
            await repository.SaveDecisionAsync(fixture.PieceId, "ContinueToReadyInventory", "اختبار", "اختبار", CancellationToken.None);
            Assert.NotNull(await repository.ExecuteDecisionAsync(fixture.PieceId, CancellationToken.None));

            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();
            try
            {
                await using var statusCommand = new SqlCommand(@"
                    UPDATE dbo.AccountingEvents
                    SET Status=@status
                    OUTPUT INSERTED.AccountingEventId, INSERTED.ReadyMadeInventoryProductId
                    WHERE AccountingEventType=5
                      AND ReadyMadeInventoryProductId IN
                          (SELECT ReadyMadeInventoryProductId FROM dbo.ReadyMadeInventoryProducts WHERE TrackingCode=@tracking);", connection, transaction);
                statusCommand.Parameters.AddWithValue("@status", existingStatus);
                statusCommand.Parameters.AddWithValue("@tracking", fixture.TrackingCode);
                await using var reader = await statusCommand.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                var eventId = reader.GetInt64(0);
                var productId = reader.GetInt32(1);
                Assert.False(await reader.ReadAsync());
                await reader.DisposeAsync();

                var replayed = await AccountingEventPostingGateway.PostAsync(
                    connection, transaction, AccountingEventType.WipToFinishedGoods,
                    50m, null, null, null, productId, fixture.TrackingCode,
                    "WIP posting replay", CancellationToken.None);

                Assert.True(replayed.IsExisting);
                Assert.Equal(eventId, replayed.AccountingEventId);
                await using var verifyCommand = new SqlCommand(
                    "SELECT Status FROM dbo.AccountingEvents WHERE AccountingEventId=@eventId", connection, transaction);
                verifyCommand.Parameters.AddWithValue("@eventId", eventId);
                Assert.Equal(existingStatus, Convert.ToString(await verifyCommand.ExecuteScalarAsync()));
            }
            finally
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber=@reference", fixture.TrackingCode));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber=@reference", fixture.TrackingCode));
        }
        finally
        {
            await DeleteWipGraphAsync(fixture);
        }
    }

    private static OrderRepository CreateOrderRepository()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = GetConnectionString() });
        return new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static CancelledPieceDispositionRepository CreateCancelledPieceRepository()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = GetConnectionString() });
        return new CancelledPieceDispositionRepository(new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    private static async Task<OrderFixture> InsertOrderFixtureAsync(string status, bool revenueRecognized, decimal remaining)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var customerId = await ReadActiveCustomerIdAsync();
        var orderNumber = $"TRKA-ORD-{suffix}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.Orders (OrderNumber,CustomerID,OrderDate,DeliveryDate,TotalAmount,DiscountAmount,PaidAmount,RemainingAmount,UrgencyStatus,OrderStatus,CreatedDate,SaleCategory,RevenueRecognized,RevenueReversalCreated)
            OUTPUT INSERTED.OrderID
            VALUES (@number,@customerId,SYSUTCDATETIME(),SYSUTCDATETIME(),@total,0,0,@remaining,N'Normal',@status,SYSUTCDATETIME(),N'TailoringOrder',@recognized,0);", connection);
        command.Parameters.AddWithValue("@number", orderNumber);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@total", remaining);
        command.Parameters.AddWithValue("@remaining", remaining);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@recognized", revenueRecognized);
        return new OrderFixture(Convert.ToInt32(await command.ExecuteScalarAsync()), orderNumber);
    }

    private static async Task<WipFixture> InsertWipFixtureAsync()
    {
        var order = await InsertOrderFixtureAsync("Cancelled", revenueRecognized: false, 0m);
        var productTypeId = await ReadActiveProductTypeIdAsync();
        var trackingCode = $"TRKA-WIP-{Guid.NewGuid():N}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        try
        {
            var orderItemId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,MeasurementSnapshot,TrackingCode,PieceStatus,CreatedDate,ProductTypeId)
                OUTPUT INSERTED.OrderItemID VALUES (@orderId,N'قطعة اختبار',1,N'{}',@tracking,N'Ready',SYSUTCDATETIME(),@productTypeId);",
                ("@orderId", order.OrderId),
                ("@tracking", trackingCode),
                ("@productTypeId", productTypeId));
            var pieceId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                OUTPUT INSERTED.PieceID VALUES (@orderItemId,@tracking,N'Ready',1,SYSUTCDATETIME());",
                ("@orderItemId", orderItemId),
                ("@tracking", trackingCode));
            await ExecuteAsync(connection, transaction, "INSERT INTO dbo.TrackingEvents (OrderItemID,OrderID,TrackingCode,Stage,Status,EventTime,IsReverted,PieceID) VALUES (@orderItemId,@orderId,@tracking,N'Assembly',N'Completed',SYSUTCDATETIME(),0,@pieceId);", order.OrderId, trackingCode, orderItemId, pieceId);
            await ExecuteAsync(connection, transaction, "INSERT INTO dbo.OrderItemFabrics (OrderItemID,Quantity,Unit,UnitCost,TotalCost,ConsumedQuantity,CreatedDate) VALUES (@orderItemId,1,N'Piece',50,50,0,SYSUTCDATETIME());", order.OrderId, trackingCode, orderItemId, null);
            transaction.Commit();
            return new WipFixture(order.OrderId, order.OrderNumber, orderItemId, pieceId, trackingCode);
        }
        catch
        {
            transaction.Rollback();
            await DeleteOrderGraphAsync(order.OrderId, order.OrderNumber);
            throw;
        }
    }

    private static async Task AssertPostingAsync(string reference, string transactionType, decimal amount, string debitAccount, string creditAccount)
    {
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference AND TransactionType = @transactionType AND Amount = @amount", reference, null, transactionType, amount));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber = @reference", reference));
        Assert.Equal(1, await CountAsync(@"SELECT COUNT(*) FROM dbo.AccountingEvents ae INNER JOIN dbo.FinancialTransactions ft ON ft.AccountingEventId=ae.AccountingEventId INNER JOIN dbo.JournalEntries je ON je.AccountingEventId=ae.AccountingEventId WHERE ft.ReferenceNumber=@reference AND ft.Amount=ae.PostingAmount", reference));
        Assert.Equal(2, await CountAsync(@"SELECT COUNT(*) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.ReferenceNumber=@reference", reference));
        Assert.Equal(1, await CountAsync(@"SELECT COUNT(*) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@reference AND la.AccountCode=@debitAccount AND jel.DebitAmount=@amount", reference, null, null, amount, debitAccount));
        Assert.Equal(1, await CountAsync(@"SELECT COUNT(*) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@reference AND la.AccountCode=@creditAccount AND jel.CreditAmount=@amount", reference, null, null, amount, null, creditAccount));
    }

    private static async Task DeleteWipGraphAsync(WipFixture fixture)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @events TABLE (AccountingEventId bigint NOT NULL PRIMARY KEY);
            INSERT INTO @events (AccountingEventId)
            SELECT ae.AccountingEventId
            FROM dbo.AccountingEvents ae
            INNER JOIN dbo.ReadyMadeInventoryProducts rip ON rip.ReadyMadeInventoryProductId=ae.ReadyMadeInventoryProductId
            WHERE rip.TrackingCode=@tracking;
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
            DELETE je FROM dbo.JournalEntries je INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
            DELETE ft FROM dbo.FinancialTransactions ft INNER JOIN @events e ON e.AccountingEventId=ft.AccountingEventId;
            DELETE ae FROM dbo.AccountingEvents ae INNER JOIN @events e ON e.AccountingEventId=ae.AccountingEventId;
            DELETE rip FROM dbo.ReadyMadeInventoryProducts rip WHERE rip.TrackingCode=@tracking;
            ", connection);
        command.Parameters.AddWithValue("@tracking", fixture.TrackingCode);
        await command.ExecuteNonQueryAsync();
        await DeleteOrderGraphAsync(fixture.OrderId, fixture.OrderNumber);
    }

    private static async Task DeleteOrderGraphAsync(int orderId, string orderNumber)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @events TABLE (AccountingEventId bigint NOT NULL PRIMARY KEY);
            INSERT INTO @events (AccountingEventId)
            SELECT ae.AccountingEventId
            FROM dbo.AccountingEvents ae
            WHERE ae.OrderId=@orderId
               OR ae.PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID=@orderId);
                DELETE cm FROM dbo.CashMovements cm INNER JOIN @events e ON e.AccountingEventId=cm.AccountingEventId;
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
            DELETE je FROM dbo.JournalEntries je INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
            DELETE ft FROM dbo.FinancialTransactions ft INNER JOIN @events e ON e.AccountingEventId=ft.AccountingEventId;
            DELETE ae FROM dbo.AccountingEvents ae INNER JOIN @events e ON e.AccountingEventId=ae.AccountingEventId;
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.JournalEntries WHERE ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.FinancialTransactions WHERE ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.Payments WHERE OrderID=@orderId;
            DELETE pwr FROM dbo.PieceWageRecords pwr INNER JOIN dbo.Pieces p ON p.PieceID = pwr.PieceID INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID WHERE oi.OrderID=@orderId;
            DELETE te FROM dbo.TrackingEvents te WHERE te.OrderID=@orderId;
            DELETE cpd FROM dbo.CancelledPieceDisposition cpd INNER JOIN dbo.Pieces p ON p.PieceID=cpd.PieceId INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID WHERE oi.OrderID=@orderId;
            DELETE FROM dbo.OrderItemFabrics WHERE OrderItemID IN (SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID=@orderId);
            DELETE FROM dbo.Pieces WHERE OrderItemID IN (SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID=@orderId);
            DELETE FROM dbo.OrderItems WHERE OrderID=@orderId;
            DELETE FROM dbo.Orders WHERE OrderID=@orderId;", connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@orderReference", $"{orderNumber}%");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteOrdersBySaleReferenceAsync(string requestReference)
    {
        var orders = new List<OrderFixture>();
        await using (var connection = new SqlConnection(GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT OrderID, OrderNumber FROM dbo.Orders WHERE SaleReference = @reference", connection);
            command.Parameters.AddWithValue("@reference", requestReference);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                orders.Add(new OrderFixture(reader.GetInt32(0), reader.GetString(1)));
        }

        foreach (var order in orders)
            await DeleteOrderGraphAsync(order.OrderId, order.OrderNumber);
    }

    private static async Task DeleteByReferenceAsync(string reference)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @events TABLE (AccountingEventId bigint NOT NULL PRIMARY KEY);
            INSERT INTO @events (AccountingEventId)
            SELECT DISTINCT ft.AccountingEventId
            FROM dbo.FinancialTransactions ft
            WHERE ft.ReferenceNumber=@reference AND ft.AccountingEventId IS NOT NULL
            UNION
            SELECT DISTINCT je.AccountingEventId
            FROM dbo.JournalEntries je
            WHERE je.ReferenceNumber=@reference AND je.AccountingEventId IS NOT NULL;
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
            DELETE je FROM dbo.JournalEntries je INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
            DELETE ft FROM dbo.FinancialTransactions ft INNER JOIN @events e ON e.AccountingEventId=ft.AccountingEventId;
            DELETE ae FROM dbo.AccountingEvents ae INNER JOIN @events e ON e.AccountingEventId=ae.AccountingEventId;
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.ReferenceNumber=@reference;
            DELETE FROM dbo.JournalEntries WHERE ReferenceNumber=@reference;
            DELETE FROM dbo.FinancialTransactions WHERE ReferenceNumber=@reference;
            DELETE FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber=@reference;", connection);
        command.Parameters.AddWithValue("@reference", reference);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadActiveCustomerIdAsync() => await ReadIntAsync("SELECT TOP(1) CustomerID FROM dbo.Customers ORDER BY CustomerID");
    private static async Task<int> ReadActiveProductTypeIdAsync() => await ReadIntAsync("SELECT TOP(1) ProductTypeId FROM dbo.PricingProductTypes WHERE IsActive=1 ORDER BY ProductTypeId");
    private static async Task<int> ReadIntAsync(string sql) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task<int> ReadIntAsync(string sql, string trackingCode) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@tracking", trackingCode); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task<int> CountByPieceIdAsync(string sql, int pieceId) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@pieceId", pieceId); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task<decimal> ReadDecimalAsync(string sql, int orderId) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@orderId", orderId); return Convert.ToDecimal(await command.ExecuteScalarAsync()); }
    private static async Task<bool> ReadBooleanAsync(string sql, int orderId) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@orderId", orderId); return Convert.ToBoolean(await command.ExecuteScalarAsync()); }
    private static async Task<int> CountAsync(string sql, string reference, int? orderId = null, string? transactionType = null, decimal? amount = null, string? debitAccount = null, string? creditAccount = null) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@reference", reference); command.Parameters.AddWithValue("@orderId", orderId ?? (object)DBNull.Value); command.Parameters.AddWithValue("@transactionType", transactionType ?? (object)DBNull.Value); command.Parameters.AddWithValue("@amount", amount ?? (object)DBNull.Value); command.Parameters.AddWithValue("@debitAccount", debitAccount ?? (object)DBNull.Value); command.Parameters.AddWithValue("@creditAccount", creditAccount ?? (object)DBNull.Value); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task<int> InsertScalarAsync(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, int orderId, string tracking, int? orderItemId, int? pieceId) { await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@orderId", orderId); command.Parameters.AddWithValue("@tracking", tracking); command.Parameters.AddWithValue("@orderItemId", orderItemId ?? (object)DBNull.Value); command.Parameters.AddWithValue("@pieceId", pieceId ?? (object)DBNull.Value); await command.ExecuteNonQueryAsync(); }
    private static async Task<int> insertTrackingEventAsync(SqlConnection connection, SqlTransaction transaction, int orderId, int orderItemId, int pieceId, string trackingCode, string stage, string status)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.TrackingEvents (OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, IsReverted, PieceID)
            OUTPUT INSERTED.TrackingEventID
            VALUES (@orderItemId, @orderId, @trackingCode, @stage, @status, SYSUTCDATETIME(), 0, @pieceId);", connection, transaction);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@trackingCode", trackingCode);
        command.Parameters.AddWithValue("@stage", stage);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    private static async Task insertPieceWageAsync(SqlConnection connection, SqlTransaction transaction, int orderId, int orderItemId, int pieceId, string trackingCode, decimal totalWage)
    {
        var trackingEventId = await insertTrackingEventAsync(connection, transaction, orderId, orderItemId, pieceId, trackingCode, "Assembly", "Completed");
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.PieceWageRecords (OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, Status, CreatedAt)
            VALUES (@orderId, @orderItemId, @pieceId, @trackingEventId, NULL, N'EMP-TEST', N'قطعة اختبار', N'Assembly', 1, @totalWage, @totalWage, N'PendingPayroll', SYSUTCDATETIME());", connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        command.Parameters.AddWithValue("@totalWage", totalWage);
        await command.ExecuteNonQueryAsync();
    }
    private static async Task<string> ReadScalarStringAsync(string sql, int orderId)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private sealed record OrderFixture(int OrderId, string OrderNumber);
    private sealed record WipFixture(int OrderId, string OrderNumber, int OrderItemId, int PieceId, string TrackingCode);
}