import os

modules = {
    "Profiles": ["ProfileManagementViewModel"],
    "Agents": ["AgentManagementViewModel"],
    "Settings": ["SettingsViewModel", "SyncViewModel"],
    "Printing": ["TemplateManagementViewModel"]
}

base_dir = r"d:\LUXCARD\desktop\Lux.Management.Console\Modules"

template = """using Lux.Management.Console.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Modules.{module}.ViewModels;

public partial class {viewmodel} : ViewModelBase
{{
    public {viewmodel}(IPermissionService permissionService, IEventBus eventBus) : base(permissionService, eventBus)
    {{
    }}
}}
"""

for module, vms in modules.items():
    vm_dir = os.path.join(base_dir, module, "ViewModels")
    os.makedirs(vm_dir, exist_ok=True)
    
    for vm in vms:
        content = template.format(module=module, viewmodel=vm)
        with open(os.path.join(vm_dir, f"{vm}.cs"), "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Created {vm}.cs")
