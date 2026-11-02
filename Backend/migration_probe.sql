SET NOCOUNT ON;
SELECT 'Orders.EditVersion' AS check_name, CASE WHEN EXISTS(SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id WHERE t.name='Orders' AND c.name='EditVersion') THEN 1 ELSE 0 END AS present;
SELECT 'TailoringOrderEditOperations' AS check_name, OBJECT_ID('dbo.TailoringOrderEditOperations','U') AS object_id;
SELECT 'TailoringPieceAddOperations' AS check_name, OBJECT_ID('dbo.TailoringPieceAddOperations','U') AS object_id;
SELECT 'TailoringPieceCancellations' AS check_name, OBJECT_ID('dbo.TailoringPieceCancellations','U') AS object_id;
SELECT 'TailoringPieceBenefitBalances' AS check_name, OBJECT_ID('dbo.TailoringPieceBenefitBalances','U') AS object_id;
SELECT 'duplicate_edit_operation' AS check_name, (SELECT COUNT(*) FROM (SELECT EditOperationId FROM dbo.TailoringOrderEditOperations GROUP BY EditOperationId HAVING COUNT(*)>1) x) AS value;
SELECT 'duplicate_add_operation' AS check_name, (SELECT COUNT(*) FROM (SELECT AddOperationId FROM dbo.TailoringPieceAddOperations GROUP BY AddOperationId HAVING COUNT(*)>1) x) AS value;
SELECT 'duplicate_piece_cancellation' AS check_name, (SELECT COUNT(*) FROM (SELECT PieceId FROM dbo.TailoringPieceCancellations GROUP BY PieceId HAVING COUNT(*)>1) x) AS value;
SELECT 'orphan_add_ops' AS check_name, (SELECT COUNT(*) FROM dbo.TailoringPieceAddOperations a LEFT JOIN dbo.TailoringOrderEditOperations e ON e.EditOperationId=a.EditOperationId WHERE e.EditOperationId IS NULL) AS value;
SELECT 'orphan_cancellations' AS check_name, (SELECT COUNT(*) FROM dbo.TailoringPieceCancellations c LEFT JOIN dbo.TailoringOrderEditOperations e ON e.EditOperationId=c.EditOperationId WHERE e.EditOperationId IS NULL) AS value;
SELECT 'orders_count' AS check_name, COUNT(*) AS value FROM dbo.Orders;
SELECT 'pieces_count' AS check_name, COUNT(*) AS value FROM dbo.Pieces;
SELECT i.name AS index_name, i.is_unique, i.is_disabled
FROM sys.indexes i
WHERE i.object_id IN (OBJECT_ID('dbo.TailoringOrderEditOperations'), OBJECT_ID('dbo.TailoringPieceAddOperations'), OBJECT_ID('dbo.TailoringPieceCancellations'))
AND i.name IS NOT NULL
ORDER BY OBJECT_NAME(i.object_id), i.name;
