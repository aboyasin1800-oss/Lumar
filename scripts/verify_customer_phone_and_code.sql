USE [LUMAR_ERP];
GO

-- PhoneNumber must be nullable/non-unique, while CustomerCode remains unique.
SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns AS c
JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.Customers')
  AND c.name IN (N'CustomerCode', N'PhoneNumber');

SELECT
    i.name AS IndexName,
    i.is_unique AS IsUnique,
    i.is_primary_key AS IsPrimaryKey,
    STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS IndexedColumns
FROM sys.indexes AS i
JOIN sys.index_columns AS ic
  ON ic.object_id = i.object_id
 AND ic.index_id = i.index_id
JOIN sys.columns AS c
  ON c.object_id = ic.object_id
 AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.Customers')
  AND i.is_hypothetical = 0
GROUP BY i.name, i.is_unique, i.is_primary_key;

SELECT
    PhoneNumber,
    COUNT(*) AS CustomerCount
FROM dbo.Customers
WHERE PhoneNumber IS NOT NULL
  AND LTRIM(RTRIM(PhoneNumber)) <> N''
GROUP BY PhoneNumber
HAVING COUNT(*) > 1
ORDER BY CustomerCount DESC, PhoneNumber;

SELECT
    CustomerCode,
    COUNT(*) AS CodeCount
FROM dbo.Customers
GROUP BY CustomerCode
HAVING COUNT(*) > 1;

DECLARE @prefix nvarchar(50) = (
    SELECT TOP (1) LTRIM(RTRIM(SettingValue))
    FROM dbo.System_Settings
    WHERE SettingName = N'CustomerCodePrefix'
);

SELECT
    @prefix AS CustomerCodePrefix,
    @prefix + CONVERT(varchar(30), ISNULL(MAX(TRY_CONVERT(int, SUBSTRING(CustomerCode, LEN(@prefix) + 1, 20))), 0) + 1) AS NextCustomerCode
FROM dbo.Customers
WHERE CustomerCode LIKE @prefix + N'%';

SELECT TOP (20)
    CustomerID,
    CustomerCode,
    CustomerName,
    PhoneNumber,
    Notes
FROM dbo.Customers
WHERE Notes LIKE N'%CUSTOMER-PHONE-DUPLICATION-VERIFICATION-01%'
ORDER BY CustomerID DESC;
GO
