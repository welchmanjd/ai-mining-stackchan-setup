using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using AiStackchanSetup.Steps;
using Microsoft.Win32;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel : BindableBase
{
    private const string ReuseValidationSkippedText = "再利用するため検証対象外";

    private readonly SerialService _serialService = new();
    private readonly FlashService _flashService = new();
    private readonly ApiTestService _apiTestService = new();
    private readonly SupportPackService _supportPackService = new();
    private readonly RetryPolicy _retryPolicy = new();
    private readonly StepTimeouts _timeouts = new();
    private readonly StepController _stepController;
    private readonly StepContext _stepContext;
    private CancellationTokenSource? _stepCts;
    private bool _abortToCompleteRequested;

    private int _totalSteps;
    private int _step = 1;
    private string _stepTitle = "接続";
    private string _stepDescription = "";
    private string _primaryButtonText = "探す";
    private bool _isBusy;
    private string _statusMessage = "";
    private string _errorMessage = "";
    private string _step1Help = "";
    private bool _isAdvancedPanelOpen;
    private bool _isManualPortSelection;

    private SerialPortInfo? _selectedPort;
    private string _firmwarePath = ResolveDefaultFirmwarePath();
    private string _flashBaudText = "921600";
    private bool _flashErase;
    private int _flashMode;
    private string _flashStatus = "";
    private string _firmwareInfoText = "";
    private string _currentFirmwareInfoText = "未取得";
    private string _firmwareCompareMessage = "";
    private string _deviceStatusSummary = "未取得";
    private string _deviceInfoJson = "";
    private string _lastProtocolResponse = "";
    private string _deviceLogPath = "";

    private string _configWifiSsid = "";
    private string _configWifiPassword = "";
    private bool _wifiPasswordStored;
    private bool _reuseWifiPassword;
    private bool _wifiEnabled = true;
    private bool _miningEnabled = true;
    private bool _aiEnabled = true;
    private string _ducoUser = "";
    private string _ducoMinerKey = "";
    private bool _ducoKeyStored;
    private bool _reuseDucoMinerKey;
    private string _configOpenAiKey = "";
    private bool _openAiKeyStored;
    private bool _reuseOpenAiKey;
    private string _configOpenAiModel = "gpt-5-nano";
    private string _configOpenAiInstructions = "あなたはスタックチャンの会話AIです。日本語で短く答えてください。返答は120文字以内。箇条書き禁止。1〜2文。相手が『聞こえる？』等の確認なら、明るく短く返してください。";
    private string _displaySleepSecondsText = "60";
    private bool _captureSerialLogAfterReboot = false;
    private string _speakerVolumeText = "160";
    private int _speakerVolumeRaw = 160;
    private string _shareAcceptedText = "シェア獲得したよ！";
    private string _attentionText = "Hi";
    private string _helloText = "こんにちはマイニングスタックチャンです";
    private string _maskedOpenAiKey = "";
    // Stub: PC保存とAzure連携はv1では未実装（UIのみ）
    private bool _saveToPc;
    private string _azureKey = "";
    private bool _azureKeyStored;
    private bool _reuseAzureKey;
    private string _azureRegion = "";
    private string _azureCustomSubdomain = "";

    private string _apiTestSummary = "未実行";
    private string _azureTestSummary = "未実行";
    private string _deviceTestSummary = "未実行";
    private Brush _apiTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    private Brush _apiTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
    private Brush _azureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    private Brush _azureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));

    private string _lastFlashResult = "";
    private string _lastApiResult = "";
    private string _lastDeviceResult = "";
    private string _lastError = "";
    private string _openAiTestedKey = "";
    private bool _openAiTestedOk;
    private string _azureTestedKey = "";
    private string _azureTestedRegion = "";
    private string _azureTestedSubdomain = "";
    private bool _azureTestedOk;
    private readonly string[] _openAiModelOptions =
    {
        "gpt-5-nano",
        "gpt-5-mini",
        "gpt-4.1-mini",
        "gpt-4o-mini"
    };

    public MainViewModel()
    {
        Ports = new ObservableCollection<SerialPortInfo>();
        _deviceLogPath = LogService.GetLatestDeviceLogPath() ?? "";

        _stepContext = new StepContext(this, _serialService, _flashService, _apiTestService, _supportPackService, _retryPolicy, _timeouts);
        _stepController = new StepController(this, _stepContext, new IStep[]
        {
            new DetectPortsStep(),
            new FlashStep(),
            new FeatureToggleStep(),
            new WifiStep(),
            new DucoStep(),
            new AzureStep(),
            new OpenAiKeyStep(),
            new AdditionalSettingsStep(),
            new RuntimeSettingsStep(),
            new CompleteStep()
        });
        _totalSteps = _stepController.TotalSteps;

        PrimaryCommand = new AsyncRelayCommand(PrimaryAsync, () => !IsBusy);
        AbortToCompleteCommand = new AsyncRelayCommand(AbortToCompleteAsync);
        CloseCommand = new RelayCommand(RequestShutdown);
        CancelCommand = new RelayCommand(CancelCurrent);
        BackCommand = new RelayCommand(GoBack, () => Step > 1 && !IsBusy);
        SkipCommand = new RelayCommand(SkipStep, () => _stepController.CanSkip && !IsBusy);
        AzureTestCommand = new AsyncRelayCommand(TestAzureAsync, () => !IsBusy);
        OpenAiTestCommand = new AsyncRelayCommand(TestOpenAiAsync, () => !IsBusy);
        ValidateApiKeysCommand = new AsyncRelayCommand(ValidateApiKeysAsync, () => !IsBusy && CanRunApiValidation);
        DumpDeviceLogCommand = new AsyncRelayCommand(DumpDeviceLogAsync, () => !IsBusy);
        BrowseFirmwareCommand = new RelayCommand(BrowseFirmware);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        OpenFlashLogCommand = new RelayCommand(OpenFlashLog);
        CreateSupportPackCommand = new AsyncRelayCommand(CreateSupportPackAsync);

        _stepController.SyncStepMetadata();
        ConfigOpenAiModel = _configOpenAiModel;
        FirmwareInfoText = BuildFirmwareInfoText(FirmwarePath);
        RefreshFirmwareComparisonMessage();
    }

    public ObservableCollection<SerialPortInfo> Ports { get; }
    public IReadOnlyList<string> OpenAiModelOptions => _openAiModelOptions;

    public int Step
    {
        get => _step;
        set
        {
            if (SetProperty(ref _step, value))
            {
                _stepController.SyncStepMetadata();
                UpdatePrimaryButtonTextForCurrentStep();
                BackCommand.RaiseCanExecuteChanged();
                SkipCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(StepIndicator));
                RaisePropertyChanged(nameof(StepProgressPercent));
                RaisePropertyChanged(nameof(InputStatusText));
                RaisePropertyChanged(nameof(IsNotFirstStep));
                RaisePropertyChanged(nameof(IsCompleteStep));
                RaisePropertyChanged(nameof(IsNotCompleteStep));
                RaisePropertyChanged(nameof(BackButtonText));
            }
        }
    }

    public string StepTitle
    {
        get => _stepTitle;
        set => SetProperty(ref _stepTitle, value);
    }

    public string StepDescription
    {
        get => _stepDescription;
        set => SetProperty(ref _stepDescription, value);
    }

    public string StepIndicator => $"{Step}/{_totalSteps}";
    public double StepProgressPercent => _totalSteps <= 1 ? 0 : ((double)(Step - 1) / (_totalSteps - 1)) * 100;
    public string BackButtonText => $"前の手順（{_stepController.GetPreviousStepTitle(Step)}）に戻る";
    public string AbortButtonText => "ここまでの設定を保存して終了";
    public string InputStatusText
    {
        get
        {
            var (filled, missing) = CalculateInputStatus();
            return $"入力済み: {filled} / 未入力: {missing}";
        }
    }

    public string PrimaryButtonText
    {
        get => _primaryButtonText;
        set => SetProperty(ref _primaryButtonText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ((AsyncRelayCommand)PrimaryCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)AzureTestCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)OpenAiTestCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ValidateApiKeysCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)DumpDeviceLogCommand).RaiseCanExecuteChanged();
                BackCommand.RaiseCanExecuteChanged();
                SkipCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(CanCancel));
            }
        }
    }

    public bool CanCancel => IsBusy && _stepCts != null;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string Step1Help
    {
        get => _step1Help;
        set => SetProperty(ref _step1Help, value);
    }

    public bool IsAdvancedPanelOpen
    {
        get => _isAdvancedPanelOpen;
        set => SetProperty(ref _isAdvancedPanelOpen, value);
    }

    public bool IsManualPortSelection
    {
        get => _isManualPortSelection;
        set => SetProperty(ref _isManualPortSelection, value);
    }

    public SerialPortInfo? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string FirmwarePath
    {
        get => _firmwarePath;
        set
        {
            if (SetProperty(ref _firmwarePath, value))
            {
                FirmwareInfoText = BuildFirmwareInfoText(value);
                RefreshFirmwareComparisonMessage();
            }
        }
    }

    public string FirmwareInfoText
    {
        get => _firmwareInfoText;
        set => SetProperty(ref _firmwareInfoText, value);
    }

    public string CurrentFirmwareInfoText
    {
        get => _currentFirmwareInfoText;
        set => SetProperty(ref _currentFirmwareInfoText, value);
    }

    public string FirmwareCompareMessage
    {
        get => _firmwareCompareMessage;
        set => SetProperty(ref _firmwareCompareMessage, value);
    }

    public string FlashBaud
    {
        get => _flashBaudText;
        set => SetProperty(ref _flashBaudText, value);
    }

    public bool FlashErase
    {
        get => _flashErase;
        set => SetProperty(ref _flashErase, value);
    }

    public int FlashMode
    {
        get => _flashMode;
        set
        {
            if (SetProperty(ref _flashMode, value))
            {
                RaisePropertyChanged(nameof(FlashModeOverwrite));
                RaisePropertyChanged(nameof(FlashModeErase));
                RaisePropertyChanged(nameof(FlashModeSkip));
                UpdatePrimaryButtonTextForCurrentStep();
            }
        }
    }

    public bool FlashModeOverwrite
    {
        get => FlashMode == 0;
        set
        {
            if (value)
            {
                FlashMode = 0;
            }
        }
    }

    public bool FlashModeErase
    {
        get => FlashMode == 1;
        set
        {
            if (value)
            {
                FlashMode = 1;
            }
        }
    }

    public bool FlashModeSkip
    {
        get => FlashMode == 2;
        set
        {
            if (value)
            {
                FlashMode = 2;
            }
        }
    }

    public string FlashStatus
    {
        get => _flashStatus;
        set => SetProperty(ref _flashStatus, value);
    }

    public string DeviceStatusSummary
    {
        get => _deviceStatusSummary;
        set => SetProperty(ref _deviceStatusSummary, value);
    }

    public string DeviceInfoJson
    {
        get => _deviceInfoJson;
        set => SetProperty(ref _deviceInfoJson, value);
    }

    public string LastProtocolResponse
    {
        get => _lastProtocolResponse;
        set => SetProperty(ref _lastProtocolResponse, value);
    }

    public string FlashLogPath => LogService.FlashLogPath;
    public string DeviceLogPath
    {
        get => _deviceLogPath;
        set => SetProperty(ref _deviceLogPath, value);
    }
    public string LogDirectory => LogService.LogDirectory;

    public bool IsNotFirstStep => Step > 1;
    public bool IsCompleteStep => Step == _stepController.LastStepIndex;
    public bool IsNotCompleteStep => !IsCompleteStep;

    public string ConfigWifiSsid
    {
        get => _configWifiSsid;
        set
        {
            if (SetProperty(ref _configWifiSsid, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string ConfigWifiPassword
    {
        get => _configWifiPassword;
        set
        {
            if (SetProperty(ref _configWifiPassword, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool WifiPasswordStored
    {
        get => _wifiPasswordStored;
        set
        {
            if (SetProperty(ref _wifiPasswordStored, value))
            {
                if (!_wifiPasswordStored)
                {
                    ReuseWifiPassword = false;
                }

                RaisePropertyChanged(nameof(CanReuseWifiPassword));
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool ReuseWifiPassword
    {
        get => _reuseWifiPassword;
        set
        {
            if (SetProperty(ref _reuseWifiPassword, value))
            {
                RaisePropertyChanged(nameof(CanEditWifiPassword));
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool CanReuseWifiPassword => WifiPasswordStored;
    public bool CanEditWifiPassword => !ReuseWifiPassword;

    public bool WifiEnabled
    {
        get => _wifiEnabled;
        set
        {
            if (SetProperty(ref _wifiEnabled, value))
            {
                if (!_wifiEnabled)
                {
                    MiningEnabled = false;
                    AiEnabled = false;
                }

                RaisePropertyChanged(nameof(FeatureToggleChildrenEnabled));
                RaiseApiValidationStateChanged();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool MiningEnabled
    {
        get => _miningEnabled;
        set
        {
            if (SetProperty(ref _miningEnabled, value))
            {
                RaisePropertyChanged(nameof(IsMiningDisabled));
                RaisePropertyChanged(nameof(MiningModeSummary));
                RaiseApiValidationStateChanged();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool AiEnabled
    {
        get => _aiEnabled;
        set
        {
            if (SetProperty(ref _aiEnabled, value))
            {
                RaiseApiValidationStateChanged();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool FeatureToggleChildrenEnabled => WifiEnabled;
    public bool IsMiningDisabled => !MiningEnabled;
    public string MiningModeSummary => MiningEnabled ? "マイニング機能はONにします" : "マイニング機能はOFFにします";

    public string DucoUser
    {
        get => _ducoUser;
        set
        {
            if (SetProperty(ref _ducoUser, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string DucoMinerKey
    {
        get => _ducoMinerKey;
        set
        {
            if (SetProperty(ref _ducoMinerKey, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool DucoKeyStored
    {
        get => _ducoKeyStored;
        set
        {
            if (SetProperty(ref _ducoKeyStored, value))
            {
                if (!_ducoKeyStored)
                {
                    ReuseDucoMinerKey = false;
                }

                RaisePropertyChanged(nameof(CanReuseDucoMinerKey));
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool ReuseDucoMinerKey
    {
        get => _reuseDucoMinerKey;
        set
        {
            if (SetProperty(ref _reuseDucoMinerKey, value))
            {
                RaisePropertyChanged(nameof(CanEditDucoMinerKey));
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool CanReuseDucoMinerKey => DucoKeyStored;
    public bool CanEditDucoMinerKey => !ReuseDucoMinerKey;

    public string ConfigOpenAiKey
    {
        get => _configOpenAiKey;
        set
        {
            if (SetProperty(ref _configOpenAiKey, value))
            {
                MaskedOpenAiKey = DeviceConfig.Mask(value);
                _openAiTestedKey = "";
                _openAiTestedOk = false;
                ApiTestSummary = "未実行";
                ApiTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                ApiTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool OpenAiKeyStored
    {
        get => _openAiKeyStored;
        set
        {
            if (SetProperty(ref _openAiKeyStored, value))
            {
                if (!_openAiKeyStored)
                {
                    ReuseOpenAiKey = false;
                }

                RaisePropertyChanged(nameof(CanReuseOpenAiKey));
                RaiseApiValidationStateChanged();
                RefreshApiValidationSummaries();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool ReuseOpenAiKey
    {
        get => _reuseOpenAiKey;
        set
        {
            if (SetProperty(ref _reuseOpenAiKey, value))
            {
                _openAiTestedKey = "";
                _openAiTestedOk = false;
                RaisePropertyChanged(nameof(CanEditOpenAiKey));
                RaiseApiValidationStateChanged();
                RefreshApiValidationSummaries();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool CanReuseOpenAiKey => OpenAiKeyStored;
    public bool CanEditOpenAiKey => !ReuseOpenAiKey;

    public string ConfigOpenAiModel
    {
        get => _configOpenAiModel;
        set => SetProperty(ref _configOpenAiModel, NormalizeOpenAiModel(value));
    }

    public string ConfigOpenAiInstructions
    {
        get => _configOpenAiInstructions;
        set => SetProperty(ref _configOpenAiInstructions, value);
    }

    public string DisplaySleepSecondsText
    {
        get => _displaySleepSecondsText;
        set => SetProperty(ref _displaySleepSecondsText, value);
    }

    public bool CaptureSerialLogAfterReboot
    {
        get => _captureSerialLogAfterReboot;
        set => SetProperty(ref _captureSerialLogAfterReboot, value);
    }

    public string SpeakerVolumeText
    {
        get => _speakerVolumeText;
        set
        {
            if (SetProperty(ref _speakerVolumeText, value))
            {
                if (int.TryParse(value, out var raw))
                {
                    raw = Math.Clamp(raw, 0, 255);
                    if (_speakerVolumeRaw != raw)
                    {
                        _speakerVolumeRaw = raw;
                        RaisePropertyChanged(nameof(SpeakerVolumeRaw));
                    }
                }
            }
        }
    }

    public int SpeakerVolumeRaw
    {
        get => _speakerVolumeRaw;
        set
        {
            var clamped = Math.Clamp(value, 0, 255);
            if (SetProperty(ref _speakerVolumeRaw, clamped))
            {
                var rawText = _speakerVolumeRaw.ToString();
                if (_speakerVolumeText != rawText)
                {
                    _speakerVolumeText = rawText;
                    RaisePropertyChanged(nameof(SpeakerVolumeText));
                }
            }
        }
    }

    public string ShareAcceptedText
    {
        get => _shareAcceptedText;
        set => SetProperty(ref _shareAcceptedText, value);
    }

    public string AttentionText
    {
        get => _attentionText;
        set => SetProperty(ref _attentionText, value);
    }

    public string HelloText
    {
        get => _helloText;
        set => SetProperty(ref _helloText, value);
    }

    public string MaskedOpenAiKey
    {
        get => _maskedOpenAiKey;
        set
        {
            if (SetProperty(ref _maskedOpenAiKey, value))
            {
                RaisePropertyChanged(nameof(HasMaskedOpenAiKey));
            }
        }
    }

    public bool HasMaskedOpenAiKey => !string.IsNullOrWhiteSpace(MaskedOpenAiKey);

    public bool SaveToPc
    {
        get => _saveToPc;
        set => SetProperty(ref _saveToPc, value);
    }

    public string AzureKey
    {
        get => _azureKey;
        set
        {
            if (SetProperty(ref _azureKey, value))
            {
                AzureKeyStored = !string.IsNullOrWhiteSpace(value);
                ResetAzureTestState();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool AzureKeyStored
    {
        get => _azureKeyStored;
        set
        {
            if (SetProperty(ref _azureKeyStored, value))
            {
                if (!_azureKeyStored)
                {
                    ReuseAzureKey = false;
                }

                RaisePropertyChanged(nameof(CanReuseAzureKey));
                RaiseApiValidationStateChanged();
                RefreshApiValidationSummaries();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool ReuseAzureKey
    {
        get => _reuseAzureKey;
        set
        {
            if (SetProperty(ref _reuseAzureKey, value))
            {
                ResetAzureTestState();
                RaisePropertyChanged(nameof(CanEditAzureKey));
                RaiseApiValidationStateChanged();
                RefreshApiValidationSummaries();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool CanReuseAzureKey => AzureKeyStored;
    public bool CanEditAzureKey => !ReuseAzureKey;
    public bool CanRunApiValidation => !IsUsingStoredApiKeys;
    public string ApiValidationGuideText => IsUsingStoredApiKeys
        ? "M5StackCore2内の情報を利用するためスキップします。"
        : "必要に応じてここでAPIキーの有効確認を実行できます。";

    private bool IsUsingStoredApiKeys =>
        (OpenAiKeyStored && ReuseOpenAiKey) || (AzureKeyStored && ReuseAzureKey);

    public string AzureRegion
    {
        get => _azureRegion;
        set
        {
            if (SetProperty(ref _azureRegion, value))
            {
                ResetAzureTestState();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string AzureCustomSubdomain
    {
        get => _azureCustomSubdomain;
        set
        {
            if (SetProperty(ref _azureCustomSubdomain, value))
            {
                ResetAzureTestState();
            }
        }
    }

    public string ApiTestSummary
    {
        get => _apiTestSummary;
        set => SetProperty(ref _apiTestSummary, value);
    }

    public Brush ApiTestSummaryBrush
    {
        get => _apiTestSummaryBrush;
        set => SetProperty(ref _apiTestSummaryBrush, value);
    }

    public Brush ApiTestSummaryBackground
    {
        get => _apiTestSummaryBackground;
        set => SetProperty(ref _apiTestSummaryBackground, value);
    }

    public string AzureTestSummary
    {
        get => _azureTestSummary;
        set => SetProperty(ref _azureTestSummary, value);
    }

    public Brush AzureTestSummaryBrush
    {
        get => _azureTestSummaryBrush;
        set => SetProperty(ref _azureTestSummaryBrush, value);
    }

    public Brush AzureTestSummaryBackground
    {
        get => _azureTestSummaryBackground;
        set => SetProperty(ref _azureTestSummaryBackground, value);
    }

    public string DeviceTestSummary
    {
        get => _deviceTestSummary;
        set => SetProperty(ref _deviceTestSummary, value);
    }

    public string LastFlashResult
    {
        get => _lastFlashResult;
        set => _lastFlashResult = value;
    }

    public string LastApiResult
    {
        get => _lastApiResult;
        set => _lastApiResult = value;
    }

    public string LastDeviceResult
    {
        get => _lastDeviceResult;
        set => _lastDeviceResult = value;
    }

    public string LastError
    {
        get => _lastError;
        set => _lastError = value;
    }

    public RelayCommand CloseCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand SkipCommand { get; }
    public AsyncRelayCommand PrimaryCommand { get; }
    public AsyncRelayCommand AbortToCompleteCommand { get; }
    public AsyncRelayCommand AzureTestCommand { get; }
    public AsyncRelayCommand OpenAiTestCommand { get; }
    public AsyncRelayCommand ValidateApiKeysCommand { get; }
    public AsyncRelayCommand DumpDeviceLogCommand { get; }
    public RelayCommand BrowseFirmwareCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand OpenFlashLogCommand { get; }
    public AsyncRelayCommand CreateSupportPackCommand { get; }

    private static string ResolveDefaultFirmwarePath()
    {
        var exeDir = GetExecutableDirectory();
        if (string.IsNullOrWhiteSpace(exeDir))
        {
            return string.Empty;
        }

        // app の1つ上（配布zipのルート）を想定
        string? rootDir = null;
        try
        {
            rootDir = Directory.GetParent(exeDir)?.FullName;
        }
        catch
        {
            rootDir = null;
        }

        // ルートの firmware のみを既定探索対象にする
        var candidates = new[]
        {
            rootDir != null ? Path.Combine(rootDir, "firmware", "stackchan_core2_public.bin") : null,
        };

        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
            {
                return c!;
            }
        }

        // 保険：名前が変わっても "_public" を含む .bin を拾う
        var searchDirs = new[]
        {
            rootDir != null ? Path.Combine(rootDir, "firmware") : null,
        };

        foreach (var d in searchDirs)
        {
            if (string.IsNullOrWhiteSpace(d) || !Directory.Exists(d))
            {
                continue;
            }

            try
            {
                var f = Directory.EnumerateFiles(d, "*.bin", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(p => Path.GetFileName(p).Contains("_public", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(f) && File.Exists(f))
                {
                    return f!;
                }
            }
            catch
            {
                // ignore
            }
        }

        return string.Empty;
    }

    private static string? GetExecutableDirectory()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        return Path.GetDirectoryName(exePath);
    }

    public void UpdateCurrentFirmwareInfo(DeviceInfo? info)
    {
        if (info == null)
        {
            CurrentFirmwareInfoText = "未取得";
            RefreshFirmwareComparisonMessage();
            return;
        }

        var app = string.IsNullOrWhiteSpace(info.App) ? "unknown" : info.App;
        var ver = string.IsNullOrWhiteSpace(info.Ver) ? "unknown" : info.Ver;
        var build = string.IsNullOrWhiteSpace(info.BuildId) ? "unknown" : info.BuildId;
        CurrentFirmwareInfoText = $"app={app} / ver={ver} / build={build}";
        RefreshFirmwareComparisonMessage();
    }

    private void RefreshFirmwareComparisonMessage()
    {
        var manifest = FirmwareManifest.FromFirmwarePath(FirmwarePath);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Ver))
        {
            FirmwareCompareMessage = "";
            return;
        }

        var currentVer = ExtractToken(CurrentFirmwareInfoText, "ver=");
        if (string.IsNullOrWhiteSpace(currentVer) || currentVer.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            FirmwareCompareMessage = "";
            return;
        }

        if (string.Equals(currentVer, manifest.Ver, StringComparison.OrdinalIgnoreCase))
        {
            FirmwareCompareMessage = "同じバージョンのファームウェアが既に書き込まれています。必要なら上書きできます。";
            return;
        }

        FirmwareCompareMessage = $"現在 ver={currentVer} / 書込 ver={manifest.Ver}";
    }

    private static string ExtractToken(string text, string prefix)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var i = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            return string.Empty;
        }

        var start = i + prefix.Length;
        var end = text.IndexOf(" / ", start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = text.Length;
        }

        return text.Substring(start, end - start).Trim();
    }

    private static string BuildFirmwareInfoText(string path)
    {
        var info = FirmwareInfo.FromFile(path);
        if (info == null)
        {
            return "未検出";
        }
        var manifest = FirmwareManifest.FromFirmwarePath(path);
        if (manifest == null)
        {
            return $"size={info.Size} bytes / mtime={info.LastWriteTime:yyyy-MM-dd HH:mm:ss} / sha256={info.Sha256[..12]}";
        }

        var ver = string.IsNullOrWhiteSpace(manifest.Ver) ? "unknown" : manifest.Ver;
        var build = string.IsNullOrWhiteSpace(manifest.BuildId) ? "unknown" : manifest.BuildId;
        return $"ver={ver} / build={build} / size={info.Size} bytes / mtime={info.LastWriteTime:yyyy-MM-dd HH:mm:ss} / sha256={info.Sha256[..12]}";
    }

}

