# META TEMPLATE CONTRACT FINAL REPAIR

## Scope And Evidence

- Date: 2026-10-24.
- Baseline: `9c59f63f7f3e8421778f3b37dfd98c3df0481a08`. No new commit or branch was created.
- Production code diff: YES.
- Live GET through `MetaWhatsAppTemplateCatalogClient` and protected Runtime Settings: HTTP 200, 11 approved Arabic templates, including all 10 requested templates and the protected campaign.
- Official parameter metadata: `components[].example.body_text_named_params[].param_name`. Neither template text nor message Body/Title is parsed for parameters or values.
- Raw local evidence: [_validation_output/meta-template-contract-final-repair/meta-templates-live.json](../_validation_output/meta-template-contract-final-repair/meta-templates-live.json).
- Test projection: [recorded metadata](../Backend/LUMAR_ERP_API_V2.Tests/Fixtures/meta-template-catalog-2026-10-24.json). All 11 template identities, languages, statuses, categories and ordered parameter lists were compared with the live response. This fixture is test evidence, not the production source of truth.
- Actual message 760: [local payload evidence](../_validation_output/meta-template-contract-final-repair/message-760-local.json). The repository and production payload builder produced 10 parameters with all values present, without a Meta POST or database write. The stored status remained `Failed`; no claim or resend was performed.

## Contract Matrix

The sender resolves the ten parameter lists from the official client; it does not retain local parameter-count contracts. The existing campaign payload is unchanged.

| Template | Body Parameters | Contract Test | Source Readiness |
| --- | ---: | --- | --- |
| lumar_order_created | 10 | PASS | Complete; actual message 760 checked |
| lumar_order_ready | 4 | PASS | Official order, customer and StoreName setting |
| lumar_order_delivered | 2 | PASS | Official order and customer |
| lumar_piece_cancelled | 6 | PASS | BLOCKED: cancelled_amount source |
| lumar_order_cancelled | 4 | PASS | Cancelled order and refund payments |
| lumar_referrer_reward | 4 | PASS | Recipient, buyer and referral ledger/account |
| lumar_referrer_reward_relation | 5 | PASS | Same sources plus recorded RelationshipType |
| lumar_order_created_with_referrer | 14 | PASS | Order, direct referral and loyalty accounts |
| lumar_order_created_with_referrer_relation | 16 | PASS | BLOCKED: relation_welcome source |
| lumar_friday_greeting | 1 | PASS | Official recipient name |

Source readiness requires the relevant records to exist. A missing required source is reported by its exact Meta parameter name and prevents POST. Passing a contract test does not certify source completeness for every existing message.

## Official Value Sources

| Meta Parameter | LUMAR Context | Official Source |
| --- | --- | --- |
| customer_name | CustomerName | Customers.CustomerName for CustomerMessages.CustomerID |
| customer_code | CustomerCode | Customers.CustomerCode for the recipient |
| order_number | OrderNumber | Orders.OrderNumber via CustomerMessages.OrderID |
| order_details | OrderDetails | Distinct nonempty OrderItems.PieceType, ordered by first OrderItemID |
| total_amount | TotalAmount | Orders.TotalAmount |
| order_total | OrderTotal | Orders.TotalAmount; no substitution for missing cancellation effects |
| paid_amount | PaidAmount | Orders.PaidAmount |
| remaining_amount | RemainingAmount | Orders.RemainingAmount |
| delivery_date | DeliveryDate | Orders.DeliveryDate; no current-date fallback |
| points_earned / customer_points_earned | PointsEarned | SUM(LoyaltyTransactions.Points) for the order/buyer, Source=PiecePurchase, TransactionType=Earn |
| points_balance / customer_points_balance | PointsBalance | Buyer's LoyaltyAccounts.CurrentPoints |
| store_name | StoreName | System_Settings.SettingValue where SettingName=StoreName |
| referred_customer_name | ReferredCustomerName | Customers.CustomerName for Orders.CustomerID |
| referrer_name | ReferrerName | Customers.CustomerName for the reward recipient or direct Registration referrer |
| referrer_code | ReferrerCode | Same referrer's Customers.CustomerCode |
| referral_points / referral_points_earned | ReferralPointsEarned | ReferralTransactions.LoyaltyPoints for RewardGranted and matching buyer/order/referrer; exact RewardGranted reference when present |
| referral_balance | ReferralBalance | Referrer's ReferralAccounts.TotalRewardPoints |
| referrer_points_balance | ReferrerPointsBalance | Referrer's LoyaltyAccounts.CurrentPoints |
| relationship | Relationship | Buyer's Customers.RelationshipType |
| piece_name | PieceName | OrderItems.PieceType joined to Pieces by the stored OrderNumber:PieceNumber reference; ambiguous matches are not used |
| cancelled_amount (whole order) | CancelledAmount | Orders.TotalAmount with OrderStatus=Cancelled |
| refund_amount | RefundAmount | Absolute refund payment amounts; a recorded PaidAmount of zero supports zero refund |
| cancelled_amount (piece) | CancelledAmount | GAP: no approved piece cancellation financial amount source in the inspected workflow |
| relation_welcome | RelationWelcome | GAP: no persisted field, configured mapping or official producer found |

## Implemented Boundaries

- `ClaimNext` and single-message reads share the official-source context loader. Claim locking, retry counters, status transitions and idempotency were not redesigned.
- Existing OrderID, CustomerID, ReferenceType and ReferenceID persistence is reused; there is no new schema or sidecar source of truth.
- The ten contracts use the existing official Meta client. Temporary catalog failures remain retryable; permanent failures stop before POST.
- Smart template aliases and hyphenated lifecycle keys resolve to the intended Meta contract.
- Missing context is not filled with examples, arbitrary zeroes, current dates, message text or titles.
- The old synthetic `OfficialMetaTemplateHandler` was removed. Runtime-store tests use isolated ephemeral protection and recorded HTTP responses.
- No template content, account identifier, AccessToken, protected campaign contract, renderer, or VS Code task configuration was changed.

## Verification

- Independent contracts plus WhatsApp regression: 69 passed, 0 failed.
- Above tests plus real read-only repository hydration after the last source changes: 70 passed, 0 failed. [TRX](../_validation_output/meta-template-contract-final-repair/verified-contracts-and-hydration.trx).
- Final combined gate on the current build (contracts, WhatsApp, message creation, lifecycle, rendering, dispatcher and real read-only hydration): 112 passed, 0 failed, 0 skipped. [TRX](../_validation_output/meta-template-contract-final-repair/final-messaging-regression.trx).
- Backend build: PASS, 0 errors. NU1900 remains because NuGet vulnerability metadata could not be downloaded.
- `git diff --check`: PASS; Git noted the existing LF/CRLF normalization policy.
- Database writes, migrations, message claims and live message sends during this repair: none.
- Database-writing integration tests, including claim-concurrency fixtures, were not executed. The read-only test and local message-760 check are not a claim-concurrency or live-delivery certification.

## Remaining Approval Gates

1. `lumar_order_created_with_referrer_relation`: `relation_welcome` has no official producer. The current model contains RelationshipType, not an approved recipient-relative welcome phrase. Do not translate or append a suffix implicitly. The owner must designate an existing source or approve a maintained mapping and its persistence.
2. `lumar_piece_cancelled`: `cancelled_amount` has no approved financial event source in the inspected piece-disposition workflow. Neither Pieces nor CancelledPieceDisposition records that amount, and no corresponding SQL module was found. Do not substitute current price, production cost, order total or zero. The owner must designate the authoritative cancellation effect or approve a separate financial/persistence change.

The context loader reads current official records. It is not an immutable event snapshot. Guaranteeing original event-time values across later order/account changes requires an explicitly approved persistence design; no such migration was performed here.

Recommended continuation: approve the two missing source contracts first, then implement only their producer/storage mapping, add negative and positive source tests, and repeat the local matrix. Any schema or financial behavior change needs a scoped implementation and rollback plan. Existing safe code changes can be rolled back individually without a database rollback; do not revert unrelated working-tree changes.

## Verdict

OrderCreated's 3-versus-10 defect is repaired and verified locally. All ten Meta parameter contracts are retrieved and covered. Eight templates have identified complete source mappings; two remain blocked by the specific source gaps above. The unconditional verdict `A- ALL REMAINING META TEMPLATE CONTRACTS REPAIRED AND READY FOR FINAL LIVE MATRIX` is withheld. No owner live test is requested before these source gates are resolved.

كتب هذا التقرير المطور سعد
