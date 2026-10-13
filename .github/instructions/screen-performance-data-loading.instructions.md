---
description: "Use when creating a new LUMAR ERP Flutter screen or its supporting list/search/filter API. Enforces server-side paging, search, filtering, lazy loading, summary APIs, indexes, and performance measurement."
applyTo: ["frontend/tailoring_system/lib/**/*.dart", "Backend/LUMAR_ERP_API_V2/**/*.cs"]
---

# LUMAR ERP Screen Performance And Data Loading Policy

## Mandatory status

This file is the current permanent and mandatory source for the LUMAR instant data experience and performance policy.

## Source of truth

This file is the authoritative project source for performance, data loading, caching, search, paging, and rendering behavior within the applicable LUMAR scope. It is not a recommendation-only reference and it does not duplicate the policy in other files.

LUMAR INSTANT DATA EXPERIENCE & PERFORMANCE POLICY
السياسة الدائمة لتجربة البيانات الفورية والأداء في LUMAR
==================================================

[STATUS]

هذه سياسة معمارية دائمة وملزمة لجميع أعمال التطوير الجديدة والتحسينات المتعلقة بالشاشات والبيانات والأداء في LUMAR.

تطبق حيث تكون مناسبة على:

- LUMAR Mobile.
- LUMAR ERP Windows.
- Shared Backend.
- واجهات API.
- استعلامات البيانات.
- الشاشات الثقيلة.
- القوائم والجداول.
- التقارير والكشوف.
- البحث والفلاتر.
- التنقل والتبويبات.

ولا تعتبر هذه السياسة إذناً لأي منفذ بتجاوز حدود ملكيته أو صلاحياته.

==================================================
[PRIMARY LAW]
==================================================

Navigation must feel immediate.

Data volume must not dictate navigation latency.

القانون:

USER ACTION
→ IMMEDIATE UI RESPONSE
→ RESTORE AVAILABLE STATE
→ FETCH ONLY WHAT IS NEEDED
→ REFRESH SAFELY
→ RENDER INCREMENTALLY

يمنع جعل فتح الشاشة أو التبويب معتمداً بلا ضرورة على اكتمال استعلام ضخم أو تحميل جميع البيانات.

==================================================
[1 — INSTANT SHELL]
==================================================

يجب فصل:

NAVIGATION
عن:
DATA ACQUISITION

عند فتح الشاشة يجب إظهار الهيكل القابل للعرض فوراً قدر الإمكان:

- App Bar.
- Navigation.
- Tabs.
- Filters.
- Search controls.
- Available session state.
- Skeleton/Placeholder عند الحاجة.

ثم تحمل البيانات بصورة مستقلة.

يمنع بلا مبرر مثبت:

CLICK
→ WAIT FOR NETWORK
→ WAIT FOR FULL DATASET
→ BUILD EVERYTHING
→ SHOW SCREEN

==================================================
[2 — SINGLE SOURCE OF TRUTH]
==================================================

الحقيقة التشغيلية والمالية تبقى في مصدرها الرسمي المركزي.

Cache أو Session State أو Local Snapshot:

ليست Source of Truth.

وظيفتها:

FAST READ EXPERIENCE
+
RESILIENCE
+
STATE RESTORATION

ولا يجوز استخدامها لتغيير Business Rules أو إثبات قيمة مالية نهائية.

أي عملية:

Payment
Approval
Settlement
Financial Write
Inventory Write
Production State Change

يجب أن تتحقق من الحالة الرسمية الحديثة وفق عقد الخادم قبل تنفيذ الكتابة.

==================================================
[3 — STALE-WHILE-REVALIDATE]
==================================================

للبيانات المقروءة المناسبة:

إذا كانت نسخة سابقة صالحة للعرض موجودة:

SHOW EXISTING DATA
→ MARK/KNOW FRESHNESS
→ REFRESH IN BACKGROUND
→ APPLY NEWEST VALID RESPONSE

لا تحول الشاشة المكتملة إلى Loading فارغ لمجرد Refresh عابر.

ويجب تحديد Freshness Policy لكل نوع بيانات حساس.

==================================================
[4 — QUERY OWNERSHIP]
==================================================

للبيانات الكبيرة:

Database/Backend مسؤولان، بحسب Source of Truth والعقد، عن:

- Filtering.
- Sorting.
- Searching.
- Pagination.
- Authorization.
- Aggregation.
- Official totals.
- Period filtering.

Mobile/Windows مسؤولان عن:

- presentation.
- interaction.
- requesting.
- state.
- incremental rendering.

ممنوع تحميل آلاف السجلات إلى العميل فقط لتنفيذ بحث أو Sort أو Filter كان الأصح تنفيذه Server-Side.

==================================================
[5 — PAGINATION STRATEGY]
==================================================

لا يوجد نوع Pagination إلزامي عالمياً.

اختر الاستراتيجية بناءً على طبيعة البيانات.

للبيانات المتغيرة والكبيرة:

PREFER KEYSET/CURSOR PAGINATION
WHEN PROVEN APPROPRIATE

خصوصاً للسجلات الزمنية والحركات المتنامية.

للبيانات الثابتة أو التي تحتاج انتقالاً صريحاً بين أرقام صفحات:

OFFSET/PAGE PAGINATION MAY BE USED

يجب أن يكون Order ثابتاً وحتمياً.

يجب عدم اختيار Cursor أو Offset بالعادات البرمجية، بل بناءً على خصائص الاستعلام.

==================================================
[6 — LAZY AND VIRTUALIZED RENDERING]
==================================================

الشاشات الكبيرة يجب أن تبني فقط ما يحتاج المستخدم لرؤيته.

يفضل عند الملاءمة:

ListView.builder
SliverList
Lazy builders
Virtualized/Paginated data sources

ويمنع:

- بناء آلاف Rows دفعة واحدة.
- تحويل كامل Dataset إلى Widgets بلا حاجة.
- إعادة بناء الشاشة كاملة لتغير صغير.
- العمليات الثقيلة المتكررة داخل build().

==================================================
[7 — TAB STATE PRESERVATION]
==================================================

لكل تبويب ثقيل يجب تقييم حفظ:

DATA SNAPSHOT
SCROLL POSITION
FILTER STATE
SEARCH STATE
SORT STATE
NEXT CURSOR/PAGE
LAST REFRESH
LOAD STATE

عند العودة لتبويب سبق فتحه:

RESTORE FIRST
→ REFRESH IF REQUIRED

بدلاً من:

DESTROY
→ REFETCH
→ REBUILD EVERYTHING

لكن يجب تطبيق Memory Budget وعدم إبقاء عدد غير محدود من الشاشات والبيانات في الذاكرة.

==================================================
[8 — ADAPTIVE PREFETCH]
==================================================

لا ينتظر التطبيق بالضرورة وصول المستخدم إلى آخر سجل.

يجوز تحميل الصفحة اللاحقة مسبقاً عندما يثبت أن ذلك يحسن التجربة.

لكن Prefetch يجب أن يكون:

BOUNDED
+
CANCELLABLE
+
NETWORK-AWARE
+
MEMORY-AWARE

وممنوع:

PRELOAD EVERYTHING

==================================================
[9 — CONCURRENCY SAFETY]
==================================================

أي شاشة تدعم:

Search
Filter
Paging
Refresh

يجب أن تمنع:

DUPLICATE REQUESTS
STALE RESPONSE OVERWRITE
OUT-OF-ORDER RESPONSE CORRUPTION

السياسة:

NEWER INTENT WINS

استخدم حسب البنية:

Debounce
Cancellation
Request generation/token
Deduplication
Idempotency where relevant

الاستجابة القديمة لا يجوز أن تستبدل نتيجة طلب أحدث.

==================================================
[10 — CACHE SAFETY]
==================================================

يفرق النظام بين:

SESSION MEMORY CACHE
PERSISTENT LOCAL CACHE
SERVER CACHE
SOURCE OF TRUTH

لا تستخدم Cache مستمرة للبيانات الحساسة لمجرد تحسين السرعة دون تحديد:

TTL/FRESHNESS
INVALIDATION
SECURITY
USER ISOLATION
LOGOUT CLEARING
DATA CLASSIFICATION

البيانات المالية المخزنة محلياً لا تصبح حقيقة مالية لمجرد أنها آخر نسخة معروضة.

==================================================
[11 — WRITE PATH SAFETY]
==================================================

READ OPTIMIZATION
لا تبرر:
UNSAFE WRITE

السرعة البصرية لا تتقدم على:

Data Integrity
Authorization
Idempotency
Concurrency Control
Official State Verification

قبل أي كتابة حساسة:

VERIFY AUTHORITATIVE STATE
→ WRITE
→ CONFIRM SERVER RESULT
→ UPDATE LOCAL VIEW

==================================================
[12 — PERFORMANCE BUDGET]
==================================================

كل شاشة ثقيلة يجب أن يكون لها Performance Budget.

لكن تحدد الأرقام بعد Baseline واقعي.

على الأقل قس:

NAVIGATION_RESPONSE
FIRST_MEANINGFUL_RENDER
FIRST_DATA
API_LATENCY
DATABASE_LATENCY
PAYLOAD_SIZE
FRAME_TIME
JANK
MEMORY
REQUEST_COUNT
REBUILD_COUNT

ممنوع إعلان:

FAST
OPTIMIZED
INSTANT

بدون قياس.

==================================================
[13 — PROFILE MODE]
==================================================

الحكم النهائي على أداء Flutter لا يعتمد على Debug Mode.

يجب للأعمال المهمة استخدام:

PROFILE MODE
+
PHYSICAL DEVICE WHEN APPLICABLE
+
FLUTTER DEVTOOLS PERFORMANCE

ويقارن:

BEFORE
→ AFTER

==================================================
[14 — NO PERFORMANCE REGRESSION]
==================================================

أي تحسين جديد لا يعتبر مكتملاً إذا:

- كسر Business Rules.
- كسر Source of Truth.
- سرّب بيانات.
- زاد استهلاك الذاكرة بلا حدود.
- سبب Duplicate Requests.
- أفسد Pagination.
- أظهر بيانات قديمة باعتبارها حديثة.
- أضعف سلامة العمليات المالية.
- أبطأ مساراً آخر جوهرياً.

==================================================
[15 — OWNERSHIP]
==================================================

هذه السياسة لا تغير حدود ملكية المشاريع.

Mobile executor يعمل فقط ضمن سلطته.

إذا كان تحسين الأداء يتطلب Backend Contract:

BACKEND CHANGE REQUEST

إذا تطلب Database Change:

PROVE NECESSITY
→ OWNER AUTHORIZATION

ولا تعد سرعة الشاشة تفويضاً تلقائياً لتغيير Backend أو Database.

==================================================
[MANDATORY ENGINEERING SEQUENCE]
==================================================

MEASURE
→ LOCATE BOTTLENECK
→ PROVE ROOT CAUSE
→ CHOOSE STRATEGY
→ IMPLEMENT MINIMUM SAFE CHANGE
→ PROFILE
→ LOAD TEST WHERE RELEVANT
→ COMPARE BASELINE
→ VERIFY CORRECTNESS
→ CLOSE

==================================================
[PERMANENT RULE]
==================================================

NO MEASUREMENT
→ NO PERFORMANCE CLAIM

NO SOURCE OF TRUTH
→ NO CACHED FACT

NO BOUNDED DATA
→ NO EAGER FULL LOAD

NO STABLE SORT
→ NO PAGINATION CONTRACT

NO AUTHORIZATION
→ NO DATA DELIVERY

NO SAFE INVALIDATION
→ NO PERSISTENT CACHE

NO PROFILE VERIFICATION
→ NO PERFORMANCE CLOSURE

==================================================
[END OF VERBATIM POLICY]
==================================================

==================================================
[IMPORTANT PERFORMANCE MIGRATION RULE]

بعد إضافة السياسة الجديدة:

راجع:
.github/instructions/screen-performance-data-loading.instructions.md

لا تحذف منه أي قاعدة قديمة فريدة وغير ممثلة في السياسة الجديدة.

صنف كل قاعدة فيه:

DUPLICATE
UNIQUE
CONFLICT
IMPLEMENTATION-INSTRUCTION

- DUPLICATE:
  تزال النسخة المكررة ويشار للمصدر الجديد.

- UNIQUE:
  تبقى في الملف المتخصص المناسب أو تنقل للمصدر المختص دون تغيير معناها.

- CONFLICT:
  لا تحسم بالتخمين. ارفعها للمالك.

- IMPLEMENTATION-INSTRUCTION:
  يجوز أن تبقى في .github/instructions إذا كانت مطلوبة لتوجيه أداة التنفيذ، بشرط ألا تصبح نسخة ثانية من السياسة الكاملة.

==================================================
[FORBIDDEN]
==================================================

ممنوع:

- تغيير Business Rules.
- تغيير العقود التشغيلية.
- تعديل Backend بسبب هذه المهمة.
- تعديل Database.
- Migration.
- تعديل Flutter production behavior.
- إعادة تصميم الشاشات.
- حذف سياسة كاملة قبل إثبات نقل كل قاعدة فريدة منها.
- إعادة صياغة السياسة الجديدة التي قدمها المالك.
- اختراع حكم لحل تناقض غير محسوم.
- إنشاء عدة Sources of Truth جديدة.
- نسخ السياسة نفسها إلى Docs و.github كاملة.
- استخدام أسماء سعد داخل السياسات الدائمة إذا كان المقصود منفذاً تقنياً عاماً، إلا إذا كان النص الموجود قراراً معتمداً لا يجوز تغييره.
- Commit أو Push أو Pull Request أو Issue أو أي كتابة إلى GitHub.
- أي تعديل خارج النسخة المحلية المصرح بها.

==================================================
[VERIFICATION]
==================================================

بعد التنظيم نفذ تدقيقاً نهائياً:

1. POLICY_INDEX_VALID
2. EACH_POLICY_HAS_SINGLE_SCOPE
3. NO_KNOWN_DUPLICATED_RULES
4. NO_LOST_RULES
5. NO_UNAUTHORIZED_LOGIC_CHANGE
6. CROSS_REFERENCES_VALID
7. PERFORMANCE_POLICY_VERBATIM_MATCH
8. GITHUB_INSTRUCTIONS_NOT_COMPETING_WITH_DOCS
9. USER_GUIDE_RULE_NOT_DUPLICATED
10. ARABIC_RULE_SINGLE_SOURCE
11. KEYBOARD_RULE_SINGLE_SOURCE
12. COLOR_RULE_SINGLE_SOURCE
13. NAVIGATION_RULE_SINGLE_SOURCE
14. PERFORMANCE_RULE_SINGLE_SOURCE
15. DATABASE_RULE_SINGLE_SOURCE

استخدم مقارنة نصية للسياسة الجديدة للتأكد من أنها مطابقة حرفياً للنص الذي قدمه المالك.

==================================================
[ROOT CAUSE REQUIREMENT]
==================================================

في التقرير حدد لماذا وصل المشروع إلى التشتت الحالي:

- ما الذي كان مكرراً.
- أين كان التكرار.
- ما الملفات التي كانت تحتوي قواعد خارج اختصاصها.
- ما التعارضات التي وجدت.
- كيف أزيل التكرار دون تغيير القرار.
- أي تعارض بقي ويتطلب قرار مالك.

==================================================
[REQUIRED OUTPUT]
==================================================

أخرج:

POLICY_FILES_BEFORE=
POLICY_FILES_AFTER=

MASTER_POLICY_INDEX=
SPECIALIZED_POLICIES=

REMOVED_DUPLICATIONS=
MOVED_RULES=
CROSS_REFERENCES_ADDED=

CONFLICTS_RESOLVED_WITHOUT_LOGIC_CHANGE=
CONFLICTS_REQUIRING_OWNER_DECISION=

NEW_PERFORMANCE_POLICY_FILE=

PERFORMANCE_POLICY_VERBATIM_VERIFIED=YES/NO

LOST_RULES=0/...

LOGIC_CHANGED=YES/NO

FILES_MODIFIED=

PROFESSIONAL_RECOMMENDATION=

==================================================
[FINAL REPORT]
==================================================

اختم بحكم واحد فقط:

A- POLICY CONSOLIDATION VERIFIED

أو:

B- POLICY CONSOLIDATION PARTIALLY VERIFIED — OWNER DECISION REQUIRED

أو:

C- POLICY CONSOLIDATION FAILED — POLICY LOGIC AT RISK

إذا ظهر أي تعارض يحتاج تغيير قرار سياسي:
لا تختر القرار بنفسك.
اذكره بدقة وانتظر المالك.

بعد التقرير STOP.

==================================================
[END OF VERBATIM POLICY]
==================================================
