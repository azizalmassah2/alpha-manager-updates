import re

path = r'd:\LUXCARD\desktop\MikroTikVoucherPrinter.UI\Views\Pages\GenerateVoucherPage.xaml'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

original = content

# Remove Height="42" attributes (global style handles sizing via MinHeight now)
content = re.sub(r'\s+Height="42"', '', content)

if content != original:
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print('Fixed: removed all Height="42" attributes from GenerateVoucherPage.xaml')
else:
    print('No changes made')
