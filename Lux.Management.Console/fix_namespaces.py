import os
import glob
import re

files_to_fix = [
    (r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Profiles\Views\ProfileManagementPage.xaml", "Profiles"),
    (r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Agents\Views\AgentManagementPage.xaml", "Agents"),
    (r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Settings\Views\SettingsPage.xaml", "Settings"),
    (r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Settings\Views\SyncPage.xaml", "Settings"),
    (r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\Views\TemplateManagementPage.xaml", "Printing")
]

for xaml_path, module_name in files_to_fix:
    cs_path = xaml_path + ".cs"
    new_namespace = f"Lux.Management.Console.Modules.{module_name}.Views"
    
    # Process XAML
    with open(xaml_path, 'r', encoding='utf-8') as f:
        xaml_content = f.read()
    
    # Fix x:Class
    class_name = os.path.basename(xaml_path).replace(".xaml", "")
    xaml_content = re.sub(r'x:Class="[^"]+"', f'x:Class="{new_namespace}.{class_name}"', xaml_content)
    
    # Change Page to UserControl in XAML if it's <Page ...>
    xaml_content = re.sub(r'^<Page ', '<UserControl ', xaml_content)
    xaml_content = re.sub(r'</Page>$', '</UserControl>', xaml_content)
    
    with open(xaml_path, 'w', encoding='utf-8') as f:
        f.write(xaml_content)

    # Process Code-Behind
    with open(cs_path, 'r', encoding='utf-8') as f:
        cs_content = f.read()
    
    # Fix namespace
    cs_content = re.sub(r'namespace [^;\n{]+[;\n{]', f'namespace {new_namespace};\n', cs_content)
    
    # Fix inheritance Page -> UserControl
    cs_content = re.sub(r'public partial class ([a-zA-Z0-9_]+) : Page', r'public partial class \1 : UserControl', cs_content)
    
    # Add System.Windows.Controls if not present
    if "using System.Windows.Controls;" not in cs_content:
        cs_content = "using System.Windows.Controls;\n" + cs_content
        
    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(cs_content)
    
    print(f"Fixed namespaces for {class_name}")

