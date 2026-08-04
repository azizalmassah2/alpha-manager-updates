using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Agents.ViewModels;

public partial class AgentManagementViewModel : ViewModelBase, IActivatable
{
    private readonly IAgentService _agentService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IActiveRouterContext _activeRouterContext;

    public ObservableCollection<AgentDto> Agents { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    private bool _isEditing;

    public string FormTitle => IsEditing ? "✏️ تعديل بيانات الوكيل" : "➕ إضافة وكيل جديد";

    private Guid? _editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAgentCommand))]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formPhone = string.Empty;

    [ObservableProperty]
    private string _formNotes = string.Empty;

    [ObservableProperty]
    private decimal _formCommissionRate;

    [ObservableProperty]
    private bool _showForm;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditAgentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAgentCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SettleBalanceCommand))]
    private AgentDto? _selectedAgent;

    [ObservableProperty]
    private int _totalAgents;

    [ObservableProperty]
    private int _activeAgents;

    [ObservableProperty]
    private WorkspaceState _currentState = WorkspaceState.Loading;

    [ObservableProperty]
    private int _totalVouchers;

    [ObservableProperty]
    private decimal _totalSalesRevenue;

    [ObservableProperty]
    private decimal _totalEarnedCommissions;

    [ObservableProperty]
    private decimal _totalNetOwed;

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public ObservableCollection<AgentDto> DisplayedAgents { get; } = new();
    private readonly List<AgentDto> _allAgentsCache = new();

    public AgentManagementViewModel(
        IPermissionService permissionService, 
        IEventBus eventBus,
        IAgentService agentService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        INotificationService notificationService,
        IActiveRouterContext activeRouterContext) : base(permissionService, eventBus)
    {
        _agentService = agentService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _activeRouterContext = activeRouterContext;

        Title = "إدارة الوكلاء";

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        _dispatcherService.InvokeAsync(async () => await LoadAgentsAsync());
    }

    public override void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        base.Dispose();
    }

    // [PHASE-2] IActivatable.ActivateAsync — Lazy Loading عند التنقل
    public Task ActivateAsync() => InitializeAsync(null);

    public async Task InitializeAsync(object? parameter = null)
    {
        await LoadAgentsAsync();
    }

    [RelayCommand]
    private async Task LoadAgentsAsync()
    {
        CurrentState = Agents.Count == 0 ? WorkspaceState.Loading : WorkspaceState.Refreshing;
        try
        {
            var agentsList = await _agentService.GetAllAgentsAsync(CancellationToken.None);
            
            await _dispatcherService.InvokeAsync(() =>
            {
                _allAgentsCache.Clear();
                _allAgentsCache.AddRange(agentsList);

                Agents.Clear();
                foreach (var a in agentsList)
                {
                    Agents.Add(a);
                }
                
                TotalAgents = Agents.Count;
                ActiveAgents = Agents.Count(a => a.IsActive);
                TotalVouchers = Agents.Sum(a => a.VoucherCount);
                TotalSalesRevenue = Agents.Sum(a => a.TotalSalesAmount);
                TotalEarnedCommissions = Agents.Sum(a => a.EarnedCommission);
                TotalNetOwed = Agents.Sum(a => a.NetOwedBalance);

                ApplySearchFilter();
            });
            CurrentState = Agents.Count == 0 ? WorkspaceState.Empty : WorkspaceState.Loaded;
        }
        catch (Exception ex)
        {
            if (CurrentState == WorkspaceState.Loading)
            {
                CurrentState = WorkspaceState.Error;
            }
            else
            {
                CurrentState = WorkspaceState.Loaded;
                _notificationService.ShowError($"فشل تحديث الوكلاء: {ex.Message}");
            }
        }
    }

    private void ApplySearchFilter()
    {
        DisplayedAgents.Clear();
        var q = SearchQuery?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(q)
            ? _allAgentsCache
            : _allAgentsCache.Where(a =>
                a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Phone.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Notes.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
        {
            DisplayedAgents.Add(item);
        }
    }

    private bool CanSettleBalance() => SelectedAgent != null;

    [RelayCommand(CanExecute = nameof(CanSettleBalance))]
    private async Task SettleBalanceAsync()
    {
        if (SelectedAgent == null) return;

        var dialog = new Views.SettleAgentBalanceDialog(SelectedAgent.Name, SelectedAgent.NetOwedBalance)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.PaymentAmount > 0)
        {
            await ExecuteBusyAsync(async (token) =>
            {
                try
                {
                    await _agentService.SettleAgentBalanceAsync(SelectedAgent.Id, dialog.PaymentAmount, dialog.PaymentNotes, token);
                    await _dispatcherService.InvokeAsync(() =>
                    {
                        _notificationService.ShowSuccess($"✅ تم تسديد مبلغ ({dialog.PaymentAmount:N0}) لحساب الوكيل [{SelectedAgent.Name}] بنجاح.", "تسديد حساب");
                    });
                    await LoadAgentsAsync();
                }
                catch (Exception ex)
                {
                    await _dispatcherService.InvokeAsync(() => _notificationService.ShowError($"فشل تسديد الحساب: {ex.Message}"));
                }
            }, "جارٍ تسديد رصيد الوكيل...");
        }
    }

    [RelayCommand]
    private void AddNew()
    {
        IsEditing = false;
        _editingId = null;
        FormName = string.Empty;
        FormPhone = string.Empty;
        FormNotes = string.Empty;
        FormCommissionRate = 0;
        ShowForm = true;
    }

    private bool CanEditAgent() => SelectedAgent != null;

    [RelayCommand(CanExecute = nameof(CanEditAgent))]
    private void EditAgent()
    {
        if (SelectedAgent == null) return;
        
        IsEditing = true;
        _editingId = SelectedAgent.Id;
        FormName = SelectedAgent.Name;
        FormPhone = SelectedAgent.Phone ?? string.Empty;
        FormNotes = SelectedAgent.Notes ?? string.Empty;
        FormCommissionRate = SelectedAgent.CommissionRate;
        ShowForm = true;
    }

    [RelayCommand]
    private void CancelForm()
    {
        ShowForm = false;
        IsEditing = false;
        _editingId = null;
    }

    private bool CanSaveAgent() => !string.IsNullOrWhiteSpace(FormName);

    [RelayCommand(CanExecute = nameof(CanSaveAgent))]
    private async Task SaveAgentAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            try
            {
                var agent = new Agent
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    Name = FormName,
                    Phone = FormPhone,
                    Notes = FormNotes,
                    CommissionRate = FormCommissionRate,
                    IsActive = IsEditing ? SelectedAgent!.IsActive : true,
                    CreatedAt = IsEditing ? SelectedAgent!.CreatedAt : DateTime.UtcNow,
                    Balance = IsEditing ? SelectedAgent!.Balance : 0
                };

                if (IsEditing)
                {
                    await _agentService.UpdateAgentAsync(agent, token);
                    await _dispatcherService.InvokeAsync(() => _notificationService.ShowSuccess("تم التعديل بنجاح"));
                }
                else
                {
                    await _agentService.CreateAgentAsync(agent, token);
                    await _dispatcherService.InvokeAsync(() => _notificationService.ShowSuccess("تمت الإضافة بنجاح"));
                }

                await _dispatcherService.InvokeAsync(() => ShowForm = false);
                await LoadAgentsAsync();
            }
            catch (Exception ex)
            {
                await _dispatcherService.InvokeAsync(() => _notificationService.ShowError(ex.Message));
            }
        }, "جاري حفظ البيانات...");
    }

    private bool CanDeleteAgent() => SelectedAgent != null;

    [RelayCommand(CanExecute = nameof(CanDeleteAgent))]
    private async Task DeleteAgentAsync()
    {
        if (SelectedAgent == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync($"هل أنت متأكد من رغبتك في حذف الوكيل [{SelectedAgent.Name}]؟\nلا يمكن التراجع، وسيتم إخفاء الكروت المرتبطة به.", "تأكيد الحذف");
        if (!confirm) return;

        await ExecuteBusyAsync(async (token) =>
        {
            try
            {
                await _agentService.DeleteAgentAsync(SelectedAgent.Id, token);
                await _dispatcherService.InvokeAsync(() =>
                {
                    _notificationService.ShowSuccess("تم حذف الوكيل.");
                    ShowForm = false;
                });
                await LoadAgentsAsync();
            }
            catch (Exception ex)
            {
                await _dispatcherService.InvokeAsync(() => _notificationService.ShowError(ex.Message));
            }
        }, "جاري الحذف...");
    }

    private bool CanToggleActive() => SelectedAgent != null;

    [RelayCommand(CanExecute = nameof(CanToggleActive))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedAgent == null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            try
            {
                var newState = await _agentService.ToggleActiveAsync(SelectedAgent.Id, token);
                await _dispatcherService.InvokeAsync(() =>
                {
                    _notificationService.ShowSuccess(newState ? "تم تنشيط الوكيل" : "تم إيقاف الوكيل");
                });
                await LoadAgentsAsync();
            }
            catch (Exception ex)
            {
                await _dispatcherService.InvokeAsync(() => _notificationService.ShowError(ex.Message));
            }
        }, "تحديث حالة الوكيل...");
    }
}
