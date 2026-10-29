$ErrorActionPreference = 'Stop'
$env:Lumar__ConnectionString = 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
Set-Location 'd:\YASIN\Backend\LUMAR_ERP_API_V2.Tests'

dotnet test LUMAR_ERP_API_V2.Tests.csproj --no-restore --filter 'FullyQualifiedName~EmployeeScannerContractTests|FullyQualifiedName~EmployeeWriteInfrastructureTests' --logger 'trx;LogFileName=employee-contract-tests.trx' --results-directory 'd:\YASIN\_tmp\test-results'
$exitCode = $LASTEXITCODE
Write-Output "EXIT_CODE=$exitCode"
exit $exitCode
