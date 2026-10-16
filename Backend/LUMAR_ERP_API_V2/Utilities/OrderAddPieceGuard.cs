namespace LUMAR_ERP_API_V2.Utilities;

public static class OrderAddPieceGuard
{
    public static bool CanAddPiece(string? orderStatus, bool hasAnyTrackingEvent)
    {
        if (hasAnyTrackingEvent)
            return false;

        return string.Equals(orderStatus, "New", StringComparison.OrdinalIgnoreCase);
    }
}
