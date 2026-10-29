$cs = $env:Lumar__ConnectionString
if (-not $cs) { $cs = [Environment]::GetEnvironmentVariable('Lumar__ConnectionString') }
if (-not $cs) { throw 'Connection string missing' }

$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @'
SELECT TOP 5 ID, TrackingCode, ScanTime, JSONData
FROM dbo.Live_Scan
ORDER BY ScanTime DESC;

SELECT TOP 20 EmployeeId, EmployeeCode, EmployeeName, JobTitle, SalaryType, IsActive
FROM dbo.Employees
ORDER BY EmployeeId DESC;

SELECT TOP 20 EmployeePieceRateAssignmentId, EmployeeId, PieceType, Stage, Rate, Extra, IsActive, EffectiveFrom, EffectiveTo
FROM dbo.EmployeePieceRateAssignments
ORDER BY EmployeeId, PieceType, Stage, EffectiveFrom DESC;
'@
$r = $cmd.ExecuteReader()
$table = 0
while ($table -lt 3) {
    Write-Host "TABLE:$table"
    while ($r.Read()) {
        $vals = @()
        for ($i = 0; $i -lt $r.FieldCount; $i++) {
            $name = $r.GetName($i)
            $val = $r.IsDBNull($i) ? '[NULL]' : $r.GetValue($i)
            $vals += "$name=$val"
        }
        Write-Host ($vals -join '; ')
    }
    $r.NextResult()
    $table++
}
$conn.Close()
