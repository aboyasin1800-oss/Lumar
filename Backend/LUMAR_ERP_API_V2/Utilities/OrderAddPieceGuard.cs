namespace LUMAR_ERP_API_V2.Utilities;

public static class OrderAddPieceGuard
{
    public static bool CanAddPiece(string? orderStatus, bool hasAnyTrackingEvent)
    {
        return string.Equals(orderStatus, "New", StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, "InProduction", StringComparison.OrdinalIgnoreCase);
    }
}
