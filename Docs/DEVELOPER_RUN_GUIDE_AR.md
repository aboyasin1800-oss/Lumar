# دليل تشغيل المطور

نفّذ الأوامر التالية من جذر المشروع `D:\YASIN` في PowerShell.

## التشغيل

```powershell
.\scripts\Start-Backend.ps1
```

افتح نافذة PowerShell ثانية لتشغيل Flutter:

```powershell
.\scripts\Start-Flutter.ps1
```

يستخدم Flutter العنوان `http://127.0.0.1:5009` افتراضياً. لتحديد عنوان مختلف في النافذة الحالية:

```powershell
$env:LUMAR_API_URL = 'http://127.0.0.1:5093'
.\scripts\Start-Flutter.ps1
```

## الإيقاف

أوقف تشغيل Flutter من نافذته باستخدام `q`. لإيقاف التطبيق وBackend معاً:

```powershell
.\scripts\Stop-System.ps1
```

## إعادة التشغيل بعد التعديلات

أثناء عمل `Start-Flutter.ps1`، اضغط `r` لإعادة التحميل السريع أو `R` لإعادة التشغيل السريع. ولإيقاف التطبيق وفتحه في نافذة Flutter جديدة:

```powershell
.\scripts\Restart-Flutter.ps1
```

## تشغيل النسخة المبنية

بعد وجود Windows Debug Build، شغّلها دون Flutter:

```powershell
.\scripts\Run-Windows-Build.ps1
```