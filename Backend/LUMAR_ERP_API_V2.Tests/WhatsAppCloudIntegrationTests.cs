using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class WhatsAppCloudIntegrationTests
{
    [Fact]
    public void YemenPhoneNumberNormalizer_NormalizesSupportedYemenFormats()
    {
        var normalizer = new YemenPhoneNumberNormalizer();

        Assert.Equal("967775106545", normalizer.TryNormalizeMobileNumber("775106545"));
        Assert.Equal("967775106545", normalizer.TryNormalizeMobileNumber("+967775106545"));
        Assert.Equal("967775106545", normalizer.TryNormalizeMobileNumber("967775106545"));
        Assert.Equal("967775106545", normalizer.TryNormalizeMobileNumber("00967775106545"));
        Assert.Equal("967775106545", normalizer.TryNormalizeMobileNumber("+ 967 775 106 545"));
        Assert.Equal("967775106545", normalizer.TryNormalizeMobileNumber("967 775 106 545"));
        Assert.Null(normalizer.TryNormalizeMobileNumber("0775106545"));
        Assert.Null(normalizer.TryNormalizeMobileNumber("966500000000"));
        Assert.Null(normalizer.TryNormalizeMobileNumber("12345"));
        Assert.Null(normalizer.TryNormalizeMobileNumber(""));
        Assert.Null(normalizer.TryNormalizeMobileNumber(null));
    }

    [Fact]
    public void MetaTemplateCatalog_UsesApprovedMetaNamesAndHeaderImagePathForCampaigns()
    {
        var catalogEntries = new[]
        {
            ("lumar_order_created", "lumar_order_created", "ar", false),
            ("lumar_order_ready", "lumar_order_ready", "ar", false),
            ("lumar_order_delivered", "lumar_order_delivered", "ar", false),
            ("lumar_piece_cancelled", "lumar_piece_cancelled", "ar", false),
            ("lumar_order_cancelled", "lumar_order_cancelled", "ar", false),
            ("lumar_referrer_reward", "lumar_referrer_reward", "ar", false),
            ("lumar_referrer_reward_relation", "lumar_referrer_reward_relation", "ar", false),
            ("lumar_order_created_with_referrer", "lumar_order_created_with_referrer", "ar", false),
            ("lumar_order_created_with_referrer_relation", "lumar_order_created_with_referrer_relation", "ar", false),
            ("lumar_friday_greeting", "lumar_friday_greeting", "ar", false),
            ("lumar_general_campaign", "lumar_general_campaign", "ar", true)
        };

        foreach (var (templateKey, expectedMetaName, expectedLanguage, expectedHeader) in catalogEntries)
        {
            var definition = WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve(templateKey, templateKey);
            Assert.NotNull(definition);
            Assert.Equal(expectedMetaName, definition!.MetaName);
            Assert.Equal(expectedLanguage, definition.LanguageCode);
            Assert.Equal(expectedHeader, definition.RequiresHeader);
        }

        var campaignMessage = new CustomerMessageDto(
            91, 92, 93, "ManualCampaign", "lumar_general_campaign", "WhatsApp",
            "العنوان", "نص الحملة", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null,
            "+967700000099", "campaign-099", "ManualCampaign", "key-99");

        var headerComponent = WhatsAppCloudMessageChannel.MetaTemplateCatalog.TryCreateHeaderComponent(
            campaignMessage with { ReferenceId = "https://example.com/campaign.png" },
            WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve("lumar_general_campaign", "ManualCampaign")!);
        Assert.NotNull(headerComponent);

        var json = System.Text.Json.JsonSerializer.Serialize(headerComponent);
        Assert.Contains("type\":\"header\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type\":\"image\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/campaign.png", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenDisabled_DoesNotFakeSuccess()
    {
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = false,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123",
                AccessToken = "token"
            }));

        var result = await channel.SendAsync(new CustomerMessageDto(
            7, 17, 42, "OrderCreated", "OrderCreated", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "967700000001", "ORD-42", "Order", "key"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Contains("disabled", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenTemplateKeyMatchesApprovedMetaTemplate_SendsTemplatePayload()
    {
        var handler = new CaptureHandler();
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(handler)),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            8, 18, 44, "OrderCreated", "lumar_order_created", "WhatsApp", "العنوان", "تم إنشاء طلبك بنجاح", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000002", "ORD-44", "Order", "key-2"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Contains("\"type\":\"template\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("lumar_order_created", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"code\":\"ar\"", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_SuccessfulProviderResponse_UsesProviderMessageId()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.123456789\"}]}");
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(handler)),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value",
                VerifyToken = "verify-token",
                AppSecret = "secret"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            8, 18, 44, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000002", "ORD-44", "Order", "key-2"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal("wamid.123456789", result.ProviderMessageId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaReturns200WithoutMessageId_FailsSafely()
    {
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}"))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            9, 19, 45, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000003", "ORD-45", "Order", "key-3"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Contains("Meta HTTP 200", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaRejectsWithErrorDetails_RecordsSafeMetadata()
    {
        var response = "{\"error\":{\"type\":\"OAuthException\",\"code\":100,\"error_subcode\":400010,\"message\":\"Auth error: AccessToken=SECRET_TOKEN_123 Authorization: Bearer SECRET_BEARER\"}}";
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.BadRequest, response))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            10, 20, 46, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000004", "ORD-46", "Order", "key-4"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal(100, result.ProviderErrorCode);
        Assert.Equal(400010, result.ProviderErrorSubcode);
        Assert.Equal("OAuthException", result.SafeProviderErrorType);
        Assert.DoesNotContain("SECRET_TOKEN_123", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaRejectsWith429_ReportsTransientFailure()
    {
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, "{\"error\":{\"type\":\"RateLimit\",\"code\":190,\"message\":\"Rate limit exceeded\"}}"))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            11, 21, 47, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000005", "ORD-47", "Order", "key-5"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.TransientFailure, result.Outcome);
        Assert.Equal(429, result.HttpStatusCode);
        Assert.True(result.Retryable);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_SanitizesAccessTokenAndAuthorizationTokensAcrossFailureReasonAndLogger()
    {
        var logger = new CaptureLogger<WhatsAppCloudMessageChannel>();
        var response = "{\"error\":{\"type\":\"OAuthException\",\"code\":100,\"error_subcode\":400010,\"message\":\"AccessToken=SECRET_TOKEN_123 Authorization: Bearer SECRET_BEARER AppSecret=APP_SECRET_VALUE VerifyToken: VERIFY_SECRET_VALUE\"}}";
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.BadRequest, response))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            logger);

        var result = await channel.SendAsync(new CustomerMessageDto(
            12, 22, 48, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000006", "ORD-48", "Order", "key-6"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal(100, result.ProviderErrorCode);
        Assert.Equal(400010, result.ProviderErrorSubcode);
        Assert.Equal("OAuthException", result.SafeProviderErrorType);
        Assert.True(result.FailureReason.Length <= 500, $"FailureReason length exceeded safe cap: {result.FailureReason.Length}");
        Assert.DoesNotContain("SECRET_TOKEN_123", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("APP_SECRET_VALUE", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("VERIFY_SECRET_VALUE", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", result.FailureReason, StringComparison.OrdinalIgnoreCase);

        var logText = string.Join(" ", logger.Messages);
        Assert.DoesNotContain("SECRET_TOKEN_123", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("APP_SECRET_VALUE", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("VERIFY_SECRET_VALUE", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token-value", logText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppWebhookController_ValidMetaQueryStringVerification_ReturnsChallenge()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, VerifyToken = "valid-token" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?hub.mode=subscribe&hub.challenge=challenge-abc&hub.verify_token=valid-token");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.VerifyWebhook(
            httpContext.Request.Query["hub.mode"],
            httpContext.Request.Query["hub.challenge"],
            httpContext.Request.Query["hub.verify_token"]);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("challenge-abc", content.Content);
    }

    [Fact]
    public async Task WhatsAppWebhookController_UsesRuntimeSettings_RejectsEmptyVerifyToken()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, VerifyToken = string.Empty, AppSecret = "legacy-secret" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?hub.mode=subscribe&hub.challenge=challenge-abc&hub.verify_token=runtime-token");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.VerifyWebhook(
            httpContext.Request.Query["hub.mode"],
            httpContext.Request.Query["hub.challenge"],
            httpContext.Request.Query["hub.verify_token"]);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task WhatsAppWebhookController_RejectsWrongNonEmptyVerifyToken()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, VerifyToken = "valid-token" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?hub.mode=subscribe&hub.challenge=challenge-abc&hub.verify_token=wrong-token");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.VerifyWebhook(
            httpContext.Request.Query["hub.mode"],
            httpContext.Request.Query["hub.challenge"],
            httpContext.Request.Query["hub.verify_token"]);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task WhatsAppWebhookController_RejectsMissingOrInvalidModeAndTokenMetaQueryValues()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, VerifyToken = "valid-token" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);

        var missingTokenCtx = new DefaultHttpContext();
        missingTokenCtx.Request.QueryString = new QueryString("?hub.mode=subscribe&hub.challenge=challenge-abc");
        controller.ControllerContext = new ControllerContext { HttpContext = missingTokenCtx };
        var missingTokenResult = await controller.VerifyWebhook(
            missingTokenCtx.Request.Query["hub.mode"],
            missingTokenCtx.Request.Query["hub.challenge"],
            missingTokenCtx.Request.Query["hub.verify_token"]);
        Assert.IsType<BadRequestResult>(missingTokenResult);

        var invalidModeCtx = new DefaultHttpContext();
        invalidModeCtx.Request.QueryString = new QueryString("?hub.mode=not-subscribe&hub.challenge=challenge-abc&hub.verify_token=valid-token");
        controller.ControllerContext = new ControllerContext { HttpContext = invalidModeCtx };
        var invalidModeResult = await controller.VerifyWebhook(
            invalidModeCtx.Request.Query["hub.mode"],
            invalidModeCtx.Request.Query["hub.challenge"],
            invalidModeCtx.Request.Query["hub.verify_token"]);
        Assert.IsType<BadRequestResult>(invalidModeResult);
    }

    [Fact]
    public async Task WhatsAppWebhookController_ConvertsUnixSecondsTimestampToUtc()
    {
        var repository = new StubMessageRepository();
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(repository, store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.1\",\"status\":\"sent\",\"timestamp\":1718633029} ]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1718633029).UtcDateTime, repository.LastOccurredAtUtc);
    }

    [Fact]
    public async Task WhatsAppWebhookController_ValidPostSignature_Accepts_WhenStatusTimestampIsNumericString()
    {
        var repository = new StubMessageRepository();
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(repository, store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.string\",\"status\":\"sent\",\"timestamp\":\"1750263773\"}]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1750263773).UtcDateTime, repository.LastOccurredAtUtc);
    }

    [Fact]
    public async Task WhatsAppWebhookController_ValidPostSignature_AppliesSafeFallback_WhenTimestampMissing()
    {
        var repository = new StubMessageRepository();
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(repository, store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.missing\",\"status\":\"sent\"}]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.InRange(repository.LastOccurredAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task WhatsAppWebhookController_ValidPostSignature_AppliesSafeFallback_WhenTimestampInvalid()
    {
        var repository = new StubMessageRepository();
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(repository, store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.invalid\",\"status\":\"sent\",\"timestamp\":\"not-a-number\"}]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.InRange(repository.LastOccurredAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task WhatsAppWebhookController_ValidPostSignature_Accepts_WhenRuntimeAppSecretMatches()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.1\",\"status\":\"sent\",\"timestamp\":123} ]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task WhatsAppWebhookController_InvalidPostSignature_Rejects()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.1\",\"status\":\"sent\",\"timestamp\":123} ]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=invalid";

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task WhatsAppWebhookController_EmptyRuntimeAppSecret_RejectsPostSafely()
    {
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=anything";

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task WhatsAppWebhookController_ValidIncomingMessageWebhook_LogsSafeInboundObservability()
    {
        var logger = new CaptureLogger<WhatsAppWebhookController>();
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
        {
            Enabled = true,
            PhoneNumberId = "123456789",
            AppSecret = "runtime-secret"
        });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, logger);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"metadata\":{\"phone_number_id\":\"123456789\"},\"messages\":[{\"from\":\"967000000001\",\"type\":\"text\",\"text\":{\"body\":\"اختبار استقبال LUMAR\"}}]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        var logText = string.Join(" ", logger.Messages).ToLowerInvariant();
        Assert.Contains("signaturevalid=true", logText, StringComparison.Ordinal);
        Assert.Contains("inboundmessagepresent=true", logText, StringComparison.Ordinal);
        Assert.Contains("phonenumberidmatchedruntime=true", logText, StringComparison.Ordinal);
        Assert.Contains("senderpresent=true", logText, StringComparison.Ordinal);
        Assert.Contains("inboundmessagetype=text", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("اختبار استقبال LUMAR", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("967000000001", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-secret", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("sha256=", logText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatsAppWebhookController_StatusOnlyWebhook_LogsStatusObservabilityWithoutInboundMessage()
    {
        var logger = new CaptureLogger<WhatsAppWebhookController>();
        var store = new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions { Enabled = true, AppSecret = "runtime-secret" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), store, logger);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var rawBody = "{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.1\",\"status\":\"sent\",\"timestamp\":123}]}}]}]}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        controller.ControllerContext.HttpContext.Request.Body = ms;
        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = ComputeSignature(rawBody, "runtime-secret");

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        var logText = string.Join(" ", logger.Messages).ToLowerInvariant();
        Assert.Contains("statuswebhookpresent=true", logText, StringComparison.Ordinal);
        Assert.Contains("inboundmessagepresent=false", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("wamid.1", logText, StringComparison.Ordinal);
    }

    private static string ComputeSignature(string rawBody, string appSecret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return $"sha256={hash}";
    }

    private sealed class FakeWhatsAppConnectionSettingsStore(WhatsAppOptions options) : IWhatsAppConnectionSettingsStore
    {
        public Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppConnectionStatusDto(
                Enabled: options.Enabled,
                PhoneNumberId: options.PhoneNumberId,
                TokenConfigured: !string.IsNullOrWhiteSpace(options.AccessToken),
                ConnectionConfigured: options.Enabled && !string.IsNullOrWhiteSpace(options.PhoneNumberId) && !string.IsNullOrWhiteSpace(options.AccessToken),
                Message: "test"));

        public Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppRuntimeSettings(
                Enabled: options.Enabled,
                BaseUrl: string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://graph.facebook.com" : options.BaseUrl,
                ApiVersion: string.IsNullOrWhiteSpace(options.ApiVersion) ? "v20.0" : options.ApiVersion,
                PhoneNumberId: options.PhoneNumberId,
                AccessToken: options.AccessToken,
                VerifyToken: options.VerifyToken,
                AppSecret: options.AppSecret,
                TimeoutSeconds: options.TimeoutSeconds));

        public Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken)
        {
            options.Enabled = request.Enabled;
            if (!string.IsNullOrWhiteSpace(request.PhoneNumberId)) options.PhoneNumberId = request.PhoneNumberId;
            if (!string.IsNullOrWhiteSpace(request.AccessToken)) options.AccessToken = request.AccessToken;
            if (!string.IsNullOrWhiteSpace(request.VerifyToken)) options.VerifyToken = request.VerifyToken;
            if (!string.IsNullOrWhiteSpace(request.AppSecret)) options.AppSecret = request.AppSecret;
            if (!string.IsNullOrWhiteSpace(request.BaseUrl)) options.BaseUrl = request.BaseUrl;
            if (!string.IsNullOrWhiteSpace(request.ApiVersion)) options.ApiVersion = request.ApiVersion;
            if (request.TimeoutSeconds > 0) options.TimeoutSeconds = request.TimeoutSeconds;

            return GetStatusAsync(cancellationToken);
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FixedHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.template\"}]}")
            };
        }
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public StubHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubMessageRepository : IMessageRepository
    {
        public DateTime LastOccurredAtUtc { get; private set; }

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
        public Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c) => Task.FromResult<MessageTemplateRecord?>(null);
        public Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c) => Task.FromResult(false);
        public Task<CustomerMessageDto?> ClaimNextAsync(string channel, int maxAttempts, TimeSpan staleProcessingWindow, CancellationToken cancellationToken) => Task.FromResult<CustomerMessageDto?>(null);
        public Task<CustomerMessageDto?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken) => Task.FromResult<CustomerMessageDto?>(null);
        public Task UpdateDeliveryStatusByProviderIdAsync(string providerMessageId, string status, DateTime occurredAtUtc, string? failureReason, CancellationToken cancellationToken)
        {
            LastOccurredAtUtc = occurredAtUtc;
            return Task.CompletedTask;
        }
        public Task MarkSentAsync(int customerMessageId, string? providerMessageId, DateTime sentAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkFailedAsync(int customerMessageId, string failureReason, bool retryable, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c) => Task.FromResult<(int? MessageId, int? NotificationId)>((null, null));
        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c) => Task.FromResult<int?>(null);
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int? customerMessageId, CancellationToken c) => Task.FromResult(0);
    }
}
