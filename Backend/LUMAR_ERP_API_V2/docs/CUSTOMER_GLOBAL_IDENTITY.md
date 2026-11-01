# Customer Global Identity Foundation

## Contract

CustomerID remains the int identity PK and the target of every existing relationship.
GlobalCustomerUid is a hidden, immutable uniqueidentifier. New online creation uses
the .NET 10 standard Guid.CreateVersion7 API. Existing rows are assigned SQL NEWID
(UUIDv4) once. A named NEWID default preserves legacy SQL writer compatibility during
deployment; all migrated official paths explicitly supply UUIDv7. Neither version is
a secret. A UUID has 128 bits of structure, not 128 independent random bits for v7.

CustomerCode remains nvarchar(20). New codes have exactly 18 characters:
`C-XXXXX-XXXXX-XXXX`. Alphabet: `0123456789ABCDEFGHJKMNPQRSTVWXYZ`.
Each of the 14 independent symbols uses RandomNumberGenerator.GetInt32(32): 70 bits.
I, L, O and U are excluded. This is symbol sampling, not a custom Base32 encoder.
Unique constraints remain the final collision guard. A collision fails the transaction;
it never reuses a customer, merges by phone, or silently rewrites a committed identity.
No offline queue, client UID input, fingerprint, operation ID or replay contract is added.

## Pre-change Call-Site Audit

| Path | Current generator | Request code | Server code | Creates | Reuses |
| --- | --- | --- | --- | --- | --- |
| POST /customers -> service -> repository CreateAsync | supplied manual code | yes | no | yes | no |
| Windows customer_creation_repository -> POST /customers/with-referral | MAX+1 | no | yes | yes | no |
| POST /customers/with-referral -> CreateWithReferralInTransactionAsync | MAX+1 | no | yes | yes | no |
| AuthRepository.RegisterMobileCustomerAsync -> same transactional helper | MAX+1 | no | yes | yes | no |
| POST /mobile/customers/family -> CreateManagedCustomerAsync, new branch | MAX+1 | lookup only | yes | yes | no |
| Same family endpoint, existing customer/access branch | none | optional lookup | no | no | yes |

Exactly three production INSERT sites exist in CustomerRepository. AuthRepository
uses the referral helper inside its existing customer/account/session transaction.
Family lookup semantics and shared-phone behavior are unchanged. An unreferenced
private timestamp generator in AuthRepository is not an official creation caller.

## Migration Plan

Affected table: dbo.Customers only. Objects: GlobalCustomerUid,
UQ_Customers_GlobalCustomerUid, DF_Customers_GlobalCustomerUid.
Approved targets are validation and the explicitly named isolated test database only.
The historical LUMAR_ERP database is never a write target.

Before apply: record count, ordered ID/code SHA256, relationship snapshots and orphans.
Test on a separately restored validation backup, never overwrite an existing database.
The available 2026-10-22 backup passed VERIFYONLY, has no backup checksums and is not
a current recovery point. It is suitable as a test source, not a claim of zero-loss recovery.

One XACT_ABORT transaction takes TABLOCKX/HOLDLOCK on Customers, adds the nullable
column, backfills only null UIDs, verifies nulls/duplicates, makes it NOT NULL, adds
the named unique constraint and compatibility default. No code widening or recoding.
Expected locks: exclusive data/table locks and Sch-M locks for ALTER/index creation;
reads/writes may wait. Lock timeout is 15 seconds; failure rolls back the transaction.
Use a quiet validation window. Rerunning preserves existing UIDs.

## Down Plan and Rollback Limits

Before commit, ROLLBACK restores the prior schema and data. Prove this in the isolated
database with an outer transaction. Failed migration never leaves a half-filled column.
After commit, prefer rolling back application binaries while RETAINING UIDs/default.
Do not delete published UIDs. Full down is only safe on an unused isolated test database
or after separate owner approval establishing that no UID has been consumed elsewhere:
drop UQ_Customers_GlobalCustomerUid, then DF_Customers_GlobalCustomerUid, then the column,
inside one transaction. No customer deletion, reseeding, baseline reset or historical writes.

## Backend / Mobile Handoff

Mobile remains online-only for registration and family creation. No Mobile source is
owned or changed here. Current registration and family request/response shapes stay
compatible; Mobile need not generate or display a UID. The server persists it internally.
Administrative CreateCustomerDto.CustomerCode becomes optional; absent code is generated,
supplied code must match the new format. Family CustomerCode remains an existing-customer
lookup, not a request to assign an arbitrary new code.

New-format codes cannot be changed through ordinary update. Sending the unchanged code
still permits name/phone/address updates. Current name and phone remain authoritative;
no aliases/history are created. Legacy codes retain their existing update behavior.

Mobile verification required by its owner: registration creates one official Customer and
account; family creation returns a new-format code; family reuse preserves the same ID/code/UID;
old/new codes display/search without numeric-only or C+digits validation; offline attempts
do not enqueue official creation. Never automatically retry a POST on a lost response.
The existing administrative create/loyalty two-step commit and absence of create idempotency
predate this change; no retry/replay guarantee is introduced. Any separate repair needs its
own bounded contract. Future phase: local customer/order creation and synchronization.

## Verification Completed 2026-11-01

The isolated database LUMAR_CUSTOMER_IDENTITY_TEST_20261101 was restored WITHOUT
REPLACE. Its 156 preexisting customers survived the migration unchanged. An outer
transaction rollback removed the UID column again, proving transactional down before
commit. The migration was subsequently committed there for real repository tests.
No existing test database was overwritten, and no test rows were deleted from validation.

Before production-code changes, the focused integration suite had 3 passes and 6 failures:
missing administrative code generation, v4 defaults instead of explicitly generated v7,
and a concurrent MAX+1 deadlock. After changes: 9/9 integration tests and 12/12 generator
tests passed, including 100,000 generated identities and eight concurrent SQL creates.
The sample is an implementation check, not a proof that random collisions are impossible.

Validation migration was committed only after an outer guard compared the counts and
ordered SHA256 snapshots of all 13 Customer/FK-source tables. All matched. This includes
every existing Customer column except the new UID, and all columns in the linked tables.
All 14 customer FKs remained enabled/trusted with zero orphans after migration.
No original customer names, phone values, parents, balances, accounts or referral rows changed.

ID/code SHA256 before and after:
`5A9D331AF2DCFCDD8FD14AC20ACF4E625ED99ECA0D99646A2F19E9BDE4AB545D`.

| Required result | Value |
| --- | --- |
| CURRENT CUSTOMER COUNT | 393 |
| GLOBAL UID TYPE / COLUMN | uniqueidentifier NOT NULL / dbo.Customers.GlobalCustomerUid |
| UUID VERSION / GENERATOR | v7 / Guid.CreateVersion7 for official creates; SQL NEWID v4 for backfill and compatibility default |
| CUSTOMER CODE FORMAT / LENGTH | C-XXXXX-XXXXX-XXXX / 18 |
| CUSTOMER CODE RANDOM BITS | 70, RandomNumberGenerator.GetInt32(32) per symbol |
| CUSTOMER CODE ALPHABET | 0123456789ABCDEFGHJKMNPQRSTVWXYZ |
| CUSTOMERCODE COLUMN ALTERED | NO; nvarchar(20), 40 bytes, existing unique object retained |
| GLOBAL UID UNIQUE OBJECT | UQ_Customers_GlobalCustomerUid |
| BACKFILL COUNT | 393 |
| GLOBAL UID NULLS / DUPLICATES | 0 / 0 |
| CUSTOMER CODE DUPLICATES | 0 |
| CURRENT CUSTOMER CODES / CUSTOMERIDS CHANGED | 0 / 0 |
| OFFICIAL CREATE PATHS FOUND / MIGRATED | 3 SQL insert sites / 3; 4 Backend endpoints plus Windows caller; family reuse preserved |
| MAX+1 CUSTOMER CREATE CALLERS REMAINING | 0 in current source |
| WINDOWS CREATE | PASS, repository request contract and Backend controller integration |
| MOBILE CUSTOMER REGISTRATION | PASS Backend integration / HANDOFF independent Mobile source |
| MOBILE FAMILY CREATE | PASS Backend integration / HANDOFF independent Mobile source |
| FAMILY REUSE | PASS, ID/code/UID and single access/referral effects |
| CUSTOMER CONCURRENT CREATE | PASS, 8 simultaneous creates |
| LOYALTY / REFERRAL / MOBILE ACCOUNT INTEGRITY | PASS / PASS / PASS |
| DATABASE INTEGRITY | PASS, 13 unchanged table hashes, 14 trusted/enabled FKs, zero orphans |
| UID AND CODE UNIQUE ENFORCEMENT | PASS, SQL 2601/2627 in isolated collision tests, enabled unique constraints in validation |
| BACKEND BUILD | PASS, Release build performed by targeted dotnet test |
| BACKEND TESTS PASSED / FAILED | 21 / 0 |
| FLUTTER TESTS | 6 passed, 0 failed; light/dark, old/new search/edit and online create |
| FLUTTER ANALYZE | PASS, 2 affected files, no issues |
| WINDOWS BUILD | PASS, Debug, lib/main_windows.dart |
| HISTORICAL DATABASE WRITES / GITHUB WRITES | 0 / 0 |
| OFFLINE-FIRST ENGINE BUILT | NO |

### Operational Limits

- Verification covers the new build and migrated validation schema. The preexisting
	Backend process was not restarted; load the new Backend build before using the new
	creation contract operationally. No Gateway/WhatsApp runtime was touched.
- No independent Mobile source was modified or Mobile UI acceptance claimed.
- Hot reload was attempted on the Windows workspace DTD but no app was connected;
	no app was launched. Windows build, widget tests and analysis passed independently.
- Existing compiler/analyzer warnings remain. NU1900 reports unavailable NuGet vulnerability
	metadata; it did not prevent builds and no dependency was added.
- The isolated database and its test records are retained; cleanup is not part of this task.
- The older backup is not a current zero-loss recovery source. UIDs must be retained after
	real use. Full offline identity acceptance, replay, collision recovery and synchronization
	are deliberately deferred. The old unused private timestamp helper is not an official caller;
	existing prefix settings and the shared SystemCodeGenerator were not removed.

NEXT FUTURE PHASE: OFFLINE-FIRST LOCAL CUSTOMER + ORDER CREATION AND SYNC.
Do not begin that phase automatically.