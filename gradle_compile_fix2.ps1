$ErrorActionPreference = 'Stop'
Set-Location 'D:\LUMAR_MOBILE\android'
$log = 'D:\YASIN\gradle_compile_final_log.txt'
& .\gradlew.bat --stop
Remove-Item 'D:\LUMAR_MOBILE\build\shared_preferences_android\kotlin' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'D:\LUMAR_MOBILE\build\shared_preferences_android\tmp' -Recurse -Force -ErrorAction SilentlyContinue
& .\gradlew.bat --no-daemon --no-build-cache --console=plain -Dkotlin.incremental=false -Dkotlin.incremental.useClasspathSnapshot=false -Dkotlin.compiler.execution.strategy=in-process :shared_preferences_android:compileDebugKotlin *> $log
$exit = $LASTEXITCODE
Write-Host "EXIT:$exit"
if ($exit -ne 0) { throw "Gradle failed with exit code $exit" }
