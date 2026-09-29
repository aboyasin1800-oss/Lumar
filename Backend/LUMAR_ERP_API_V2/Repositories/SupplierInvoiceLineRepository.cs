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

        const string sql = "SELECT l.SupplierInvoiceLineId,l.SupplierInvoiceId,l.InventoryItemId,item.ItemCode,item.ItemName,l.Quantity,l.UnitCost,l.Quantity*l.UnitCost,l.RollCount,l.Status FROM dbo.SupplierInvoiceLines l INNER JOIN dbo.InventoryItems item ON item.InventoryItemID=l.InventoryItemId WHERE l.SupplierInvoiceId=@invoiceId ORDER BY l.SupplierInvoiceLineId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@invoiceId", invoiceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<PurchasingInvoiceLineDto>();
        while (await reader.ReadAsync(cancellationToken))
            lines.Add(new PurchasingInvoiceLineDto(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.GetString(9)));
        return lines;
    }
}