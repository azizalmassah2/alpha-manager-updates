# قواعد مشروع MikroTikVoucherPrinter

## ✅ قاعدة إلزامية: البناء بعد كل تعديل

**بعد أي تعديل على الكود يجب بناء المشروع الرئيسي (Alpha Manager) فوراً والتحقق من نجاح البناء قبل الإبلاغ عن اكتمال المهمة.**

### أمر البناء الافتراضي (المشروع الرئيسي):
```powershell
dotnet build "Lux.Management.Console\Lux.Management.Console.csproj" --no-restore 2>&1 | Select-Object -Last 10
```

### إذا كان التطبيق شغالاً (locked DLLs)، ابنِ المكتبة المعدلة مباشرة:
```powershell
# مثال: إذا عدّلت Lux.MikroTik
dotnet build "Lux.MikroTik\Lux.MikroTik.csproj" --no-restore 2>&1 | Select-Object -Last 10

# مثال: إذا عدّلت Infrastructure
dotnet build "MikroTikVoucherPrinter.Infrastructure\MikroTikVoucherPrinter.Infrastructure.csproj" --no-restore 2>&1 | Select-Object -Last 10
```

### لا تُبلّغ عن اكتمال المهمة إلا بعد:
- ✅ `Build succeeded`
- ✅ `0 Error(s)`
