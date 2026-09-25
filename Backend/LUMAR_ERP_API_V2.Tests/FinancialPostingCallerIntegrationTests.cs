using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
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
        var repository = CreateOrderRepository();
        var customerId = await ReadActiveCustomerIdAsync();
        var productTypeId = await ReadActiveProductTypeIdAsync();
        int? orderId = null;
        string? orderNumber = null;

        try
        {
            var request = new CreateOrderDto
            {
                CustomerId = customerId,
                TotalAmount = 100m,
                AdvancePayment = 25m,
                UrgencyStatus = "Normal",
                SaleCategory = "TailoringOrder",
                PaymentMethod = "Cash",
                RequestReference = requestReference,
                Items = [new CreateOrderItemDto { PieceType = "قطعة اختبار", Quantity = 1, ProductTypeId = productTypeId }]
            };

            var created = await repository.CreateAsync(request, CancellationToken.None);
            Assert.NotNull(created);
            orderId = created!.OrderId;
            orderNumber = created.OrderNumber;
            await AssertPostingAsync($"{created.OrderNumber}:Advance", "CustomerAdvance", 25m, "1000", "1160");

            var replayed = await repository.CreateAsync(request, CancellationToken.None);
            Assert.NotNull(replayed);
            Assert.Equal(created.OrderId, replayed!.OrderId);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference AND TransactionType = N'CustomerAdvance'", $"{created.OrderNumber}:Advance"));
        }
        finally
        {
            if (orderId is not null && orderNumber is not null) await DeleteOrderGraphAsync(orderId.Value, orderNumber);
        }
    }

    [Fact]
    public async Task CustomerAdvance_RollsBackOrderGraph_WhenPaymentMethodExceedsStorageLimit()
    {
        var requestReference = $"TRKA-ADV-ROLLBACK-{Guid.NewGuid():N}";
        var oversizedPaymentMethod = new string('X', 51);
        var repository = CreateOrderRepository();
        var customerId = await ReadActiveCustomerIdAsync();
        var productTypeId = await ReadActiveProductTypeIdAsync();

        try
        {
            var request = new CreateOrderDto
            {
                CustomerId = customerId,
                TotalAmount = 100m,
                AdvancePayment = 25m,
                UrgencyStatus = "Normal",
                SaleCategory = "TailoringOrder",
                PaymentMethod = oversizedPaymentMethod,
                RequestReference = requestReference,
                Items = [new CreateOrderItemDto { PieceType = "قطعة اختبار", Quantity = 1, ProductTypeId = productTypeId }]
            };

            await Assert.ThrowsAsync<SqlException>(() => repository.CreateAsync(request, CancellationToken.None));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference = @reference", requestReference));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.Payments WHERE PaymentMethod = @reference", oversizedPaymentMethod));
        }
        finally
        {
            await DeleteOrdersBySaleReferenceAsync(requestReference);
        }
    }

    [Fact]
    public async Task CustomerPayment_Succeeds_Deduplicates_AndRollsBackOnTransitionalConflict()
    {
        var repository = CreateOrderRepository();
        var success = await InsertOrderFixtureAsync("Delivered", revenueRecognized: true, 100m);
        var failure = await InsertOrderFixtureAsync("Delivered", revenueRecognized: true, 100m);
        var successReference = $"TRKA-PAY-{Guid.NewGuid():N}";
        var failureReference = $"TRKA-PAY-FAIL-{Guid.NewGuid():N}";

        try
        {
            var collected = await repository.CollectCustomerPaymentAsync(success.OrderId, 40m, "Cash", successReference, null, CancellationToken.None);
            Assert.NotNull(collected);
            await AssertPostingAsync(successReference, "CustomerPayment", 40m, "1000", "1200");
            Assert.Null(await repository.CollectCustomerPaymentAsync(success.OrderId, 40m, "Cash", successReference, null, CancellationToken.None));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", successReference));

            await InsertFinancialTransactionAsync(failureReference, "RevenueRecognized", 40m);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CollectCustomerPaymentAsync(failure.OrderId, 40m, "Cash", failureReference, null, CancellationToken.None));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.Payments WHERE OrderID = @orderId AND ReferenceNo = @reference", failureReference, failure.OrderId));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber = @reference", failureReference));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", failureReference));
            Assert.Equal(0m, await ReadDecimalAsync("SELECT PaidAmount FROM dbo.Orders WHERE OrderID = @orderId", failure.OrderId));
        }
        finally
        {
            await DeleteByReferenceAsync(successReference);
            await DeleteByReferenceAsync(failureReference);
            await DeleteOrderGraphAsync(success.OrderId, success.OrderNumber);
            await DeleteOrderGraphAsync(failure.OrderId, failure.OrderNumber);
        }
    }

    [Fact]
    public async Task RevenueRecognized_Succeeds_Deduplicates_AndRollsBackOnTransitionalConflict()
    {
        var repository = CreateOrderRepository();
        var success = await InsertOrderFixtureAsync("Delivered", revenueRecognized: false, 100m);
        var failure = await InsertOrderFixtureAsync("Delivered", revenueRecognized: false, 100m);
        var failureReference = $"{failure.OrderNumber}:RevenueRecognized";

        try
        {
            var recognized = await repository.RecognizeDeliveryRevenueAsync(success.OrderId, CancellationToken.None);
            Assert.NotNull(recognized);
            await AssertPostingAsync($"{success.OrderNumber}:RevenueRecognized", "RevenueRecognized", 100m, "1200", "4200");
            await repository.RecognizeDeliveryRevenueAsync(success.OrderId, CancellationToken.None);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", $"{success.OrderNumber}:RevenueRecognized"));

            await InsertFinancialTransactionAsync(failureReference, "CustomerPayment", 100m);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RecognizeDeliveryRevenueAsync(failure.OrderId, CancellationToken.None));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber = @reference", failureReference));
            Assert.False(await ReadBooleanAsync("SELECT RevenueRecognized FROM dbo.Orders WHERE OrderID = @orderId", failure.OrderId));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", failureReference));
        }
        finally
        {
            await DeleteByReferenceAsync(failureReference);
            await DeleteOrderGraphAsync(success.OrderId, success.OrderNumber);
            await DeleteOrderGraphAsync(failure.OrderId, failure.OrderNumber);
        }
    }

    [Fact]
    public async Task WipToFinishedGoods_Succeeds_Deduplicates_AndRollsBackOnTransitionalConflict()
    {
        var repository = CreateCancelledPieceRepository();
        var success = await InsertWipFixtureAsync();
        var failure = await InsertWipFixtureAsync();

        try
        {
            await repository.SaveDecisionAsync(success.PieceId, "ContinueToReadyInventory", "اختبار", "اختبار", CancellationToken.None);
            var transferred = await repository.ExecuteDecisionAsync(success.PieceId, CancellationToken.None);
            Assert.NotNull(transferred);
            await AssertPostingAsync(success.TrackingCode, "WipToFinishedGoods", 50m, "1110", "1130");
            var replayed = await repository.ExecuteDecisionAsync(success.PieceId, CancellationToken.None);
            Assert.NotNull(replayed);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", success.TrackingCode));

            await repository.SaveDecisionAsync(failure.PieceId, "ContinueToReadyInventory", "اختبار", "اختبار", CancellationToken.None);
            await InsertFinancialTransactionAsync(failure.TrackingCode, "WipToFinishedGoods", 10m);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExecuteDecisionAsync(failure.PieceId, CancellationToken.None));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber = @reference", failure.TrackingCode));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.ReadyMadeInventoryProducts WHERE TrackingCode = @reference", failure.TrackingCode));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference", failure.TrackingCode));
        }
        finally
        {
            await DeleteByReferenceAsync(success.TrackingCode);
            await DeleteByReferenceAsync(failure.TrackingCode);
            await DeleteWipGraphAsync(success);
            await DeleteWipGraphAsync(failure);
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
                OUTPUT INSERTED.OrderItemID VALUES (@orderId,N'قطعة اختبار',1,N'{}',@tracking,N'Ready',SYSUTCDATETIME(),@productTypeId);", order.OrderId, trackingCode, productTypeId);
            var pieceId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                OUTPUT INSERTED.PieceID VALUES (@orderItemId,@tracking,N'Ready',1,SYSUTCDATETIME());", orderItemId, trackingCode, null);
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
        Assert.Equal(2, await CountAsync(@"SELECT COUNT(*) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.ReferenceNumber=@reference", reference));
        Assert.Equal(1, await CountAsync(@"SELECT COUNT(*) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@reference AND la.AccountCode=@debitAccount AND jel.DebitAmount=@amount", reference, null, null, amount, debitAccount));
        Assert.Equal(1, await CountAsync(@"SELECT COUNT(*) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@reference AND la.AccountCode=@creditAccount AND jel.CreditAmount=@amount", reference, null, null, amount, null, creditAccount));
    }

    private static async Task DeleteWipGraphAsync(WipFixture fixture)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
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
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.FinancialTransactions ft ON ft.ReferenceNumber=je.ReferenceNumber WHERE ft.ReferenceNumber LIKE @orderReference;
            DELETE je FROM dbo.JournalEntries je INNER JOIN dbo.FinancialTransactions ft ON ft.ReferenceNumber=je.ReferenceNumber WHERE ft.ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.FinancialTransactions WHERE ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber LIKE @orderReference;
            DELETE FROM dbo.Payments WHERE OrderID=@orderId;
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
            DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.ReferenceNumber=@reference;
            DELETE FROM dbo.JournalEntries WHERE ReferenceNumber=@reference;
            DELETE FROM dbo.FinancialTransactions WHERE ReferenceNumber=@reference;
            DELETE FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber=@reference;", connection);
        command.Parameters.AddWithValue("@reference", reference);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertFinancialTransactionAsync(string reference, string transactionType, decimal amount)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand("INSERT INTO dbo.FinancialTransactions (ReferenceNumber,TransactionType,Amount,Description,CreatedAt) VALUES (@reference,@transactionType,@amount,N'اختبار تكاملي',SYSUTCDATETIME())", connection);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@transactionType", transactionType);
        command.Parameters.AddWithValue("@amount", amount);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadActiveCustomerIdAsync() => await ReadIntAsync("SELECT TOP(1) CustomerID FROM dbo.Customers ORDER BY CustomerID");
    private static async Task<int> ReadActiveProductTypeIdAsync() => await ReadIntAsync("SELECT TOP(1) ProductTypeId FROM dbo.PricingProductTypes WHERE IsActive=1 ORDER BY ProductTypeId");
    private static async Task<int> ReadIntAsync(string sql) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task<decimal> ReadDecimalAsync(string sql, int orderId) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@orderId", orderId); return Convert.ToDecimal(await command.ExecuteScalarAsync()); }
    private static async Task<bool> ReadBooleanAsync(string sql, int orderId) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@orderId", orderId); return Convert.ToBoolean(await command.ExecuteScalarAsync()); }
    private static async Task<int> CountAsync(string sql, string reference, int? orderId = null, string? transactionType = null, decimal? amount = null, string? debitAccount = null, string? creditAccount = null) { await using var connection = new SqlConnection(GetConnectionString()); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@reference", reference); command.Parameters.AddWithValue("@orderId", orderId ?? (object)DBNull.Value); command.Parameters.AddWithValue("@transactionType", transactionType ?? (object)DBNull.Value); command.Parameters.AddWithValue("@amount", amount ?? (object)DBNull.Value); command.Parameters.AddWithValue("@debitAccount", debitAccount ?? (object)DBNull.Value); command.Parameters.AddWithValue("@creditAccount", creditAccount ?? (object)DBNull.Value); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task<int> InsertScalarAsync(SqlConnection connection, SqlTransaction transaction, string sql, int orderId, string tracking, int? productTypeId) { await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@orderId", orderId); command.Parameters.AddWithValue("@tracking", tracking); command.Parameters.AddWithValue("@productTypeId", productTypeId ?? (object)DBNull.Value); command.Parameters.AddWithValue("@orderItemId", orderId); return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, int orderId, string tracking, int? orderItemId, int? pieceId) { await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@orderId", orderId); command.Parameters.AddWithValue("@tracking", tracking); command.Parameters.AddWithValue("@orderItemId", orderItemId ?? (object)DBNull.Value); command.Parameters.AddWithValue("@pieceId", pieceId ?? (object)DBNull.Value); await command.ExecuteNonQueryAsync(); }

    private sealed record OrderFixture(int OrderId, string OrderNumber);
    private sealed record WipFixture(int OrderId, string OrderNumber, int OrderItemId, int PieceId, string TrackingCode);
}