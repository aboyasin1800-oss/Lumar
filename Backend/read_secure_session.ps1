$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$securePath = Join-Path $env:APPDATA 'com.example\tailoring_system\flutter_secure_storage.dat'
$bytes = [System.IO.File]::ReadAllBytes($securePath)
$plain = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$text = [System.Text.Encoding]::UTF8.GetString($plain)
$obj = $text | ConvertFrom-Json
$keys = $obj.psobject.Properties.Name
Write-Host "KEY_COUNT=$($keys.Count)"
$tokenKeyExists = $false
foreach ($k in $keys) {
  if ($k -match 'token|auth|username|remembered') {
    $v = $obj.$k
    if ($k -eq 'lumar_auth_token') {
      $tokenKeyExists = $true
      Write-Host "TOKEN_PRESENT=TRUE"
      Write-Host "TOKEN_LENGTH=$($v.ToString().Length)"
    } else {
      Write-Host "KEY=$k"
      Write-Host "VALUE_LENGTH=$($v.ToString().Length)"
    }
  }
}
if (-not $tokenKeyExists) { Write-Host 'TOKEN_PRESENT=FALSE' }
