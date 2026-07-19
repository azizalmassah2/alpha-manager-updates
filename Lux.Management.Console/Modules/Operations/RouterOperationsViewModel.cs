using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using Lux.Management.Console.ViewModels;
namespace Lux.Management.Console.Modules.Operations.ViewModels;

public partial class RouterOperationsViewModel : ViewModelBase
{
    private readonly IBatchOperationService _batchOperationService;
    private readonly IOperationEngine _operationEngine;

    public RouterOperationsViewModel(
        IBatchOperationService batchOperationService, 
        IOperationEngine operationEngine,
        IPermissionService permissionService,
        IEventBus eventBus) 
        : base(permissionService, eventBus)
    {
        _batchOperationService = batchOperationService;
        _operationEngine = operationEngine;
    }

    [RelayCommand]
    private async Task BackupRouterAsync()
    {
        try
        {
            var jobId = await _batchOperationService.ExecuteRouterBackupAsync();
            MessageBox.Show($"Backup Job Queued. Job ID: {jobId}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to queue backup: {ex.Message}");
        }
    }
}
