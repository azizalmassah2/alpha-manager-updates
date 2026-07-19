import os
import shutil

src_dir = r"d:\LUXCARD\desktop\MikroTikVoucherPrinter.UI\Views\Pages"
dest_base = r"d:\LUXCARD\desktop\Lux.Management.Console\Modules"

mapping = {
    "ProfileManagementPage.xaml": r"Profiles\Views",
    "ProfileManagementPage.xaml.cs": r"Profiles\Views",
    "AgentManagementPage.xaml": r"Agents\Views",
    "AgentManagementPage.xaml.cs": r"Agents\Views",
    "SettingsPage.xaml": r"Settings\Views",
    "SettingsPage.xaml.cs": r"Settings\Views",
    "SyncPage.xaml": r"Settings\Views",
    "SyncPage.xaml.cs": r"Settings\Views",
    "TemplateManagementPage.xaml": r"Printing\Views",
    "TemplateManagementPage.xaml.cs": r"Printing\Views",
}

for file, relative_dest in mapping.items():
    src_file = os.path.join(src_dir, file)
    dest_dir = os.path.join(dest_base, relative_dest)
    os.makedirs(dest_dir, exist_ok=True)
    dest_file = os.path.join(dest_dir, file)
    
    shutil.copy2(src_file, dest_file)
    print(f"Copied {file} to {dest_file}")
