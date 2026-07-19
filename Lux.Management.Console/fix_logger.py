import re

files = [
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\ViewModels\GenerateVoucherViewModel.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\ViewModels\VoucherManagementViewModel.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\ViewModels\PrintCenterViewModel.cs"
]

def fix_logger(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Replace Logger.LogError and Logger.LogInformation
    content = re.sub(r'Logger\.LogError\(', 'System.Diagnostics.Debug.WriteLine(', content)
    content = re.sub(r'Logger\.LogInformation\(', 'System.Diagnostics.Debug.WriteLine(', content)
    content = re.sub(r'Logger\.LogWarning\(', 'System.Diagnostics.Debug.WriteLine(', content)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

for f in files:
    fix_logger(f)
print("Replaced Logger")
