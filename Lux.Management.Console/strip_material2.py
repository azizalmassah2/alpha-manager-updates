import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Remove xmlns:materialDesign
    content = re.sub(r'xmlns:materialDesign="[^"]*"', '', content)
    
    # Remove properties starting with materialDesign:
    content = re.sub(r'materialDesign:[a-zA-Z0-9_\.]+=(?:"[^"]*"|\'[^\']*\')', '', content)
    
    # Replace elements starting with <materialDesign:xyz> with <Border> or just remove them.
    # Actually, if it's an element, I should probably replace it with a native element to keep the layout from breaking too much.
    # materialDesign:PackIcon -> TextBlock
    content = re.sub(r'<materialDesign:PackIcon\s+Kind="[^"]*"([^>]*)/>', r'<TextBlock Text="Icon" \1/>', content)
    # materialDesign:Card -> Border
    content = re.sub(r'<materialDesign:Card', r'<Border', content)
    content = re.sub(r'</materialDesign:Card>', r'</Border>', content)
    
    # Remove BasedOn for materialDesign resources
    content = re.sub(r'BasedOn="\{StaticResource MaterialDesign[^}]*\}"', '', content)
    
    # Remove Style="{StaticResource MaterialDesign...}"
    content = re.sub(r'Style="\{StaticResource MaterialDesign[^}]*\}"', '', content)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Cleaned advanced MaterialDesign from", file)
