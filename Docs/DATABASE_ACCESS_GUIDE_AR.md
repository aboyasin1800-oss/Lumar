# دليل الوصول إلى قاعدة البيانات - LUMAR ERP

## معلومات الوصول غير السرية

- اسم الخادم: YASIN-YASIN\SQLEXPRESS
- اسم قاعدة البيانات: LUMAR_ERP
- طريقة الدخول: Windows Authentication / مصادقة ويندوز
- المسار الكامل لأداة الاتصال: C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE

## أمر التحقق المباشر

```powershell
& "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE" `
  -S "YASIN-YASIN\SQLEXPRESS" `
  -d "LUMAR_ERP" `
  -E `
  -C `
  -b `
  -r 1 `
  -l 15 `
  -Q "SET NOCOUNT ON; SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName, SYSTEM_USER AS ConnectedUser, GETDATE() AS ServerTime;"
```

## شرح الخيارات بالعربية

- -S = اسم الخادم والمثيل
- -d = اسم قاعدة البيانات
- -E = استخدام مصادقة ويندوز
- -C = الوثوق بشهادة الخادم المحلية
- -b = إرجاع رمز فشل عند حدوث خطأ
- -r 1 = إظهار رسائل الخطأ
- -l 15 = مهلة الاتصال 15 ثانية

## ملاحظات السلامة

- هذا الملف لا يحتوي على كلمة مرور أو اسم مستخدم سري.
- لا يحتوي على سلسلة اتصال سرية.
- لا يحتوي على أي أمر UPDATE أو INSERT أو DELETE أو ALTER.
- لا يحتوي على أي Migration أو ترحيل قاعدة بيانات.
- الغرض فقط هو التحقق من الوصول والقراءة فقط.

## هدف التوثيق

يُستخدم هذا الملف لتوثيق مكان الوصول الحالي إلى قاعدة البيانات في البيئة المحلية، بحيث لا تضيع معلومات المسار أو اسم المثيل عند الحاجة إلى التحقق المفاجئ.
