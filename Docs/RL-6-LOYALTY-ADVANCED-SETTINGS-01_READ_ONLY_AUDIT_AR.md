# RL-6-LOYALTY-ADVANCED-SETTINGS-01
## تقرير تدقيق قراءة فقط لإعدادات الولاء المتقدمة

## نطاق التدقيق

تمت مراجعة المصادر المتاحة في:

- قائمة جداول قاعدة البيانات الموثقة في `DATABASE_INVENTORY.md`.
- ملفات Migration الخاصة بمركز إعدادات النقاط.
- DTOs وRepositories وServices وControllers في Backend.
- Models وRepository وشاشات Flutter.
- الاختبارات والعقود والتوثيق السابق.

لم يتم إنشاء جدول أو عمود أو Migration أو Mock Data، ولم يتم تغيير قواعد احتساب النقاط.

## الإعدادات الموجودة فعليًا

### إعدادات البرنامج العامة

المصدر: `dbo.LoyaltyProgramSettings`.

الحقول الموجودة:

- `IsEnabled`: حالة برنامج الولاء.
- `PointsPerPiece`: قيمة إدارية محفوظة للتوافق والعرض، وليست مصدر النقاط الرسمي حسب التدقيق السابق.
- `PointMonetaryValue`: قيمة النقطة المستخدمة في الاسترداد.
- `EffectiveFromUtc`: بداية سريان الإعداد.
- `UpdatedAtUtc`: وقت التحديث.

المسار البرمجي:

- DTO: `LoyaltyProgramSettingsDto` في `DTOs/Loyalty/LoyaltyPiecePointDtos.cs`.
- Repository: `LoyaltyManagementSettingsRepository`.
- Service: `LoyaltyManagementSettingsService`.
- Endpoints: `GET /api/loyalty-management/program-settings` و`PUT /api/loyalty-management/program-settings/{id}`، إضافة إلى مساري التفعيل والتعطيل.
- Flutter: `LoyaltyRepository.getProgramSettings/updateProgramSettings` وشاشة `PointsSettingsScreen`.

هذه الحقول معروضة حاليًا داخل شاشة إعدادات النقاط.

### بيانات حساب العميل الموجودة

المصدر: `dbo.LoyaltyAccounts`.

الحقول المرتبطة بالتدقيق:

- `CurrentPoints`.
- `LifetimeEarnedPoints`.
- `LifetimeRedeemedPoints`.
- `PendingExpirePoints`.
- `VipLevelId`.
- `LastActivityAt`.

`LastActivityAt` قيمة نشاط للحساب وليست إعدادًا زمنيًا أو سياسة Grace Period. ويتم تحديثها عند معاملات الحساب الحالية.

### مستويات VIP

المصدر: `dbo.VipLevels`، مع ربط اختياري من `LoyaltyAccounts.VipLevelId`.

الحقول الموجودة:

- `MinimumPoints`.
- `Multiplier`.
- `Priority`.
- `IsActive`.

المسارات:

- `LoyaltyManagementSettingsRepository` و`LoyaltyManagementSettingsService`.
- `/api/loyalty-management/vip-levels` ومسارات القراءة والتعديل والتفعيل والتعطيل.
- شاشة Flutter مستقلة لمستويات VIP.

الربط الفعلي هو حفظ المستوى وعرضه وتجميعه. لم يثبت تطبيق `Multiplier` على محرك Earn أو Referral، ولا يوجد ربط فعلي بين التجميد والنشاط وVIP.

### قواعد الولاء

المصدر: `dbo.LoyaltyRules`.

الحقول الموجودة تشمل النوع والحالة والقيمة والمضاعف والمكافأة والأولوية وربط VIP وفترة بداية ونهاية اختيارية.

يوجد CRUD في Repository وService وEndpoints `/api/loyalty-management/loyalty-rules`. لكن التدقيق لم يجد محركًا تشغيليًا يطبق هذه القواعد على منح النقاط، لذلك لا تُعرض كسياسات تجميد أو استبدال داخل شاشة إعدادات النقاط.

### معاملات الاسترداد

المصادر: `dbo.LoyaltyRedemptions` و`dbo.LoyaltyTransactions` و`dbo.CustomerLedgerEntries`.

المسارات الحالية:

- `POST /api/loyalty-redemptions/preview`.
- `POST /api/loyalty-redemptions/apply`.
- `POST /api/loyalty-redemptions/reverse`.

الموجود هو تسجيل عملية الاسترداد وحساب قيمتها والتحقق من الرصيد وقيمة النقطة وحالة البرنامج. لا توجد في هذه الجداول أو DTOs أو endpoints إعدادات حدود أو سماح مستقلة.

## نتائج الأقسام المطلوبة

| القسم | النتيجة | قرار الشاشة |
|---|---|---|
| إعدادات الاستبدال | لا يوجد حد أدنى أو حد أعلى أو `AllowRedemption` أو إعداد تفعيل مستقل | لم تتم إضافة حقول |
| فترة السماح | لا يوجد `GracePeriod` أو `ActivityWindow` أو سياسة مبنية على `LastActivityAt` | لم تتم إضافة حقول |
| فترة الإنذار | لا يوجد `WarningPeriod` أو `NotificationPeriod` مرتبط بالولاء قبل التجميد | لم تتم إضافة حقول |
| تجميد الحساب | لا يوجد `IsFrozen` أو `AccountStatus` أو `FreezeDate` أو `FreezePolicy` أو `LoyaltyAccountStatus` | لا يدعم النظام التجميد حاليًا |
| سياسات نشاط العميل | يوجد `LastActivityAt` فقط كبيان حساب، ويمكن اشتقاق آخر حركة من معاملات الولاء | لا توجد سياسة قابلة للتعديل لعرضها |
| التكامل مع VIP | يوجد `VipLevelId` و`VipLevels` وحقول `MinimumPoints/Multiplier` | الربط الإداري موجود، ولا يوجد تأثير مثبت للتجميد أو النشاط أو Multiplier على المحرك |

## السياسة المستقبلية المطلوبة

السياسة المذكورة في الطلب غير مطبقة حاليًا. لا يوجد مسار يجمّد الحساب بعد انتهاء مدة، ولا مسار يعيد تفعيله مع إعادة بدء مدة، ولا حماية تشغيلية تمنع VIP أو المكافآت بسبب التجميد.

لذلك لم يتم تنفيذ أي جزء منها، مع الحفاظ على الرصيد والحركات الحالية دون حذف أو مصادرة.

## ما تم عرضه داخل الشاشة

تبقى شاشة إعدادات النقاط على الإعدادات ذات العقد الفعلي فقط:

- حالة البرنامج.
- `PointsPerPiece` للعرض والتوافق الإداري فقط.
- `PointMonetaryValue`.
- تاريخ السريان.
- مفتاح تفعيل وتعطيل البرنامج.
- إعدادات النقاط المرتبطة بالمصدر الرسمي `ProductTypeId` وإعدادات الأصناف المستوردة الحالية.

لم تتم إضافة شاشة مستقلة أو أقسام وهمية للإعدادات غير الموجودة.

## ما يحتاج بناءً مستقبليًا

قبل أي تنفيذ مستقبلي يلزم تصميم واعتماد عقد جديد يشمل على الأقل:

1. أعمدة وإعدادات حدود الاستبدال والسماح به.
2. سياسة مدة السماح ومصدر آخر نشاط المعتمد.
3. سياسة الإنذار وقناة الإشعار وقوالبه.
4. حالة الحساب المجمد وتواريخ التجميد وإعادة التفعيل.
5. خدمة أو Worker لتقييم السياسات دون حذف الرصيد.
6. حراس تشغيلية تمنع Earn وRewards وVIP benefits للحساب المجمد.
7. قرار صريح حول تطبيق VIP Multiplier وقواعد LoyaltyRules على المحرك.
8. DTOs وRepositories وServices وEndpoints واختبارات تكامل قبل إضافة أي واجهة.

## نتيجة التدقيق

- يدعم النظام حاليًا إعدادات البرنامج العامة وقيمة النقطة والاسترداد الأساسي ومستويات VIP وبيان آخر نشاط.
- لا يدعم حاليًا حدود الاستبدال كإعدادات.
- لا يدعم حاليًا فترة السماح.
- لا يدعم حاليًا فترة الإنذار.
- لا يدعم حاليًا تجميد حساب الولاء.
- لا يوجد مبرر تقني لإضافة أقسام إدخال جديدة إلى شاشة إعدادات النقاط في هذه المهمة.
- بقي مصدر النقاط المكتسبة الرسمي هو `LoyaltyPiecePointSettings` حسب `ProductTypeId`، ولم تتم إعادته إلى `PointsPerPiece`.

## نتائج التحقق

لم تُجر أي تغييرات برمجية في هذه المهمة بعد التدقيق، لذلك يجب تشغيل Backend Build وFlutter Analyze وWindows Build بعد أي تنفيذ لاحق لعقد جديد. آخر تحقق سابق للشاشة المعدلة كان خاليًا من أخطاء Dart ووقت التشغيل، لكنه لا يُعد Build جديدًا لهذه المهمة التي انتهت كتدقيق وتوثيق فقط.

كتب هذا التقرير المطور سعد
