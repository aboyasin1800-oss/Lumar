# تقرير Migration النهائي للمراجعة: RL-6-LOYALTY-POINTS-SETTINGS-CENTER-01

## الحالة

تم اعتماد نتيجة التدقيق والمصادر الرسمية. أُعد ملفا Migration النهائيان ونُفذا بنجاح على قاعدة البيانات المحلية. لم يتم تشغيل أمر `database update`؛ التطبيق تم مباشرة عبر سكربتي SQL المعتمدين. نُفذت تعديلات Backend وبدأ تحقق Flutter.

ملف Migration:

`Backend/LUMAR_ERP_API_V2/Migrations/20260918_RL6_LOYALTY_POINTS_SETTINGS_CENTER_01.sql`

`Backend/LUMAR_ERP_API_V2/Migrations/20260918_RL6_LOYALTY_POINTS_SETTINGS_CENTER_02_ORDER_SOURCE_KEYS.sql`

## نتيجة تدقيق الوجود الصفري

| البند | النتيجة الحالية | الدليل أو القيد |
|---|---|---|
| `PricingProductTypes.ProductTypeId` | موجود كمصدر رسمي في Backend | إدارة القياسات وقواعد الاستهلاك تستخدم هذا المصدر |
| `LoyaltyPiecePointSettings.ProductTypeId` | `0` قبل الإضافة | فحص `sys.columns` داخل Migration؛ الجدول الحالي يستخدم `PieceCode`, `PieceName`, `Points`, `IsActive` |
| FK من إعداد نقاط منتجاتنا إلى `PricingProductTypes` | `0` قبل الإضافة | فحص اسم القيد الجديد داخل `sys.foreign_keys` |
| unique index للربط الرسمي | `0` قبل الإضافة | فحص `UX_LoyaltyPiecePointSettings_ProductTypeId` داخل `sys.indexes` |
| جدول بديل لإعداد نقاط منتجاتنا بالمعرف الرسمي | غير موجود في Backend المدقق | لا يوجد Repository/DTO/endpoint رسمي لذلك |
| `ImportedReadyMadeProducts.ImportedReadyMadeProductId` | موجود كمصدر رسمي | `GET /inventory/imported` يعيد المعرف |
| جدول إعداد نقاط المستورد | `0` قبل الإضافة | فحص `sys.tables` للاسم الجديد |
| نقاط مضمّنة في مصدر المستورد | غير مثبتة | DTO الحالي يعيد بيانات الصنف ولا يعيد إعداد نقاط |
| ربط تاريخي للمستورد في الولاء | غير مثبت | لا يجوز إعادة تفسير حركات `LoyaltyTransactions` |
| Endpoint/service لإعداد نقاط المستورد | غير موجود | سيُنفذ بعد اعتماد وتطبيق Migration فقط |
| انتقال المعرف إلى المبيعات | غير مكتمل | `OrderItems` و`CreateOrderItemDto` لا يحملان `ImportedReadyMadeProductId`؛ الواجهة تحتفظ به ضمن بيانات العرض/snapshot فقط |

## بيانات قديمة يجب عدم حذفها

يوثق تدقيق قاعدة البيانات السابق أن عدد صفوف `LoyaltyPiecePointSettings` كان صفًا واحدًا وقت الالتقاط، وأن عدد حركات `LoyaltyTransactions` كان `111`. هذه البيانات لا تُحذف ولا تُعاد حسابها.

الصف القديم في `LoyaltyPiecePointSettings` يصنف كالتالي إلى أن يثبت العكس من صاحب العمل:

- صف legacy غير مربوط بـ`ProductTypeId`.
- لا تتم محاولة مطابقته بالاسم أو `PieceCode` أو `TrackingCode` أو `TRK`.
- لا ينقل تلقائيًا إلى أي `ProductTypeId`.
- يبقى محفوظًا لأغراض التوافق/المراجعة، لكن لا يصبح أساسًا للربط الرسمي الجديد.

ملاحظة تشغيلية: الاتصال الحي في جلسة التدقيق الحالية لم يُرجع نتيجة لاختبار SQL البسيط، لذلك يجب إعادة تنفيذ استعلام مخطط قراءة فقط قبل اعتماد الأسماء النهائية للقيود والفهارس وأعداد الصفوف الحالية.

## Migration النهائي للمراجعة

### 1. ربط إعدادات منتجاتنا بالمصدر الرسمي

إضافة عمود nullable إلى `dbo.LoyaltyPiecePointSettings`:

- `ProductTypeId int NULL`
- FK إلى `dbo.PricingProductTypes(ProductTypeId)`
- فهرس unique مفلتر على `ProductTypeId` عندما تكون القيمة غير NULL
- الإبقاء على الصفوف القديمة غير المربوطة دون حذف
- عدم إنشاء قيمة تلقائية من `PieceCode` أو الاسم

اسم القيد: `FK_LoyaltyPiecePointSettings_PricingProductTypes_ProductTypeId`.

اسم الفهرس الفريد المفلتر: `UX_LoyaltyPiecePointSettings_ProductTypeId`.

يُضاف تحقق غير سالب للنقاط إن لم يكن موجودًا، مع احترام القيد الفعلي الذي سيؤكده استعلام المخطط.

### 2. الجدول الجديد لإعداد نقاط الأصناف المستوردة

إنشاء جدول مستقل باسم نهائي `dbo.LoyaltyImportedReadyMadeProductPointSettings`:

- `ImportedReadyMadeProductId int NOT NULL`
- مفتاح أساسي على `ImportedReadyMadeProductId`
- FK إلى `dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId)`
- `Points decimal(18,2) NOT NULL` أو النوع المتوافق مع إعدادات النقاط بعد تأكيد المخطط
- `IsActive bit NOT NULL` بقيمة افتراضية `1`
- `CreatedAt datetime2 NOT NULL`
- `UpdatedAtUtc datetime2 NOT NULL`
- تحقق `Points >= 0`

المفتاح الأساسي/القيد الفريد: `PK_LoyaltyImportedReadyMadeProductPointSettings` على `ImportedReadyMadeProductId`.

المفتاح الأجنبي: `FK_LoyaltyImportedReadyMadeProductPointSettings_ImportedReadyMadeProducts` إلى `dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId)`.

الفهرس: `IX_LoyaltyImportedReadyMadeProductPointSettings_IsActive` على `(IsActive, ImportedReadyMadeProductId)`.

لا يضيف الجدول أصنافًا مستوردة، ولا يستخدم `ProductCode` أو الاسم أو رقم البيع كمفتاح.

### 3. تمرير معرف المستورد في البيع

قبل اعتماد احتساب نقاط المستورد، يجب إضافة `ImportedReadyMadeProductId` إلى عقد عنصر البيع ومسار التخزين المناسب، مع FK/تحقق ملائم بعد تحديد جداول المبيعات التي تمثل بيع المستورد فعليًا.

وجود المعرف في Flutter داخل snapshot لا يكفي؛ لا يجوز للمحرك الاعتماد على نص snapshot أو على `ProductCode`.

## سياسة البيانات القديمة

- لا حذف للصفوف القديمة.
- لا تعديل أو إعادة حساب لـ`LoyaltyTransactions`.
- لا تحويل تلقائي للصف legacy إلى نوع رسمي.
- تصنيف الصفوف غير القابلة للمطابقة كـ`UnlinkedLegacy` في تقرير الإدارة أو أداة المراجعة، لا كنوع منتج جديد.
- بعد اكتمال الربط الرسمي، الإعداد المفقود أو غير النشط أو ذو النقاط الصفرية يمنح صفر نقاط، ولا يوجد fallback إلى `PointsPerPiece`.
- إزالة fallback والهاردكود `100` من المحرك تكون في مرحلة Backend اللاحقة، بعد اعتماد Migration والعقد.

## ملخص Up/Down المقترح

### Up

1. تحقق من وجود الجداول والأعمدة المرجعية.
2. أضف `ProductTypeId` nullable إلى `LoyaltyPiecePointSettings` إن لم يكن موجودًا.
3. أنشئ FK والفهرس unique المفلتر.
4. أنشئ جدول إعداد نقاط المستورد مع PK وFK والفهارس والقيود.
5. لا تملأ الربط تلقائيًا ولا تعدل الحركات التاريخية.

### Down

1. احذف FK والفهرس المضافين.
2. احذف عمود `ProductTypeId` فقط بعد إزالة استخدامه من التطبيق، مع رفض الرجوع إذا احتوى بيانات ربط دون تصدير/مراجعة صريحة.
3. احذف جدول إعداد نقاط المستورد فقط إذا كان فارغًا أو بعد إجراء ترحيل عكسي معتمد.
4. لا تحذف أي صف من `LoyaltyTransactions`.

## نتائج فحوص الوجود الصفري داخل Migration

كل نتيجة أدناه يجب أن تكون `0` قبل تنفيذ عملية الإنشاء. إذا كانت غير صفرية، يستخدم الملف `THROW` وتُلغى المعاملة:

| العنصر المراد إضافته | فحص الوجود | النتيجة المطلوبة قبل الإضافة |
|---|---|---:|
| `LoyaltyPiecePointSettings.ProductTypeId` | `sys.columns` | `0` |
| `UX_LoyaltyPiecePointSettings_ProductTypeId` | `sys.indexes` | `0` |
| `FK_LoyaltyPiecePointSettings_PricingProductTypes_ProductTypeId` | `sys.foreign_keys` | `0` |
| `LoyaltyImportedReadyMadeProductPointSettings` | `sys.tables` | `0` |

أما الكيانات المرجعية الرسمية فليست عناصر جديدة، ولذلك يجب أن تكون موجودة: `PricingProductTypes`, `PricingProductTypes.ProductTypeId`, `ImportedReadyMadeProducts`, و`ImportedReadyMadeProducts.ImportedReadyMadeProductId`. غياب أي منها يوقف Migration.

## الأعمدة والجداول والمفاتيح والقيود

| النوع | الاسم | التفاصيل |
|---|---|---|
| عمود جديد | `LoyaltyPiecePointSettings.ProductTypeId` | `int NULL` |
| جدول جديد | `LoyaltyImportedReadyMadeProductPointSettings` | إعداد واحد لكل `ImportedReadyMadeProductId` |
| FK | `FK_LoyaltyPiecePointSettings_PricingProductTypes_ProductTypeId` | إلى `PricingProductTypes(ProductTypeId)` |
| FK | `FK_LoyaltyImportedReadyMadeProductPointSettings_ImportedReadyMadeProducts` | إلى `ImportedReadyMadeProducts(ImportedReadyMadeProductId)` |
| قيد فريد | `UX_LoyaltyPiecePointSettings_ProductTypeId` | فهرس unique مفلتر للقيم غير الفارغة |
| قيد فريد/مفتاح أساسي | `PK_LoyaltyImportedReadyMadeProductPointSettings` | على `ImportedReadyMadeProductId` |
| قيد تحقق | `CK_LoyaltyImportedReadyMadeProductPointSettings_Points` | `Points >= 0` |
| فهرس | `IX_LoyaltyImportedReadyMadeProductPointSettings_IsActive` | `(IsActive, ImportedReadyMadeProductId)` |

### مفاتيح مصدر البيع

أضيفت إلى `dbo.OrderItems` بعد فحوص صفرية مستقلة:

| النوع | الاسم | التفاصيل |
|---|---|---|
| عمود جديد | `OrderItems.ProductTypeId` | `int NULL` |
| عمود جديد | `OrderItems.ImportedReadyMadeProductId` | `int NULL` |
| FK | `FK_OrderItems_PricingProductTypes_ProductTypeId` | إلى `PricingProductTypes(ProductTypeId)` |
| FK | `FK_OrderItems_ImportedReadyMadeProducts_ImportedReadyMadeProductId` | إلى `ImportedReadyMadeProducts(ImportedReadyMadeProductId)` |
| فهرس | `IX_OrderItems_ProductTypeId` | فهرس مفلتر للقيم غير الفارغة |
| فهرس | `IX_OrderItems_ImportedReadyMadeProductId` | فهرس مفلتر للقيم غير الفارغة |

نتائج التطبيق الفعلية:

- `RL6_MIGRATION_APPLIED`
- `RL6_ORDER_SOURCE_KEYS_APPLIED`

## ما يحتاج اعتمادًا قبل التنفيذ

1. مراجعة أسماء الكيانات والقيود وأنواع الأعمدة أعلاه.
2. اعتماد سياسة الصف legacy غير المربوط.
3. اعتماد تعديل عقد البيع لاحقًا لحفظ `ImportedReadyMadeProductId`.
4. الموافقة الصريحة على تطبيق Migration فقط. لا تشمل هذه الموافقة تنفيذ `database update` تلقائيًا ما لم يُذكر ذلك صراحة.

## القرار المطلوب

تم تنفيذ Migration وBackend. اختبارات المحرك وتكامل الطلب نجحت `8/8`. الاختبارات الكاملة بنت المشروع لكنها تضمنت خمس اختبارات تكامل غير مرتبطة فشلت بسبب عدم إتاحة اتصال SQL Server. بدأ تنفيذ Flutter، ونجح تشخيص VS Code للملفات المعدلة بلا أخطاء، بينما تعذر hot restart وhot reload من DTD بخطأ عام `-32603` دون أخطاء تشغيل مسجلة.

كتب هذا التقرير المطور سعد
