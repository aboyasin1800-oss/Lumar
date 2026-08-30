$ErrorActionPreference = 'Stop'

$application = Join-Path $PSScriptRoot '..\frontend\tailoring_system\build\windows\x64\runner\Debug\tailoring_system.exe'
if (-not (Test-Path $application)) {
    throw "لم يتم العثور على نسخة Windows المبنية. شغّل البناء أولاً ثم أعد المحاولة."
}

Start-Process -FilePath $application