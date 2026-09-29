using LUMAR_ERP_API_V2.DTOs.Inventory;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IGoodsReceiptTransactionRuntime
{
    Task<GoodsReceiptRuntimeResult> CreateGoodsReceiptInTransactionAsync(SqlConnection connection, SqlTransaction transaction, CreateGoodsReceiptDto request, CancellationToken cancellationToken);
    Task<GoodsReceiptReversalResult> ReverseGoodsReceiptInTransactionAsync(SqlConnection connection, SqlTransaction transaction, ReverseGoodsReceiptDto request, CancellationToken cancellationToken);
}