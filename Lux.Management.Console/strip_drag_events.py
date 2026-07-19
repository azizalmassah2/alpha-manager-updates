import os
import re

file = r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\Views\TemplateManagementPage.xaml"

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

content = re.sub(r'\s+DragDelta="[^"]+"', '', content)
content = re.sub(r'\s+DragCompleted="[^"]+"', '', content)

with open(file, 'w', encoding='utf-8') as f:
    f.write(content)

print(f"Stripped Drag events from {file}")
