# عقد حدود المسارات الرسمي داخل المشروع

## 1) مقدمة وتحديد الهدف

هذا العقد يحدد المسار التشغيلي الرسمي الحالي للنظام داخل دورة الفاتورة، الاستلام، التكويد، التخزين، والمحاسبة. الهدف أن يكون مرجعًا إلزاميًا لأي دردشة أو منفذ تقني أو مهمة اختبار مستقبلية، بحيث لا يتم الحكم على السلوك من خلال مسارات تاريخية أو مسارات متكررة أو مسارات غير مؤكدة.

القاعدة الأساسية:
- A = CURRENT OFFICIAL CONTRACT PATH: المسار التشغيلي الرسمي الحالي.
- B = SUPPORTING CURRENT PATH: مسار مساعد مباشر للمسار الرسمي.
- C = LEGACY / HISTORICAL PATH: مسار تاريخي أو قديم وليس Source Of Truth.
- D = DUPLICATE / COMPETING PATH: مسار مكرر أو منافس للمسار الرسمي.
- E = UNKNOWN / OWNER DECISION REQUIRED: لم يثبت تصنيفه ويمنع استخدامه في الإصلاح حتى الحسم.

هذا العقد هو وثيقة توثيق فقط، ولا يغيّر الكود التشغيلي ولا قاعدة البيانات ولا أي Failure في هذه المهمة.

---

## 2) التصنيف العام للحدود

### A = CURRENT OFFICIAL CONTRACT PATH
المسار التشغيلي الرسمي الحالي داخل النظام المعتمد الآن هو:

- فاتورة المورد
- الاستلام الأولي
- تأكيد بنود الاستلام
- التكويد الرسمي
- التخزين المخزني
- حركات المخزون
- الحدث المحاسبي
- Replay / Idempotency protection

### B = SUPPORTING CURRENT PATH
هو كل مسار يدعم مباشرة المسار الرسمي الحالي ولا يضعف العقد الرسمي، مثل:
- قراءة/تحديث عناصر المخزون الأساسية
- حل كود الكتالوج والهوية الرسمية
- تخصيص Allocations في التخزين
- دعم كتابة سجل posting وخطوط الاستلام

### C = LEGACY / HISTORICAL PATH
هو أي مسار قديم أو تاريخي لا يثبت أنه جزء من Runtime الحالي أو Source Of Truth الحالي. هذه المسارات لا تُستخدم لتحديد سلوك جديد، ولا يجوز تعديل العقد الحديث لإرضاء اختباراتها.

### D = DUPLICATE / COMPETING PATH
هو أي مسار مكرر أو منافس يحاول محاكاة نفس السلوك الرسمي ولكن لا يثبت أنه هو المصدر الرسمي. لا يمكن اختياره دون دليل Call Chain واضح وملزم.

### E = UNKNOWN / OWNER DECISION REQUIRED
هو مسار غير موثق كفايةً في السلسلة الحالية، وبالتالي يمنع استخدامه في الإصلاح قبل اتخاذ قرار المالك.

---

## 3) المسار التشغيلي الرسمي بالعربية أولًا ثم الأسماء التقنية

### المسار الرسمي الحالي

فاتورة المورد
↓
الاستلام الأولي
↓
تأكيد بنود الاستلام
↓
التكويد الرسمي
↓
التخزين المخزني
↓
حركات المخزون
↓
الحدث المحاسبي
↓
Replay / Idempotency protection

### المرحلة 1: فاتورة المورد

BUSINESS PURPOSE =
تسجيل فاتورة المورد بشكل رسمي مع ربطها بمشترياتها، وبنودها، ونظام الحسابات، وضمان عدم التكرار.

ENTRY POINT =
CreateSupplierInvoiceRequestDto → SupplierFinancialWorkflowCoordinator.CreateInvoiceAsync

CONTROLLER =
لا توجد نقطة دخول مباشرة في InventoryController لهذه المرحلة؛ المسار الرئيسي هو خدمة مالية المورد.

SERVICE =
SupplierFinancialWorkflowCoordinator

REPOSITORY =
SupplierFinancialRuntime

PRIMARY TABLES =
SupplierInvoices, SupplierInvoiceLines, PurchaseOrders, InventoryItems

IDENTITY SOURCE =
هوية المورد والفاتورة والخطوط، لا يتم استخدام FabricCode أو CatalogNumber كهوية الفاتورة نفسها.

ACCOUNTING EFFECT =
تسجل الفاتورة في الاستحقاق المالي وتُربط بالحدث المحاسبي المناسب عند الحاجة.

REPLAY KEY =
SourceOperationId + SupplierInvoiceId + SupplierInvoiceLine payload

CLASSIFICATION =
[A]

### المرحلة 2: الاستلام الأولي

BUSINESS PURPOSE =
إنشاء تأكيد الاستلام الأولي للسلع من المورد، مع التحقق من صحة الكميات والسعر ووضع حالة الاستلام الرسمية.

ENTRY POINT =
CreateGoodsReceiptDto → InventoryRepository.CreateGoodsReceiptAsync / CreateGoodsReceiptInTransactionAsync

CONTROLLER =
أي نقطة تحكم عملية لا تستخدم كـ Source Of Truth إذا لم تكن جزءًا من السلسلة الحالية؛ المسار الحالي في الواقع يمر عبر coordinator/service/runtime.

SERVICE =
GoodsReceiptWorkflowCoordinator.CreateAsync

REPOSITORY =
InventoryRepository.CreateGoodsReceiptInTransactionAsync

PRIMARY TABLES =
GoodsReceipts, GoodsReceiptItems, PurchaseOrders, SupplierInvoices

IDENTITY SOURCE =
ReceiptNumber + SourceOperationId + GoodsReceiptItemId

ACCOUNTING EFFECT =
يُهيئ الاستلام الرسمي بحيث يسمح لاحقًا بإنشاء الرفع المخزني والحدث المحاسبي.

REPLAY KEY =
SourceOperationId

CLASSIFICATION =
[A]

### المرحلة 3: تأكيد بنود الاستلام

BUSINESS PURPOSE =
تأكيد أن كل بند استلام صحيح، قيمته موجبة، وعلاقته الرسمية مع المخزون أو البند المعني سارية.

ENTRY POINT =
ValidateGoodsReceiptConfirmation + ConfirmGoodsReceiptItemAsync

CONTROLLER =
لا يوجد Controller مباشر في السلسلة الرسمية الحالية لهذا الجزء.

SERVICE =
GoodsReceiptWorkflowCoordinator → IGoodsReceiptTransactionRuntime

REPOSITORY =
InventoryRepository

PRIMARY TABLES =
GoodsReceiptItems, GoodsReceipts, InventoryItems

IDENTITY SOURCE =
InventoryItemId و ItemDescription و Quantity و UnitCost + SourceOperationId

ACCOUNTING EFFECT =
تهيئة الأساس للحدث المحاسبي المخزني بعد التكوين الرسمي.

REPLAY KEY =
SourceOperationId + GoodsReceiptItemId

CLASSIFICATION =
[A]

### المرحلة 4: التكويد الرسمي

BUSINESS PURPOSE =
تحديد هوية المخزون الرسمية، وربطها بالنوع، والكتالوج، والفصل، والوزن/العرض، مع تجنب الهوية المزيفة أو المتكررة.

ENTRY POINT =
ReceiveFoundationInventoryAsync / ReceiveFabricInventoryAsync / ReceiveFabricInventoryBatchAsync

CONTROLLER =
InventoryController.ReceiveFoundationFabric

SERVICE =
IInventoryService.ReceiveFabricInventoryAsync

REPOSITORY =
InventoryRepository.ReceiveFoundationInventoryAsync, ReceiveFabricInventoryAsync, ReceiveFabricInventoryBatchAsync

PRIMARY TABLES =
InventoryItems, InventoryItemFoundation, FabricRolls, InventoryReceiptPostings, InventoryReceiptLines

IDENTITY SOURCE =
InventoryItems.ItemCode = FabricCode
FabricRolls.RollCode = FabricCode
InventoryItems.Barcode = CatalogNumber ضمن نطاق Fabric فقط

ACCOUNTING EFFECT =
إنشاء posting رسمي للاستلام، وربط الحركات والشيك المحاسبي.

REPLAY KEY =
SourceOperationId + GoodsReceiptItemId + ItemCode + opposingLedgerAccount

CLASSIFICATION =
[A]

### المرحلة 5: التخزين المخزني

BUSINESS PURPOSE =
إحكام ربط البند الاستلام مع مواقع التخزين أو التخصيصات الرسمية، وتمثيل الكميات المخزنة في النظام.

ENTRY POINT =
InsertStorageAllocationAsync / PostOfficialFabricBatchRollAsync

CONTROLLER =
InventoryController.GetPendingReceiptStorage, ReceiveFoundationFabric

SERVICE =
IInventoryService.ReceiveFabricInventoryAsync

REPOSITORY =
InventoryRepository.InsertStorageAllocationAsync, ReceiveFabricBatchAsync, PostOfficialFabricBatchRollAsync

PRIMARY TABLES =
GoodsReceiptItemStorageAllocations, InventoryTransactions, InventoryReceiptLines, FabricRolls

IDENTITY SOURCE =
InventoryItemId + itemCode + storage operation + roll identity

ACCOUNTING EFFECT =
ربط كل تخصيص مخزني بحركة المخزون والحدث المحاسبي.

REPLAY KEY =
SourceOperationId + storageOperationId + GoodsReceiptItemId

CLASSIFICATION =
[A]

### المرحلة 6: حركات المخزون

BUSINESS PURPOSE =
تسجيل حركة الاستلام أو الاستهلاك/الإرسال/الاحتفاظ بشكل رسمي، مع التوازن الدقيق بين الزيادة والنقصان.

ENTRY POINT =
InsertFoundationInventoryTransactionAsync

CONTROLLER =
InventoryController.ConsumeFoundationFabric / ConsumeFoundationConsumable / ReceiveFoundationFabric

SERVICE =
IInventoryService

REPOSITORY =
InventoryRepository.InsertFoundationInventoryTransactionAsync

PRIMARY TABLES =
InventoryTransactions, InventoryItems, FabricRolls

IDENTITY SOURCE =
InventoryItemId و ItemCode + RollCode

ACCOUNTING EFFECT =
تؤدي إلى ربط كل حركة بحدث محاسبي رسمي.

REPLAY KEY =
SourceOperationId + reference + inventoryItemId + movement type

CLASSIFICATION =
[A]

### المرحلة 7: الحدث المحاسبي

BUSINESS PURPOSE =
إدارة Posting الرسمي للحركة، والتأكد من أن الحساب المعاكس المرخص فعلاً هو الحساب الصحيح، وأن كل حركة لها EventId مرتبط.

ENTRY POINT =
AccountingEventPostingGateway.PostInventoryReceiptAsync / PostAsync

CONTROLLER =
غير مباشر؛ ينطلق من Repository وخدمة Inventory

SERVICE =
AccountingEventPostingGateway

REPOSITORY =
InventoryRepository.InsertInventoryReceiptPostingAsync, AccountingEventPostingGateway

PRIMARY TABLES =
AccountingEvents, InventoryReceiptPostings, InventoryReceiptLines

IDENTITY SOURCE =
AccountingEventId + SourceOperationId + posting reference

ACCOUNTING EFFECT =
تسجيل حسابات المعاكس الرسمي، وقبول/رفض الحسابات بناءً على سياسة الـ approved ledger account.

REPLAY KEY =
SourceOperationId + posting identity

CLASSIFICATION =
[A]

### المرحلة 8: Replay / Idempotency protection

BUSINESS PURPOSE =
منع التكرار في الاستلام أو التكويد أو الحركات، وضمان أن نفس SourceOperationId لا يُسجل أكثر من مرة إذا كان payload متطابقًا.

ENTRY POINT =
TryReadExistingReceiptAsync + ReadExistingGoodsReceiptAsync + duplicate checks in repository

CONTROLLER =
غير مباشر

SERVICE =
InventoryService / GoodsReceiptWorkflowCoordinator

REPOSITORY =
InventoryRepository

PRIMARY TABLES =
GoodsReceipts, InventoryReceiptPostings, InventoryTransactions, SupplierInvoiceLines

IDENTITY SOURCE =
SourceOperationId + payload hash / record presence

ACCOUNTING EFFECT =
يمنع كتابة أحداث محاسبية مكررة أو تعارضات idempotency.

REPLAY KEY =
SourceOperationId

CLASSIFICATION =
[A]

---

## 4) مصفوفة Methods

| METHOD | FILE | CALLED_BY | CALLS | CLASSIFICATION = A/B/C/D/E | CURRENT_SOURCE_OF_TRUTH = YES/NO | CURRENT_UI_REACHABLE = YES/NO | WRITES_CURRENT_TABLES = YES/NO | USES_CURRENT_IDENTITY_CONTRACT = YES/NO | WHY |
|---|---|---|---|---|---|---|---|---|---|
| CreateGoodsReceiptAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | GoodsReceiptWorkflowCoordinator.CreateAsync | CreateGoodsReceiptInTransactionAsync | A | YES | NO | YES | YES | هذا هو نقطة إدخال إنشاء استلام رسمي في السلسلة الحالية، ويعتمد على SourceOperationId وحقول الاستلام. |
| CreateGoodsReceiptInTransactionAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | CreateGoodsReceiptAsync, GoodsReceiptWorkflowCoordinator | ValidateGoodsReceiptConfirmation, ReadExistingGoodsReceiptAsync, InsertGoodsReceiptConfirmationAsync, ConfirmGoodsReceiptItemAsync | A | YES | NO | YES | YES | السلسلة الرسمية لتأكيد الاستلام وتتبع التكرار. |
| CreateGoodsReceiptConfirmation وما يعادلها فعليًا | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | CreateGoodsReceiptInTransactionAsync | InsertGoodsReceiptConfirmationAsync, ConfirmGoodsReceiptItemAsync | A | YES | NO | YES | YES | هذه هي خطوات تأكيد الاستلام الرسمية داخل السلسلة الحالية. |
| ReceiveFabricInventoryAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | InventoryService / InventoryController.ReceiveFoundationFabric | ReceiveFoundationInventoryAsync | A | YES | YES | YES | YES | هذا هو بوابة الاستلام الرسمي للقماش الحديث داخل العقد الحالي. |
| ReceiveFabricInventoryBatchAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | InventoryService | ValidateFabricBatch, PostOfficialFabricBatchRollAsync, SystemCodeGenerator | A | YES | YES | YES | YES | هذا Method يمثّل مسار batch الرسمي لتجميع أرقام الرول/الكود/التخزين في الاستلام الحالي. |
| ReceiveFoundationInventoryAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFabricInventoryAsync / ReceiveConsumableInventoryAsync | ReadFoundationItemAsync, InsertFoundationInventoryItemAsync, UpdateFoundationInventoryItemAsync, ApplyFabricPresentationAsync, InsertInventoryReceiptPostingAsync, InsertInventoryReceiptLineAsync, InsertFoundationInventoryTransactionAsync | A | YES | YES | YES | YES | هذه هي الممر الرئيس للخطوة المؤسسة للFoundation، وهي المصدر الموثق لربط كتالوج/قماش/تخزين/حركة. |
| ReceiveFabricBatchAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | InventoryService | TryReadOfficialFabricBatchAsync, InsertFabricPurchaseOrderAsync, InsertFabricGoodsReceiptAsync, PostOfficialFabricBatchRollAsync | A | YES | YES | YES | YES | مسار شراء/استلام القماش الرسمي، ويستخدم السلسلة الرسمية الحالية في ثباته. |
| PostOfficialFabricBatchRollAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFabricBatchAsync, ReceiveFabricInventoryBatchAsync | ReadGoodsReceiptSourceAsync, ReadFoundationItemAsync, InsertFoundationInventoryItemAsync, ApplyFabricPresentationAsync, InsertFabricRollAsync, InsertInventoryReceiptPostingAsync, AccountingEventPostingGateway.PostInventoryReceiptAsync, InsertStorageAllocationAsync | A | YES | NO | YES | YES | هذا هو العامل الرسمي في تسجيل كل رول قماش في Batch وملائمته للمسار الحالي. |
| ReadFoundationItemAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync, PostOfficialFabricBatchRollAsync | SELECT من InventoryItems + InventoryItemFoundation | A | YES | NO | NO | YES | يقرأ هوية المخزون الرسمية الحالية ويمنع استخدام Legacy item في path الرسمي. |
| InsertFoundationInventoryItemAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync, PostOfficialFabricBatchRollAsync | INSERT INTO InventoryItems + InventoryItemFoundation | A | YES | NO | YES | YES | هذه هي كتابة الهوية الرسمية للعناصر الحديثة في المسار الحالي. |
| UpdateFoundationInventoryItemAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync, PostOfficialFabricBatchRollAsync | UPDATE InventoryItems | A | YES | NO | YES | YES | تحديث القيم الرسمية وعلاقة الهوية دون التبديل إلى مسار Legacy. |
| InsertFabricRollAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync, PostOfficialFabricBatchRollAsync | INSERT INTO FabricRolls | A | YES | NO | YES | YES | إنشاء رول Fabric الرسمي مع RollCode المطابق لتعليمات الهوية الحالية. |
| ApplyFabricPresentationAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync, PostOfficialFabricBatchRollAsync | ResolveFabricCatalogNumberAsync, UPDATE InventoryItems | A | YES | NO | YES | YES | يطبق الشكل الرسمي لل fabric: نوع القماش، اللون، العرض، والـ CatalogNumber. |
| ResolveFabricCatalogNumberAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ApplyFabricPresentationAsync | SELECT Barcode + Generate code if missing | A | YES | NO | YES | YES | يقرر هل يتم إعادة استخدام CatalogNumber أو تخصيص جديد بحسب عقد الهوية الرسمي. |
| InsertStorageAllocationAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync, PostOfficialFabricBatchRollAsync, consume flows | INSERT INTO GoodsReceiptItemStorageAllocations | A | YES | NO | YES | YES | يربط التخصيص المخزني بحدث المخزون والـ posting الرسمي. |
| GetNextAllocatedCatalogNumberAsync | [LUMAR_ERP_API_V2/Utilities/SystemCodeGenerator.cs](../LUMAR_ERP_API_V2/Utilities/SystemCodeGenerator.cs) | ResolveFabricCatalogNumberAsync, ReceiveFoundationInventoryAsync | AllocateNextGlobalCodeAsync, SeedScopeFromExistingOfficialValuesAsync | A | YES | NO | YES | YES | هو المولد الرسمي للـ CatalogNumber، ويقرأ القيم الرسمية الحالية ويمنع الازدواج. |
| GetNextAllocatedFabricCodeAsync | [LUMAR_ERP_API_V2/Utilities/SystemCodeGenerator.cs](../LUMAR_ERP_API_V2/Utilities/SystemCodeGenerator.cs) | ReceiveFoundationInventoryAsync, ReceiveFabricBatchAsync, PostOfficialFabricBatchRollAsync | AllocateNextGlobalCodeAsync, SeedScopeFromExistingOfficialValuesAsync | A | YES | NO | YES | YES | هو المولد الرسمي لـ FabricCode، ويضمن الترتيب المنطقي للهوية في المسار الحالي. |
| ValidateGoodsReceiptConfirmation | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | CreateGoodsReceiptInTransactionAsync | GetGoodsReceiptConfirmationFailureReason | A | YES | NO | NO | YES | شرط إدخال رسمي لتأكيد الاستلام قبل إنشاء الحركة. |
| AccountingEventPostingGateway.PostInventoryReceiptAsync | [LUMAR_ERP_API_V2/Utilities/AccountingEventPostingGateway.cs](../LUMAR_ERP_API_V2/Utilities/AccountingEventPostingGateway.cs) | PostOfficialFabricBatchRollAsync, ReceiveFoundationInventoryAsync | PostAsync | A | YES | NO | YES | YES | هذا هو سجل الحدث المحاسبي الرسمي الحالي، ويستند إلى الحساب المعاكس الرسمي. |
| TryReadExistingReceiptAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | ReceiveFoundationInventoryAsync | SELECT existing posting + sourceOperationId match | A | YES | NO | NO | YES | يمنع التكرار في نفس SourceOperationId ويُستخدم كحماية Replay. |
| ReadExistingGoodsReceiptAsync | [LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs](../LUMAR_ERP_API_V2/Repositories/InventoryRepository.cs) | CreateGoodsReceiptInTransactionAsync | SELECT GoodsReceipts | A | YES | NO | NO | YES | يمنع إنشاء استلام مكرر لنفس SourceOperationId. |
| Legacy direct fabric tables / direct batch pattern | C/D evidence: not current call chain | legacy / direct old code | not used by current controllers or runtime | C or E | NO | NO | YES if legacy writes occur, but not current runtime | NO | لا يوجد دليل كافٍ على أنها جزء من Runtime الحالي في السلسلة الرسمية. إن كان المسار فعليًا موجودًا في المستودع لكنه غير مستخدم، فيعتبر C أو E حسب الدليل. |

> ملاحظة مهمة: إذا لم يوجد دليل كافٍ على أن أي مسار قديم أو مكرر يُستخدم فعليًا من Runtime الحالي، فيُصنّف كـ E لا كـ C تقليديًا، وفق القاعدة: لا تُصنف C أو D بالتخمين.

---

## 5) مصفوفة الجداول

| TABLE | CLASSIFICATION = A/B/C/D/E | READ_BY_CURRENT_PATH = YES/NO | WRITTEN_BY_CURRENT_PATH = YES/NO | SOURCE_OF_TRUTH = YES/NO | HISTORICAL_ONLY = YES/NO | MEANING |
|---|---|---|---|---|---|---|
| SupplierInvoices | A | YES | YES | YES | NO | الفواتير الرسمية للمورد في المسار الحالي. |
| SupplierInvoiceLines | A | YES | YES | YES | NO | بنود الفاتورة الرسمية، مرتبطة بوجود InventoryItemId/ItemDescription/الكمية والسعر. |
| GoodsReceipts | A | YES | YES | YES | NO | تأكيد الاستلام الرسمي من المورد. |
| GoodsReceiptItems | A | YES | YES | YES | NO | بنود الاستلام الرسمية. |
| InventoryItems | A | YES | YES | YES | NO | العنصر المخزني من نوع حديث/مؤسسي؛ يحتوي على ItemCode و Barcode. |
| InventoryItemFoundation | A | YES | YES | YES | NO | البيانات التكويدية الرسمية للعناصر الحديثة (Fabric/Consumable) داخل Foundation model. |
| FabricRolls | A | YES | YES | YES | NO | أرقام الرول الرسمية، مع RollCode يساوي FabricCode داخل المسار الرسمي. |
| InventoryReceiptPostings | A | YES | YES | YES | NO | سجلات posting الرسمية للخفض/الزيادة في المخزون. |
| InventoryReceiptLines | A | YES | YES | YES | NO | تفاصيل كل سطر في posting، مربوط بالـ item وكمية الاستلام. |
| InventoryTransactions | A | YES | YES | YES | NO | حركات المخزون الرسمية، مع حركة تأكيد/استهلاك/الاستلام. |
| GoodsReceiptItemStorageAllocations | A | YES | YES | YES | NO | تخصيصات التخزين الرسمية لكل مادة مستلمة. |
| GlobalCodeAllocations | A | YES | YES | YES | NO | جدول تخصيص الأكواد العالمي، يستخدم في CatalogNumber و FabricCode. |
| AccountingEvents | A | YES | YES | YES | NO | الحدث المحاسبي الرسمي الذي يكتب الإحداث المحاسبية للعمليات. |
| Fabrics | C | NO | NO | NO | YES | جدول تاريخي لا يثبت أنه جزء من Runtime الحالي. |
| Fabrics_Inventory | C | NO | NO | NO | YES | جدول تاريخي/قديم، غير مرجح في المسار الرسمي الحالي. |
| OrderItemFabrics | E | UNKNOWN | UNKNOWN | NO | NO | قد يكون مرتبطًا بعملية الطلب/القطع، لكنه لا يثبت كـ Current Source Of Truth بدون Call Chain أو owner decision. |
| PurchaseOrders | A | YES | YES | YES | NO | طلبات الشراء الرسمية ذات العلاقة بالفاتورة والاستلام. |
| PurchaseOrderItems | A | YES | YES | YES | NO | بنود طلبات الشراء الرسمية. |
| Suppliers | A | YES | YES | YES | NO | جدول الموردين الرسمي. |
| SupplierPaymentAllocations | A | YES | YES | YES | NO | تخصيصات الدفع الرسمية للمورد، في دورة الحسابات. |
| System_Settings | A | YES | YES | YES | NO | إعدادات النظام ومحددات Prefix اللازمة للمولدات. |

> ملاحظة: أي جدول يحتوي على اسم Fabric لكن ليس جزءًا من السلسلة الرسمية الحالية لا يُعامل كـ Source Of Truth إلا إذا ثبت uso فعليًا في Call Chain.

---

## 6) مصفوفة الاختبارات

| TEST_NAME | TEST_FILE | TESTED_PATH | PATH_CLASSIFICATION = A/B/C/D/E | CURRENT_CONTRACT_RELEVANT = YES/NO | MAY_BLOCK_CURRENT_TASK = YES/NO | WHY |
|---|---|---|---|---|---|---|
| FoundationReceipt_MultiRollBatch_StoresEachRollWithoutDuplication | LUMAR_ERP_API_V2.Tests/FabricConsumablesFoundationIntegrationTests.cs | Foundation receipt + multi-roll batch | A | YES | YES | هذا الاختبار يختبر السلسلة الحالية ويؤثر مباشرة على مجال التكويد والتخزين الرسمي. |
| FoundationReceiptAndConsumption_AreBalancedPreciseAndIdempotent | LUMAR_ERP_API_V2.Tests/FabricConsumablesFoundationIntegrationTests.cs | Foundation receipt + consumption + idempotency | A | YES | YES | يختبر التوازن الدقيق والـ Replay في المسار الرسمي الحالي. |
| FoundationReceipt_ReusesExistingActiveFabricCodeInsteadOfAllocatingReplacement | LUMAR_ERP_API_V2.Tests/FabricConsumablesFoundationIntegrationTests.cs | identity reuse / fabric code contract | A | YES | YES | هذا الاختبار يضمن أن الهوية الرسمية لا تُبدّل عند تكرار العناصر المتطابقة. |
| TailoringEdit_AddPiece_AddsOfficialPriceToExistingTotal_WhenLegacyPieceHasNoPriceSnapshot | LUMAR_ERP_API_V2.Tests/FabricConsumablesFoundationIntegrationTests.OrderEdit.cs | order edit + price sync | A/B | YES | YES | هذا الاختبار مرتبط بالدورة الحالية، لا بالـ Legacy path. |
| TailoringEdit_CancelPiece_PreservesActualPaidDiscountAndRemaining | LUMAR_ERP_API_V2.Tests/FabricConsumablesFoundationIntegrationTests.OrderEdit.cs | order edit + rollback/remaining | A/B | YES | YES | نفس الشيء: مرتبط بالسلوك الحالي، وليس Legacy. |
| TailoringEdit_SameAddOperationIdWithSamePayload_IsReplaySafe | LUMAR_ERP_API_V2.Tests/FabricConsumablesFoundationIntegrationTests.OrderEdit.cs | replay safety | A/B | YES | YES | يختبر Replay / idempotency الخاص بالسلوك الحالي. |
| أي اختبار يربط Fabric legacy أو Fabrics_Inventory بشكل مباشر | أي ملف اختبار قديم أو غير موثق | historical direct fabric tables | C | NO | NO | لا يجوز استخدامه لتحديد سلوك العقد الحديث. إذا كان يختبر C فقط، فلا يسمح بتعديل العقد الحالي لإرضائه. |
| أي اختبار يختبر D path غير موثق | غير محدد | competing path | D | NO | NO | يجب تحديد المسار الرسمي أولًا قبل التعامل معه. |
| أي اختبار لا يوجد له دليل Runtime usage | غير محدد | unknown path | E | NO | NO | لا يُستخدم للحكم قبل حسمه. |

### القاعدة المهمة لاختبارات الدورة
- إذا الاختبار يختبر A أو B: يمكن أن يمنع الإغلاق.
- إذا الاختبار يختبر C فقط: لا يجوز تغيير النظام الحديث لإرضائه.
- إذا الاختبار يختبر D: يجب أولًا تحديد أي Path هو الرسمي.
- إذا الاختبار E: لا يستخدم للحكم حتى يُحسم.

---

## 7) LEGACY / HISTORICAL FREEZE

### LEGACY / HISTORICAL FREEZE

1. المسارات C ليست Source Of Truth.
2. لا يجوز استخدامها لتحديد سلوك جديد.
3. لا يجوز تعديل العقد الحديث لجعل اختبارات Legacy تنجح.
4. لا يجوز توصيل Legacy بمولد CatalogNumber أو FabricCode الحديث لمجرد إعادة إحيائه.
5. لا يجوز حذف Legacy ضمن مهمة حالية إلا بأمر مستقل من المالك.
6. بقاؤه في المستودع لا يعني أنه جزء من Runtime الحالي.
7. أي Failure ناتج فقط عن C لا يفتح العقد الحديث.

---

## 8) COMPETING PATH RULE

### D = DUPLICATE / COMPETING PATH

#### مثال مرجحي داخل هذا المشروع
- OFFICIAL_COMPETITOR = Direct legacy fabric inventory path based on older fabric tables, not the current Foundation + GoodsReceipt + InventoryTransactions chain
- WHY_COMPETING = لأنه يملأ نفس الفكرة الوظيفية (قماش/تخزين/كتالوج) ولكنه لا يثبت أنه هو مسار الRuntime الحالي عند الإسناد الحقيقي.
- CURRENT_RUNTIME_CALLERS = لا يوجد دليل كافٍ على أن Controller أو Service الحالي يستدعيه كحل رسمي في السلسلة الحالية.
- CONTRACT_DIFFERENCE = يختلف عن identity contract الحالي: ItemCode/Barcode/FabricCode لا يتم ربطهما بنفس الارتباط الرسمي للـ Foundation path.
- RISK = قد يسبب تناقضًا في الهوية، أو إعادة توليد FabricCode، أو إضافة كتالوجات غير متطابقة مع current contract.
- RECOMMENDATION = إبقاؤه خارج السلوك الحالي وعدم استخدامه لتحديد new behavior.
- OWNER_DECISION_REQUIRED = YES

يُمنع اختيار Path مفضل بلا دليل Call Chain. أي competitor لا يثبت Runtime usage لا يُؤخذ كـ official path.

---

## 9) Identity Contract (هوية الكتالوج والقماش)

### CatalogNumber = هوية الكتالوج/المجلد.
### FabricCode = هوية القماش/الرول العالمية.

لا توجد هوية أعمال ثالثة مستقلة باسم RollCode.

لعناصر Fabric الحديثة:
- InventoryItems.ItemCode = FabricCode
- FabricRolls.RollCode = FabricCode
- InventoryItems.Barcode = CatalogNumber ضمن نطاق Fabric فقط

لا يعني ذلك أن كل Barcode في InventoryItems هو CatalogNumber.

### القاعدة الرسمية للهوية
- إذا كان العنصر في المسار الحديث Fabric أو Foundation fabric، فيجب أن يتمتع بهوية موحدة:
  - ItemCode = FabricCode
  - RollCode = FabricCode
  - Barcode = CatalogNumber عند استخدام fabric catalog في context الرسمي
- لا يجوز استخدام أي legacy item أو direct fabric row كـ identity replacement في العقد الحديث.
- لا يجوز ربط أي مسار C أو D بمولد CatalogNumber أو FabricCode الحديث بلا تصريح واضح من owner.

---

## 10) Failure Routing Rule

عند ظهور Failure مستقبلًا:

1. حدد Method أو Table أو Test التي فشلت.
2. ارجع إلى هذه الوثيقة.
3. اقرأ Classification.
4. إذا A/B:
   شخّص العقد الحالي.
5. إذا C:
   لا تعدل العقد الحالي بسببه.
6. إذا D:
   لا تصلح قبل حسم المسار الرسمي.
7. إذا E:
   اطلب قرار المالك.
8. يمنع إنشاء Patch مخصوص للاختبار قبل تحديد Classification.

---

## 11) دليل الإثبات للـ classification

### A / B Evidence
- السلسلة الرسمية تتضح من:
  - InventoryController.ReceiveFoundationFabric
  - IInventoryService.ReceiveFabricInventoryAsync
  - InventoryRepository.ReceiveFabricInventoryAsync / ReceiveFabricInventoryBatchAsync / ReceiveFoundationInventoryAsync
  - SystemCodeGenerator.GetNextAllocatedCatalogNumberAsync / GetNextAllocatedFabricCodeAsync
  - AccountingEventPostingGateway.PostInventoryReceiptAsync
  - GoodsReceiptWorkflowCoordinator.CreateAsync
  - InventoryRepository.CreateGoodsReceiptInTransactionAsync
- هذه Methods موجودة في Current Runtime path وتكتب الجداول الرسمية الحالية.

### C / D Evidence
- أي مسار قديم أو منافس لا يثبت أنه مدعو من controller/service الحالي أو لا يكتب الجداول الرسمية الحالية يُعامل كـ C أو D أو E حسب مستوى الإثبات.
- لا يجوز الاستناد إلى أسماء Browsing أو حقول مشابهة فقط، بل يجب إثبات Call Chain أو Runtime usage.

### E Evidence
- إذا لم يوجد دليل كافٍ على Current call chain أو Runtime usage، فيُصنّف على أنه E.
- لا يجوز اعتبار أي Legacy table أو competing path officialًا إلا بعد توثيق owner/owner decision.

---

## 12) ملف التقرير النهائي

المواصفات الإجمالية:

CONTRACT_FILE = CURRENT_SUPPLIER_RECEIPT_FABRIC_STORAGE_BOUNDARY_CONTRACT.md

OFFICIAL_FLOW_DOCUMENTED = YES

A_METHODS = CreateGoodsReceiptAsync, CreateGoodsReceiptInTransactionAsync, CreateGoodsReceiptConfirmation / receipt confirmation, ReceiveFabricInventoryAsync, ReceiveFabricInventoryBatchAsync, ReceiveFoundationInventoryAsync, ReceiveFabricBatchAsync, PostOfficialFabricBatchRollAsync, ReadFoundationItemAsync, InsertFoundationInventoryItemAsync, UpdateFoundationInventoryItemAsync, InsertFabricRollAsync, ApplyFabricPresentationAsync, ResolveFabricCatalogNumberAsync, InsertStorageAllocationAsync, GetNextAllocatedCatalogNumberAsync, GetNextAllocatedFabricCodeAsync, AccountingEventPostingGateway.PostInventoryReceiptAsync, TryReadExistingReceiptAsync, ReadExistingGoodsReceiptAsync

B_METHODS = InventoryService support methods, storage support methods, read/update support methods for destination tables

C_METHODS = Legacy direct fabric storage tables and historical direct batches not proven as source-of-truth

D_METHODS = competing fabric identity or direct fabric batch patterns without proven current runtime callers

E_METHODS = any unresolved or unproven path pending owner decision

A_B_TABLES = SupplierInvoices, SupplierInvoiceLines, GoodsReceipts, GoodsReceiptItems, InventoryItems, InventoryItemFoundation, FabricRolls, InventoryReceiptPostings, InventoryReceiptLines, InventoryTransactions, GoodsReceiptItemStorageAllocations, GlobalCodeAllocations, AccountingEvents, PurchaseOrders, PurchaseOrderItems, Suppliers, System_Settings

LEGACY_TABLES = Fabrics, Fabrics_Inventory

COMPETING_TABLES = any Fabric-direct historical table or alternate direct path not proven in current runtime

CURRENT_CONTRACT_TESTS = FoundationReceipt_MultiRollBatch_StoresEachRollWithoutDuplication, FoundationReceiptAndConsumption_AreBalancedPreciseAndIdempotent, FoundationReceipt_ReusesExistingActiveFabricCodeInsteadOfAllocatingReplacement, TailoringEdit_AddPiece_AddsOfficialPriceToExistingTotal_WhenLegacyPieceHasNoPriceSnapshot, TailoringEdit_CancelPiece_PreservesActualPaidDiscountAndRemaining, TailoringEdit_SameAddOperationIdWithSamePayload_IsReplaySafe

LEGACY_TESTS = any obsolete fabric tests that directly validate historical tables without proving current path usage

METHOD_MATRIX_COMPLETE = YES

TABLE_MATRIX_COMPLETE = YES

TEST_MATRIX_COMPLETE = YES

LEGACY_FREEZE_PRESENT = YES

FAILURE_ROUTING_RULE_PRESENT = YES

ARABIC_FIRST = YES

FILES_CHANGED = [وثيقة العقد فقط]

CODE_CHANGED = NO

DATABASE_CHANGED = NO

FINAL_VERDICT = A — BOUNDARY CONTRACT DOCUMENTED AND READY FOR FUTURE CHATS

---

## 13) النتيجة النهائية

هذا الملف هو العقد الرسمي الحالي الذي يحدد حدود المسارات الرسمية، ولم يُستخدم أي مسار تاريخي أو منافس كـ Source Of Truth. كل مهمة مستقبلية أو دردشة Copilot أو منفذ تقني يجب أن تبدأ من هذا الملف قبل لمس أي جزء من دورة الفاتورة أو الاستلام أو التكويد أو التخزين.

ممنوع تعديل كود التشغيل أو البيانات أو الاختبارات بناءً على المسارات C/D/E دون قرار واضح من المالك.

تطبيق سريع للقاعدة: إذا لم تكن هناك Call Chain موثقة لل path، فلا تُستخدمه.

---

## 14) التحقق الإلزامي للوثيقة

METHOD_MATRIX_COMPLETE = YES

TABLE_MATRIX_COMPLETE = YES

TEST_MATRIX_COMPLETE = YES

LEGACY_FREEZE_PRESENT = YES

COMPETING_PATH_RULE_PRESENT = YES

IDENTITY_CONTRACT_PRESENT = YES

FAILURE_ROUTING_RULE_PRESENT = YES

ARABIC_FIRST = YES

---

## 15) الإغلاق الرسمي لمسار تخزين القماش وهوية FabricCode

### 15.1 الحكم الرسمي للإغلاق

A — FABRIC STORAGE + FABRICCODE ALLOCATION LIVE VERIFIED AND OFFICIALLY CLOSED

هذا المسار أصبح عقدًا مغلقًا ومحميًا بعد نجاح الاختبار الحي على قاعدة البيانات LUMAR_ERP_ES_VALIDATION_R3.

### 15.2 العقد المغلق النهائي

- CatalogNumber = هوية الكتالوج.
- FabricCode = الهوية الرسمية الفريدة عالميًا للرول/القماش.
- RollCode = FabricCode توافقًا، وليس هوية أعمال ثالثة.
- Backend (الخادم الخلفي) هو المسؤول عن تخصيص هوية الرول الجديد.
- Flutter (فلاتر) لا يفرض FabricCode أو CatalogNumber الرسمية الجديدة.
- التخصيص يستأنف من أعلى هوية رسمية موجودة بأمان داخل المعاملة المقفلة.
- لا يستخدم هذا المسار GlobalCodeAllocations.
- لا ينشئ جدول تخصيص أثناء التشغيل.
- الصوف ثبت حيًا: CAT0001 / FA0001..FA0003.
- القطن ثبت حيًا: CAT0002 / FA0004..FA0008.
- الحرير ثبت حيًا: CAT0003 / FA0009..FA0010.
- لا تكرار في FabricCode.
- لا تكرار في RollCode.
- لا رولات يتيمة.
- FabricCode = RollCode مثبت حيًا.
- تخصيص التخزين لكل بند مثبت.
- الحكم الحي النهائي: PASS - FABRIC STORAGE LIVE VERIFIED.

### 15.3 قاعدة الحماية بعد الإغلاق

يُمنع على أي منفذ أو دردشة مستقبلية:

- إعادة تصميم هذا المسار.
- تغيير معنى FabricCode.
- إعادة RollCode كهوية أعمال مستقلة.
- تغيير خوارزمية التخصيص.
- إعادة GlobalCodeAllocations إلى هذا المسار.
- تعديل العقود أو الحمايات المغلقة تحت مسمى تحسين أو تنظيف أو Refactor (إعادة تنظيم الكود).
- إعادة فتح المهمة بسبب فشل غير مرتبط بها.

لا يُعاد فتح هذا العقد أو تعديله إلا بأمر صريح جديد من المالك ياسين.

إذا ظهر Regression (تراجع وظيفي) حي مستقبلًا، يُسجل كحادثة تحتاج تشخيص السبب الجذري أولًا، ولا يعتبر ذلك بحد ذاته إذنًا بإعادة تصميم العقد أو تغييره.

### 15.4 الحكم النهائي للتوثيق

OFFICIAL_CLOSURE_STATUS = LOCKED

REOPEN_REQUIRES_EXPLICIT_OWNER_ORDER = YES

CLOSED_CONTRACT_MODIFIED = NO

LIVE_VERIFICATION = PASS - FABRIC STORAGE LIVE VERIFIED

FINAL_CLIENT_DECLARATION = A — FABRIC STORAGE + FABRICCODE ALLOCATION LIVE VERIFIED AND OFFICIALLY CLOSED

---

