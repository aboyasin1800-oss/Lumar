using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            "+967700000099", "campaign-099", "ManualCampaign", "key-99", "https://example.com/campaign.png");

        var headerComponent = WhatsAppCloudMessageChannel.MetaTemplateCatalog.TryCreateHeaderComponent(
            campaignMessage,
            WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve("lumar_general_campaign", "ManualCampaign")!);
        Assert.NotNull(headerComponent);

        var json = System.Text.Json.JsonSerializer.Serialize(headerComponent);
        Assert.Contains("type\":\"header\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type\":\"image\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/campaign.png", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MetaTemplateCatalog_ImplementsExplicitParameterContracts_ForAllSupportedTemplates()
    {
        var expected = new[]
        {
            ("lumar_order_created", new[] { "customer_name", "order_number", "total_amount" }),
            ("lumar_order_ready", new[] { "customer_name", "order_number", "delivery_date" }),
            ("lumar_order_delivered", new[] { "customer_name", "order_number", "delivery_date" }),
            ("lumar_piece_cancelled", new[] { "customer_name", "order_number", "order_details" }),
            ("lumar_order_cancelled", new[] { "customer_name", "order_number", "order_details" }),
            ("lumar_referrer_reward", new[] { "referrer_name", "referrer_code", "referral_points_earned" }),
            ("lumar_referrer_reward_relation", new[] { "referrer_name", "relationship", "referral_points_earned" }),
            ("lumar_order_created_with_referrer", new[] { "customer_name", "order_number", "referrer_name" }),
            ("lumar_order_created_with_referrer_relation", new[] { "customer_name", "order_number", "relationship" }),
            ("lumar_friday_greeting", new[] { "customer_name", "relation_welcome" }),
            ("lumar_general_campaign", new[] { "customer_name", "campaign_text" })
        };

        foreach (var (templateKey, parameterNames) in expected)
        {
            var definition = WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve(templateKey, templateKey);
            Assert.NotNull(definition);
            Assert.Equal(parameterNames, definition!.Parameters.Select(p => p.Name).ToArray());
        }
    }

    [Fact]
    public void MetaTemplateCatalog_WhenCampaignImageIsNotConfigured_DoesNotInventHeaderFallback()
    {
        var message = new CustomerMessageDto(
            91, 92, 93, "ManualCampaign", "lumar_general_campaign", "WhatsApp",
            "العنوان", "نص الحملة", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null,
            "+967700000099", "campaign-099", "ManualCampaign", "key-99");

        var headerComponent = WhatsAppCloudMessageChannel.MetaTemplateCatalog.TryCreateHeaderComponent(
            message,
            WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve("lumar_general_campaign", "ManualCampaign")!);

        Assert.Null(headerComponent);
    }

    [Fact]
    public async Task MetaWhatsAppTemplateCatalogClient_UsesRuntimeSettingsAndFetchesOfficialTemplateList()
    {
        var client = new MetaWhatsAppTemplateCatalogClient(
            new FixedHttpClientFactory(new HttpClient(new OfficialMetaTemplateHandler())),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "1413441637074832",
                AccessToken = "runtime-access-token"
            }),
            NullLogger<MetaWhatsAppTemplateCatalogClient>.Instance);

        var templates = await client.GetTemplatesAsync(CancellationToken.None);

        Assert.NotEmpty(templates);
        Assert.Contains(templates, x => x.Name == "lumar_order_created");
        Assert.Contains(templates, x => x.Name == "lumar_order_ready");
        Assert.Contains(templates, x => x.Name == "lumar_order_delivered");
        Assert.Contains(templates, x => x.Name == "lumar_piece_cancelled");
        Assert.Contains(templates, x => x.Name == "lumar_order_cancelled");
        Assert.Contains(templates, x => x.Name == "lumar_referrer_reward");
        Assert.Contains(templates, x => x.Name == "lumar_referrer_reward_relation");
        Assert.Contains(templates, x => x.Name == "lumar_order_created_with_referrer");
        Assert.Contains(templates, x => x.Name == "lumar_order_created_with_referrer_relation");
        Assert.Contains(templates, x => x.Name == "lumar_friday_greeting");
        Assert.DoesNotContain(templates, x => string.Equals(x.Name, "lumar_general_campaign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MetaWhatsAppTemplateCatalogClient_UsesProtectedRuntimeStore_AndCallsOfficialMetagraphEndpoint()
    {
        var previousOverride = Environment.GetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH");
        var tempDirectory = Path.Combine(Path.GetTempPath(), "lumar-whatsapp-runtime-test-" + Guid.NewGuid().ToString("N"));
        var tempStorePath = Path.Combine(tempDirectory, "settings.json");
        Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH", tempStorePath);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:Enabled"] = "true",
                    ["WhatsApp:BaseUrl"] = "https://graph.facebook.com",
                    ["WhatsApp:ApiVersion"] = "v20.0",
                    ["WhatsApp:PhoneNumberId"] = "1413441637074832"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddOptions<WhatsAppOptions>().Configure(options => { });
            services.AddDataProtection()
                .SetApplicationName("LUMAR_ERP_API_V2")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR_ERP_API_V2", "DataProtection-Keys")));

            services.AddSingleton<IWhatsAppConnectionSettingsStore, WhatsAppConnectionSettingsStore>();
            services.AddHttpClient();
            services.AddScoped<IMetaWhatsAppTemplateCatalogClient, MetaWhatsAppTemplateCatalogClient>();

            using var provider = services.BuildServiceProvider();

            var store = provider.GetRequiredService<IWhatsAppConnectionSettingsStore>();
            await store.UpdateAsync(new WhatsAppConnectionSettingsUpdateDto
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "1413441637074832",
                AccessToken = "runtime-access-token",
                TimeoutSeconds = 30
            }, CancellationToken.None);

            var runtime = await store.GetRuntimeSettingsAsync(CancellationToken.None);

            Assert.True(runtime.Enabled);
            Assert.Equal("https://graph.facebook.com", runtime.BaseUrl);
            Assert.Equal("v20.0", runtime.ApiVersion);
            Assert.Equal("1413441637074832", runtime.PhoneNumberId);
            Assert.Equal("runtime-access-token", runtime.AccessToken);

            var client = provider.GetRequiredService<IMetaWhatsAppTemplateCatalogClient>();
            var templates = await client.GetTemplatesAsync(CancellationToken.None);

            Assert.NotNull(templates);
            Assert.True(templates.Count >= 0,
                "The runtime-backed Meta fetch path must return a collection safely without exposing or printing any secret material.");
        }
        finally
        {
            if (previousOverride is null)
            {
                Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH", previousOverride);
            }

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MetaTemplateCatalog_AllOfficialTemplates_ResolveToExactMetaNameLanguageAndParameters()
    {
        var expectations = new[]
        {
            (TemplateKey: "lumar_order_created", MetadataName: "lumar_order_created", Language: "ar", Parameters: new[] { "customer_name", "order_number", "total_amount" }),
            (TemplateKey: "lumar_order_ready", MetadataName: "lumar_order_ready", Language: "ar", Parameters: new[] { "customer_name", "order_number", "delivery_date" }),
            (TemplateKey: "lumar_order_delivered", MetadataName: "lumar_order_delivered", Language: "ar", Parameters: new[] { "customer_name", "order_number", "delivery_date" }),
            (TemplateKey: "lumar_piece_cancelled", MetadataName: "lumar_piece_cancelled", Language: "ar", Parameters: new[] { "customer_name", "order_number", "order_details" }),
            (TemplateKey: "lumar_order_cancelled", MetadataName: "lumar_order_cancelled", Language: "ar", Parameters: new[] { "customer_name", "order_number", "order_details" }),
            (TemplateKey: "lumar_referrer_reward", MetadataName: "lumar_referrer_reward", Language: "ar", Parameters: new[] { "referrer_name", "referrer_code", "referral_points_earned" }),
            (TemplateKey: "lumar_referrer_reward_relation", MetadataName: "lumar_referrer_reward_relation", Language: "ar", Parameters: new[] { "referrer_name", "relationship", "referral_points_earned" }),
            (TemplateKey: "lumar_order_created_with_referrer", MetadataName: "lumar_order_created_with_referrer", Language: "ar", Parameters: new[] { "customer_name", "order_number", "referrer_name" }),
            (TemplateKey: "lumar_order_created_with_referrer_relation", MetadataName: "lumar_order_created_with_referrer_relation", Language: "ar", Parameters: new[] { "customer_name", "order_number", "relationship" }),
            (TemplateKey: "lumar_friday_greeting", MetadataName: "lumar_friday_greeting", Language: "ar", Parameters: new[] { "customer_name", "relation_welcome" }),
            (TemplateKey: "lumar_general_campaign", MetadataName: "lumar_general_campaign", Language: "ar", Parameters: new[] { "customer_name", "campaign_text" })
        };

        foreach (var expectation in expectations)
        {
            var definition = WhatsAppCloudMessageChannel.MetaTemplateCatalog.Resolve(expectation.TemplateKey, expectation.TemplateKey);
            Assert.NotNull(definition);
            Assert.Equal(expectation.MetadataName, definition!.MetaName);
            Assert.Equal(expectation.Language, definition.LanguageCode);
            Assert.Equal(expectation.Parameters, definition.Parameters.Select(p => p.Name).ToArray());
        }
    }

    [Fact]
    public async Task MetaTemplateCatalog_UsesOfficialContextValues_ForAllSupportedTemplates()
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

        var templateCases = new[]
        {
            (TemplateKey: "lumar_order_created", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1001", ["TotalAmount"] = 1250m }, ExpectedValues: new[] { "أحمد", "ORD-1001", "1250" }, NeedsHeader: false),
            (TemplateKey: "lumar_order_ready", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1002", ["DeliveryDate"] = new DateTime(2026, 10, 25) }, ExpectedValues: new[] { "أحمد", "ORD-1002", "2026-10-25" }, NeedsHeader: false),
            (TemplateKey: "lumar_order_delivered", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1003", ["DeliveryDate"] = new DateTime(2026, 10, 26) }, ExpectedValues: new[] { "أحمد", "ORD-1003", "2026-10-26" }, NeedsHeader: false),
            (TemplateKey: "lumar_piece_cancelled", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1004", ["OrderDetails"] = "قميص + بنطال" }, ExpectedValues: new[] { "أحمد", "ORD-1004", "قميص + بنطال" }, NeedsHeader: false),
            (TemplateKey: "lumar_order_cancelled", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1005", ["OrderDetails"] = "إلغاء جزئي" }, ExpectedValues: new[] { "أحمد", "ORD-1005", "إلغاء جزئي" }, NeedsHeader: false),
            (TemplateKey: "lumar_referrer_reward", Context: new Dictionary<string, object?> { ["ReferrerName"] = "سالم", ["ReferrerCode"] = "REF-77", ["ReferralPointsEarned"] = 42m }, ExpectedValues: new[] { "سالم", "REF-77", "42" }, NeedsHeader: false),
            (TemplateKey: "lumar_referrer_reward_relation", Context: new Dictionary<string, object?> { ["ReferrerName"] = "سالم", ["Relationship"] = "أخ", ["ReferralPointsEarned"] = 42m }, ExpectedValues: new[] { "سالم", "أخ", "42" }, NeedsHeader: false),
            (TemplateKey: "lumar_order_created_with_referrer", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1006", ["ReferrerName"] = "سالم" }, ExpectedValues: new[] { "أحمد", "ORD-1006", "سالم" }, NeedsHeader: false),
            (TemplateKey: "lumar_order_created_with_referrer_relation", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["OrderNumber"] = "ORD-1007", ["Relationship"] = "أخ" }, ExpectedValues: new[] { "أحمد", "ORD-1007", "أخ" }, NeedsHeader: false),
            (TemplateKey: "lumar_friday_greeting", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["RelationWelcome"] = "أهلًا بك" }, ExpectedValues: new[] { "أحمد", "أهلًا بك" }, NeedsHeader: false),
            (TemplateKey: "lumar_general_campaign", Context: new Dictionary<string, object?> { ["CustomerName"] = "أحمد", ["CampaignText"] = "عرض خاص لفريقك" }, ExpectedValues: new[] { "أحمد", "عرض خاص لفريقك" }, NeedsHeader: true)
        };

        foreach (var templateCase in templateCases)
        {
            handler.Reset();
            var message = new CustomerMessageDto(
                300 + templateCases.ToList().IndexOf(templateCase), 301, 302, "ManualCampaign", templateCase.TemplateKey, "WhatsApp",
                "العنوان غير الرسمي", "نص بديل غير رسمي", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null,
                "+9677000000" + (templateCases.ToList().IndexOf(templateCase) + 10), "ORD-" + (templateCases.ToList().IndexOf(templateCase) + 1000), "Order", "key-" + (templateCases.ToList().IndexOf(templateCase) + 10),
                templateCase.NeedsHeader ? "https://example.com/campaign.png" : null,
                templateCase.Context);

            var result = await channel.SendAsync(message, CancellationToken.None);
            Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);

            using var payloadDoc = JsonDocument.Parse(handler.LastRequestBody);
            var payloadValues = new List<string>();
            CollectTextValues(payloadDoc.RootElement, payloadValues);

            foreach (var expected in templateCase.ExpectedValues)
            {
                Assert.Contains(expected, payloadValues, StringComparer.Ordinal);
            }

            Assert.DoesNotContain("العنوان غير الرسمي", payloadValues, StringComparer.Ordinal);
            Assert.DoesNotContain("نص بديل غير رسمي", payloadValues, StringComparer.Ordinal);

            if (templateCase.NeedsHeader)
            {
                Assert.Contains("https://example.com/campaign.png", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
            }
        }

        static void CollectTextValues(JsonElement element, List<string> values)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                values.Add(element.GetString() ?? string.Empty);
                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                    CollectTextValues(child, values);
                return;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                    CollectTextValues(property.Value, values);
            }
        }
    }

    [Fact]
    public async Task MetaTemplateCatalog_CampaignImageHeader_IsPersistedAndMappedToWhatsAppHeader()
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

        var message = new CustomerMessageDto(
            500, 501, 502, "ManualCampaign", "lumar_general_campaign", "WhatsApp",
            "العنوان", "تقديم الحملة", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null,
            "+967700000099", "campaign-099", "ManualCampaign", "key-99", "https://cdn.example.com/campaign.png",
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["CampaignText"] = "عرض خاص لفريقك"
            });

        var result = await channel.SendAsync(message, CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Contains("https://cdn.example.com/campaign.png", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"type\":\"header\"", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"type\":\"image\"", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);

        using var payload = JsonDocument.Parse(handler.LastRequestBody);
        var components = payload.RootElement.GetProperty("template").GetProperty("components");
        var bodyParameters = components[1].GetProperty("parameters");

        Assert.Equal(2, bodyParameters.GetArrayLength());
        Assert.Equal("text", bodyParameters[0].GetProperty("type").GetString());
        Assert.Equal("customer_name", bodyParameters[0].GetProperty("parameter_name").GetString());
        Assert.Equal("أحمد", bodyParameters[0].GetProperty("text").GetString());
        Assert.Equal("text", bodyParameters[1].GetProperty("type").GetString());
        Assert.Equal("campaign_text", bodyParameters[1].GetProperty("parameter_name").GetString());
        Assert.Equal("عرض خاص لفريقك", bodyParameters[1].GetProperty("text").GetString());
        Assert.Equal("https://cdn.example.com/campaign.png", components[0].GetProperty("parameters")[0].GetProperty("image").GetProperty("link").GetString());
        Assert.False(bodyParameters[0].TryGetProperty("parameter_name", out var firstParameterName) && string.IsNullOrWhiteSpace(firstParameterName.GetString()));
        Assert.False(bodyParameters[1].TryGetProperty("parameter_name", out var secondParameterName) && string.IsNullOrWhiteSpace(secondParameterName.GetString()));
        Assert.Equal(2, bodyParameters.GetArrayLength());
        Assert.False(components[0].GetProperty("parameters")[0].TryGetProperty("parameter_name", out var headerParameterName));
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_UsesOfficialContextValues_InsteadOfTitleBodyReferenceFallback()
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

        var message = new CustomerMessageDto(
            200, 201, 202, "OrderCreated", "lumar_order_created", "WhatsApp",
            "العنوان غير الرسمي", "نص بديل غير رسمي", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null,
            "+967700000010", "ORD-202", "Order", "key-202", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["CustomerCode"] = "C-202",
                ["OrderNumber"] = "ORD-202",
                ["TotalAmount"] = 1250m,
                ["CurrencyName"] = "الريال اليمني",
                ["PaidAmount"] = 950m,
                ["RemainingAmount"] = 300m,
                ["DeliveryDate"] = new DateTime(2026, 10, 25),
                ["PointsEarned"] = 18m,
                ["PointsBalance"] = 320m,
                ["OrderDetails"] = "قميص + بنطال",
                ["StoreName"] = "LUMAR",
            });

        var result = await channel.SendAsync(message, CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);

        using var payloadDoc = JsonDocument.Parse(handler.LastRequestBody);
        var payloadValues = new List<string>();
        CollectTextValues(payloadDoc.RootElement, payloadValues);

        Assert.Contains("أحمد", payloadValues, StringComparer.Ordinal);
        Assert.Contains("ORD-202", payloadValues, StringComparer.Ordinal);
        Assert.Contains("1250", payloadValues, StringComparer.Ordinal);
        Assert.DoesNotContain("العنوان غير الرسمي", payloadValues, StringComparer.Ordinal);
        Assert.DoesNotContain("نص بديل غير رسمي", payloadValues, StringComparer.Ordinal);

        static void CollectTextValues(JsonElement element, List<string> values)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                values.Add(element.GetString() ?? string.Empty);
                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                    CollectTextValues(child, values);
                return;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                    CollectTextValues(property.Value, values);
            }
        }
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenRequiredParameterIsMissing_StopsBeforeMetaHttpCall()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.fail\"}]}");
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

        var message = new CustomerMessageDto(
            201, 202, 203, "OrderCreated", "lumar_order_created", "WhatsApp",
            "العنوان", "الجسم", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null,
            "+967700000011", "ORD-203", "Order", "key-203", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-203",
                ["PaidAmount"] = 100m,
                ["RemainingAmount"] = 50m,
            });

        var result = await channel.SendAsync(message, CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Contains("Missing Required Meta Parameter", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("total_amount", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(handler.WasCalled);
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
            8, 18, 44, "OrderCreated", "lumar_order_created", "WhatsApp", "العنوان", "تم إنشاء طلبك بنجاح", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000002", "ORD-44", "Order", "key-2", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-44",
                ["TotalAmount"] = 1250m
            }), CancellationToken.None);

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
            8, 18, 44, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000002", "ORD-44", "Order", "key-2", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-44",
                ["DeliveryDate"] = new DateTime(2026, 10, 25)
            }), CancellationToken.None);

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
            9, 19, 45, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000003", "ORD-45", "Order", "key-3", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-45",
                ["DeliveryDate"] = new DateTime(2026, 10, 25)
            }), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Contains("Meta HTTP 200", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaRejectsWithErrorDetails_RecordsSafeMetadata()
    {
        var response = "{\"error\":{\"type\":\"OAuthException\",\"code\":100,\"error_subcode\":400010,\"message\":\"Auth error: AccessToken=SECRET_TOKEN_123 Authorization: Bearer SECRET_BEARER\",\"error_data\":{\"details\":\"Template name does not exist in the translation. Template=general-campaign-smart Language=ar AccessToken=SECRET_TOKEN_123\"}}}";
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
            10, 20, 46, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000004", "ORD-46", "Order", "key-4", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-46",
                ["DeliveryDate"] = new DateTime(2026, 10, 25)
            }), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal(100, result.ProviderErrorCode);
        Assert.Equal(400010, result.ProviderErrorSubcode);
        Assert.Equal("OAuthException", result.SafeProviderErrorType);
        Assert.Contains("Template name does not exist in the translation", result.ProviderErrorDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET_TOKEN_123", result.ProviderErrorDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", result.ProviderErrorDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", result.ProviderErrorDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", result.ProviderErrorDetails, StringComparison.OrdinalIgnoreCase);
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
            11, 21, 47, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000005", "ORD-47", "Order", "key-5", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-47",
                ["DeliveryDate"] = new DateTime(2026, 10, 25)
            }), CancellationToken.None);

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
            12, 22, 48, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+967700000006", "ORD-48", "Order", "key-6", null,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = "أحمد",
                ["OrderNumber"] = "ORD-48",
                ["DeliveryDate"] = new DateTime(2026, 10, 25)
            }), CancellationToken.None);

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

        public void Reset()
        {
            LastRequestBody = string.Empty;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.template\"}]}")
            };
        }
    }

    private sealed class OfficialMetaTemplateHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("runtime-access-token", request.Headers.Authorization?.Parameter);
            Assert.Contains("1413441637074832/message_templates", request.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);

            var payload = new
            {
                data = new[]
                {
                    new { id = "1", name = "lumar_order_created", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "2", name = "lumar_order_ready", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "3", name = "lumar_order_delivered", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "4", name = "lumar_piece_cancelled", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "5", name = "lumar_order_cancelled", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "6", name = "lumar_referrer_reward", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "7", name = "lumar_referrer_reward_relation", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "8", name = "lumar_order_created_with_referrer", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "9", name = "lumar_order_created_with_referrer_relation", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } },
                    new { id = "10", name = "lumar_friday_greeting", status = "APPROVED", language = "ar", category = "UTILITY", components = new[] { new { type = "body" } } }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
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

        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
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
        public Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, string? campaignImageUrl, string? rawCampaignText = null, CancellationToken c = default) => Task.FromResult<(int? MessageId, int? NotificationId)>((null, null));
        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c) => Task.FromResult<int?>(null);
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int? customerMessageId, CancellationToken c) => Task.FromResult(0);
    }
}
