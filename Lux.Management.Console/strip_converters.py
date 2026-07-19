import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Remove the converters xmlns
    content = re.sub(r'xmlns:converters="clr-namespace:MikroTikVoucherPrinter\.UI\.Converters"', '', content)
    
    # Remove converter resources
    content = re.sub(r'<converters:[a-zA-Z0-9_]+\s+x:Key="[^"]*"\s*/>', '', content)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Cleaned converters from", file)
