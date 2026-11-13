$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$securePath = Join-Path $env:APPDATA 'com.example\tailoring_system\flutter_secure_storage.dat'
$bytes = [System.IO.File]::ReadAllBytes($securePath)
$plain = [System.Text.Encoding]::UTF8.GetString([System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser))
$obj = $plain | ConvertFrom-Json
$token = $obj.lumar_auth_token
if (-not $token) { throw 'NO_AUTH_TOKEN_FOUND' }
$tokenFile = Join-Path $env:TEMP 'lumar_auth_token.txt'
[System.IO.File]::WriteAllText($tokenFile, $token)
Write-Host 'TOKEN_READY=TRUE'
Write-Host 'COMMAND_READY=YES'
& curl.exe -sS -m 300 -D - -H "Authorization: Bearer $token" http://127.0.0.1:5093/settings/test-data-reset/readiness
