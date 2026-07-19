import os

file = r"d:\LUXCARD\desktop\Lux.Management.Console\App.xaml"

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

namespaces = """             xmlns:printingviews="clr-namespace:Lux.Management.Console.Modules.Printing.Views"
             xmlns:printingvms="clr-namespace:Lux.Management.Console.Modules.Printing.ViewModels"
             xmlns:profilesviews="clr-namespace:Lux.Management.Console.Modules.Profiles.Views"
             xmlns:profilesvms="clr-namespace:Lux.Management.Console.Modules.Profiles.ViewModels"
             xmlns:agentsviews="clr-namespace:Lux.Management.Console.Modules.Agents.Views"
             xmlns:agentsvms="clr-namespace:Lux.Management.Console.Modules.Agents.ViewModels"
             xmlns:settingsviews="clr-namespace:Lux.Management.Console.Modules.Settings.Views"
             xmlns:settingsvms="clr-namespace:Lux.Management.Console.Modules.Settings.ViewModels"
"""

content = content.replace(
"""             xmlns:printingviews="clr-namespace:Lux.Management.Console.Modules.Printing.Views"
             xmlns:printingvms="clr-namespace:Lux.Management.Console.Modules.Printing.ViewModels"
""", namespaces)

templates = """            <DataTemplate DataType="{x:Type printingvms:PrintCenterViewModel}">
                <printingviews:PrintCenterPage />
            </DataTemplate>
            <DataTemplate DataType="{x:Type profilesvms:ProfileManagementViewModel}">
                <profilesviews:ProfileManagementPage />
            </DataTemplate>
            <DataTemplate DataType="{x:Type agentsvms:AgentManagementViewModel}">
                <agentsviews:AgentManagementPage />
            </DataTemplate>
            <DataTemplate DataType="{x:Type settingsvms:SettingsViewModel}">
                <settingsviews:SettingsPage />
            </DataTemplate>
            <DataTemplate DataType="{x:Type settingsvms:SyncViewModel}">
                <settingsviews:SyncPage />
            </DataTemplate>
            <DataTemplate DataType="{x:Type printingvms:TemplateManagementViewModel}">
                <printingviews:TemplateManagementPage />
            </DataTemplate>
"""

content = content.replace(
"""            <DataTemplate DataType="{x:Type printingvms:PrintCenterViewModel}">
                <printingviews:PrintCenterPage />
            </DataTemplate>
""", templates)

with open(file, 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated App.xaml with new DataTemplates and Namespaces")
