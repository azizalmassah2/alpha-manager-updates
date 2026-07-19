import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Replace <materialDesign:PackIcon with <TextBlock Text="Icon"
    # But some might already be closed like />
    content = re.sub(r'<materialDesign:PackIcon', r'<TextBlock Text="Icon"', content)
    content = re.sub(r'</materialDesign:PackIcon>', r'</TextBlock>', content)
    
    # Catch any other materialDesign elements
    content = re.sub(r'<materialDesign:[a-zA-Z0-9_]+', r'<Border', content)
    content = re.sub(r'</materialDesign:[a-zA-Z0-9_]+>', r'</Border>', content)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Cleaned ALL materialDesign tags from", file)
