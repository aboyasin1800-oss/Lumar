using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Services;

public interface IGoodsReceiptWorkflowCoordinator
{
    Task<GoodsReceiptRuntimeResult> CreateAsync(CreateGoodsReceiptRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken);
    Task<GoodsReceiptReversalResult> ReverseAsync(ReverseGoodsReceiptRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken);
}

public sealed class GoodsReceiptWorkflowCoordinator(
    OperationalSqlConnectionFactory connections,
    IGoodsReceiptTransactionRuntime runtime,
    Es7OperationalAudit audit) : IGoodsReceiptWorkflowCoordinator
{
    public async Task<GoodsReceiptRuntimeResult> CreateAsync(CreateGoodsReceiptRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await runtime.CreateGoodsReceiptInTransactionAsync(connection, transaction, new CreateGoodsReceiptDto
            {
                SupplierId = request.SupplierId,
                PurchaseOrderId = request.PurchaseOrderId,
                WarehouseId = request.WarehouseId,
                ReceiptNumber = request.ReceiptNumber,
                ReceiptDate = request.ReceiptDate,
                Notes = request.Notes,
                CreatedBy = user.Username,
                SourceOperationId = request.SourceOperationId,
                Items = request.Items
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            audit.Record(user, "GoodsReceipt.Create", request.SourceOperationId, result.GoodsReceiptId, correlationId);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<GoodsReceiptReversalResult> ReverseAsync(ReverseGoodsReceiptRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await runtime.ReverseGoodsReceiptInTransactionAsync(connection, transaction, new ReverseGoodsReceiptDto
            {
                GoodsReceiptId = request.GoodsReceiptId,
                SourceOperationId = request.SourceOperationId,
                Reason = request.Reason,
                ReversedBy = user.Username
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            audit.Record(user, "GoodsReceipt.Reverse", request.SourceOperationId, result.GoodsReceiptReversalId, correlationId);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}