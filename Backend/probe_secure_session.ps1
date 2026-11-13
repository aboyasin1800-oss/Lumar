$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$securePath = Join-Path $env:APPDATA 'com.example\tailoring_system\flutter_secure_storage.dat'
Write-Host "SECURE_PATH=$securePath"
if (-not (Test-Path $securePath)) {
  Write-Host 'SECURE_FILE_MISSING'
  exit 2
}
$bytes = [System.IO.File]::ReadAllBytes($securePath)
Write-Host "RAW_BYTES=$($bytes.Length)"
try {
  $plain = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
  Write-Host "DECRYPTED_BYTES=$($plain.Length)"
  $text = [System.Text.Encoding]::UTF8.GetString($plain)
  Write-Host "TEXT_LENGTH=$($text.Length)"
  if ($text.Length -gt 0) {
    Write-Host "TEXT_PREFIX=$($text.Substring(0, [Math]::Min(200, $text.Length)))"
  }
} catch {
  Write-Host "DECRYPT_FAILED=$($_.Exception.Message)"
  exit 3
}
