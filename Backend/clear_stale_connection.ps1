[Environment]::SetEnvironmentVariable('Lumar__ConnectionString', '', 'User')
$user = [Environment]::GetEnvironmentVariable('Lumar__ConnectionString', 'User')
$process = [Environment]::GetEnvironmentVariable('Lumar__ConnectionString', 'Process')
$machine = [Environment]::GetEnvironmentVariable('Lumar__ConnectionString', 'Machine')

if ($user) { 'USER_SCOPE=' + $user } else { 'USER_SCOPE=<NULL>' }
if ($process) { 'PROCESS_SCOPE=' + $process } else { 'PROCESS_SCOPE=<NULL>' }
if ($machine) { 'MACHINE_SCOPE=' + $machine } else { 'MACHINE_SCOPE=<NULL>' }
