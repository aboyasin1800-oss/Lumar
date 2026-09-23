using System.Text.Json;
using LUMAR_ERP_API_V2.DTOs.Production;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public static class ProductionTrackingEngine
{
    private const string ProductionRoutesSettingKey = "ProductionRoutesConfig";
    private static readonly string[] CanonicalStages =
    [
        "Printing",
        "FabricPrep",
        "Cutting",
        "Sewing",
        "Buttons",
        "Ironing",
        "Quality",
        "Assembly"
    ];

    private static readonly HashSet<string> AllowedStages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Printing",
            "FabricPrep",
            "Cutting",
            "Sewing",
            "Buttons",
            "Ironing",
            "Quality",
            "Assembly"
        };

    public static string ResolveRouteKey(string? pieceType)
    {
        return int.TryParse(pieceType?.Trim(), out var productTypeId) && productTypeId > 0
            ? productTypeId.ToString()
            : string.Empty;
    }

    public static IReadOnlyList<string> GetRoute(int productTypeId) =>
        productTypeId > 0 && ResolveRoutes().TryGetValue(productTypeId.ToString(), out var route)
            ? route
            : Array.Empty<string>();

    public static IReadOnlyList<string> GetRoute(string? pieceType)
    {
        var routeKey = ResolveRouteKey(pieceType);
        return int.TryParse(routeKey, out var productTypeId) ? GetRoute(productTypeId) : Array.Empty<string>();
    }

    public static string? GetNextStage(int productTypeId, string? currentStage) =>
        GetNextStage(productTypeId.ToString(), currentStage);

    public static string? GetNextStage(string? pieceType, string? currentStage)
    {
        var normalizedCurrent = NormalizeStage(currentStage);
        var route = GetRoute(pieceType);
        if (route.Count == 0) return null;

        if (string.IsNullOrWhiteSpace(normalizedCurrent) ||
            string.Equals(normalizedCurrent, "New", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedCurrent, "OrderCreated", StringComparison.OrdinalIgnoreCase))
        {
            return route.FirstOrDefault();
        }

        var validation = ValidateTransition(pieceType, currentStage, null);
        return validation.NextStage;
    }

    public static ProductionTrackingTransitionValidation ValidateTransition(string? pieceType, string? currentStage, string? requestedStage)
    {
        var route = GetRoute(pieceType);
        if (route.Count == 0)
        {
            return new ProductionTrackingTransitionValidation(false, NormalizeStage(currentStage), NormalizeStage(requestedStage), null,
                "No active production route is defined for this piece type.");
        }

        var normalizedCurrent = NormalizeStage(currentStage);
        var normalizedRequested = NormalizeStage(requestedStage);

        if (string.IsNullOrWhiteSpace(normalizedCurrent))
        {
            var firstStage = route.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstStage) && string.Equals(normalizedRequested, firstStage, StringComparison.OrdinalIgnoreCase))
            {
                return new ProductionTrackingTransitionValidation(true, "New", firstStage, route.Count > 1 ? route[1] : null,
                    $"Piece can begin with {firstStage}.");
            }

            return new ProductionTrackingTransitionValidation(false, "New", normalizedRequested, null,
                $"New pieces can only begin at {firstStage}.");
        }

        if (string.Equals(normalizedCurrent, "New", StringComparison.OrdinalIgnoreCase))
        {
            var firstStage = route.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstStage) && string.Equals(normalizedRequested, firstStage, StringComparison.OrdinalIgnoreCase))
            {
                return new ProductionTrackingTransitionValidation(true, "New", firstStage, route.Count > 1 ? route[1] : null,
                    $"{firstStage} is the valid first production stage.");
            }

            return new ProductionTrackingTransitionValidation(false, "New", normalizedRequested, null,
                $"A new piece must start at {firstStage} and follow the active route.");
        }

        if (string.Equals(normalizedCurrent, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            return new ProductionTrackingTransitionValidation(false, normalizedCurrent, normalizedRequested, null,
                "A piece already delivered cannot advance further.");
        }

        var currentIndex = IndexOf(route, normalizedCurrent);
        if (currentIndex < 0)
        {
            var historicalIndex = IndexOf(CanonicalStages, normalizedCurrent);
            if (historicalIndex >= 0)
            {
                var nextActiveStage = route.FirstOrDefault(stage => IndexOf(CanonicalStages, stage) > historicalIndex);
                if (string.IsNullOrWhiteSpace(normalizedRequested))
                {
                    return new ProductionTrackingTransitionValidation(true, normalizedCurrent, null, nextActiveStage,
                        nextActiveStage is null ? "Piece is already at the final active route stage." : $"Next active stage is {nextActiveStage}.");
                }

                if (!string.IsNullOrWhiteSpace(nextActiveStage) && string.Equals(normalizedRequested, nextActiveStage, StringComparison.OrdinalIgnoreCase))
                {
                    var nextIndex = IndexOf(route, nextActiveStage);
                    var nextAfterHistoricalStage = nextIndex + 1 < route.Count ? route[nextIndex + 1] : null;
                    return new ProductionTrackingTransitionValidation(true, normalizedCurrent, normalizedRequested, nextAfterHistoricalStage,
                        $"Transition from historical stage {normalizedCurrent} to {normalizedRequested} is valid.");
                }

                return new ProductionTrackingTransitionValidation(false, normalizedCurrent, normalizedRequested, nextActiveStage,
                    nextActiveStage is null
                        ? "The piece has reached the final active route stage."
                        : $"Only {nextActiveStage} is allowed next for this piece type.");
            }

            return new ProductionTrackingTransitionValidation(false, normalizedCurrent, normalizedRequested, null,
                "The current stage is not part of the valid route for this piece type.");
        }

        if (string.IsNullOrWhiteSpace(normalizedRequested))
        {
            var nextStage = currentIndex + 1 < route.Count ? route[currentIndex + 1] : null;
            return new ProductionTrackingTransitionValidation(true, normalizedCurrent, null, nextStage,
                nextStage is null ? "Piece is already at the final stage." : $"Next valid stage is {nextStage}.");
        }

        if (string.Equals(normalizedCurrent, normalizedRequested, StringComparison.OrdinalIgnoreCase))
        {
            return new ProductionTrackingTransitionValidation(false, normalizedCurrent, normalizedRequested, null,
                "The piece cannot repeat the same production stage without a new valid transition.");
        }

        var requestedIndex = IndexOf(route, normalizedRequested);
        if (requestedIndex < 0)
        {
            return new ProductionTrackingTransitionValidation(false, normalizedCurrent, normalizedRequested, null,
                "The requested stage is not included in the active route for this piece type.");
        }

        if (requestedIndex != currentIndex + 1)
        {
            var nextStage = currentIndex + 1 < route.Count ? route[currentIndex + 1] : null;
            return new ProductionTrackingTransitionValidation(false, normalizedCurrent, normalizedRequested, nextStage,
                nextStage is null
                    ? "The piece has already reached the final route stage and cannot advance further."
                    : $"Only {nextStage} is allowed next for this piece type.");
        }

        var nextStageAfterRequest = requestedIndex + 1 < route.Count ? route[requestedIndex + 1] : null;
        return new ProductionTrackingTransitionValidation(true, normalizedCurrent, normalizedRequested, nextStageAfterRequest,
            $"Transition from {normalizedCurrent} to {normalizedRequested} is valid.");
    }

    public static ProductionTrackingTransitionValidation ValidateTransition(int productTypeId, string? currentStage, string? requestedStage) =>
        ValidateTransition(productTypeId.ToString(), currentStage, requestedStage);

    public static string DetermineOrderStatus(int completedPieceCount, int totalRequiredPieces, int cancelledPieces, bool hasBlockedPieces, string? orderStatus)
    {
        if (hasBlockedPieces)
        {
            return string.IsNullOrWhiteSpace(orderStatus) ? "InProduction" : orderStatus;
        }

        if (cancelledPieces > 0 && completedPieceCount + cancelledPieces >= totalRequiredPieces)
        {
            return "Cancelled";
        }

        if (totalRequiredPieces <= 0)
        {
            return string.IsNullOrWhiteSpace(orderStatus) ? "New" : orderStatus;
        }

        if (completedPieceCount >= totalRequiredPieces && cancelledPieces == 0)
        {
            return "ReadyForDelivery";
        }

        return string.IsNullOrWhiteSpace(orderStatus) ? "InProduction" : orderStatus;
    }

    public static ProductionTrackingAdvanceResultDto AdvancePiece(string? pieceType, string? currentStatus, string? requestedStage, int? pieceId = null, string? trackingCode = null)
    {
        var previousStatus = NormalizeStage(currentStatus) ?? "New";
        var validation = ValidateTransition(pieceType, previousStatus, requestedStage);

        if (!validation.IsAllowed)
        {
            return new ProductionTrackingAdvanceResultDto(pieceId, trackingCode, previousStatus, previousStatus, validation.NextStage,
                validation.Message, false);
        }

        var newStatus = NormalizeStage(requestedStage) ?? previousStatus;
        return new ProductionTrackingAdvanceResultDto(pieceId, trackingCode, previousStatus, newStatus, validation.NextStage,
            validation.Message, true);
    }

    private static IReadOnlyDictionary<string, string[]> ResolveRoutes()
    {
        return TryReadConfiguredRoutes();
    }

    private static IReadOnlyDictionary<string, string[]> TryReadConfiguredRoutes()
    {
        var configValue = ReadSettingValue(ProductionRoutesSettingKey);
        if (string.IsNullOrWhiteSpace(configValue))
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(configValue);
            var routes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var route in ParseRouteEntries(document.RootElement))
            {
                if (!string.IsNullOrWhiteSpace(route.Key))
                {
                    routes[route.Key] = route.Value;
                }
            }

            return routes;
        }
        catch
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<KeyValuePair<string, string[]>> ParseRouteEntries(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var parsed = ParseRouteEntry(item);
                if (!string.IsNullOrWhiteSpace(parsed.Key))
                {
                    yield return parsed;
                }
            }
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("routes", out var routesElement) && routesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in routesElement.EnumerateArray())
                {
                    var parsed = ParseRouteEntry(item);
                    if (!string.IsNullOrWhiteSpace(parsed.Key))
                    {
                        yield return parsed;
                    }
                }
                yield break;
            }

            var single = ParseRouteEntry(root);
            if (!string.IsNullOrWhiteSpace(single.Key))
            {
                yield return single;
            }
        }
    }

    private static KeyValuePair<string, string[]> ParseRouteEntry(JsonElement entry)
    {
        var productTypeId = TryReadInt(entry, "productTypeId", "ProductTypeId");
        if (productTypeId <= 0)
        {
            return new KeyValuePair<string, string[]>(string.Empty, Array.Empty<string>());
        }

        var rawStages = new List<string>();
        if (TryReadArray(entry, out var array, "stages", "Stages", "route", "Route", "routes", "Routes"))
        {
            foreach (var stage in array.EnumerateArray())
            {
                if (stage.ValueKind == JsonValueKind.String)
                {
                    rawStages.Add(stage.GetString() ?? string.Empty);
                }
            }
        }
        else if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var stage in valueElement.EnumerateArray())
            {
                if (stage.ValueKind == JsonValueKind.String)
                {
                    rawStages.Add(stage.GetString() ?? string.Empty);
                }
            }
        }

        if (entry.TryGetProperty("isEnabled", out var enabledElement) &&
            enabledElement.ValueKind == JsonValueKind.False)
        {
            rawStages.Clear();
        }

        var canonicalStages = CanonicalizeStages(rawStages);
        return new KeyValuePair<string, string[]>(productTypeId.ToString(), canonicalStages);
    }

    private static string[] CanonicalizeStages(IReadOnlyList<string> rawStages)
    {
        var unique = rawStages
            .Select(NormalizeStage)
            .OfType<string>()
            .Where(stage => AllowedStages.Contains(stage))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unique.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ordered = new List<string>();
        foreach (var stage in CanonicalStages)
        {
            if (unique.Contains(stage, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(stage);
            }
        }

        if (ordered.Count > 0)
        {
            return ordered.ToArray();
        }

        return unique.ToArray();
    }

    private static bool TryReadArray(JsonElement element, out JsonElement values, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                values = value;
                return true;
            }
        }

        values = default;
        return false;
    }

    private static string? ReadSettingValue(string settingName)
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT TOP (1) SettingValue FROM dbo.System_Settings WITH (NOLOCK) WHERE SettingName = @name", connection);
            command.Parameters.AddWithValue("@name", settingName);
            var value = command.ExecuteScalar();
            return value is null ? null : Convert.ToString(value);
        }
        catch
        {
            return null;
        }
    }

    private static int TryReadInt(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }
        }

        return 0;
    }

    private static string? NormalizeStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return null;

        var trimmed = stage.Trim();
        return trimmed switch
        {
            "New" => "New",
            "OrderCreated" => "New",
            "Printing" => "Printing",
            "FabricPrep" => "FabricPrep",
            "Cutting" => "Cutting",
            "Sewing" => "Sewing",
            "Buttons" => "Buttons",
            "Ironing" => "Ironing",
            "Quality" => "Quality",
            "Assembly" => "Assembly",
            "Ready" => "Ready",
            "Delivery" => "Delivery",
            _ => trimmed
        };
    }

    private static int IndexOf(IReadOnlyList<string> route, string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return -1;
        for (var i = 0; i < route.Count; i++)
        {
            if (string.Equals(route[i], stage, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }
}

public sealed record ProductionTrackingTransitionValidation(
    bool IsAllowed,
    string? CurrentStage,
    string? RequestedStage,
    string? NextStage,
    string Message);
