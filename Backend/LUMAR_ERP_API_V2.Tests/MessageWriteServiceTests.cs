using System.Collections.Generic;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class MessageWriteServiceTests
{
    [Fact]
    public void MessageTemplateRenderer_RendersKnownTokensCaseInsensitive()
    {
        var renderer = new MessageTemplateRenderer();
        var body = "مرحباً {{CustomerName}}، تم إنشاء الطلب رقم {{OrderId}} بقيمة {{Amount}} ريال.";

        var rendered = renderer.Render(body, new Dictionary<string, object?>
        {
            ["CustomerName"] = "سالم",
            ["OrderId"] = 42,
            ["Amount"] = 149.99m,
        });

        Assert.Equal("مرحباً سالم، تم إنشاء الطلب رقم 42 بقيمة 149.99 ريال.", rendered);
    }

    [Fact]
    public async Task MessageRepository_GetCustomerMessagesAsync_ProjectsAllRequiredMessageFields()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Message repository validation must target LUMAR_ERP_ES_VALIDATION.");

        var uniqueSuffix = "ValidationCustomerProjection-" + Guid.NewGuid().ToString("N")[..12];
        var channel = "ValidationInApp-" + Guid.NewGuid().ToString("N")[..16];
        var messageId = await SeedQueuedMessageAsync(connectionString, channel, uniqueSuffix);
        try
        {
            var repository = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

            var messages = await repository.GetCustomerMessagesAsync(CancellationToken.None);
            var row = messages.Single(x => x.CustomerMessageId == messageId);

            var expectedProviderMessageId = "provider-live-" + uniqueSuffix;

            Assert.Equal("ValidationCustomerProjection", row.TemplateKey);
            Assert.Equal(expectedProviderMessageId, row.ProviderMessageId);
            Assert.Equal("770000000", row.PhoneNumber);
            Assert.Equal("validation-" + uniqueSuffix, row.ReferenceId);
            Assert.Equal("Validation", row.ReferenceType);
            Assert.Equal("validation-" + uniqueSuffix, row.IdempotencyKey);
            Assert.NotNull(row.ProcessingAtUtc);
            Assert.Equal("Transient failure", row.FailureReason);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", cleanup);
            command.Parameters.AddWithValue("@id", messageId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task ConcurrentDuplicateIdempotencyKey_CreatesSingleMessageAndSingleNotification()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Concurrent idempotency validation must target LUMAR_ERP_ES_VALIDATION.");

        var channel = "ValidationInApp-" + Guid.NewGuid().ToString("N")[..16];
        var idempotencyKey = "validation-duplicate-" + Guid.NewGuid().ToString("N");
        var repo1 = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));
        var repo2 = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

        try
        {
            var tasks = new[]
            {
                repo1.CreateCustomerMessageAndNotificationAsync(
                    101,
                    9001,
                    "ValidationDuplicateIdempotency",
                    "ValidationCustomerProjection",
                    channel,
                    "Validation title",
                    "Validation body",
                    "770000000",
                    "validation-duplicate", 
                    "Validation",
                    idempotencyKey,
                    CancellationToken.None),
                repo2.CreateCustomerMessageAndNotificationAsync(
                    101,
                    9001,
                    "ValidationDuplicateIdempotency",
                    "ValidationCustomerProjection",
                    channel,
                    "Validation title",
                    "Validation body",
                    "770000000",
                    "validation-duplicate",
                    "Validation",
                    idempotencyKey,
                    CancellationToken.None)
            };

            var results = await Task.WhenAll(tasks);
            var messageIds = results.Select(x => x.MessageId).Where(x => x is not null).Distinct().ToArray();
            var notificationIds = results.Select(x => x.NotificationId).Where(x => x is not null).Distinct().ToArray();

            Assert.Single(messageIds);
            Assert.Single(notificationIds);

            await using var verify = new SqlConnection(connectionString);
            await verify.OpenAsync();
            await using var messageCountCommand = new SqlCommand("SELECT COUNT(*) FROM dbo.CustomerMessages WHERE IdempotencyKey = @idempotencyKey;", verify);
            messageCountCommand.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
            var messageCount = (int)(await messageCountCommand.ExecuteScalarAsync() ?? 0);

            await using var notificationCountCommand = new SqlCommand(@"
                SELECT COUNT(*)
                FROM dbo.CustomerNotifications n
                INNER JOIN dbo.CustomerMessages m ON m.CustomerMessageID = n.CustomerMessageID
                WHERE m.IdempotencyKey = @idempotencyKey;", verify);
            notificationCountCommand.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
            var notificationCount = (int)(await notificationCountCommand.ExecuteScalarAsync() ?? 0);

            Assert.Equal(1, messageCount);
            Assert.Equal(1, notificationCount);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerNotifications WHERE CustomerMessageID IN (SELECT CustomerMessageID FROM dbo.CustomerMessages WHERE IdempotencyKey = @idempotencyKey); DELETE FROM dbo.CustomerMessages WHERE IdempotencyKey = @idempotencyKey;", cleanup);
            command.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_ClaimNextAsync_TracksAttemptCountAndStopsAtMaxAttempts()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Attempt-count validation must target LUMAR_ERP_ES_VALIDATION.");

        var repo = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

        var channel = "ValidationInApp-" + Guid.NewGuid().ToString("N")[..16];
        var id = await SeedQueuedMessageAsync(connectionString, channel, "ValidationAttemptCount-" + Guid.NewGuid().ToString("N")[..12], deliveryStatus: "Pending", attemptCount: 0, processingAtUtc: null, failureReason: null);

        try
        {
            var first = await repo.ClaimNextAsync(channel, 2, TimeSpan.FromMinutes(30), CancellationToken.None);
            Assert.NotNull(first);
            Assert.Equal(1, first!.AttemptCount);
            Assert.Equal("Processing", first.DeliveryStatus);

            await repo.MarkFailedAsync(id, "Transient failure", true, CancellationToken.None);

            var second = await repo.ClaimNextAsync(channel, 2, TimeSpan.FromMinutes(30), CancellationToken.None);
            Assert.NotNull(second);
            Assert.Equal(2, second!.AttemptCount);
            Assert.Equal("Processing", second.DeliveryStatus);

            await repo.MarkFailedAsync(id, "Transient failure", true, CancellationToken.None);

            var third = await repo.ClaimNextAsync(channel, 2, TimeSpan.FromMinutes(30), CancellationToken.None);
            Assert.Null(third);

            var staleAtLimitId = await SeedQueuedMessageAsync(
                connectionString,
                channel,
                "ValidationStaleAtLimit-" + Guid.NewGuid().ToString("N")[..12],
                deliveryStatus: "Processing",
                attemptCount: 2,
                processingAtUtc: DateTime.UtcNow.AddMinutes(-31),
                failureReason: "Transient failure");

            var staleAtLimit = await repo.ClaimNextAsync(channel, 2, TimeSpan.FromMinutes(30), CancellationToken.None);
            Assert.Null(staleAtLimit);

            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var delete = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID IN (@one, @two);", cleanup);
            delete.Parameters.AddWithValue("@one", id);
            delete.Parameters.AddWithValue("@two", staleAtLimitId);
            await delete.ExecuteNonQueryAsync();
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var delete = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", cleanup);
            delete.Parameters.AddWithValue("@id", id);
            await delete.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_ClaimNextAsync_RespectsPersistedRetryabilityState()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Retryability validation must target LUMAR_ERP_ES_VALIDATION.");

        var repo = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

        var retryableChannel = "ValidationRetryable-" + Guid.NewGuid().ToString("N")[..16];
        var permanentChannel = "ValidationPermanent-" + Guid.NewGuid().ToString("N")[..16];
        var retryableId = await SeedQueuedMessageAsync(connectionString, retryableChannel, "ValidationRetryable-" + Guid.NewGuid().ToString("N")[..12], deliveryStatus: "Failed", attemptCount: 0, failureReason: "Transient failure");
        var permanentId = await SeedQueuedMessageAsync(connectionString, permanentChannel, "ValidationPermanent-" + Guid.NewGuid().ToString("N")[..12], deliveryStatus: "Failed", attemptCount: 0, failureReason: "Invalid WhatsApp destination.");

        try
        {
            await repo.MarkFailedAsync(retryableId, "Transient failure", true, CancellationToken.None);
            await repo.MarkFailedAsync(permanentId, "Invalid WhatsApp destination.", false, CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"SELECT IsRetryable FROM dbo.CustomerMessages WHERE CustomerMessageID IN (@retryableId, @permanentId) ORDER BY CustomerMessageID;", connection);
            command.Parameters.AddWithValue("@retryableId", retryableId);
            command.Parameters.AddWithValue("@permanentId", permanentId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(true, reader.GetBoolean(0));
            Assert.True(await reader.ReadAsync());
            Assert.Equal(false, reader.GetBoolean(0));

            var retryableClaim = await repo.ClaimNextAsync(retryableChannel, 3, TimeSpan.FromMinutes(30), CancellationToken.None);
            var permanentClaim = await repo.ClaimNextAsync(permanentChannel, 3, TimeSpan.FromMinutes(30), CancellationToken.None);

            Assert.NotNull(retryableClaim);
            Assert.Equal(retryableId, retryableClaim!.CustomerMessageId);
            Assert.Null(permanentClaim);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var delete = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID IN (@retryableId, @permanentId);", cleanup);
            delete.Parameters.AddWithValue("@retryableId", retryableId);
            delete.Parameters.AddWithValue("@permanentId", permanentId);
            await delete.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public void ManualCampaignRequest_BindsNumericRecipientModeValues()
    {
        var single = System.Text.Json.JsonSerializer.Deserialize<ManualCampaignRequest>("""
            {
              "campaignReference": "campaign-001",
              "recipientMode": 0,
              "customerId": 15,
              "messageText": "اختبار من لومار لنجاح الحملات",
              "templateKey": "general-campaign-smart",
              "title": "حملة إعلانية"
            }
            """);

        var all = System.Text.Json.JsonSerializer.Deserialize<ManualCampaignRequest>("""
            {
              "campaignReference": "campaign-002",
              "recipientMode": 1,
              "customerId": 15,
              "messageText": "نتيجة جيدة",
              "templateKey": "general-campaign-smart",
              "title": "حملة إعلانية"
            }
            """);

        Assert.NotNull(single);
        Assert.Equal(ManualCampaignRecipientMode.SingleCustomer, single!.RecipientMode);
        Assert.NotNull(all);
        Assert.Equal(ManualCampaignRecipientMode.AllEligibleCustomers, all!.RecipientMode);
    }

    [Fact]
    public void ManualCampaignRequest_RejectsUnknownRecipientModeValue()
    {
        var payload = """
            {
              "campaignReference": "campaign-003",
              "recipientMode": 99,
              "customerId": 15,
              "messageText": "اختبار",
              "templateKey": "general-campaign-smart",
              "title": "حملة"
            }
            """;

        var exception = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<ManualCampaignRequest>(payload));

        Assert.Contains("recipientMode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MessageWriteService_RespectsMasterAndReferralRuntimeSettings()
    {
        var repo = new FakeMessageRepository();
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: false,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo);

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Null(result.CustomerMessageId);
        Assert.Equal("OrderCreated", result.TemplateKey);
    }

    [Fact]
    public async Task ManualCampaignService_SingleCustomer_UsesOfficialCustomerAndMessageWriteServiceOnce()
    {
        var messageWrite = new RecordingMessageWriteService();
        var customerRepo = new FakeCustomerRepository(
            new CustomerDetailsDto(15, "C-15", "سالم", "967775106545", null, null, null, null, null, true, 0m, 0, 0m, null));
        var service = new ManualCampaignService(
            customerRepo,
            messageWrite,
            new YemenPhoneNumberNormalizer(),
            new FakeSettingsRepository(new MessageSettingsDto(true, true, false, true, true, true)));

        var result = await service.ProcessAsync(new ManualCampaignRequest(
            CampaignReference: "manual-campaign-001",
            RecipientMode: ManualCampaignRecipientMode.SingleCustomer,
            CustomerId: 15,
            MessageText: "عرض خاص لعملائنا",
            TemplateKey: "general-campaign-smart",
            Title: "حملة إعلانية",
            CancellationToken.None), CancellationToken.None);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Single(messageWrite.Requests);
        Assert.Equal(15, messageWrite.Requests[0].CustomerId);
        Assert.Equal("general-campaign-smart", messageWrite.Requests[0].TemplateKey);
        Assert.Equal("manual-campaign-001", messageWrite.Requests[0].ReferenceId);
    }

    [Fact]
    public async Task ManualCampaignService_InvalidPhone_SkipsWithoutCallingMessageWriteService()
    {
        var messageWrite = new RecordingMessageWriteService();
        var customerRepo = new FakeCustomerRepository(
            new CustomerDetailsDto(22, "C-22", "علي", "0775106545", null, null, null, null, null, true, 0m, 0, 0m, null));
        var service = new ManualCampaignService(
            customerRepo,
            messageWrite,
            new YemenPhoneNumberNormalizer(),
            new FakeSettingsRepository(new MessageSettingsDto(true, true, false, true, true, true)));

        var result = await service.ProcessAsync(new ManualCampaignRequest(
            CampaignReference: "manual-campaign-invalid-phone",
            RecipientMode: ManualCampaignRecipientMode.SingleCustomer,
            CustomerId: 22,
            MessageText: "رسالة تجريبية",
            TemplateKey: "general-campaign-smart",
            Title: "حملة",
            CancellationToken.None), CancellationToken.None);

        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Empty(messageWrite.Requests);
    }

    [Fact]
    public async Task ManualCampaignService_DuplicateCampaignReferenceForSameCustomer_IsBlocked()
    {
        var messageWrite = new RecordingMessageWriteService();
        var customerRepo = new FakeCustomerRepository(
            new CustomerDetailsDto(7, "C-7", "أحمد", "967775106545", null, null, null, null, null, true, 0m, 0, 0m, null));
        var service = new ManualCampaignService(
            customerRepo,
            messageWrite,
            new YemenPhoneNumberNormalizer(),
            new FakeSettingsRepository(new MessageSettingsDto(true, true, false, true, true, true)));

        var first = await service.ProcessAsync(new ManualCampaignRequest(
            CampaignReference: "duplicate-campaign",
            RecipientMode: ManualCampaignRecipientMode.SingleCustomer,
            CustomerId: 7,
            MessageText: "عرض", 
            TemplateKey: "general-campaign-smart",
            Title: "حملة",
            CancellationToken.None), CancellationToken.None);

        var second = await service.ProcessAsync(new ManualCampaignRequest(
            CampaignReference: "duplicate-campaign",
            RecipientMode: ManualCampaignRecipientMode.SingleCustomer,
            CustomerId: 7,
            MessageText: "عرض",
            TemplateKey: "general-campaign-smart",
            Title: "حملة",
            CancellationToken.None), CancellationToken.None);

        Assert.Equal(1, first.CreatedCount);
        Assert.Equal(1, second.DuplicateCount);
        Assert.Single(messageWrite.Requests);
    }

    [Fact]
    public async Task MessageService_ExposesTemplateManagementContract()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageService(repo);

        var created = await service.CreateTemplateAsync(new CreateMessageTemplateDto(
            TemplateKey: "OrderCreated",
            Title: "طلب جديد",
            Channel: "InApp",
            Body: "مرحبا {{CustomerName}}",
            IsActive: true), CancellationToken.None);

        var updated = await service.UpdateTemplateAsync(created!.TemplateId, new UpdateMessageTemplateDto(
            TemplateKey: "OrderCreated",
            Title: "تحديث الطلب",
            Channel: "InApp",
            Body: "مرحبا {{CustomerName}} تم تحديث الطلب",
            IsActive: true), CancellationToken.None);

        var activated = await service.ActivateTemplateAsync(updated!.TemplateId, CancellationToken.None);
        var deactivated = await service.DeactivateTemplateAsync(activated!.TemplateId, CancellationToken.None);

        Assert.NotNull(created);
        Assert.NotNull(updated);
        Assert.NotNull(activated);
        Assert.NotNull(deactivated);
        Assert.Equal("OrderCreated", created.TemplateKey);
        Assert.Equal("تحديث الطلب", updated.Title);
        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task TryCreateOrderCreatedMessageAsync_IsIdempotent()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var first = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        var second = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Single(repo.Messages);
        Assert.Single(repo.Notifications);
        Assert.Equal(repo.Messages[0].CustomerMessageId, repo.Notifications[0].CustomerMessageId);
    }

    [Fact]
    public async Task OutboundMessageDispatcher_SendsEligibleMessage_AndPersistsProviderMessageId()
    {
        var repo = new FakeMessageRepository();
        var channel = new FakeOutboundChannel("InApp", true);
        var dispatcher = new OutboundMessageDispatcher(repo, [channel], new LoggerFactory().CreateLogger<OutboundMessageDispatcher>(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OutboundMessaging:MaxAttempts"] = "5",
            ["OutboundMessaging:StaleProcessingMinutes"] = "30",
        }).Build());

        var messageId = await repo.CreateCustomerMessageAsync(7, 99, "OrderCreated", "OrderCreated", "InApp", "Title", "Body", "770000000", "ORD-99", "Order", "idempotency-1", CancellationToken.None);
        Assert.NotNull(messageId);

        var result = await dispatcher.DispatchNextAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Sent);
        Assert.Equal(messageId, result.CustomerMessageId);
        Assert.Equal("provider-123", repo.Messages.Single(x => x.CustomerMessageId == messageId.Value).ProviderMessageId);
        Assert.Equal("Sent", repo.Messages.Single(x => x.CustomerMessageId == messageId.Value).DeliveryStatus);
    }

    [Fact]
    public async Task OutboundMessageDispatcher_RetryUsesSameMessage_WhenTransientFailureOccurs()
    {
        var repo = new FakeMessageRepository();
        var channel = new FakeOutboundChannel("InApp", false, forcedTransientFailure: true);
        var dispatcher = new OutboundMessageDispatcher(repo, [channel], new LoggerFactory().CreateLogger<OutboundMessageDispatcher>(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OutboundMessaging:MaxAttempts"] = "3",
            ["OutboundMessaging:StaleProcessingMinutes"] = "30",
        }).Build());

        var messageId = await repo.CreateCustomerMessageAsync(7, 99, "OrderCreated", "OrderCreated", "InApp", "Title", "Body", "770000000", "ORD-99", "Order", "idempotency-2", CancellationToken.None);
        Assert.NotNull(messageId);

        var first = await dispatcher.DispatchNextAsync(CancellationToken.None);
        var second = await dispatcher.DispatchNextAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.False(first!.Sent);
        Assert.Equal(messageId, first.CustomerMessageId);
        Assert.Equal(2, repo.Messages.Single(x => x.CustomerMessageId == messageId.Value).AttemptCount);
        Assert.True(second is null || second.CustomerMessageId == messageId.Value);
    }

    [Fact]
    public async Task MessageRepository_MarkSentAsync_DoesNotSetDeliveredAtUtc_AndTracksProviderMessageId()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Outbound messaging validation must target LUMAR_ERP_ES_VALIDATION.");

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..12];
        var providerMessageId = "provider-live-" + uniqueSuffix;
        var messageId = await SeedQueuedMessageAsync(connectionString, "ValidationDeliveryState", uniqueSuffix, deliveryStatus: "Pending", providerMessageId: providerMessageId);
        try
        {
            var repo = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

            var sentAtUtc = new DateTime(2026, 10, 19, 12, 15, 0, DateTimeKind.Utc);
            await repo.MarkSentAsync(messageId, providerMessageId, sentAtUtc, CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
                SELECT DeliveryStatus, ProviderMessageId, SentAtUtc, DeliveredAtUtc, ProcessingAtUtc, FailureReason
                FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", connection);
            command.Parameters.AddWithValue("@id", messageId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Sent", reader.GetString(0));
            Assert.Equal("provider-live-" + uniqueSuffix, reader.GetString(1));
            Assert.Equal(sentAtUtc, reader.GetDateTime(2));
            Assert.Equal(DBNull.Value, reader[3]);
            Assert.Equal(DBNull.Value, reader[4]);
            Assert.Equal(DBNull.Value, reader[5]);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", cleanup);
            command.Parameters.AddWithValue("@id", messageId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_UpdateDeliveryStatusByProviderIdAsync_RespectsDeliveryLifecycle_WithoutDowngrades()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Outbound messaging validation must target LUMAR_ERP_ES_VALIDATION.");

        var repo = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

        var pendingProviderMessageId = "provider-pending-" + Guid.NewGuid().ToString("N")[..12];
        var sentProviderMessageId = "provider-sent-" + Guid.NewGuid().ToString("N")[..12];
        var deliveredProviderMessageId = "provider-delivered-" + Guid.NewGuid().ToString("N")[..12];
        var readProviderMessageId = "provider-read-" + Guid.NewGuid().ToString("N")[..12];

        var pendingId = await SeedQueuedMessageAsync(connectionString, "ValidationDeliveryFlow", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Pending", failureReason: null, providerMessageId: pendingProviderMessageId);
        var sentId = await SeedQueuedMessageAsync(connectionString, "ValidationDeliveryFlow", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Sent", failureReason: null, providerMessageId: sentProviderMessageId);
        var deliveredId = await SeedQueuedMessageAsync(connectionString, "ValidationDeliveryFlow", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Delivered", failureReason: null, providerMessageId: deliveredProviderMessageId);
        var readId = await SeedQueuedMessageAsync(connectionString, "ValidationDeliveryFlow", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Read", failureReason: null, providerMessageId: readProviderMessageId);

        try
        {
            var sentAt = new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);
            await repo.UpdateDeliveryStatusByProviderIdAsync(pendingProviderMessageId, "Sent", sentAt, null, CancellationToken.None);
            await repo.UpdateDeliveryStatusByProviderIdAsync(sentProviderMessageId, "Delivered", sentAt.AddMinutes(1), null, CancellationToken.None);
            await repo.UpdateDeliveryStatusByProviderIdAsync(deliveredProviderMessageId, "Read", sentAt.AddMinutes(2), null, CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
                SELECT CustomerMessageID, DeliveryStatus, SentAtUtc, DeliveredAtUtc, ReadAtUtc
                FROM dbo.CustomerMessages
                WHERE CustomerMessageID IN (@pendingId, @sentId, @deliveredId, @readId)
                ORDER BY CustomerMessageID;", connection);
            command.Parameters.AddWithValue("@pendingId", pendingId);
            command.Parameters.AddWithValue("@sentId", sentId);
            command.Parameters.AddWithValue("@deliveredId", deliveredId);
            command.Parameters.AddWithValue("@readId", readId);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("Sent", reader.GetString(1));
            Assert.NotEqual(DBNull.Value, reader[2]);
            Assert.Equal(DBNull.Value, reader[3]);
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Delivered", reader.GetString(1));
            Assert.NotEqual(DBNull.Value, reader[3]);
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Read", reader.GetString(1));
            Assert.NotEqual(DBNull.Value, reader[4]);
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Read", reader.GetString(1));
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID IN (@pendingId, @sentId, @deliveredId, @readId);", cleanup);
            command.Parameters.AddWithValue("@pendingId", pendingId);
            command.Parameters.AddWithValue("@sentId", sentId);
            command.Parameters.AddWithValue("@deliveredId", deliveredId);
            command.Parameters.AddWithValue("@readId", readId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_UpdateDeliveryStatusByProviderIdAsync_FailedWebhook_SetsRetryabilityToZero_AndBlocksReclaim()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Webhook failed validation must target LUMAR_ERP_ES_VALIDATION.");

        var repo = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

        var providerMessageId = "provider-webhook-failed-" + Guid.NewGuid().ToString("N")[..12];
        var messageId = await SeedQueuedMessageAsync(connectionString, "ValidationWebhookFailed", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Sent", attemptCount: 1, failureReason: null, providerMessageId: providerMessageId, isRetryable: true);

        try
        {
            await repo.UpdateDeliveryStatusByProviderIdAsync(providerMessageId, "Failed", DateTime.UtcNow, "Permanent failure from provider callback", CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"SELECT DeliveryStatus, IsRetryable, ProcessingAtUtc, FailureReason FROM dbo.CustomerMessages WHERE CustomerMessageID = @messageId;", connection);
            command.Parameters.AddWithValue("@messageId", messageId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Failed", reader.GetString(0));
            Assert.False(reader.GetBoolean(1));
            Assert.Equal(DBNull.Value, reader[2]);
            Assert.Equal("Permanent failure from provider callback", reader.GetString(3));

            var reclaimed = await repo.ClaimNextAsync("ValidationWebhookFailed", 3, TimeSpan.FromMinutes(30), CancellationToken.None);
            Assert.Null(reclaimed);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID = @messageId;", cleanup);
            command.Parameters.AddWithValue("@messageId", messageId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_UpdateDeliveryStatusByProviderIdAsync_RejectsLateDowngrades_AndPreservesHighStatus()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Outbound messaging validation must target LUMAR_ERP_ES_VALIDATION.");

        var repo = new MessageRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

        var deliveredProviderMessageId = "provider-delivered-late-" + Guid.NewGuid().ToString("N")[..12];
        var readProviderMessageId = "provider-read-late-" + Guid.NewGuid().ToString("N")[..12];
        var failedProviderMessageId = "provider-failed-late-" + Guid.NewGuid().ToString("N")[..12];

        var deliveredId = await SeedQueuedMessageAsync(connectionString, "ValidationLateDowngrade", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Delivered", failureReason: null, providerMessageId: deliveredProviderMessageId);
        var readId = await SeedQueuedMessageAsync(connectionString, "ValidationLateDowngrade", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Read", failureReason: null, providerMessageId: readProviderMessageId);
        var failedId = await SeedQueuedMessageAsync(connectionString, "ValidationLateDowngrade", Guid.NewGuid().ToString("N")[..10], deliveryStatus: "Failed", failureReason: null, providerMessageId: failedProviderMessageId);

        try
        {
            await repo.UpdateDeliveryStatusByProviderIdAsync(deliveredProviderMessageId, "Sent", DateTime.UtcNow, null, CancellationToken.None);
            await repo.UpdateDeliveryStatusByProviderIdAsync(readProviderMessageId, "Sent", DateTime.UtcNow.AddMinutes(1), null, CancellationToken.None);
            await repo.UpdateDeliveryStatusByProviderIdAsync(readProviderMessageId, "Delivered", DateTime.UtcNow.AddMinutes(2), null, CancellationToken.None);
            await repo.UpdateDeliveryStatusByProviderIdAsync(failedProviderMessageId, "Failed", DateTime.UtcNow.AddMinutes(3), "Bad phone number", CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
                SELECT CustomerMessageID, DeliveryStatus, DeliveredAtUtc, ReadAtUtc, FailureReason
                FROM dbo.CustomerMessages
                WHERE CustomerMessageID IN (@deliveredId, @readId, @failedId)
                ORDER BY CustomerMessageID;", connection);
            command.Parameters.AddWithValue("@deliveredId", deliveredId);
            command.Parameters.AddWithValue("@readId", readId);
            command.Parameters.AddWithValue("@failedId", failedId);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("Delivered", reader.GetString(1));
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Read", reader.GetString(1));
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Failed", reader.GetString(1));
            Assert.Equal("Bad phone number", reader.GetString(4));
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID IN (@deliveredId, @readId, @failedId);", cleanup);
            command.Parameters.AddWithValue("@deliveredId", deliveredId);
            command.Parameters.AddWithValue("@readId", readId);
            command.Parameters.AddWithValue("@failedId", failedId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_ClaimNextAsync_ConcurrentClaims_OnlyOneConnectionWins_AndLifecyclePersists()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Outbound messaging validation must target LUMAR_ERP_ES_VALIDATION.");

        var channel = "ValidationInApp-" + Guid.NewGuid().ToString("N")[..16];
        var messageId = await SeedQueuedMessageAsync(connectionString, channel, "ValidationConcurrentClaim-" + Guid.NewGuid().ToString("N")[..12]);
        try
        {
            var repo1 = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));
            var repo2 = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

            await using (var print = new SqlConnection(connectionString))
            {
                await print.OpenAsync();
                await using var list = new SqlCommand(@"
                    SELECT CustomerMessageID, EventType, TemplateKey, DeliveryStatus, AttemptCount, ProcessingAtUtc, IdempotencyKey
                    FROM dbo.CustomerMessages
                    WHERE Channel = @channel
                    ORDER BY CreatedAtUtc ASC, CustomerMessageID ASC;", print);
                list.Parameters.AddWithValue("@channel", channel);
                await using var reader = await list.ExecuteReaderAsync();
                Console.WriteLine($"[ClaimDebug] channel={channel} seedMessageId={messageId}");
                while (await reader.ReadAsync())
                {
                    Console.WriteLine($"[ClaimDebug] eligible CustomerMessageID={reader.GetInt32(0)}, EventType={reader.GetString(1)}, TemplateKey={reader.GetString(2)}, DeliveryStatus={reader.GetString(3)}, AttemptCount={reader.GetInt32(4)}, ProcessingAtUtc={(reader.IsDBNull(5) ? "NULL" : reader.GetDateTime(5).ToString("O"))}, IdempotencyKey={reader.GetString(6)}");
                }
            }

            var firstClaimTask = repo1.ClaimNextAsync(channel, 5, TimeSpan.FromMinutes(30), CancellationToken.None);
            var secondClaimTask = repo2.ClaimNextAsync(channel, 5, TimeSpan.FromMinutes(30), CancellationToken.None);
            var claimed = await Task.WhenAll(firstClaimTask, secondClaimTask);

            Console.WriteLine($"[ClaimDebug] claimedIds={string.Join(",", claimed.Select(x => x is null ? "null" : x.CustomerMessageId.ToString()))}");
            Assert.Equal(1, claimed.Count(x => x is not null));
            var winner = claimed.Single(x => x is not null)!;
            Assert.Equal(messageId, winner.CustomerMessageId);

            await using (var verify = new SqlConnection(connectionString))
            {
                await verify.OpenAsync();
                await using var command = new SqlCommand(@"
                    SELECT DeliveryStatus, AttemptCount, ProcessingAtUtc, FailureReason, ProviderMessageId
                    FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", verify);
                command.Parameters.AddWithValue("@id", messageId);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Processing", reader.GetString(0));
                Assert.Equal(1, reader.GetInt32(1));
                Assert.NotEqual(DBNull.Value, reader[2]);
                Assert.Equal(DBNull.Value, reader[3]);
                Assert.Equal(DBNull.Value, reader[4]);
            }

            var providerMessageId = "provider-live-" + Guid.NewGuid().ToString("N");
            await repo1.MarkSentAsync(messageId, providerMessageId, DateTime.UtcNow, CancellationToken.None);

            await using (var verify = new SqlConnection(connectionString))
            {
                await verify.OpenAsync();
                await using var command = new SqlCommand(@"
                    SELECT DeliveryStatus, ProviderMessageId, ProcessingAtUtc, SentAtUtc
                    FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", verify);
                command.Parameters.AddWithValue("@id", messageId);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Sent", reader.GetString(0));
                Assert.Equal(providerMessageId, reader.GetString(1));
                Assert.Equal(DBNull.Value, reader[2]);
                Assert.NotEqual(DBNull.Value, reader[3]);
            }
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", cleanup);
            command.Parameters.AddWithValue("@id", messageId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_ClaimNextAsync_ConcurrentClaims_UsesDistinctMessages_WhenTwoMessagesShareUniqueChannel()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Outbound messaging validation must target LUMAR_ERP_ES_VALIDATION.");

        var channel = "ValidationInApp-" + Guid.NewGuid().ToString("N")[..16];
        var firstMessageId = await SeedQueuedMessageAsync(connectionString, channel, "ValidationTwoWinnersA-" + Guid.NewGuid().ToString("N")[..12]);
        var secondMessageId = await SeedQueuedMessageAsync(connectionString, channel, "ValidationTwoWinnersB-" + Guid.NewGuid().ToString("N")[..12]);

        try
        {
            var repo1 = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));
            var repo2 = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

            var firstClaimTask = repo1.ClaimNextAsync(channel, 5, TimeSpan.FromMinutes(30), CancellationToken.None);
            var secondClaimTask = repo2.ClaimNextAsync(channel, 5, TimeSpan.FromMinutes(30), CancellationToken.None);
            var claimed = await Task.WhenAll(firstClaimTask, secondClaimTask);

            Assert.Equal(2, claimed.Count(x => x is not null));
            Assert.Equal(2, claimed.Select(x => x!.CustomerMessageId).Distinct().Count());
            Assert.Contains(claimed, x => x!.CustomerMessageId == firstMessageId);
            Assert.Contains(claimed, x => x!.CustomerMessageId == secondMessageId);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var delete = new SqlCommand(@"DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID IN (@id1, @id2);", cleanup);
            delete.Parameters.AddWithValue("@id1", firstMessageId);
            delete.Parameters.AddWithValue("@id2", secondMessageId);
            await delete.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageRepository_ClaimNextAsync_StaleProcessingMessagesAreEligibleAfterWindow()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Outbound messaging validation must target LUMAR_ERP_ES_VALIDATION.");

        var channel = "ValidationInApp-" + Guid.NewGuid().ToString("N")[..16];
        var messageId = await SeedQueuedMessageAsync(
            connectionString,
            channel,
            "ValidationStaleProcessing-" + Guid.NewGuid().ToString("N")[..12],
            deliveryStatus: "Processing",
            attemptCount: 1,
            processingAtUtc: DateTime.UtcNow.AddMinutes(-31),
            failureReason: "Transient failure");

        try
        {
            var repo = new MessageRepository(
                new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
                new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));

            var claimed = await repo.ClaimNextAsync(channel, 5, TimeSpan.FromMinutes(30), CancellationToken.None);

            Assert.NotNull(claimed);
            Assert.Equal(messageId, claimed!.CustomerMessageId);
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new SqlCommand("DELETE FROM dbo.CustomerMessages WHERE CustomerMessageID = @id;", cleanup);
            command.Parameters.AddWithValue("@id", messageId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MessageWriteService_UsesExistingOrderReadyAndDeliveredTemplates_ForLifecycleEvents()
    {
        var repo = new StrictTemplateLookupRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var ready = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderReady",
            TemplateKey: "OrderReady",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
            }), CancellationToken.None);

        var delivered = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderDelivered",
            TemplateKey: "OrderDelivered",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
            }), CancellationToken.None);

        Assert.True(ready.Created, "OrderReady should resolve to the existing active order-ready template key.");
        Assert.True(delivered.Created, "OrderDelivered should resolve to the existing active order-delivered template key.");
        Assert.Equal("order-ready", ready.TemplateKey);
        Assert.Equal("order-delivered", delivered.TemplateKey);
    }

    [Fact]
    public async Task TryCreateAsync_UsesUniqueRewardGrantIdentity_ForReferralRewards()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var rewardA = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "770000000",
            ReferenceId: "RewardGranted:9001",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "مستفيد أ",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
                ["PointsEarned"] = 10m,
            }), CancellationToken.None);

        var rewardB = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "770000000",
            ReferenceId: "RewardGranted:9002",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "مستفيد أ",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
                ["PointsEarned"] = 12m,
            }), CancellationToken.None);

        var repeatedA = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "770000000",
            ReferenceId: "RewardGranted:9001",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "مستفيد أ",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
                ["PointsEarned"] = 10m,
            }), CancellationToken.None);

        Assert.True(rewardA.Created);
        Assert.True(rewardB.Created);
        Assert.False(repeatedA.Created);
        Assert.Equal(2, repo.Messages.Count(x => x.EventType == "ReferralPointsEarned"));
        Assert.Equal(2, repo.Notifications.Count(x => x.EventType == "ReferralPointsEarned"));
    }

    [Fact]
    public async Task TryCreateAsync_WhenTemplateIsMissing_ReturnsFalse()
    {
        var repo = new FakeMessageRepository { TemplateExists = false };
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "MissingTemplate",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Empty(repo.Messages);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task OrderService_CreateAsync_UsesOfficialLoyaltyAndSystemSettingValuesInOrderCreatedContext()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            301,
            "ORD-301",
            9,
            "أحمد",
            "770000301",
            DateTime.UtcNow,
            null,
            220m,
            50m,
            170m,
            0m,
            "Normal",
            "Created",
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var loyaltyResult = new OrderLoyaltyProcessingResult(
            301,
            9,
            30m,
            15m,
            15m,
            [],
            new LoyaltyAccountDto(5, 9, 250m, 1000m, 50m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Active"),
            [new LoyaltyAccountDto(5, 9, 250m, 1000m, 50m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Active")],
            [],
            [],
            []);

        var service = new OrderService(
            new FakeOrderRepository(order, order),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(loyaltyResult),
            messageService,
            customerRepository: new FakeCustomerRepository(
                new CustomerDetailsDto(9, "C-301", "أحمد", "770000301", null, null, null, null, null, true, null, null, null, null)),
            settingsRepository: new FakeSettingsRepository(new MessageSettingsDto(true, true, false, true, true, true))
            {
                StoreName = "LUMAR",
                CurrencyName = "الريال اليمني"
            });

        var createRequest = new CreateOrderDto
        {
            CustomerId = 9,
            TotalAmount = 220m,
            DiscountAmount = 0m,
            AdvancePayment = 50m,
            UrgencyStatus = "Normal",
            SaleCategory = "TailoringOrder",
            Items = Array.Empty<CreateOrderItemDto>()
        };

        var result = await service.CreateAsync(createRequest, CancellationToken.None);

        Assert.NotNull(result);
        var request = Assert.Single(messageService.Requests);
        Assert.Equal("OrderCreated", request.EventType);
        Assert.Equal(15m, request.Context!["PointsEarned"]);
        Assert.Equal(250m, request.Context["PointsBalance"]);
        Assert.Equal("الريال اليمني", request.Context["CurrencyName"]);
        Assert.Equal("LUMAR", request.Context["StoreName"]);
    }

    [Fact]
    public void MessageTemplateRenderer_RendersLifecycleTokensWithoutUnresolvedPlaceholders()
    {
        var renderer = new MessageTemplateRenderer();
        var orderCreated = "مرحباً {{CustomerName}}، طلبك {{OrderNumber}} بقيمة {{TotalAmount}} {{CurrencyName}}، المدفوع {{PaidAmount}}، المتبقي {{RemainingAmount}}، تاريخ التسليم {{DeliveryDate}}، نقاطك الآن {{PointsBalance}}، نقاطك المكتسبة {{PointsEarned}}، الكود {{CustomerCode}}، المتجر {{StoreName}}، التفاصيل {{OrderDetails}}.";
        var orderReady = "عزيزي {{CustomerName}}، طلبك رقم {{OrderNumber}} أصبح جاهزاً للاستلام، المتبقي {{RemainingAmount}}، المتجر {{StoreName}}.";
        var orderDelivered = "شكرًا {{CustomerName}}، تم تسليم الطلب {{OrderNumber}} من متجر {{StoreName}}. المجموع {{TotalAmount}} {{CurrencyName}}.";

        var values = new Dictionary<string, object?>
        {
            ["CustomerName"] = "أحمد",
            ["OrderNumber"] = "ORD-301",
            ["TotalAmount"] = 220m,
            ["CurrencyName"] = "الريال اليمني",
            ["PaidAmount"] = 50m,
            ["RemainingAmount"] = 170m,
            ["DeliveryDate"] = new DateTime(2026, 10, 20),
            ["PointsBalance"] = 250m,
            ["PointsEarned"] = 15m,
            ["CustomerCode"] = "C-301",
            ["StoreName"] = "LUMAR",
            ["OrderDetails"] = "قميص + بنطال",
        };

        var created = renderer.Render(orderCreated, values);
        var ready = renderer.Render(orderReady, values);
        var delivered = renderer.Render(orderDelivered, values);

        Assert.DoesNotContain("{{", created, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{{", ready, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{{", delivered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryCreateAsync_WhenCustomerHasNoReferrer_CreatesOnlyCustomerMessage()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "order-created-customer-smart",
            PhoneNumber: "770000001",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderDetails"] = "قميص + بنطلون",
                ["TotalAmount"] = 149.99m,
                ["PaidAmount"] = 100m,
                ["RemainingAmount"] = 49.99m,
                ["DeliveryDate"] = new DateTime(2026, 1, 10),
                ["PointsEarned"] = 15m,
                ["PointsBalance"] = 250m,
                ["CustomerCode"] = "C-001",
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal(1, repo.Messages.Count(x => x.EventType == "OrderCreated"));
        Assert.Equal(1, repo.Notifications.Count(x => x.EventType == "OrderCreated"));
        Assert.DoesNotContain(repo.Messages, x => x.TemplateKey.Contains("referrer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryCreateAsync_WhenReferrerPhoneDiffers_CreatesSeparateCustomerAndReferrerMessages()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "order-created-customer-smart",
            PhoneNumber: "770000001",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CustomerCode"] = "C-001",
                ["OrderNumber"] = "ORD-42",
                ["OrderDetails"] = "قميص + بنطلون",
                ["TotalAmount"] = 149.99m,
                ["PaidAmount"] = 100m,
                ["RemainingAmount"] = 49.99m,
                ["DeliveryDate"] = new DateTime(2026, 1, 10),
                ["PointsEarned"] = 15m,
                ["PointsBalance"] = 250m,
                ["ReferrerCustomerId"] = 52,
                ["ReferrerName"] = "محمود",
                ["ReferrerCode"] = "R-900",
                ["ReferrerPhoneNumber"] = "779999999",
                ["CustomerPhoneNumber"] = "770000001",
                ["ReferralIntroduction"] = "العميل الذي عرّفك على مؤسسة لومــــــVIPـــــار هو سالم.",
                ["ReferralPointsEarned"] = 12m,
                ["ReferrerPointsBalance"] = 620m,
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal(2, repo.Messages.Count(x => x.EventType == "OrderCreated"));
        Assert.Equal(2, repo.Notifications.Count(x => x.EventType == "OrderCreated"));
        Assert.Contains(repo.Messages, x => x.TemplateKey == "order-created-customer-smart");
        Assert.Contains(repo.Messages, x => x.TemplateKey == "referrer-points-earned-smart");
    }

    [Fact]
    public async Task TryCreateAsync_WhenNotificationCreationFails_DoesNotLeavePartialState()
    {
        var repo = new FakeMessageRepository { FailNotificationCreation = true };
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None));

        Assert.Empty(repo.Messages);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_WhenOrderIsNotOfficiallyReady_DoesNotSendOrderReadyMessage()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            99,
            "ORD-99",
            7,
            "سالم",
            "770000099",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "Confirmed",
            "Pending",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var productionRepository = new FakeProductionRepository(
            new PieceDto(12, 1, "TRK-99", "Cutting", 1, DateTime.UtcNow, "YALAQ", 99, "ORD-99", 9),
            new ProductionTrackingAdvanceResultDto(12, "TRK-99", "Cutting", "Ironing", "Ironing", "تم التقدم إلى Ironing", true));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(12, "TRK-99", "YALAQ", "Ironing", "SCN-1", "EMP-1", "REF-1", 9), CancellationToken.None);

        Assert.Empty(messageService.Requests);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_WhenOrderBecomesReadyForDelivery_SendsOrderReadyExactlyOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            100,
            "ORD-100",
            7,
            "سالم",
            "770000100",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "ReadyForDelivery",
            "Ready",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var productionRepository = new FakeProductionRepository(
            new PieceDto(13, 1, "TRK-100", "Ready", 1, DateTime.UtcNow, "YALAQ", 100, "ORD-100", 9),
            new ProductionTrackingAdvanceResultDto(13, "TRK-100", "Ready", "ReadyForDelivery", null, "Order is ready for delivery and can be packed.", true, false, true));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(13, "TRK-100", "YALAQ", "ReadyForDelivery", "SCN-2", "EMP-2", "REF-2", 9), CancellationToken.None);

        Assert.Single(messageService.Requests);
        Assert.Equal("OrderReady", messageService.Requests[0].EventType);
        Assert.Equal("ORD-100", messageService.Requests[0].ReferenceId);
        Assert.Equal(7, messageService.Requests[0].CustomerId);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_WhenOrderBecomesReadyForDelivery_UsesOfficialOrderValuesInContext()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            100,
            "ORD-100",
            7,
            "سالم",
            "770000100",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "ReadyForDelivery",
            "Ready",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var productionRepository = new FakeProductionRepository(
            new PieceDto(13, 1, "TRK-100", "Ready", 1, DateTime.UtcNow, "YALAQ", 100, "ORD-100", 9),
            new ProductionTrackingAdvanceResultDto(13, "TRK-100", "Ready", "ReadyForDelivery", null, "Order is ready for delivery and can be packed.", true, false, true));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(13, "TRK-100", "YALAQ", "ReadyForDelivery", "SCN-2", "EMP-2", "REF-2", 9), CancellationToken.None);

        var request = Assert.Single(messageService.Requests);
        Assert.Equal(150m, Assert.IsType<decimal>(request.Context!["Amount"]));
        Assert.Equal("770000100", request.PhoneNumber);
        Assert.Equal("ORD-100", request.Context["OrderNumber"]);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_WhenMessageMentionsReadyForDeliveryButTransitionDidNotOccur_DoesNotSendOrderReady()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            101,
            "ORD-101",
            7,
            "محمود",
            "770000101",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "Confirmed",
            "Pending",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var productionRepository = new FakeProductionRepository(
            new PieceDto(14, 1, "TRK-101", "Cutting", 1, DateTime.UtcNow, "YALAQ", 101, "ORD-101", 9),
            new ProductionTrackingAdvanceResultDto(14, "TRK-101", "Cutting", "Ironing", "Ironing", "Order is ready for delivery and can be packed.", true, false, false));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(14, "TRK-101", "YALAQ", "Ironing", "SCN-3", "EMP-3", "REF-3", 9), CancellationToken.None);

        Assert.Empty(messageService.Requests);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_ReplayDoesNotResendOrderReady_WhenTransitionOnlyHappensOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            102,
            "ORD-102",
            7,
            "باسم",
            "770000102",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "ReadyForDelivery",
            "Ready",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var piece = new PieceDto(15, 1, "TRK-102", "Ready", 1, DateTime.UtcNow, "YALAQ", 102, "ORD-102", 9);
        var results = new Queue<ProductionTrackingAdvanceResultDto>([
            new ProductionTrackingAdvanceResultDto(15, "TRK-102", "Ready", "ReadyForDelivery", null, "Order is ready for delivery and can be packed.", true, false, true),
            new ProductionTrackingAdvanceResultDto(15, "TRK-102", "ReadyForDelivery", "ReadyForDelivery", null, "Order remains ready for delivery.", true, false, false)
        ]);

        var productionRepository = new SequenceProductionRepository(piece, results);
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        var first = await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(15, "TRK-102", "YALAQ", "ReadyForDelivery", "SCN-4", "EMP-4", "REF-4", 9), CancellationToken.None);
        var second = await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(15, "TRK-102", "YALAQ", "ReadyForDelivery", "SCN-4", "EMP-4", "REF-4", 9), CancellationToken.None);

        Assert.NotNull(first);
        Assert.True(first!.BecameReadyForDelivery);
        Assert.Equal(1, messageService.Requests.Count(x => x.EventType == "OrderReady"));

        Assert.NotNull(second);
        Assert.False(second!.BecameReadyForDelivery);
        Assert.Equal(1, messageService.Requests.Count(x => x.EventType == "OrderReady"));
    }

    [Fact]
    public async Task OrderService_DeliverAsync_WhenOrderDeliveryIsNotOfficiallyCompleted_DoesNotSendOrderDeliveredMessage()
    {
        var messageService = new RecordingMessageWriteService();
        var service = new OrderService(
            new FakeOrderRepository(new OrderDetailsDto(
                200,
                "ORD-200",
                8,
                "أحمد",
                "770000200",
                DateTime.UtcNow,
                null,
                200m,
                0m,
                0m,
                200m,
                "Normal",
                "Confirmed",
                null,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                "TailoringOrder",
                false,
                null,
                false,
                null), deliverResult: null),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await service.DeliverAsync(200, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(messageService.Requests);
    }

    [Fact]
    public async Task OrderService_DeliverAsync_WhenOrderIsOfficiallyDelivered_UsesOfficialOrderValuesInContext()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            201,
            "ORD-201",
            8,
            "أحمد",
            "770000201",
            DateTime.UtcNow,
            null,
            200m,
            0m,
            200m,
            0m,
            "Normal",
            "ReadyForDelivery",
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var service = new OrderService(
            new FakeOrderRepository(order, order),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService,
            customerRepository: new FakeCustomerRepository(
                new CustomerDetailsDto(8, "C-201", "أحمد", "770000201", null, null, null, null, null, true, null, null, null, null)),
            settingsRepository: new FakeSettingsRepository(new MessageSettingsDto(true, true, false, true, true, true))
            {
                StoreName = "LUMAR",
                CurrencyName = "الريال اليمني"
            });

        var result = await service.DeliverAsync(201, CancellationToken.None);

        Assert.NotNull(result);
        var request = Assert.Single(messageService.Requests);
        Assert.Equal("OrderDelivered", request.EventType);
        Assert.Equal(200m, Assert.IsType<decimal>(request.Context!["Amount"]));
        Assert.Equal("770000201", request.PhoneNumber);
        Assert.Equal("ORD-201", request.Context["OrderNumber"]);
        Assert.Equal("الريال اليمني", request.Context["CurrencyName"]);
        Assert.Equal("LUMAR", request.Context["StoreName"]);
        Assert.Equal("C-201", request.Context["CustomerCode"]);
    }

    [Fact]
    public async Task OrderService_DeliverAsync_WhenOrderIsOfficiallyDelivered_SendsOrderDeliveredOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            201,
            "ORD-201",
            8,
            "أحمد",
            "770000201",
            DateTime.UtcNow,
            null,
            200m,
            0m,
            200m,
            0m,
            "Normal",
            "ReadyForDelivery",
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var service = new OrderService(
            new FakeOrderRepository(order, order),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await service.DeliverAsync(201, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(messageService.Requests);
        Assert.Equal("OrderDelivered", messageService.Requests[0].EventType);
        Assert.Equal("ORD-201", messageService.Requests[0].ReferenceId);
    }

    [Fact]
    public async Task OrderService_CancelOrderAsync_WhenOrderCancellationSucceeds_SendsOrderCancelledOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            202,
            "ORD-202",
            8,
            "أحمد",
            "770000202",
            DateTime.UtcNow,
            null,
            200m,
            0m,
            0m,
            200m,
            "Normal",
            "Confirmed",
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var service = new OrderService(
            new FakeOrderRepository(order, cancelledOrder: order),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await service.CancelOrderAsync(202, "Customer request", "Admin", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(messageService.Requests);
        Assert.Equal("OrderCancelled", messageService.Requests[0].EventType);
        Assert.Equal("ORD-202", messageService.Requests[0].ReferenceId);
    }

    [Fact]
    public async Task CancelledPieceDispositionService_SaveDecisionAsync_WhenPieceIsCancelled_SendsPieceCancelledOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            42,
            "ORD-42",
            7,
            "سالم",
            "770000042",
            DateTime.UtcNow,
            null,
            300m,
            0m,
            0m,
            300m,
            "Normal",
            "Confirmed",
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);
        var piece = new PieceDto(15, 1, "TRK-042", "Cancelled", 1, DateTime.UtcNow, "YALAQ", 42, "ORD-42", 9);

        var service = new CancelledPieceDispositionService(
            new FakeCancelledPieceDispositionRepository(),
            new FakeProductionRepository(piece),
            new FakeOrderRepository(order),
            messageService);

        var result = await service.SaveDecisionAsync(15, "StopAndHold", "Cancelled by customer", "admin", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(messageService.Requests);
        Assert.Equal("PieceCancelled", messageService.Requests[0].EventType);
        Assert.Equal("ORD-42:1", messageService.Requests[0].ReferenceId);
    }

    [Fact]
    public async Task TryCreateAsync_UsesIdempotencyForReadyAndDeliveredBusinessEvents()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var readyOne = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderReady",
            "OrderReady",
            "770000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        var readyTwo = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderReady",
            "OrderReady",
            "770000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        var deliveredOne = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderDelivered",
            "OrderDelivered",
            "770000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        var deliveredTwo = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderDelivered",
            "OrderDelivered",
            "770000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        Assert.True(readyOne.Created);
        Assert.False(readyTwo.Created);
        Assert.True(deliveredOne.Created);
        Assert.False(deliveredTwo.Created);
        Assert.Equal(2, repo.Messages.Count);
        Assert.Equal(2, repo.Notifications.Count);
    }

    [Fact]
    public async Task MessageWriteService_WhenWhatsAppIsEnabled_UsesWhatsAppTransportChannelEvenWhenTemplateIsInApp()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo);

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "967775106545",
            ReferenceId: "manual-campaign-001",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.CustomerNotificationId);
        Assert.NotNull(result.CustomerMessageId);
        Assert.Equal(1, repo.Notifications.Count);
        Assert.Equal(1, repo.Messages.Count);
        Assert.Equal("WhatsApp", repo.Messages[0].Channel);
        Assert.Equal("general-campaign-smart", repo.Messages[0].TemplateKey);
        Assert.Equal(result.Title, repo.Notifications[0].Title);
        Assert.Equal(result.Body, repo.Notifications[0].Body);
    }

    [Fact]
    public async Task MessageWriteService_WhenPhoneIsLocalFormat_UsesNormalizedWhatsAppNumber()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo, new YemenPhoneNumberNormalizer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "775106545",
            ReferenceId: "manual-campaign-local-phone",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.CustomerMessageId);
        Assert.Equal("WhatsApp", repo.Messages[0].Channel);
        Assert.Equal("967775106545", repo.Messages[0].PhoneNumber);
    }

    [Fact]
    public async Task MessageWriteService_WhenPhoneUsesPlusPrefix_UsesNormalizedWhatsAppNumber()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo, new YemenPhoneNumberNormalizer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "+967775106545",
            ReferenceId: "manual-campaign-plus-phone",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.CustomerMessageId);
        Assert.Equal("967775106545", repo.Messages[0].PhoneNumber);
    }

    [Fact]
    public async Task MessageWriteService_WhenPhoneUsesDoubleZeroPrefix_UsesNormalizedWhatsAppNumber()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo, new YemenPhoneNumberNormalizer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "00967775106545",
            ReferenceId: "manual-campaign-00967-phone",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.CustomerMessageId);
        Assert.Equal("967775106545", repo.Messages[0].PhoneNumber);
    }

    [Fact]
    public async Task MessageWriteService_WhenPhoneIsMissingOrInvalid_OnlyCreatesNotificationWithoutChannelMessage()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo, new YemenPhoneNumberNormalizer());

        var invalidResult = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "0775106545",
            ReferenceId: "manual-campaign-invalid-phone",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        var emptyResult = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "  ",
            ReferenceId: "manual-campaign-empty-phone",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        Assert.True(invalidResult.Created);
        Assert.NotNull(invalidResult.CustomerNotificationId);
        Assert.Null(invalidResult.CustomerMessageId);
        Assert.True(emptyResult.Created);
        Assert.NotNull(emptyResult.CustomerNotificationId);
        Assert.Null(emptyResult.CustomerMessageId);
        Assert.Equal(2, repo.Notifications.Count);
        Assert.Empty(repo.Messages);
    }

    [Fact]
    public async Task MessageWriteService_WhenWhatsAppIsEnabledAndTemplateIsInApp_UsesWhatsAppTransportWithSameContent()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: true));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo, new YemenPhoneNumberNormalizer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "ManualCampaign",
            TemplateKey: "general-campaign-smart",
            PhoneNumber: "967775106545",
            ReferenceId: "manual-campaign-inapp-template",
            ReferenceType: "ManualCampaign",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["CampaignText"] = "عرض خاص لعملائنا",
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.CustomerNotificationId);
        Assert.NotNull(result.CustomerMessageId);
        Assert.Equal("WhatsApp", repo.Messages[0].Channel);
        Assert.Equal(result.Title, repo.Notifications[0].Title);
        Assert.Equal(result.Body, repo.Notifications[0].Body);
        Assert.Equal(repo.Messages[0].Title, repo.Notifications[0].Title);
        Assert.Equal(repo.Messages[0].Body, repo.Notifications[0].Body);
    }

    [Fact]
    public async Task MessageWriteService_WhenWhatsAppChannelIsDisabled_StillCreatesNotificationWithoutChannelMessage()
    {
        var repo = new FakeMessageRepository();
        repo.TemplateExists = true;
        var settingsRepo = new FakeSettingsRepository(new MessageSettingsDto(
            MessageMasterEnabled: true,
            DirectReferralDefaultOn: true,
            OtherReferralDefaultOff: false,
            FridayGreetingEnabled: true,
            CampaignEnabled: true,
            WhatsAppEnabled: false));
        var service = new MessageWriteService(repo, new MessageTemplateRenderer(), settingsRepo);

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 7,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "770000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.CustomerNotificationId);
        Assert.Equal(1, repo.Notifications.Count);
        Assert.Equal(0, repo.Messages.Count);
        Assert.Equal("order-created-customer-smart", result.TemplateKey);
    }

    [Fact]
    public async Task ReadyMadeSalesService_SendsReadyMadeSaleCompletedMessage()
    {
        var messageService = new RecordingMessageWriteService();
        var readyMadeSalesService = new ReadyMadeSalesService(
            new FakeReadyMadeSalesRepository(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await readyMadeSalesService.CreateAsync(new CreateReadyMadeSaleDto
        {
            CustomerId = 7,
            PaymentType = "Cash",
            PaidAmount = 100m,
            CashAccountId = 1,
            Items =
            [
                new CreateReadyMadeSaleItemDto
                {
                    ReadyMadeInventoryProductId = 88,
                    ProductTypeId = 9,
                    Quantity = 1,
                    UnitPrice = 100m,
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(101, result.OrderId);
        Assert.Single(messageService.Requests);
        Assert.Equal("ReadyMadeSaleCompleted", messageService.Requests[0].EventType);
        Assert.Equal(7, messageService.Requests[0].CustomerId);
    }

    private sealed class RecordingMessageWriteService : IMessageWriteService
    {
        public List<MessageWriteRequest> Requests { get; } = new();

        public Task<MessageWriteResult> TryCreateAsync(MessageWriteRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new MessageWriteResult(true, 1, 1, request.TemplateKey, "x", "y"));
        }
    }

    private sealed class FakeOrderRepository(OrderDetailsDto? currentOrder = null, OrderDetailsDto? deliveredOrder = null, OrderDetailsDto? cancelledOrder = null, bool? deliverResult = null) : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>(Array.Empty<OrderListDto>());
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(currentOrder ?? cancelledOrder);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken)
        {
            var created = new OrderDetailsDto(
                301,
                "ORD-301",
                order.CustomerId,
                "أحمد",
                "770000301",
                order.OrderDate ?? DateTime.UtcNow,
                order.DeliveryDate ?? DateTime.UtcNow,
                order.TotalAmount,
                order.DiscountAmount,
                order.AdvancePayment,
                Math.Max(0m, order.TotalAmount - order.AdvancePayment - order.DiscountAmount),
                "Normal",
                "Created",
                null,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                order.SaleCategory,
                false,
                null,
                false,
                null,
                null);

            return Task.FromResult<OrderDetailsDto?>(created);
        }
        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderItemDto>>(Array.Empty<OrderItemDto>());
        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>(Array.Empty<OrderPieceDto>());
        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>(Array.Empty<OrderFabricDto>());
        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>(Array.Empty<OrderPaymentDto>());
        public Task<IReadOnlyList<OrderTrackingDto>?> GetTrackingAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderTrackingDto>?>(Array.Empty<OrderTrackingDto>());
        public Task<int> CountCreatedOrdersForDayAsync(DateTime utcDayStart, DateTime utcDayEnd, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);
        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(deliverResult is null ? deliveredOrder : deliverResult.Value ? deliveredOrder : null);
        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => Task.FromResult(cancelledOrder ?? currentOrder);
    }

    private sealed class FakeCancelledPieceDispositionRepository : ICancelledPieceDispositionRepository
    {
        public Task<CancelledPieceDispositionDto?> GetByPieceIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<CancelledPieceDispositionDto?>(new CancelledPieceDispositionDto(1, pieceId, "StopAndHold", "Cancelled by customer", "admin", DateTime.UtcNow, "NotTransferred", null, null, DateTime.UtcNow));
        public Task<CancelledPieceDispositionDto> SaveDecisionAsync(int pieceId, string decision, string? reason, string? decidedBy, CancellationToken cancellationToken)
            => Task.FromResult(new CancelledPieceDispositionDto(1, pieceId, decision, reason, decidedBy, DateTime.UtcNow, "NotTransferred", null, null, DateTime.UtcNow));
        public Task<CancelledPieceDispositionDto?> ExecuteDecisionAsync(int pieceId, CancellationToken cancellationToken)
            => Task.FromResult<CancelledPieceDispositionDto?>(new CancelledPieceDispositionDto(1, pieceId, "StopAndHold", "Cancelled by customer", "admin", DateTime.UtcNow, "NotTransferred", null, null, DateTime.UtcNow));
    }

    private sealed class FakeProductionRepository(PieceDto piece, ProductionTrackingAdvanceResultDto result = default!) : IProductionRepository
    {
        public Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PieceDto>>(Array.Empty<PieceDto>());
        public Task<IReadOnlyList<PieceDto>> GetReadyMadePiecesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PieceDto>>(Array.Empty<PieceDto>());
        public Task<PieceDto?> GetPieceByIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<PieceDto?>(piece);
        public Task<PieceDto?> GetReadyMadePieceByIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<PieceDto?>(null);
        public Task<WorkCardDto?> GetWorkCardAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<WorkCardDto?>(null);
        public Task<WorkCardDto?> GetReadyMadeWorkCardAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<WorkCardDto?>(null);
        public Task<IReadOnlyList<TrackingEventDto>> GetPieceTrackingAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackingEventDto>>(Array.Empty<TrackingEventDto>());
        public Task<IReadOnlyList<ProductionStageDto>> GetStagesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionStageDto>>(Array.Empty<ProductionStageDto>());
        public Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FactoryMonitoringDashboardDto> GetFactoryMonitoringAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionOrderDto>>(Array.Empty<ReadyMadeProductionOrderDto>());
        public Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<ReadyMadeProductionOrderDto?>(null);
        public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionOrderItemDto>>(Array.Empty<ReadyMadeProductionOrderItemDto>());
        public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int itemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionPieceDto>>(Array.Empty<ReadyMadeProductionPieceDto>());
        public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int pieceInstanceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackingEventDto>>(Array.Empty<TrackingEventDto>());
        public Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionDeliveryDto>>(Array.Empty<ProductionDeliveryDto>());
        public Task<ProductionTrackingRouteDto?> GetPieceRouteAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetPieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingAdvanceResultDto?>(result);
    }

    private sealed class SequenceProductionRepository(PieceDto piece, Queue<ProductionTrackingAdvanceResultDto> results) : IProductionRepository
    {
        public Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PieceDto>>(Array.Empty<PieceDto>());
        public Task<IReadOnlyList<PieceDto>> GetReadyMadePiecesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PieceDto>>(Array.Empty<PieceDto>());
        public Task<PieceDto?> GetPieceByIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<PieceDto?>(piece);
        public Task<PieceDto?> GetReadyMadePieceByIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<PieceDto?>(null);
        public Task<WorkCardDto?> GetWorkCardAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<WorkCardDto?>(null);
        public Task<WorkCardDto?> GetReadyMadeWorkCardAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<WorkCardDto?>(null);
        public Task<IReadOnlyList<TrackingEventDto>> GetPieceTrackingAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackingEventDto>>(Array.Empty<TrackingEventDto>());
        public Task<IReadOnlyList<ProductionStageDto>> GetStagesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionStageDto>>(Array.Empty<ProductionStageDto>());
        public Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FactoryMonitoringDashboardDto> GetFactoryMonitoringAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionOrderDto>>(Array.Empty<ReadyMadeProductionOrderDto>());
        public Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<ReadyMadeProductionOrderDto?>(null);
        public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionOrderItemDto>>(Array.Empty<ReadyMadeProductionOrderItemDto>());
        public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int itemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionPieceDto>>(Array.Empty<ReadyMadeProductionPieceDto>());
        public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int pieceInstanceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackingEventDto>>(Array.Empty<TrackingEventDto>());
        public Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionDeliveryDto>>(Array.Empty<ProductionDeliveryDto>());
        public Task<ProductionTrackingRouteDto?> GetPieceRouteAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetPieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult<ProductionTrackingAdvanceResultDto?>(results.Count > 0 ? results.Dequeue() : results.Peek());
    }

    private sealed class FakeReadyMadeSalesRepository : IReadyMadeSalesRepository
    {
        public Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
            => Task.FromResult(new ReadyMadeSaleResultDto(
                101,
                "RMS-101",
                1,
                "INV-1",
                sale.CustomerId,
                "Test Customer",
                "REF-1",
                sale.PaymentType,
                100m,
                0m,
                100m,
                100m,
                0m,
                [new ReadyMadeSaleLineDto(88, null, 9, "Ready item", 1, 100m, 100m, 80m)]));
    }

    private sealed class FakeOutboundChannel(string channelName, bool succeed, bool forcedTransientFailure = false) : IOutboundMessageChannel
    {
        public string ChannelName { get; } = channelName;

        public Task<OutboundAdapterResult> SendAsync(CustomerMessageDto message, CancellationToken cancellationToken)
        {
            if (forcedTransientFailure)
                return Task.FromResult(new OutboundAdapterResult(OutboundDispatchOutcome.TransientFailure, FailureReason: "Temporary transport issue", Retryable: true));

            return succeed
                ? Task.FromResult(new OutboundAdapterResult(OutboundDispatchOutcome.Sent, "provider-123"))
                : Task.FromResult(new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "Unsupported payload", Retryable: false));
        }
    }

    private sealed class FakeOrderLoyaltyIntegrationService : IOrderLoyaltyIntegrationService
    {
        private readonly OrderLoyaltyProcessingResult? _result;

        public FakeOrderLoyaltyIntegrationService(OrderLoyaltyProcessingResult? result = null)
        {
            _result = result;
        }

        public Task<OrderLoyaltyProcessingResult?> ProcessIfEligibleAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(_result);
        public Task<OrderLoyaltyProcessingResult> ProcessOrderAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeVipLevelEvaluationService : IVipLevelEvaluationService
    {
        public Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelEvaluationCriteriaDto>>(Array.Empty<VipLevelEvaluationCriteriaDto>());

        public Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipCustomerListItemDto>>(Array.Empty<VipCustomerListItemDto>());

        public Task<VipLevelEvaluationDto?> GetCustomerEvaluationAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelEvaluationDto?>(null);

        public Task<VipLevelEvaluationDto?> EvaluateCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelEvaluationDto?>(null);

        public Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelEvaluationDto>>(Array.Empty<VipLevelEvaluationDto>());

        public Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateNetworkAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelEvaluationDto>>(Array.Empty<VipLevelEvaluationDto>());
    }

    private static async Task<int> SeedQueuedMessageAsync(
        string connectionString,
        string channel,
        string uniqueSuffix,
        string deliveryStatus = "Failed",
        int attemptCount = 1,
        DateTime? processingAtUtc = null,
        string? failureReason = "Transient failure",
        string? providerMessageId = null,
        bool isRetryable = true)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.CustomerMessages (
                CustomerID, EventType, TemplateKey, Channel, Title, Body, DeliveryStatus,
                CreatedAtUtc, AttemptCount, ProcessingAtUtc, FailureReason, ProviderMessageId,
                PhoneNumber, ReferenceID, ReferenceType, IdempotencyKey, IsRetryable)
            OUTPUT INSERTED.CustomerMessageID
            VALUES (@customerId, @eventType, @templateKey, @channel, @title, @body, @deliveryStatus,
                    SYSUTCDATETIME(), @attemptCount, @processingAtUtc, @failureReason, @providerMessageId,
                    @phoneNumber, @referenceId, @referenceType, @idempotencyKey, @isRetryable);", connection);
        command.Parameters.AddWithValue("@customerId", 101);
        command.Parameters.AddWithValue("@eventType", "ValidationConcurrentClaim");
        command.Parameters.AddWithValue("@templateKey", "ValidationCustomerProjection");
        command.Parameters.AddWithValue("@channel", channel);
        command.Parameters.AddWithValue("@title", "Validation title " + uniqueSuffix);
        command.Parameters.AddWithValue("@body", "Validation body " + uniqueSuffix);
        command.Parameters.AddWithValue("@deliveryStatus", deliveryStatus);
        command.Parameters.AddWithValue("@attemptCount", attemptCount);
        var effectiveProcessingAtUtc = processingAtUtc ?? DateTime.UtcNow;
        command.Parameters.AddWithValue("@processingAtUtc", effectiveProcessingAtUtc);
        command.Parameters.AddWithValue("@failureReason", (object?)failureReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@providerMessageId", (object?)(providerMessageId ?? ("provider-live-" + uniqueSuffix)) ?? DBNull.Value);
        command.Parameters.AddWithValue("@phoneNumber", "770000000");
        command.Parameters.AddWithValue("@referenceId", "validation-" + uniqueSuffix);
        command.Parameters.AddWithValue("@referenceType", "Validation");
        command.Parameters.AddWithValue("@idempotencyKey", "validation-" + uniqueSuffix);
        command.Parameters.AddWithValue("@isRetryable", isRetryable);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private sealed class FakeCustomerRepository(params CustomerDetailsDto[] customers) : ICustomerRepository
    {
        public string? CustomerCode { get; set; }

        public Task<IReadOnlyList<CustomerListDto>> GetListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerListDto>>(customers.Select(item => new CustomerListDto(
                item.CustomerId,
                item.CustomerCode,
                item.CustomerName,
                item.PhoneNumber,
                item.TotalPoints,
                item.TotalPieces,
                item.TotalDebts,
                item.RelationshipType)).ToList());

        public Task<CustomerDetailsDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult(customers.FirstOrDefault(item => item.CustomerId == customerId));

        public Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurementsAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerMeasurementDto>>(Array.Empty<CustomerMeasurementDto>());
        public Task<IReadOnlyList<CustomerMeasurementDto>?> UpsertMeasurementsAsync(int customerId, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerMeasurementDto>?>(Array.Empty<CustomerMeasurementDto>());
        public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerLedgerEntryDto>>(Array.Empty<CustomerLedgerEntryDto>());
        public Task<CustomerLoyaltyDto?> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<CustomerLoyaltyDto?>(null);
        public Task<CustomerReferralDto?> GetReferralsAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<CustomerReferralDto?>(null);
        public Task<CustomerDetailsDto> CreateAsync(CreateCustomerDto customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CustomerCreationResultDto> CreateWithReferralAsync(CreateCustomerWithReferralDto customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CustomerCreationResultDto> CreateWithReferralInTransactionAsync(SqlConnection connection, SqlTransaction transaction, CreateCustomerWithReferralDto customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CustomerDetailsDto?> UpdateAsync(int customerId, UpdateCustomerDto customer, CancellationToken cancellationToken) => Task.FromResult<CustomerDetailsDto?>(null);
        public Task<IReadOnlyList<CustomerListDto>> SearchAsync(string term, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerListDto>>(Array.Empty<CustomerListDto>());
        public Task<IReadOnlyList<CustomerReferralCandidateDto>> SearchReferralCandidatesAsync(string term, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerReferralCandidateDto>>(Array.Empty<CustomerReferralCandidateDto>());
        public Task<IReadOnlyList<LinkedCustomerAccessDto>> GetLinkedCustomersAsync(string username, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LinkedCustomerAccessDto>>(Array.Empty<LinkedCustomerAccessDto>());
        public Task<LinkedCustomerAccessDto> CreateManagedCustomerAsync(string username, CreateManagedCustomerDto request, CancellationToken cancellationToken)
            => Task.FromResult(new LinkedCustomerAccessDto(0, null, request.CustomerName, request.RelationshipType ?? "Family", "Managed", true));
        public Task<ReferrerLookupResultDto?> ResolveReferrerAsync(ReferrerLookupRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
                return Task.FromResult<ReferrerLookupResultDto?>(null);

            if (!string.IsNullOrWhiteSpace(request.CustomerCode))
            {
                var match = customers.FirstOrDefault(item => item.CustomerCode == request.CustomerCode && item.IsActive);
                return Task.FromResult(match is null ? null : new ReferrerLookupResultDto(match.CustomerId, match.CustomerCode ?? string.Empty, match.CustomerName ?? string.Empty));
            }

            if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Task.FromResult<ReferrerLookupResultDto?>(null);

            var matches = customers
                .Where(item => item.IsActive && string.Equals(item.CustomerName, request.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(item.PhoneNumber, request.PhoneNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.CustomerId)
                .Select(item => new ReferrerLookupResultDto(item.CustomerId, item.CustomerCode ?? string.Empty, item.CustomerName ?? string.Empty))
                .ToList();

            if (matches.Count == 0)
                return Task.FromResult<ReferrerLookupResultDto?>(null);

            if (matches.Count > 1)
                throw new InvalidOperationException("Multiple active customers matched the supplied name and phone number. Use CustomerCode.");

            return Task.FromResult<ReferrerLookupResultDto?>(matches[0]);
        }

        public Task<IReadOnlyList<CustomerDetailsDto>> GetAncestorsAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CustomerDetailsDto>>(Array.Empty<CustomerDetailsDto>());
        public Task<IReadOnlyList<ReferralTreeNodeDto>> GetDescendantsAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReferralTreeNodeDto>>(Array.Empty<ReferralTreeNodeDto>());
        public Task<int?> ResolveReferralCodeAsync(string referralCode, CancellationToken cancellationToken) => Task.FromResult<int?>(null);
        public Task<bool> WouldCreateCycleAsync(int customerId, int parentCustomerId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private MessageSettingsDto _settings;

        public string? StoreName { get; set; }
        public string? CurrencyName { get; set; }

        public FakeSettingsRepository() : this(new MessageSettingsDto(true, true, false, true, true))
        {
        }

        public FakeSettingsRepository(ISettingsRepository? inner)
            : this(inner is null ? new MessageSettingsDto(true, true, false, true, true) : throw new NotSupportedException())
        {
        }

        public FakeSettingsRepository(MessageSettingsDto settings)
        {
            _settings = settings;
        }

        public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
        public Task<SystemSettingDetailsDto?> GetByIdAsync(int id, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(null);
        public Task<SystemSettingDetailsDto?> GetByKeyAsync(string key, CancellationToken c)
        {
            var value = key switch
            {
                "StoreName" => StoreName,
                "Currency" => CurrencyName,
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(value))
                return Task.FromResult<SystemSettingDetailsDto?>(null);

            return Task.FromResult<SystemSettingDetailsDto?>(new SystemSettingDetailsDto(0, key, value, "General", null));
        }

        public Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SettingCategoryDto>>(Array.Empty<SettingCategoryDto>());
        public Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
        public Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken c) => Task.FromResult(new ProductionRoutesConfigDto(Array.Empty<ProductionRouteEntryDto>()));
        public Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken c) => Task.FromResult(config);
        public Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken c) => Task.FromResult(_settings);
        public Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken c)
        {
            _settings = config;
            return Task.FromResult(_settings);
        }

        public Task<bool> UpdateCurrencySettingsAsync(string currencyName, string preferredCurrency, CancellationToken c) => Task.FromResult(true);
        public Task<bool> UpdatePrintSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken c) => Task.FromResult(true);
        public Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken c) => Task.FromResult(new GeneralCashAccountSettingDto(null, null, false, "لم يتم تحديد صندوق نقدي عام افتراضي بعد."));
        public Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken c) => Task.FromResult(new GeneralCashAccountSettingDto(cashAccountId, null, cashAccountId is not null, ""));
        public Task<IReadOnlyList<SystemSettingListDto>> SearchAsync(string query, CancellationToken c) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
        public Task<SystemSettingListDto> ResolveSettingAsync(string key, CancellationToken c) => Task.FromResult(new SystemSettingListDto(0, key, string.Empty, string.Empty));
        public Task<SystemSettingDetailsDto?> UpsertSettingAsync(SystemSettingDetailsDto setting, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(setting);
        public Task<bool> DeleteSettingAsync(int id, CancellationToken c) => Task.FromResult(true);
        public Task<IReadOnlyList<SystemSettingListDto>> GetSystemSettingsAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
        public Task<SystemSettingDetailsDto?> GetSystemSettingByKeyAsync(string key, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(null);
        public Task<SystemSettingDetailsDto?> UpsertSystemSettingAsync(SystemSettingDetailsDto setting, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(setting);
        public Task<bool> DeleteSystemSettingAsync(int id, CancellationToken c) => Task.FromResult(true);
        public Task<IReadOnlyList<SystemSettingListDto>> GetSettingsByCategoryAsync(string category, CancellationToken c) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
    }

    private sealed class StrictTemplateLookupRepository : IMessageRepository
    {
        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Array.Empty<CustomerMessageDto>());
        public Task<CustomerMessageDto?> GetCustomerMessageAsync(int id, CancellationToken c) => Task.FromResult<CustomerMessageDto?>(null);
        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Array.Empty<CustomerMessageDto>());
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Array.Empty<CustomerNotificationDto>());
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Array.Empty<CustomerNotificationDto>());
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(Array.Empty<MessageTemplateDto>());
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesByIdAsync(int id, CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(Array.Empty<MessageTemplateDto>());
        public Task<MessageTemplateDto?> CreateTemplateAsync(CreateMessageTemplateDto request, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<MessageTemplateDto?> UpdateTemplateAsync(int id, UpdateMessageTemplateDto request, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<MessageTemplateDto?> ActivateTemplateAsync(int id, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<MessageTemplateDto?> DeactivateTemplateAsync(int id, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentMessageDto>>(Array.Empty<SentMessageDto>());
        public Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentLogDto>>(Array.Empty<SentLogDto>());
        public Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c)
        {
            var key = templateKey.Trim();
            if (string.Equals(key, "order-ready", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<MessageTemplateRecord?>(new MessageTemplateRecord(1, "order-ready", "جاهزية الطلب", "InApp", "يسعدنا إبلاغك بأن طلبك رقم {{OrderNumber}} أصبح جاهزاً للاستلام."));
            if (string.Equals(key, "order-delivered", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<MessageTemplateRecord?>(new MessageTemplateRecord(2, "order-delivered", "تسليم الطلب", "InApp", "شكراً لك {{CustomerName}}. تم تسليم الطلب رقم {{OrderNumber}} بنجاح."));
            return Task.FromResult<MessageTemplateRecord?>(null);
        }
        public Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c) => Task.FromResult(false);
        public Task<CustomerMessageDto?> ClaimNextAsync(string channel, int maxAttempts, TimeSpan staleProcessingWindow, CancellationToken cancellationToken) => Task.FromResult<CustomerMessageDto?>(null);
        public Task<CustomerMessageDto?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken) => Task.FromResult<CustomerMessageDto?>(null);
        public Task UpdateDeliveryStatusByProviderIdAsync(string providerMessageId, string status, DateTime occurredAtUtc, string? failureReason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkSentAsync(int customerMessageId, string? providerMessageId, DateTime sentAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkFailedAsync(int customerMessageId, string failureReason, bool retryable, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
            => Task.FromResult((MessageId: (int?)1, NotificationId: (int?)1));
        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c) => Task.FromResult<int?>(1);
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int? customerMessageId, CancellationToken c) => Task.FromResult(1);
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public List<CustomerMessageDto> Messages { get; } = new();
        public List<CustomerNotificationDto> Notifications { get; } = new();
        public HashSet<string> ProcessedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool TemplateExists { get; set; } = true;
        public bool FailNotificationCreation { get; set; }

        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Messages);
        public Task<CustomerMessageDto?> GetCustomerMessageAsync(int id, CancellationToken c) => Task.FromResult<CustomerMessageDto?>(Messages.FirstOrDefault(x => x.CustomerMessageId == id));
        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Messages.Where(x => x.CustomerId == customerId).ToList());
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Notifications);
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Notifications.Where(x => x.CustomerId == customerId).ToList());
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(new[]
        {
            new MessageTemplateDto(1, "dbo.Orders", "OrderCreated", "إنشاء الطلب", "تم إنشاء الطلب", "sms", "مرحباً {CustomerName}، تم إنشاء الطلب رقم {OrderNumber} بقيمة {Amount} ريال.", true, DateTime.UtcNow, DateTime.UtcNow)
        });
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesByIdAsync(int id, CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(new[]
        {
            new MessageTemplateDto(1, "dbo.Orders", "OrderCreated", "إنشاء الطلب", "تم إنشاء الطلب", "sms", "مرحباً {CustomerName}، تم إنشاء الطلب رقم {OrderNumber} بقيمة {Amount} ريال.", true, DateTime.UtcNow, DateTime.UtcNow)
        });
        public Task<MessageTemplateDto?> CreateTemplateAsync(CreateMessageTemplateDto request, CancellationToken c)
            => Task.FromResult<MessageTemplateDto?>(new MessageTemplateDto(1, "dbo.MessageTemplates", request.TemplateKey, null, request.Title, request.Channel, request.Body, request.IsActive, DateTime.UtcNow, DateTime.UtcNow));
        public Task<MessageTemplateDto?> UpdateTemplateAsync(int id, UpdateMessageTemplateDto request, CancellationToken c)
            => Task.FromResult<MessageTemplateDto?>(new MessageTemplateDto(id, "dbo.MessageTemplates", request.TemplateKey ?? "OrderCreated", null, request.Title ?? "تحديث الطلب", request.Channel ?? "InApp", request.Body ?? "مرحبا {{CustomerName}}", request.IsActive ?? true, DateTime.UtcNow, DateTime.UtcNow));
        public Task<MessageTemplateDto?> ActivateTemplateAsync(int id, CancellationToken c)
            => Task.FromResult<MessageTemplateDto?>(new MessageTemplateDto(id, "dbo.MessageTemplates", "OrderCreated", null, "تحديث الطلب", "InApp", "مرحبا {{CustomerName}}", true, DateTime.UtcNow, DateTime.UtcNow));
        public Task<MessageTemplateDto?> DeactivateTemplateAsync(int id, CancellationToken c)
            => Task.FromResult<MessageTemplateDto?>(new MessageTemplateDto(id, "dbo.MessageTemplates", "OrderCreated", null, "تحديث الطلب", "InApp", "مرحبا {{CustomerName}}", false, DateTime.UtcNow, DateTime.UtcNow));
        public Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentMessageDto>>(Array.Empty<SentMessageDto>());
        public Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentLogDto>>(Array.Empty<SentLogDto>());
        public Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c)
            => TemplateExists ? Task.FromResult<MessageTemplateRecord?>(new MessageTemplateRecord(1, templateKey, "إنشاء الطلب", "InApp", "مرحباً {{CustomerName}}، تم إنشاء الطلب رقم {{OrderNumber}} بقيمة {{Amount}} ريال.")) : Task.FromResult<MessageTemplateRecord?>(null);

        public Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c) => Task.FromResult(ProcessedKeys.Contains(idempotencyKey));

        public Task<CustomerMessageDto?> ClaimNextAsync(string channel, int maxAttempts, TimeSpan staleProcessingWindow, CancellationToken cancellationToken)
        {
            var candidate = Messages
                .Where(x => string.Equals(x.Channel, channel, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.DeliveryStatus == "Pending" || x.DeliveryStatus == "Failed")
                .Where(x => x.AttemptCount < maxAttempts)
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefault();

            if (candidate is null)
                return Task.FromResult<CustomerMessageDto?>(null);

            var claimed = candidate with { DeliveryStatus = "Processing", ProcessingAtUtc = DateTime.UtcNow, AttemptCount = candidate.AttemptCount + 1, FailureReason = null };
            var index = Messages.IndexOf(candidate);
            Messages[index] = claimed;
            return Task.FromResult<CustomerMessageDto?>(claimed);
        }

        public Task<CustomerMessageDto?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken)
            => Task.FromResult<CustomerMessageDto?>(Messages.FirstOrDefault(x => string.Equals(x.ProviderMessageId, providerMessageId, StringComparison.OrdinalIgnoreCase)));

        public Task UpdateDeliveryStatusByProviderIdAsync(string providerMessageId, string status, DateTime occurredAtUtc, string? failureReason, CancellationToken cancellationToken)
        {
            var message = Messages.FirstOrDefault(x => string.Equals(x.ProviderMessageId, providerMessageId, StringComparison.OrdinalIgnoreCase));
            if (message is null)
                return Task.CompletedTask;

            var updated = status switch
            {
                "Sent" => message with { DeliveryStatus = message.DeliveryStatus is "Pending" or "Processing" or "Failed" or "Sent" ? "Sent" : message.DeliveryStatus, SentAtUtc = message.SentAtUtc ?? occurredAtUtc, ProcessingAtUtc = null, FailureReason = null, ProviderMessageId = providerMessageId },
                "Delivered" => message with { DeliveryStatus = message.DeliveryStatus is "Pending" or "Processing" or "Failed" or "Sent" or "Delivered" ? "Delivered" : message.DeliveryStatus, DeliveredAtUtc = message.DeliveredAtUtc ?? occurredAtUtc, ProcessingAtUtc = null, FailureReason = null },
                "Read" => message with { DeliveryStatus = message.DeliveryStatus is "Pending" or "Processing" or "Failed" or "Sent" or "Delivered" or "Read" ? "Read" : message.DeliveryStatus, ReadAtUtc = message.ReadAtUtc ?? occurredAtUtc, ProcessingAtUtc = null, FailureReason = null },
                "Failed" => message with { DeliveryStatus = "Failed", FailureReason = failureReason ?? message.FailureReason, ProcessingAtUtc = null },
                _ => message
            };

            var index = Messages.IndexOf(message);
            Messages[index] = updated;
            return Task.CompletedTask;
        }

        public Task MarkSentAsync(int customerMessageId, string? providerMessageId, DateTime sentAtUtc, CancellationToken cancellationToken)
        {
            var message = Messages.First(x => x.CustomerMessageId == customerMessageId);
            var updated = message with { DeliveryStatus = "Sent", SentAtUtc = message.SentAtUtc ?? sentAtUtc, ProcessingAtUtc = null, FailureReason = null, ProviderMessageId = providerMessageId, DeliveredAtUtc = message.DeliveredAtUtc };
            var index = Messages.IndexOf(message);
            Messages[index] = updated;
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(int customerMessageId, string failureReason, bool retryable, CancellationToken cancellationToken)
        {
            var message = Messages.First(x => x.CustomerMessageId == customerMessageId);
            var updated = message with { DeliveryStatus = "Failed", FailureReason = failureReason, ProcessingAtUtc = null };
            var index = Messages.IndexOf(message);
            Messages[index] = updated;
            return Task.CompletedTask;
        }

        public Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
        {
            if (FailNotificationCreation)
            {
                throw new InvalidOperationException("Notification write failed.");
            }

            var messageId = Messages.Count + 1;
            var notificationId = Notifications.Count + 1;
            ProcessedKeys.Add(idempotencyKey);
            Messages.Add(new CustomerMessageDto(messageId, customerId, orderId, eventType, templateKey, channel, title, body, "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, phoneNumber, referenceId, referenceType, idempotencyKey));
            Notifications.Add(new CustomerNotificationDto(notificationId, customerId, orderId, eventType, title, body, "Unread", false, DateTime.UtcNow, null, messageId));
            return Task.FromResult((MessageId: (int?)messageId, NotificationId: (int?)notificationId));
        }

        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
        {
            var id = Messages.Count + 1;
            ProcessedKeys.Add(idempotencyKey);
            Messages.Add(new CustomerMessageDto(id, customerId, orderId, eventType, templateKey, channel, title, body, "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, phoneNumber, referenceId, referenceType, idempotencyKey));
            return Task.FromResult<int?>(id);
        }
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int? customerMessageId, CancellationToken c)
        {
            if (FailNotificationCreation)
                throw new InvalidOperationException("Notification write failed.");

            var id = Notifications.Count + 1;
            Notifications.Add(new CustomerNotificationDto(id, customerId, orderId, eventType, title, body, "Unread", false, DateTime.UtcNow, null, customerMessageId));
            return Task.FromResult(id);
        }
    }
}
