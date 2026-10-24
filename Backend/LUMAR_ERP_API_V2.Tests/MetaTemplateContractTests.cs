using System.Globalization;
using System.Data;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class MetaTemplateContractTests
{
    public static TheoryData<string, int> Contracts => new()
    {
        { "lumar_order_created", 10 },
        { "lumar_order_ready", 4 },
        { "lumar_order_delivered", 2 },
        { "lumar_piece_cancelled", 6 },
        { "lumar_order_cancelled", 4 },
        { "lumar_referrer_reward", 4 },
        { "lumar_referrer_reward_relation", 5 },
        { "lumar_order_created_with_referrer", 14 },
        { "lumar_order_created_with_referrer_relation", 16 },
        { "lumar_friday_greeting", 1 }
    };

    private static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>
    {
        ["customer_name"] = "CustomerName", ["customer_code"] = "CustomerCode",
        ["order_number"] = "OrderNumber", ["order_details"] = "OrderDetails",
        ["total_amount"] = "TotalAmount", ["paid_amount"] = "PaidAmount",
        ["remaining_amount"] = "RemainingAmount", ["delivery_date"] = "DeliveryDate",
        ["points_earned"] = "PointsEarned", ["points_balance"] = "PointsBalance",
        ["customer_points_earned"] = "PointsEarned", ["customer_points_balance"] = "PointsBalance",
        ["store_name"] = "StoreName", ["piece_name"] = "PieceName",
        ["cancelled_amount"] = "CancelledAmount", ["refund_amount"] = "RefundAmount",
        ["order_total"] = "OrderTotal", ["referrer_name"] = "ReferrerName",
        ["referrer_code"] = "ReferrerCode", ["referred_customer_name"] = "ReferredCustomerName",
        ["referral_points"] = "ReferralPointsEarned", ["referral_points_earned"] = "ReferralPointsEarned",
        ["referral_balance"] = "ReferralBalance", ["referrer_points_balance"] = "ReferrerPointsBalance",
        ["relationship"] = "Relationship", ["relation_welcome"] = "RelationWelcome"
    };

    [Theory]
    [MemberData(nameof(Contracts))]
    public async Task RecordedMetaContract_DrivesExactPayloadNamesOrderAndOfficialContextValues(string templateName, int count)
    {
        using var outbound = new PayloadCaptureHandler();
        var factory = new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound));
        var channel = new WhatsAppCloudMessageChannel(factory, new ContractSettingsStore());
        var context = CreateContext();
        var result = await channel.SendAsync(CreateMessage(templateName, context), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Equal(1, factory.CatalogRequests);
        Assert.Single(outbound.Payloads);
        using var payload = JsonDocument.Parse(outbound.Payloads[0]);
        var template = payload.RootElement.GetProperty("template");
        Assert.Equal(templateName, template.GetProperty("name").GetString());
        Assert.Equal("ar", template.GetProperty("language").GetProperty("code").GetString());
        var parameters = template.GetProperty("components")[0].GetProperty("parameters").EnumerateArray().ToArray();
        var expectedNames = ReadRecordedNames(templateName);
        Assert.Equal(count, parameters.Length);
        Assert.Equal(expectedNames, parameters.Select(parameter => parameter.GetProperty("parameter_name").GetString()));
        Assert.Equal(expectedNames.Select(name => Format(context[Sources[name]]!)),
            parameters.Select(parameter => parameter.GetProperty("text").GetString()));
        Assert.All(parameters, parameter => Assert.Equal("text", parameter.GetProperty("type").GetString()));
        Assert.DoesNotContain("UNTRUSTED", outbound.Payloads[0]);
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public async Task EveryRequiredValue_WhenAbsent_IsNamedAndBlocksPost(string templateName, int count)
    {
        var names = ReadRecordedNames(templateName);
        Assert.Equal(count, names.Length);
        foreach (var name in names)
        {
            using var outbound = new PayloadCaptureHandler();
            var channel = new WhatsAppCloudMessageChannel(
                new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound)), new ContractSettingsStore());
            var context = CreateContext();
            context.Remove(Sources[name]);
            if (name == "order_total")
                context.Remove("TotalAmount");
            var result = await channel.SendAsync(CreateMessage(templateName, context), CancellationToken.None);

            Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
            Assert.Equal($"Missing Required Meta Parameter: {name}", result.FailureReason);
            Assert.Empty(outbound.Payloads);
        }
    }

    [Fact]
    public async Task OrderCreated_UsesTenRealNamedParameters_AndFollowsChangedMetadataOrder()
    {
        var catalog = JsonNode.Parse(RecordedMetaTemplateHttpClientFactory.ReadCatalog())!;
        var template = catalog["data"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "lumar_order_created")!;
        var body = template["components"]![0]!;
        var names = body["example"]!["body_text_named_params"]!.AsArray();
        var reversed = names.Reverse().Select(node => node!.DeepClone()).ToArray();
        body["example"]!["body_text_named_params"] = new JsonArray(reversed);
        body["text"] = "UNTRUSTED {{not_a_metadata_parameter}}";
        using var outbound = new PayloadCaptureHandler();
        var channel = new WhatsAppCloudMessageChannel(
            new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound), catalog.ToJsonString()), new ContractSettingsStore());

        var result = await channel.SendAsync(CreateMessage("lumar_order_created", CreateContext()), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        using var payload = JsonDocument.Parse(Assert.Single(outbound.Payloads));
        var parameters = payload.RootElement.GetProperty("template").GetProperty("components")[0].GetProperty("parameters");
        Assert.Equal(ReadRecordedNames("lumar_order_created").Reverse(),
            parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameter_name").GetString()));
    }

    [Fact]
    public async Task UnknownOfficialParameter_IsNotFilledFromExamplesOrBody()
    {
        var catalog = JsonNode.Parse(RecordedMetaTemplateHttpClientFactory.ReadCatalog())!;
        var template = catalog["data"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "lumar_order_created")!;
        template["components"]![0]!["example"]!["body_text_named_params"]!.AsArray()
            .Add(new JsonObject { ["param_name"] = "unknown_official_value", ["example"] = "DO_NOT_SEND" });
        using var outbound = new PayloadCaptureHandler();
        var channel = new WhatsAppCloudMessageChannel(
            new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound), catalog.ToJsonString()), new ContractSettingsStore());

        var result = await channel.SendAsync(CreateMessage("lumar_order_created", CreateContext()), CancellationToken.None);

        Assert.Equal("Missing Required Meta Parameter: unknown_official_value", result.FailureReason);
        Assert.Empty(outbound.Payloads);
    }

    [Theory]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":[{\"id\":\"test\",\"name\":\"lumar_order_created\",\"status\":\"APPROVED\",\"language\":\"ar\",\"components\":[{\"type\":\"BODY\",\"text\":\"{{customer_name}}\"}]}]}")]
    public async Task MissingOfficialMetadata_NeverUsesLocalThreeParameterFallback(string json)
    {
        using var outbound = new PayloadCaptureHandler();
        var channel = new WhatsAppCloudMessageChannel(
            new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound), json), new ContractSettingsStore());

        var result = await channel.SendAsync(CreateMessage("lumar_order_created", CreateContext()), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Contains("Official Meta template contract unavailable", result.FailureReason);
        Assert.Empty(outbound.Payloads);
    }

    [Fact]
    public async Task ProtectedCampaign_DoesNotChangeItsLiveVerifiedPayloadOrRequireCatalog()
    {
        using var outbound = new PayloadCaptureHandler();
        var factory = new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound), "{\"data\":[]}");
        var context = CreateContext();
        context["RawCampaignText"] = "Campaign input";
        var channel = new WhatsAppCloudMessageChannel(factory, new ContractSettingsStore());
        var message = CreateMessage("lumar_general_campaign", context) with { CampaignImageUrl = "https://example.com/campaign.png" };

        var result = await channel.SendAsync(message, CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Equal(0, factory.CatalogRequests);
        using var payload = JsonDocument.Parse(Assert.Single(outbound.Payloads));
        var components = payload.RootElement.GetProperty("template").GetProperty("components");
        Assert.Equal("https://example.com/campaign.png", components[0].GetProperty("parameters")[0].GetProperty("image").GetProperty("link").GetString());
        Assert.Equal(new[] { "customer_name", "campaign_text" },
            components[1].GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("parameter_name").GetString()));
    }

    [Theory]
    [InlineData("order-created-customer-smart", "lumar_order_created")]
    [InlineData("order-referrer-combined-smart", "lumar_order_created_with_referrer")]
    [InlineData("referrer-points-earned-smart", "lumar_referrer_reward")]
    [InlineData("ReferralPointsEarned", "lumar_referrer_reward")]
    [InlineData("order-ready", "lumar_order_ready")]
    [InlineData("order-delivered", "lumar_order_delivered")]
    [InlineData("piece-cancelled", "lumar_piece_cancelled")]
    [InlineData("order-created-with-referrer-relation", "lumar_order_created_with_referrer_relation")]
    public void PersistedSmartTemplateKeys_ResolveToTheCorrectRecipientContract(string key, string expectedName)
    {
        Assert.Equal(expectedName, WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve(key, "OrderCreated")?.MetaName);
    }

    [Fact]
    public async Task RepositoryHydration_PreservesDatabaseTypesAndDoesNotFillMissingSources()
    {
        using var table = new DataTable();
        table.Columns.Add("CustomerCode", typeof(string));
        table.Columns.Add("PaidAmount", typeof(decimal));
        table.Columns.Add("DeliveryDate", typeof(DateTime));
        table.Columns.Add("PointsEarned", typeof(decimal));
        table.Columns.Add("RelationWelcome", typeof(string));
        table.Rows.Add("C-801", 0m, new DateTime(2026, 10, 25), 200m, DBNull.Value);
        using var reader = table.CreateDataReader();
        Assert.True(await reader.ReadAsync());
        var context = new Dictionary<string, object?> { ["RelationWelcome"] = "Stale value" };
        var mapper = typeof(MessageRepository).GetMethod("CopyContextFields", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(mapper);
        mapper.Invoke(null, [reader, context]);

        Assert.Equal("C-801", context["CustomerCode"]);
        Assert.Equal(0m, Assert.IsType<decimal>(context["PaidAmount"]));
        Assert.Equal(200m, Assert.IsType<decimal>(context["PointsEarned"]));
        Assert.Equal(new DateTime(2026, 10, 25), Assert.IsType<DateTime>(context["DeliveryDate"]));
        Assert.False(context.ContainsKey("RelationWelcome"));
        Assert.False(context.ContainsKey("CancelledAmount"));
    }

    [Theory]
    [InlineData("أب", "والدك")]
    [InlineData("أم", "والدتك")]
    [InlineData("أخ", "أخاك")]
    [InlineData("Brother", "أخاك")]
    [InlineData("أخت", "أختك")]
    [InlineData("ابن", "ابنك")]
    [InlineData("ابنة", "ابنتك")]
    [InlineData("ابن عم", "ابن عمك")]
    [InlineData("صديق", "صديقك")]
    [InlineData("Friend", "صديقك")]
    public void RelationshipWelcomeMapper_MapsOnlyApprovedValues(string relationshipType, string expected)
    {
        Assert.Equal(expected, RelationshipWelcomeMapper.TryMap(relationshipType));
    }

    [Theory]
    [InlineData("عم")]
    [InlineData("حفيد")]
    [InlineData("أبو عم")]
    [InlineData("صهر")]
    public void RelationshipWelcomeMapper_RejectsUnapprovedRelationshipNames(string relationshipType)
    {
        Assert.Null(RelationshipWelcomeMapper.TryMap(relationshipType));
    }

    [Theory]
    [InlineData("Primary")]
    [InlineData("???")]
    [InlineData("أخرى")]
    [InlineData("فارغ")]
    [InlineData("NULL")]
    [InlineData("لا توجد صلة")]
    [InlineData("")]
    [InlineData(null)]
    public void RelationshipWelcomeMapper_IgnoresInvalidValues(string? relationshipType)
    {
        Assert.Null(RelationshipWelcomeMapper.TryMap(relationshipType));
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]
    public async Task CatalogHttpFailure_PreservesRetryabilityAndNeverPosts(int statusCode, bool retryable)
    {
        using var outbound = new PayloadCaptureHandler();
        var channel = new WhatsAppCloudMessageChannel(
            new RecordedMetaTemplateHttpClientFactory(new HttpClient(outbound), "{\"error\":{\"code\":100}}", (HttpStatusCode)statusCode),
            new ContractSettingsStore());

        var result = await channel.SendAsync(CreateMessage("lumar_order_created", CreateContext()), CancellationToken.None);

        Assert.Equal(retryable, result.Retryable);
        Assert.Equal(statusCode, result.HttpStatusCode);
        Assert.Equal(retryable ? OutboundDispatchOutcome.TransientFailure : OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Empty(outbound.Payloads);
    }

    internal static string[] ReadRecordedNames(string templateName)
    {
        using var document = JsonDocument.Parse(RecordedMetaTemplateHttpClientFactory.ReadCatalog());
        var template = document.RootElement.GetProperty("data").EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == templateName);
        return template.GetProperty("components").EnumerateArray().Single(component => component.GetProperty("type").GetString() == "BODY")
            .GetProperty("example").GetProperty("body_text_named_params").EnumerateArray()
            .Select(parameter => parameter.GetProperty("param_name").GetString()!).ToArray();
    }

    internal static Dictionary<string, object?> CreateContext() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["CustomerName"] = "Order customer", ["CustomerCode"] = "C-801", ["OrderNumber"] = "ORD-901",
        ["OrderDetails"] = "Shirt, trousers", ["TotalAmount"] = 1250m, ["PaidAmount"] = 950m,
        ["RemainingAmount"] = 300m, ["DeliveryDate"] = new DateTime(2026, 10, 25),
        ["PointsEarned"] = 18m, ["PointsBalance"] = 320m, ["StoreName"] = "Configured branch",
        ["PieceName"] = "Shirt", ["CancelledAmount"] = 150m, ["RefundAmount"] = 50m,
        ["OrderTotal"] = 1100m, ["ReferrerName"] = "Referring customer", ["ReferrerCode"] = "C-802",
        ["ReferredCustomerName"] = "Referred customer", ["ReferralPointsEarned"] = 12m,
        ["ReferrerPointsBalance"] = 120m, ["ReferralBalance"] = 60m, ["Relationship"] = "Configured relationship",
        ["RelationWelcome"] = "Configured welcome"
    };

    private static CustomerMessageDto CreateMessage(string templateName, IReadOnlyDictionary<string, object?> context) => new(
        760, 801, 901, "OrderCreated", templateName, "WhatsApp", "UNTRUSTED TITLE", "UNTRUSTED BODY",
        "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "967700000001", "UNTRUSTED REF", "Order", "contract-test", null, context);

    private static string Format(object value) => value is DateTime date
        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : Convert.ToString(value, CultureInfo.InvariantCulture)!;

    private sealed class PayloadCaptureHandler : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}") };
        }
    }

    private sealed class ContractSettingsStore : IWhatsAppConnectionSettingsStore
    {
        public Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(
            new WhatsAppRuntimeSettings(true, "https://graph.facebook.com", "v20.0", "test-phone-id", "test-token", "", "", 30));
        public Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

internal sealed class RecordedMetaTemplateHttpClientFactory(HttpClient outboundClient, string? catalogJson = null, HttpStatusCode statusCode = HttpStatusCode.OK) : IHttpClientFactory
{
    private readonly string _catalogJson = catalogJson ?? ReadCatalog();
    private readonly HttpStatusCode _statusCode = statusCode;
    public int CatalogRequests { get; private set; }
    public HttpClient CreateClient(string name) => name == "WhatsAppCloud"
        ? outboundClient
        : new HttpClient(new RecordedCatalogHandler(this));

    public static string ReadCatalog() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "meta-template-catalog-2026-10-24.json"));

    private sealed class RecordedCatalogHandler(RecordedMetaTemplateHttpClientFactory owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("graph.facebook.com", request.RequestUri!.Host);
            Assert.Equal("/v20.0/1413441637074832/message_templates", request.RequestUri.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            owner.CatalogRequests++;
            return Task.FromResult(new HttpResponseMessage(owner._statusCode)
            {
                Content = new StringContent(owner._catalogJson, Encoding.UTF8, "application/json")
            });
        }
    }
}