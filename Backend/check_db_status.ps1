$conn = New-Object System.Data.SqlClient.SqlConnection('Data Source=YASIN-YASIN\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT name, state_desc FROM sys.databases WHERE name IN ('LUMAR_ERP_VIP_1','LUMAR_ERP_GOLDEN_CLEAN_R2','LUMAR_ERP_ES_VALIDATION') ORDER BY name;"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader[0] + ' | ' + $reader[1])
}
$reader.Close()
$conn.Close()
