import os

file = r"d:\LUXCARD\desktop\Lux.Management.Console\ViewModels\MainViewModel.cs"

with open(file, 'r', encoding='utf-8') as f:
    content = f.read()

commands = """    [RelayCommand]
    private void NavigateProfiles()
    {
        _regionManager.NavigateTo<Lux.Management.Console.Modules.Profiles.ViewModels.ProfileManagementViewModel>("MainRegion");
    }

    [RelayCommand]
    private void NavigateAgents()
    {
        _regionManager.NavigateTo<Lux.Management.Console.Modules.Agents.ViewModels.AgentManagementViewModel>("MainRegion");
    }

    [RelayCommand]
    private void NavigateTemplates()
    {
        _regionManager.NavigateTo<Lux.Management.Console.Modules.Printing.ViewModels.TemplateManagementViewModel>("MainRegion");
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        _regionManager.NavigateTo<Lux.Management.Console.Modules.Settings.ViewModels.SettingsViewModel>("MainRegion");
    }
}"""

content = content.replace("}", commands)

with open(file, 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated MainViewModel.cs commands")
