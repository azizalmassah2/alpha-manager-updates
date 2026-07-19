import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # If it's one of the copied files, update the x:Class and namespaces
    if "MikroTikVoucherPrinter.UI" in content:
        if "VoucherManagementPage" in file or "GenerateVoucherPage" in file:
            content = re.sub(r'x:Class="MikroTikVoucherPrinter\.UI\.Views\.Pages\.[^"]+"', f'x:Class="Lux.Management.Console.Modules.Vouchers.Views.{os.path.basename(file)[:-5]}"', content)
        elif "PrintCenterPage" in file:
            content = re.sub(r'x:Class="MikroTikVoucherPrinter\.UI\.Views\.Pages\.[^"]+"', f'x:Class="Lux.Management.Console.Modules.Printing.Views.{os.path.basename(file)[:-5]}"', content)
        
        # Remove local namespace which points to old ViewModels
        content = re.sub(r'xmlns:local="clr-namespace:MikroTikVoucherPrinter\.UI[^"]*"', '', content)
        content = re.sub(r'xmlns:vm="clr-namespace:MikroTikVoucherPrinter\.UI\.ViewModels[^"]*"', '', content)

        # Remove d:DataContext="{d:DesignInstance ... }"
        content = re.sub(r'd:DataContext="[^"]+"', '', content)

        with open(file, 'w', encoding='utf-8') as f:
            f.write(content)
        print("Updated", file)
