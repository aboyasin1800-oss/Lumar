using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;

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
            await EnsureInvoiceLinesAsync(connection, transaction, result.SupplierInvoiceId, request, user.Username, cancellationToken);
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

    private static async Task EnsureInvoiceLinesAsync(SqlConnection connection, SqlTransaction transaction, int invoiceId, CreateSupplierInvoiceRequestDto request, string createdBy, CancellationToken cancellationToken)
    {
        if (request.Lines is not { Count: > 0 }) throw new ArgumentException("Supplier invoice lines are required.");
        var total = request.Lines.Sum(line => decimal.Round(line.Quantity * line.UnitCost, 6, MidpointRounding.AwayFromZero));
        if (total != request.Amount) throw new InvalidOperationException("Supplier invoice amount must equal the lines total.");

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            var operationId = DeriveLineOperationId(request.SourceOperationId, index);
            await using var command = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.SupplierInvoiceLines WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@operation)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.SupplierInvoiceLines WHERE SourceOperationId=@operation AND SupplierInvoiceId=@invoiceId AND ((InventoryItemId=@itemId) OR (InventoryItemId IS NULL AND @itemId IS NULL)) AND ItemDescription=@description AND ItemType=@itemType AND ((SupplierItemCode IS NULL AND @supplierItemCode IS NULL) OR SupplierItemCode=@supplierItemCode) AND Quantity=@quantity AND UnitCost=@unitCost AND ((RollCount IS NULL AND @rollCount IS NULL) OR RollCount=@rollCount) AND Status=N'Posted')
        THROW 52120,N'IDEMPOTENCY CONFLICT',1;
END
ELSE
BEGIN
    IF @itemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.InventoryItems WITH(UPDLOCK,HOLDLOCK) WHERE InventoryItemID=@itemId AND IsActive=1)
        THROW 52121,N'Supplier invoice inventory item is unavailable.',1;
    INSERT dbo.SupplierInvoiceLines(SupplierInvoiceId,InventoryItemId,ItemDescription,ItemType,SupplierItemCode,Quantity,UnitCost,RollCount,SourceOperationId,Status,CreatedBy)
    VALUES(@invoiceId,@itemId,@description,@itemType,@supplierItemCode,@quantity,@unitCost,@rollCount,@operation,N'Posted',@createdBy);
END", connection, transaction);
            command.Parameters.AddWithValue("@invoiceId", invoiceId);
            command.Parameters.AddWithValue("@itemId", line.InventoryItemId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@description", (line.ItemDescription ?? string.Empty).Trim());
            command.Parameters.AddWithValue("@itemType", line.ItemType.Trim());
            command.Parameters.AddWithValue("@supplierItemCode", string.IsNullOrWhiteSpace(line.SupplierItemCode) ? DBNull.Value : line.SupplierItemCode.Trim());
            AddDecimal(command, "@quantity", line.Quantity);
            AddDecimal(command, "@unitCost", line.UnitCost);
            command.Parameters.AddWithValue("@rollCount", line.RollCount ?? (object)DBNull.Value);
            command.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operationId;
            command.Parameters.AddWithValue("@createdBy", createdBy);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException exception) when (exception.Number is 52120 or 52121)
            {
                throw new InvalidOperationException(exception.Message, exception);
            }
        }
    }

    private static Guid DeriveLineOperationId(Guid invoiceOperationId, int index)
    {
        Span<byte> input = stackalloc byte[20];
        invoiceOperationId.TryWriteBytes(input);
        BitConverter.TryWriteBytes(input[16..], index);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        hash[7] = (byte)((hash[7] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }

    private static void AddDecimal(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 6;
        parameter.Value = value;
    }
}