import os
import re

files_to_clean = [
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Profiles\Views\ProfileManagementPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Agents\Views\AgentManagementPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Settings\Views\SettingsPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Settings\Views\SyncPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\Views\TemplateManagementPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\Views\VoucherManagementPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\Views\GenerateVoucherPage.xaml.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\Views\PrintCenterPage.xaml.cs"
]

for file in files_to_clean:
    if not os.path.exists(file):
        continue
    
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract namespace and class name
    ns_match = re.search(r'namespace\s+([^;\n]+)[;\n]', content)
    class_match = re.search(r'public\s+partial\s+class\s+([a-zA-Z0-9_]+)', content)
    
    if ns_match and class_match:
        ns = ns_match.group(1).strip()
        cls = class_match.group(1).strip()
        
        bare_code = f"""using System.Windows.Controls;

namespace {ns};

public partial class {cls} : UserControl
{{
    public {cls}()
    {{
        InitializeComponent();
    }}
}}
"""
        with open(file, 'w', encoding='utf-8') as f:
            f.write(bare_code)
        print(f"Stripped code-behind for {cls}")

# Now remove event handlers from XAML
import glob

xaml_files = glob.glob(r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\**\*.xaml", recursive=True)

events_to_remove = ["Click", "SelectionChanged", "Loaded", "Unloaded", "TextChanged", "KeyDown", "KeyUp", "MouseDoubleClick", "PreviewTextInput", "LostFocus", "GotFocus"]

for file in xaml_files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()
        
    for event in events_to_remove:
        # Regex to remove event="handlerName"
        content = re.sub(rf'\s+{event}="[^"]+"', '', content)
        
    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)
        
    print(f"Stripped event handlers from {file}")

