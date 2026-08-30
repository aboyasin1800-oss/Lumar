$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot '..\Backend\LUMAR_ERP_API_V2\LUMAR_ERP_API_V2.csproj'
if (-not (Test-Path $projectPath)) {
    throw "لم يتم العثور على مشروع Backend: $projectPath"
}

& dotnet run --project $projectPath