# Orders Foundation Contract

## Status

Architecture contract only. This document creates no controllers, services, repositories, EF models, migrations, database changes, screens, or operational logic.

## Order Contract

`Order` is the aggregate root for the order foundation. Its authoritative source is `LUMAR_ERP.dbo.Orders`; its primary key is `OrderID`.

| Field | Database column | Type | Nullable | Contract role |
| --- | --- | --- | --- | --- |
| `orderId` | `OrderID` | int | No | Identifier |
| `orderNumber` | `OrderNumber` | string, max 50 | No | Business number |
| `customerId` | `CustomerID` | int | No | Customer reference |
| `orderDate` | `OrderDate` | datetime | No | Order date |
| `deliveryDate` | `DeliveryDate` | datetime | Yes | Planned or actual delivery date |
| `totalAmount` | `TotalAmount` | decimal(18,2) | No | Stored total |
| `discountAmount` | `DiscountAmount` | decimal(18,2) | No | Stored discount |
| `paidAmount` | `PaidAmount` | decimal(18,2) | No | Stored paid amount |
| `remainingAmount` | `RemainingAmount` | decimal(18,2) | No | Stored balance |
| `urgencyStatus` | `UrgencyStatus` | string, max 30 | No | Stored urgency value |
| `orderStatus` | `OrderStatus` | string, max 30 | No | Stored lifecycle value |
| `notes` | `Notes` | string | Yes | Notes |
| `createdDate` | `CreatedDate` | datetime | No | Creation timestamp |
| `updatedDate` | `UpdatedDate` | datetime | Yes | Update timestamp |
| `cancellationReason` | `CancellationReason` | string, max 512 | Yes | Cancellation record |
| `cancelledAt` | `CancelledAt` | datetime | Yes | Cancellation timestamp |
| `cancelledBy` | `CancelledBy` | string, max 256 | Yes | Cancelling actor text |
| `saleCategory` | `SaleCategory` | string, max 50 | No | Sale category |
| `revenueRecognized` | `RevenueRecognized` | bool | No | Stored finance flag |
| `revenueRecognizedAt` | `RevenueRecognizedAt` | datetime | Yes | Finance timestamp |
| `revenueReversalCreated` | `RevenueReversalCreated` | bool | No | Stored finance flag |
| `revenueReversalCreatedAt` | `RevenueReversalCreatedAt` | datetime | Yes | Finance timestamp |

No field is added beyond the current schema.

## DTO Contracts

### OrderListDto

For `GET /orders` list rows.

```text
orderId: int
orderNumber: string
customerId: int
orderDate: datetime
deliveryDate: datetime?
totalAmount: decimal(18,2)
paidAmount: decimal(18,2)
remainingAmount: decimal(18,2)
urgencyStatus: string
orderStatus: string
saleCategory: string
```

### OrderDetailsDto

For `GET /orders/{id}`. It represents the complete stored `Orders` record.

```text
orderId: int
orderNumber: string
customerId: int
orderDate: datetime
deliveryDate: datetime?
totalAmount: decimal(18,2)
discountAmount: decimal(18,2)
paidAmount: decimal(18,2)
remainingAmount: decimal(18,2)
urgencyStatus: string
orderStatus: string
notes: string?
createdDate: datetime
updatedDate: datetime?
cancellationReason: string?
cancelledAt: datetime?
cancelledBy: string?
saleCategory: string
revenueRecognized: bool
revenueRecognizedAt: datetime?
revenueReversalCreated: bool
revenueReversalCreatedAt: datetime?
```

### CreateOrderDto

For `POST /orders`. It contains only database-backed order input fields. Calculated or process-controlled stored values are excluded pending their dedicated contracts.

```text
orderNumber: string
customerId: int
orderDate: datetime
deliveryDate: datetime?
discountAmount: decimal(18,2)
urgencyStatus: string
notes: string?
saleCategory: string
```

### UpdateOrderDto

For `PUT /orders/{id}`. It does not define cancellation, payment, production, delivery, or finance transitions.

```text
orderNumber: string
customerId: int
orderDate: datetime
deliveryDate: datetime?
discountAmount: decimal(18,2)
urgencyStatus: string
notes: string?
saleCategory: string
```

### OrderItemDto

Source: `OrderItems`.

```text
orderItemId: int
orderId: int
pieceType: string
quantity: int
fabricCode: string?
fabricType: string?
fabricColor: string?
request1: string?
request2: string?
notes1: string?
notes2: string?
measurementSnapshot: string?
trackingCode: string?
pieceStatus: string?
createdDate: datetime
```

### OrderFabricDto

Source: `OrderItemFabrics`.

```text
orderItemFabricId: int
orderItemId: int
inventoryItemId: int?
fabricCode: string?
fabricType: string?
fabricColor: string?
quantity: decimal(18,2)
unit: string
unitCost: decimal(18,2)
totalCost: decimal(18,2)
consumedQuantity: decimal(18,2)
createdDate: datetime
```

### OrderTrackingDto

Source: `Production_Tracking`. Its primary key `TrackingCode` is `int`, while `OrderItems.TrackingCode` is nullable `nvarchar(50)`; the current schema does not declare a direct FK between them.

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

### OrderPieceDto

Source: `Pieces`.

```text
pieceId: int
orderItemId: int
trackingCode: string
pieceStatus: string
pieceNumber: int
createdDate: datetime
```

### OrderDeliveryDto

This is a read-only projection over existing `Orders` fields; no delivery table currently exists in the referenced tables.

```text
orderId: int
orderStatus: string
deliveryDate: datetime?
```

No delivery field beyond the current `Orders` columns is introduced.

## API Contracts

All routes are rooted at `/orders`. Pagination, validation, error envelopes, transition enforcement, and mutation semantics are deferred.

| Method | Route | Response contract | Permission |
| --- | --- | --- | --- |
| GET | `/orders` | `OrderListDto[]` | `Orders.View` |
| GET | `/orders/{id}` | `OrderDetailsDto` | `Orders.View` |
| POST | `/orders` | `OrderDetailsDto` | `Orders.Create` |
| PUT | `/orders/{id}` | `OrderDetailsDto` | `Orders.Edit` |
| GET | `/orders/{id}/items` | `OrderItemDto[]` | `Orders.View` |
| GET | `/orders/{id}/pieces` | `OrderPieceDto[]` | `Orders.Tracking` |
| GET | `/orders/{id}/tracking` | `OrderTrackingDto[]` | `Orders.Tracking` |
| GET | `/orders/{id}/delivery` | `OrderDeliveryDto` | `Orders.Delivery` |

`OrderFabricDto` is nested in an item read projection or exposed by a later approved item-fabrics API. No route beyond the eight requested routes is defined here.

## Permission Contracts

| Permission | Scope |
| --- | --- |
| `Orders.View` | Read order list, details, and item data |
| `Orders.Create` | Create orders |
| `Orders.Edit` | Update permitted order profile fields |
| `Orders.Cancel` | Reserved for a future approved cancellation contract |
| `Orders.Delivery` | Read delivery projection |
| `Orders.Tracking` | Read pieces and tracking projections |

## Navigation Contract

```text
Order Tracking
|- List
|- Details
|- Pieces
|- Tracking
|- Delivery
`- History
```

This is information architecture only. No Flutter routes, screens, or widgets are created.

## Lifecycle Contract

The required conceptual lifecycle is `Created`, `Confirmed`, `InProduction`, `ReadyForDelivery`, `Delivered`, and `Cancelled`. The current database has no status enum or constraint, so the contract records observed values rather than inventing mappings.

| Conceptual state | Current observed `Orders.OrderStatus` evidence | Contract decision |
| --- | --- | --- |
| Created | `New` (42 rows) | `New` is the observed initial stored value |
| Confirmed | No `Confirmed` value observed | Not mapped; requires later approval |
| InProduction | `InProduction` (4 rows) | Direct observed value |
| ReadyForDelivery | `ReadyForDelivery` (4 rows) | Direct observed value |
| Delivered | `Delivered` (44 rows) | Direct observed value |
| Cancelled | `Cancelled` (10 rows) | Direct observed value |

Additional observed order values are `Started` (6), `Paid` (12), and `PartialPaid` (5). They are not mapped to the requested delivery lifecycle: `Started` needs a lifecycle decision, while `Paid` and `PartialPaid` appear to be payment states and must not be treated as production or delivery stages without approval.

Related current values:

| Source | Values observed |
| --- | --- |
| `OrderItems.PieceStatus` | `New` (91), `Delivered` (41) |
| `Pieces.PieceStatus` | `New` (72), `Cutting` (4), `Printing` (3), `InProduction` (1), `Ready` (10), `Delivered` (20) |
| `Production_Tracking.Status` | `تمت الطباعة` (1), `جاهز للاستلام` (1) |
| `Orders.UrgencyStatus` | `Normal` (115), `Medium` (6), `Urgent` (6) |

No transition logic, state change endpoint, or status normalization is defined by this contract.

## Relationships With Other Modules

| Area | Current database evidence | Contract boundary |
| --- | --- | --- |
| Customer | `Orders.CustomerID` exists, but no declared FK to `Customers.CustomerID` was found | Customer is referenced by identifier only; no customer fields are embedded |
| Order -> Item | Declared FK `OrderItems.OrderID` -> `Orders.OrderID` | Item records belong to the order |
| Item -> Fabric | Declared FK `OrderItemFabrics.OrderItemID` -> `OrderItems.OrderItemID` with cascade | Fabric allocation belongs to an item |
| Item -> Inventory | Declared FK `OrderItemFabrics.InventoryItemID` -> `InventoryItems.InventoryItemID` with `SET_NULL` | Inventory remains an external foundation |
| Item -> Piece | Declared FK `Pieces.OrderItemID` -> `OrderItems.OrderItemID` | Piece records belong to an item |
| Tracking | `Production_Tracking` relates to `Customers` and `Invoice_Header`, and is referenced by invoices, scans, and measurements; no declared direct order FK exists | Tracking is exposed as a read projection only until the key mapping is approved |
| Ready-made production | `ReadyMadeProductionOrders` -> items -> piece instances has internal declared FKs; no declared FK to `Orders` was found | Separate production aggregate; do not assume it belongs to a customer order |
| Production | Pieces and tracking support production visibility; production orders and materials remain outside this contract | No production command is defined |
| Payroll | `PieceWageRecords` has declared FKs to `Orders`, `OrderItems`, and `Pieces` | Payroll owns wage calculations; order contract exposes no wage data |
| Finance | `Payments.OrderID`, `Invoice_Header.OrderID`, loyalty and referral transaction order references have declared FKs to `Orders` | Finance and loyalty own payment/revenue rules; order contract exposes stored financial totals only |
| Inventory | `OrderItemFabrics` references inventory items | Inventory owns stock movement and costing rules |

## Architecture Notes

- `LUMAR_ERP` remains the source of truth. No database schema or data change is implied.
- `OrderDeliveryDto` is deliberately read-only because the referenced schema has no dedicated delivery entity.
- The type mismatch and absence of a declared FK between order item tracking text and `Production_Tracking.TrackingCode` must be resolved by a separate data-governance contract before a reliable order tracking query is implemented.
- `OrderDetailsDto` does not embed items, pieces, tracking, payments, payroll, or inventory to keep API boundaries explicit.
- `ReadyMadeProductionOrders` are included as a documented adjacent aggregate, not as child data of `Order`; the current schema provides no declared relationship to `Orders`.
- Cancellation, payment updates, delivery confirmation, production transitions, inventory consumption, wage generation, and revenue recognition each require separate approved contracts before implementation.
