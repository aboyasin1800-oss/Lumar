SET NOCOUNT ON;
WITH ES_COL AS (
  SELECT s.name AS schema_name,
         t.name AS table_name,
         c.name AS column_name,
         CONCAT(
           UPPER(TYPE_NAME(c.user_type_id)),
           CASE 
             WHEN TYPE_NAME(c.user_type_id) IN ('char','nchar','varchar','nvarchar','binary','varbinary') THEN CONCAT('(', CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length / 2 AS nvarchar(10)) END, ')')
             WHEN TYPE_NAME(c.user_type_id) IN ('decimal','numeric') THEN CONCAT('(', c.precision, ',', c.scale, ')')
             WHEN TYPE_NAME(c.user_type_id) IN ('datetime2','datetimeoffset','time') THEN CONCAT('(', c.scale, ')')
             ELSE ''
           END,
           CASE WHEN c.is_nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END
         ) AS def
  FROM LUMAR_ERP_ES_VALIDATION.sys.columns c
  JOIN LUMAR_ERP_ES_VALIDATION.sys.tables t ON t.object_id = c.object_id
  JOIN LUMAR_ERP_ES_VALIDATION.sys.schemas s ON s.schema_id = t.schema_id
),
VIP_COL AS (
  SELECT s.name AS schema_name,
         t.name AS table_name,
         c.name AS column_name,
         CONCAT(
           UPPER(TYPE_NAME(c.user_type_id)),
           CASE 
             WHEN TYPE_NAME(c.user_type_id) IN ('char','nchar','varchar','nvarchar','binary','varbinary') THEN CONCAT('(', CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length / 2 AS nvarchar(10)) END, ')')
             WHEN TYPE_NAME(c.user_type_id) IN ('decimal','numeric') THEN CONCAT('(', c.precision, ',', c.scale, ')')
             WHEN TYPE_NAME(c.user_type_id) IN ('datetime2','datetimeoffset','time') THEN CONCAT('(', c.scale, ')')
             ELSE ''
           END,
           CASE WHEN c.is_nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END
         ) AS def
  FROM LUMAR_ERP_VIP_1.sys.columns c
  JOIN LUMAR_ERP_VIP_1.sys.tables t ON t.object_id = c.object_id
  JOIN LUMAR_ERP_VIP_1.sys.schemas s ON s.schema_id = t.schema_id
),
MISSING AS (
  SELECT e.schema_name, e.table_name, e.column_name, e.def AS ESDefinition, NULL AS VIPDefinition
  FROM ES_COL e
  LEFT JOIN VIP_COL v
    ON v.schema_name = e.schema_name
   AND v.table_name = e.table_name
   AND v.column_name = e.column_name
  WHERE v.column_name IS NULL
),
EXTRA AS (
  SELECT v.schema_name, v.table_name, v.column_name, NULL AS ESDefinition, v.def AS VIPDefinition
  FROM VIP_COL v
  LEFT JOIN ES_COL e
    ON e.schema_name = v.schema_name
   AND e.table_name = v.table_name
   AND e.column_name = v.column_name
  WHERE e.column_name IS NULL
),
DIFFERENT AS (
  SELECT e.schema_name,
         e.table_name,
         e.column_name,
         e.def AS ESDefinition,
         v.def AS VIPDefinition
  FROM ES_COL e
  JOIN VIP_COL v
    ON v.schema_name = e.schema_name
   AND v.table_name = e.table_name
   AND v.column_name = e.column_name
  WHERE e.def <> v.def
)
SELECT 'MissingColumnsInVIP' AS metric, COUNT(*) AS value FROM MISSING;
SELECT 'ExtraColumnsInVIP' AS metric, COUNT(*) AS value FROM EXTRA;
SELECT 'DifferentColumnDefinitions' AS metric, COUNT(*) AS value FROM DIFFERENT;
SELECT CONCAT(schema_name,'.',table_name,'.',column_name) AS obj, ESDefinition, VIPDefinition
FROM (SELECT TOP 20 * FROM MISSING ORDER BY schema_name,table_name,column_name) a;
SELECT CONCAT(schema_name,'.',table_name,'.',column_name) AS obj, ESDefinition, VIPDefinition
FROM (SELECT TOP 20 * FROM EXTRA ORDER BY schema_name,table_name,column_name) a;
SELECT CONCAT(schema_name,'.',table_name,'.',column_name) AS obj, ESDefinition, VIPDefinition
FROM (SELECT TOP 20 * FROM DIFFERENT ORDER BY schema_name,table_name,column_name) a;
