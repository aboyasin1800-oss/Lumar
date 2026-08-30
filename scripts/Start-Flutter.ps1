$ErrorActionPreference = 'Stop'

$flutter = Join-Path $PSScriptRoot '..\Tools\FlutterSDK\bin\flutter.bat'
$projectPath = Join-Path $PSScriptRoot '..\frontend\tailoring_system'
$apiUrl = if ($env:LUMAR_API_URL) { $env:LUMAR_API_URL } else { 'http://127.0.0.1:5009' }

if (-not (Test-Path $flutter)) { throw "لم يتم العثور على Flutter: $flutter" }
if (-not (Test-Path $projectPath)) { throw "لم يتم العثور على مشروع Flutter: $projectPath" }

Push-Location $projectPath
try {
    & $flutter run -d windows "--dart-define=LUMAR_API_URL=$apiUrl"
} finally {
    Pop-Location
}