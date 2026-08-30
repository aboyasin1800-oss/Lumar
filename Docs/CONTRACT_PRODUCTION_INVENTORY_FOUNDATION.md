# Production and Inventory Foundation Contract

## Status

Architecture contract only, derived from the current `LUMAR_ERP` schema and stored values. This document creates no controllers, services, repositories, EF models, DbContexts, migrations, database changes, screens, Flutter code, builds, or business logic.

## Piece Lifecycle

`Pieces` is the per-customer-order piece record, identified by `PieceID`. Its declared parent is `OrderItems` through `Pieces.OrderItemID -> OrderItems.OrderItemID`.

The current schema has no workflow definition or state constraint. The following are the actual stored values, not proposed transitions:

| Source | Actual stored values | Evidence |
| --- | --- | --- |
| `Pieces.PieceStatus` | `New`, `Cutting`, `Printing`, `InProduction`, `Ready`, `Delivered` | 72, 4, 3, 1, 10, 20 rows respectively |
| `OrderItems.PieceStatus` | `New`, `Delivered` | 91, 41 rows |
| `TrackingEvents.Stage` | `OrderCreated`, `Cutting`, `Printing`, `Sewing`, `Buttons`, `Ironing`, `Quality`, `Assembly`, `Ready`, `Delivery`, `Production`, `Reversal` | Stored events |
| `TrackingEvents.Status` | `New`, `Cutting`, `Printing`, `Sewing`, `Buttons`, `Ironing`, `Quality`, `Assembly`, `Ready`, `Delivered`, `InProduction`, `InProgress`, `ReadyForSale` | Stored events |
| `Production_Tracking.Status` | `تمت الطباعة`, `جاهز للاستلام` | 2 rows |

The recorded lifecycle evidence begins with `OrderCreated` in `TrackingEvents`, then contains the operational stages above. `ReadyForDelivery` is not stored as a `Pieces.PieceStatus` or `TrackingEvents.Status`; `Ready` and `جاهز للاستلام` are the closest existing values but are not mapped to `ReadyForDelivery` by this contract. A future state-governance contract must define legal transitions.

## DTO Contracts

Field names below correspond only to existing database columns. Nullable values are marked `?`.

### PieceDto

Source: `Pieces`.

```text
pieceId: int
orderItemId: int
trackingCode: string
pieceStatus: string
pieceNumber: int
createdDate: datetime
```

### PieceTrackingDto

Source: `Production_Tracking`.

```text
trackingCode: int
invoiceId: int?
customerId: int?
itemType: string?
status: string?
cuttingEmployee: string?
sewingEmployee: string?
ironingEmployee: string?
createdDate: datetime?
cuttingDate: datetime?
sewingDate: datetime?
ironingDate: datetime?
isCompleted: bool?
isDelivered: bool?
deliveryDate: datetime?
isOnHold: bool?
holdReason: string?
```

### ProductionStageDto

Read-only stage vocabulary from `TrackingEvents.Stage` and `TrackingEvents.Status`.

```text
stage: string
status: string
```

No stage identifier, display order, or transition rule exists in the current reference tables.

### ScannerDto

Source: `Scanners`.

```text
scannerId: int
scannerCode: string
scannerName: string
description: string?
isActive: bool
createdAt: datetime
updatedAt: datetime?
```

### LiveScanDto

Source: `Live_Scan`.

```text
id: int
trackingCode: int?
scanTime: datetime?
```

### TrackingEventDto

Source: `TrackingEvents`.

```text
trackingEventId: int
orderItemId: int?
orderId: int?
trackingCode: string?
stage: string
status: string
eventTime: datetime
employeeCode: string?
notes: string?
isReverted: bool
revertedAt: datetime?
pieceId: int?
readyMadeProductionOrderPieceInstanceId: int?
```

### PieceWageDto

Source: `PieceWageRecords`.

```text
pieceWageRecordId: int
orderId: int
orderItemId: int
pieceId: int
trackingEventId: int
employeeId: int?
employeeCode: string?
pieceType: string
stage: string
quantity: decimal(18,2)
wageRate: decimal(18,2)
totalWage: decimal(18,2)
payrollPeriodId: int?
payrollRecordId: int?
status: string
notes: string?
createdAt: datetime
```

### PieceWageRateDto

Source: `PieceWageRates`.

```text
pieceWageRateId: int
pieceType: string
stage: string
wageRate: decimal(18,2)
isActive: bool
notes: string?
createdAt: datetime
updatedAt: datetime?
```

### InventoryItemDto

Source: `InventoryItems`.

```text
inventoryItemId: int
itemCode: string
itemName: string
category: string
unit: string
currentQuantity: decimal(18,2)
availableQuantity: decimal(18,2)
reservedQuantity: decimal(18,2)
isActive: bool
createdAt: datetime
updatedAt: datetime?
barcode: string?
fabricCategory: string?
fabricColor: string?
fabricWidth: decimal(18,4)?
fabricWidthUnit: string?
inchPrice: decimal(18,6)?
yardPrice: decimal(18,4)?
```

### InventoryTransactionDto

Source: `InventoryTransactions`.

```text
transactionId: int
inventoryItemId: int
transactionType: string
quantity: decimal(18,2)
referenceNumber: string?
notes: string?
createdAt: datetime
totalCostImpact: decimal(18,2)?
unitCost: decimal(18,2)?
```

### FabricDto

This contract distinguishes the two existing fabric tables; they have incompatible `FabricCode` types and no declared FK between them.

```text
fabricId: int
fabricCode: string
fabricName: string
fabricPrice: decimal(18,2)
isActive: bool
```

```text
inventoryFabricCode: int
inventoryFabricName: string?
unit: string?
color: string?
quantityYard: decimal(10,2)?
quantityInch: decimal(10,2)?
totalRollCost: decimal(18,2)?
pricePerYard: decimal(18,2)?
pricePerInch: decimal(18,2)?
usedQuantity: decimal(10,2)?
availableQuantity: decimal(10,2)?
```

### ReadyMadeProductDto

Source: `ReadyMadeInventoryProducts`.

```text
readyMadeInventoryProductId: int
readyMadeProductionOrderId: int
readyMadeProductionOrderItemId: int
readyMadeProductionOrderPieceInstanceId: int
productionOrderNumber: string
productionName: string
pieceType: string
pieceNumber: int
trackingCode: string
fabricCode: string?
fabricType: string?
fabricColor: string?
catalogNumber: string?
fabricUnit: string?
fabricWidth: string?
fabricWidthUnit: string?
actualCost: decimal(18,4)?
suggestedSellingPrice: decimal(18,4)?
measurementSnapshot: string?
readyForSaleAt: datetime
status: string
source: string
notes: string?
isActive: bool
createdAt: datetime
```

### ReadyMadeOrderDto

Source: `ReadyMadeProductionOrders`, with its existing item and piece-instance child records represented separately by their table columns.

```text
readyMadeProductionOrderId: int
productionOrderNumber: string
productionName: string
totalCost: decimal(18,4)
profitPercentage: decimal(18,4)
suggestedSellingPrice: decimal(18,4)
status: string
notes: string?
createdAt: datetime
```

Child table field sets:

```text
ReadyMadeProductionOrderItems:
readyMadeProductionOrderItemId, readyMadeProductionOrderId, pieceType, quantity,
fabricCode?, fabricType?, fabricColor?, catalogNumber?, fabricUnit?, fabricWidth?,
fabricWidthUnit?, inchPrice?, fabricCost?, pieceCost?, lineTotal?, consumption?,
request1?, request2?, notes1?, notes2?, measurementSnapshot?, pieceStatus, createdAt

ReadyMadeProductionOrderPieceInstances:
readyMadeProductionOrderPieceInstanceId, readyMadeProductionOrderItemId,
pieceNumber, trackingCode, pieceStatus, createdAt
```

## API Contracts

Only the requested read routes are defined. Pagination, filtering, response envelopes, authorization, and query semantics remain separate contracts.

| Method | Route | Response |
| --- | --- | --- |
| GET | `/production/pieces` | `PieceDto[]` |
| GET | `/production/pieces/{id}` | `PieceDto` |
| GET | `/production/pieces/{id}/tracking` | `PieceTrackingDto[]` and/or `TrackingEventDto[]`; exact join rule deferred |
| GET | `/production/stages` | `ProductionStageDto[]` |
| GET | `/production/scanners` | `ScannerDto[]` |
| GET | `/production/live-scan` | `LiveScanDto[]` |
| GET | `/production/pieces/{id}/wages` | `PieceWageDto[]` |
| GET | `/inventory/items` | `InventoryItemDto[]` |
| GET | `/inventory/items/{id}` | `InventoryItemDto` |
| GET | `/inventory/transactions` | `InventoryTransactionDto[]` |
| GET | `/inventory/fabrics` | `FabricDto[]` |
| GET | `/inventory/readymade` | `ReadyMadeProductDto[]` |
| GET | `/inventory/imported` | Existing `ImportedReadyMadeProducts` field projection; no requested DTO name exists |

## Production Stages

| Candidate stage | Current evidence | Decision |
| --- | --- | --- |
| Printing | `TrackingEvents.Stage`, `PieceWageRecords.Stage`, `Pieces.PieceStatus` | Present |
| Cutting | `TrackingEvents.Stage`, `PieceWageRecords.Stage`, `Pieces.PieceStatus` | Present |
| Sewing | `TrackingEvents.Stage`, `PieceWageRecords.Stage` | Present |
| Buttons | `TrackingEvents.Stage`, `PieceWageRecords.Stage` | Present |
| Ironing | `TrackingEvents.Stage`, `PieceWageRecords.Stage` | Present |
| Quality | `TrackingEvents.Stage`, `PieceWageRecords.Stage` | Present |
| Assembly | `TrackingEvents.Stage`, `PieceWageRecords.Stage`, ready-made piece status | Present |
| Ready | `TrackingEvents.Stage`, `PieceWageRecords.Stage`, `Pieces.PieceStatus` | Present |
| Delivery | `TrackingEvents.Stage`, `PieceWageRecords.Stage` | Present |

`OrderCreated`, `Production`, and `Reversal` also appear in `TrackingEvents.Stage`; this contract records them but does not label them as production work stages.

## Scanner Architecture

| Table | Current role | Declared relationship |
| --- | --- | --- |
| `Scanners` | Scanner reference registry, 9 rows | No FK to employee, stage, piece, or tracking record |
| `Live_Scan` | Time-stamped scan of nullable integer `TrackingCode`, 5 rows | Declared FK `TrackingCode -> Production_Tracking.TrackingCode` |
| `Production_Tracking` | Tracking record with assignment fields for cutting, sewing, and ironing employee codes | FKs to `Customers` and `Invoice_Header`; no Scanner FK |
| `TrackingEvents` | Stage/status event with optional `EmployeeCode` and optional piece/order references | FKs to `Orders`, `OrderItems`, `Pieces`, and ready-made piece instances; no Scanner FK |

Current schema conclusion: a scanner cannot be linked to a specific employee, stage, or piece by a declared database relationship. `Live_Scan` links only to `Production_Tracking` through `TrackingCode`; employee and stage evidence belongs to other records and must not be inferred as scanner assignment.

## Piece Wage Flow

- The wage record is `PieceWageRecords`, with declared FKs to `Orders`, `OrderItems`, `Pieces`, `TrackingEvents`, `PayrollPeriods`, and optionally `PayrollRecords` and `Employees`.
- The stored wage point is the creation of `PieceWageRecords`, which persists `Stage`, `Quantity`, `WageRate`, and `TotalWage`.
- `PieceWageRates` supplies existing rate reference fields by `PieceType` and `Stage`, but the schema declares no FK from wage records to rate records.
- `PayrollRecords.PieceWageAmount` is the payroll summary field; its declared relationship is to employee and payroll period, not directly to each wage record.
- `TrackingEvents` is the declared event reference through `PieceWageRecords.TrackingEventID`; this is the current strongest database-enforced link between tracking and wage records.
- The schema does not define the command order, calculation formula, or status-update trigger. Those are explicitly deferred.

## Inventory Contracts and Warehouse Classification

### Fabric warehouse

| Aspect | Current evidence |
| --- | --- |
| Tables | `Fabrics`, `Fabrics_Inventory`, and fabric-capable `InventoryItems` |
| Relationships | `Invoice_Details.FabricCode -> Fabrics_Inventory.FabricCode`; no FK between `Fabrics` and `Fabrics_Inventory` |
| Movements | No dedicated fabric transaction table in the referenced set; `OrderItemFabrics` tracks allocated/consumed quantities and references `InventoryItems` |
| Sale point | Invoice detail references `Fabrics_Inventory`; no sale workflow is defined here |
| Stocktake point | `Fabrics_Inventory.QuantityYard`, `QuantityInch`, `UsedQuantity`, and `AvailableQuantity`; no stocktake transaction exists in the reference set |

### LUMAR ready-made products warehouse

| Aspect | Current evidence |
| --- | --- |
| Tables | `ReadyMadeProductionOrders`, `ReadyMadeProductionOrderItems`, `ReadyMadeProductionOrderPieceInstances`, `ReadyMadeInventoryProducts` |
| Relationships | Order -> item -> piece instance; inventory product references all three with declared FKs |
| Movements | No dedicated ready-made movement table is in the reference set |
| Sale point | `ReadyMadeInventoryProducts.ReadyForSaleAt`, `Status`, and `SuggestedSellingPrice` indicate sale readiness; no sale transaction link is declared |
| Stocktake point | `ReadyMadeInventoryProducts.IsActive`, `Status`, and one row per piece instance; no explicit count field or stocktake table exists |

### Imported products warehouse

| Aspect | Current evidence |
| --- | --- |
| Tables | `ImportedReadyMadeProducts` |
| Relationships | No declared FK in the reference set |
| Movements | No dedicated movement table exists in the reference set |
| Sale point | `SellingPrice`; no sales FK is declared |
| Stocktake point | `Quantity` and optional `AlertThreshold`; no stocktake table exists |

`InventoryItems` plus `InventoryTransactions` remains the only declared general inventory movement pair: `InventoryTransactions.InventoryItemID -> InventoryItems.InventoryItemID` with cascade delete.

## Navigation Contracts

```text
Production
|- Piece Tracking
|- Tracking
|- Scanners
|- Live Scan
|- Production Stages
|- Piece Wages
|- Ready-Made Production
`- Delivery
```

```text
Inventory
|- Fabric Warehouse
|- LUMAR Product Inventory
|- Imported Product Inventory
|- Transactions
|- Stocktake
`- Reports
```

These are navigation contracts only; no UI routes, screens, or widgets are created.

## Cross-Module Relationships and Effects

| Module | Current relationship | Architectural effect of production change |
| --- | --- | --- |
| Orders | Pieces -> OrderItems; TrackingEvents -> Orders/OrderItems; wage records -> orders/items | Piece and tracking changes can affect customer order visibility and delivery interpretation |
| Payroll | Wage records optionally reference payroll records and periods; payroll has `PieceWageAmount` | Wage changes require a separately approved payroll reconciliation contract |
| Finance | `Production_Tracking` references invoice; `ReadyMadeInventoryProducts` stores cost and suggested price | Production status is not itself a finance posting rule in this schema |
| Inventory | Order-item fabrics and production material consumption reference inventory items | Consumption or ready-made availability changes need an inventory consistency contract |
| Customers | `Production_Tracking.CustomerID` references `Customers`; orders have `CustomerID` without a declared FK | Production visibility can be customer-facing, but the mapping key to pieces is not fully enforced |

## Architecture Risks

- `Pieces.TrackingCode` is `nvarchar(50)`, while `Production_Tracking.TrackingCode` and `Live_Scan.TrackingCode` are `int`; there is no declared FK from pieces or order items to production tracking.
- `Scanners` has no relationship to scans, employees, stages, or pieces. The current schema cannot reliably attribute a live scan to a device or employee.
- `Fabrics.FabricCode` is text and `Fabrics_Inventory.FabricCode` is integer, with no declared relationship; they cannot be joined by an assumed code contract.
- Stage/status values are free text with overlapping but nonidentical vocabularies, including Arabic tracking statuses. No constraint guarantees a legal lifecycle.
- The imported and ready-made stores lack dedicated stock movement and stocktake records in the referenced set.
- `Production_Tracking` relates to invoice/customer but lacks a declared direct FK to orders, order items, or pieces.

## Final Recommendation

Preserve the current schema as the sole source of truth and implement no production or inventory workflow until a separate data-governance contract resolves the tracking key type mismatch, scanner attribution gap, fabric-code mismatch, and lifecycle vocabulary. The first future implementation should be read-only projections based only on declared FKs, with ambiguous joins explicitly excluded.
