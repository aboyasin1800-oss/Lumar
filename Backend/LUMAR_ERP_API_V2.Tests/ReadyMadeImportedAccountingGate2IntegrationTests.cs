using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReadyMadeImportedAccountingGate2IntegrationTests
{
    [Fact]
    public async Task ImportedReceiptAndSale_PostOfficialReceiptCostAndRevenueWithoutCashMovement()
    {
        var connectionString = GetTestConnectionString();
        var suffix = Guid.NewGuid().ToString("N");
        var productCode = $"G2-IMP-{suffix}";
        var saleReference = $"G2-IMP-SALE-{suffix}";
        var productId = 0;
        var orderId = 0;

        try
        {
            var repository = CreateInventoryRepository(connectionString);
            var imported = await repository.UpsertImportedProductAsync(new CreateImportedProductDto
            {
                ProductName = "Gate 2 Imported Product",
                ProductType = "Gate2",
                ProductCode = productCode,
                Unit = "Piece",
                Quantity = 2m,
                PurchasePrice = 123.4567m,
                SellingPrice = 250m,
                Category = "Imported",
                Notes = "Gate 2 integration fixture"
            }, CancellationToken.None);
            Assert.NotNull(imported);
            productId = imported!.ImportedReadyMadeProductId;

            var sales = CreateSalesRepository(connectionString);
            var saleRequest = new CreateReadyMadeSaleDto
            {
                CustomerId = await ReadActiveCustomerIdAsync(connectionString),
                PaymentType = "Cash",
                CashAccountId = 1,
                PaidAmount = 250m,
                SaleReference = saleReference,
                Items = [new CreateReadyMadeSaleItemDto
                {
                    ImportedReadyMadeProductId = productId,
                    Quantity = 1,
                    UnitPrice = 250m
                }]
            };
            var sale = await sales.CreateAsync(saleRequest, CancellationToken.None);
            orderId = sale.OrderId;
            var repeatedSale = await sales.CreateAsync(saleRequest, CancellationToken.None);
            Assert.Equal(sale.OrderId, repeatedSale.OrderId);

            Assert.Equal(1m, await ReadDecimalAsync(connectionString, "SELECT Quantity FROM dbo.ImportedReadyMadeProducts WHERE ImportedReadyMadeProductId=@id", productId));
            Assert.Equal(1m, await ReadDecimalAsync(connectionString, "SELECT CurrentQuantity FROM dbo.InventoryItems WHERE ItemCode=@code", null, productCode));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.ImportedReadyMadeInventoryReceipts WHERE ImportedReadyMadeProductId=@id AND AccountingEventId IS NOT NULL AND InventoryTransactionId IS NOT NULL", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=12 AND ImportedReadyMadeInventoryReceiptId IN (SELECT ImportedReadyMadeInventoryReceiptId FROM dbo.ImportedReadyMadeInventoryReceipts WHERE ImportedReadyMadeProductId=@id)", productId));
            Assert.Equal(246.91m, await ReadDecimalAsync(connectionString, "SELECT PostingAmount FROM dbo.ImportedReadyMadeInventoryReceipts WHERE ImportedReadyMadeProductId=@id", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.FinancialTransactions ft INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId WHERE ae.AccountingEventType=12 AND ae.ImportedReadyMadeInventoryReceiptId IN (SELECT ImportedReadyMadeInventoryReceiptId FROM dbo.ImportedReadyMadeInventoryReceipts WHERE ImportedReadyMadeProductId=@id) AND ft.TransactionType=N'ImportedReadyMadeInventoryReceived' AND ft.Amount=246.91", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntryLines debitLine INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=debitLine.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.JournalEntryLines creditLine ON creditLine.JournalEntryId=je.JournalEntryId INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId=debitLine.LedgerAccountId INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId=creditLine.LedgerAccountId WHERE ae.AccountingEventType=12 AND ae.ImportedReadyMadeInventoryReceiptId IN (SELECT ImportedReadyMadeInventoryReceiptId FROM dbo.ImportedReadyMadeInventoryReceipts WHERE ImportedReadyMadeProductId=@id) AND debitAccount.AccountCode=N'1103' AND creditAccount.AccountCode=N'2100' AND debitLine.DebitAmount=246.91 AND creditLine.CreditAmount=246.91", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId=@orderId AND OrderItemId IN (SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID=@orderId) AND AccountingEventId IS NOT NULL AND InventoryTransactionId IS NOT NULL", orderId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=13 AND ImportedReadyMadeSaleCostPostingId IN (SELECT ImportedReadyMadeSaleCostPostingId FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId=@orderId)", orderId));
            Assert.Equal(123.46m, await ReadDecimalAsync(connectionString, "SELECT PostingAmount FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId=@orderId", orderId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.FinancialTransactions ft INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId WHERE ae.AccountingEventType=13 AND ae.ImportedReadyMadeSaleCostPostingId IN (SELECT ImportedReadyMadeSaleCostPostingId FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId=@orderId) AND ft.TransactionType=N'ImportedReadyMadeCost' AND ft.Amount=123.46", orderId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntryLines debitLine INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=debitLine.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.JournalEntryLines creditLine ON creditLine.JournalEntryId=je.JournalEntryId INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId=debitLine.LedgerAccountId INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId=creditLine.LedgerAccountId WHERE ae.AccountingEventType=13 AND ae.ImportedReadyMadeSaleCostPostingId IN (SELECT ImportedReadyMadeSaleCostPostingId FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId=@orderId) AND debitAccount.AccountCode=N'5200' AND creditAccount.AccountCode=N'1103' AND debitLine.DebitAmount=123.46 AND creditLine.CreditAmount=123.46", orderId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=3 AND OrderId=@orderId", orderId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.CashMovements cm INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=cm.AccountingEventId INNER JOIN dbo.Payments p ON p.PaymentID=ae.PaymentId WHERE p.OrderID=@orderId AND ae.AccountingEventType=2", orderId));
            Assert.Equal(0, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.CashMovements cm INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=cm.AccountingEventId WHERE ae.AccountingEventType=13 AND ae.ImportedReadyMadeSaleCostPostingId IN (SELECT ImportedReadyMadeSaleCostPostingId FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId=@orderId)", orderId));
        }
        finally
        {
            await CleanupAsync(connectionString, orderId, productId, null, productCode, saleReference);
        }
    }

    [Fact]
    public async Task LocalReadyMadeReceiptAndSale_PostWipAndCostUsingProductIdentity()
    {
        var connectionString = GetTestConnectionString();
        var suffix = Guid.NewGuid().ToString("N");
        var saleReference = $"G2-RM-SALE-{suffix}";
        var trackingCode = $"G2-RM-TRACK-{suffix}";
        var productId = 0;
        var productionOrderId = 0;
        var productionItemId = 0;
        var pieceInstanceId = 0;
        var orderId = 0;

        try
        {
            var productTypeId = await ReadActiveProductTypeIdAsync(connectionString);
            (productionOrderId, productionItemId, pieceInstanceId, productId) = await InsertLocalFixtureAsync(connectionString, productTypeId, trackingCode, 100m);
            var sales = CreateSalesRepository(connectionString);
            var sale = await sales.CreateAsync(new CreateReadyMadeSaleDto
            {
                CustomerId = await ReadActiveCustomerIdAsync(connectionString),
                PaymentType = "Credit",
                PaidAmount = 0m,
                SaleReference = saleReference,
                Items = [new CreateReadyMadeSaleItemDto
                {
                    ReadyMadeInventoryProductId = productId,
                    ProductTypeId = productTypeId,
                    Quantity = 1,
                    UnitPrice = 200m
                }]
            }, CancellationToken.None);
            orderId = sale.OrderId;

            Assert.Equal("Sold", await ReadStringAsync(connectionString, "SELECT Status FROM dbo.ReadyMadeInventoryProducts WHERE ReadyMadeInventoryProductId=@id", productId));
            Assert.Equal(0m, await ReadDecimalAsync(connectionString, "SELECT CurrentQuantity FROM dbo.InventoryItems WHERE ItemCode=@code", null, $"RMP-{productId}"));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=5 AND ReadyMadeInventoryProductId=@id", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.FinancialTransactions ft INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId WHERE ae.AccountingEventType=5 AND ae.ReadyMadeInventoryProductId=@id AND ft.TransactionType=N'WipToFinishedGoods' AND ft.Amount=100", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntryLines debitLine INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=debitLine.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.JournalEntryLines creditLine ON creditLine.JournalEntryId=je.JournalEntryId INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId=debitLine.LedgerAccountId INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId=creditLine.LedgerAccountId WHERE ae.AccountingEventType=5 AND ae.ReadyMadeInventoryProductId=@id AND debitAccount.AccountCode=N'1110' AND creditAccount.AccountCode=N'1130' AND debitLine.DebitAmount=100 AND creditLine.CreditAmount=100", productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.ReadyMadeSaleCostPostings WHERE OrderId=@orderId AND ReadyMadeInventoryProductId=@productId AND AccountingEventId IS NOT NULL AND InventoryTransactionId IS NOT NULL", id: orderId, secondId: productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=11 AND ReadyMadeSaleCostPostingId IN (SELECT ReadyMadeSaleCostPostingId FROM dbo.ReadyMadeSaleCostPostings WHERE OrderId=@orderId AND ReadyMadeInventoryProductId=@productId)", id: orderId, secondId: productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.FinancialTransactions ft INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId INNER JOIN dbo.ReadyMadeSaleCostPostings s ON s.ReadyMadeSaleCostPostingId=ae.ReadyMadeSaleCostPostingId WHERE ae.AccountingEventType=11 AND s.OrderId=@orderId AND s.ReadyMadeInventoryProductId=@productId AND ft.TransactionType=N'ReadyMadeCost' AND ft.Amount=100", id: orderId, secondId: productId));
            Assert.Equal(1, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntryLines debitLine INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=debitLine.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.ReadyMadeSaleCostPostings s ON s.ReadyMadeSaleCostPostingId=ae.ReadyMadeSaleCostPostingId INNER JOIN dbo.JournalEntryLines creditLine ON creditLine.JournalEntryId=je.JournalEntryId INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId=debitLine.LedgerAccountId INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId=creditLine.LedgerAccountId WHERE ae.AccountingEventType=11 AND s.OrderId=@orderId AND s.ReadyMadeInventoryProductId=@productId AND debitAccount.AccountCode=N'5200' AND creditAccount.AccountCode=N'1110' AND debitLine.DebitAmount=100 AND creditLine.CreditAmount=100", id: orderId, secondId: productId));
            Assert.Equal(2, await ReadIntAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE ReadyMadeInventoryProductId=@productId OR TransactionID IN (SELECT InventoryTransactionId FROM dbo.ReadyMadeSaleCostPostings WHERE OrderId=@orderId)", id: orderId, secondId: productId));
        }
        finally
        {
            await CleanupAsync(connectionString, orderId, null, productId, null, saleReference, productionOrderId, productionItemId, pieceInstanceId, trackingCode);
        }
    }

    private static string GetTestConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    private static InventoryRepository CreateInventoryRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new InventoryRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static ReadyMadeSalesRepository CreateSalesRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new ReadyMadeSalesRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static async Task<(int ProductionOrderId, int ProductionItemId, int PieceInstanceId, int ProductId)> InsertLocalFixtureAsync(string connectionString, int productTypeId, string trackingCode, decimal actualCost)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var productionNumber = $"G2-RM-ORDER-{Guid.NewGuid():N}";
        try
        {
            var productionOrderId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.ReadyMadeProductionOrders
                    (ProductionOrderNumber, ProductionName, TotalCost, ProfitPercentage, SuggestedSellingPrice, Status, Notes, CreatedAt)
                OUTPUT INSERTED.ReadyMadeProductionOrderId
                VALUES (@number, N'Gate 2 local product', @cost, 0, 200, N'Completed', N'Gate 2 fixture', SYSUTCDATETIME());", ("@number", productionNumber), ("@cost", actualCost));
            var productionItemId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.ReadyMadeProductionOrderItems
                    (ReadyMadeProductionOrderId, PieceType, Quantity, FabricCode, FabricType, FabricColor, CatalogNumber,
                     FabricCost, PieceCost, LineTotal, MeasurementSnapshot, PieceStatus, CreatedAt)
                OUTPUT INSERTED.ReadyMadeProductionOrderItemId
                VALUES (@orderId, N'Gate 2 local product', 1, N'G2-FAB', N'Fabric', N'White', N'G2-CAT',
                        @cost, @cost, @cost, N'{}', N'Ready', SYSUTCDATETIME());", ("@orderId", productionOrderId), ("@cost", actualCost));
            var pieceInstanceId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.ReadyMadeProductionOrderPieceInstances
                    (ReadyMadeProductionOrderItemId, PieceNumber, TrackingCode, PieceStatus, CreatedAt)
                OUTPUT INSERTED.ReadyMadeProductionOrderPieceInstanceId
                VALUES (@itemId, 1, @tracking, N'Ready', SYSUTCDATETIME());", ("@itemId", productionItemId), ("@tracking", trackingCode));
            var productId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.ReadyMadeInventoryProducts
                    (ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadeProductionOrderPieceInstanceId,
                     ProductTypeId, ProductionOrderNumber, ProductionName, PieceType, PieceNumber, TrackingCode,
                     FabricCode, FabricType, FabricColor, CatalogNumber, FabricUnit, ActualCost, SuggestedSellingPrice,
                     MeasurementSnapshot, ReadyForSaleAt, Status, Source, Notes, IsActive, CreatedAt)
                OUTPUT INSERTED.ReadyMadeInventoryProductId
                VALUES (@orderId, @itemId, @pieceId, @productTypeId, @number, N'Gate 2 local product', N'Gate 2 local product',
                        1, @tracking, N'G2-FAB', N'Fabric', N'White', N'G2-CAT', N'Piece', @cost, 200,
                        N'{}', SYSUTCDATETIME(), N'AvailableForSale', N'Gate2Test', N'Gate 2 fixture', 1, SYSUTCDATETIME());",
                ("@orderId", productionOrderId), ("@itemId", productionItemId), ("@pieceId", pieceInstanceId),
                ("@productTypeId", productTypeId), ("@number", productionNumber), ("@tracking", trackingCode), ("@cost", actualCost));
            var inventoryItemId = await InsertIntAsync(connection, transaction, @"
                INSERT INTO dbo.InventoryItems
                    (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt)
                OUTPUT INSERTED.InventoryItemID
                VALUES (@code, N'Gate 2 local product', N'ReadyMadeProduct', N'Piece', 1, 1, 0, 1, SYSUTCDATETIME());", ("@code", $"RMP-{productId}"));
            var posting = await AccountingEventPostingGateway.PostAsync(
                connection,
                transaction,
                AccountingEventType.WipToFinishedGoods,
                actualCost,
                null,
                null,
                null,
                productId,
                $"{trackingCode}:WipToFinishedGoods",
                "Gate 2 local WIP transfer",
                CancellationToken.None);
            await ExecuteAsync(connection, transaction, @"
                INSERT INTO dbo.InventoryTransactions
                    (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost,
                     AccountingEventId, ReadyMadeInventoryProductId, OperationalCostImpact)
                VALUES (@itemId, N'Receive', 1, @reference, N'Gate 2 local WIP transfer', SYSUTCDATETIME(), @cost, @cost,
                        @eventId, @productId, @cost);", ("@itemId", inventoryItemId), ("@reference", $"{trackingCode}:WipToFinishedGoods"),
                ("@cost", actualCost), ("@eventId", posting.AccountingEventId), ("@productId", productId));
            await transaction.CommitAsync();
            return (productionOrderId, productionItemId, pieceInstanceId, productId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> ReadActiveCustomerIdAsync(string connectionString) => await ReadIntAsync(connectionString, "SELECT TOP (1) CustomerID FROM dbo.Customers WHERE IsActive=1 ORDER BY CustomerID");
    private static async Task<int> ReadActiveProductTypeIdAsync(string connectionString) => await ReadIntAsync(connectionString, "SELECT TOP (1) ProductTypeId FROM dbo.PricingProductTypes WHERE IsActive=1 ORDER BY ProductTypeId");

    private static async Task<int> ReadIntAsync(string connectionString, string sql, int? id = null, string? code = null, int? secondId = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@orderId", id ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@productId", secondId ?? (object)DBNull.Value);
        var value = await command.ExecuteScalarAsync();
        return value is null ? 0 : Convert.ToInt32(value);
    }

    private static async Task<decimal> ReadDecimalAsync(string connectionString, string sql, int? id = null, string? code = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@code", code ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@orderId", id ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@productId", id ?? (object)DBNull.Value);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ReadStringAsync(string connectionString, string sql, int id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var value = await command.ExecuteScalarAsync();
        return value is string result ? result : string.Empty;
    }

    private static async Task<int> InsertIntAsync(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(string connectionString, int orderId, int? importedProductId, int? localProductId, string? productCode, string saleReference, int productionOrderId = 0, int productionItemId = 0, int pieceInstanceId = 0, string? trackingCode = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string sql = @"
                DECLARE @events TABLE (AccountingEventId bigint NOT NULL PRIMARY KEY);
                INSERT INTO @events
                    SELECT DISTINCT ae.AccountingEventId
                    FROM dbo.AccountingEvents ae
                    LEFT JOIN dbo.ReadyMadeSaleCostPostings rsc ON rsc.ReadyMadeSaleCostPostingId=ae.ReadyMadeSaleCostPostingId
                    LEFT JOIN dbo.ImportedReadyMadeSaleCostPostings isc ON isc.ImportedReadyMadeSaleCostPostingId=ae.ImportedReadyMadeSaleCostPostingId
                    LEFT JOIN dbo.ImportedReadyMadeInventoryReceipts ir ON ir.ImportedReadyMadeInventoryReceiptId=ae.ImportedReadyMadeInventoryReceiptId
                    LEFT JOIN dbo.Payments p ON p.PaymentID=ae.PaymentId
                    WHERE (@orderId > 0 AND (ae.OrderId=@orderId OR p.OrderID=@orderId OR rsc.OrderId=@orderId OR isc.OrderId=@orderId))
                       OR (@importedProductId IS NOT NULL AND (ir.ImportedReadyMadeProductId=@importedProductId OR isc.ImportedReadyMadeProductId=@importedProductId))
                       OR (@localProductId IS NOT NULL AND (ae.ReadyMadeInventoryProductId=@localProductId OR rsc.ReadyMadeInventoryProductId=@localProductId));
                UPDATE dbo.ReadyMadeSaleCostPostings SET AccountingEventId=NULL, InventoryTransactionId=NULL WHERE (@orderId > 0 AND OrderId=@orderId) OR (@localProductId IS NOT NULL AND ReadyMadeInventoryProductId=@localProductId);
                UPDATE dbo.ImportedReadyMadeSaleCostPostings SET AccountingEventId=NULL, InventoryTransactionId=NULL WHERE (@orderId > 0 AND OrderId=@orderId) OR (@importedProductId IS NOT NULL AND ImportedReadyMadeProductId=@importedProductId);
                UPDATE dbo.ImportedReadyMadeInventoryReceipts SET AccountingEventId=NULL, InventoryTransactionId=NULL WHERE @importedProductId IS NOT NULL AND ImportedReadyMadeProductId=@importedProductId;
                DELETE cm FROM dbo.CashMovements cm INNER JOIN @events e ON e.AccountingEventId=cm.AccountingEventId;
                DELETE it FROM dbo.InventoryTransactions it LEFT JOIN @events e ON e.AccountingEventId=it.AccountingEventId WHERE e.AccountingEventId IS NOT NULL OR (@localProductId IS NOT NULL AND it.ReadyMadeInventoryProductId=@localProductId) OR (@importedProductId IS NOT NULL AND it.ImportedReadyMadeInventoryReceiptId IN (SELECT ImportedReadyMadeInventoryReceiptId FROM dbo.ImportedReadyMadeInventoryReceipts WHERE ImportedReadyMadeProductId=@importedProductId));
                DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
                DELETE je FROM dbo.JournalEntries je INNER JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
                DELETE ft FROM dbo.FinancialTransactions ft INNER JOIN @events e ON e.AccountingEventId=ft.AccountingEventId;
                DELETE ae FROM dbo.AccountingEvents ae INNER JOIN @events e ON e.AccountingEventId=ae.AccountingEventId;
                DELETE FROM dbo.ReadyMadeSaleCostPostings WHERE (@orderId > 0 AND OrderId=@orderId) OR (@localProductId IS NOT NULL AND ReadyMadeInventoryProductId=@localProductId);
                DELETE FROM dbo.ImportedReadyMadeSaleCostPostings WHERE (@orderId > 0 AND OrderId=@orderId) OR (@importedProductId IS NOT NULL AND ImportedReadyMadeProductId=@importedProductId);
                DELETE FROM dbo.ImportedReadyMadeInventoryReceipts WHERE @importedProductId IS NOT NULL AND ImportedReadyMadeProductId=@importedProductId;
                DELETE d FROM dbo.Invoice_Details d INNER JOIN dbo.Invoice_Header ih ON ih.InvoiceID=d.InvoiceID WHERE ih.OrderID=@orderId;
                DELETE pt FROM dbo.Production_Tracking pt INNER JOIN dbo.Invoice_Header ih ON ih.InvoiceID=pt.InvoiceID WHERE ih.OrderID=@orderId;
                DELETE FROM dbo.Invoice_Header WHERE OrderID=@orderId;
                DELETE FROM dbo.Payments WHERE OrderID=@orderId;
                DELETE FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber LIKE @orderReference;
                DELETE FROM dbo.OrderItems WHERE OrderID=@orderId;
                DELETE FROM dbo.Orders WHERE OrderID=@orderId;
                DELETE ii FROM dbo.InventoryItems ii WHERE (@productCode IS NOT NULL AND ii.ItemCode=@productCode) OR (@localProductId IS NOT NULL AND ii.ItemCode=CONCAT(N'RMP-',@localProductId));
                DELETE FROM dbo.ImportedReadyMadeProducts WHERE @importedProductId IS NOT NULL AND ImportedReadyMadeProductId=@importedProductId;
                DELETE FROM dbo.ReadyMadeInventoryProducts WHERE @localProductId IS NOT NULL AND ReadyMadeInventoryProductId=@localProductId;
                DELETE FROM dbo.ReadyMadeProductionOrderPieceInstances WHERE @pieceInstanceId > 0 AND ReadyMadeProductionOrderPieceInstanceId=@pieceInstanceId;
                DELETE FROM dbo.ReadyMadeProductionOrderItems WHERE @productionItemId > 0 AND ReadyMadeProductionOrderItemId=@productionItemId;
                DELETE FROM dbo.ReadyMadeProductionOrders WHERE @productionOrderId > 0 AND ReadyMadeProductionOrderId=@productionOrderId;";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@orderId", orderId);
            command.Parameters.AddWithValue("@importedProductId", importedProductId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@localProductId", localProductId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@productCode", productCode ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@orderReference", $"{saleReference}%");
            command.Parameters.AddWithValue("@productionOrderId", productionOrderId);
            command.Parameters.AddWithValue("@productionItemId", productionItemId);
            command.Parameters.AddWithValue("@pieceInstanceId", pieceInstanceId);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}