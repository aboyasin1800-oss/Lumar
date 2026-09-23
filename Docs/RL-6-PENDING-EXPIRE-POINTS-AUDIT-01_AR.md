# RL-6-PENDING-EXPIRE-POINTS-AUDIT-01
## تدقيق `PendingExpirePoints` و`LastActivityAt`

## نطاق التدقيق

تم إجراء بحث قراءة فقط في Backend وRepositories وServices وControllers وJobs/Background Services وSQL/Migrations وDTOs وFlutter Models والاختبارات والتوثيق.

لم يتم تعديل قاعدة البيانات أو Backend أو Flutter، ولم يتم إنشاء Migration أو سياسة انتهاء أو تجميد.

## النتيجة المختصرة

- `PendingExpirePoints` حقل Snapshot محفوظ داخل `dbo.LoyaltyAccounts`.
- يبدأ بقيمة `0` عند إنشاء الحساب.
- يُقرأ ويُعاد تمريره في DTOs وعمليات القراءة.
- لا يوجد أي كود يحسبه أو يزيده أو ينقصه أو ينقله من `CurrentPoints`.
- يوجد `UPDATE` يمرر قيمته الحالية كما هي ضمن تحديث الحساب بعد المعاملة.
- لا يوجد Job أو Worker أو SQL/Migration يطبّق انتهاء صلاحية اعتمادًا عليه.
- لا يمثل حاليًا نظام انتهاء صلاحية، ولا نظام تجميد، ولا نظام ترحيل مؤقت فعال.
- أقرب توصيف دقيق له: حقل محجوز/لقطة غير مفعلة تشغيليًا.

`LastActivityAt` مختلف: يتم تحديثه فعليًا عند إنشاء الحساب ومعاملات الولاء والاسترداد، لكنه لا يدخل حاليًا في أي مقارنة زمنية أو قرار Grace/Warning/Freeze.

## الأدلة التفصيلية لـ `PendingExpirePoints`

### DTOs

1. [LoyaltyDtos.cs](../Backend/LUMAR_ERP_API_V2/DTOs/Loyalty/LoyaltyDtos.cs)
   - `LoyaltyAccountDto.PendingExpirePoints`.
   - `LoyaltyBalanceDto.PendingExpirePoints`.
2. [CustomerDtos.cs](../Backend/LUMAR_ERP_API_V2/DTOs/Customers/CustomerDtos.cs)
   - `CustomerLoyaltyDto.PendingExpirePoints`.

هذه تعريفات نقل بيانات فقط، ولا تحتوي على منطق حساب.

### القراءة في `LoyaltyRepository`

في [LoyaltyRepository.cs](../Backend/LUMAR_ERP_API_V2/Repositories/LoyaltyRepository.cs):

- `GetAccountByCustomerAsync`: يقرأ `PendingExpirePoints` من `dbo.LoyaltyAccounts`.
- `EnsureAccountAsync`: يقرأ القيمة عبر `OUTPUT INSERTED` بعد إنشاء الحساب.
- `GetBalanceAsync`: يعيد القيمة ضمن `LoyaltyBalanceDto`.
- `CreateTransactionAsync`: يأخذ القيمة من الحساب ويضعها في الحساب المحدّث دون عملية حساب عليها.

الاستعلام المستخدم للقراءة:

```sql
SELECT LoyaltyAccountId, CustomerId, CurrentPoints,
       LifetimeEarnedPoints, LifetimeRedeemedPoints,
       PendingExpirePoints, VipLevelId, CreatedAt,
       UpdatedAt, LastActivityAt
FROM dbo.LoyaltyAccounts
WHERE CustomerId = @customerId;
```

### الكتابة في `LoyaltyRepository`

عند إنشاء الحساب في `EnsureAccountAsync`:

```sql
INSERT INTO dbo.LoyaltyAccounts
    (CustomerId, CurrentPoints, LifetimeEarnedPoints,
     LifetimeRedeemedPoints, PendingExpirePoints, VipLevelId,
     CreatedAt, UpdatedAt, LastActivityAt)
VALUES
    (@customerId, 0, 0, 0, 0, NULL,
     SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME());
```

هذه تهيئة أولية إلى صفر وليست عملية ترحيل أو انتهاء.

بعد المعاملة في `CreateTransactionAsync` يوجد:

```csharp
PendingExpirePoints = result.Account.PendingExpirePoints
```

ثم SQL:

```sql
UPDATE dbo.LoyaltyAccounts
SET CurrentPoints = @currentPoints,
    LifetimeEarnedPoints = @lifetimeEarnedPoints,
    LifetimeRedeemedPoints = @lifetimeRedeemedPoints,
    PendingExpirePoints = @pendingExpirePoints,
    UpdatedAt = SYSUTCDATETIME(),
    LastActivityAt = SYSUTCDATETIME()
WHERE LoyaltyAccountId = @loyaltyAccountId;
```

القيمة الممررة هي قيمة الحساب السابقة نفسها؛ `LoyaltyAccountResolver.ApplyTransaction` لا يغيّر `PendingExpirePoints`.

### القراءة في `LoyaltyRedemptionRepository`

في [LoyaltyRedemptionRepository.cs](../Backend/LUMAR_ERP_API_V2/Repositories/LoyaltyRedemptionRepository.cs)، يقرأ `ApplyAtomicAsync`:

```sql
SELECT LoyaltyAccountId, CurrentPoints,
       LifetimeEarnedPoints, LifetimeRedeemedPoints,
       PendingExpirePoints
FROM dbo.LoyaltyAccounts WITH (UPDLOCK, HOLDLOCK)
WHERE CustomerId = @customerId;
```

يتم وضع القيمة في متغير محلي `pendingExpire` ثم تُمرر إلى `LoyaltyAccountDto` الناتج بعد الاسترداد. لا يوجد تعديل لها في `accountUpdateSql`.

### عدم وجود SQL أو Jobs

البحث الحرفي عن `PendingExpirePoints` في ملفات SQL وMigrations لم يجد استخدامًا إضافيًا.

ولم يجد التدقيق Job أو Background Service أو Scheduled Task ينفذ:

- زيادة `PendingExpirePoints`.
- إنقاص `PendingExpirePoints`.
- نقل نقاط من `CurrentPoints` إليه.
- إزالة نقاط منه أو مصادرتها.

### Flutter والاختبارات

- [loyalty_models.dart](../frontend/tailoring_system/lib/models/loyalty_models.dart): يقرأ الحقل في نماذج الحساب والتوازن فقط.
- [customer_models.dart](../frontend/tailoring_system/lib/models/customer_models.dart): يقرأ الحقل في نموذج ولاء العميل فقط.
- `LoyaltyPhaseTwoTests` يتحقق من أن القيمة الابتدائية صفر.
- لا توجد شاشة أو خدمة Flutter تطبق سياسة انتهاء على القيمة.

## أين ينقص `CurrentPoints`؟

### الاسترداد الذري

في [LoyaltyRedemptionRepository.cs](../Backend/LUMAR_ERP_API_V2/Repositories/LoyaltyRedemptionRepository.cs):

```sql
VALUES (..., N'Redeem', @points, @before, @after, ...)
```

والقيمة:

```csharp
@after = currentPoints - pointsRedeemed
```

ثم:

```sql
UPDATE dbo.LoyaltyAccounts
SET CurrentPoints = @currentPoints,
    LifetimeRedeemedPoints = @lifetimeRedeemed,
    LastActivityAt = SYSUTCDATETIME()
WHERE LoyaltyAccountId = @accountId;
```

هذا هو الإنقاص التشغيلي الصريح المخصص للاسترداد، مع فحص أن `pointsRedeemed` لا يتجاوز الرصيد.

### المعاملات العامة

في [LoyaltyAccountResolver.cs](../Backend/LUMAR_ERP_API_V2/Utilities/LoyaltyAccountResolver.cs):

```csharp
var balanceAfter = balanceBefore + points;
```

وفي [LoyaltyController.cs](../Backend/LUMAR_ERP_API_V2/Controllers/LoyaltyController.cs) يوجد:

`POST /api/loyalty/transactions`

وهو يقبل `points` من الطلب العام دون أن يفرض في Controller أن تكون موجبة. لذلك يمكن نظريًا تمرير قيمة سالبة وإنقاص `CurrentPoints` عبر هذا المسار، بحسب صلاحيات واستعمال العميل لهذا endpoint.

### الكسب والتعديلات

- مسار كسب الطلب في [OrderLoyaltyIntegrationService.cs](../Backend/LUMAR_ERP_API_V2/Services/OrderLoyaltyIntegrationService.cs) ينشئ معاملة `Earn` بموجب `buyerDelta` غير سالب.
- مكافآت الإحالة تنشئ `Adjust` بقيم موجبة في المسار الحالي.
- التراجع عن الاسترداد ينشئ `Reversal` موجبًا لإعادة النقاط.

## استخدام `LastActivityAt`

### الكتابة الفعلية

1. [LoyaltyRepository.cs](../Backend/LUMAR_ERP_API_V2/Repositories/LoyaltyRepository.cs)
   - `EnsureAccountAsync`: يكتب `SYSUTCDATETIME()` عند إنشاء الحساب.
   - `CreateTransactionAsync`: يكتب `SYSUTCDATETIME()` عند كل معاملة تمر عبر Repository.
2. [LoyaltyAccountResolver.cs](../Backend/LUMAR_ERP_API_V2/Utilities/LoyaltyAccountResolver.cs)
   - `EnsureAccount`: يضع وقت الإنشاء في DTO الافتراضي.
   - `ApplyTransaction`: يحدّث `LastActivityAt` إلى `DateTime.UtcNow`.
3. [LoyaltyRedemptionRepository.cs](../Backend/LUMAR_ERP_API_V2/Repositories/LoyaltyRedemptionRepository.cs)
   - `ApplyAtomicAsync`: يحدّث `LastActivityAt` عند الاسترداد الذري.

### القراءة الفعلية

- `LoyaltyRepository.GetAccountByCustomerAsync`.
- `LoyaltyRepository.GetBalanceAsync`.
- `CustomerRepository.GetLoyaltyAsync` مع عرض بيانات VIP.
- DTOs وFlutter Models.

### هل هو مستخدم أم تسجيل فقط؟

هو مستخدم فعليًا كسجل زمني لآخر معاملة/نشاط؛ ليس مجرد عمود لا يتغير. لكنه في الوضع الحالي **تسجيل تاريخ فقط** من ناحية القرار التشغيلي، لأن التدقيق لم يجد:

- مقارنة `LastActivityAt` مع تاريخ حالي.
- حساب عدد أيام inactivity.
- استخدامه في منح أو منع النقاط.
- استخدامه في إشعار أو إنذار.
- استخدامه في تجميد أو إعادة تفعيل الحساب.
- Job أو Worker يراقبه.

## تصنيف `PendingExpirePoints`

| الاحتمال | النتيجة |
|---|---|
| نظام انتهاء صلاحية فعال | لا |
| نظام تجميد | لا |
| نظام ترحيل مؤقت فعال | لا يوجد دليل على ذلك |
| حقل غير مستخدم تشغيليًا | نعم، مع قراءة وتهيئة وتمرير فقط |

## هل يصلحان كأساس للمرحلة القادمة؟

نعم، لكن كجزء من نموذج جديد وليس كنظام قائم جاهز:

- `LastActivityAt` يصلح كبداية لحساب inactivity وبدء Grace Period.
- `PendingExpirePoints` يمكن أن يمثل رصيدًا مرشحًا للانتهاء مستقبلًا، لكن معناه الحالي غير مثبت، ولا يجوز تعبئته أو إنقاصه قبل اعتماد سياسة واضحة.
- لا يكفي الحقلان وحدهما لبناء Account Freeze؛ يلزم على الأقل حالة الحساب، تاريخ بدء Grace، تاريخ الإنذار، سياسة التجميد، وآلية إعادة التفعيل.
- يلزم تحديد هل الرصيد المرشح للانتهاء يُخصم من `CurrentPoints` أم يُفصل محاسبيًا، وكيف تحفظ الحركات والتراجع والتدقيق.
- يجب إضافة Job/Worker أو آلية تشغيل مجدولة، وحراس تمنع Earn وRewards وVIP benefits للحساب المجمد، واختبارات تكامل.

## القرار

لا يجوز في المرحلة التالية اعتبار `PendingExpirePoints` نظام انتهاء أو تجميد قائمًا. يمكن استخدام `LastActivityAt` و`PendingExpirePoints` كأساس بيانات أولي بعد اعتماد عقد أعمال جديد، لكن التنفيذ يتطلب تصميمًا ومراجعة ومigration منفصلة واختبارات قبل ربطه بشاشة إعدادات النقاط.

لم يتم تغيير مصدر النقاط المكتسبة: يبقى `LoyaltyPiecePointSettings` حسب `ProductTypeId`.

كتب هذا التقرير المطور سعد
