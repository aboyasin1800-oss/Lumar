# Customers Foundation Contract

## Status

Architecture contract only. This document defines no controllers, services, repositories, database changes, migrations, or user-interface implementation.

## Customer Entity Contract

`Customer` is the aggregate root for the Customers foundation. Its authoritative data source is `LUMAR_ERP.dbo.Customers`.

| Field | Database column | Type | Nullable | Contract role |
| --- | --- | --- | --- | --- |
| `customerId` | `CustomerID` | int | No | Identifier |
| `customerCode` | `CustomerCode` | string, max 20 | Yes | Business code |
| `customerName` | `CustomerName` | string, max 100 | Yes | Display name |
| `phoneNumber` | `PhoneNumber` | string, max 20 | Yes | Contact number |
| `parentCustomerCode` | `ParentCustomerCode` | string, max 20 | Yes | Parent or related customer code |
| `totalPoints` | `TotalPoints` | decimal(18,2) | Yes | Current points summary |
| `totalPieces` | `TotalPieces` | int | Yes | Current pieces summary |
| `totalDebts` | `TotalDebts` | decimal(10,2) | Yes | Current debt summary |
| `relationshipType` | `RelationshipType` | string, max 100 | Yes | Relationship classification |

No field is added beyond the current database schema.

## DTO Contracts

All names use the requested contract names. Field casing can be adapted by API serialization policy later without changing the contract meaning.

### CustomerListDto

For `GET /customers` list rows.

```text
customerId: int
customerCode: string?
customerName: string?
phoneNumber: string?
totalPoints: decimal?
totalPieces: int?
totalDebts: decimal?
relationshipType: string?
```

### CustomerDetailsDto

For `GET /customers/{id}`. It represents the current `Customers` columns only.

```text
customerId: int
customerCode: string?
customerName: string?
phoneNumber: string?
parentCustomerCode: string?
totalPoints: decimal?
totalPieces: int?
totalDebts: decimal?
relationshipType: string?
```

### CreateCustomerDto

For `POST /customers`. It accepts only writable fields already present in `Customers`; generated `CustomerID` and stored summary values are excluded.

```text
customerCode: string?
customerName: string?
phoneNumber: string?
parentCustomerCode: string?
relationshipType: string?
```

### UpdateCustomerDto

For `PUT /customers/{id}`. It accepts the same database-backed customer profile fields as creation.

```text
customerCode: string?
customerName: string?
phoneNumber: string?
parentCustomerCode: string?
relationshipType: string?
```

### CustomerMeasurementDto

Source: `CustomerMeasurements`.

```text
id: int
customerId: int
pieceType: string
measurementName: string
measurementValue: decimal(18,2)
createdAtUtc: datetime
revisionNumber: int
```

### CustomerLedgerEntryDto

Source: `CustomerLedgerEntries`.

```text
customerLedgerEntryId: int
customerId: int
referenceNumber: string
debitAmount: decimal(18,2)
creditAmount: decimal(18,2)
balanceAfterTransaction: decimal(18,2)
createdAt: datetime
```

### CustomerLoyaltyDto

Source: `LoyaltyAccounts`, with optional `VipLevels` display data.

```text
loyaltyAccountId: int
customerId: int
currentPoints: decimal(18,2)
lifetimeEarnedPoints: decimal(18,2)
lifetimeRedeemedPoints: decimal(18,2)
pendingExpirePoints: decimal(18,2)
vipLevelId: int?
vipLevelCode: string?
vipLevelDisplayName: string?
createdAt: datetime
updatedAt: datetime
lastActivityAt: datetime?
```

### CustomerReferralDto

Source: `ReferralAccounts` and the customer-owned `ReferralCodes` record. It does not include referral transaction history because it was not requested as a customer endpoint.

```text
referralAccountId: int
customerId: int
referralCodeId: int?
referralCode: string?
referralCodeIsActive: bool?
totalReferrals: int
successfulReferrals: int
totalRewardsAmount: decimal(18,2)
totalRewardPoints: decimal(18,2)
createdAt: datetime
updatedAt: datetime
referralCodeCreatedAt: datetime?
referralCodeLastUsedAt: datetime?
```

## API Contracts

All routes are rooted at `/customers`. Response envelopes, pagination, validation behavior, error formats, and authorization enforcement are intentionally deferred.

| Method | Route | Response contract | Required permission |
| --- | --- | --- | --- |
| GET | `/customers` | `CustomerListDto[]` | `Customers.View` |
| GET | `/customers/{id}` | `CustomerDetailsDto` | `Customers.View` |
| POST | `/customers` | `CustomerDetailsDto` | `Customers.Create` |
| PUT | `/customers/{id}` | `CustomerDetailsDto` | `Customers.Edit` |
| GET | `/customers/{id}/measurements` | `CustomerMeasurementDto[]` | `Customers.View` |
| GET | `/customers/{id}/ledger` | `CustomerLedgerEntryDto[]` | `Customers.View` |
| GET | `/customers/{id}/loyalty` | `CustomerLoyaltyDto` | `Customers.Loyalty` |
| GET | `/customers/{id}/referrals` | `CustomerReferralDto` | `Customers.Referrals` |

`Customers.Delete` is reserved as a permission contract. No delete endpoint is defined in this phase.

## Permission Contracts

| Permission | Scope |
| --- | --- |
| `Customers.View` | Read customer list, details, measurements, and ledger |
| `Customers.Create` | Create a customer profile |
| `Customers.Edit` | Update a customer profile |
| `Customers.Delete` | Reserved for a future approved deletion or deactivation contract |
| `Customers.Loyalty` | Read customer loyalty data |
| `Customers.Referrals` | Read customer referral data |

## Navigation Contract

```text
Customers
|- List
|- Details
|- Measurements
|- Ledger
|- Loyalty
`- Referrals
```

This is information architecture only; no Flutter routes, screens, widgets, or navigation code are created.

## Relationships With Other Modules

| Relationship | Current evidence | Contract implication |
| --- | --- | --- |
| Customers -> Orders | `Orders.CustomerID` is the customer identifier in order rows; no declared FK was found in the current schema | Orders remain outside this foundation; customer detail does not embed orders |
| Orders -> Payments | `Payments.OrderID` has declared FK to `Orders.OrderID` | Payments are reached through orders, not directly from `Customers` |
| Customers -> CustomerLedgerEntries | Declared FK `CustomerLedgerEntries.CustomerID` -> `Customers.CustomerID`, cascade delete | Ledger endpoint reads entries by `CustomerID` |
| Customers -> LoyaltyAccounts | Declared FK `LoyaltyAccounts.CustomerId` -> `Customers.CustomerID`, cascade delete | Loyalty endpoint reads account by `CustomerId` |
| LoyaltyAccounts -> VipLevels | Declared FK `LoyaltyAccounts.VipLevelId` -> `VipLevels.VipLevelId` | Loyalty DTO may expose only the existing VIP display fields listed above |
| Customers -> ReferralAccounts | Declared FK `ReferralAccounts.CustomerId` -> `Customers.CustomerID`, cascade delete | Referral endpoint reads summary by `CustomerId` |
| Customers -> ReferralCodes | Declared FK `ReferralCodes.CustomerId` -> `Customers.CustomerID`, cascade delete | Referral DTO may expose code fields |
| ReferralAccounts -> ReferralCodes | Declared FK `ReferralAccounts.ReferralCodeId` -> `ReferralCodes.ReferralCodeId` | Referral summary can pair account with code |
| CustomerMeasurements | Contains `CustomerId` but no declared FK was found | Measurement reads must treat the link as logical until schema governance approves a change |
| CustomerMessages and CustomerNotifications | Contain `CustomerID` and `OrderID` but no declared FK was found | They remain separate activity records and are outside the requested customer API surface |

## Architecture Notes

- `LUMAR_ERP` remains the source of truth. No schema or data change is implied by these contracts.
- Customer profile fields and stored totals are represented as current database values. No recalculation rule is defined here.
- `CustomerCode` is nullable in the current schema; uniqueness and requiredness are not assumed without an approved database-constraint review.
- `CustomerMeasurements.CustomerId` is a logical relationship, not a database-enforced FK in the current schema.
- Orders and payments are deliberately excluded from `CustomerDetailsDto` to avoid coupling the foundation contract to order reconstruction.
- Customer messages and notifications are activity data; a future activity contract must decide their query boundary.
- Field validation, pagination, deletion policy, audit behavior, and concurrency policy require separate approved contracts before implementation.
