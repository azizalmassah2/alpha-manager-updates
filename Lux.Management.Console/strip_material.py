import os
import glob
import re

files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Remove all materialDesign properties
    content = re.sub(r'materialDesign:[A-Za-z0-9_.]+="[^"]+"', '', content)
    # Remove materialDesign elements entirely (or replace with native equivalents if possible)
    # Actually just removing the xmlns is enough to find where they are used as elements
    content = re.sub(r'xmlns:materialDesign="[^"]+"', '', content)
    
    # Also remove the attached properties that are failing like ShadowAssist.Elevation
    # Actually Elevation was mentioned in the error. Let's see if there are properties like "Elevation" standalone
    content = re.sub(r'Elevation="[^"]+"', '', content)
    
    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Cleaned MaterialDesign from", file)
