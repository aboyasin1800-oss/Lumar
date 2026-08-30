$ErrorActionPreference = 'Stop'

$startScript = Join-Path $PSScriptRoot 'Start-Flutter.ps1'
Get-Process -Name tailoring_system -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process powershell.exe -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $startScript