import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Remove Setters with materialDesign properties
    content = re.sub(r'<Setter\s+Property="materialDesign:[^"]+"\s+Value="[^"]+"\s*/>', '', content)
    
    # Also find any <Setter ... > ... </Setter>
    content = re.sub(r'<Setter\s+Property="materialDesign:[^>]+>.*?</Setter>', '', content, flags=re.DOTALL)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Cleaned Setters from", file)
