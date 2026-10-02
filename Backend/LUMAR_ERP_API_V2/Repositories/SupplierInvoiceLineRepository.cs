using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Purchasing;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ISupplierInvoiceLineRepository
{
    Task<IReadOnlyList<PurchasingInvoiceLineDto>?> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken);
}

public sealed class SupplierInvoiceLineRepository(ReadOnlySqlConnectionFactory connections) : ISupplierInvoiceLineRepository
{
    public async Task<IReadOnlyList<PurchasingInvoiceLineDto>?> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using (var invoice = new SqlCommand("SELECT COUNT(*) FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@invoiceId", connection))
        {
            invoice.Parameters.AddWithValue("@invoiceId", invoiceId);
            if (Convert.ToInt32(await invoice.ExecuteScalarAsync(cancellationToken)) == 0) return null;
        }

        var hasProductType = await ColumnExistsAsync(connection, "dbo.SupplierInvoiceLines", "ProductType", cancellationToken);
        var hasUnitCode = await ColumnExistsAsync(connection, "dbo.SupplierInvoiceLines", "UnitCode", cancellationToken);
        var hasItemCount = await ColumnExistsAsync(connection, "dbo.SupplierInvoiceLines", "ItemCount", cancellationToken);

        var productTypeSql = hasProductType ? "COALESCE(l.ProductType, l.ItemType, N'Legacy')" : "COALESCE(l.ItemType, N'Legacy')";
        var unitCodeSql = hasUnitCode ? "COALESCE(l.UnitCode, N'قطعة')" : "N'قطعة'";
        var itemCountSql = hasItemCount ? "COALESCE(l.ItemCount, l.Quantity)" : "l.Quantity";

        var sql = $@"SELECT l.SupplierInvoiceLineId,l.SupplierInvoiceId,l.InventoryItemId,item.ItemCode,COALESCE(l.ItemDescription,item.ItemName,N'غير محدد'),{productTypeSql} AS ItemType,l.SupplierItemCode,l.Quantity,l.UnitCost,l.Quantity*l.UnitCost,l.RollCount,l.Status,{productTypeSql} AS ProductType,{unitCodeSql} AS UnitCode,{itemCountSql} AS ItemCount FROM dbo.SupplierInvoiceLines l LEFT JOIN dbo.InventoryItems item ON item.InventoryItemID=l.InventoryItemId WHERE l.SupplierInvoiceId=@invoiceId ORDER BY l.SupplierInvoiceLineId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@invoiceId", invoiceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<PurchasingInvoiceLineDto>();
        while (await reader.ReadAsync(cancellationToken))
            lines.Add(new PurchasingInvoiceLineDto(reader.GetInt64(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetInt32(10), reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetDecimal(14)));
        return lines;
    }

    private static async Task<bool> ColumnExistsAsync(SqlConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(@"SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@tableName) AND name = @columnName) THEN 1 ELSE 0 END AS bit)", connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }
}