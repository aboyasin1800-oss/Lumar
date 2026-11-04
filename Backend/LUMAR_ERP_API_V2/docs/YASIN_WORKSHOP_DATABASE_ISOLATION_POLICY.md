# LUMAR — YASIN WORKSHOP DATABASE ABSOLUTE ISOLATION POLICY

## MISSION

أنشئ داخل مشروع LUMAR ملف سياسة دائم وواضح يمنع مشروع LUMAR وأي منفذ تقني يعمل عليه من الاقتراب كتابيًا من قواعد بيانات مشروع «معمل ياسين».

هذه سياسة حماية وعزل دائمة.

قواعد البيانات المحمية هي:

YASIN_WORKSHOP_ES_VALIDATION

YASIN_WORKSHOP_GOLDEN_1

وهاتان القاعدتان:

- ليستا جزءًا من مشروع LUMAR.
- ليستا قواعد اختبار لـLUMAR.
- ليستا قواعد استعادة لـLUMAR.
- ليستا قواعد تطوير لـLUMAR.
- ليستا قواعد مرجعية لـLUMAR.

وهما ملك حصري لمشروع مستقل اسمه:

YASIN WORKSHOP

مشروع معمل ياسين.

============================================================
[POLICY FILE]
============================================================

هذا المسار هو الموقع الرسمي الحالي للسياسات داخل مشروع LUMAR في هذا المشروع:

- docs/YASIN_WORKSHOP_DATABASE_ISOLATION_POLICY.md

تم وضع السياسة داخل الموقع الرسمي الحالي لمشروع LUMAR بعد تدقيق بنية ملفات السياسات الموجودة. لا تم إنشاء مسار سياسات جديد.

لا يغيّر هذا الملف أي قاعدة أعمال LUMAR.

============================================================
[ABSOLUTE WRITE PROHIBITION]
============================================================

يُمنع منعًا باتًا على أي مهمة أو Script أو Migration أو Test أو Backend أو أداة تخص مشروع LUMAR تنفيذ أي كتابة أو تعديل أو إدارة على:

YASIN_WORKSHOP_ES_VALIDATION

أو:

YASIN_WORKSHOP_GOLDEN_1

يشمل الحظر، دون حصر:

- CREATE
- ALTER
- DROP
- TRUNCATE
- INSERT
- UPDATE
- DELETE
- MERGE
- Migration
- Seed
- Data Fix
- Schema Fix
- Index Creation
- View Creation/Modification
- Stored Procedure Creation/Modification
- Function Creation/Modification
- RESTORE
- BACKUP overwrite
- ATTACH
- DETACH
- Rename
- Shrink
- Cleanup
- Test Write
- Data Repair
- Baseline Promotion
- Permission Modification
- أي DDL أو DML أو تغيير إداري آخر

============================================================
[NO AUTOMATIC DISCOVERY / NO FALLBACK]
============================================================

ممنوع اعتبار القاعدتين هدفًا مناسبًا لمجرد وجودهما على نفس SQL Server Instance.

ممنوع:

- اختيار أي منهما كـ fallback.
- استخدامهما عند فشل قاعدة LUMAR.
- استخدامهما كقواعد اختبار مؤقتة.
- استخدامهما لاختبار Migration.
- استخدامهما لاستعادة Backup.
- تنظيفهما.
- مقارنتهما تلقائيًا مع قواعد LUMAR.
- إدخالهما في Scripts جماعية تستهدف قواعد المثيل.
- إدخالهما في أي عملية wildcard أو loop على sys.databases بغرض الكتابة.
- استخدام أي Connection String خاص بهما في LUMAR.

وجود القاعدتين على نفس المثيل لا يمنح مشروع LUMAR أي حق عليهما.

============================================================
[READ-ONLY EXCEPTION]
============================================================

حتى القراءة من القاعدتين ليست جزءًا طبيعيًا من مشروع LUMAR.

يسمح بالقراءة فقط إذا أصدر مالك المشروع ياسين طلبًا صريحًا ومحددًا يطلب فيه قراءة إحدى قاعدتي معمل ياسين لغرض معلوم.

عندها فقط:

- SELECT بالقدر اللازم.
- لا كتابة.
- لا تعديل.
- لا Migration.
- لا Seed.
- لا Fix.
- لا إنشاء Object.
- لا تغيير Permission.
- لا Backup/Restore.
- لا Baseline Promotion.

إذن القراءة لا يتحول إلى إذن كتابة.

إذا طلب المالك "افحص" أو "قارن قراءة فقط"، يبقى النطاق READ-ONLY.

أي كتابة تحتاج تفويضًا جديدًا وصريحًا ومستقلًا يذكر قاعدة معمل ياسين المستهدفة ونوع الكتابة المطلوبة.

============================================================
[FAIL-CLOSED SAFETY GATE]
============================================================

أي أداة أو Script أو Test أو Migration تابع لـLUMAR يكتشف أن Database Name المستهدفة تساوي:

YASIN_WORKSHOP_ES_VALIDATION

أو:

YASIN_WORKSHOP_GOLDEN_1

يجب أن يعتبر ذلك هدفًا خارج مشروع LUMAR ويتوقف عن الكتابة.

السلوك المطلوب:

FAIL CLOSED

وليس:

TRY AND CONTINUE.

إذا كان Script في LUMAR يقوم بالدوران على قواعد SQL Server، فيجب أن تكون قاعدتا معمل ياسين مستبعدتين صراحة من أي مسار كتابة.

============================================================
[PROJECT BOUNDARY]
============================================================

قواعد LUMAR الرسمية وسياساتها تبقى مستقلة.

قواعد معمل ياسين:

- YASIN_WORKSHOP_ES_VALIDATION
- YASIN_WORKSHOP_GOLDEN_1

لا تدخل في:

- Source of Truth لـLUMAR.
- Golden Baseline لـLUMAR.
- Validation لـLUMAR.
- Testing لـLUMAR.
- Recovery لـLUMAR.
- Production لـLUMAR.
- Migration Target لـLUMAR.

ولا يجوز استنتاج أي علاقة بين النظامين من مجرد وجود القواعد على:

YASIN-YASIN\SQLEXPRESS

============================================================
[PERMANENCE]
============================================================

هذه السياسة:

- PERMANENT
- MANDATORY
- FAIL-CLOSED

وتبقى سارية على جميع مهام LUMAR الحالية والمستقبلية.

لا يلزم إعادة ذكرها في كل أمر تقني حتى تكون نافذة.

ممنوع إلغاؤها أو تخفيفها أو تجاوزها إلا بقرار صريح من المالك ياسين.

============================================================
[THIS TASK IS DOCUMENTATION ONLY]
============================================================

هذه المهمة لإنشاء وتثبيت السياسة فقط.

ممنوع في هذه المهمة:

- الاتصال كتابيًا بأي قاعدة.
- تعديل أي قاعدة.
- تشغيل Migration.
- تشغيل Test يكتب بيانات.
- تعديل Schema.
- اختبار الحظر عن طريق محاولة كتابة فعلية.
- لمس YASIN_WORKSHOP_ES_VALIDATION.
- لمس YASIN_WORKSHOP_GOLDEN_1.
- تعديل قواعد LUMAR.
- تعديل قواعد أعمال LUMAR.

تحقق من السياسة من الملفات فقط.

============================================================
[VERIFICATION]
============================================================

بعد إنشاء الملف:

1. أعد قراءته من القرص.
2. تحقق من وجود الاسمين حرفيًا:
   - YASIN_WORKSHOP_ES_VALIDATION
   - YASIN_WORKSHOP_GOLDEN_1
3. أثبت وجود:
   - ABSOLUTE WRITE PROHIBITION
4. أثبت وجود:
   - READ-ONLY EXCEPTION
5. أثبت وجود:
   - FAIL-CLOSED
6. أثبت أن القراءة تحتاج طلبًا صريحًا من المالك.
7. أثبت أن ملكية القاعدتين لمشروع YASIN WORKSHOP وليستا لـLUMAR.
8. تحقق أن السياسة أصبحت قابلة للاكتشاف من نظام/فهرس السياسات الحالي في LUMAR إذا كان هناك فهرس رسمي قائم.

============================================================
[FINAL REPORT]
============================================================

POLICY_PATH=docs/YASIN_WORKSHOP_DATABASE_ISOLATION_POLICY.md
POLICY_CREATED=YES
POLICY_READ_BACK=YES

YASIN_WORKSHOP_ES_VALIDATION_PROTECTED=YES
YASIN_WORKSHOP_GOLDEN_1_PROTECTED=YES

ABSOLUTE_WRITE_PROHIBITION_PRESENT=YES
READ_ONLY_REQUIRES_OWNER_REQUEST=YES
FAIL_CLOSED_PRESENT=YES
LUMAR_OWNERSHIP_CLAIM_FORBIDDEN=YES

LUMAR_POLICY_INDEX_UPDATED=NO_POLICY_INDEX_FOUND_IN_CURRENT_LUMAR_PROJECT
DATABASE_CONNECTIONS_OPENED=NO
DATABASES_MODIFIED=NO
MIGRATIONS_RUN=NO
TEST_WRITES_RUN=NO

FILES_CHANGED=1

FINAL_VERDICT=DOCUMENTATION_ONLY_POLICY_INSTALLED_IN_OFFICIAL_LUMAR_DOCS_LOCATION; YASIN WORKSHOP DATABASES REMAIN EXCLUDED FROM LUMAR WRITES AND FROM ALL LUMAR AUTOMATION; READ-ONLY ACCESS REQUIRES EXPLICIT OWNER REQUEST AND IS NOT A WRITE PERMIT.

============================================================

## Ownership and Safety Notice

- تم تأكيد أن قواعد معمل ياسين ليست جزءًا من مشروع LUMAR.
- لا يوجد فهرس سياسات مركزي رسمي قائم في هذا المشروع الحالي، لذلك تم وضع السياسة في الموقع الرسمي الحالي للوثائق (docs) دون إنشاء مسار سياسات جديد.
- هذه السياسة بلاغية وقانونية داخل المشروع: تم تصميمها كحاجز كتابي نهائي ضد الوصول الكتابي إلى قواعد معمل ياسين من أي مهمة أو Script أو Test أو Migration تابع لـLUMAR.
- لا يُسمح بأي كتابة أو تعديل أو إعادة بناء أو ترحيل أو استعادة أو تنظيف أو إصلاح بيانات لأنظمة معمل ياسين من مشروع LUMAR.
- أي قراءة تحتاج طلبًا صريحًا من المالك ياسين وتبقى ضمن READ-ONLY فقط.
