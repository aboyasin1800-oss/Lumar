# تدقيق قراءة فقط: إعادة بناء مركز إعدادات الولاء والنقاط

## الحالة

هذا التقرير ناتج عن تدقيق قراءة فقط. لم يتم تعديل Flutter أو Backend أو قاعدة البيانات، ولم يتم إنشاء Migration.

## 1. المصدر الرسمي لقطع منتجاتنا

المصدر الحالي المثبت في Backend هو `dbo.PricingProductTypes`، وتدعمه شاشة إدارة القياسات وقواعد الاستهلاك.

المعرف الرسمي الثابت هو `ProductTypeId`.

الحقول المناسبة للعرض هي:

- `NameAr`: اسم النوع.
- `Code`: رمز نوع المنتج التجاري عند الحاجة.
- `IsActive`: حالة النوع.

إدارة القياسات تنشئ وتعدل هذه الكيانات من خلال مسارات `consumption-rules/measurement-types`. لا يوجد دليل مقبول على أن `TrackingCode` أو `TRK` أو Barcode يمثل هوية نوع المنتج.

## 2. فجوة الربط الحالية لمنتجاتنا

جدول `LoyaltyPiecePointSettings` الحالي يملك:

- `LoyaltyPiecePointSettingId`
- `PieceCode`
- `PieceName`
- `Points`
- `IsActive`

ولا يملك `ProductTypeId`.

لذلك لا يمكن تنفيذ الربط الثابت المطلوب بين إعداد النقاط و`PricingProductTypes.ProductTypeId` بالاعتماد على العقد الحالي. استخدام الاسم أو `Code` كحل بديل يحتاج عقدًا واضحًا، ولا يجوز استخدام TRK أو أكواد القطع المنتجة.

## 3. المصدر الرسمي للأصناف المستوردة

المصدر الحالي المثبت في Backend هو `dbo.ImportedReadyMadeProducts` عبر:

`GET /inventory/imported`

المعرف الثابت هو `ImportedReadyMadeProductId`.

الحقول الحالية تشمل:

- `ProductName`
- `ProductType`
- `ProductCode`
- `Unit`
- `Quantity`
- `IsActive`
- `Category`

لا يوجد جدول أو endpoint حالي لإعداد نقاط المستورد، ولا توجد علاقة حالية بين `ImportedReadyMadeProducts` وإعدادات النقاط.

## 4. إعدادات البرنامج الحالية

Endpoint القراءة:

`GET /api/loyalty-management/program-settings`

Endpoint التعديل:

`PUT /api/loyalty-management/program-settings/{id}`

الحقول الفعلية:

- `IsEnabled`
- `PointsPerPiece`
- `PointMonetaryValue`
- `EffectiveFromUtc`

قواعد التحقق الحالية:

- `PointsPerPiece >= 0`
- `PointMonetaryValue > 0`

## 5. الحقول غير الموجودة

ليست موجودة في DTO أو Endpoint إعداد البرنامج الحالي:

- الحد الأدنى للاستبدال.
- الحد الأقصى للاستبدال.
- السماح بكسب النقاط.
- السماح باستبدال النقاط.
- فترة الإنذار.
- فترة انتهاء الصلاحية.
- فترة المصادرة النهائية.

لم يتم إنشاء أعمدة أو Migration أو واجهات وهمية لهذه الحقول.

## 6. نتيجة تدقيق fallback

في `PiecePointSettingsRepository.EvaluatePiecePointsAsync` و`OrderLoyaltyIntegrationService.ProcessOrderAsync`:

1. يحاول النظام قراءة إعداد القطعة الفعّال باستخدام `PieceCode`.
2. يقرأ `PointsPerPiece` من إعداد البرنامج.
3. يمرر القيمة العامة إلى `PiecePointsEngine.CalculateForItem`.

النتيجة المثبتة: إعداد قطعة غير موجود أو غير فعّال يدخل مسار fallback إلى `PointsPerPiece` العام، وليس مسار "لا يمنح نقاطًا".

لم يتم تغيير هذا السلوك.

## 7. التفعيل العام

`IsEnabled` محفوظ في إعداد البرنامج، وتوجد مسارات تفعيل وتعطيل منفصلة في Backend. لكن تدقيق القراءة لم يثبت من جميع مسارات منح النقاط والإحالات أن المفتاح العام يمنع كل منح جديد، لذلك يلزم اختبار تكاملي قبل إعلان هذا السلوك معتمدًا.

## 8. الصلاحية والمصادرة

لم يثبت وجود أعمدة أو إعدادات أو مهمة مجدولة أو نوع حركة مصادرة لهذه الميزة في العقد الحالي. لم ينفذ أي تصفير أو مصادرة أو تعديل للحركات التاريخية.

## 9. الخلاصة

يمكن تنفيذ مركز التنقل والقراءة الأولية فقط على العقود الحالية. أما الإدارة التشغيلية المطلوبة لربط الأنواع الرسمية والمستورد وتعديل كل إعداد نقطة فتحتاج عقد ربط Backend مستقلًا، خصوصًا لأن معرفات المصدر الرسمية غير موجودة في جداول إعداد النقاط الحالية.
