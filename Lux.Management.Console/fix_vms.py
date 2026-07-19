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

    # Add correct namespaces
    usings = """using Lux.Management.Console.Core;
using Lux.Management.Console.Modules._Migration;
"""
    if 'Lux.Management.Console.Core;' not in content:
        content = usings + content

    # Fix InitializeAsync override
    content = content.replace("public override async Task InitializeAsync(object parameter = null)", "public async Task InitializeAsync(object parameter = null)")
    content = content.replace("public override async Task InitializeAsync(object parameter)", "public async Task InitializeAsync(object parameter = null)")

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Fixed {file_path}")

for f in files:
    fix_file(f)
