# Business Foundation Contract

## Status

Architecture contracts only, based on the current `LUMAR_ERP` schema and stored values. This document creates no controllers, services, repositories, EF models, DbContexts, migrations, database changes, Flutter code, screens, or business logic.

## Lifecycle Evidence

The database does not provide state machines or transition constraints. The following are observed values or column-supported lifecycles, not approved workflow implementations.

| Area | Current evidence | Contract boundary |
| --- | --- | --- |
| Employee | `Employees.Status = Active` for 11 current rows; `HireDate`, `TerminationDate`, `IsActive` exist | No inactive, terminated, or other stored status observed |
| Attendance | Date, check-in/out, worked/overtime hours, absence flag/reason | No approval lifecycle table or field exists |
| Leave | Requested dates, status, approval actor/time | No current status values were queried; transitions are undefined |
| Draw | `Employee_Draws` has draw date/amount; settlement has date/amount and optional `JournalEntryId` | Draw-to-settlement relationship is declared by `DrawID` |
| Payroll period | `Draft` (2), `Generated` (6), `Paid` (2) | No `Approved` stored status observed, though `ApprovedAt` exists |
| Payroll record | `Calculated` (18) | No payment workflow field is defined on the record |
| Piece wage | `PendingPayroll` (150) | Rate, quantity, total, stage and optional payroll links exist; no formula contract exists |
| Finance | Reference number, transaction type, amount, date; journal header/lines | No financial transaction status field exists |
| Loyalty | Transaction types `Earn` (75), `Redeem` (1), `Adjust` (30), `Reversal` (5) | Account balances and transaction snapshots are stored |
| Referral | Transaction types `Registration` (5), `RewardGranted` (27), `RewardReversal` (4) | No approval state field exists |
| Login | Mobile accounts store active/lockout/token-version data; sessions store expiry/revocation | There are no mobile account records currently, so no observed account-type values |
| Recovery | Challenge created/expiry, failed/max attempts, used/revoked timestamps | No recovery execution status field exists |

## DTO Contracts

All DTO fields map to existing columns only. `?` marks nullable database columns. Sensitive credential material is excluded from response contracts.

### EmployeeDto

```text
employeeId: int
employeeCode: string
employeeName: string
jobTitle: string?
scannerCode: string?
phoneNumber: string?
baseSalary: decimal(10,2)?
notes: string?
isActive: bool?
salaryType: string?
fixedSalary: decimal(18,2)?
fullName: string
nationalId: string?
phone: string?
email: string?
address: string?
hireDate: datetime
terminationDate: datetime?
status: string
departmentId: int
basicSalary: decimal(18,2)
pieceWageRate: decimal(18,2)
overtimeHourlyRate: decimal(18,2)
createdAt: datetime
updatedAt: datetime?
```

### AttendanceDto

```text
employeeAttendanceId: int
employeeId: int
attendanceDate: datetime
checkInTime: datetime?
checkOutTime: datetime?
workedHours: decimal(18,2)
overtimeHours: decimal(18,2)
isAbsent: bool
absenceReason: string?
notes: string?
createdAt: datetime
```

### LeaveRequestDto

```text
leaveRequestId: int
employeeId: int
leaveType: string
startDate: datetime
endDate: datetime
requestedDays: decimal(10,2)
status: string
reason: string?
approvedBy: string?
approvedAt: datetime?
createdAt: datetime
```

### EmployeeDrawDto

```text
drawId: int
employeeCode: string?
drawDate: datetime?
amount: decimal(18,2)?
notes: string?
```

### EmployeeDrawSettlementDto

```text
settlementId: int
drawId: int
employeeCode: string?
settlementDate: datetime
amount: decimal(18,2)
notes: string?
journalEntryId: int?
```

### PayrollRecordDto

```text
payrollRecordId: int
payrollPeriodId: int
employeeId: int
basicSalaryAmount: decimal(18,2)
pieceWageAmount: decimal(18,2)
attendanceAdjustmentAmount: decimal(18,2)
overtimeAmount: decimal(18,2)
grossAmount: decimal(18,2)
deductionsAmount: decimal(18,2)
netAmount: decimal(18,2)
status: string
notes: string?
createdAt: datetime
```

`PayrollPeriods` fields: `payrollPeriodId`, `periodCode`, `startDate`, `endDate`, `status`, `notes?`, `generatedAt?`, `approvedAt?`, `createdAt`. `PayrollItems` fields: `payrollItemId`, `payrollRecordId`, `itemType`, `itemName`, `quantity`, `rate`, `amount`, `notes?`.

### PieceWageDto

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

`PieceWageRates` fields: `pieceWageRateId`, `pieceType`, `stage`, `wageRate`, `isActive`, `notes?`, `createdAt`, `updatedAt?`.

### FinancialTransactionDto

```text
financialTransactionId: int
referenceNumber: string
transactionType: string
amount: decimal(18,2)
description: string?
createdAt: datetime
```

### JournalEntryDto

```text
journalEntryId: int
referenceNumber: string
description: string?
entryDate: datetime
createdAt: datetime
```

`JournalEntryLines` fields: `journalEntryLineId`, `journalEntryId`, `ledgerAccountId`, `debitAmount`, `creditAmount`, `description?`. `LedgerAccounts` fields: `ledgerAccountId`, `accountCode`, `accountName`, `accountType`, `isActive`, `createdAt`, `updatedAt?`. `CashAccounts` fields: `cashAccountId`, `accountName`, `currentBalance`, `isActive`, `createdAt`.

### LoyaltyAccountDto

```text
loyaltyAccountId: int
customerId: int
currentPoints: decimal(18,2)
lifetimeEarnedPoints: decimal(18,2)
lifetimeRedeemedPoints: decimal(18,2)
pendingExpirePoints: decimal(18,2)
vipLevelId: int?
createdAt: datetime
updatedAt: datetime
lastActivityAt: datetime?
```

### LoyaltyTransactionDto

```text
loyaltyTransactionId: long
loyaltyAccountId: int
customerId: int
orderId: int?
rewardId: int?
transactionType: string
points: decimal(18,2)
balanceBefore: decimal(18,2)
balanceAfter: decimal(18,2)
source: string
notes: string?
createdAt: datetime
```

`LoyaltyRules`, `LoyaltyRewards`, and `LoyaltyRedemptions` remain supporting reference/transaction tables. Their columns are not duplicated into these primary contracts.

### ReferralAccountDto

```text
referralAccountId: int
customerId: int
referralCodeId: int?
totalReferrals: int
successfulReferrals: int
totalRewardsAmount: decimal(18,2)
totalRewardPoints: decimal(18,2)
createdAt: datetime
updatedAt: datetime
```

### ReferralTransactionDto

```text
referralTransactionId: long
referrerCustomerId: int
referredCustomerId: int?
referralCodeId: int?
orderId: int?
referralRewardId: int?
transactionType: string
fixedRewardAmount: decimal(18,2)
loyaltyPoints: decimal(18,2)
notes: string?
createdAt: datetime
```

`ReferralCodes`, `ReferralRewards`, and `ReferralAnalytics` remain supporting code, reward, and snapshot tables.

### UserDto

Source: `Users`. `UserPassword` is deliberately excluded: it is sensitive legacy credential data and must never be returned by an API contract.

```text
userId: int
username: string
fullName: string
userRole: string
isActive: bool
createdDate: datetime
```

### RoleDto

```text
securityRoleId: int
roleCode: string
roleName: string
parentRoleCode: string?
isSystemRole: bool
isActive: bool
createdAtUtc: datetime
```

### PermissionDto

```text
securityPermissionId: int
permissionCode: string
permissionName: string
moduleCode: string
category: string
actionCode: string
isActive: bool
createdAtUtc: datetime
```

### MobileAccountDto

Sensitive `passwordHash` and `securityStamp` are excluded from API responses.

```text
mobileAccountId: int
accountType: string
employeeId: int?
customerId: int?
username: string
normalizedUsername: string
isActive: bool
failedLoginAttempts: int
lockoutEndUtc: datetime?
tokenVersion: int
createdAtUtc: datetime
updatedAtUtc: datetime?
```

### MobileSessionDto

`refreshTokenHash` is excluded from API responses.

```text
sessionId: guid
familyId: guid
mobileAccountId: int
deviceName: string
platform: string
createdAtUtc: datetime
expiresAtUtc: datetime
lastUsedAtUtc: datetime?
revokedAtUtc: datetime?
revocationReason: string?
replacedBySessionId: guid?
```

### MobileRecoveryDto

`challengeHash` is excluded from API responses.

```text
mobileRecoveryChallengeId: long
mobileAccountId: int
purpose: string
createdAtUtc: datetime
expiresAtUtc: datetime
failedAttempts: int
maxAttempts: int
usedAtUtc: datetime?
revokedAtUtc: datetime?
```

## API Contracts

Routes are contracts only. The schema does not determine complete write endpoints, validation, pagination, authorization middleware, error format, or workflow mutations; those require separate approval.

| Area | Read routes grounded in existing tables |
| --- | --- |
| Employees | `GET /employees`, `GET /employees/{id}`, `GET /employees/{id}/attendance`, `GET /employees/{id}/leave-requests`, `GET /employees/{id}/draws` |
| Payroll | `GET /payroll/periods`, `GET /payroll/periods/{id}`, `GET /payroll/records`, `GET /payroll/records/{id}`, `GET /payroll/records/{id}/items`, `GET /payroll/piece-wages` |
| Finance | `GET /finance/transactions`, `GET /finance/journal-entries`, `GET /finance/journal-entries/{id}`, `GET /finance/ledger-accounts`, `GET /finance/cash-accounts` |
| Loyalty | `GET /loyalty/accounts`, `GET /loyalty/accounts/{customerId}`, `GET /loyalty/transactions`, `GET /loyalty/rules`, `GET /loyalty/rewards`, `GET /loyalty/redemptions` |
| Referral | `GET /referrals/accounts`, `GET /referrals/codes`, `GET /referrals/transactions`, `GET /referrals/rewards`, `GET /referrals/analytics` |
| Users | `GET /users`, `GET /users/{id}` |
| Roles | `GET /roles`, `GET /roles/{id}`, `GET /roles/{id}/permissions` |
| Permissions | `GET /permissions` |
| Mobile accounts | `GET /mobile/accounts`, `GET /mobile/accounts/{id}` |
| Mobile sessions | `GET /mobile/sessions`, `GET /mobile/accounts/{id}/sessions` |
| Recovery | `GET /mobile/recovery-challenges`, `GET /mobile/accounts/{id}/recovery-challenges` |

No write route is defined in this task; defining one would require workflow, audit, idempotency, and validation contracts.

## Permission Matrix

The database stores generic `SecurityPermissions` and mappings, but no permission codes matching the requested wildcard families were queried. The following are reserved permission naming contracts, not asserted existing records.

| Family | Read | Manage/operate | Administer | Scope |
| --- | --- | --- | --- | --- |
| `Employees.*` | `Employees.View` | `Employees.Manage` | `Employees.Admin` | Employee, attendance, leave, draws |
| `Payroll.*` | `Payroll.View` | `Payroll.Manage` | `Payroll.Admin` | Periods, records, items, piece wages |
| `Finance.*` | `Finance.View` | `Finance.Manage` | `Finance.Admin` | Transactions, journals, cash, ledger |
| `Loyalty.*` | `Loyalty.View` | `Loyalty.Manage` | `Loyalty.Admin` | Accounts, rules, rewards, transactions |
| `Referral.*` | `Referral.View` | `Referral.Manage` | `Referral.Admin` | Accounts, codes, rewards, transactions |
| `Security.*` | `Security.View` | `Security.Manage` | `Security.Admin` | Users, roles, permissions, mappings, claims |
| `Mobile.*` | `Mobile.View` | `Mobile.Manage` | `Mobile.Admin` | Accounts, sessions, recovery challenges |

`View` is read-only. `Manage` is reserved for approved operational changes. `Admin` is reserved for security-sensitive configuration. Assignment to roles is not defined here.

## Navigation Contracts

```text
Employees
|- Directory
|- Attendance
|- Leave Requests
|- Draws
`- Draw Settlements

Payroll
|- Periods
|- Payroll Records
|- Payroll Items
`- Piece Wages

Finance
|- Transactions
|- Journal Entries
|- Ledger Accounts
`- Cash Accounts

Loyalty
|- Accounts
|- Transactions
|- Rules
|- Rewards
`- Redemptions

Referrals
|- Accounts
|- Codes
|- Transactions
|- Rewards
`- Analytics

Administration
|- Users
|- Roles
|- Permissions
|- Role Permissions
`- User Claims

Mobile Identity
|- Mobile Accounts
|- Sessions
`- Recovery Challenges
```

This is information architecture only. No routes, screens, or UI components are created.

## Declared Cross-Module Relationships

| Relationship | Database evidence | Contract implication |
| --- | --- | --- |
| Employees -> Payroll | `PayrollRecords.EmployeeId -> Employees.EmployeeID`; payroll record -> payroll period | Payroll record belongs to employee and period |
| Employees -> Piece wages | `PieceWageRecords.EmployeeId -> Employees.EmployeeID` with `SET_NULL` | Wage can preserve record if employee reference is nulled |
| Piece wages -> Payroll | Optional FKs to payroll period and payroll record | Wage assignment timing is undefined |
| Finance -> Payroll | `EmployeeDrawSettlements.JournalEntryId` exists but has no declared FK | Financial settlement link is logical only |
| Finance -> Orders | `FinancialTransactions` has only reference text; no declared FK to orders | Order-finance linkage must not be inferred |
| Loyalty -> Customers | FKs from loyalty account/transaction/redemption to customers | Customer is authoritative identity |
| Loyalty -> Orders | Transactions/redemptions optionally reference orders | Order reward linkage is database-enforced where FK exists |
| Referral -> Customers | Referral account/code/transaction FKs to customers | Customer is authoritative identity |
| Referral -> Orders | Referral transaction optionally references order | Reward event may relate to an order |
| Security -> Users | Role assignments and claims have FKs to users; role permissions link roles and permissions | `Users.UserRole` is a legacy text field beside normalized mappings |
| Mobile -> Customers | `MobileAccounts.CustomerId -> Customers.CustomerID` | Account can represent a customer |
| Mobile -> Employees | `MobileAccounts.EmployeeId -> Employees.EmployeeID` | Account can represent an employee |
| Mobile session/recovery -> account | Both tables FK to MobileAccounts with cascade delete | Session and recovery state belong to mobile account |

## Architecture Risks

- `Employees` contains overlapping salary fields (`BaseSalary`, `FixedSalary`, `BasicSalary`) with no documented precedence.
- `Employee_Draws` identifies the employee by nullable `EmployeeCode`, not a declared FK to `Employees`.
- `EmployeeDrawSettlements.JournalEntryId` has no declared FK; finance settlement integrity is not database-enforced.
- No FK links `PieceWageRates` to `PieceWageRecords`; the stored rate source cannot be proven.
- `FinancialTransactions` has no declared relationship to orders, payroll, cash accounts, or journal entries.
- Lifecycle values are free text without a status dictionary or transition enforcement.
- `Users.UserPassword` is a legacy password field with a short string type; it must not be exposed or used without a dedicated security review and migration plan.
- Mobile account, session, and recovery tables currently have zero rows; their behavior cannot be inferred from runtime data.
- Security permission families are a proposed naming convention; existing `SecurityPermissions` rows must be reviewed before any seeding or role assignment.

## Final Recommendation

Keep all seven modules read-only at the contract stage. Before any implementation, approve separate workflow and security contracts for employee salary precedence, draw-to-journal integrity, wage-rate provenance, finance reference integrity, status transitions, credential handling, and role-permission mapping. Do not create or modify database structures until those decisions are made.
