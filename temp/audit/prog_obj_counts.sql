SET NOCOUNT ON;
WITH ES AS (
  SELECT o.type, s.name AS schema_name, o.name AS object_name
  FROM LUMAR_ERP_ES_VALIDATION.sys.objects o
  JOIN LUMAR_ERP_ES_VALIDATION.sys.schemas s ON s.schema_id = o.schema_id
  WHERE o.type IN ('V','P','FN','TF','IF','TR','SO')
), VIP AS (
  SELECT o.type, s.name AS schema_name, o.name AS object_name
  FROM LUMAR_ERP_VIP_1.sys.objects o
  JOIN LUMAR_ERP_VIP_1.sys.schemas s ON s.schema_id = o.schema_id
  WHERE o.type IN ('V','P','FN','TF','IF','TR','SO')
)
SELECT category, cnt
FROM (
  SELECT 'VIEW_MISSING' AS category, COUNT(*) AS cnt
  FROM (SELECT e.object_name FROM ES e WHERE e.type='V' AND NOT EXISTS (SELECT 1 FROM VIP v WHERE v.type='V' AND v.schema_name=e.schema_name AND v.object_name=e.object_name)) x
  UNION ALL
  SELECT 'VIEW_EXTRA', COUNT(*)
  FROM (SELECT v.object_name FROM VIP v WHERE v.type='V' AND NOT EXISTS (SELECT 1 FROM ES e WHERE e.type='V' AND e.schema_name=v.schema_name AND e.object_name=v.object_name)) x2
  UNION ALL
  SELECT 'PROC_MISSING', COUNT(*)
  FROM (SELECT e.object_name FROM ES e WHERE e.type IN ('P','FN','TF','IF') AND NOT EXISTS (SELECT 1 FROM VIP v WHERE v.type IN ('P','FN','TF','IF') AND v.schema_name=e.schema_name AND v.object_name=e.object_name)) x3
  UNION ALL
  SELECT 'PROC_EXTRA', COUNT(*)
  FROM (SELECT v.object_name FROM VIP v WHERE v.type IN ('P','FN','TF','IF') AND NOT EXISTS (SELECT 1 FROM ES e WHERE e.type IN ('P','FN','TF','IF') AND e.schema_name=v.schema_name AND e.object_name=v.object_name)) x4
  UNION ALL
  SELECT 'TRIGGER_MISSING', COUNT(*)
  FROM (SELECT e.object_name FROM ES e WHERE e.type='TR' AND NOT EXISTS (SELECT 1 FROM VIP v WHERE v.type='TR' AND v.schema_name=e.schema_name AND v.object_name=e.object_name)) x5
  UNION ALL
  SELECT 'TRIGGER_EXTRA', COUNT(*)
  FROM (SELECT v.object_name FROM VIP v WHERE v.type='TR' AND NOT EXISTS (SELECT 1 FROM ES e WHERE e.type='TR' AND e.schema_name=v.schema_name AND e.object_name=v.object_name)) x6
  UNION ALL
  SELECT 'SEQUENCE_MISSING', COUNT(*)
  FROM (SELECT e.object_name FROM ES e WHERE e.type='SO' AND NOT EXISTS (SELECT 1 FROM VIP v WHERE v.type='SO' AND v.schema_name=e.schema_name AND v.object_name=e.object_name)) x7
  UNION ALL
  SELECT 'SEQUENCE_EXTRA', COUNT(*)
  FROM (SELECT v.object_name FROM VIP v WHERE v.type='SO' AND NOT EXISTS (SELECT 1 FROM ES e WHERE e.type='SO' AND e.schema_name=v.schema_name AND e.object_name=v.object_name)) x8
) q
ORDER BY category;
