using System.Collections.Generic;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
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
            Assert.Equal("0500000000", row.PhoneNumber);
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
                    "0500000000",
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
                    "0500000000",
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
            PhoneNumber: "0500000000",
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
            PhoneNumber: "0500000000",
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
            PhoneNumber: "0500000000",
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

        var messageId = await repo.CreateCustomerMessageAsync(7, 99, "OrderCreated", "OrderCreated", "InApp", "Title", "Body", "0500000000", "ORD-99", "Order", "idempotency-1", CancellationToken.None);
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

        var messageId = await repo.CreateCustomerMessageAsync(7, 99, "OrderCreated", "OrderCreated", "InApp", "Title", "Body", "0500000000", "ORD-99", "Order", "idempotency-2", CancellationToken.None);
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
    public async Task TryCreateAsync_UsesUniqueRewardGrantIdentity_ForReferralRewards()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var rewardA = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "0500000000",
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
            PhoneNumber: "0500000000",
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
            PhoneNumber: "0500000000",
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
            PhoneNumber: "0500000000",
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
    public async Task TryCreateAsync_WhenCustomerHasNoReferrer_CreatesOnlyCustomerMessage()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "order-created-customer-smart",
            PhoneNumber: "0500000001",
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
            PhoneNumber: "0500000001",
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
                ["ReferrerPhoneNumber"] = "0500009999",
                ["CustomerPhoneNumber"] = "0500000001",
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
            PhoneNumber: "0500000000",
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
            "0500000099",
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
            "0500000100",
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
            new ProductionTrackingAdvanceResultDto(13, "TRK-100", "Ready", "ReadyForDelivery", null, "Order is ready for delivery and can be packed.", true));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(13, "TRK-100", "YALAQ", "ReadyForDelivery", "SCN-2", "EMP-2", "REF-2", 9), CancellationToken.None);

        Assert.Single(messageService.Requests);
        Assert.Equal("OrderReady", messageService.Requests[0].EventType);
        Assert.Equal("ORD-100", messageService.Requests[0].ReferenceId);
        Assert.Equal(7, messageService.Requests[0].CustomerId);
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
                "0500000200",
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
    public async Task OrderService_DeliverAsync_WhenOrderIsOfficiallyDelivered_SendsOrderDeliveredOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            201,
            "ORD-201",
            8,
            "أحمد",
            "0500000201",
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
    public async Task TryCreateAsync_UsesIdempotencyForReadyAndDeliveredBusinessEvents()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var readyOne = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderReady",
            "OrderReady",
            "0500000042",
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
            "0500000042",
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
            "0500000042",
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
            "0500000042",
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

    private sealed class FakeOrderRepository(OrderDetailsDto? currentOrder = null, OrderDetailsDto? deliveredOrder = null, bool? deliverResult = null) : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>(Array.Empty<OrderListDto>());
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(currentOrder);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProductionRepository(PieceDto piece, ProductionTrackingAdvanceResultDto result) : IProductionRepository
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
        public Task<OrderLoyaltyProcessingResult?> ProcessIfEligibleAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderLoyaltyProcessingResult?>(null);
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
        string? failureReason = "Transient failure")
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.CustomerMessages (
                CustomerID, EventType, TemplateKey, Channel, Title, Body, DeliveryStatus,
                CreatedAtUtc, AttemptCount, ProcessingAtUtc, FailureReason, ProviderMessageId,
                PhoneNumber, ReferenceID, ReferenceType, IdempotencyKey)
            OUTPUT INSERTED.CustomerMessageID
            VALUES (@customerId, @eventType, @templateKey, @channel, @title, @body, @deliveryStatus,
                    SYSUTCDATETIME(), @attemptCount, @processingAtUtc, @failureReason, @providerMessageId,
                    @phoneNumber, @referenceId, @referenceType, @idempotencyKey);", connection);
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
        command.Parameters.AddWithValue("@providerMessageId", "provider-live-" + uniqueSuffix);
        command.Parameters.AddWithValue("@phoneNumber", "0500000000");
        command.Parameters.AddWithValue("@referenceId", "validation-" + uniqueSuffix);
        command.Parameters.AddWithValue("@referenceType", "Validation");
        command.Parameters.AddWithValue("@idempotencyKey", "validation-" + uniqueSuffix);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private MessageSettingsDto _settings;

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
        public Task<SystemSettingDetailsDto?> GetByKeyAsync(string key, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(null);
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
            => TemplateExists ? Task.FromResult<MessageTemplateRecord?>(new MessageTemplateRecord(1, templateKey, "إنشاء الطلب", "sms", "مرحباً {{CustomerName}}، تم إنشاء الطلب رقم {{OrderNumber}} بقيمة {{Amount}} ريال.")) : Task.FromResult<MessageTemplateRecord?>(null);

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
                "Sent" => message with { DeliveryStatus = "Sent", SentAtUtc = occurredAtUtc, ProcessingAtUtc = null, FailureReason = null, ProviderMessageId = providerMessageId, DeliveredAtUtc = message.DeliveredAtUtc ?? occurredAtUtc },
                "Delivered" => message with { DeliveryStatus = "Delivered", DeliveredAtUtc = occurredAtUtc, ProcessingAtUtc = null, FailureReason = null },
                "Read" => message with { DeliveryStatus = "Read", ReadAtUtc = occurredAtUtc, ProcessingAtUtc = null, FailureReason = null },
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
            var updated = message with { DeliveryStatus = "Sent", SentAtUtc = sentAtUtc, ProcessingAtUtc = null, FailureReason = null, ProviderMessageId = providerMessageId, DeliveredAtUtc = message.DeliveredAtUtc ?? sentAtUtc };
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
            Messages.Add(new CustomerMessageDto(messageId, customerId, orderId, eventType, templateKey, channel, title, body, "Pending", DateTime.UtcNow, null, null, 0, null, null, null, phoneNumber, referenceId, referenceType, idempotencyKey));
            Notifications.Add(new CustomerNotificationDto(notificationId, customerId, orderId, eventType, title, body, "Unread", false, DateTime.UtcNow, null, messageId));
            return Task.FromResult((MessageId: (int?)messageId, NotificationId: (int?)notificationId));
        }

        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
        {
            var id = Messages.Count + 1;
            ProcessedKeys.Add(idempotencyKey);
            Messages.Add(new CustomerMessageDto(id, customerId, orderId, eventType, templateKey, channel, title, body, "Pending", DateTime.UtcNow, null, null, 0, null, null, null, phoneNumber, referenceId, referenceType, idempotencyKey));
            return Task.FromResult<int?>(id);
        }
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int customerMessageId, CancellationToken c)
        {
            var id = Notifications.Count + 1;
            Notifications.Add(new CustomerNotificationDto(id, customerId, orderId, eventType, title, body, "Unread", false, DateTime.UtcNow, null, customerMessageId));
            return Task.FromResult(id);
        }
    }
}
