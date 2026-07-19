import os
import re

files = [
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\ViewModels\GenerateVoucherViewModel.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\ViewModels\VoucherManagementViewModel.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\ViewModels\PrintCenterViewModel.cs"
]

def fix_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Alias IDialogService
    if 'using IDialogService = Lux.Management.Console.Core.IDialogService;' not in content:
        content = content.replace('using Lux.Management.Console.Core;', 'using Lux.Management.Console.Core;\nusing IDialogService = Lux.Management.Console.Core.IDialogService;')

    # Fix InitializeAsync override
    content = re.sub(r'public\s+override\s+(async\s+)?Task\s+InitializeAsync', r'public \1Task InitializeAsync', content)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Fixed {file_path}")

for f in files:
    fix_file(f)
