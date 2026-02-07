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
using Microsoft.Win32;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public class MainViewModel : BindableBase
{
    private readonly SerialService _serialService = new();
    private readonly FlashService _flashService = new();
    private readonly ApiTestService _apiTestService = new();
    private readonly SupportPackService _supportPackService = new();

    private int _step = 1;
    private string _stepTitle = "接続";
    private string _primaryButtonText = "探す";
    private bool _isBusy;
    private string _statusMessage = "";
    private string _errorMessage = "";
    private string _step1Help = "";
    private bool _isAdvanced;

    private SerialPortInfo? _selectedPort;
    private string _firmwarePath = Path.Combine(AppContext.BaseDirectory, "Resources", "firmware", "stackchan_core2.bin");
    private string _flashBaudText = "921600";
    private bool _flashErase;
    private string _flashStatus = "";

    private string _configWifiSsid = "";
    private string _configWifiPassword = "";
    private string _configOpenAiKey = "";
    private string _maskedOpenAiKey = "";
    // Stub: PC保存とAzure連携はv1では未実装（UIのみ）
    private bool _saveToPc;
    private string _azureKey = "";
    private string _azureRegion = "";

    private string _apiTestSummary = "未実行";
    private string _deviceTestSummary = "未実行";

    private string _lastFlashResult = "";
    private string _lastApiResult = "";
    private string _lastDeviceResult = "";
    private string _lastError = "";

    public MainViewModel()
    {
        Ports = new ObservableCollection<SerialPortInfo>();

        PrimaryCommand = new AsyncRelayCommand(PrimaryAsync, () => !IsBusy);
        CloseCommand = new RelayCommand(() => Application.Current.Shutdown());
        BrowseFirmwareCommand = new RelayCommand(BrowseFirmware);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        OpenFlashLogCommand = new RelayCommand(OpenFlashLog);
        CreateSupportPackCommand = new AsyncRelayCommand(CreateSupportPackAsync);

        UpdateStepMetadata();
    }

    public ObservableCollection<SerialPortInfo> Ports { get; }

    public int Step
    {
        get => _step;
        set
        {
            if (SetProperty(ref _step, value))
            {
                UpdateStepMetadata();
                RaisePropertyChanged(nameof(StepIndicator));
            }
        }
    }

    public string StepTitle
    {
        get => _stepTitle;
        set => SetProperty(ref _stepTitle, value);
    }

    public string StepIndicator => $"{Step}/5";

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
            }
        }
    }

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

    public bool IsAdvanced
    {
        get => _isAdvanced;
        set => SetProperty(ref _isAdvanced, value);
    }

    public SerialPortInfo? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    public string FirmwarePath
    {
        get => _firmwarePath;
        set => SetProperty(ref _firmwarePath, value);
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

    public string FlashStatus
    {
        get => _flashStatus;
        set => SetProperty(ref _flashStatus, value);
    }

    public string FlashLogPath => LogService.FlashLogPath;

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

    public string ConfigOpenAiKey
    {
        get => _configOpenAiKey;
        set
        {
            if (SetProperty(ref _configOpenAiKey, value))
            {
                MaskedOpenAiKey = DeviceConfig.Mask(value);
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
        set => SetProperty(ref _azureKey, value);
    }

    public string AzureRegion
    {
        get => _azureRegion;
        set => SetProperty(ref _azureRegion, value);
    }

    public string ApiTestSummary
    {
        get => _apiTestSummary;
        set => SetProperty(ref _apiTestSummary, value);
    }

    public string DeviceTestSummary
    {
        get => _deviceTestSummary;
        set => SetProperty(ref _deviceTestSummary, value);
    }

    public RelayCommand CloseCommand { get; }
    public AsyncRelayCommand PrimaryCommand { get; }
    public RelayCommand BrowseFirmwareCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand OpenFlashLogCommand { get; }
    public AsyncRelayCommand CreateSupportPackCommand { get; }

    private async Task PrimaryAsync()
    {
        ErrorMessage = "";

        switch (Step)
        {
            case 1:
                await DetectPortsAsync();
                break;
            case 2:
                await FlashAsync();
                break;
            case 3:
                await SendConfigAsync();
                break;
            case 4:
                await RunTestsAsync();
                break;
            case 5:
                Application.Current.Shutdown();
                break;
        }
    }

    private async Task DetectPortsAsync()
    {
        IsBusy = true;
        StatusMessage = "USBポートを探しています...";
        Step1Help = "";

        try
        {
            Ports.Clear();
            var ports = await _serialService.DetectPortsAsync();
            foreach (var port in ports)
            {
                Ports.Add(port);
            }

            SelectedPort = _serialService.SelectBestPort(Ports);

            if (SelectedPort == null)
            {
                Step1Help = "見つかりません。充電専用ケーブル/USBポート/ドライバを確認してください。";
                StatusMessage = "未検出";
            }
            else
            {
                StatusMessage = $"{SelectedPort.DisplayName} を検出";
                Step = 2;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Detect ports failed");
            ErrorMessage = "ポート検出に失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FlashAsync()
    {
        if (SelectedPort == null)
        {
            ErrorMessage = "COMポートが未選択です";
            return;
        }

        if (!File.Exists(FirmwarePath))
        {
            ErrorMessage = "ファームウェアファイルが見つかりません";
            return;
        }

        if (!int.TryParse(FlashBaud, out var baud))
        {
            ErrorMessage = "Baudが不正です";
            return;
        }

        IsBusy = true;
        FlashStatus = "書き込み中...";
        StatusMessage = "";

        try
        {
            var result = await _flashService.FlashAsync(SelectedPort.PortName, baud, FlashErase, FirmwarePath, CancellationToken.None);
            _lastFlashResult = result.Success ? "success" : "fail";

            if (result.Success)
            {
                FlashStatus = "書き込み完了";
                Step = 3;
            }
            else
            {
                FlashStatus = "書き込み失敗";
                ErrorMessage = $"書き込みに失敗しました。ログ: {result.LogPath}";
                PrimaryButtonText = "再試行";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Flash failed");
            ErrorMessage = $"書き込みに失敗しました。ログ: {LogService.FlashLogPath}";
            _lastError = ex.Message;
            PrimaryButtonText = "再試行";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SendConfigAsync()
    {
        if (SelectedPort == null)
        {
            ErrorMessage = "COMポートが未選択です";
            return;
        }

        if (string.IsNullOrWhiteSpace(ConfigWifiSsid) || string.IsNullOrWhiteSpace(ConfigWifiPassword))
        {
            ErrorMessage = "Wi-Fi情報が未入力です";
            return;
        }

        if (string.IsNullOrWhiteSpace(ConfigOpenAiKey))
        {
            ErrorMessage = "OpenAI APIキーが未入力です";
            return;
        }

        IsBusy = true;
        StatusMessage = "デバイスに設定を送信中...";

        try
        {
            var hello = await _serialService.HelloAsync(SelectedPort.PortName);
            if (!hello.Success)
            {
                ErrorMessage = hello.Message;
                _lastError = hello.Message;
                return;
            }

            var config = new DeviceConfig
            {
                WifiSsid = ConfigWifiSsid,
                WifiPassword = ConfigWifiPassword,
                OpenAiKey = ConfigOpenAiKey,
                AzureKey = AzureKey,
                AzureRegion = AzureRegion
            };

            var setResult = await _serialService.SendConfigAsync(SelectedPort.PortName, config);
            if (!setResult.Success)
            {
                ErrorMessage = $"設定送信失敗: {setResult.Message}";
                _lastError = setResult.Message;
                return;
            }

            var applyResult = await _serialService.ApplyConfigAsync(SelectedPort.PortName);
            if (!applyResult.Success)
            {
                ErrorMessage = $"設定適用失敗: {applyResult.Message}";
                _lastError = applyResult.Message;
                return;
            }

            StatusMessage = "設定送信完了";
            Step = 4;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Send config failed");
            ErrorMessage = "設定送信に失敗しました";
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
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
            var apiResult = await _apiTestService.TestAsync(ConfigOpenAiKey, CancellationToken.None);
            ApiTestSummary = apiResult.Success ? "OK" : apiResult.Message;
            _lastApiResult = apiResult.Success ? "success" : apiResult.Message;

            // Stub: 端末側TEST_RUN未実装の場合はSkippedとして扱う
            var deviceResult = await _serialService.RunTestAsync(SelectedPort.PortName);
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

            if (apiResult.Success && (deviceResult.Success || deviceResult.Skipped))
            {
                StatusMessage = "テスト完了";
                Step = 5;
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

    private async Task CreateSupportPackAsync()
    {
        try
        {
            var summary = new SupportSummary
            {
                AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                OsVersion = Environment.OSVersion.ToString(),
                DetectedPorts = string.Join(",", Ports.Select(p => p.PortName)),
                SelectedPort = SelectedPort?.PortName ?? "",
                FlashResult = _lastFlashResult,
                ApiTest = _lastApiResult,
                DeviceTest = _lastDeviceResult,
                LastError = _lastError,
                Config = new DeviceConfig
                {
                    WifiSsid = ConfigWifiSsid,
                    WifiPassword = ConfigWifiPassword,
                    OpenAiKey = ConfigOpenAiKey,
                    AzureKey = AzureKey,
                    AzureRegion = AzureRegion
                }.ToMasked()
            };

            var deviceLog = SelectedPort != null
                ? await _serialService.DumpLogAsync(SelectedPort.PortName)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(deviceLog))
            {
                await File.WriteAllTextAsync(LogService.DeviceLogPath, deviceLog);
            }

            var zipPath = await _supportPackService.CreateSupportPackAsync(summary);
            StatusMessage = $"サポート用ログを作成: {zipPath}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Support pack failed");
            ErrorMessage = "サポート用ログ作成に失敗しました";
        }
    }

    private void UpdateStepMetadata()
    {
        switch (Step)
        {
            case 1:
                StepTitle = "接続";
                PrimaryButtonText = "探す";
                break;
            case 2:
                StepTitle = "書き込み";
                PrimaryButtonText = "書き込む";
                break;
            case 3:
                StepTitle = "設定";
                PrimaryButtonText = "保存してデバイスに送る";
                break;
            case 4:
                StepTitle = "テスト";
                PrimaryButtonText = "AIテストを実行";
                break;
            case 5:
                StepTitle = "完了";
                PrimaryButtonText = "閉じる";
                break;
        }
    }
}
