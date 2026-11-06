$connectionString = 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION_R2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = 'SELECT UnitId, Code, NameAr, InchesPerUnit FROM dbo.InventoryUnitCatalog ORDER BY UnitId'
$rdr = $cmd.ExecuteReader()
while ($rdr.Read()) {
    $unitId = $rdr.GetInt16(0)
    $code = $rdr.GetString(1)
    $nameAr = $rdr.GetString(2)
    $inches = $rdr.IsDBNull(3)
    if (-not $inches) { $inches = [string]$rdr.GetDecimal(3) } else { $inches = 'NULL' }
    Write-Output ("UNIT_ID={0};UNIT_CODE={1};UNIT_NAME={2};INCHES={3}" -f $unitId, $code, $nameAr, $inches)
}
$rdr.Close()
$conn.Close()
