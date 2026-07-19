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

public partial class WirelessOperationsViewModel : ViewModelBase
{
    private readonly IBatchOperationService _batchOperationService;

    public WirelessOperationsViewModel(
        IBatchOperationService batchOperationService,
        IPermissionService permissionService,
        IEventBus eventBus) 
        : base(permissionService, eventBus)
    {
        _batchOperationService = batchOperationService;
    }

    [RelayCommand]
    private async Task RunSignalCheckAsync()
    {
        try
        {
            var jobId = await _batchOperationService.ExecuteWirelessSignalCheckAsync(new List<Guid> { Guid.NewGuid() });
            MessageBox.Show($"Wireless Signal Check Job Queued. Job ID: {jobId}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to queue signal check: {ex.Message}");
        }
    }
}
