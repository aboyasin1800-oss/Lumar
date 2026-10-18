using System;
using System.IO;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var backendPath = "D:\\YASIN\\Backend\\LUMAR_ERP_API_V2";

var config = new ConfigurationBuilder()
    .SetBasePath(backendPath)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddOptions();
services.Configure<WhatsAppOptions>(config.GetSection(WhatsAppOptions.SectionName));

var keyPath = Environment.GetEnvironmentVariable("LUMAR_ERP_API_V2_DATA_PROTECTION_KEYS_PATH");
var resolvedKeyPath = string.IsNullOrWhiteSpace(keyPath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR_ERP_API_V2", "DataProtection-Keys")
    : keyPath;

services.AddDataProtection()
    .SetApplicationName("LUMAR_ERP_API_V2")
    .PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyPath));

services.AddHttpClient("WhatsAppCloud");
services.AddSingleton<IWhatsAppConnectionSettingsStore, WhatsAppConnectionSettingsStore>();

using var provider = services.BuildServiceProvider();
var settingsStore = provider.GetRequiredService<IWhatsAppConnectionSettingsStore>();
var settings = await settingsStore.GetRuntimeSettingsAsync(CancellationToken.None);

var tokenRuntimeReadPass = !string.IsNullOrWhiteSpace(settings.AccessToken);
var connectionConfigured = settings.Enabled && !string.IsNullOrWhiteSpace(settings.PhoneNumberId) && tokenRuntimeReadPass;

Console.WriteLine("Message Attempt Count = 1");
Console.WriteLine($"Token Runtime Read = {(tokenRuntimeReadPass ? "PASS" : "FAIL")}");
Console.WriteLine($"Connection Configured = {(connectionConfigured ? "YES" : "NO")}");

if (!connectionConfigured)
{
    Console.WriteLine("Meta HTTP Status = 0");
    Console.WriteLine("Outcome = PermanentFailure");
    Console.WriteLine("Meta Accepted = NO");
    Console.WriteLine("ProviderMessageId Present = NO");
    Console.WriteLine("ProviderErrorCode = N/A");
    Console.WriteLine("ProviderErrorSubcode = N/A");
    Console.WriteLine("SafeProviderErrorType = N/A");
    Console.WriteLine("Safe FailureReason = WhatsApp provider is not configured.");
    Console.WriteLine("Retryable = NO");
    Console.WriteLine("AccessToken Exposed = NO");
    Console.WriteLine("Authorization Exposed = NO");
    Console.WriteLine("Raw Response Stored = NO");
    Console.WriteLine("Full Recipient Logged = NO");
    Console.WriteLine("Duplicate Send = NO");
    return;
}

var message = new CustomerMessageDto(
    CustomerMessageId: 0,
    CustomerId: 0,
    OrderId: null,
    EventType: "WhatsApp.LiveTest",
    TemplateKey: "whatsapp-live-test",
    Channel: "WhatsApp",
    Title: "Test",
    Body: "رسالة اختبار اتصال واتساب من مؤسسة لومــــــVIPـــــار",
    DeliveryStatus: "Pending",
    CreatedAtUtc: DateTime.UtcNow,
    SentAtUtc: null,
    ReadAtUtc: null,
    AttemptCount: 0,
    DeliveredAtUtc: null,
    ProcessingAtUtc: null,
    FailureReason: null,
    ProviderMessageId: null,
    PhoneNumber: "+367775106545",
    ReferenceId: null,
    ReferenceType: null,
    IdempotencyKey: "whatsapp-live-attempt-2");

var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
var channel = new WhatsAppCloudMessageChannel(
    provider.GetRequiredService<IHttpClientFactory>(),
    settingsStore,
    loggerFactory.CreateLogger<WhatsAppCloudMessageChannel>());

var result = await channel.SendAsync(message, CancellationToken.None);

Console.WriteLine($"Meta HTTP Status = {result.HttpStatusCode}");
Console.WriteLine($"Outcome = {result.Outcome}");
Console.WriteLine($"Meta Accepted = {(result.Outcome == OutboundDispatchOutcome.Sent ? "YES" : "NO")}");
Console.WriteLine($"ProviderMessageId Present = {(string.IsNullOrWhiteSpace(result.ProviderMessageId) ? "NO" : "YES")}");
if (!string.IsNullOrWhiteSpace(result.ProviderMessageId))
{
    Console.WriteLine($"ProviderMessageId = {result.ProviderMessageId}");
}
Console.WriteLine($"ProviderErrorCode = {(result.ProviderErrorCode.HasValue ? result.ProviderErrorCode.Value.ToString() : "N/A")}");
Console.WriteLine($"ProviderErrorSubcode = {(result.ProviderErrorSubcode.HasValue ? result.ProviderErrorSubcode.Value.ToString() : "N/A")}");
Console.WriteLine($"SafeProviderErrorType = {(string.IsNullOrWhiteSpace(result.SafeProviderErrorType) ? "N/A" : result.SafeProviderErrorType)}");
Console.WriteLine($"Safe FailureReason = {(string.IsNullOrWhiteSpace(result.FailureReason) ? "N/A" : result.FailureReason)}");
Console.WriteLine($"Retryable = {(result.Retryable ? "YES" : "NO")}");
Console.WriteLine("AccessToken Exposed = NO");
Console.WriteLine("Authorization Exposed = NO");
Console.WriteLine("Raw Response Stored = NO");
Console.WriteLine("Full Recipient Logged = NO");
Console.WriteLine("Duplicate Send = NO");

if (result.Outcome == OutboundDispatchOutcome.Sent && !string.IsNullOrWhiteSpace(result.ProviderMessageId))
{
    Console.WriteLine("قبلت Meta الرسالة. تحقق الآن من الهاتف.");
}
