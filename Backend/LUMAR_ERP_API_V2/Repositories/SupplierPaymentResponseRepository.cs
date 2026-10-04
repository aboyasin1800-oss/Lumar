using System.Data;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class SupplierPaymentResponseRepository(OperationalSqlConnectionFactory connections) : ISupplierPaymentResponseRepository
{
    public async Task<SupplierPaymentDto?> GetPaymentAsync(int supplierPaymentId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "SELECT SupplierPaymentId, SupplierId, PaymentNumber, PaymentDate, Amount, PaymentMethod, ReferenceNumber, Notes, CreatedAt, JournalEntryId " +
            "FROM dbo.SupplierPayments WHERE SupplierPaymentId = @supplierPaymentId",
            connection);
        command.Parameters.AddWithValue("@supplierPaymentId", supplierPaymentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapPayment(reader);
    }

    public async Task<SupplierPaymentAcknowledgementDto?> GetAcknowledgementAsync(int supplierPaymentId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "SELECT SupplierPaymentAcknowledgementId, SupplierPaymentId, SupplierId, Status, CreatedAt, AcknowledgedAt, Notes, SourceOperationId " +
            "FROM dbo.SupplierPaymentAcknowledgements WHERE SupplierPaymentId = @supplierPaymentId ORDER BY CreatedAt DESC",
            connection);
        command.Parameters.AddWithValue("@supplierPaymentId", supplierPaymentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapAcknowledgement(reader);
    }

    public async Task<PaymentDisputeDto?> GetDisputeAsync(int supplierPaymentId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "SELECT PaymentDisputeId, SupplierPaymentId, SupplierId, DisputedAmount, Status, Reason, CreatedAt, ResolvedAt, SourceOperationId " +
            "FROM dbo.PaymentDisputes WHERE SupplierPaymentId = @supplierPaymentId ORDER BY CreatedAt DESC",
            connection);
        command.Parameters.AddWithValue("@supplierPaymentId", supplierPaymentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapDispute(reader);
    }

    public async Task<SupplierPaymentAcknowledgementDto> CreateAcknowledgementAsync(int supplierPaymentId, int supplierId, string? notes, Guid sourceOperationId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                "INSERT INTO dbo.SupplierPaymentAcknowledgements (SupplierPaymentId, SupplierId, Status, Notes, SourceOperationId, CreatedAt, AcknowledgedAt) " +
                "OUTPUT INSERTED.SupplierPaymentAcknowledgementId, INSERTED.SupplierPaymentId, INSERTED.SupplierId, INSERTED.Status, INSERTED.CreatedAt, INSERTED.AcknowledgedAt, INSERTED.Notes, INSERTED.SourceOperationId " +
                "VALUES (@supplierPaymentId, @supplierId, N'Acknowledged', @notes, @sourceOperationId, SYSUTCDATETIME(), SYSUTCDATETIME())",
                connection);
            command.Parameters.AddWithValue("@supplierPaymentId", supplierPaymentId);
            command.Parameters.AddWithValue("@supplierId", supplierId);
            command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
            command.Parameters.AddWithValue("@sourceOperationId", sourceOperationId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Supplier payment acknowledgement could not be created.");

            return MapAcknowledgement(reader);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("A supplier payment acknowledgement already exists for this payment.", ex);
        }
    }

    public async Task<PaymentDisputeDto> CreateDisputeAsync(int supplierPaymentId, int supplierId, decimal? disputedAmount, string reason, Guid sourceOperationId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                "INSERT INTO dbo.PaymentDisputes (SupplierPaymentId, SupplierId, DisputedAmount, Status, Reason, SourceOperationId, CreatedAt, ResolvedAt) " +
                "OUTPUT INSERTED.PaymentDisputeId, INSERTED.SupplierPaymentId, INSERTED.SupplierId, INSERTED.DisputedAmount, INSERTED.Status, INSERTED.Reason, INSERTED.CreatedAt, INSERTED.ResolvedAt, INSERTED.SourceOperationId " +
                "VALUES (@supplierPaymentId, @supplierId, @disputedAmount, N'Open', @reason, @sourceOperationId, SYSUTCDATETIME(), NULL)",
                connection);
            command.Parameters.AddWithValue("@supplierPaymentId", supplierPaymentId);
            command.Parameters.AddWithValue("@supplierId", supplierId);
            command.Parameters.AddWithValue("@disputedAmount", disputedAmount ?? 0m);
            command.Parameters.AddWithValue("@reason", reason.Trim());
            command.Parameters.AddWithValue("@sourceOperationId", sourceOperationId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Supplier payment dispute could not be created.");

            return MapDispute(reader);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("A supplier payment dispute already exists for this payment.", ex);
        }
    }

    private static SupplierPaymentDto MapPayment(SqlDataReader r)
        => new(
            r.GetInt32(0),
            r.GetInt32(1),
            r.GetString(2),
            r.GetDateTime(3),
            r.GetDecimal(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.GetDateTime(8),
            r.IsDBNull(9) ? null : (int?)r.GetInt32(9));

    private static SupplierPaymentAcknowledgementDto MapAcknowledgement(SqlDataReader r)
        => new(
            r.GetInt32(0),
            r.GetInt32(1),
            r.GetInt32(2),
            r.GetString(3),
            r.GetDateTime(4),
            r.IsDBNull(5) ? null : r.GetDateTime(5),
            r.IsDBNull(6) ? null : r.GetString(6),
            r.IsDBNull(7) ? null : (Guid?)r.GetGuid(7));

    private static PaymentDisputeDto MapDispute(SqlDataReader r)
        => new(
            r.GetInt32(0),
            r.GetInt32(1),
            r.GetInt32(2),
            r.IsDBNull(3) ? null : r.GetDecimal(3),
            r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.GetDateTime(6),
            r.IsDBNull(7) ? null : r.GetDateTime(7),
            r.IsDBNull(8) ? null : (Guid?)r.GetGuid(8));
}
