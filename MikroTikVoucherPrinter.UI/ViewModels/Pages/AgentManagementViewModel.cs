using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class AgentManagementViewModel : BaseViewModel
{
    private readonly IAgentService _agentService;

    public ObservableCollection<AgentDto> Agents { get; } = new();

    // ═══════════════════════════════════════════════════
    //  حالة نموذج التحرير
    // ═══════════════════════════════════════════════════
    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set { SetProperty(ref _isEditing, value); OnPropertyChanged(nameof(FormTitle)); }
    }

    public string FormTitle => IsEditing ? "✏️ تعديل بيانات الوكيل" : "➕ إضافة وكيل جديد";

    // ═══════════════════════════════════════════════════
    //  حقول النموذج
    // ═══════════════════════════════════════════════════
    private Guid? _editingId;

    private string _formName = "";
    public string FormName { get => _formName; set { SetProperty(ref _formName, value); SaveCommand.NotifyCanExecuteChanged(); } }

    private string _formPhone = "";
    public string FormPhone { get => _formPhone; set => SetProperty(ref _formPhone, value); }

    private string _formNotes = "";
    public string FormNotes { get => _formNotes; set => SetProperty(ref _formNotes, value); }

    private decimal _formCommissionRate;
    public decimal FormCommissionRate { get => _formCommissionRate; set => SetProperty(ref _formCommissionRate, value); }

    private bool _showForm;
    public bool ShowForm { get => _showForm; set => SetProperty(ref _showForm, value); }

    // ═══════════════════════════════════════════════════
    //  التحديد
    // ═══════════════════════════════════════════════════
    private AgentDto? _selectedAgent;
    public AgentDto? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            SetProperty(ref _selectedAgent, value);
            (EditCommand   as RelayCommand)?.NotifyCanExecuteChanged();
            (DeleteCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (ToggleActiveCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    // ═══════════════════════════════════════════════════
    //  الإحصائيات
    // ═══════════════════════════════════════════════════
    private int _totalAgents;
    public int TotalAgents { get => _totalAgents; set => SetProperty(ref _totalAgents, value); }

    private int _activeAgents;
    public int ActiveAgents { get => _activeAgents; set => SetProperty(ref _activeAgents, value); }

    private int _totalVouchers;
    public int TotalVouchers { get => _totalVouchers; set => SetProperty(ref _totalVouchers, value); }

    private string _lastMessage = "";
    public string LastMessage { get => _lastMessage; set => SetProperty(ref _lastMessage, value); }

    // ═══════════════════════════════════════════════════
    //  الأوامر
    // ═══════════════════════════════════════════════════
    public IAsyncRelayCommand LoadCommand        { get; }
    public IAsyncRelayCommand SaveCommand        { get; }
    public IAsyncRelayCommand DeleteCommand      { get; }
    public IAsyncRelayCommand ToggleActiveCommand { get; }
    public IRelayCommand      EditCommand         { get; }
    public IRelayCommand      AddNewCommand       { get; }
    public IRelayCommand      CancelFormCommand   { get; }

    public AgentManagementViewModel(IAgentService agentService, ILogger<AgentManagementViewModel> logger)
        : base(logger)
    {
        _agentService = agentService;
        Title = "إدارة الوكلاء";

        LoadCommand         = new AsyncRelayCommand(LoadAgentsAsync);
        SaveCommand         = new AsyncRelayCommand(SaveAgentAsync, () => !string.IsNullOrWhiteSpace(FormName));
        DeleteCommand       = new AsyncRelayCommand(DeleteAgentAsync, () => SelectedAgent != null);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => SelectedAgent != null);
        EditCommand         = new RelayCommand(BeginEdit, () => SelectedAgent != null);
        AddNewCommand       = new RelayCommand(BeginAddNew);
        CancelFormCommand   = new RelayCommand(CancelForm);
    }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadAgentsAsync();
    }

    // ═══════════════════════════════════════════════════
    //  جلب قائمة الوكلاء
    // ═══════════════════════════════════════════════════
    private async Task LoadAgentsAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var data = await _agentService.GetAllAgentsAsync(token);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Agents.Clear();
                foreach (var a in data) Agents.Add(a);

                TotalAgents   = data.Count;
                ActiveAgents  = data.Count(a => a.IsActive);
                TotalVouchers = data.Sum(a => a.VoucherCount);
            });

            Logger.LogInformation("✅ تم تحميل {Count} وكيل", data.Count);
        }, "جاري تحميل الوكلاء...");
    }

    // ═══════════════════════════════════════════════════
    //  بدء إضافة جديد
    // ═══════════════════════════════════════════════════
    private void BeginAddNew()
    {
        _editingId         = null;
        IsEditing          = false;
        FormName           = "";
        FormPhone          = "";
        FormNotes          = "";
        FormCommissionRate = 0;
        ShowForm           = true;
    }

    // ═══════════════════════════════════════════════════
    //  بدء التعديل
    // ═══════════════════════════════════════════════════
    private void BeginEdit()
    {
        if (SelectedAgent == null) return;

        _editingId         = SelectedAgent.Id;
        IsEditing          = true;
        FormName           = SelectedAgent.Name;
        FormPhone          = SelectedAgent.Phone;
        FormNotes          = SelectedAgent.Notes;
        FormCommissionRate = SelectedAgent.CommissionRate;
        ShowForm           = true;
    }

    // ═══════════════════════════════════════════════════
    //  إلغاء النموذج
    // ═══════════════════════════════════════════════════
    private void CancelForm()
    {
        ShowForm  = false;
        IsEditing = false;
    }

    // ═══════════════════════════════════════════════════
    //  حفظ (إنشاء أو تعديل)
    // ═══════════════════════════════════════════════════
    private async Task SaveAgentAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            if (IsEditing && _editingId.HasValue)
            {
                var agent = new Agent
                {
                    Id             = _editingId.Value,
                    Name           = FormName.Trim(),
                    Phone          = FormPhone.Trim(),
                    Notes          = FormNotes.Trim(),
                    CommissionRate = FormCommissionRate,
                    IsActive       = true
                };
                await _agentService.UpdateAgentAsync(agent, token);
                LastMessage = $"✅ تم تحديث بيانات الوكيل: {FormName}";
            }
            else
            {
                var agent = new Agent
                {
                    Name           = FormName.Trim(),
                    Phone          = FormPhone.Trim(),
                    Notes          = FormNotes.Trim(),
                    CommissionRate = FormCommissionRate,
                    IsActive       = true
                };
                await _agentService.CreateAgentAsync(agent, token);
                LastMessage = $"✅ تم إضافة وكيل جديد: {FormName}";
            }

            ShowForm = false;
            await LoadAgentsAsync();

        }, "جاري الحفظ...");
    }

    // ═══════════════════════════════════════════════════
    //  حذف وكيل
    // ═══════════════════════════════════════════════════
    private async Task DeleteAgentAsync()
    {
        if (SelectedAgent == null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var name = SelectedAgent.Name;
            await _agentService.DeleteAgentAsync(SelectedAgent.Id, token);
            LastMessage = $"🗑️ تم حذف الوكيل: {name}";
            await LoadAgentsAsync();

        }, "جاري الحذف...");
    }

    // ═══════════════════════════════════════════════════
    //  تبديل التفعيل
    // ═══════════════════════════════════════════════════
    private async Task ToggleActiveAsync()
    {
        if (SelectedAgent == null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var newState = await _agentService.ToggleActiveAsync(SelectedAgent.Id, token);
            LastMessage = newState
                ? $"✅ تم تفعيل الوكيل: {SelectedAgent.Name}"
                : $"⏸️ تم إيقاف الوكيل: {SelectedAgent.Name}";
            await LoadAgentsAsync();

        }, "جاري تبديل الحالة...");
    }
}
