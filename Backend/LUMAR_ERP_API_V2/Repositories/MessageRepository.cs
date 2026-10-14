using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Messages;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class MessageRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IMessageRepository
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
        r.NullableDateTime("DeliveredAtUtc"),
        r.NullableDateTime("ProcessingAtUtc"),
        r.NullableString("FailureReason"),
        r.NullableString("ProviderMessageId"),
        r.NullableString("PhoneNumber"),
        r.NullableString("ReferenceID"),
        r.NullableString("ReferenceType"),
        r.NullableString("IdempotencyKey"));

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

    public async Task<MessageTemplateDto?> CreateTemplateAsync(CreateMessageTemplateDto request, CancellationToken c)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey)) throw new ArgumentException("TemplateKey is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Channel)) throw new ArgumentException("Channel is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ArgumentException("Body is required.");

        const string sql = @"INSERT INTO dbo.MessageTemplates (TemplateKey, Title, Channel, Body, IsActive, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.MessageTemplateID, INSERTED.TemplateKey, INSERTED.Title, INSERTED.Channel, INSERTED.Body, INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc
VALUES (@templateKey, @title, @channel, @body, @isActive, SYSUTCDATETIME(), SYSUTCDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@templateKey", request.TemplateKey.Trim());
        command.Parameters.AddWithValue("@title", request.Title.Trim());
        command.Parameters.AddWithValue("@channel", request.Channel.Trim());
        command.Parameters.AddWithValue("@body", request.Body.Trim());
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        await using var reader = await command.ExecuteReaderAsync(c);
        if (!await reader.ReadAsync(c)) return null;

        return new MessageTemplateDto(reader.GetInt32(0), "dbo.MessageTemplates", reader.IsDBNull(1) ? null : reader.GetString(1), null, reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetBoolean(5), reader.GetDateTime(6), reader.GetDateTime(7));
    }

    public async Task<MessageTemplateDto?> UpdateTemplateAsync(int id, UpdateMessageTemplateDto request, CancellationToken c)
    {
        if (id <= 0) throw new ArgumentException("Template id must be positive.");
        if (string.IsNullOrWhiteSpace(request.TemplateKey)) throw new ArgumentException("TemplateKey is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Channel)) throw new ArgumentException("Channel is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ArgumentException("Body is required.");
        if (request.IsActive is null) throw new ArgumentException("IsActive is required.");

        const string sql = @"UPDATE dbo.MessageTemplates
SET TemplateKey = @templateKey,
    Title = @title,
    Channel = @channel,
    Body = @body,
    IsActive = @isActive,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE MessageTemplateID = @id;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@templateKey", request.TemplateKey.Trim());
        command.Parameters.AddWithValue("@title", request.Title.Trim());
        command.Parameters.AddWithValue("@channel", request.Channel.Trim());
        command.Parameters.AddWithValue("@body", request.Body.Trim());
        command.Parameters.AddWithValue("@isActive", request.IsActive.Value);
        var affected = await command.ExecuteNonQueryAsync(c);
        return affected > 0 ? (await GetTemplatesByIdAsync(id, c)).FirstOrDefault() : null;
    }

    public async Task<MessageTemplateDto?> ActivateTemplateAsync(int id, CancellationToken c)
    {
        const string sql = "UPDATE dbo.MessageTemplates SET IsActive = 1, UpdatedAtUtc = SYSUTCDATETIME() WHERE MessageTemplateID = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var affected = await command.ExecuteNonQueryAsync(c);
        return affected > 0 ? (await GetTemplatesByIdAsync(id, c)).FirstOrDefault() : null;
    }

    public async Task<MessageTemplateDto?> DeactivateTemplateAsync(int id, CancellationToken c)
    {
        const string sql = "UPDATE dbo.MessageTemplates SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME() WHERE MessageTemplateID = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(c);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var affected = await command.ExecuteNonQueryAsync(c);
        return affected > 0 ? (await GetTemplatesByIdAsync(id, c)).FirstOrDefault() : null;
    }

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

    public async Task<CustomerMessageDto?> ClaimNextAsync(string channel, int maxAttempts, TimeSpan staleProcessingWindow, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH candidate AS (
                SELECT TOP (1) m.*
                FROM dbo.CustomerMessages AS m
                WHERE m.Channel = @channel
                  AND m.DeliveryStatus IN (N'Pending', N'Failed')
                  AND m.AttemptCount < @maxAttempts
                  AND (m.DeliveryStatus = N'Pending' OR m.ProcessingAtUtc IS NULL OR DATEDIFF(MINUTE, m.ProcessingAtUtc, SYSUTCDATETIME()) >= @staleMinutes)
                ORDER BY m.CreatedAtUtc ASC, m.CustomerMessageID ASC
            )
            UPDATE candidate
            SET DeliveryStatus = N'Processing',
                ProcessingAtUtc = SYSUTCDATETIME(),
                AttemptCount = AttemptCount + 1,
                FailureReason = NULL
            OUTPUT INSERTED.CustomerMessageID,
                   INSERTED.CustomerID,
                   INSERTED.OrderID,
                   INSERTED.EventType,
                   INSERTED.TemplateKey,
                   INSERTED.Channel,
                   INSERTED.Title,
                   INSERTED.Body,
                   INSERTED.DeliveryStatus,
                   INSERTED.CreatedAtUtc,
                   INSERTED.SentAtUtc,
                   INSERTED.ReadAtUtc,
                   INSERTED.AttemptCount,
                   INSERTED.DeliveredAtUtc,
                   INSERTED.ProcessingAtUtc,
                   INSERTED.FailureReason,
                   INSERTED.ProviderMessageId,
                   INSERTED.PhoneNumber,
                   INSERTED.ReferenceID,
                   INSERTED.ReferenceType,
                   INSERTED.IdempotencyKey;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@channel", channel);
        command.Parameters.AddWithValue("@maxAttempts", maxAttempts);
        command.Parameters.AddWithValue("@staleMinutes", (int)staleProcessingWindow.TotalMinutes);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return Message(reader);
    }

    public async Task<CustomerMessageDto?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT CustomerMessageID,CustomerID,OrderID,EventType,TemplateKey,Channel,Title,Body,DeliveryStatus,CreatedAtUtc,SentAtUtc,ReadAtUtc,AttemptCount,DeliveredAtUtc,ProcessingAtUtc,FailureReason,ProviderMessageId,PhoneNumber,ReferenceID,ReferenceType,IdempotencyKey
            FROM dbo.CustomerMessages WITH (NOLOCK)
            WHERE ProviderMessageId = @providerMessageId;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@providerMessageId", providerMessageId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return Message(reader);
    }

    public async Task UpdateDeliveryStatusByProviderIdAsync(string providerMessageId, string status, DateTime occurredAtUtc, string? failureReason, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE dbo.CustomerMessages
            SET DeliveryStatus = CASE
                    WHEN @status = N'Sent' THEN N'Sent'
                    WHEN @status = N'Delivered' THEN CASE WHEN DeliveryStatus IN (N'Sent', N'Delivered', N'Read') THEN DeliveryStatus ELSE N'Delivered' END
                    WHEN @status = N'Read' THEN CASE WHEN DeliveryStatus = N'Read' THEN N'Read' ELSE N'Read' END
                    WHEN @status = N'Failed' THEN N'Failed'
                    ELSE DeliveryStatus
                END,
                SentAtUtc = CASE WHEN @status = N'Sent' AND SentAtUtc IS NULL THEN @occurredAtUtc ELSE SentAtUtc END,
                DeliveredAtUtc = CASE WHEN @status = N'Delivered' AND DeliveredAtUtc IS NULL THEN @occurredAtUtc ELSE DeliveredAtUtc END,
                ReadAtUtc = CASE WHEN @status = N'Read' AND ReadAtUtc IS NULL THEN @occurredAtUtc ELSE ReadAtUtc END,
                ProcessingAtUtc = CASE WHEN @status = N'Sent' THEN NULL ELSE ProcessingAtUtc END,
                FailureReason = CASE WHEN @status = N'Failed' AND @failureReason IS NOT NULL THEN @failureReason WHEN @status = N'Failed' AND @failureReason IS NULL THEN FailureReason ELSE NULL END
            WHERE ProviderMessageId = @providerMessageId;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@providerMessageId", providerMessageId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@occurredAtUtc", occurredAtUtc);
        command.Parameters.AddWithValue("@failureReason", (object?)failureReason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkSentAsync(int customerMessageId, string? providerMessageId, DateTime sentAtUtc, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE dbo.CustomerMessages
            SET DeliveryStatus = N'Sent',
                ProviderMessageId = @providerMessageId,
                SentAtUtc = @sentAtUtc,
                ProcessingAtUtc = NULL,
                FailureReason = NULL,
                DeliveredAtUtc = CASE WHEN DeliveredAtUtc IS NULL THEN @sentAtUtc ELSE DeliveredAtUtc END
            WHERE CustomerMessageID = @customerMessageId;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerMessageId", customerMessageId);
        command.Parameters.AddWithValue("@providerMessageId", (object?)providerMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("@sentAtUtc", sentAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(int customerMessageId, string failureReason, bool retryable, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE dbo.CustomerMessages
            SET DeliveryStatus = N'Failed',
                FailureReason = @failureReason,
                ProcessingAtUtc = NULL
            WHERE CustomerMessageID = @customerMessageId;";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customerMessageId", customerMessageId);
        command.Parameters.AddWithValue("@failureReason", failureReason.Length > 500 ? failureReason[..497] + "..." : failureReason);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
