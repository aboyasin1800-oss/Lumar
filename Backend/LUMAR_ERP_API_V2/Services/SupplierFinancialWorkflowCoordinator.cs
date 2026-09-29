using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Services;

public interface ISupplierFinancialWorkflowCoordinator
{
    Task<SupplierFinancialInvoiceResult> CreateInvoiceAsync(CreateSupplierInvoiceRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken);
    Task<SupplierFinancialPaymentResult> CreatePaymentAsync(CreateSupplierPaymentRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken);
    Task<SupplierFinancialAllocationResult> AllocateAsync(AllocateSupplierPaymentRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken);
    Task<SupplierFinancialReversalWorkflowResult> ReverseAsync(ReverseSupplierFinancialRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken);
}

public sealed class SupplierFinancialWorkflowCoordinator(
    OperationalSqlConnectionFactory connections,
    SupplierFinancialRuntime runtime,
    Es7OperationalAudit audit) : ISupplierFinancialWorkflowCoordinator
{
    public async Task<SupplierFinancialInvoiceResult> CreateInvoiceAsync(CreateSupplierInvoiceRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(request.SupplierId, request.InvoiceNumber, request.InvoiceDate, request.DueDate, request.Amount, request.Notes, request.SourceOperationId, user.Username, request.CurrencyCode), cancellationToken);
            if (request.PurchaseOrderId.HasValue)
                await EnsureInvoicePurchaseOrderAsync(connection, transaction, result.SupplierInvoiceId, request.SupplierId, request.PurchaseOrderId.Value, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            audit.Record(user, "SupplierInvoice.Create", request.SourceOperationId, result.SupplierInvoiceId, correlationId);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<SupplierFinancialPaymentResult> CreatePaymentAsync(CreateSupplierPaymentRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(request.SupplierId, request.SupplierInvoiceId, request.Amount, request.PaymentDate, request.CashAccountId, request.PaymentKind, request.PaymentMethod, request.ReferenceNumber, request.Notes, request.SourceOperationId, user.Username, request.CurrencyCode), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            audit.Record(user, "SupplierPayment.Create", request.SourceOperationId, result.SupplierPaymentId, correlationId);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<SupplierFinancialAllocationResult> AllocateAsync(AllocateSupplierPaymentRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await runtime.AllocateAsync(connection, transaction, request.SupplierPaymentId, request.SupplierInvoiceId, request.Amount, request.AllocationDate, request.SourceOperationId, request.ReferenceNumber, user.Username, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            audit.Record(user, "SupplierPaymentAllocation.Create", request.SourceOperationId, result.SupplierPaymentAllocationId, correlationId);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<SupplierFinancialReversalWorkflowResult> ReverseAsync(ReverseSupplierFinancialRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var accountingEventId = request.DocumentType.Trim() switch
            {
                "Invoice" => await runtime.ReverseInvoiceAsync(connection, transaction, request.DocumentId, request.SourceOperationId, request.DocumentType, request.Reason, user.Username, cancellationToken),
                "Payment" => await runtime.ReversePaymentAsync(connection, transaction, request.DocumentId, request.SourceOperationId, request.DocumentType, request.Reason, user.Username, cancellationToken),
                "Allocation" => await runtime.ReverseAllocationAsync(connection, transaction, request.DocumentId, request.SourceOperationId, request.DocumentType, request.Reason, user.Username, cancellationToken),
                _ => throw new ArgumentException("DocumentType must be Invoice, Payment, or Allocation.")
            };
            await transaction.CommitAsync(cancellationToken);
            audit.Record(user, $"Supplier{request.DocumentType.Trim()}.Reverse", request.SourceOperationId, request.DocumentId, correlationId);
            return new SupplierFinancialReversalWorkflowResult(request.DocumentType.Trim(), request.DocumentId, accountingEventId, request.SourceOperationId);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task EnsureInvoicePurchaseOrderAsync(SqlConnection connection, SqlTransaction transaction, int invoiceId, int supplierId, int purchaseOrderId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("UPDATE dbo.SupplierInvoices SET PurchaseOrderId=@purchaseOrderId WHERE SupplierInvoiceId=@invoiceId AND SupplierId=@supplierId AND PurchaseOrderId IS NULL AND EXISTS (SELECT 1 FROM dbo.PurchaseOrders WHERE PurchaseOrderId=@purchaseOrderId AND SupplierId=@supplierId);", connection, transaction);
        command.Parameters.AddWithValue("@purchaseOrderId", purchaseOrderId);
        command.Parameters.AddWithValue("@invoiceId", invoiceId);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1) return;

        await using var verify = new SqlCommand("SELECT PurchaseOrderId FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@invoiceId AND SupplierId=@supplierId;", connection, transaction);
        verify.Parameters.AddWithValue("@invoiceId", invoiceId);
        verify.Parameters.AddWithValue("@supplierId", supplierId);
        var value = await verify.ExecuteScalarAsync(cancellationToken);
        if (value is not int existingPurchaseOrderId || existingPurchaseOrderId != purchaseOrderId)
            throw new InvalidOperationException("Supplier invoice purchase order link is invalid or conflicts with the existing invoice.");
    }
}