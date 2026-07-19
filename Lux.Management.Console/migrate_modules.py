import os
import re

base_path = r"d:\LUXCARD\desktop\Lux.Management.Console"

moves = {
    "DashboardPage.xaml": "Dashboard",
    "DashboardPage.xaml.cs": "Dashboard",
    "DashboardViewModel.cs": "Dashboard",
    "DevicesMonitorPage.xaml": "Monitoring",
    "DevicesMonitorPage.xaml.cs": "Monitoring",
    "DevicesMonitorViewModel.cs": "Monitoring",
    "DeviceDetailsPage.xaml": "Devices",
    "DeviceDetailsPage.xaml.cs": "Devices",
    "DeviceDetailsViewModel.cs": "Devices",
    "OperationsCenterPage.xaml": "Operations",
    "OperationsCenterPage.xaml.cs": "Operations",
    "OperationsCenterViewModel.cs": "Operations",
    "FirmwarePage.xaml": "Firmware",
    "FirmwarePage.xaml.cs": "Firmware",
    "FirmwareViewModel.cs": "Firmware",
    "AlertsPage.xaml": "Monitoring",
    "AlertsPage.xaml.cs": "Monitoring",
    "AlertsViewModel.cs": "Monitoring"
}

# Move files
for file, mod in moves.items():
    src = ""
    if "ViewModel" in file:
        src = os.path.join(base_path, "ViewModels", file)
    else:
        src = os.path.join(base_path, "Views", file)
    
    dest = os.path.join(base_path, "Modules", mod, file)
    
    if os.path.exists(src):
        os.rename(src, dest)
        print(f"Moved {file} to {mod}")

        # Update namespaces in C# and XAML files
        with open(dest, 'r', encoding='utf-8') as f:
            content = f.read()

        if file.endswith(".cs"):
            content = re.sub(r'namespace Lux\.Management\.Console\.(Views|ViewModels);?', f'namespace Lux.Management.Console.Modules.{mod};', content)
        elif file.endswith(".xaml"):
            content = re.sub(r'x:Class="Lux\.Management\.Console\.Views\.([^"]+)"', f'x:Class="Lux.Management.Console.Modules.{mod}.\\1"', content)

        with open(dest, 'w', encoding='utf-8') as f:
            f.write(content)
