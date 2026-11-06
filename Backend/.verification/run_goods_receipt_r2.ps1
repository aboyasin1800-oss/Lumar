Set-Location 'D:\YASIN\Backend'
& dotnet test "LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj" --filter "FullyQualifiedName~GoodsReceiptRuntimeIntegrationTests" --logger "trx;LogFileName=goods_receipt_r2.trx" --results-directory ".verification" --nologo -v minimal 2>&1 | Tee-Object -FilePath '.verification\goods_receipt_r2.txt'
$code = $LASTEXITCODE
Write-Output "EXIT_CODE=$code"
exit $code
