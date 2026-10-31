$env:LUMAR_LOCAL_GATEWAY_PORT = '5094'
$env:LUMAR_LOCAL_TEST_RECIPIENT = '967775106545'
$env:LUMAR_LOCAL_MEDIA_ORIGIN = 'https://public-backend.example.com'
Set-Location 'd:\YASIN\Gateway\LocalWhatsApp'
node .\src\server.mjs 2>&1 | Tee-Object -Variable out
$LASTEXITCODE | Out-File 'd:\YASIN\temp\node_gateway_exit.txt'
$out | Out-File 'd:\YASIN\temp\node_gateway_stdout.txt'
