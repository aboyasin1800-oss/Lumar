using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class OrderTrackingRuntimeVerificationTests : IDisposable
{
    private const string ServerUrl = "http://127.0.0.1:5011";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Process _apiProcess;
    private readonly HttpClient _httpClient;

    public OrderTrackingRuntimeVerificationTests()
    {
        var connectionString = GetValidationConnectionString();
        _apiProcess = StartApi(connectionString);
        _httpClient = new HttpClient { BaseAddress = new Uri(ServerUrl) };
        WaitForApiReadyAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        if (!_apiProcess.HasExited)
        {
            _apiProcess.Kill(entireProcessTree: true);
            _apiProcess.WaitForExit(5000);
        }
        _apiProcess.Dispose();
    }

    [Fact]
    public async Task Order_tracking_runtime_contract_matches_live_validation_data()
    {
        var missingOrderId = await ReadScalarAsync<int>("SELECT ISNULL(MAX(OrderID), 0) + 999999 AS MissingOrderId FROM dbo.Orders;");
        var zeroPieceOrderId = await ReadScalarAsync<int?>(@"
            SELECT TOP (1) o.OrderID
            FROM dbo.Orders o
            LEFT JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
            LEFT JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
            GROUP BY o.OrderID
            HAVING COUNT(p.PieceID) = 0
            ORDER BY o.OrderID;");
        var singlePieceOrderId = await ReadScalarAsync<int?>(@"
            SELECT TOP (1) o.OrderID
            FROM dbo.Orders o
            LEFT JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
            LEFT JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
            GROUP BY o.OrderID
            HAVING COUNT(p.PieceID) = 1
            ORDER BY o.OrderID;");
        var multiPieceOrderId = await ReadScalarAsync<int?>(@"
            SELECT TOP (1) o.OrderID
            FROM dbo.Orders o
            LEFT JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
            LEFT JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
            GROUP BY o.OrderID
            HAVING COUNT(p.PieceID) > 1
            ORDER BY o.OrderID;");
        var multiEventOrderId = await ReadScalarAsync<int?>(@"
            SELECT TOP (1) o.OrderID
            FROM dbo.Orders o
            INNER JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
            INNER JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
            INNER JOIN dbo.TrackingEvents te ON te.PieceID = p.PieceID
            GROUP BY o.OrderID
            HAVING COUNT(DISTINCT te.TrackingEventID) > 1
            ORDER BY o.OrderID;");
        var readyForDeliveryOrderId = await ReadScalarAsync<int?>(@"
            SELECT TOP (1) o.OrderID
            FROM dbo.Orders o
            INNER JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
            INNER JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
            WHERE p.PieceStatus IN ('ReadyForDelivery', 'Delivered')
            GROUP BY o.OrderID
            ORDER BY o.OrderID;");

        Assert.True(missingOrderId > 0, "A missing order id should be available for 404 validation.");

        var missingResponse = await _httpClient.GetAsync($"/orders/{missingOrderId}/tracking");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        if (zeroPieceOrderId is not null)
        {
            var zeroPieceResponse = await _httpClient.GetAsync($"/orders/{zeroPieceOrderId}/tracking");
            Assert.Equal(HttpStatusCode.OK, zeroPieceResponse.StatusCode);
            var zeroPiecePayload = JsonDocument.Parse(await zeroPieceResponse.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Array, zeroPiecePayload.RootElement.ValueKind);
            Assert.Empty(zeroPiecePayload.RootElement.EnumerateArray());
        }
        else
        {
            Console.WriteLine("No order without pieces exists in LUMAR_ERP_ES_VALIDATION; zero-piece contract is documented as not observed in the live dataset.");
        }

        if (singlePieceOrderId is not null)
        {
            await VerifyLiveContractAsync(singlePieceOrderId.Value, expectMultiple: false, expectReady: false, expectedMinimumPieceCount: 1);
        }
        else
        {
            Console.WriteLine("No single-piece order exists in LUMAR_ERP_ES_VALIDATION; single-piece contract is documented as not observed in the live dataset.");
        }

        if (multiPieceOrderId is not null)
        {
            await VerifyLiveContractAsync(multiPieceOrderId.Value, expectMultiple: true, expectReady: false, expectedMinimumPieceCount: 2);
        }
        else
        {
            Console.WriteLine("No multi-piece order exists in LUMAR_ERP_ES_VALIDATION; multi-piece contract is documented as not observed in the live dataset.");
        }

        if (multiEventOrderId is not null)
        {
            await VerifyLiveContractAsync(multiEventOrderId.Value, expectMultiple: true, expectReady: false, expectedMinimumPieceCount: 1);
        }
        else
        {
            Console.WriteLine("No order with multiple tracking events exists in LUMAR_ERP_ES_VALIDATION; timing and event-order checks are documented as not observed in the live dataset.");
        }

        if (readyForDeliveryOrderId is not null)
        {
            await VerifyLiveContractAsync(readyForDeliveryOrderId.Value, expectMultiple: false, expectReady: true, expectedMinimumPieceCount: 1);
        }
        else
        {
            Console.WriteLine("No ready-for-delivery order exists in LUMAR_ERP_ES_VALIDATION; delivery-readiness contract is documented as not observed in the live dataset.");
        }
    }

    private async Task VerifyLiveContractAsync(int orderId, bool expectMultiple, bool expectReady, int expectedMinimumPieceCount)
    {
        var response = await _httpClient.GetAsync($"/orders/{orderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, payload.RootElement.ValueKind);

        var items = payload.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.True(items.Count >= expectedMinimumPieceCount, $"Order {orderId} returned less than expected tracking entries.");

        var pieceCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var eventTimes = new List<DateTime>();

        foreach (var item in items)
        {
            var trackingCode = item.TryGetProperty("trackingCode", out var codeProp) ? codeProp.GetString() : null;
            var currentStage = item.TryGetProperty("currentStage", out var currentProp) ? currentProp.GetString() : null;
            var nextStage = item.TryGetProperty("nextStage", out var nextProp) ? nextProp.GetString() : null;
            var status = item.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            var readyForDelivery = item.TryGetProperty("readyForDelivery", out var readyProp) && readyProp.ValueKind == JsonValueKind.True;
            var isDelivered = item.TryGetProperty("isDelivered", out var deliveredProp) && deliveredProp.ValueKind == JsonValueKind.True;

            Assert.False(string.IsNullOrWhiteSpace(trackingCode), $"TrackingCode must be a non-empty string for order {orderId}.");
            Assert.True(pieceCodes.Add(trackingCode!), $"Duplicate tracking code {trackingCode} returned for order {orderId}.");

            if (expectReady)
            {
                Assert.True(readyForDelivery || isDelivered || string.Equals(status, "ReadyForDelivery", StringComparison.OrdinalIgnoreCase),
                    $"Ready-for-delivery order {orderId} must expose delivery readiness in the tracking contract.");
            }

            if (item.TryGetProperty("cuttingDate", out var cuttingDateProp) && cuttingDateProp.ValueKind != JsonValueKind.Null)
                eventTimes.Add(cuttingDateProp.GetDateTime());
            if (item.TryGetProperty("sewingDate", out var sewingDateProp) && sewingDateProp.ValueKind != JsonValueKind.Null)
                eventTimes.Add(sewingDateProp.GetDateTime());
            if (item.TryGetProperty("ironingDate", out var ironingDateProp) && ironingDateProp.ValueKind != JsonValueKind.Null)
                eventTimes.Add(ironingDateProp.GetDateTime());

            if (expectMultiple)
            {
                Assert.NotNull(currentStage);
                if (!string.IsNullOrWhiteSpace(nextStage))
                    Assert.False(string.Equals(currentStage, nextStage, StringComparison.OrdinalIgnoreCase), $"Current stage and next stage should differ for order {orderId}.");
            }
        }

        if (eventTimes.Count > 1)
        {
            var ordered = eventTimes.OrderBy(value => value).ToList();
            Assert.True(ordered.SequenceEqual(eventTimes), $"Tracking event dates for order {orderId} are not ordered chronologically.");
        }
    }

    private static Process StartApi(string connectionString)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project \"D:\\YASIN\\Backend\\LUMAR_ERP_API_V2\\LUMAR_ERP_API_V2.csproj\" --no-launch-profile --urls http://127.0.0.1:5011",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = "D:\\YASIN"
        };
        psi.Environment["Lumar__ConnectionString"] = connectionString;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        return Process.Start(psi)!;
    }

    private static async Task WaitForApiReadyAsync()
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(ServerUrl) };
                var response = await client.GetAsync("/swagger/index.html");
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect)
                    return;
            }
            catch
            {
                // retry while startup is still binding the local port.
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("The local API did not become ready within the expected startup window.");
    }

    private static string GetValidationConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Order tracking runtime verification requires LUMAR_ERP_ES_VALIDATION.");
        return builder.ConnectionString;
    }

    private static async Task<T?> ReadScalarAsync<T>(string sql)
    {
        var connectionString = GetValidationConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        if (result is null || result is DBNull)
            return default;
        if (typeof(T) == typeof(int?))
            return (T?)(object?)Convert.ToInt32(result);
        return (T)Convert.ChangeType(result, typeof(T));
    }
}
