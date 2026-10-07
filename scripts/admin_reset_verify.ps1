$ErrorActionPreference = 'Stop'

$adminUser = 'admin'
$newPassword = 'Admin#Reset2026!Q'

function Get-Pbkdf2StoredValue {
    param(
        [string]$PlainText,
        [byte[]]$Salt
    )

    $kdf = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
        [System.Text.Encoding]::UTF8.GetBytes($PlainText),
        $Salt,
        210000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256
    )

    $hashBytes = $kdf.GetBytes(32)
    return 'PBKDF2$210000$' + [Convert]::ToBase64String($Salt) + '$' + [Convert]::ToBase64String($hashBytes)
}

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$salt = New-Object byte[] 16
$rng.GetBytes($salt)
$storedHash = Get-Pbkdf2StoredValue -PlainText $newPassword -Salt $salt

$conn = New-Object System.Data.SqlClient.SqlConnection 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "UPDATE dbo.Users SET PasswordHash=@hash, UserPassword='', SecurityStamp=NEWID() WHERE Username=@user AND UserRole='Admin' AND IsActive=1; SELECT @@ROWCOUNT;"
    $cmd.Parameters.Add('@hash', [System.Data.SqlDbType]::NVarChar, 4000).Value = $storedHash
    $cmd.Parameters.Add('@user', [System.Data.SqlDbType]::NVarChar, 50).Value = $adminUser
    $rows = $cmd.ExecuteScalar()
    Write-Output "ADMIN_RESET_ROWS=$rows"
} finally {
    $conn.Close()
}

# verify DB match without exposing hash or password
$query = @'
SELECT TOP 1 UserId, Username, UserRole, IsActive, PasswordHash FROM dbo.Users WHERE Username='admin';
'@

$readConn = New-Object System.Data.SqlClient.SqlConnection 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
$readConn.Open()
try {
    $readCmd = $readConn.CreateCommand()
    $readCmd.CommandText = $query
    $reader = $readCmd.ExecuteReader()
    while ($reader.Read()) {
        $role = $reader.GetString(2)
        $isActive = $reader.GetBoolean(3)
        $storedFromDb = $reader.GetString(4)
        $parts = $storedFromDb.Split('$')
        $saltFromDb = [Convert]::FromBase64String($parts[2])
        $expected = Get-Pbkdf2StoredValue -PlainText $newPassword -Salt $saltFromDb
        $matched = $storedFromDb -eq $expected
        Write-Output "ADMIN_ROLE=$role"
        Write-Output "ADMIN_ACTIVE=$isActive"
        Write-Output "ADMIN_MATCHED=$matched"
    }
    $reader.Close()
} finally {
    $readConn.Close()
}

$baseUrl = 'http://127.0.0.1:5093'
$loginBody = @{ username = $adminUser; password = $newPassword } | ConvertTo-Json
$loginStatus = 0
$token = $null
try {
    $loginResponse = Invoke-WebRequest -Uri "$baseUrl/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody -UseBasicParsing
    $loginStatus = [int]$loginResponse.StatusCode
    $token = ($loginResponse.Content | ConvertFrom-Json).token
} catch {
    $loginStatus = [int]$_.Exception.Response.StatusCode
    if ($_.Exception.Response) {
        $loginStatus = [int]$_.Exception.Response.StatusCode
    }
}
Write-Output "LOGIN_STATUS=$loginStatus"
Write-Output "TOKEN_PRESENT=$($null -ne $token -and $token.Length -gt 20)"

if ($null -ne $token -and $token.Length -gt 20) {
    try {
        $meResponse = Invoke-WebRequest -Uri "$baseUrl/auth/me" -Method Get -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing
        Write-Output "ME_STATUS=$($meResponse.StatusCode)"
        Write-Output "ME_USER=$((($meResponse.Content | ConvertFrom-Json).user).username)"
    } catch {
        $meStatus = [int]$_.Exception.Response.StatusCode
        Write-Output "ME_STATUS=$meStatus"
    }
} else {
    Write-Output "ME_STATUS=SKIPPED"
}
