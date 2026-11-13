$query = @'
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DbName, GETDATE() AS NowUtc;
'@
Invoke-Sqlcmd -ServerInstance 'YASIN-YASIN\SQLEXPRESS' -Database 'master' -Query $query -SuppressProviderContextWarning | Format-List *
