$cs = 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;'
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @'
SELECT
    o.OrderID,
    o.OrderNumber,
    oi.OrderItemID,
    oi.ProductTypeId,
    pt.Code AS ProductTypeCode,
    pt.NameAr AS ProductTypeName,
    p.PieceID,
    p.TrackingCode,
    p.PieceNumber,
    f.FabricCode,
    f.FabricType,
    f.FabricColor,
    f.Quantity,
    f.ConsumedQuantity,
    f.UnitCost AS OrderItemFabricUnitCost,
    f.TotalCost AS OrderItemFabricTotalCost,
    JSON_VALUE(sv.OriginalPricingSnapshot, '$.FullCostPerPiece') AS OriginalFullCostPerPiece,
    JSON_VALUE(sv.OriginalPricingSnapshot, '$.OperationalCostPerPiece') AS OriginalOperationalCostPerPiece,
    JSON_VALUE(sv.OriginalPricingSnapshot, '$.FabricCostPerPiece') AS OriginalFabricCostPerPiece,
    JSON_VALUE(sv.OriginalPricingSnapshot, '$.FullCostTotal') AS OriginalFullCostTotal,
    JSON_VALUE(sv.ApprovedPricingSnapshot, '$.FullCostPerPiece') AS ApprovedFullCostPerPiece,
    JSON_VALUE(sv.ApprovedPricingSnapshot, '$.OperationalCostPerPiece') AS ApprovedOperationalCostPerPiece,
    JSON_VALUE(sv.ApprovedPricingSnapshot, '$.FabricCostPerPiece') AS ApprovedFabricCostPerPiece,
    sv.OriginalPricingSnapshot,
    sv.ApprovedPricingSnapshot
FROM dbo.Orders o
JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
LEFT JOIN dbo.PricingProductTypes pt ON pt.ProductTypeId = oi.ProductTypeId
LEFT JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
LEFT JOIN dbo.OrderItemFabrics f ON f.OrderItemID = oi.OrderItemID
LEFT JOIN dbo.TailoringPieceSaleValues sv ON sv.PieceID = p.PieceID
WHERE o.OrderID = 1581
ORDER BY oi.OrderItemID, p.PieceNumber;
'@
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    $parts = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $r.FieldCount; $i++) {
        $v = $r.GetValue($i)
        if ($null -eq $v) { $v = '' }
        $parts.Add([string]$v)
    }
    Write-Output ($parts -join ' | ')
}
$r.Close()
$conn.Close()
