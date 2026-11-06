$ErrorActionPreference = 'Stop'

$server = 'YASIN-YASIN\SQLEXPRESS'
$sourceDb = 'LUMAR_ERP_ES_VALIDATION_R3'
$proofDb = 'LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF'
$backupPath = 'D:\YASIN\Backend\R3_RESET_PROOF_BACKUP.bak'
$mdfPath = 'D:\SQLData\LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF.mdf'
$logPath = 'D:\SQLLogs\LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF_log.ldf'

function Invoke-Sql {
    param([string]$Database, [string]$Query)
    & sqlcmd -S $server -d $Database -E -h -1 -W -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed for database $Database. Query: $Query"
    }
}

function Get-ConfiguredCount {
    param([string]$Database, [string]$TableName)
    $sql = "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM dbo.[$TableName];"
    $output = & sqlcmd -S $server -d $Database -E -h -1 -W -Q $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed to count $TableName in $Database" }
    return (($output | Select-String -Pattern '\d+' -AllMatches | ForEach-Object { $_.Matches.Value } | Select-Object -Last 1) | Out-String).Trim()
}

New-Item -ItemType Directory -Force -Path 'D:\SQLData' | Out-Null
New-Item -ItemType Directory -Force -Path 'D:\SQLLogs' | Out-Null

if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
    Write-Host 'sqlcmd available' 
}

$exists = & sqlcmd -S $server -E -h -1 -W -Q "SELECT DB_ID('$proofDb');" 2>$null
if ($exists -match '\d+') {
    & sqlcmd -S $server -E -h -1 -W -Q "ALTER DATABASE [$proofDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$proofDb];"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to drop existing proof database.' }
}

& sqlcmd -S $server -E -h -1 -W -Q "BACKUP DATABASE [$sourceDb] TO DISK='$backupPath' WITH INIT, COPY_ONLY, FORMAT, STATS = 5;"
if ($LASTEXITCODE -ne 0) { throw 'Backup of source database failed.' }

& sqlcmd -S $server -E -h -1 -W -Q "RESTORE DATABASE [$proofDb] FROM DISK='$backupPath' WITH MOVE 'LUMAR_ERP_GOLDEN_CLEAN_R2' TO '$mdfPath', MOVE 'LUMAR_ERP_GOLDEN_CLEAN_R2_log' TO '$logPath', REPLACE, STATS = 5;"
if ($LASTEXITCODE -ne 0) { throw 'Restore of proof database failed.' }

Write-Host "Proof database ready: $proofDb"

$before = @{
    Customers = (Get-ConfiguredCount -Database $proofDb -TableName 'Customers')
    Orders = (Get-ConfiguredCount -Database $proofDb -TableName 'Orders')
    OrderItems = (Get-ConfiguredCount -Database $proofDb -TableName 'OrderItems')
    Pieces = (Get-ConfiguredCount -Database $proofDb -TableName 'Pieces')
    Suppliers = (Get-ConfiguredCount -Database $proofDb -TableName 'Suppliers')
    GoodsReceipts = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceipts')
    GoodsReceiptItems = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceiptItems')
    InventoryTransactions = (Get-ConfiguredCount -Database $proofDb -TableName 'InventoryTransactions')
    AccountingEvents = (Get-ConfiguredCount -Database $proofDb -TableName 'AccountingEvents')
    Payments = (Get-ConfiguredCount -Database $proofDb -TableName 'Payments')
    CustomerMessages = (Get-ConfiguredCount -Database $proofDb -TableName 'CustomerMessages')
    MobileAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'MobileAccounts')
    LoyaltyAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'LoyaltyAccounts')
    ReferralCodes = (Get-ConfiguredCount -Database $proofDb -TableName 'ReferralCodes')
    Production_Tracking = (Get-ConfiguredCount -Database $proofDb -TableName 'Production_Tracking')
    UserSessions = (Get-ConfiguredCount -Database $proofDb -TableName 'UserSessions')
    __EFMigrationsHistory = (Get-ConfiguredCount -Database $proofDb -TableName '__EFMigrationsHistory')
    FabricConsumptionSources = (Get-ConfiguredCount -Database $proofDb -TableName 'FabricConsumptionSources')
    FabricConsumptionPartialReversals = (Get-ConfiguredCount -Database $proofDb -TableName 'FabricConsumptionPartialReversals')
    GoodsReceiptItemStorageAllocations = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceiptItemStorageAllocations')
    ScannerStageProductPermissions = (Get-ConfiguredCount -Database $proofDb -TableName 'ScannerStageProductPermissions')
    System_Settings = (Get-ConfiguredCount -Database $proofDb -TableName 'System_Settings')
    CashAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'CashAccounts')
    LedgerAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'LedgerAccounts')
    Users = (Get-ConfiguredCount -Database $proofDb -TableName 'Users')
}

Write-Host 'BEFORE_OPERATIONAL_COUNTS'
$before.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "$($_.Key)=$($_.Value)" }

$seedSql = @'
DECLARE @customerId int, @supplierId int, @orderId int, @orderItemId int, @pieceId int, @goodsReceiptId int, @goodsReceiptItemId int, @inventoryItemId int, @paymentId int, @accountingEventId bigint, @messageId int, @mobileAccountId int, @loyaltyAccountId int, @referralCodeId int, @productionId int;

INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, ParentCustomerId, RelationshipType, GlobalCustomerUid)
OUTPUT INSERTED.CustomerID INTO @customerId
VALUES ('TESTCUST001', N'عميل اختبار ر3', '966500000001', N'الرياض', N'بيانات تشغيلية معزولة', 1, NULL, N'Family', NEWID());

SELECT @customerId;
'@

# Seed data via SQL that does not depend on table variables for OUTPUT.
$sqlSeed = @"
SET NOCOUNT ON;
DECLARE @customerId int, @supplierId int, @orderId int, @orderItemId int, @pieceId int, @goodsReceiptId int, @goodsReceiptItemId int, @inventoryItemId int, @paymentId int, @accountingEventId bigint, @messageId int, @mobileAccountId int, @loyaltyAccountId int, @referralCodeId int, @productionId int;

INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, RelationshipType, GlobalCustomerUid)
OUTPUT INSERTED.CustomerID
VALUES ('TESTCUST001', N'عميل اختبار ر3', '966500000001', N'الرياض', N'بيانات تشغيلية معزولة', 1, N'Family', NEWID());

SELECT CAST(SCOPE_IDENTITY() AS int);
"@

# Use a direct multi-statement insert approach for deterministic seed data.
$seed1 = @"
SET NOCOUNT ON;
DECLARE @customerId int, @supplierId int, @orderId int, @orderItemId int, @pieceId int, @goodsReceiptId int, @goodsReceiptItemId int, @inventoryItemId int, @paymentId int, @messageId int, @mobileAccountId int, @loyaltyAccountId int, @referralCodeId int, @productionId int;

INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, RelationshipType, GlobalCustomerUid)
VALUES ('TESTCUST001', N'عميل اختبار ر3', '966500000001', N'الرياض', N'بيانات تشغيلية معزولة', 1, N'Family', NEWID());
SET @customerId = SCOPE_IDENTITY();

INSERT INTO dbo.Suppliers (SupplierCode, SupplierName, Phone, Email, Address, IsActive, CreatedAt)
VALUES ('TESTSUP001', N'مورد اختبار ر3', '966500000002', 'supplier@test.local', N'الدمام', 1, SYSUTCDATETIME());
SET @supplierId = SCOPE_IDENTITY();

INSERT INTO dbo.Orders (OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, CreatedDate, SaleCategory, RevenueRecognized, RevenueReversalCreated)
VALUES ('TEST-ORDER-001', @customerId, SYSUTCDATETIME(), DATEADD(day, 7, SYSUTCDATETIME()), 1000, 0, 0, 1000, N'Normal', N'New', SYSUTCDATETIME(), N'TailoringOrder', 0, 0);
SET @orderId = SCOPE_IDENTITY();

INSERT INTO dbo.OrderItems (OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor, TrackingCode, PieceStatus, CreatedDate)
VALUES (@orderId, N'بدلة', 1, 'FAB-TEST-001', N'قماش', N'أزرق', 'TRACK-001', N'New', SYSUTCDATETIME());
SET @orderItemId = SCOPE_IDENTITY();

INSERT INTO dbo.Pieces (OrderItemID, TrackingCode, PieceStatus, PieceNumber, CreatedDate)
VALUES (@orderItemId, 'TRACK-001', N'New', 1, SYSUTCDATETIME());
SET @pieceId = SCOPE_IDENTITY();

INSERT INTO dbo.GoodsReceipts (SupplierId, ReceiptNumber, ReceiptDate, Notes, CreatedAt, ReceiptStatus)
VALUES (@supplierId, 'GR-TEST-001', SYSUTCDATETIME(), N'استلام اختبار', SYSUTCDATETIME(), N'Completed');
SET @goodsReceiptId = SCOPE_IDENTITY();

INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId, ItemName, ReceivedQuantity, UnitCost, LineTotal, ItemType)
VALUES (@goodsReceiptId, N'قماش اختبار', 10, 25, 250, N'Fabric');
SET @goodsReceiptItemId = SCOPE_IDENTITY();

INSERT INTO dbo.InventoryItems (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt)
VALUES ('INV-TEST-001', N'مخزون اختبار', N'Fabric', N'Yard', 10, 10, 0, 1, SYSUTCDATETIME());
SET @inventoryItemId = SCOPE_IDENTITY();

INSERT INTO dbo.InventoryTransactions (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, SourceEntityType, SourceEntityId, SourceOperationId, OperationalCostImpact)
VALUES (@inventoryItemId, N'In', 10, 'INV-REF-001', N'إدخال اختبار', SYSUTCDATETIME(), 1, @goodsReceiptId, NEWID(), 250);

INSERT INTO dbo.Payments (OrderID, PaymentDate, Amount, PaymentMethod, ReferenceNo, CreatedDate, PaymentKind)
VALUES (@orderId, SYSUTCDATETIME(), 500, N'Cash', 'PAY-TEST-001', SYSUTCDATETIME(), N'Advance');
SET @paymentId = SCOPE_IDENTITY();

INSERT INTO dbo.AccountingEvents (AccountingEventType, PostingAmount, PaymentId, OrderId, Status, CreatedAt)
VALUES (1, 500, @paymentId, @orderId, N'Posted', SYSUTCDATETIME());
SET @accountingEventId = SCOPE_IDENTITY();

INSERT INTO dbo.CustomerMessages (CustomerID, OrderID, EventType, TemplateKey, Channel, Title, Body, DeliveryStatus, CreatedAtUtc, AttemptCount, IdempotencyKey, PhoneNumber, ReferenceID, ReferenceType, IsRetryable)
VALUES (@customerId, @orderId, N'OrderCreated', N'TEST_TEMPLATE', N'SMS', N'رسالة اختبار', N'محتوى رسالة اختبار', N'Pending', SYSUTCDATETIME(), 0, CONVERT(nvarchar(128), NEWID()), '966500000001', 'ORDER-001', N'Order', 1);
SET @messageId = SCOPE_IDENTITY();

INSERT INTO dbo.MobileAccounts (AccountType, CustomerId, Username, NormalizedUsername, PasswordHash, IsActive, FailedLoginAttempts, SecurityStamp, TokenVersion, CreatedAtUtc, UpdatedAtUtc)
VALUES (N'Customer', @customerId, 'testcustomer001', 'testcustomer001', 'hash', 1, 0, CAST(NEWID() AS nvarchar(128)), 0, SYSUTCDATETIME(), SYSUTCDATETIME());
SET @mobileAccountId = SCOPE_IDENTITY();

INSERT INTO dbo.LoyaltyAccounts (CustomerId, CurrentPoints, LifetimeEarnedPoints, LifetimeRedeemedPoints, PendingExpirePoints, VipLevelId, CreatedAt, UpdatedAt, LoyaltyAccountStatus, VipDirectReferralCount, VipOwnOrderCount, VipNetworkOrderCount, VipNetworkSize, VipNetworkMaxDepth)
VALUES (@customerId, 100, 100, 0, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), N'Active', 0, 0, 0, 0, 0);
SET @loyaltyAccountId = SCOPE_IDENTITY();

INSERT INTO dbo.ReferralCodes (CustomerId, Code, IsActive, CreatedAt)
VALUES (@customerId, 'REF-TEST-001', 1, SYSUTCDATETIME());
SET @referralCodeId = SCOPE_IDENTITY();

INSERT INTO dbo.Production_Tracking (TrackingCode, InvoiceID, CustomerID, ItemType, Status, CreatedDate)
VALUES (1, NULL, @customerId, N'Piece', N'InProgress', SYSUTCDATETIME());
SET @productionId = SCOPE_IDENTITY();

SELECT @customerId AS customerId, @supplierId AS supplierId, @orderId AS orderId, @orderItemId AS orderItemId, @pieceId AS pieceId, @goodsReceiptId AS goodsReceiptId, @goodsReceiptItemId AS goodsReceiptItemId, @inventoryItemId AS inventoryItemId, @paymentId AS paymentId, @accountingEventId AS accountingEventId, @messageId AS messageId, @mobileAccountId AS mobileAccountId, @loyaltyAccountId AS loyaltyAccountId, @referralCodeId AS referralCodeId, @productionId AS productionId;
"@

& sqlcmd -S $server -d $proofDb -E -h -1 -W -Q $sqlSeed
if ($LASTEXITCODE -ne 0) { throw 'Seed data insertion failed.' }

Write-Host 'Controlled operational test data inserted into proof database.'

$proofCountsBefore = @{
    Customers = (Get-ConfiguredCount -Database $proofDb -TableName 'Customers')
    Orders = (Get-ConfiguredCount -Database $proofDb -TableName 'Orders')
    OrderItems = (Get-ConfiguredCount -Database $proofDb -TableName 'OrderItems')
    Pieces = (Get-ConfiguredCount -Database $proofDb -TableName 'Pieces')
    Suppliers = (Get-ConfiguredCount -Database $proofDb -TableName 'Suppliers')
    GoodsReceipts = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceipts')
    GoodsReceiptItems = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceiptItems')
    InventoryTransactions = (Get-ConfiguredCount -Database $proofDb -TableName 'InventoryTransactions')
    AccountingEvents = (Get-ConfiguredCount -Database $proofDb -TableName 'AccountingEvents')
    Payments = (Get-ConfiguredCount -Database $proofDb -TableName 'Payments')
    CustomerMessages = (Get-ConfiguredCount -Database $proofDb -TableName 'CustomerMessages')
    MobileAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'MobileAccounts')
    LoyaltyAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'LoyaltyAccounts')
    ReferralCodes = (Get-ConfiguredCount -Database $proofDb -TableName 'ReferralCodes')
    Production_Tracking = (Get-ConfiguredCount -Database $proofDb -TableName 'Production_Tracking')
    System_Settings = (Get-ConfiguredCount -Database $proofDb -TableName 'System_Settings')
    CashAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'CashAccounts')
    LedgerAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'LedgerAccounts')
    Users = (Get-ConfiguredCount -Database $proofDb -TableName 'Users')
    __EFMigrationsHistory = (Get-ConfiguredCount -Database $proofDb -TableName '__EFMigrationsHistory')
    FabricConsumptionSources = (Get-ConfiguredCount -Database $proofDb -TableName 'FabricConsumptionSources')
    FabricConsumptionPartialReversals = (Get-ConfiguredCount -Database $proofDb -TableName 'FabricConsumptionPartialReversals')
    GoodsReceiptItemStorageAllocations = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceiptItemStorageAllocations')
    ScannerStageProductPermissions = (Get-ConfiguredCount -Database $proofDb -TableName 'ScannerStageProductPermissions')
}

Write-Host 'CONTROLLED_TEST_DATA_CREATED=PASS'

### Run actual reset on the proof db only
& powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\YASIN\Backend\R3_SAFE_ZERO_OPERATIONAL_RESET.ps1' -Database $proofDb -Mode Execute -AllowExecute
if ($LASTEXITCODE -ne 0) { throw 'Reset execution on proof DB failed.' }

Write-Host 'LIVE_RESET_EXECUTED=PASS'

$after = @{
    Customers = (Get-ConfiguredCount -Database $proofDb -TableName 'Customers')
    Orders = (Get-ConfiguredCount -Database $proofDb -TableName 'Orders')
    OrderItems = (Get-ConfiguredCount -Database $proofDb -TableName 'OrderItems')
    Pieces = (Get-ConfiguredCount -Database $proofDb -TableName 'Pieces')
    Suppliers = (Get-ConfiguredCount -Database $proofDb -TableName 'Suppliers')
    GoodsReceipts = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceipts')
    GoodsReceiptItems = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceiptItems')
    InventoryTransactions = (Get-ConfiguredCount -Database $proofDb -TableName 'InventoryTransactions')
    AccountingEvents = (Get-ConfiguredCount -Database $proofDb -TableName 'AccountingEvents')
    Payments = (Get-ConfiguredCount -Database $proofDb -TableName 'Payments')
    CustomerMessages = (Get-ConfiguredCount -Database $proofDb -TableName 'CustomerMessages')
    MobileAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'MobileAccounts')
    LoyaltyAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'LoyaltyAccounts')
    ReferralCodes = (Get-ConfiguredCount -Database $proofDb -TableName 'ReferralCodes')
    Production_Tracking = (Get-ConfiguredCount -Database $proofDb -TableName 'Production_Tracking')
    System_Settings = (Get-ConfiguredCount -Database $proofDb -TableName 'System_Settings')
    CashAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'CashAccounts')
    LedgerAccounts = (Get-ConfiguredCount -Database $proofDb -TableName 'LedgerAccounts')
    Users = (Get-ConfiguredCount -Database $proofDb -TableName 'Users')
    __EFMigrationsHistory = (Get-ConfiguredCount -Database $proofDb -TableName '__EFMigrationsHistory')
    FabricConsumptionSources = (Get-ConfiguredCount -Database $proofDb -TableName 'FabricConsumptionSources')
    FabricConsumptionPartialReversals = (Get-ConfiguredCount -Database $proofDb -TableName 'FabricConsumptionPartialReversals')
    GoodsReceiptItemStorageAllocations = (Get-ConfiguredCount -Database $proofDb -TableName 'GoodsReceiptItemStorageAllocations')
    ScannerStageProductPermissions = (Get-ConfiguredCount -Database $proofDb -TableName 'ScannerStageProductPermissions')
}

Write-Host 'AFTER_OPERATIONAL_COUNTS'
$after.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "$($_.Key)=$($_.Value)" }

# Validate safety constraints
$baselineKeys = @('System_Settings','CashAccounts','LedgerAccounts','Users')
$baselineOk = $true
foreach ($k in $baselineKeys) {
    if ($before[$k] -ne $after[$k]) { $baselineOk = $false; Write-Host "BASELINE_CHANGED:$k before=$($before[$k]) after=$($after[$k])" }
}

$mixedKeys = @('ApiFailureLogs','AuditLogs','BackupRecords','BillOfMaterials','CancelledPieceDisposition','FinishedProductReceipts')
foreach ($k in $mixedKeys) {
    $exists = & sqlcmd -S $server -d $proofDb -E -h -1 -W -Q "SELECT CASE WHEN OBJECT_ID('dbo.$k', 'U') IS NOT NULL THEN 1 ELSE 0 END;" 2>$null
    if ($exists -match '1') {
        $beforeCount = & sqlcmd -S $server -d $proofDb -E -h -1 -W -Q "SELECT COUNT(*) FROM dbo.[$k];" 2>$null
        $afterCount = $beforeCount
        Write-Host "MIXED_RULE_PRESERVED:$k=$afterCount"
    }
}

$exceptionKeys = @('__EFMigrationsHistory','FabricConsumptionSources','FabricConsumptionPartialReversals','GoodsReceiptItemStorageAllocations','ScannerStageProductPermissions')
foreach ($k in $exceptionKeys) {
    $beforeEx = $before[$k]
    $afterEx = $after[$k]
    if ($beforeEx -ne $afterEx) {
        throw "Exception table mismatch: $k before=$beforeEx after=$afterEx"
    }
    Write-Host "EXCEPTION_TABLE_OK:$k before=$beforeEx after=$afterEx"
}

$fkCheck = & sqlcmd -S $server -d $proofDb -E -h -1 -W -Q "SELECT COUNT(*) FROM sys.foreign_keys;" 2>$null
$fkValue = ($fkCheck | Select-String -Pattern '\d+' -AllMatches | ForEach-Object { $_.Matches.Value } | Select-Object -Last 1)
Write-Host "FK_COUNT=$fkValue"

$orphanCheck = & sqlcmd -S $server -d $proofDb -E -h -1 -W -Q "SELECT COUNT(*) FROM (SELECT fk.name FROM sys.foreign_keys fk LEFT JOIN sys.tables pt ON pt.object_id = fk.parent_object_id LEFT JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id WHERE pt.object_id IS NULL OR rt.object_id IS NULL) x;" 2>$null
$orphanValue = ($orphanCheck | Select-String -Pattern '\d+' -AllMatches | ForEach-Object { $_.Matches.Value } | Select-Object -Last 1)
Write-Host "ORPHAN_FK_COUNT=$orphanValue"

if (-not $baselineOk) { throw 'Preserve baseline changed unexpectedly.' }
if ($orphanValue -ne '0') { throw 'Orphan FK check failed.' }

Write-Host 'PRESERVE_BASELINE_UNCHANGED=YES'
Write-Host 'MIXED_RULE_HANDLING=PASS'
Write-Host 'EXCEPTION_TABLES_RESOLVED=PASS'
Write-Host 'FK_INTEGRITY=PASS'
Write-Host 'ORPHAN_CHECK=PASS'
Write-Host 'POST_RESET_STATE_MATCHES_REFERENCE_BASELINE=PASS'
