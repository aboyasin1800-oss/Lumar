$connectionString = "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION_R2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'InventoryUnitCatalog'"
$tableExists = [int]$cmd.ExecuteScalar()
Write-Output "TABLE_EXISTS=$tableExists"
$cmd.CommandText = "SELECT COUNT(*) FROM dbo.InventoryUnitCatalog"
$rowCount = [int]$cmd.ExecuteScalar()
Write-Output "ROW_COUNT=$rowCount"
if ($rowCount -gt 0) {
    $cmd.CommandText = "SELECT UnitId, Code, NameAr, InchesPerUnit FROM dbo.InventoryUnitCatalog ORDER BY UnitId"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $unitId = $reader.GetInt32(0)
        $code = $reader.GetString(1)
        $name = $reader.GetString(2)
        if ($reader.IsDBNull(3)) { $inches = 'NULL' } else { $inches = $reader.GetDecimal(3).ToString() }
        Write-Output "ROW=UnitId=$unitId;Code=$code;NameAr=$name;Inches=$inches"
    }
    $reader.Close()
}
$conn.Close()
