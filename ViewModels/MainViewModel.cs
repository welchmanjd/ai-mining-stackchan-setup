using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    private string _deviceStatusSummary = "未取得";
    private string _deviceInfoJson = "";
    private string _lastProtocolResponse = "";

    private string _configWifiSsid = "";
    private string _configWifiPassword = "";
    private string _ducoUser = "";
    private string _ducoMinerKey = "";
    private string _configOpenAiKey = "";
    private string _maskedOpenAiKey = "";
    // Stub: PC保存とAzure連携はv1では未実装（UIのみ）
    private bool _saveToPc;
    private string _azureKey = "";
    private string _azureRegion = "";
    private string _azureCustomSubdomain = "";

    private string _apiTestSummary = "未実行";
    private string _azureTestSummary = "未実行";
    private string _deviceTestSummary = "未実行";

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

        _stepContext = new StepContext(this, _serialService, _flashService, _apiTestService, _supportPackService, _retryPolicy, _timeouts);
        _stepController = new StepController(this, _stepContext, new IStep[]
        {
            new DetectPortsStep(),
            new FlashStep(),
            new WifiStep(),
            new DucoStep(),
            new AzureStep(),
            new OpenAiConfigStep(),
            new LogStep(),
            new CompleteStep()
        });
        _totalSteps = _stepController.TotalSteps;

        PrimaryCommand = new AsyncRelayCommand(PrimaryAsync, () => !IsBusy);
        CloseCommand = new RelayCommand(() => Application.Current.Shutdown());
        CancelCommand = new RelayCommand(CancelCurrent);
        BackCommand = new RelayCommand(GoBack, () => Step > 1 && !IsBusy);
        SkipCommand = new RelayCommand(SkipStep, () => _stepController.CanSkip && !IsBusy);
        AzureTestCommand = new AsyncRelayCommand(TestAzureAsync, () => !IsBusy);
        OpenAiTestCommand = new AsyncRelayCommand(TestOpenAiAsync, () => !IsBusy);
        DumpDeviceLogCommand = new AsyncRelayCommand(DumpDeviceLogAsync, () => !IsBusy);
        BrowseFirmwareCommand = new RelayCommand(BrowseFirmware);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        OpenFlashLogCommand = new RelayCommand(OpenFlashLog);
        CreateSupportPackCommand = new AsyncRelayCommand(CreateSupportPackAsync);

        _stepController.SyncStepMetadata();
        FirmwareInfoText = BuildFirmwareInfoText(FirmwarePath);
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
            }
        }
    }

    public string FirmwareInfoText
    {
        get => _firmwareInfoText;
        set => SetProperty(ref _firmwareInfoText, value);
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
    public string DeviceLogPath => LogService.DeviceLogPath;

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
            }
        }
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
                ResetAzureTestState();
            }
        }
    }

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

    public string AzureTestSummary
    {
        get => _azureTestSummary;
        set => SetProperty(ref _azureTestSummary, value);
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
    public AsyncRelayCommand AzureTestCommand { get; }
    public AsyncRelayCommand OpenAiTestCommand { get; }
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
            _stepController.SyncStepMetadata();
            return;
        }

        if (result.Status == StepStatus.Cancelled)
        {
            StatusMessage = "中止しました";
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
    }

    private void CancelCurrent()
    {
        _stepCts?.Cancel();
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
                ApiTestSummary = apiResult.Success ? "OK" : apiResult.Message;
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
                ApiTestSummary = "OK (確認済み)";
            }

            var azureOk = _azureTestedOk &&
                          _azureTestedKey == AzureKey &&
                          _azureTestedRegion == AzureRegion &&
                          _azureTestedSubdomain == AzureCustomSubdomain;
            ApiTestResult? azureResult = null;
            if (string.IsNullOrWhiteSpace(AzureKey))
            {
                AzureTestSummary = "未入力";
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
                    azureOk = true;
                }
                else
                {
                    AzureTestSummary = azureResult.Success ? "OK" : azureResult.Message;
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
                AzureTestSummary = "OK (確認済み)";
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
            }
            else
            {
                AzureTestSummary = azureResult.Success ? "OK" : azureResult.Message;
            }

            if (azureResult.Success)
            {
                _azureTestedKey = AzureKey;
                _azureTestedRegion = AzureRegion;
                _azureTestedSubdomain = AzureCustomSubdomain;
                _azureTestedOk = true;
            }

            StatusMessage = "Azureキー確認完了";
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
            ApiTestSummary = apiResult.Success ? "OK" : apiResult.Message;
            _lastApiResult = apiResult.Success ? "success" : apiResult.Message;
            if (apiResult.Success)
            {
                _openAiTestedKey = ConfigOpenAiKey;
                _openAiTestedOk = true;
            }
            StatusMessage = "OpenAIキー確認完了";
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

            Directory.CreateDirectory(LogService.LogDirectory);
            var config = BuildDeviceConfig();
            var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
            await File.WriteAllTextAsync(LogService.DeviceLogPath, sanitized);
            StatusMessage = $"デバイスログを保存しました: {LogService.DeviceLogPath}";
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

    private DeviceConfig BuildDeviceConfig()
    {
        return new DeviceConfig
        {
            WifiSsid = ConfigWifiSsid,
            WifiPassword = ConfigWifiPassword,
            DucoUser = DucoUser,
            DucoMinerKey = DucoMinerKey,
            OpenAiKey = ConfigOpenAiKey,
            AzureKey = AzureKey,
            AzureRegion = AzureRegion,
            AzureCustomSubdomain = AzureCustomSubdomain
        };
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

            var deviceLog = SelectedPort != null
                ? await _serialService.DumpLogAsync(SelectedPort.PortName)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(deviceLog))
            {
                var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
                await File.WriteAllTextAsync(LogService.DeviceLogPath, sanitized);
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
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Resources", "firmware", "stackchan_core2.bin");
    }

    private static string BuildFirmwareInfoText(string path)
    {
        var info = FirmwareInfo.FromFile(path);
        if (info == null)
        {
            return "未検出";
        }

        return $"size={info.Size} bytes / mtime={info.LastWriteTime:yyyy-MM-dd HH:mm:ss} / sha256={info.Sha256[..12]}";
    }
}
