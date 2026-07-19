import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Remove Foreground from Border
    # This is tricky because it might span multiple lines. 
    # Let's just find Foreground="..." and if it's inside a Border, remove it.
    # Actually, we can just replace `<Border ... Foreground="..."` using a function.
    
    def remove_foreground_from_border(match):
        inner = match.group(1)
        inner = re.sub(r'Foreground="[^"]*"', '', inner)
        inner = re.sub(r'UniformCornerRadius', 'CornerRadius', inner)
        return '<Border' + inner + '>'

    content = re.sub(r'<Border([^>]+)>', remove_foreground_from_border, content)

    # Also fix Kind="xyz" inside TextBlock (since we replaced PackIcon with TextBlock)
    content = re.sub(r'Kind="[^"]*"', '', content)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Cleaned Border attributes from", file)
