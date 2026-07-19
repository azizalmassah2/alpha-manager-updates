import os

file = r"d:\LUXCARD\desktop\Lux.Management.Console\App.xaml.cs"

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

namespaces = """using Lux.Management.Console.Modules.Settings.Views;
using Lux.Management.Console.Modules._Migration;
"""

content = content.replace(
"""using Lux.Management.Console.Modules.Settings.Views;""",
namespaces)


services = """                services.AddSingleton<IEventBus, EventBus>();
                services.AddSingleton<ILegacyScreenMigrationService, LegacyScreenMigrationService>();"""

content = content.replace(
"""                services.AddSingleton<IEventBus, EventBus>();""",
services)

with open(file, 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated App.xaml.cs with ILegacyScreenMigrationService")
