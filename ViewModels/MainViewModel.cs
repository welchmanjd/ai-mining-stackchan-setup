using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

public class MainViewModel : BindableBase
{
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
    private string _speakerVolumeText = "160";
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
        FirmwareInfoText = BuildFirmwareInfoText(FirmwarePath);
        RefreshFirmwareComparisonMessage();
    }

    public ObservableCollection<SerialPortInfo> Ports { get; }

    public int Step
    {
        get => _step;
        set
        {
            if (SetProperty(ref _step, value))
            {
                _stepController.SyncStepMetadata();
                BackCommand.RaiseCanExecuteChanged();
                SkipCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(StepIndicator));
                RaisePropertyChanged(nameof(IsCompleteStep));
                RaisePropertyChanged(nameof(IsNotCompleteStep));
            }
        }
    }

    public string StepTitle
    {
        get => _stepTitle;
        set => SetProperty(ref _stepTitle, value);
    }

    public string StepIndicator => $"{Step}/{_totalSteps}";

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
        set => SetProperty(ref _selectedPort, value);
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

    public bool IsCompleteStep => Step == 9;
    public bool IsNotCompleteStep => Step != 9;

    public string ConfigWifiSsid
    {
        get => _configWifiSsid;
        set => SetProperty(ref _configWifiSsid, value);
    }

    public string ConfigWifiPassword
    {
        get => _configWifiPassword;
        set => SetProperty(ref _configWifiPassword, value);
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
            }
        }
    }

    public bool FeatureToggleChildrenEnabled => WifiEnabled;
    public bool IsMiningDisabled => !MiningEnabled;
    public string MiningModeSummary => MiningEnabled ? "マイニング機能はONにします" : "マイニング機能はOFFにします";

    public string DucoUser
    {
        get => _ducoUser;
        set => SetProperty(ref _ducoUser, value);
    }

    public string DucoMinerKey
    {
        get => _ducoMinerKey;
        set => SetProperty(ref _ducoMinerKey, value);
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
            }
        }
    }

    public bool CanReuseOpenAiKey => OpenAiKeyStored;
    public bool CanEditOpenAiKey => !ReuseOpenAiKey;

    public string ConfigOpenAiModel
    {
        get => _configOpenAiModel;
        set => SetProperty(ref _configOpenAiModel, value);
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

    public string SpeakerVolumeText
    {
        get => _speakerVolumeText;
        set => SetProperty(ref _speakerVolumeText, value);
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
        set => SetProperty(ref _maskedOpenAiKey, value);
    }

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

    private async Task PrimaryAsync()
    {
        ErrorMessage = "";
        _stepCts?.Dispose();
        _stepCts = new CancellationTokenSource();
        RaisePropertyChanged(nameof(CanCancel));

        StepResult result;
        try
        {
            result = await _stepController.ExecuteCurrentAsync(_stepCts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Step cancelled by user");
            result = StepResult.Cancelled();
        }
        finally
        {
            _stepCts.Dispose();
            _stepCts = null;
            RaisePropertyChanged(nameof(CanCancel));
        }

        if (result.Status == StepStatus.Success || result.Status == StepStatus.Skipped)
        {
            _stepController.MoveNext();
            AutoSkipOptionalSteps();
            _stepController.SyncStepMetadata();
            if (_abortToCompleteRequested)
            {
                await ExecuteAbortToCompleteAsync();
            }
            return;
        }

        if (result.Status == StepStatus.Cancelled)
        {
            StatusMessage = "中止しました";
            if (_abortToCompleteRequested)
            {
                await ExecuteAbortToCompleteAsync();
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ErrorMessage = result.ErrorMessage;
        }

        if (result.CanRetry)
        {
            PrimaryButtonText = "再試行";
        }

        if (_abortToCompleteRequested)
        {
            await ExecuteAbortToCompleteAsync();
        }
    }

    private void AutoSkipOptionalSteps()
    {
        while (true)
        {
            if (Step == 5 && !WifiEnabled)
            {
                Step = 6;
                continue;
            }

            if (Step == 6 && !(WifiEnabled && (MiningEnabled || AiEnabled)))
            {
                Step = 7;
                continue;
            }

            if (Step == 7 && (!WifiEnabled || !AiEnabled))
            {
                Step = 8;
                continue;
            }

            break;
        }
    }
    private void CancelCurrent()
    {
        _stepCts?.Cancel();
    }

    public void PrepareForShutdown()
    {
        try
        {
            CancelCurrent();
            _serialService.Close();
            _flashService.KillActiveProcesses();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PrepareForShutdown failed");
        }
    }

    private void RequestShutdown()
    {
        PrepareForShutdown();
        Application.Current.Shutdown();
    }

    private async Task AbortToCompleteAsync()
    {
        _abortToCompleteRequested = true;
        if (IsBusy)
        {
            CancelCurrent();
            return;
        }

        await ExecuteAbortToCompleteAsync();
    }

    private async Task ExecuteAbortToCompleteAsync()
    {
        _abortToCompleteRequested = false;
        try
        {
            await CreateSupportPackAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Abort to complete failed");
        }

        Step = 8;
        _stepController.SyncStepMetadata();
        StatusMessage = "完了画面に移動しました";
    }

    private void GoBack()
    {
        _stepController.MovePrevious();
        _stepController.SyncStepMetadata();
        BackCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
    }

    private void SkipStep()
    {
        _stepController.Skip();
        _stepController.SyncStepMetadata();
        BackCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
    }

    private async Task RunTestsAsync()
    {
        if (SelectedPort == null)
        {
            ErrorMessage = "COMポートが未選択です";
            return;
        }

        IsBusy = true;
        StatusMessage = "テスト中...";

        try
        {
            var openAiOk = _openAiTestedOk && _openAiTestedKey == ConfigOpenAiKey;
            ApiTestResult? apiResult = null;
            if (!openAiOk)
            {
                apiResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                    ct => _apiTestService.TestAsync(ConfigOpenAiKey, ct),
                    TimeSpan.FromSeconds(25),
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    CancellationToken.None);
                ApiTestSummary = apiResult.Success ? "利用可能です" : $"利用できません: {apiResult.Message}";
                ApiTestSummaryBrush = apiResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                    : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                ApiTestSummaryBackground = apiResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
                    : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                _lastApiResult = apiResult.Success ? "success" : apiResult.Message;
                if (apiResult.Success)
                {
                    _openAiTestedKey = ConfigOpenAiKey;
                    _openAiTestedOk = true;
                    openAiOk = true;
                }
            }
            else
            {
                ApiTestSummary = "利用可能です (確認済み)";
                ApiTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                ApiTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3));
            }

            var azureOk = _azureTestedOk &&
                          _azureTestedKey == AzureKey &&
                          _azureTestedRegion == AzureRegion &&
                          _azureTestedSubdomain == AzureCustomSubdomain;
            ApiTestResult? azureResult = null;
            if (string.IsNullOrWhiteSpace(AzureKey))
            {
                AzureTestSummary = "未入力";
                AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                azureOk = true;
            }
            else if (!azureOk)
            {
                azureResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                    ct => _apiTestService.TestAzureSpeechAsync(AzureKey, AzureRegion, AzureCustomSubdomain, ct),
                    TimeSpan.FromSeconds(25),
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    CancellationToken.None);
                if (azureResult.Message == "未入力")
                {
                    AzureTestSummary = "未入力";
                    AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                    AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                    azureOk = true;
                }
                else
                {
                    AzureTestSummary = azureResult.Success ? "利用可能です" : $"利用できません: {azureResult.Message}";
                    AzureTestSummaryBrush = azureResult.Success
                        ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                        : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    AzureTestSummaryBackground = azureResult.Success
                        ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
                        : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                }

                if (azureResult.Success)
                {
                    _azureTestedKey = AzureKey;
                    _azureTestedRegion = AzureRegion;
                    _azureTestedSubdomain = AzureCustomSubdomain;
                    _azureTestedOk = true;
                    azureOk = true;
                }
            }
            else
            {
                AzureTestSummary = "利用可能です (確認済み)";
                AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3));
            }

            // Stub: 端末側TEST_RUN未実装の場合はSkippedとして扱う
            var deviceResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                ct => _serialService.RunTestAsync(SelectedPort.PortName, ct),
                TimeSpan.FromSeconds(30),
                maxAttempts: 2,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                CancellationToken.None);
            if (deviceResult.Skipped)
            {
                DeviceTestSummary = "未実装の可能性";
                _lastDeviceResult = "skipped";
            }
            else
            {
                DeviceTestSummary = deviceResult.Success ? "OK" : deviceResult.Message;
                _lastDeviceResult = deviceResult.Success ? "success" : deviceResult.Message;
            }

            if (openAiOk && azureOk && (deviceResult.Success || deviceResult.Skipped))
            {
                StatusMessage = "テスト完了";
                Step = 8;
            }
            else
            {
                ErrorMessage = "テストに失敗しました。再試行してください。";
                PrimaryButtonText = "再試行";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Test failed");
            ErrorMessage = "テストに失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestAzureAsync()
    {
        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = "Azureキーを確認中...";

        try
        {
            var azureResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                ct => _apiTestService.TestAzureSpeechAsync(AzureKey, AzureRegion, AzureCustomSubdomain, ct),
                TimeSpan.FromSeconds(25),
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                CancellationToken.None);
            if (azureResult.Message == "未入力")
            {
                AzureTestSummary = "未入力";
                AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            }
            else
            {
                AzureTestSummary = azureResult.Success ? "利用可能です" : $"利用できません: {azureResult.Message}";
                AzureTestSummaryBrush = azureResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                    : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                AzureTestSummaryBackground = azureResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
                    : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
            }

            if (azureResult.Success)
            {
                _azureTestedKey = AzureKey;
                _azureTestedRegion = AzureRegion;
                _azureTestedSubdomain = AzureCustomSubdomain;
                _azureTestedOk = true;
            }

            StatusMessage = azureResult.Success ? "Azureキー: 利用可能です" : $"Azureキー: 利用できません ({azureResult.Message})";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Azure test failed");
            ErrorMessage = "Azureキー確認に失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ValidateApiKeysAsync()
    {
        if (!CanRunApiValidation)
        {
            StatusMessage = "M5StackCore2内の情報を利用するため、APIキー確認をスキップしました。";
            return;
        }

        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = "APIキーを確認中...";

        try
        {
            if (WifiEnabled && AiEnabled)
            {
                var apiResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                    ct => _apiTestService.TestAsync(ConfigOpenAiKey, ct),
                    TimeSpan.FromSeconds(25),
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    CancellationToken.None);

                ApiTestSummary = apiResult.Success ? "利用可能です" : $"利用できません: {apiResult.Message}";
                ApiTestSummaryBrush = apiResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                    : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                ApiTestSummaryBackground = apiResult.Success
                    ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
                    : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                _lastApiResult = apiResult.Success ? "success" : apiResult.Message;
                if (apiResult.Success)
                {
                    _openAiTestedKey = ConfigOpenAiKey;
                    _openAiTestedOk = true;
                }
            }
            else
            {
                ApiTestSummary = "対象外 (Wi-Fi/AIがOFF)";
                ApiTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                ApiTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            }

            if (WifiEnabled && (MiningEnabled || AiEnabled))
            {
                var azureResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                    ct => _apiTestService.TestAzureSpeechAsync(AzureKey, AzureRegion, AzureCustomSubdomain, ct),
                    TimeSpan.FromSeconds(25),
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    CancellationToken.None);

                if (azureResult.Message == "未入力")
                {
                    AzureTestSummary = "未入力";
                    AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                    AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                }
                else
                {
                    AzureTestSummary = azureResult.Success ? "利用可能です" : $"利用できません: {azureResult.Message}";
                    AzureTestSummaryBrush = azureResult.Success
                        ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                        : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    AzureTestSummaryBackground = azureResult.Success
                        ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
                        : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                }

                if (azureResult.Success)
                {
                    _azureTestedKey = AzureKey;
                    _azureTestedRegion = AzureRegion;
                    _azureTestedSubdomain = AzureCustomSubdomain;
                    _azureTestedOk = true;
                }
            }
            else
            {
                AzureTestSummary = "対象外 (Wi-Fi/機能がOFF)";
                AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            }

            StatusMessage = "APIキー確認が完了しました";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Validate API keys failed");
            ErrorMessage = "APIキー確認に失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseApiValidationStateChanged()
    {
        RaisePropertyChanged(nameof(CanRunApiValidation));
        RaisePropertyChanged(nameof(ApiValidationGuideText));
        ((AsyncRelayCommand)ValidateApiKeysCommand).RaiseCanExecuteChanged();
    }

    private async Task TestOpenAiAsync()
    {
        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = "OpenAIキーを確認中...";

        try
        {
            var apiResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                ct => _apiTestService.TestAsync(ConfigOpenAiKey, ct),
                TimeSpan.FromSeconds(25),
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                CancellationToken.None);
            ApiTestSummary = apiResult.Success ? "利用可能です" : $"利用できません: {apiResult.Message}";
            ApiTestSummaryBrush = apiResult.Success
                ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            ApiTestSummaryBackground = apiResult.Success
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
                : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
            _lastApiResult = apiResult.Success ? "success" : apiResult.Message;
            if (apiResult.Success)
            {
                _openAiTestedKey = ConfigOpenAiKey;
                _openAiTestedOk = true;
            }
            StatusMessage = apiResult.Success ? "OpenAIキー: 利用可能です" : $"OpenAIキー: 利用できません ({apiResult.Message})";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenAI test failed");
            ErrorMessage = "OpenAIキー確認に失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DumpDeviceLogAsync()
    {
        if (SelectedPort == null)
        {
            ErrorMessage = "COMポートが未選択です";
            return;
        }

        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = "デバイスログを取得中...";

        try
        {
            var deviceLog = await _serialService.DumpLogAsync(SelectedPort.PortName);
            if (string.IsNullOrWhiteSpace(deviceLog))
            {
                ErrorMessage = "デバイスログが空でした";
                return;
            }

            var config = BuildDeviceConfig();
            var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
            var path = LogService.CreateDeviceLogPath();
            await File.WriteAllTextAsync(path, sanitized);
            DeviceLogPath = path;
            StatusMessage = $"デバイスログを保存しました: {path}";
        }
        catch (SerialCommandException ex)
        {
            Log.Warning(ex, "Device log dump not supported");
            ErrorMessage = "デバイスがLOG_DUMPに対応していません";
            _lastError = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Device log dump failed");
            ErrorMessage = "デバイスログ取得に失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetAzureTestState()
    {
        _azureTestedKey = "";
        _azureTestedRegion = "";
        _azureTestedSubdomain = "";
        _azureTestedOk = false;
        AzureTestSummary = "未実行";
        AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
    }

    private void BrowseFirmware()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "BINファイル (*.bin)|*.bin|すべてのファイル (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            FirmwarePath = dialog.FileName;
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogService.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogService.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Open log folder failed");
        }
    }

    private void OpenFlashLog()
    {
        try
        {
            Directory.CreateDirectory(LogService.LogDirectory);
            if (File.Exists(LogService.FlashLogPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogService.FlashLogPath,
                    UseShellExecute = true
                });
            }
            else
            {
                OpenLogFolder();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Open flash log failed");
        }
    }

    public DeviceConfig BuildDeviceConfig()
    {
        var displaySleepSeconds = 60;
        if (!string.IsNullOrWhiteSpace(DisplaySleepSecondsText) &&
            int.TryParse(DisplaySleepSecondsText, out var parsedSleep) &&
            parsedSleep >= 0)
        {
            displaySleepSeconds = parsedSleep;
        }

        var speakerVolume = 160;
        if (!string.IsNullOrWhiteSpace(SpeakerVolumeText) &&
            int.TryParse(SpeakerVolumeText, out var parsedVolume) &&
            parsedVolume >= 0 && parsedVolume <= 255)
        {
            speakerVolume = parsedVolume;
        }

        var wifiEnabled = WifiEnabled;
        var miningEnabled = wifiEnabled && MiningEnabled;
        var aiEnabled = wifiEnabled && AiEnabled;

        return new DeviceConfig
        {
            WifiEnabled = wifiEnabled,
            MiningEnabled = miningEnabled,
            AiEnabled = aiEnabled,
            WifiSsid = ConfigWifiSsid,
            WifiPassword = (ReuseWifiPassword && WifiPasswordStored) ? "" : ConfigWifiPassword,
            DucoUser = DucoUser,
            DucoMinerKey = (ReuseDucoMinerKey && DucoKeyStored) ? "" : DucoMinerKey,
            OpenAiKey = (ReuseOpenAiKey && OpenAiKeyStored) ? "" : ConfigOpenAiKey,
            OpenAiModel = ConfigOpenAiModel,
            OpenAiInstructions = ConfigOpenAiInstructions,
            AzureKey = (ReuseAzureKey && AzureKeyStored) ? "" : AzureKey,
            AzureRegion = AzureRegion,
            AzureCustomSubdomain = AzureCustomSubdomain,
            DisplaySleepSeconds = displaySleepSeconds,
            SpeakerVolume = speakerVolume,
            ShareAcceptedText = ShareAcceptedText,
            AttentionText = AttentionText,
            HelloText = HelloText
        };
    }

    public void ApplyConfigSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (TryGetString(root, "wifi_ssid", out var ssid) && !string.IsNullOrWhiteSpace(ssid))
            {
                ConfigWifiSsid = ssid;
            }
            if (TryGetBool(root, "wifi_pass_set", out var wifiPassSet))
            {
                WifiPasswordStored = wifiPassSet;
                ReuseWifiPassword = wifiPassSet;
            }

            if (TryGetBool(root, "wifi_enabled", out var wifiEnabled))
            {
                WifiEnabled = wifiEnabled;
            }
            if (TryGetBool(root, "mining_enabled", out var miningEnabled))
            {
                MiningEnabled = miningEnabled;
            }
            if (TryGetBool(root, "ai_enabled", out var aiEnabled))
            {
                AiEnabled = aiEnabled;
            }

            if (TryGetString(root, "duco_user", out var ducoUser) && !string.IsNullOrWhiteSpace(ducoUser))
            {
                DucoUser = ducoUser;
            }
            if (TryGetBool(root, "duco_key_set", out var ducoKeySet))
            {
                DucoKeyStored = ducoKeySet;
                ReuseDucoMinerKey = ducoKeySet;
            }

            if (TryGetString(root, "az_region", out var azRegion) && !string.IsNullOrWhiteSpace(azRegion))
            {
                AzureRegion = azRegion;
            }
            if (TryGetString(root, "az_custom_subdomain", out var azSubdomain) && !string.IsNullOrWhiteSpace(azSubdomain))
            {
                AzureCustomSubdomain = azSubdomain;
            }
            else if (TryGetString(root, "az_endpoint", out var azEndpoint) && !string.IsNullOrWhiteSpace(azEndpoint))
            {
                AzureCustomSubdomain = azEndpoint;
            }
            if (TryGetBool(root, "az_key_set", out var azKeySet))
            {
                AzureKeyStored = azKeySet;
                ReuseAzureKey = azKeySet;
            }

            if (TryGetString(root, "openai_model", out var openAiModel) && !string.IsNullOrWhiteSpace(openAiModel))
            {
                ConfigOpenAiModel = openAiModel;
            }
            if (TryGetString(root, "openai_instructions", out var openAiInstructions) && !string.IsNullOrWhiteSpace(openAiInstructions))
            {
                ConfigOpenAiInstructions = openAiInstructions;
            }
            if (TryGetBool(root, "openai_key_set", out var openAiKeySet) && openAiKeySet && string.IsNullOrWhiteSpace(ConfigOpenAiKey))
            {
                OpenAiKeyStored = true;
                ReuseOpenAiKey = true;
                MaskedOpenAiKey = "(保存済み)";
            }
            else if (TryGetBool(root, "openai_key_set", out openAiKeySet))
            {
                OpenAiKeyStored = openAiKeySet;
                ReuseOpenAiKey = openAiKeySet;
            }

            if (TryGetInt(root, "display_sleep_s", out var displaySleepSeconds))
            {
                DisplaySleepSecondsText = displaySleepSeconds.ToString();
            }
            if (TryGetInt(root, "spk_volume", out var speakerVolume))
            {
                SpeakerVolumeText = speakerVolume.ToString();
            }

            if (TryGetString(root, "share_accepted_text", out var shareAcceptedText) && !string.IsNullOrWhiteSpace(shareAcceptedText))
            {
                ShareAcceptedText = shareAcceptedText;
            }
            if (TryGetString(root, "attention_text", out var attentionText) && !string.IsNullOrWhiteSpace(attentionText))
            {
                AttentionText = attentionText;
            }
            if (TryGetString(root, "hello_text", out var helloText) && !string.IsNullOrWhiteSpace(helloText))
            {
                HelloText = helloText;
            }
        }
        catch
        {
            // best effort: keep current/default input values
        }
    }

    private static bool TryGetString(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(key, out var elem))
        {
            return false;
        }

        if (elem.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = elem.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetBool(JsonElement root, string key, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(key, out var elem))
        {
            return false;
        }

        if (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False)
        {
            value = elem.GetBoolean();
            return true;
        }

        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var n))
        {
            value = n != 0;
            return true;
        }

        return false;
    }

    private static bool TryGetInt(JsonElement root, string key, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(key, out var elem))
        {
            return false;
        }

        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var n))
        {
            value = n;
            return true;
        }

        return false;
    }

    private async Task CreateSupportPackAsync()
    {
        try
        {
            var config = BuildDeviceConfig();

            var summary = new SupportSummary
            {
                AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                DotNetVersion = Environment.Version.ToString(),
                OsVersion = Environment.OSVersion.ToString(),
                AppBaseDirectory = AppContext.BaseDirectory,
                FirmwarePath = FirmwarePath,
                FirmwareInfo = FirmwareInfoText,
                DetectedPorts = string.Join(",", Ports.Select(p => p.PortName)),
                SelectedPort = SelectedPort?.PortName ?? "",
                FlashResult = _lastFlashResult,
                ApiTest = _lastApiResult,
                DeviceTest = _lastDeviceResult,
                LastError = _lastError,
                DeviceInfoJson = string.IsNullOrWhiteSpace(DeviceInfoJson) ? _serialService.LastInfoJson : DeviceInfoJson,
                LastProtocolResponse = string.IsNullOrWhiteSpace(LastProtocolResponse) ? _serialService.LastProtocolResponse : LastProtocolResponse,
                Config = config.ToMasked()
            };

            string deviceLog;
            try
            {
                deviceLog = SelectedPort != null
                    ? await _serialService.DumpLogAsync(SelectedPort.PortName)
                    : string.Empty;
            }
            catch (SerialCommandException ex)
            {
                Log.Warning(ex, "Device log dump not supported");
                deviceLog = string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(deviceLog))
            {
                var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
                var path = LogService.CreateDeviceLogPath();
                await File.WriteAllTextAsync(path, sanitized);
                DeviceLogPath = path;
            }

            var zipPath = await _supportPackService.CreateSupportPackAsync(summary, config);
            StatusMessage = $"サポート用ログを作成: {zipPath}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Support pack failed");
            ErrorMessage = "サポート用ログ作成に失敗しました";
        }
    }

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

        // まずは「ルートの firmware」を最優先（ユーザー差し替え口）
        // 次に app\firmware（同梱の保険）
        // 最後に exeDir 直下（旧互換）
        var candidates = new[]
        {
            rootDir != null ? Path.Combine(rootDir, "firmware", "stackchan_core2_public.bin") : null,
            Path.Combine(exeDir, "firmware", "stackchan_core2_public.bin"),
            Path.Combine(exeDir, "stackchan_core2_public.bin"),
        };

        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
            {
                return c!;
            }
        }

        // 保険：名前が変わっても "_public" を含む .bin を拾う
        // ルート firmware → app firmware の順で探す
        var searchDirs = new[]
        {
            rootDir != null ? Path.Combine(rootDir, "firmware") : null,
            Path.Combine(exeDir, "firmware"),
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
