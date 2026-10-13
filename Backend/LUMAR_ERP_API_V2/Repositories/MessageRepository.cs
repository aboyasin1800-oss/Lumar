using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Messages;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class MessageRepository(ReadOnlySqlConnectionFactory connections) : IMessageRepository
{
    private static CustomerMessageDto Message(SqlDataReader r) => new(
        r.GetInt32(0),
        r.GetInt32(1),
        r.NullableInt32("OrderID"),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6),
        r.GetString(7),
        r.GetString(8),
        r.GetDateTime(9),
        r.NullableDateTime("SentAtUtc"),
        r.NullableDateTime("ReadAtUtc"),
        r.GetInt32(12),
        r.NullableDateTime("DeliveredAtUtc"));

    private static CustomerNotificationDto Notification(SqlDataReader r) => new(
        r.GetInt32(0),
        r.GetInt32(1),
        r.NullableInt32("OrderID"),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6),
        r.GetBoolean(7),
        r.GetDateTime(8),
        r.NullableDateTime("ReadAtUtc"),
        r.NullableInt32("CustomerMessageID"));

    private static MessageTemplateDto Template(SqlDataReader r) => new(
        r.GetInt32(0),
        "dbo.MessageTemplates",
        r.NullableString("TemplateKey"),
        r.NullableString("TemplateName"),
        r.NullableString("Title"),
        r.NullableString("Channel"),
        r.NullableString("Body"),
        r.NullableBoolean("IsActive"),
        r.NullableDateTime("CreatedAtUtc"),
        r.NullableDateTime("UpdatedAtUtc"));

    private static SentMessageDto SentMessage(SqlDataReader r) => new(
        r.GetInt32(0),
        r.NullableInt32("TrackingCode"),
        r.NullableInt32("CustomerID"),
        r.NullableString("MessageText"),
        r.NullableDateTime("SentDate"));

    private static SentLogDto SentLog(SqlDataReader r) => new(
        r.GetInt32(0),
        r.NullableInt32("MessageID"));

    public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(CancellationToken c)
        => Q("SELECT CustomerMessageID,CustomerID,OrderID,EventType,TemplateKey,Channel,Title,Body,DeliveryStatus,CreatedAtUtc,SentAtUtc,ReadAtUtc,AttemptCount,DeliveredAtUtc FROM dbo.CustomerMessages ORDER BY CreatedAtUtc DESC,CustomerMessageID DESC", null, Message, c);

    public async Task<CustomerMessageDto?> GetCustomerMessageAsync(int id, CancellationToken c)
        => (await Q("SELECT CustomerMessageID,CustomerID,OrderID,EventType,TemplateKey,Channel,Title,Body,DeliveryStatus,CreatedAtUtc,SentAtUtc,ReadAtUtc,AttemptCount,DeliveredAtUtc FROM dbo.CustomerMessages WHERE CustomerMessageID=@id", id, Message, c)).FirstOrDefault();

    public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(int customerId, CancellationToken c)
        => Q("SELECT CustomerMessageID,CustomerID,OrderID,EventType,TemplateKey,Channel,Title,Body,DeliveryStatus,CreatedAtUtc,SentAtUtc,ReadAtUtc,AttemptCount,DeliveredAtUtc FROM dbo.CustomerMessages WHERE CustomerID=@id ORDER BY CreatedAtUtc DESC,CustomerMessageID DESC", customerId, Message, c);

    public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(CancellationToken c)
        => Q("SELECT CustomerNotificationID,CustomerID,OrderID,EventType,Title,Body,Status,IsRead,CreatedAtUtc,ReadAtUtc,CustomerMessageID FROM dbo.CustomerNotifications ORDER BY CreatedAtUtc DESC,CustomerNotificationID DESC", null, Notification, c);

    public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(int customerId, CancellationToken c)
        => Q("SELECT CustomerNotificationID,CustomerID,OrderID,EventType,Title,Body,Status,IsRead,CreatedAtUtc,ReadAtUtc,CustomerMessageID FROM dbo.CustomerNotifications WHERE CustomerID=@id ORDER BY CreatedAtUtc DESC,CustomerNotificationID DESC", customerId, Notification, c);

    public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesAsync(CancellationToken c)
        => Q("SELECT MessageTemplateID AS TemplateId,'dbo.MessageTemplates' AS SourceTable,TemplateKey,NULL AS TemplateName,Title,Channel,Body,IsActive,CreatedAtUtc,UpdatedAtUtc FROM dbo.MessageTemplates ORDER BY MessageTemplateID DESC", null, Template, c);

    public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesByIdAsync(int id, CancellationToken c)
        => Q("SELECT MessageTemplateID AS TemplateId,'dbo.MessageTemplates' AS SourceTable,TemplateKey,NULL AS TemplateName,Title,Channel,Body,IsActive,CreatedAtUtc,UpdatedAtUtc FROM dbo.MessageTemplates WHERE MessageTemplateID=@id ORDER BY MessageTemplateID DESC", id, Template, c);

    public Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c)
        => Q("SELECT MessageID,TrackingCode,CustomerID,MessageText,SentDate FROM dbo.Sent_Messages ORDER BY SentDate DESC,MessageID DESC", null, SentMessage, c);

    public Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c)
        => Q("SELECT ID,MessageID FROM dbo.Sent_Log ORDER BY ID DESC", null, SentLog, c);

    public async Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c)
    {
        const string sql = "SELECT TOP(1) MessageTemplateID, TemplateKey, Title, Channel, Body FROM dbo.MessageTemplates WHERE IsActive = 1 AND TemplateKey = @templateKey ORDER BY MessageTemplateID DESC";
        await using var connection = connections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@templateKey", templateKey);
        await using var reader = await command.ExecuteReaderAsync(c);
        if (!await reader.ReadAsync(c))
            return null;

        return new MessageTemplateRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4));
    }

    public async Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c)
    {
        const string sql = "SELECT TOP(1) 1 FROM dbo.CustomerMessages WITH (NOLOCK) WHERE IdempotencyKey = @idempotencyKey";
        await using var connection = connections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
        var value = await command.ExecuteScalarAsync(c);
        return value is not null;
    }

    public async Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
    {
        const string insertMessageSql = "INSERT INTO dbo.CustomerMessages (CustomerID,OrderID,EventType,TemplateKey,Channel,Title,Body,DeliveryStatus,CreatedAtUtc,AttemptCount,IdempotencyKey,PhoneNumber,ReferenceID,ReferenceType) OUTPUT INSERTED.CustomerMessageID VALUES (@customerId,@orderId,@eventType,@templateKey,@channel,@title,@body,N'Pending',GETUTCDATE(),0,@idempotencyKey,@phoneNumber,@referenceId,@referenceType)";
        const string insertNotificationSql = "INSERT INTO dbo.CustomerNotifications (CustomerID,OrderID,EventType,Title,Body,Status,IsRead,CreatedAtUtc,CustomerMessageID) OUTPUT INSERTED.CustomerNotificationID VALUES (@customerId,@orderId,@eventType,@title,@body,N'Unread',0,GETUTCDATE(),@customerMessageId)";

        await using var connection = connections.Create();
        await connection.OpenAsync(c);
        await using var transaction = connection.BeginTransaction();

        try
        {
            int? messageId;
            await using (var messageCommand = new SqlCommand(insertMessageSql, connection, transaction))
            {
                messageCommand.Parameters.AddWithValue("@customerId", customerId);
                messageCommand.Parameters.AddWithValue("@orderId", (object?)orderId ?? DBNull.Value);
                messageCommand.Parameters.AddWithValue("@eventType", eventType);
                messageCommand.Parameters.AddWithValue("@templateKey", templateKey);
                messageCommand.Parameters.AddWithValue("@channel", channel);
                messageCommand.Parameters.AddWithValue("@title", title);
                messageCommand.Parameters.AddWithValue("@body", body);
                messageCommand.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
                messageCommand.Parameters.AddWithValue("@phoneNumber", phoneNumber ?? string.Empty);
                messageCommand.Parameters.AddWithValue("@referenceId", referenceId ?? string.Empty);
                messageCommand.Parameters.AddWithValue("@referenceType", referenceType ?? string.Empty);
                var messageValue = await messageCommand.ExecuteScalarAsync(c);
                messageId = messageValue is int id ? id : null;
            }

            if (messageId is null or <= 0)
                throw new InvalidOperationException("Customer message creation did not return a valid identifier.");

            int? notificationId;
            await using (var notificationCommand = new SqlCommand(insertNotificationSql, connection, transaction))
            {
                notificationCommand.Parameters.AddWithValue("@customerId", customerId);
                notificationCommand.Parameters.AddWithValue("@orderId", (object?)orderId ?? DBNull.Value);
                notificationCommand.Parameters.AddWithValue("@eventType", eventType);
                notificationCommand.Parameters.AddWithValue("@title", title);
                notificationCommand.Parameters.AddWithValue("@body", body);
                notificationCommand.Parameters.AddWithValue("@customerMessageId", messageId.Value);
                var notificationValue = await notificationCommand.ExecuteScalarAsync(c);
                notificationId = notificationValue is int id ? id : null;
            }

            if (notificationId is null or <= 0)
                throw new InvalidOperationException("Customer notification creation did not return a valid identifier.");

            await transaction.CommitAsync(c);
            return (messageId, notificationId);
        }
        catch
        {
            await transaction.RollbackAsync(c);
            throw;
        }
    }

    public async Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
    {
        const string sql = "INSERT INTO dbo.CustomerMessages (CustomerID,OrderID,EventType,TemplateKey,Channel,Title,Body,DeliveryStatus,CreatedAtUtc,AttemptCount,IdempotencyKey,PhoneNumber,ReferenceID,ReferenceType) OUTPUT INSERTED.CustomerMessageID VALUES (@customerId,@orderId,@eventType,@templateKey,@channel,@title,@body,N'Pending',GETUTCDATE(),0,@idempotencyKey,@phoneNumber,@referenceId,@referenceType)";
        await using var connection = connections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderId", (object?)orderId ?? DBNull.Value);
        command.Parameters.AddWithValue("@eventType", eventType);
        command.Parameters.AddWithValue("@templateKey", templateKey);
        command.Parameters.AddWithValue("@channel", channel);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@body", body);
        command.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("@phoneNumber", phoneNumber ?? string.Empty);
        command.Parameters.AddWithValue("@referenceId", referenceId ?? string.Empty);
        command.Parameters.AddWithValue("@referenceType", referenceType ?? string.Empty);
        var value = await command.ExecuteScalarAsync(c);
        return value is int id ? id : null;
    }

    public async Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int customerMessageId, CancellationToken c)
    {
        const string sql = "INSERT INTO dbo.CustomerNotifications (CustomerID,OrderID,EventType,Title,Body,Status,IsRead,CreatedAtUtc,CustomerMessageID) OUTPUT INSERTED.CustomerNotificationID VALUES (@customerId,@orderId,@eventType,@title,@body,N'Unread',0,GETUTCDATE(),@customerMessageId)";
        await using var connection = connections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderId", (object?)orderId ?? DBNull.Value);
        command.Parameters.AddWithValue("@eventType", eventType);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@body", body);
        command.Parameters.AddWithValue("@customerMessageId", customerMessageId);
        var value = await command.ExecuteScalarAsync(c);
        return value is int id ? id : 0;
    }

    private async Task<IReadOnlyList<T>> Q<T>(string sql, int? id, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        if (id.HasValue)
            command.Parameters.AddWithValue("@id", id.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<T>();
        while (await reader.ReadAsync(ct))
            items.Add(map(reader));
        return items;
    }
}
