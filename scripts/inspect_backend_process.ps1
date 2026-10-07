$proc = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = '22168'"
if ($null -eq $proc) {
    Write-Output 'NO_PROCESS_FOUND'
    exit
}
Write-Output "PROCESS_NAME=$($proc.Name)"
Write-Output "COMMAND_LINE=$($proc.CommandLine)"
Write-Output "EXECUTABLE_PATH=$($proc.ExecutablePath)"
