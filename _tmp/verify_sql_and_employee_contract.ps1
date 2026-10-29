$ErrorActionPreference = 'Stop'

$service = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
if ($null -eq $service) {
    throw 'SQL Server service not found: MSSQL$SQLEXPRESS'
}

if ($service.Status -ne 'Running') {
    Write-Output "SERVICE_STATUS_BEFORE=$($service.Status)"
    Start-Service -Name $service.Name
    $service = Get-Service -Name $service.Name
}

Write-Output "SERVICE_STATUS=$($service.Status)"

$conn = New-Object System.Data.SqlClient.SqlConnection('Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;')
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT DB_NAME() AS DatabaseName;'
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Output "DATABASE_NAME=$($reader['DatabaseName'])"
    }
    $reader.Close()
}
finally {
    $conn.Close()
}

Set-Location 'd:\YASIN\Backend\LUMAR_ERP_API_V2.Tests'
dotnet test LUMAR_ERP_API_V2.Tests.csproj --no-restore --filter 'FullyQualifiedName~EmployeeScannerContractTests|FullyQualifiedName~EmployeeWriteInfrastructureTests' --logger 'console;verbosity=minimal'
$exitCode = $LASTEXITCODE
Write-Output "EXIT_CODE=$exitCode"
exit $exitCode
