namespace LUMAR_ERP_API_V2.Utilities;

using LUMAR_ERP_API_V2.DTOs.Loyalty;

public static class PiecePointsEngine
{
    public static decimal CalculateForItem(
        LoyaltyPiecePointSettingDto? setting,
        decimal quantity,
        decimal fallbackPointsPerPiece,
        bool programEnabled = true)
    {
        if (!programEnabled || quantity <= 0m) return 0m;
        var pointsPerPiece = setting?.IsActive == true ? setting.Points : 0m;
        return pointsPerPiece * quantity;
    }

    public static decimal CalculateBuyerEarn(decimal basePoints, string pieceCode, decimal quantity, decimal fallbackPointsPerPiece)
    {
        return basePoints > 0m && quantity > 0m ? basePoints : 0m;
    }
}

public static class ReferralRewardEngine
{
    private static readonly decimal[] Levels = [0.50m, 0.25m, 0.125m, 0.0625m];

    public static IReadOnlyList<decimal> CalculateLevels(decimal basePoints, params int[] levels)
    {
        var values = new List<decimal>();
        var selected = levels.Length == 0 ? new[] { 1, 2, 3, 4 } : levels;
        foreach (var level in selected)
        {
            var percentage = level switch
            {
                1 => 0.50m,
                2 => 0.25m,
                3 => 0.125m,
                4 => 0.0625m,
                _ => 0m
            };
            values.Add(basePoints * percentage);
        }

        return values;
    }

    public static string CreateFingerprint(int referrerCustomerId, int referredCustomerId, int orderId, int level, string source, string engineVersion, string orderItemsFingerprint)
    {
        return $"{referrerCustomerId}|{referredCustomerId}|{orderId}|{level}|{source}|{engineVersion}|{orderItemsFingerprint}";
    }

    public static bool IsDuplicate(string fingerprint, string candidate)
    {
        return string.Equals(fingerprint, candidate, StringComparison.Ordinal);
    }
}
