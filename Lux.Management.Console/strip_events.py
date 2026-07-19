import os
import re

file = r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\Views\VoucherManagementPage.xaml"

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

content = re.sub(r'SelectionChanged="MainGrid_SelectionChanged"', '', content)
content = re.sub(r'Click="VoucherOptionsButton_Click"', '', content)

with open(file, 'w', encoding='utf-8') as f:
    f.write(content)
print("Removed event handlers from", file)
