$cs = 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;'
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @'
WITH active_matrices AS (
    SELECT CostMatrixId, ProductTypeId,
           ROW_NUMBER() OVER (PARTITION BY ProductTypeId ORDER BY Version DESC, CostMatrixId DESC) AS rn
    FROM dbo.PricingCostMatrices
    WHERE Channel = N'Tailoring' AND Status = N'Active' AND IsActive = 1
), matrix_costs AS (
    SELECT am.ProductTypeId,
           SUM(CASE WHEN ci.Code = N'OP_SEWING' THEN m.UnitCost * m.Quantity ELSE 0 END) AS SewingCost,
           SUM(CASE WHEN ci.Code = N'OP_CONSUMABLES' THEN m.UnitCost * m.Quantity ELSE 0 END) AS ConsumablesCost,
           SUM(CASE WHEN ci.Code = N'OP_IRON_PACK' THEN m.UnitCost * m.Quantity ELSE 0 END) AS IroningCost,
           SUM(CASE WHEN ci.Code = N'OP_FIXED' THEN m.UnitCost * m.Quantity ELSE 0 END) AS FixedCost
    FROM active_matrices am
    INNER JOIN dbo.PricingCostMatrixItems m ON m.CostMatrixId = am.CostMatrixId
    INNER JOIN dbo.PricingCostItems ci ON ci.CostItemId = m.CostItemId AND ci.IsActive = 1
    WHERE am.rn = 1 AND am.ProductTypeId = 4
    GROUP BY am.ProductTypeId
)
SELECT pt.ProductTypeId, pt.Code, pt.NameAr,
       mc.SewingCost, mc.ConsumablesCost, mc.IroningCost, mc.FixedCost,
       (mc.SewingCost + mc.ConsumablesCost + mc.IroningCost + mc.FixedCost) AS TotalOperationalCost,
       cm.CostMatrixId, cm.Version, cm.Status, cm.IsActive, cm.EffectiveFrom, cm.EffectiveTo
FROM dbo.PricingProductTypes pt
LEFT JOIN matrix_costs mc ON mc.ProductTypeId = pt.ProductTypeId
LEFT JOIN dbo.PricingCostMatrices cm ON cm.ProductTypeId = pt.ProductTypeId AND cm.Channel = N'Tailoring' AND cm.Status = N'Active' AND cm.IsActive = 1
WHERE pt.ProductTypeId = 4
ORDER BY cm.Version DESC, cm.CostMatrixId DESC;
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
