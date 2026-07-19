import os
import re

search_dir = r"D:\LUXCARD\desktop\Lux.Management.Console"
extensions = ['.xaml', '.cs']

patterns = [
    (re.compile(r'(Background|Foreground|BorderBrush|Fill|Stroke|Color)="[#a-zA-Z]'), "Hardcoded Brush Property"),
    (re.compile(r'#[0-9a-fA-F]{6,8}'), "Hex Color"),
    (re.compile(r'Brushes\.(White|Black|Gray|Red|Blue|Green|Orange|Yellow|Transparent|Light|Dark)[a-zA-Z]*'), "Brushes.X usage"),
    (re.compile(r'SolidColorBrush\s+x:Key'), "SolidColorBrush definition"),
]

exclude_files = ['DarkTheme.xaml', 'LightTheme.xaml']

matches = []

for root, _, files in os.walk(search_dir):
    if 'obj' in root or 'bin' in root or '.git' in root:
        continue
    for file in files:
        if any(file.endswith(ext) for ext in extensions) and file not in exclude_files:
            filepath = os.path.join(root, file)
            with open(filepath, 'r', encoding='utf-8') as f:
                try:
                    for line_idx, line in enumerate(f):
                        for pattern, desc in patterns:
                            # Skip looking for "Color=" if it's actually binding or dynamic resource
                            if desc == "Hardcoded Brush Property" and '="{DynamicResource ' in line: continue
                            if desc == "Hardcoded Brush Property" and '="{StaticResource ' in line: continue
                            if desc == "Hardcoded Brush Property" and '="{Binding ' in line: continue
                            
                            found = pattern.findall(line)
                            if found:
                                # We might have multiple per line
                                matches.append(f"{os.path.relpath(filepath, search_dir)} (Line {line_idx+1}): {line.strip()} [{desc}]")
                except Exception as e:
                    pass

with open(r"C:\Users\MrAziz\.gemini\antigravity\brain\60da867a-9407-426e-9fce-4fef08391c75\scratch\ThemeAuditResults.txt", "w", encoding='utf-8') as out:
    for m in matches:
        out.write(m + "\n")

print(f"Found {len(matches)} potential hardcoded color usages.")
