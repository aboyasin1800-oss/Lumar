[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$database = 'LUMAR_ERP_CUSTOMERS_ONLY_VALIDATION'

if (-not ('PasswordResetPbkdf2' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Security.Cryptography;
using System.Text;

public static class PasswordResetPbkdf2
{
    public static byte[] Derive(string password, byte[] salt, int iterations, int length)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password)))
        {
            var output = new byte[length];
            var block = 1;
            var offset = 0;

            while (offset < length)
            {
                var input = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
                input[salt.Length] = (byte)(block >> 24);
                input[salt.Length + 1] = (byte)(block >> 16);
                input[salt.Length + 2] = (byte)(block >> 8);
                input[salt.Length + 3] = (byte)block;

                var current = hmac.ComputeHash(input);
                var combined = (byte[])current.Clone();
                for (var iteration = 1; iteration < iterations; iteration++)
                {
                    current = hmac.ComputeHash(current);
                    for (var index = 0; index < combined.Length; index++)
                    {
                        combined[index] = (byte)(combined[index] ^ current[index]);
                    }
                }

                var count = Math.Min(combined.Length, length - offset);
                Buffer.BlockCopy(combined, 0, output, offset, count);
                offset += count;
                block++;
            }

            return output;
        }
    }
}
'@
}

$first = Read-Host -AsSecureString 'New password for admin'
$second = Read-Host -AsSecureString 'Confirm new password'
$firstBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($first)
$secondBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($second)

try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($firstBstr)
    $confirmation = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secondBstr)

    if ($password -ne $confirmation) {
        throw 'The password confirmation does not match.'
    }

    if ($password.Length -lt 8) {
        throw 'The password must contain at least 8 characters.'
    }

    $salt = [byte[]]::new(16)
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try {
        $rng.GetBytes($salt)
    }
    finally {
        $rng.Dispose()
    }

    $hash = [PasswordResetPbkdf2]::Derive($password, $salt, 210000, 32)
    $passwordHash = 'PBKDF2$210000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($hash)
    $connectionString = "Server=YASIN-YASIN\SQLEXPRESS;Database=$database;Integrated Security=True;TrustServerCertificate=True"
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
    $connection.Open()

    try {
        $command = $connection.CreateCommand()
        $command.CommandText = @'
UPDATE dbo.Users
SET PasswordHash = @hash,
    UserPassword = '',
    SecurityStamp = NEWID()
WHERE Username = N'admin'
  AND IsActive = 1;

UPDATE dbo.UserSessions
SET RevokedAtUtc = SYSUTCDATETIME(),
    RevocationReason = 'PasswordReset'
WHERE UserId = (SELECT UserID FROM dbo.Users WHERE Username = N'admin')
  AND RevokedAtUtc IS NULL;
'@
        [void]$command.Parameters.AddWithValue('@hash', $passwordHash)
        $affected = $command.ExecuteNonQuery()

        if ($affected -lt 1) {
            throw 'No active admin account was updated.'
        }

        Write-Output "ADMIN_PASSWORD_RESET_COMPLETED database=$database"
    }
    finally {
        $connection.Close()
    }
}
finally {
    if ($firstBstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($firstBstr)
    }

    if ($secondBstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secondBstr)
    }
}