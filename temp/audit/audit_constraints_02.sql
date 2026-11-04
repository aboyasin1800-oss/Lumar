SET NOCOUNT ON;

CREATE TABLE #ES_PK (schema_name sysname, table_name sysname, constraint_name sysname, column_list nvarchar(4000), key_order nvarchar(4000));
CREATE TABLE #VIP_PK (schema_name sysname, table_name sysname, constraint_name sysname, column_list nvarchar(4000), key_order nvarchar(4000));
CREATE TABLE #ES_FK (schema_name sysname, table_name sysname, fk_name sysname, parent_columns nvarchar(4000), referenced_schema sysname, referenced_table sysname, referenced_columns nvarchar(4000), delete_action nvarchar(20), update_action nvarchar(20), is_disabled bit, is_not_trusted bit);
CREATE TABLE #VIP_FK (schema_name sysname, table_name sysname, fk_name sysname, parent_columns nvarchar(4000), referenced_schema sysname, referenced_table sysname, referenced_columns nvarchar(4000), delete_action nvarchar(20), update_action nvarchar(20), is_disabled bit, is_not_trusted bit);
CREATE TABLE #ES_CHECK (schema_name sysname, table_name sysname, check_name sysname, definition nvarchar(4000), is_disabled bit, is_not_trusted bit);
CREATE TABLE #VIP_CHECK (schema_name sysname, table_name sysname, check_name sysname, definition nvarchar(4000), is_disabled bit, is_not_trusted bit);
CREATE TABLE #ES_UQ (schema_name sysname, table_name sysname, uq_name sysname, column_list nvarchar(4000));
CREATE TABLE #VIP_UQ (schema_name sysname, table_name sysname, uq_name sysname, column_list nvarchar(4000));
CREATE TABLE #ES_DEF (schema_name sysname, table_name sysname, default_name sysname, column_name sysname, definition nvarchar(4000));
CREATE TABLE #VIP_DEF (schema_name sysname, table_name sysname, default_name sysname, column_name sysname, definition nvarchar(4000));
CREATE TABLE #ES_IDX (schema_name sysname, table_name sysname, index_name sysname, is_unique bit, key_columns nvarchar(4000), include_columns nvarchar(4000), filter_definition nvarchar(4000), is_disabled bit);
CREATE TABLE #VIP_IDX (schema_name sysname, table_name sysname, index_name sysname, is_unique bit, key_columns nvarchar(4000), include_columns nvarchar(4000), filter_definition nvarchar(4000), is_disabled bit);

INSERT INTO #ES_PK(schema_name, table_name, constraint_name, column_list, key_order)
SELECT SCHEMA_NAME(t.schema_id), t.name, kc.name,
       STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
       STRING_AGG(CAST(ic.key_ordinal AS nvarchar(10)) + ':' + c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
FROM LUMAR_ERP_ES_VALIDATION.sys.key_constraints kc
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables t ON t.object_id = kc.parent_object_id
JOIN LUMAR_ERP_ES_VALIDATION.sys.indexes i ON i.object_id = t.object_id AND i.name = kc.name
JOIN LUMAR_ERP_ES_VALIDATION.sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
WHERE kc.type='PK'
GROUP BY SCHEMA_NAME(t.schema_id), t.name, kc.name;

INSERT INTO #VIP_PK(schema_name, table_name, constraint_name, column_list, key_order)
SELECT SCHEMA_NAME(t.schema_id), t.name, kc.name,
       STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
       STRING_AGG(CAST(ic.key_ordinal AS nvarchar(10)) + ':' + c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
FROM LUMAR_ERP_VIP_1.sys.key_constraints kc
JOIN LUMAR_ERP_VIP_1.sys.tables t ON t.object_id = kc.parent_object_id
JOIN LUMAR_ERP_VIP_1.sys.indexes i ON i.object_id = t.object_id AND i.name = kc.name
JOIN LUMAR_ERP_VIP_1.sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
JOIN LUMAR_ERP_VIP_1.sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
WHERE kc.type='PK'
GROUP BY SCHEMA_NAME(t.schema_id), t.name, kc.name;

INSERT INTO #ES_FK(schema_name, table_name, fk_name, parent_columns, referenced_schema, referenced_table, referenced_columns, delete_action, update_action, is_disabled, is_not_trusted)
SELECT SCHEMA_NAME(f.parent_object_id), OBJECT_NAME(f.parent_object_id), f.name,
       STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fc.constraint_column_id),
       SCHEMA_NAME(r.schema_id), OBJECT_NAME(f.referenced_object_id),
       STRING_AGG(rc.name, ',') WITHIN GROUP (ORDER BY fc.constraint_column_id),
       CASE f.delete_referential_action WHEN 0 THEN 'NO_ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET_NULL' WHEN 3 THEN 'SET_DEFAULT' ELSE 'UNKNOWN' END,
       CASE f.update_referential_action WHEN 0 THEN 'NO_ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET_NULL' WHEN 3 THEN 'SET_DEFAULT' ELSE 'UNKNOWN' END,
       f.is_disabled, f.is_not_trusted
FROM LUMAR_ERP_ES_VALIDATION.sys.foreign_keys f
JOIN LUMAR_ERP_ES_VALIDATION.sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=fc.parent_column_id
JOIN LUMAR_ERP_ES_VALIDATION.sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=fc.referenced_column_id
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables r ON r.object_id=f.referenced_object_id
GROUP BY SCHEMA_NAME(f.parent_object_id), OBJECT_NAME(f.parent_object_id), f.name, SCHEMA_NAME(r.schema_id), OBJECT_NAME(f.referenced_object_id), f.delete_referential_action, f.update_referential_action, f.is_disabled, f.is_not_trusted;

INSERT INTO #VIP_FK(schema_name, table_name, fk_name, parent_columns, referenced_schema, referenced_table, referenced_columns, delete_action, update_action, is_disabled, is_not_trusted)
SELECT SCHEMA_NAME(f.parent_object_id), OBJECT_NAME(f.parent_object_id), f.name,
       STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fc.constraint_column_id),
       SCHEMA_NAME(r.schema_id), OBJECT_NAME(f.referenced_object_id),
       STRING_AGG(rc.name, ',') WITHIN GROUP (ORDER BY fc.constraint_column_id),
       CASE f.delete_referential_action WHEN 0 THEN 'NO_ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET_NULL' WHEN 3 THEN 'SET_DEFAULT' ELSE 'UNKNOWN' END,
       CASE f.update_referential_action WHEN 0 THEN 'NO_ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET_NULL' WHEN 3 THEN 'SET_DEFAULT' ELSE 'UNKNOWN' END,
       f.is_disabled, f.is_not_trusted
FROM LUMAR_ERP_VIP_1.sys.foreign_keys f
JOIN LUMAR_ERP_VIP_1.sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
JOIN LUMAR_ERP_VIP_1.sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=fc.parent_column_id
JOIN LUMAR_ERP_VIP_1.sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=fc.referenced_column_id
JOIN LUMAR_ERP_VIP_1.sys.tables r ON r.object_id=f.referenced_object_id
GROUP BY SCHEMA_NAME(f.parent_object_id), OBJECT_NAME(f.parent_object_id), f.name, SCHEMA_NAME(r.schema_id), OBJECT_NAME(f.referenced_object_id), f.delete_referential_action, f.update_referential_action, f.is_disabled, f.is_not_trusted;

INSERT INTO #ES_CHECK(schema_name, table_name, check_name, definition, is_disabled, is_not_trusted)
SELECT SCHEMA_NAME(t.schema_id), t.name, cc.name, cc.definition, cc.is_disabled, cc.is_not_trusted
FROM LUMAR_ERP_ES_VALIDATION.sys.check_constraints cc
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables t ON t.object_id = cc.parent_object_id;

INSERT INTO #VIP_CHECK(schema_name, table_name, check_name, definition, is_disabled, is_not_trusted)
SELECT SCHEMA_NAME(t.schema_id), t.name, cc.name, cc.definition, cc.is_disabled, cc.is_not_trusted
FROM LUMAR_ERP_VIP_1.sys.check_constraints cc
JOIN LUMAR_ERP_VIP_1.sys.tables t ON t.object_id = cc.parent_object_id;

INSERT INTO #ES_UQ(schema_name, table_name, uq_name, column_list)
SELECT SCHEMA_NAME(t.schema_id), t.name, kc.name,
       STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
FROM LUMAR_ERP_ES_VALIDATION.sys.key_constraints kc
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables t ON t.object_id = kc.parent_object_id
JOIN LUMAR_ERP_ES_VALIDATION.sys.indexes i ON i.object_id=t.object_id AND i.name = kc.name
JOIN LUMAR_ERP_ES_VALIDATION.sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.is_included_column=0
JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
WHERE kc.type='UQ'
GROUP BY SCHEMA_NAME(t.schema_id), t.name, kc.name;

INSERT INTO #VIP_UQ(schema_name, table_name, uq_name, column_list)
SELECT SCHEMA_NAME(t.schema_id), t.name, kc.name,
       STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
FROM LUMAR_ERP_VIP_1.sys.key_constraints kc
JOIN LUMAR_ERP_VIP_1.sys.tables t ON t.object_id = kc.parent_object_id
JOIN LUMAR_ERP_VIP_1.sys.indexes i ON i.object_id=t.object_id AND i.name = kc.name
JOIN LUMAR_ERP_VIP_1.sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.is_included_column=0
JOIN LUMAR_ERP_VIP_1.sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
WHERE kc.type='UQ'
GROUP BY SCHEMA_NAME(t.schema_id), t.name, kc.name;

INSERT INTO #ES_DEF(schema_name, table_name, default_name, column_name, definition)
SELECT SCHEMA_NAME(t.schema_id), t.name, dc.name, c.name, dc.definition
FROM LUMAR_ERP_ES_VALIDATION.sys.default_constraints dc
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables t ON t.object_id = dc.parent_object_id
JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id;

INSERT INTO #VIP_DEF(schema_name, table_name, default_name, column_name, definition)
SELECT SCHEMA_NAME(t.schema_id), t.name, dc.name, c.name, dc.definition
FROM LUMAR_ERP_VIP_1.sys.default_constraints dc
JOIN LUMAR_ERP_VIP_1.sys.tables t ON t.object_id = dc.parent_object_id
JOIN LUMAR_ERP_VIP_1.sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id;

INSERT INTO #ES_IDX(schema_name, table_name, index_name, is_unique, key_columns, include_columns, filter_definition, is_disabled)
SELECT SCHEMA_NAME(t.schema_id), t.name, i.name, i.is_unique,
       STRING_AGG(CASE WHEN ic.is_included_column=0 THEN c.name END, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
       STRING_AGG(CASE WHEN ic.is_included_column=1 THEN c.name END, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
       i.filter_definition, i.is_disabled
FROM LUMAR_ERP_ES_VALIDATION.sys.indexes i
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables t ON t.object_id = i.object_id
LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
WHERE i.name IS NOT NULL AND i.type IN (1,2) AND i.is_hypothetical = 0
GROUP BY SCHEMA_NAME(t.schema_id), t.name, i.name, i.is_unique, i.filter_definition, i.is_disabled;

INSERT INTO #VIP_IDX(schema_name, table_name, index_name, is_unique, key_columns, include_columns, filter_definition, is_disabled)
SELECT SCHEMA_NAME(t.schema_id), t.name, i.name, i.is_unique,
       STRING_AGG(CASE WHEN ic.is_included_column=0 THEN c.name END, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
       STRING_AGG(CASE WHEN ic.is_included_column=1 THEN c.name END, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
       i.filter_definition, i.is_disabled
FROM LUMAR_ERP_VIP_1.sys.indexes i
JOIN LUMAR_ERP_VIP_1.sys.tables t ON t.object_id = i.object_id
LEFT JOIN LUMAR_ERP_VIP_1.sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
LEFT JOIN LUMAR_ERP_VIP_1.sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
WHERE i.name IS NOT NULL AND i.type IN (1,2) AND i.is_hypothetical = 0
GROUP BY SCHEMA_NAME(t.schema_id), t.name, i.name, i.is_unique, i.filter_definition, i.is_disabled;

SELECT 'PK_MISSING', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.constraint_name
  FROM #ES_PK e
  LEFT JOIN #VIP_PK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.constraint_name = e.constraint_name
  WHERE v.constraint_name IS NULL
) x;
SELECT 'PK_EXTRA', COUNT(*) FROM (
  SELECT v.schema_name, v.table_name, v.constraint_name
  FROM #VIP_PK v
  LEFT JOIN #ES_PK e ON e.schema_name = v.schema_name AND e.table_name = v.table_name AND e.constraint_name = v.constraint_name
  WHERE e.constraint_name IS NULL
) x;
SELECT 'PK_DIFFERENT', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.constraint_name
  FROM #ES_PK e
  JOIN #VIP_PK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.constraint_name = e.constraint_name
  WHERE e.column_list <> v.column_list OR e.key_order <> v.key_order
) x;
SELECT 'PK_DRAFT_LIST';
SELECT e.schema_name, e.table_name, e.constraint_name, e.column_list, v.column_list, e.key_order, v.key_order
FROM #ES_PK e
JOIN #VIP_PK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.constraint_name = e.constraint_name
WHERE e.column_list <> v.column_list OR e.key_order <> v.key_order
ORDER BY e.schema_name, e.table_name, e.constraint_name;

SELECT 'FK_MISSING', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.fk_name
  FROM #ES_FK e
  LEFT JOIN #VIP_FK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.fk_name = e.fk_name
  WHERE v.fk_name IS NULL
) x;
SELECT 'FK_EXTRA', COUNT(*) FROM (
  SELECT v.schema_name, v.table_name, v.fk_name
  FROM #VIP_FK v
  LEFT JOIN #ES_FK e ON e.schema_name = v.schema_name AND e.table_name = v.table_name AND e.fk_name = v.fk_name
  WHERE e.fk_name IS NULL
) x;
SELECT 'FK_DIFFERENT', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.fk_name
  FROM #ES_FK e
  JOIN #VIP_FK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.fk_name = e.fk_name
  WHERE e.parent_columns <> v.parent_columns OR e.referenced_schema <> v.referenced_schema OR e.referenced_table <> v.referenced_table OR e.referenced_columns <> v.referenced_columns OR e.delete_action <> v.delete_action OR e.update_action <> v.update_action OR e.is_disabled <> v.is_disabled OR e.is_not_trusted <> v.is_not_trusted
) x;
SELECT 'FK_ES_DISABLED', COUNT(*) FROM #ES_FK WHERE is_disabled = 1;
SELECT 'FK_VIP_DISABLED', COUNT(*) FROM #VIP_FK WHERE is_disabled = 1;
SELECT 'FK_ES_UNTRUSTED', COUNT(*) FROM #ES_FK WHERE is_not_trusted = 1;
SELECT 'FK_VIP_UNTRUSTED', COUNT(*) FROM #VIP_FK WHERE is_not_trusted = 1;
SELECT 'FK_DRAFT_LIST';
SELECT e.schema_name, e.table_name, e.fk_name, e.parent_columns, e.referenced_schema, e.referenced_table, e.referenced_columns, e.delete_action, v.delete_action, e.update_action, v.update_action, e.is_disabled, v.is_disabled, e.is_not_trusted, v.is_not_trusted
FROM #ES_FK e
JOIN #VIP_FK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.fk_name = e.fk_name
WHERE e.parent_columns <> v.parent_columns OR e.referenced_schema <> v.referenced_schema OR e.referenced_table <> v.referenced_table OR e.referenced_columns <> v.referenced_columns OR e.delete_action <> v.delete_action OR e.update_action <> v.update_action OR e.is_disabled <> v.is_disabled OR e.is_not_trusted <> v.is_not_trusted
ORDER BY e.schema_name, e.table_name, e.fk_name;

SELECT 'CHECK_MISSING', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.check_name
  FROM #ES_CHECK e
  LEFT JOIN #VIP_CHECK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.check_name = e.check_name
  WHERE v.check_name IS NULL
) x;
SELECT 'CHECK_EXTRA', COUNT(*) FROM (
  SELECT v.schema_name, v.table_name, v.check_name
  FROM #VIP_CHECK v
  LEFT JOIN #ES_CHECK e ON e.schema_name = v.schema_name AND e.table_name = v.table_name AND e.check_name = v.check_name
  WHERE e.check_name IS NULL
) x;
SELECT 'CHECK_DIFFERENT', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.check_name
  FROM #ES_CHECK e
  JOIN #VIP_CHECK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.check_name = e.check_name
  WHERE e.definition <> v.definition OR e.is_disabled <> v.is_disabled OR e.is_not_trusted <> v.is_not_trusted
) x;
SELECT 'CHECK_ES_DISABLED', COUNT(*) FROM #ES_CHECK WHERE is_disabled = 1;
SELECT 'CHECK_VIP_DISABLED', COUNT(*) FROM #VIP_CHECK WHERE is_disabled = 1;
SELECT 'CHECK_ES_UNTRUSTED', COUNT(*) FROM #ES_CHECK WHERE is_not_trusted = 1;
SELECT 'CHECK_VIP_UNTRUSTED', COUNT(*) FROM #VIP_CHECK WHERE is_not_trusted = 1;
SELECT 'CHECK_DRAFT_LIST';
SELECT e.schema_name, e.table_name, e.check_name, e.definition, v.definition, e.is_disabled, v.is_disabled, e.is_not_trusted, v.is_not_trusted
FROM #ES_CHECK e
JOIN #VIP_CHECK v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.check_name = e.check_name
WHERE e.definition <> v.definition OR e.is_disabled <> v.is_disabled OR e.is_not_trusted <> v.is_not_trusted
ORDER BY e.schema_name, e.table_name, e.check_name;

SELECT 'UNIQUE_MISSING', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.uq_name
  FROM #ES_UQ e
  LEFT JOIN #VIP_UQ v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.uq_name = e.uq_name
  WHERE v.uq_name IS NULL
) x;
SELECT 'UNIQUE_EXTRA', COUNT(*) FROM (
  SELECT v.schema_name, v.table_name, v.uq_name
  FROM #VIP_UQ v
  LEFT JOIN #ES_UQ e ON e.schema_name = v.schema_name AND e.table_name = v.table_name AND e.uq_name = v.uq_name
  WHERE e.uq_name IS NULL
) x;
SELECT 'UNIQUE_DIFFERENT', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.uq_name
  FROM #ES_UQ e
  JOIN #VIP_UQ v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.uq_name = e.uq_name
  WHERE e.column_list <> v.column_list
) x;
SELECT 'UNIQUE_DRAFT_LIST';
SELECT e.schema_name, e.table_name, e.uq_name, e.column_list, v.column_list
FROM #ES_UQ e
JOIN #VIP_UQ v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.uq_name = e.uq_name
WHERE e.column_list <> v.column_list
ORDER BY e.schema_name, e.table_name, e.uq_name;

SELECT 'DEFAULT_MISSING', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.default_name, e.column_name
  FROM #ES_DEF e
  LEFT JOIN #VIP_DEF v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.default_name = e.default_name AND v.column_name = e.column_name
  WHERE v.default_name IS NULL
) x;
SELECT 'DEFAULT_EXTRA', COUNT(*) FROM (
  SELECT v.schema_name, v.table_name, v.default_name, v.column_name
  FROM #VIP_DEF v
  LEFT JOIN #ES_DEF e ON e.schema_name = v.schema_name AND e.table_name = v.table_name AND e.default_name = v.default_name AND e.column_name = v.column_name
  WHERE e.default_name IS NULL
) x;
SELECT 'DEFAULT_DIFFERENT', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.default_name, e.column_name
  FROM #ES_DEF e
  JOIN #VIP_DEF v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.default_name = e.default_name AND v.column_name = e.column_name
  WHERE e.definition <> v.definition
) x;
SELECT 'DEFAULT_DRAFT_LIST';
SELECT e.schema_name, e.table_name, e.default_name, e.column_name, e.definition, v.definition
FROM #ES_DEF e
JOIN #VIP_DEF v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.default_name = e.default_name AND v.column_name = e.column_name
WHERE e.definition <> v.definition
ORDER BY e.schema_name, e.table_name, e.default_name, e.column_name;

SELECT 'INDEX_MISSING', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.index_name
  FROM #ES_IDX e
  LEFT JOIN #VIP_IDX v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.index_name = e.index_name
  WHERE v.index_name IS NULL
) x;
SELECT 'INDEX_EXTRA', COUNT(*) FROM (
  SELECT v.schema_name, v.table_name, v.index_name
  FROM #VIP_IDX v
  LEFT JOIN #ES_IDX e ON e.schema_name = v.schema_name AND e.table_name = v.table_name AND e.index_name = v.index_name
  WHERE e.index_name IS NULL
) x;
SELECT 'INDEX_DIFFERENT', COUNT(*) FROM (
  SELECT e.schema_name, e.table_name, e.index_name
  FROM #ES_IDX e
  JOIN #VIP_IDX v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.index_name = e.index_name
  WHERE e.is_unique <> v.is_unique OR e.key_columns <> v.key_columns OR e.include_columns <> v.include_columns OR e.filter_definition <> v.filter_definition OR e.is_disabled <> v.is_disabled
) x;
SELECT 'INDEX_DRAFT_LIST';
SELECT e.schema_name, e.table_name, e.index_name, e.is_unique, v.is_unique, e.key_columns, v.key_columns, e.include_columns, v.include_columns, e.filter_definition, v.filter_definition, e.is_disabled, v.is_disabled
FROM #ES_IDX e
JOIN #VIP_IDX v ON v.schema_name = e.schema_name AND v.table_name = e.table_name AND v.index_name = e.index_name
WHERE e.is_unique <> v.is_unique OR e.key_columns <> v.key_columns OR e.include_columns <> v.include_columns OR e.filter_definition <> v.filter_definition OR e.is_disabled <> v.is_disabled
ORDER BY e.schema_name, e.table_name, e.index_name;
