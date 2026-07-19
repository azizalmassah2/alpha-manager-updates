using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using System.Collections.Generic;
using Lux.Management.Console.ViewModels;
namespace Lux.Management.Console.Modules.Operations.ViewModels;

public partial class ModemOperationsViewModel : ViewModelBase
{
    private readonly IBatchOperationService _batchOperationService;

    public ModemOperationsViewModel(
        IBatchOperationService batchOperationService,
        IPermissionService permissionService,
        IEventBus eventBus) 
        : base(permissionService, eventBus)
    {
        _batchOperationService = batchOperationService;
    }

    [RelayCommand]
    private async Task RebootAllModemsAsync()
    {
        try
        {
            // For now passing empty list or mock guids, in real life we would select them from UI
            var jobId = await _batchOperationService.ExecuteModemBatchRebootAsync(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
            MessageBox.Show($"Batch Reboot Job Queued. Job ID: {jobId}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to queue batch reboot: {ex.Message}");
        }
    }
}
