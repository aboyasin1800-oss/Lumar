$ErrorActionPreference = 'Stop'
Set-Location 'D:\LUMAR_MOBILE\android'
Remove-Item 'D:\LUMAR_MOBILE\build\shared_preferences_android\kotlin' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'D:\LUMAR_MOBILE\build\shared_preferences_android\tmp' -Recurse -Force -ErrorAction SilentlyContinue
$log = 'D:\YASIN\gradle_compile_log.txt'
& .\gradlew.bat --no-daemon --console=plain :shared_preferences_android:compileDebugKotlin *>$log
$exit = $LASTEXITCODE
Write-Host "EXIT:$exit"
if ($exit -ne 0) { throw "Gradle failed with exit code $exit" }
