using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class SupplierMessagingRepository(ReadOnlySqlConnectionFactory connections) : ISupplierMessagingRepository
{
    public Task<IReadOnlyList<SupplierMessageDto>> GetMessagesAsync(int supplierId, int skip, int take, CancellationToken cancellationToken)
        => QueryAsync(
            "SELECT SupplierMessageId, SupplierId, MobileAccountId, MessageType, Subject, Body, Channel, RelatedEntityType, RelatedEntityId, DeliveryStatus, IsRead, ReadAtUtc, CreatedAtUtc, SentAtUtc, IdempotencyKey FROM dbo.SupplierMessages WHERE SupplierId = @supplierId ORDER BY CreatedAtUtc DESC, SupplierMessageId DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            supplierId,
            skip,
            take,
            reader => new SupplierMessageDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.GetString(9),
                reader.GetBoolean(10),
                reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                reader.GetDateTime(12),
                reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                reader.GetString(14)),
            cancellationToken);

    public async Task<SupplierMessageDto?> GetMessageAsync(int supplierId, int supplierMessageId, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT SupplierMessageId, SupplierId, MobileAccountId, MessageType, Subject, Body, Channel, RelatedEntityType, RelatedEntityId, DeliveryStatus, IsRead, ReadAtUtc, CreatedAtUtc, SentAtUtc, IdempotencyKey FROM dbo.SupplierMessages WHERE SupplierId = @supplierId AND SupplierMessageId = @supplierMessageId",
            supplierId,
            0,
            1,
            reader => new SupplierMessageDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.GetString(9),
                reader.GetBoolean(10),
                reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                reader.GetDateTime(12),
                reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                reader.GetString(14)),
            cancellationToken,
            supplierMessageId);

        return rows.FirstOrDefault();
    }

    public Task<IReadOnlyList<SupplierNotificationDto>> GetNotificationsAsync(int supplierId, int skip, int take, CancellationToken cancellationToken)
        => QueryAsync(
            "SELECT SupplierNotificationId, SupplierId, MobileAccountId, NotificationType, Title, Body, IsRead, ReadAtUtc, CreatedAtUtc, ExpiresAtUtc, ReferenceType, ReferenceId FROM dbo.SupplierNotifications WHERE SupplierId = @supplierId ORDER BY CreatedAtUtc DESC, SupplierNotificationId DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            supplierId,
            skip,
            take,
            reader => new SupplierNotificationDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11)),
            cancellationToken);

    public async Task<SupplierNotificationDto?> GetNotificationAsync(int supplierId, int supplierNotificationId, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT SupplierNotificationId, SupplierId, MobileAccountId, NotificationType, Title, Body, IsRead, ReadAtUtc, CreatedAtUtc, ExpiresAtUtc, ReferenceType, ReferenceId FROM dbo.SupplierNotifications WHERE SupplierId = @supplierId AND SupplierNotificationId = @supplierNotificationId",
            supplierId,
            0,
            1,
            reader => new SupplierNotificationDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11)),
            cancellationToken,
            supplierNotificationId);

        return rows.FirstOrDefault();
    }

    public Task<IReadOnlyList<SupplierAnnouncementDto>> GetAnnouncementsAsync(int supplierId, int skip, int take, CancellationToken cancellationToken)
        => QueryAsync(
            "SELECT SupplierAnnouncementId, SupplierId, Title, Body, IsActive, StartsAtUtc, EndsAtUtc, CreatedAtUtc FROM dbo.SupplierAnnouncements WHERE SupplierId = @supplierId ORDER BY CreatedAtUtc DESC, SupplierAnnouncementId DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            supplierId,
            skip,
            take,
            reader => new SupplierAnnouncementDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetDateTime(5),
                reader.GetDateTime(6),
                reader.GetDateTime(7)),
            cancellationToken);

    public async Task<SupplierAnnouncementDto?> GetAnnouncementAsync(int supplierId, int supplierAnnouncementId, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT SupplierAnnouncementId, SupplierId, Title, Body, IsActive, StartsAtUtc, EndsAtUtc, CreatedAtUtc FROM dbo.SupplierAnnouncements WHERE SupplierId = @supplierId AND SupplierAnnouncementId = @supplierAnnouncementId",
            supplierId,
            0,
            1,
            reader => new SupplierAnnouncementDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetDateTime(5),
                reader.GetDateTime(6),
                reader.GetDateTime(7)),
            cancellationToken,
            supplierAnnouncementId);

        return rows.FirstOrDefault();
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        int supplierId,
        int skip,
        int take,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken,
        int? secondaryId = null)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        command.Parameters.AddWithValue("@skip", skip);
        command.Parameters.AddWithValue("@take", take);
        if (secondaryId.HasValue)
        {
            var name = sql.Contains("SupplierMessageId") ? "@supplierMessageId" : sql.Contains("SupplierNotificationId") ? "@supplierNotificationId" : "@supplierAnnouncementId";
            command.Parameters.AddWithValue(name, secondaryId.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(map(reader));
        }

        return results;
    }
}
