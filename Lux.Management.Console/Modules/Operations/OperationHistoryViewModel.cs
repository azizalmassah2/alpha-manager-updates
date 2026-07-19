using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.ViewModels;

namespace Lux.Management.Console.Modules.Operations.ViewModels;

public partial class OperationHistoryViewModel : ViewModelBase
{
    private readonly IOperationHistoryService _historyService;

    [ObservableProperty]
    private ObservableCollection<OperationAuditRecord> _auditRecords = new();

    public OperationHistoryViewModel(
        IOperationHistoryService historyService,
        IPermissionService permissionService,
        IEventBus eventBus) 
        : base(permissionService, eventBus)
    {
        _historyService = historyService;
        _ = LoadHistoryAsync();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task LoadHistoryAsync()
    {
        var history = await _historyService.GetAuditHistoryAsync(1, 50);
        AuditRecords.Clear();
        foreach (var record in history)
        {
            AuditRecords.Add(record);
        }
    }
}
