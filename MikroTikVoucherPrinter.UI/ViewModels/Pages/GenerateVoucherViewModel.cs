using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class GenerateVoucherViewModel : BaseViewModel
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IProfileService _profileService;
    private readonly IPrintService _printService;
    private readonly IVoucherQueryService _queryService;
    private readonly IAgentService _agentService;
    private readonly ISyncService _syncService;
    private readonly IGenericRepository<Batch> _batchRepo;
    private readonly ITemplateService _templateService;
    private readonly ISettingsService _settingsService;

    private Guid _fallbackSystemTemplateId;
    private bool _suppressPrintTemplatePersist;

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط§ظ„ط£ط­ط±ظپ ط§ظ„ظ…ط³طھط®ط¯ظ…ط© ظپظٹ ط§ظ„طھظˆظ„ظٹط¯ ط­ط³ط¨ ط§ظ„ظ†ظ…ط·
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private const string DIGITS = "0123456789";
    private const string DIGITS_SAFE = "23456789"; // ط¨ط¯ظˆظ† 0,1 ظ„طھط¬ظ†ط¨ ط§ظ„ط®ظ„ط·
    private const string LETTERS_UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // ط¨ط¯ظˆظ† I,O
    private const string LETTERS_LOWER = "abcdefghjkmnpqrstuvwxyz"; // ط¨ط¯ظˆظ† i,l,o
    private const string MIXED = LETTERS_UPPER + DIGITS_SAFE;
    private const string LOWERCASE_MIXED = LETTERS_LOWER + DIGITS_SAFE;

    public GenerateVoucherViewModel(
        IVoucherRepository voucherRepository,
        IProfileService profileService,
        IPrintService printService,
        IVoucherQueryService queryService,
        IAgentService agentService,
        ISyncService syncService,
        IGenericRepository<Batch> batchRepo,
        ITemplateService templateService,
        ISettingsService settingsService,
        ILogger<GenerateVoucherViewModel> logger) : base(logger)
    {
        _voucherRepository = voucherRepository;
        _profileService = profileService;
        _printService = printService;
        _queryService = queryService;
        _agentService = agentService;
        _syncService = syncService;
        _batchRepo = batchRepo;
        _templateService = templateService;
        _settingsService = settingsService;
        Title = "ط¥ظ†ط´ط§ط، ظƒط±ظˆطھ";
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط§ظ„ط¨ط§ظ‚ط§طھ (Profiles) - طھظڈط¬ظ„ط¨ ظ…ظ† ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    public ObservableCollection<Profile> Profiles { get; } = new();

    private Profile? _selectedProfile;
    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                // طھط­ط¯ظٹط« ط§ظ„ط³ط¹ط± طھظ„ظ‚ط§ط¦ظٹط§ظ‹ ط¹ظ†ط¯ ط§ط®طھظٹط§ط± ط¨ط§ظ‚ط©
                if (value != null) 
                {
                    Price = value.Price;
                    AutoSelectTemplateForProfile(value);
                }
            }
        }
    }

    private async void AutoSelectTemplateForProfile(Profile profile)
    {
        if (profile.TemplateId.HasValue && profile.TemplateId.Value != Guid.Empty)
        {
            var match = PrintTemplateOptions.FirstOrDefault(o => o.Source?.Id == profile.TemplateId.Value);
            if (match != null)
            {
                SelectedPrintTemplateOption = match;
                return;
            }
        }
        
        // No template linked
        var oldMsg = StatusMessage;
        StatusMessage = "ظ„ط§ ظٹظˆط¬ط¯ ظ‚ط§ظ„ط¨ ظ…ط±طھط¨ط· ط¨ط§ظ„ط¨ط§ظ‚ط©!";
        await Task.Delay(3000);
        if (StatusMessage == "ظ„ط§ ظٹظˆط¬ط¯ ظ‚ط§ظ„ط¨ ظ…ط±طھط¨ط· ط¨ط§ظ„ط¨ط§ظ‚ط©!") 
            StatusMessage = oldMsg ?? "ط§ظ„ظ†ط¸ط§ظ… ظ…ط³طھط¹ط¯ ظ„ظ„ظ…ط¹ط§ظ„ط¬ط©";
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط§ظ„ظˆظƒظ„ط§ط، (Agents) - ظٹظڈط¬ظ„ط¨ظˆظ† ظ…ظ† ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    public ObservableCollection<AgentDto> Agents { get; } = new();

    private AgentDto? _selectedAgent;
    public AgentDto? SelectedAgent
    {
        get => _selectedAgent;
        set => SetProperty(ref _selectedAgent, value);
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ظˆط¶ط¹ ط§ظ„طھظˆظ„ظٹط¯: ظƒط±ظˆطھ ط¨ط§ظ„ظƒظ…ظٹط© ط£ظˆ ظƒط±طھ ظˆط§ط­ط¯
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private GenerationMode _generationMode = GenerationMode.Bulk;
    public GenerationMode GenerationMode
    {
        get => _generationMode;
        set => SetProperty(ref _generationMode, value);
    }

    public bool IsBulkMode
    {
        get => GenerationMode == GenerationMode.Bulk;
        set { GenerationMode = value ? GenerationMode.Bulk : GenerationMode.Single; OnPropertyChanged(nameof(IsBulkMode)); OnPropertyChanged(nameof(IsSingleMode)); }
    }

    public bool IsSingleMode
    {
        get => GenerationMode == GenerationMode.Single;
        set { GenerationMode = value ? GenerationMode.Single : GenerationMode.Bulk; OnPropertyChanged(nameof(IsBulkMode)); OnPropertyChanged(nameof(IsSingleMode)); }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط®طµط§ط¦طµ ط§ظ„طھظˆظ„ظٹط¯ ط¨ط§ظ„ظƒظ…ظٹط© (Bulk)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private int _count = 0;
    public int Count 
    { 
        get => _count; 
        set 
        {
            if (SetProperty(ref _count, value))
                OnPropertyChanged(nameof(TotalPrice));
        }
    }

    private string _prefix = "";
    public string Prefix { get => _prefix; set => SetProperty(ref _prefix, value); }

    private int _usernameLength = 9;
    public int UsernameLength { get => _usernameLength; set => SetProperty(ref _usernameLength, value); }

    private int _passwordLength = 6;
    public int PasswordLength { get => _passwordLength; set => SetProperty(ref _passwordLength, value); }

    private CharacterMode _characterMode = CharacterMode.DigitsOnly;
    public CharacterMode SelectedCharacterMode { get => _characterMode; set => SetProperty(ref _characterMode, value); }

    private string _passwordPrefix = "";
    public string PasswordPrefix { get => _passwordPrefix; set => SetProperty(ref _passwordPrefix, value); }

    private CharacterMode _passwordCharacterMode = CharacterMode.DigitsOnly;
    public CharacterMode PasswordCharacterMode { get => _passwordCharacterMode; set => SetProperty(ref _passwordCharacterMode, value); }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط®طµط§ط¦طµ ظƒط±طھ ظˆط§ط­ط¯ (Single)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private string _singleUsername = "";
    public string SingleUsername { get => _singleUsername; set => SetProperty(ref _singleUsername, value); }

    private string _singlePassword = "";
    public string SinglePassword { get => _singlePassword; set => SetProperty(ref _singlePassword, value); }

    private bool _emptyPassword = false;
    public bool EmptyPassword { get => _emptyPassword; set => SetProperty(ref _emptyPassword, value); }

    private bool _bindToFirstDevice = false;
    public bool BindToFirstDevice { get => _bindToFirstDevice; set => SetProperty(ref _bindToFirstDevice, value); }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط®طµط§ط¦طµ ظ…ط´طھط±ظƒط©
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private decimal _price = 0;
    public decimal Price 
    { 
        get => _price; 
        set 
        {
            if (SetProperty(ref _price, value))
                OnPropertyChanged(nameof(TotalPrice));
        }
    }

    public decimal TotalPrice => Price * Count;

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ظ†ظ…ط· ط¨ظٹط§ظ†ط§طھ ط§ظ„ط§ط¹طھظ…ط§ط¯ (Credential Mode)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private CredentialMode _credentialMode = CredentialMode.UsernameOnly;
    public CredentialMode SelectedCredentialMode
    {
        get => _credentialMode;
        set
        {
            SetProperty(ref _credentialMode, value);
            OnPropertyChanged(nameof(ShowPasswordLength));
            OnPropertyChanged(nameof(CredentialModeHint));
            OnPropertyChanged(nameof(ShowSinglePasswordBox));
        }
    }

    /// <summary>ط­ظ‚ظˆظ„ طھظˆظ„ظٹط¯ ظƒظ„ظ…ط© ط³ط± ظ…ط³طھظ‚ظ„ط© (ط¨ط§ط¯ط¦ط©طŒ ظ†ظˆط¹ ط£ط­ط±ظپطŒ ط·ظˆظ„).</summary>
    public bool ShowPasswordLength => _credentialMode == CredentialMode.UsernameAndPassword;

    /// <summary>ط­ظ‚ظ„ ط¥ط¯ط®ط§ظ„ ظƒظ„ظ…ط© ط³ط± ظٹط¯ظˆظٹ ظ„ظƒط±طھ ظˆط§ط­ط¯ ط¹ظ†ط¯ ظˆط¬ظˆط¯ ظˆط¶ط¹ ظƒظ„ظ…ط© ط³ط± ظ…ط³طھظ‚ظ„ط©.</summary>
    public bool ShowSinglePasswordBox => _credentialMode == CredentialMode.UsernameAndPassword;

    /// <summary>طھظ„ظ…ظٹط­ طھظˆط¶ظٹط­ظٹ ط®ظپظٹظپ ظٹط¸ظ‡ط± طھط­طھ ط§ظ„ط¥ط¹ط¯ط§ط¯</summary>
    public string CredentialModeHint => _credentialMode switch
    {
        CredentialMode.UsernameOnly           => "âڑ ï¸ڈ ط¨ط¯ظˆظ† ظƒظ„ظ…ط© ط³ط± â€” ظٹطµظ„ط­ ظ„ظ„ط´ط¨ظƒط§طھ ط§ظ„ظ…ظپطھظˆط­ط©",
        CredentialMode.UsernameEqualsPassword  => "ًں”‘ ظƒظ„ظ…ط© ط§ظ„ط³ط± = ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… طھظ…ط§ظ…ط§ظ‹",
        CredentialMode.UsernameAndPassword     => "ًں”’ ظƒظ„ظ…ط© ط³ط± ط¹ط´ظˆط§ط¦ظٹط© ظ…ط³طھظ‚ظ„ط© â€” ط£ط¹ظ„ظ‰ ط£ظ…ط§ظ†",
        _                                      => ""
    };

    private bool _printAfterGenerate = true;
    public bool PrintAfterGenerate { get => _printAfterGenerate; set => SetProperty(ref _printAfterGenerate, value); }

    private bool _autoSyncAfterGenerate = true;
    public bool AutoSyncAfterGenerate { get => _autoSyncAfterGenerate; set => SetProperty(ref _autoSyncAfterGenerate, value); }

    public ObservableCollection<PrintTemplatePickOption> PrintTemplateOptions { get; } = new();

    private PrintTemplatePickOption? _selectedPrintTemplateOption;
    public PrintTemplatePickOption? SelectedPrintTemplateOption
    {
        get => _selectedPrintTemplateOption;
        set
        {
            if (SetProperty(ref _selectedPrintTemplateOption, value))
            {
                OnPropertyChanged(nameof(PrintTemplateLiveSummary));
                if (!_suppressPrintTemplatePersist)
                    _ = PersistGeneratePrintTemplateChoiceAsync();
            }
        }
    }

    public string PrintTemplateLiveSummary =>
        SelectedPrintTemplateOption?.Source == null
            ? "ط§ط®طھط± ظ‚ط§ظ„ط¨ط§ظ‹ ظ„ط¹ط±ط¶ ظ…ظ„ط®طµ ط§ظ„ظ…ط¹ط§ظٹظ†ط© ظ‡ظ†ط§."
            : $"{SelectedPrintTemplateOption.Source.KindDisplay} آ· {SelectedPrintTemplateOption.Source.GridSummary} آ· {SelectedPrintTemplateOption.Source.Name}";

    // ظ†طھظٹط¬ط© ط§ظ„ط¹ظ…ظ„ظٹط© ط§ظ„ط£ط®ظٹط±ط©
    private string _resultMessage = "";
    public string ResultMessage { get => _resultMessage; set => SetProperty(ref _resultMessage, value); }

    private bool _hasResult = false;
    public bool HasResult { get => _hasResult; set => SetProperty(ref _hasResult, value); }

    private bool _isGenerating = false;
    public bool IsGenerating { get => _isGenerating; set => SetProperty(ref _isGenerating, value); }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط§ظ„ط£ظˆط§ظ…ط± (Commands)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط§ظ„طھظ‡ظٹط¦ط© - ط¬ظ„ط¨ ط§ظ„ط¨ط§ظ‚ط§طھ ظ…ظ† ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    // ظˆط¶ط¹ ط§ظ„ط£ظˆظپ ظ„ط§ظٹظ† â€” ظ†ط¹ط±ط¶ ط±ط³ط§ظ„ط© طھط­ط°ظٹط± ظ„ظ„ظ…ط³طھط®ط¯ظ…
    private bool _isOfflineMode = false;
    public bool IsOfflineMode { get => _isOfflineMode; set => SetProperty(ref _isOfflineMode, value); }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await ExecuteBusyAsync(async (token) =>
        {
            try
            {
                // ط¬ظ„ط¨ ط§ظ„ط¨ط§ظ‚ط§طھ â€” ظ…ظ† ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ ط£ظ„ط§ظ‹طŒ ط«ظ… ط§ظ„ظƒط§ط´ ط¹ظ†ط¯ ط§ظ„ظپط´ظ„
                var profiles = await _profileService.GetAllProfilesAsync(MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, token);
                var agents   = await _agentService.GetAllAgentsAsync(token);

                bool fromCache = profiles.Any() && profiles[0].IsFromCache;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsOfflineMode = fromCache;

                    Profiles.Clear();
                    foreach (var p in profiles) Profiles.Add(p);
                    if (Profiles.Count > 0) SelectedProfile = Profiles[0];

                    Agents.Clear();
                    Agents.Add(new AgentDto { Id = Guid.Empty, Name = "-- ط¨ط¯ظˆظ† ظˆظƒظٹظ„ --" });
                    foreach (var a in agents.Where(x => x.IsActive)) Agents.Add(a);
                    SelectedAgent = Agents[0];
                });

                Logger.LogInformation("âœ… طھظ… طھط­ظ…ظٹظ„ {Count} ط¨ط§ظ‚ط© | ظˆط¶ط¹ ط§ظ„ط£ظˆظپ ظ„ط§ظٹظ†: {Offline}", profiles.Count, fromCache);

                await LoadPrintTemplatePickerAsync(token);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ResultMessage = $"â‌Œ ط®ط·ط£ ط£ط«ظ†ط§ط، طھط­ظ…ظٹظ„ ط§ظ„ط¨ظٹط§ظ†ط§طھ: {ex.Message}";
                    HasResult = true;
                    System.Windows.MessageBox.Show($"ط¹ط°ط±ط§ظ‹طŒ ظپط´ظ„ طھط­ظ…ظٹظ„ ط§ظ„ط¨ظٹط§ظ†ط§طھ ظ…ظ† ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ ط£ظˆ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ.\nطھظپط§طµظٹظ„ ط§ظ„ط®ط·ط£: {ex.Message}", "ط®ط·ط£ ظپظٹ طھط­ظ…ظٹظ„ ط§ظ„طµظپط­ط©", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
                throw; // rethrow to let ExecuteBusyAsync handle it
            }
        }, "ط¬ط§ط±ظٹ طھط­ظ…ظٹظ„ ط§ظ„ط¨ط§ظ‚ط§طھ ظ…ظ† ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ...");
    }

    private async Task LoadPrintTemplatePickerAsync(System.Threading.CancellationToken token)
    {
        _fallbackSystemTemplateId = await _templateService.GetPrimarySystemTemplateIdAsync(token);
        var templates = await _templateService.GetTemplatesAsync(token);
        var lastRaw = _settingsService.Get("Print.LastGenerateTemplateId", "");
        _ = Guid.TryParse(lastRaw, out var lastGuid);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _suppressPrintTemplatePersist = true;
            try
            {
                PrintTemplateOptions.Clear();
                PrintTemplateOptions.Add(new PrintTemplatePickOption
                {
                    IsProfileDefaultChoice = true,
                    Title = "ط§ظپطھط±ط§ط¶ظٹ ط§ظ„ط¨ط§ظ‚ط©",
                    Subtitle = "ظ‚ط§ظ„ط¨ ط§ظ„ط¨ط§ظ‚ط© â€” ط£ظˆ ط§ظ„ظ‚ط§ظ„ط¨ ط§ظ„ظ†ط¸ط§ظ…ظٹ ط¥ظ† ظ„ظ… ظٹظڈط­ط¯ظ‘ظژط¯",
                    TemplateId = null,
                    ThumbnailPath = null,
                    Source = null
                });

                foreach (var dto in templates)
                {
                    PrintTemplateOptions.Add(new PrintTemplatePickOption
                    {
                        TemplateId = dto.Id,
                        Title = dto.Name,
                        Subtitle = $"{dto.KindDisplay} آ· {dto.GridSummary}",
                        ThumbnailPath = dto.BackgroundImagePath,
                        Source = dto
                    });
                }

                SelectedPrintTemplateOption = PrintTemplateOptions.FirstOrDefault(o => o.TemplateId == lastGuid)
                    ?? PrintTemplateOptions.FirstOrDefault(o => o.IsProfileDefaultChoice)
                    ?? PrintTemplateOptions.FirstOrDefault();
            }
            finally
            {
                _suppressPrintTemplatePersist = false;
            }
        });
    }

    private async Task PersistGeneratePrintTemplateChoiceAsync()
    {
        try
        {
            if (SelectedPrintTemplateOption?.TemplateId is Guid tid && !SelectedPrintTemplateOption.IsProfileDefaultChoice)
                _settingsService.Set("Print.LastGenerateTemplateId", tid.ToString());
            else
                _settingsService.Set("Print.LastGenerateTemplateId", string.Empty);
            await _settingsService.SaveAsync();
        }
        catch
        {
            /* طھط¬ط§ظ‡ظ„ ظپط´ظ„ ط§ظ„ط­ظپط¸ â€” ظ„ط§ ظٹط¹ط·ظ„ ط§ظ„طھظˆظ„ظٹط¯ */
        }
    }

    private Guid? ResolveEffectivePrintTemplateId()
    {
        if (SelectedPrintTemplateOption?.IsProfileDefaultChoice == true)
            return SelectedProfile?.TemplateId ?? (_fallbackSystemTemplateId != Guid.Empty ? _fallbackSystemTemplateId : null);

        if (SelectedPrintTemplateOption?.TemplateId is Guid g)
            return g;

        return _fallbackSystemTemplateId != Guid.Empty ? _fallbackSystemTemplateId : null;
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ظ…ط­ط±ظƒ طھظˆظ„ظٹط¯ ط§ظ„ط£ط­ط±ظپ ط§ظ„ط°ظƒظٹ
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private static string PoolForCharacterMode(CharacterMode mode) => mode switch
    {
        CharacterMode.DigitsOnly => DIGITS,
        CharacterMode.LettersOnly => LETTERS_UPPER,
        CharacterMode.Mixed => MIXED,
        CharacterMode.LowercaseMixed => LOWERCASE_MIXED,
        _ => MIXED
    };

    private string GetCharacterPool() => PoolForCharacterMode(SelectedCharacterMode);

    private string GetPasswordCharacterPool() => PoolForCharacterMode(PasswordCharacterMode);

    private string GenerateRandomString(Random rnd, int length, string pool)
    {
        return new string(Enumerable.Repeat(pool, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط®طµط§ط¦طµ طھطھط¨ط¹ ط§ظ„طھظ‚ط¯ظ… ط§ظ„ط­ظٹ (Live Progress)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private int _progressCurrent;
    public int ProgressCurrent { get => _progressCurrent; set { SetProperty(ref _progressCurrent, value); OnPropertyChanged(nameof(ProgressPercent)); } }

    private int _progressTotal;
    public int ProgressTotal { get => _progressTotal; set => SetProperty(ref _progressTotal, value); }

    public int ProgressPercent => _progressTotal > 0 ? (int)((double)_progressCurrent / _progressTotal * 100) : 0;

    private string _currentPhase = "";
    public string CurrentPhase { get => _currentPhase; set => SetProperty(ref _currentPhase, value); }

    private string _elapsedTimeText = "00:00";
    public string ElapsedTimeText { get => _elapsedTimeText; set => SetProperty(ref _elapsedTimeText, value); }

    private int _syncSuccessCount;
    public int SyncSuccessCount { get => _syncSuccessCount; set => SetProperty(ref _syncSuccessCount, value); }

    private int _syncFailedCount;
    public int SyncFailedCount { get => _syncFailedCount; set => SetProperty(ref _syncFailedCount, value); }

    private System.Diagnostics.Stopwatch? _stopwatch;
    private System.Threading.Timer? _elapsedTimer;

    private void StartElapsedTimer()
    {
        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _elapsedTimer = new System.Threading.Timer(_ =>
        {
            if (_stopwatch != null)
            {
                var elapsed = _stopwatch.Elapsed;
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ElapsedTimeText = elapsed.TotalHours >= 1
                        ? elapsed.ToString(@"hh\:mm\:ss")
                        : elapsed.ToString(@"mm\:ss");
                });
            }
        }, null, 0, 500);
    }

    private void StopElapsedTimer()
    {
        _stopwatch?.Stop();
        _elapsedTimer?.Dispose();
        _elapsedTimer = null;
    }

    private void ResetProgress()
    {
        ProgressCurrent = 0;
        ProgressTotal = 0;
        SyncSuccessCount = 0;
        SyncFailedCount = 0;
        CurrentPhase = "";
        ElapsedTimeText = "00:00";
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  طھظˆظ„ظٹط¯ ظƒط±ظˆطھ ط¨ط§ظ„ظƒظ…ظٹط© (Bulk Generation)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    [RelayCommand]
    private async Task RefreshProfilesAsync()
    {
        await InitializeAsync();
    }

    [RelayCommand]
    private void IncrementCount() => Count = Math.Min(10000, Count + 1);

    [RelayCommand]
    private void DecrementCount() => Count = Math.Max(1, Count - 1);

    [RelayCommand]
    private void IncrementUsernameLength() => UsernameLength = Math.Min(32, UsernameLength + 1);

    [RelayCommand]
    private void DecrementUsernameLength() => UsernameLength = Math.Max(1, UsernameLength - 1);

    [RelayCommand]
    private void IncrementPasswordLength() => PasswordLength = Math.Min(32, PasswordLength + 1);

    [RelayCommand]
    private void DecrementPasswordLength() => PasswordLength = Math.Max(1, PasswordLength - 1);

    [RelayCommand]
    private async Task GenerateBulkAsync()
    {
        if (SelectedProfile == null)
        {
            ResultMessage = "â‌Œ ظ„ط§ ظٹظ…ظƒظ† ط¥ظ†ط´ط§ط، ط§ظ„ظƒط±ظˆطھ: ظٹط±ط¬ظ‰ ط§ط®طھظٹط§ط± ط¨ط§ظ‚ط© (Profile) ط£ظˆظ„ط§ظ‹.";
            HasResult = true;
            return;
        }

        if (Count <= 0 || Count > 10000)
        {
            ResultMessage = "â‌Œ ط¹ط¯ط¯ ط§ظ„ظƒط±ظˆطھ ط؛ظٹط± طµط§ظ„ط­. ظٹط¬ط¨ ط£ظ† ظٹظƒظˆظ† ط¨ظٹظ† 1 ظˆ 10,000.";
            HasResult = true;
            return;
        }

        if (SelectedCredentialMode == CredentialMode.UsernameAndPassword && PasswordLength <= 0)
        {
            ResultMessage = "â‌Œ ظٹط±ط¬ظ‰ طھط­ط¯ظٹط¯ ط·ظˆظ„ طµط§ظ„ط­ ظ„ظƒظ„ظ…ط© ط§ظ„ط³ط±.";
            HasResult = true;
            return;
        }

        await ExecuteBusyAsync(async (token) =>
        {
            HasResult = false;
            ResultMessage = "";
            ResetProgress();
            StartElapsedTimer();
            IsGenerating = true;

            try
            {
                Logger.LogInformation("âڑ™ï¸ڈ ط¬ط§ط±ظٹ طھظˆظ„ظٹط¯ {Count} ظƒط±طھ ط¨ظ†ظ…ط· {Mode}...", Count, SelectedCharacterMode);

                // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ ط§ظ„ظ…ط±ط­ظ„ط© 1: طھظˆظ„ظٹط¯ ط§ظ„ط¨ظٹط§ظ†ط§طھ â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
                CurrentPhase = "طھظˆظ„ظٹط¯ ط§ظ„ظƒط±ظˆطھ";
                ProgressTotal = Count;
                StatusMessage = $"ًں”„ طھظˆظ„ظٹط¯ {Count} ظƒط±طھ...";
                var batchId = Guid.NewGuid();

                var newBatch = new Batch
                {
                    Id = batchId,
                    Name = $"ط¯ظپط¹ط© {DateTime.Now:yyyy-MM-dd HH:mm}",
                    ProfileName = SelectedProfile.Name,
                    TotalCount = Count
                };
                await _batchRepo.AddAsync(newBatch, token);

                var list = new List<Voucher>();
                var rnd = new Random();
                string pool = GetCharacterPool();

                for (int i = 0; i < Count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    string user = Prefix + GenerateRandomString(rnd, UsernameLength, pool);
                    string pass = SelectedCredentialMode switch
                    {
                        CredentialMode.UsernameOnly           => "",
                        CredentialMode.UsernameEqualsPassword => user,
                        CredentialMode.UsernameAndPassword =>
                            PasswordPrefix + GenerateRandomString(rnd, PasswordLength, GetPasswordCharacterPool()),
                        _ => ""
                    };

                    list.Add(new Voucher
                    {
                        Username       = user,
                        Password       = pass,
                        ProfileName    = SelectedProfile!.Name,
                        BatchId        = batchId,
                        Price          = Price,
                        CredentialMode = SelectedCredentialMode,
                        AgentId        = (SelectedAgent != null && SelectedAgent.Id != Guid.Empty) ? SelectedAgent.Id : null
                    });

                    ProgressCurrent = i + 1;
                }

                // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ ط§ظ„ظ…ط±ط­ظ„ط© 2: ط­ظپط¸ ظپظٹ ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
                CurrentPhase = "ط­ظپط¸ ظپظٹ ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ";
                ProgressCurrent = 0;
                ProgressTotal = 1;
                StatusMessage = $"ًں’¾ ط­ظپط¸ {Count} ظƒط±طھ...";

                var result = await _voucherRepository.BulkInsertSafelyAsync(list, token);
                ProgressCurrent = 1;

                string resultSummary = $"âœ… طھظ… ط­ظپط¸ {result.SuccessCount} ظƒط±طھ ظپظٹ ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ";
                if (result.FailedCount > 0)
                    resultSummary += $"\nâڑ ï¸ڈ ظپط´ظ„ {result.FailedCount} ظƒط±طھ (طھظƒط±ط§ط±)";
                ResultMessage = resultSummary;
                HasResult = true;

                Logger.LogInformation("ط§ظ„ظ†طھظٹط¬ط©: ظ†ط¬ط­ {S} | ظپط´ظ„ {F}", result.SuccessCount, result.FailedCount);

                // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ ط§ظ„ظ…ط±ط­ظ„ط© 3: ظ…ط²ط§ظ…ظ†ط© ظ…ط¹ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
                if (AutoSyncAfterGenerate && result.SuccessCount > 0)
                {
                    CurrentPhase = "ظ…ط²ط§ظ…ظ†ط© ظ…ط¹ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ";
                    ProgressCurrent = 0;
                    ProgressTotal = result.SuccessCount;
                    SyncSuccessCount = 0;
                    SyncFailedCount = 0;
                    StatusMessage = $"ًں”„ ظ…ط²ط§ظ…ظ†ط© {result.SuccessCount} ظƒط±طھ...";

                    var progress = new Progress<(int success, int failed, int total)>(update =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressTotal = update.total;
                            SyncSuccessCount = update.success;
                            SyncFailedCount = update.failed;
                            ProgressCurrent = update.success + update.failed;
                        });
                    });

                    var syncResult = await _syncService.ProcessBatchAsync(batchId, progress, token);

                    SyncSuccessCount = syncResult.Success;
                    SyncFailedCount = syncResult.Failed;
                    ProgressCurrent = syncResult.Success + syncResult.Failed;

                    ResultMessage += $"\nًں”„ ظ…ط²ط§ظ…ظ†ط©: {syncResult.Success} ظ†ط¬ط­";
                    if (syncResult.Failed > 0)
                        ResultMessage += $" | {syncResult.Failed} ظپط´ظ„";
                }

                // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ ط§ظ„ظ…ط±ط­ظ„ط© 4: ط·ط¨ط§ط¹ط© طھظ„ظ‚ط§ط¦ظٹط© â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
                if (PrintAfterGenerate && result.SuccessCount > 0)
                {
                    CurrentPhase = "طھط¬ظ‡ظٹط² ط§ظ„ط·ط¨ط§ط¹ط©";
                    ProgressCurrent = 0;
                    ProgressTotal = 1;
                    StatusMessage = "ًں–¨ï¸ڈ طھط¬ظ‡ظٹط² ظ…ظ„ظپ ط§ظ„ط·ط¨ط§ط¹ط©...";
                    await AutoPrintLastBatchAsync(batchId, token);
                    ProgressCurrent = 1;
                }

                CurrentPhase = "ط§ظƒطھظ…ظ„طھ ط§ظ„ط¹ظ…ظ„ظٹط©";
                StatusMessage = $"ًںژ‰ طھظ… â€” {result.SuccessCount} ظƒط±طھ ط¬ط§ظ‡ط²!";
            }
            finally
            {
                StopElapsedTimer();
                IsGenerating = false;
            }

        }, "ًںڑ€ ط¨ط¯ط، ط¥ظ†ط´ط§ط، ط§ظ„ظƒط±ظˆطھ...");
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  طھظˆظ„ظٹط¯ ظƒط±طھ ظˆط§ط­ط¯ (Single Card)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    [RelayCommand]
    private async Task GenerateSingleAsync()
    {
        if (SelectedProfile == null)
        {
            ResultMessage = "â‌Œ ظ„ط§ ظٹظ…ظƒظ† ط¥ظ†ط´ط§ط، ط§ظ„ظƒط±طھ: ظٹط±ط¬ظ‰ ط§ط®طھظٹط§ط± ط¨ط§ظ‚ط© (Profile) ط£ظˆظ„ط§ظ‹.";
            HasResult = true;
            return;
        }

        if (SelectedCredentialMode == CredentialMode.UsernameAndPassword && PasswordLength <= 0)
        {
            ResultMessage = "â‌Œ ظٹط±ط¬ظ‰ طھط­ط¯ظٹط¯ ط·ظˆظ„ طµط§ظ„ط­ ظ„ظƒظ„ظ…ط© ط§ظ„ط³ط±.";
            HasResult = true;
            return;
        }

        await ExecuteBusyAsync(async (token) =>
        {
            HasResult = false;
            IsGenerating = true;
            ResetProgress();
            CurrentPhase = "طھظˆظ„ظٹط¯ ظƒط±طھ ظˆط§ط­ط¯";
            ProgressTotal = 1;
            StartElapsedTimer();
            Logger.LogInformation("âڑ™ï¸ڈ ط¬ط§ط±ظٹ طھظˆظ„ظٹط¯ ظƒط±طھ ظˆط§ط­ط¯ ط¨ظ†ظ…ط· {Mode}...", SelectedCharacterMode);

            var batchId = Guid.NewGuid();

            // ط¥ظ†ط´ط§ط، ط§ظ„ط¨ط§طھط´ ظˆط­ظپط¸ظ‡ ط£ظˆظ„ط§ظ‹
            var newBatch = new Batch
            {
                Id = batchId,
                Name = $"ط¨ط·ط§ظ‚ط© ظ…ظپط±ط¯ط© {DateTime.Now:yyyy-MM-dd HH:mm}",
                ProfileName = SelectedProfile.Name,
                TotalCount = 1
            };
            await _batchRepo.AddAsync(newBatch, token);

            var rnd = new Random();
            string pool = GetCharacterPool();
            string user = Prefix + GenerateRandomString(rnd, UsernameLength, pool);

            // ظƒظ„ظ…ط© ط§ظ„ط³ط± ط­ط³ط¨ ط§ظ„ظ†ظ…ط· ط§ظ„ظ…ط®طھط§ط±
            string pass = SelectedCredentialMode switch
            {
                CredentialMode.UsernameOnly           => "",
                CredentialMode.UsernameEqualsPassword => user,
                CredentialMode.UsernameAndPassword    => PasswordPrefix + GenerateRandomString(rnd, PasswordLength, GetPasswordCharacterPool()),
                _ => ""
            };

            var voucher = new Voucher
            {
                Username       = user,
                Password       = pass,
                ProfileName    = SelectedProfile!.Name,
                BatchId        = batchId,
                Price          = Price,
                CredentialMode = SelectedCredentialMode,
                AgentId        = (SelectedAgent != null && SelectedAgent.Id != Guid.Empty) ? SelectedAgent.Id : null
            };

            var result = await _voucherRepository.BulkInsertSafelyAsync(new[] { voucher }, token);

            if (result.SuccessCount > 0)
            {
                ResultMessage = SelectedCredentialMode switch
                {
                    CredentialMode.UsernameOnly          => $"âœ… طھظ… ط¥ظ†ط´ط§ط، ظƒط±طھ ظˆط§ط­ط¯\nط§ظ„ظ…ط³طھط®ط¯ظ…: {user}\n(ط¨ط¯ظˆظ† ظƒظ„ظ…ط© ط³ط±)",
                    CredentialMode.UsernameEqualsPassword => $"âœ… طھظ… ط¥ظ†ط´ط§ط، ظƒط±طھ ظˆط§ط­ط¯\nط§ظ„ظ…ط³طھط®ط¯ظ…: {user}\nط§ظ„ط±ظ…ط² = ط§ظ„ط§ط³ظ…",
                    _                                    => $"âœ… طھظ… ط¥ظ†ط´ط§ط، ظƒط±طھ ظˆط§ط­ط¯\nط§ظ„ظ…ط³طھط®ط¯ظ…: {user}\nظƒظ„ظ…ط© ط§ظ„ط³ط±: {pass}"
                };

                if (PrintAfterGenerate)
                    await AutoPrintLastBatchAsync(batchId, token);

                if (AutoSyncAfterGenerate)
                {
                    Logger.LogInformation("ط¨ط¯ط، ط§ظ„ظ…ط²ط§ظ…ظ†ط© ط§ظ„طھظ„ظ‚ط§ط¦ظٹط© ظ…ط¹ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ...");
                    
                    var progress = new Progress<(int success, int failed, int total)>(update =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressTotal = update.total;
                            SyncSuccessCount = update.success;
                            SyncFailedCount = update.failed;
                            ProgressCurrent = update.success + update.failed;
                        });
                    });

                    var syncResult = await _syncService.ProcessBatchAsync(batchId, progress, token);
                    if (syncResult.Success > 0)
                        ResultMessage += "\nًں”„ طھظ…طھ ط§ظ„ظ…ط²ط§ظ…ظ†ط© ظ…ط¹ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ ط¨ظ†ط¬ط§ط­.";
                    else if (syncResult.Failed > 0)
                        ResultMessage += "\nâڑ ï¸ڈ ظپط´ظ„طھ ظ…ط²ط§ظ…ظ†ط© ط§ظ„ظƒط±طھ ظ…ط¹ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ.";
                }
            }
            else
            {
                ResultMessage = "â‌Œ ظپط´ظ„ ط¥ظ†ط´ط§ط، ط§ظ„ظƒط±طھ.";
            }
            HasResult = true;
            ProgressCurrent = 1;
            StopElapsedTimer();
            IsGenerating = false;

        }, "ط¬ط§ط±ظٹ ط¥ظ†ط´ط§ط، ط§ظ„ظƒط±طھ...");
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ط§ظ„ط·ط¨ط§ط¹ط© ط§ظ„طھظ„ظ‚ط§ط¦ظٹط© ظ„ط¢ط®ط± ط¯ظپط¹ط©
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private async Task AutoPrintLastBatchAsync(Guid batchId, System.Threading.CancellationToken token)
    {
        try
        {
            var vouchers = await _queryService.GetVouchersByBatchIdAsync(batchId, token);
            if (vouchers.Count == 0) return;

            var settings = new PrintSettingsDto();
            var tid = ResolveEffectivePrintTemplateId();
            if (tid.HasValue)
                settings.CustomTemplateId = tid.Value;

            var pdfResult = await _printService.GeneratePdfAsync(
                new List<VoucherDto>(vouchers), settings, token);

            if (pdfResult.IsSuccess)
            {
                string tempFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"luxcard_batch_{DateTime.Now:HHmmss}.pdf");
                System.IO.File.WriteAllBytes(tempFile, pdfResult.Value);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(tempFile) { UseShellExecute = true });

                ResultMessage += "\nًں–¨ï¸ڈ طھظ… ظپطھط­ ظ…ظ„ظپ ط§ظ„ط·ط¨ط§ط¹ط© طھظ„ظ‚ط§ط¦ظٹط§ظ‹!";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ظپط´ظ„طھ ط§ظ„ط·ط¨ط§ط¹ط© ط§ظ„طھظ„ظ‚ط§ط¦ظٹط©");
            ResultMessage += "\nâڑ ï¸ڈ ظپط´ظ„طھ ط§ظ„ط·ط¨ط§ط¹ط© ط§ظ„طھظ„ظ‚ط§ط¦ظٹط©";
        }
    }

}
