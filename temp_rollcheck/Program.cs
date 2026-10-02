using System;
using System.Threading.Tasks;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

var cs = "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

await using var c = new SqlConnection(cs); await c.OpenAsync();
await using var t = (SqlTransaction)await c.BeginTransactionAsync();
try {
    var supplier = await ScalarAsync(c, t, "INSERT dbo.Suppliers(SupplierCode,SupplierName,IsActive,CreatedAt) OUTPUT INSERTED.SupplierId VALUES(@code,N'Test supplier',1,SYSUTCDATETIME())", ("@code", "TEMP-ROLL-" + Guid.NewGuid().ToString("N")[..8]));
    var warehouse = await ScalarAsync(c, t, "INSERT dbo.Warehouses(WarehouseCode,WarehouseName,IsActive) OUTPUT INSERTED.WarehouseId VALUES(@code,N'Test warehouse',1)", ("@code", "TEMP-W-" + Guid.NewGuid().ToString("N")[..8]));
    var item = await ScalarAsync(c, t, "INSERT dbo.InventoryItems(ItemCode,ItemName,Category,Unit,CurrentQuantity,AvailableQuantity,ReservedQuantity,IsActive,CreatedAt,UpdatedAt) OUTPUT INSERTED.InventoryItemID VALUES(@code,N'Test item',N'Foundation',N'Piece',1,1,0,1,SYSUTCDATETIME(),SYSUTCDATETIME())", ("@code", "TEMP-I-" + Guid.NewGuid().ToString("N")[..8]));
    await ExecuteAsync(c, t, "INSERT dbo.InventoryItemFoundation(InventoryItemId,InventoryClassId,UnitId,CurrencyCode,OriginalQuantity,AvailableQuantity,ConsumedQuantity,OfficialUnitCost,OperationalValue,CreatedAt,UpdatedAt) VALUES(@item,2,3,'YER',1,1,0,12,12,SYSUTCDATETIME(),SYSUTCDATETIME())", ("@item", item));
    var po = await ScalarAsync(c, t, "INSERT dbo.PurchaseOrders(PurchaseOrderNumber,SupplierId,OrderDate,Status,TotalAmount,CreatedAt) OUTPUT INSERTED.PurchaseOrderId VALUES(@number,@supplier,SYSUTCDATETIME(),N'Open',48,SYSUTCDATETIME())", ("@number", "TEMP-PO-" + Guid.NewGuid().ToString("N")[..8]), ("@supplier", supplier));
    await ExecuteAsync(c, t, "INSERT dbo.PurchaseOrderItems(PurchaseOrderId,ItemName,Quantity,UnitCost,LineTotal) VALUES(@po,N'Test item',4,12,48)", ("@po", po));
    var invoice = await ScalarAsync(c, t, "INSERT dbo.SupplierInvoices(SupplierId,PurchaseOrderId,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,AmountPaid,Status,Notes,CreatedAt) OUTPUT INSERTED.SupplierInvoiceId VALUES(@supplier,@po,@number,SYSUTCDATETIME(),SYSUTCDATETIME(),48,0,N'Open',NULL,SYSUTCDATETIME())", ("@supplier", supplier), ("@po", po), ("@number", "TEMP-INV-" + Guid.NewGuid().ToString("N")[..8]));
    var invoiceLine = await ScalarAsync(c, t, "INSERT dbo.SupplierInvoiceLines(SupplierInvoiceId,InventoryItemId,Quantity,UnitCost,RollCount,SourceOperationId,Status,CreatedBy,ItemType,ItemDescription,SupplierItemCode) OUTPUT INSERTED.SupplierInvoiceLineId VALUES(@invoice,@item,4,12,4,@operation,N'Posted',N'ES6-Test',N'Fabric',N'Test fabric',N'Testcode')", ("@invoice", invoice), ("@item", item), ("@operation", Guid.NewGuid()));
    await t.CommitAsync();

    var repository = new InventoryRepository(new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = cs })), new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = cs })));
    var result = await repository.CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
    {
        SupplierId = supplier,
        PurchaseOrderId = po,
        WarehouseId = warehouse,
        ReceiptNumber = "TEMP-ROLLTEST-" + Guid.NewGuid().ToString("N")[..8],
        ReceiptDate = DateTime.UtcNow,
        CreatedBy = "probe",
        SourceOperationId = Guid.NewGuid(),
        Items = [new CreateGoodsReceiptItemDto { InventoryItemId = item, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = invoiceLine, RollCount = 4 }]
    }, CancellationToken.None);

    Console.WriteLine($"GoodsReceiptId={result.GoodsReceiptId}");
    await using var q = new SqlConnection(cs); await q.OpenAsync();
    await using var cmd = new SqlCommand("SELECT GoodsReceiptItemId, SupplierInvoiceLineId, RollCount, ItemType FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@id", q);
    cmd.Parameters.AddWithValue("@id", result.GoodsReceiptId);
    await using var rdr = await cmd.ExecuteReaderAsync();
    while (await rdr.ReadAsync())
    {
        Console.WriteLine($"Row={rdr[0]}, SupplierInvoiceLineId={rdr[1]}, RollCount={rdr[2] ?? "NULL"}, ItemType={rdr[3] ?? "NULL"}");
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
    throw;
}

static Task<int> ScalarAsync(SqlConnection c, SqlTransaction t, string sql, params (string Name, object Value)[] values) { using var cmd = new SqlCommand(sql, c, t); foreach (var v in values) cmd.Parameters.AddWithValue(v.Name, v.Value); return Convert.ToInt32(cmd.ExecuteScalar()); }
static Task ExecuteAsync(SqlConnection c, SqlTransaction t, string sql, params (string Name, object Value)[] values) { using var cmd = new SqlCommand(sql, c, t); foreach (var v in values) cmd.Parameters.AddWithValue(v.Name, v.Value); cmd.ExecuteNonQuery(); return Task.CompletedTask; }
