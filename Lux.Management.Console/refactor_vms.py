import os
import re

files = [
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\ViewModels\GenerateVoucherViewModel.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Vouchers\ViewModels\VoucherManagementViewModel.cs",
    r"d:\LUXCARD\desktop\Lux.Management.Console\Modules\Printing\ViewModels\PrintCenterViewModel.cs"
]

def replace_in_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Change namespace
    content = re.sub(r'namespace MikroTikVoucherPrinter\.UI\.ViewModels\.Pages;', 
                     lambda m: 'namespace ' + ('Lux.Management.Console.Modules.Vouchers.ViewModels;' if 'Vouchers' in file_path else 'Lux.Management.Console.Modules.Printing.ViewModels;'), content)

    # Add usings
    if 'Lux.Platform.Abstractions.Interfaces' not in content:
        content = 'using Lux.Platform.Abstractions.Interfaces;\n' + content
    if 'Lux.Management.Console.ViewModels' not in content:
        content = 'using Lux.Management.Console.ViewModels;\n' + content

    # Inherit from ViewModelBase
    content = content.replace(' : BaseViewModel', ' : ViewModelBase')

    # Update constructor
    class_name = os.path.basename(file_path).replace('.cs', '')
    
    # We will find the constructor using regex
    # The constructor looks like: public ClassName(....) : base(...)
    
    ctor_pattern = r'public\s+' + class_name + r'\s*\((.*?)\)\s*:\s*base\((.*?)\)'
    
    def repl_ctor(match):
        args = match.group(1)
        base_args = match.group(2)
        
        # Add new dependencies
        new_args = args + ', IPermissionService permissionService, IEventBus eventBus, INotificationService notificationService, IDialogService dialogService, ILegacyScreenMigrationService migrationService'
        
        new_base = 'permissionService, eventBus'
        
        # We need to assign the new dependencies in the constructor body
        # So we just replace the signature for now, and we will inject assignments at the start of the body
        return f'private readonly INotificationService _notificationService;\n    private readonly IDialogService _dialogService;\n    private readonly ILegacyScreenMigrationService _migrationService;\n\n    public {class_name}({new_args}) : base({new_base})'
        
    content = re.sub(ctor_pattern, repl_ctor, content, flags=re.DOTALL)
    
    # Now we need to inject assignments into the constructor body. We can find the first '{' after the constructor
    body_pattern = r'(public ' + class_name + r'\s*\(.*?\) : base\(.*?\)\s*\{)'
    def repl_body(match):
        return match.group(1) + '\n        _notificationService = notificationService;\n        _dialogService = dialogService;\n        _migrationService = migrationService;\n        _migrationService.TrackScreen(this.GetType().Name, "MikroTikVoucherPrinter.UI", Lux.Management.Console.Modules._Migration.MigrationStatus.Completed, "Migrated with CommunityToolkit.Mvvm and Abstractions");'
        
    content = re.sub(body_pattern, repl_body, content, flags=re.DOTALL)
    
    # Replace MessageBox with _notificationService
    content = re.sub(r'System\.Windows\.MessageBox\.Show\(([^,]+),\s*([^,]+),\s*System\.Windows\.MessageBoxButton\.OK,\s*System\.Windows\.MessageBoxImage\.Error\)', 
                     r'_notificationService.ShowError(\1)', content)
    content = re.sub(r'MessageBox\.Show\(([^,]+),\s*([^,]+),\s*MessageBoxButton\.OK,\s*MessageBoxImage\.Information[^\)]*\)', 
                     r'_notificationService.ShowInfo(\1)', content)
    content = re.sub(r'MessageBox\.Show\(([^,]+),\s*([^,]+),\s*MessageBoxButton\.OK,\s*MessageBoxImage\.Error[^\)]*\)', 
                     r'_notificationService.ShowError(\1)', content)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {file_path}")

for f in files:
    replace_in_file(f)
