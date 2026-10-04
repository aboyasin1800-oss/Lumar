using LUMAR_ERP_API_V2.Data; using LUMAR_ERP_API_V2.DTOs.Suppliers; using Microsoft.Data.SqlClient;
namespace LUMAR_ERP_API_V2.Repositories;
public sealed class SupplierRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : ISupplierRepository
{
    public async Task<SupplierDetailsDto> CreateAsync(CreateSupplierRequestDto supplier, CancellationToken ct)
    {
        var code = supplier.SupplierCode.Trim();
        var name = supplier.SupplierName.Trim();
        if (code.Length == 0 || name.Length == 0) throw new ArgumentException("رمز المورد واسمه مطلوبان.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            await using (var applicationLock = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000; SELECT @result;", connection, transaction))
            {
                applicationLock.Parameters.AddWithValue("@resource", $"SupplierCode:{code.ToUpperInvariant()}");
                if (Convert.ToInt32(await applicationLock.ExecuteScalarAsync(ct)) < 0)
                    throw new InvalidOperationException("تعذر حجز رمز المورد. أعد المحاولة.");
            }

            await using (var duplicate = new SqlCommand("SELECT 1 FROM dbo.Suppliers WITH (UPDLOCK, HOLDLOCK) WHERE SupplierCode=@code", connection, transaction))
            {
                duplicate.Parameters.AddWithValue("@code", code);
                if (await duplicate.ExecuteScalarAsync(ct) is not null)
                    throw new InvalidOperationException("يوجد مورد مسجل بالرمز نفسه.");
            }

            const string sql = "INSERT INTO dbo.Suppliers (SupplierCode,SupplierName,Phone,Email,Address,IsActive,CreatedAt) OUTPUT INSERTED.SupplierId,INSERTED.SupplierCode,INSERTED.SupplierName,INSERTED.Phone,INSERTED.Email,INSERTED.Address,INSERTED.IsActive,INSERTED.CreatedAt,INSERTED.UpdatedAt VALUES (@code,@name,@phone,@email,@address,1,SYSUTCDATETIME())";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@code", code);
            command.Parameters.AddWithValue("@name", name);
            AddNullable(command, "@phone", supplier.Phone);
            AddNullable(command, "@email", supplier.Email);
            AddNullable(command, "@address", supplier.Address);
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            var created = new SupplierDetailsDto(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.NullableString("Phone"),reader.NullableString("Email"),reader.NullableString("Address"),reader.GetBoolean(6),reader.GetDateTime(7),reader.NullableDateTime("UpdatedAt"));
            await reader.CloseAsync();
            await transaction.CommitAsync(ct);
            return created;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<IReadOnlyList<SupplierListDto>> GetAllAsync(CancellationToken ct) => QueryAsync("SELECT SupplierId, SupplierCode, SupplierName, Phone, Email, IsActive FROM dbo.Suppliers ORDER BY SupplierName, SupplierId", null, r => new SupplierListDto(r.GetInt32(0),r.GetString(1),r.GetString(2),r.NullableString("Phone"),r.NullableString("Email"),r.GetBoolean(5)),ct);
    public async Task<SupplierDetailsDto?> GetByIdAsync(int id,CancellationToken ct) => (await QueryAsync("SELECT SupplierId, SupplierCode, SupplierName, Phone, Email, Address, IsActive, CreatedAt, UpdatedAt FROM dbo.Suppliers WHERE SupplierId=@id",id,r=>new SupplierDetailsDto(r.GetInt32(0),r.GetString(1),r.GetString(2),r.NullableString("Phone"),r.NullableString("Email"),r.NullableString("Address"),r.GetBoolean(6),r.GetDateTime(7),r.NullableDateTime("UpdatedAt")),ct)).FirstOrDefault();
    public async Task<SupplierHomeSummaryDto?> GetHomeSummaryAsync(int supplierId, CancellationToken ct)
    {
        var rows = await QueryAsync(
            "SELECT " +
            "(SELECT TOP 1 BalanceAfterTransaction FROM dbo.SupplierLedgerEntries WHERE SupplierId=@id ORDER BY CreatedAt DESC, SupplierLedgerEntryId DESC) AS CurrentBalance, " +
            "(SELECT COUNT(*) FROM dbo.SupplierInvoices WHERE SupplierId=@id) AS InvoiceCount, " +
            "(SELECT COUNT(*) FROM dbo.SupplierPayments WHERE SupplierId=@id) AS PaymentCount, " +
            "(SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE SupplierId=@id) AS GoodsReceiptCount, " +
            "(SELECT COUNT(*) FROM dbo.SupplierPaymentAcknowledgements WHERE SupplierId=@id) AS PendingAcknowledgements, " +
            "(SELECT COUNT(*) FROM dbo.PaymentDisputes WHERE SupplierId=@id AND Status = N'Open') AS OpenDisputes FROM dbo.Suppliers WHERE SupplierId=@id",
            supplierId,
            r => new SupplierHomeSummaryDto(
                r.IsDBNull(0) ? 0m : r.GetDecimal(0),
                r.GetInt32(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetInt32(5)),
            ct);

        return rows.FirstOrDefault();
    }

    public async Task<SupplierPaymentResponseStatusDto?> GetPaymentResponseStatusAsync(int supplierId, int paymentId, CancellationToken ct)
    {
        var rows = await QueryAsync(
            "SELECT sp.SupplierPaymentId, sp.SupplierId, sp.PaymentNumber, " +
            "CASE WHEN ack.SupplierPaymentAcknowledgementId IS NOT NULL THEN ack.Status ELSE N'NotAcknowledged' END AS AcknowledgementStatus, " +
            "CASE WHEN dispute.PaymentDisputeId IS NOT NULL THEN dispute.Status ELSE N'NoDispute' END AS DisputeStatus, " +
            "dispute.DisputedAmount, ack.AcknowledgedAt, dispute.ResolvedAt, sp.CreatedAt " +
            "FROM dbo.SupplierPayments sp " +
            "LEFT JOIN dbo.SupplierPaymentAcknowledgements ack ON ack.SupplierPaymentId = sp.SupplierPaymentId AND ack.SupplierId = @supplierId " +
            "LEFT JOIN dbo.PaymentDisputes dispute ON dispute.SupplierPaymentId = sp.SupplierPaymentId AND dispute.SupplierId = @supplierId " +
            "WHERE sp.SupplierPaymentId = @paymentId AND sp.SupplierId = @supplierId",
            supplierId,
            r => new SupplierPaymentResponseStatusDto(
                r.GetInt32(0),
                r.GetInt32(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.IsDBNull(5) ? null : (decimal?)r.GetDecimal(5),
                r.IsDBNull(6) ? null : (DateTime?)r.GetDateTime(6),
                r.IsDBNull(7) ? null : (DateTime?)r.GetDateTime(7),
                r.GetDateTime(8)),
            ct,
            paymentId);

        return rows.FirstOrDefault();
    }

    public Task<IReadOnlyList<SupplierTransactionDto>> GetTransactionsAsync(int id,CancellationToken ct) => QueryAsync("SELECT SupplierTransactionId,SupplierId,ReferenceNumber,TransactionType,Amount,Description,CreatedAt FROM dbo.SupplierTransactions WHERE SupplierId=@id ORDER BY CreatedAt DESC,SupplierTransactionId DESC",id,r=>new SupplierTransactionDto(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetString(3),r.GetDecimal(4),r.NullableString("Description"),r.GetDateTime(6)),ct);
    public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int id,CancellationToken ct) => QueryAsync("SELECT SupplierLedgerEntryId,SupplierId,ReferenceNumber,DebitAmount,CreditAmount,BalanceAfterTransaction,CreatedAt FROM dbo.SupplierLedgerEntries WHERE SupplierId=@id ORDER BY CreatedAt DESC,SupplierLedgerEntryId DESC",id,r=>new SupplierLedgerEntryDto(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5),r.GetDateTime(6)),ct);
    public Task<IReadOnlyList<SupplierInvoiceDto>> GetInvoicesAsync(int id,CancellationToken ct) => QueryAsync("SELECT SupplierInvoiceId,SupplierId,PurchaseOrderId,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,AmountPaid,Status,Notes,CreatedAt FROM dbo.SupplierInvoices WHERE SupplierId=@id ORDER BY InvoiceDate DESC,SupplierInvoiceId DESC",id,MapInvoice,ct);
    public Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(int id,CancellationToken ct) => QueryAsync("SELECT SupplierPaymentId,SupplierId,PaymentNumber,PaymentDate,Amount,PaymentMethod,ReferenceNumber,Notes,CreatedAt,JournalEntryId FROM dbo.SupplierPayments WHERE SupplierId=@id ORDER BY PaymentDate DESC,SupplierPaymentId DESC",id,MapPayment,ct);
    public Task<IReadOnlyList<SupplierPaymentAllocationDto>> GetAllocationsAsync(int id,CancellationToken ct) => QueryAsync("SELECT a.SupplierPaymentAllocationId,a.SupplierPaymentId,a.SupplierInvoiceId,a.AllocatedAmount,a.AllocationDate,a.Notes,a.CreatedAt FROM dbo.SupplierPaymentAllocations a JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@id ORDER BY a.AllocationDate DESC,a.SupplierPaymentAllocationId DESC",id,MapAllocation,ct);
    private static SupplierInvoiceDto MapInvoice(SqlDataReader r)=>new(r.GetInt32(0),r.GetInt32(1),r.NullableInt32("PurchaseOrderId"),r.GetString(3),r.GetDateTime(4),r.GetDateTime(5),r.GetDecimal(6),r.GetDecimal(7),r.GetString(8),r.NullableString("Notes"),r.GetDateTime(10)); private static SupplierPaymentDto MapPayment(SqlDataReader r)=>new(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetDateTime(3),r.GetDecimal(4),r.NullableString("PaymentMethod"),r.NullableString("ReferenceNumber"),r.NullableString("Notes"),r.GetDateTime(8),r.NullableInt32("JournalEntryId")); private static SupplierPaymentAllocationDto MapAllocation(SqlDataReader r)=>new(r.GetInt32(0),r.GetInt32(1),r.GetInt32(2),r.GetDecimal(3),r.GetDateTime(4),r.NullableString("Notes"),r.GetDateTime(6));
    private static void AddNullable(SqlCommand command,string name,string? value)=>command.Parameters.AddWithValue(name,string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim());
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql,int? id,Func<SqlDataReader,T> map,CancellationToken ct, int? paymentId = null){await using var c=connections.Create();await c.OpenAsync(ct);await using var cmd=new SqlCommand(sql,c);if(id.HasValue)cmd.Parameters.AddWithValue("@id",id.Value);if(paymentId.HasValue)cmd.Parameters.AddWithValue("@paymentId",paymentId.Value);await using var r=await cmd.ExecuteReaderAsync(ct);var items=new List<T>();while(await r.ReadAsync(ct))items.Add(map(r));return items;}
}