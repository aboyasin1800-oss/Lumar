UPDATE dbo.System_Settings
SET SettingValue = N'{"routes":[{"pieceType":"PANTS","stages":["Printing","FabricPrep","Cutting","Sewing","Ironing","Quality","Assembly"],"isEnabled":true},{"pieceType":"SHIRT","stages":["Printing","FabricPrep","Cutting","Sewing","Buttons","Ironing","Quality","Assembly"],"isEnabled":true}]}'
WHERE SettingName = 'ProductionRoutesConfig';

SELECT SettingID, SettingName, SettingValue
FROM dbo.System_Settings
WHERE SettingName = 'ProductionRoutesConfig';

SELECT CASE WHEN SettingValue LIKE '%FabricPrep%' AND SettingValue NOT LIKE '%Ready%' AND SettingValue NOT LIKE '%Delivery%' THEN 'CANONICAL_OK' ELSE 'NOT_CANONICAL' END AS RouteStatus
FROM dbo.System_Settings
WHERE SettingName = 'ProductionRoutesConfig';
