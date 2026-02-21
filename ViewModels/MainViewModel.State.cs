using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: expose bindable UI state, computed properties, and command properties.
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
                HandleBusyStateChanged();
            }
        }
    }

    public bool CanCancel => IsBusy && _stepCts != null;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string StatusAssistMessage
    {
        get => _statusAssistMessage;
        set => SetProperty(ref _statusAssistMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string StepGuidanceMessage
    {
        get => _stepGuidanceMessage;
        set => SetProperty(ref _stepGuidanceMessage, value);
    }

    public bool ShowFailureActions
    {
        get => _showFailureActions;
        set => SetProperty(ref _showFailureActions, value);
    }

    public bool CanRetryCurrentStep
    {
        get => _canRetryCurrentStep;
        set => SetProperty(ref _canRetryCurrentStep, value);
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
        get => _flashState.FirmwarePath;
        set
        {
            if (SetProperty(ref _flashState.FirmwarePath, value))
            {
                FirmwareInfoText = BuildFirmwareInfoText(value);
                RefreshFirmwareComparisonMessage();
            }
        }
    }

    public string FirmwareInfoText
    {
        get => _flashState.FirmwareInfoText;
        set => SetProperty(ref _flashState.FirmwareInfoText, value);
    }

    public string CurrentFirmwareInfoText
    {
        get => _flashState.CurrentFirmwareInfoText;
        set
        {
            if (SetProperty(ref _flashState.CurrentFirmwareInfoText, value))
            {
                RaisePropertyChanged(nameof(FlashOperationTitle));
                RaisePropertyChanged(nameof(FlashOverwriteOptionText));
                RaisePropertyChanged(nameof(FlashEraseOptionText));
                RaisePropertyChanged(nameof(ShowFlashSkipOption));

                if (!ShowFlashSkipOption && FlashModeSkip)
                {
                    FlashMode = 0;
                }
            }
        }
    }

    public string FirmwareCompareMessage
    {
        get => _flashState.FirmwareCompareMessage;
        set => SetProperty(ref _flashState.FirmwareCompareMessage, value);
    }

    public string FlashOperationTitle => HasCurrentFirmwareOnDevice
        ? "ファームウエアの上書き"
        : "ファームウエアの書き込み";

    public string FlashOverwriteOptionText => HasCurrentFirmwareOnDevice
        ? "ファームウエアを上書き"
        : "ファームウエアを書き込み";

    public string FlashEraseOptionText => HasCurrentFirmwareOnDevice
        ? "フラッシュを消去して上書き"
        : "フラッシュを消去して書き込み";

    public bool ShowFlashSkipOption => HasCurrentFirmwareOnDevice;

    private bool HasCurrentFirmwareOnDevice
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrentFirmwareInfoText))
            {
                return false;
            }

            return !string.Equals(CurrentFirmwareInfoText, "未取得", StringComparison.Ordinal);
        }
    }

    public string FlashBaud
    {
        get => _flashState.FlashBaudText;
        set => SetProperty(ref _flashState.FlashBaudText, value);
    }

    public bool FlashErase
    {
        get => _flashState.FlashErase;
        set => SetProperty(ref _flashState.FlashErase, value);
    }

    public int FlashMode
    {
        get => _flashState.FlashMode;
        set
        {
            if (SetProperty(ref _flashState.FlashMode, value))
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
            if (value && ShowFlashSkipOption)
            {
                FlashMode = 2;
            }
        }
    }

    public string FlashStatus
    {
        get => _flashState.FlashStatus;
        set => SetProperty(ref _flashState.FlashStatus, value);
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
        get => _wifiState.ConfigWifiSsid;
        set
        {
            if (SetProperty(ref _wifiState.ConfigWifiSsid, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string ConfigWifiPassword
    {
        get => _wifiState.ConfigWifiPassword;
        set
        {
            var normalized = InputSanitizer.NormalizeWifiPassword(value);
            if (SetProperty(ref _wifiState.ConfigWifiPassword, normalized))
            {
                RaisePropertyChanged(nameof(InputStatusText));
                RaisePropertyChanged(nameof(ShowWifiPasswordSpaceWarning));
            }
        }
    }

    public bool WifiPasswordStored
    {
        get => _wifiState.WifiPasswordStored;
        set
        {
            if (SetProperty(ref _wifiState.WifiPasswordStored, value))
            {
                if (!_wifiState.WifiPasswordStored)
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
        get => _wifiState.ReuseWifiPassword;
        set
        {
            if (SetProperty(ref _wifiState.ReuseWifiPassword, value))
            {
                RaisePropertyChanged(nameof(CanEditWifiPassword));
                RaisePropertyChanged(nameof(InputStatusText));
                RaisePropertyChanged(nameof(ShowWifiPasswordSpaceWarning));
            }
        }
    }

    public bool CanReuseWifiPassword => WifiPasswordStored;
    public bool CanEditWifiPassword => !ReuseWifiPassword;
    public bool ShowWifiPasswordSpaceWarning => CanEditWifiPassword && InputSanitizer.HasEdgeWhitespace(ConfigWifiPassword);
    public bool ShowWifiPassword
    {
        get => _showWifiPassword;
        set => SetProperty(ref _showWifiPassword, value);
    }

    public bool WifiEnabled
    {
        get => _wifiState.WifiEnabled;
        set
        {
            if (SetProperty(ref _wifiState.WifiEnabled, value))
            {
                if (!_wifiState.WifiEnabled)
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
        get => _wifiState.MiningEnabled;
        set
        {
            if (SetProperty(ref _wifiState.MiningEnabled, value))
            {
                RaisePropertyChanged(nameof(IsMiningDisabled));
                RaiseApiValidationStateChanged();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool AiEnabled
    {
        get => _wifiState.AiEnabled;
        set
        {
            if (SetProperty(ref _wifiState.AiEnabled, value))
            {
                RaiseApiValidationStateChanged();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool FeatureToggleChildrenEnabled => WifiEnabled;
    public bool IsMiningDisabled => !MiningEnabled;

    public string DucoUser
    {
        get => _wifiState.DucoUser;
        set
        {
            if (SetProperty(ref _wifiState.DucoUser, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string DucoMinerKey
    {
        get => _wifiState.DucoMinerKey;
        set
        {
            if (SetProperty(ref _wifiState.DucoMinerKey, value))
            {
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool DucoKeyStored
    {
        get => _wifiState.DucoKeyStored;
        set
        {
            if (SetProperty(ref _wifiState.DucoKeyStored, value))
            {
                if (!_wifiState.DucoKeyStored)
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
        get => _wifiState.ReuseDucoMinerKey;
        set
        {
            if (SetProperty(ref _wifiState.ReuseDucoMinerKey, value))
            {
                RaisePropertyChanged(nameof(CanEditDucoMinerKey));
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool CanReuseDucoMinerKey => DucoKeyStored;
    public bool CanEditDucoMinerKey => !ReuseDucoMinerKey;
    public bool ShowDucoMinerKey
    {
        get => _showDucoMinerKey;
        set => SetProperty(ref _showDucoMinerKey, value);
    }

    public string ConfigOpenAiKey
    {
        get => _aiState.ConfigOpenAiKey;
        set
        {
            var normalized = InputSanitizer.NormalizeSecret(value);
            if (SetProperty(ref _aiState.ConfigOpenAiKey, normalized))
            {
                MaskedOpenAiKey = DeviceConfig.Mask(normalized);
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
        get => _aiState.OpenAiKeyStored;
        set
        {
            if (SetProperty(ref _aiState.OpenAiKeyStored, value))
            {
                if (!_aiState.OpenAiKeyStored)
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
        get => _aiState.ReuseOpenAiKey;
        set
        {
            if (SetProperty(ref _aiState.ReuseOpenAiKey, value))
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
    public bool ShowOpenAiKey
    {
        get => _showOpenAiKey;
        set => SetProperty(ref _showOpenAiKey, value);
    }

    public string ConfigOpenAiModel
    {
        get => _aiState.ConfigOpenAiModel;
        set => SetProperty(ref _aiState.ConfigOpenAiModel, NormalizeOpenAiModel(value));
    }

    public string ConfigOpenAiInstructions
    {
        get => _aiState.ConfigOpenAiInstructions;
        set => SetProperty(ref _aiState.ConfigOpenAiInstructions, value);
    }

    public string DisplaySleepSecondsText
    {
        get => _aiState.DisplaySleepSecondsText;
        set => SetProperty(ref _aiState.DisplaySleepSecondsText, value);
    }

    public bool CaptureSerialLogAfterReboot
    {
        get => _aiState.CaptureSerialLogAfterReboot;
        set => SetProperty(ref _aiState.CaptureSerialLogAfterReboot, value);
    }

    public string SpeakerVolumeText
    {
        get => _aiState.SpeakerVolumeText;
        set
        {
            if (SetProperty(ref _aiState.SpeakerVolumeText, value))
            {
                if (int.TryParse(value, out var raw))
                {
                    raw = Math.Clamp(raw, 0, 255);
                    if (_aiState.SpeakerVolumeRaw != raw)
                    {
                        _aiState.SpeakerVolumeRaw = raw;
                        RaisePropertyChanged(nameof(SpeakerVolumeRaw));
                    }
                }
            }
        }
    }

    public int SpeakerVolumeRaw
    {
        get => _aiState.SpeakerVolumeRaw;
        set
        {
            var clamped = Math.Clamp(value, 0, 255);
            if (SetProperty(ref _aiState.SpeakerVolumeRaw, clamped))
            {
                var rawText = _aiState.SpeakerVolumeRaw.ToString();
                if (_aiState.SpeakerVolumeText != rawText)
                {
                    _aiState.SpeakerVolumeText = rawText;
                    RaisePropertyChanged(nameof(SpeakerVolumeText));
                }
            }
        }
    }

    public string ShareAcceptedText
    {
        get => _aiState.ShareAcceptedText;
        set => SetProperty(ref _aiState.ShareAcceptedText, value);
    }

    public string AttentionText
    {
        get => _aiState.AttentionText;
        set => SetProperty(ref _aiState.AttentionText, value);
    }

    public string HelloText
    {
        get => _aiState.HelloText;
        set => SetProperty(ref _aiState.HelloText, value);
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
        get => _aiState.AzureKey;
        set
        {
            var normalized = InputSanitizer.NormalizeSecret(value);
            if (SetProperty(ref _aiState.AzureKey, normalized))
            {
                ResetAzureTestState();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public bool AzureKeyStored
    {
        get => _aiState.AzureKeyStored;
        set
        {
            if (SetProperty(ref _aiState.AzureKeyStored, value))
            {
                if (!_aiState.AzureKeyStored)
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
        get => _aiState.ReuseAzureKey;
        set
        {
            if (SetProperty(ref _aiState.ReuseAzureKey, value))
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
    public bool ShowAzureKey
    {
        get => _showAzureKey;
        set => SetProperty(ref _showAzureKey, value);
    }
    public bool CanRunApiValidation => !IsUsingStoredApiKeys;
    public string ApiValidationGuideText => IsUsingStoredApiKeys
        ? "M5StackCore2内の情報を利用するためスキップします。"
        : "必要に応じてここでAPIキーの有効確認を実行できます。";

    private bool IsUsingStoredApiKeys => _configurationStateService.IsUsingStoredApiKeys(this);

    public string AzureRegion
    {
        get => _aiState.AzureRegion;
        set
        {
            if (SetProperty(ref _aiState.AzureRegion, value))
            {
                ResetAzureTestState();
                RaisePropertyChanged(nameof(InputStatusText));
            }
        }
    }

    public string AzureCustomSubdomain
    {
        get => _aiState.AzureCustomSubdomain;
        set
        {
            if (SetProperty(ref _aiState.AzureCustomSubdomain, value))
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
    public AsyncRelayCommand AzureTestCommand { get; }
    public AsyncRelayCommand OpenAiTestCommand { get; }
    public AsyncRelayCommand ValidateApiKeysCommand { get; }
    public AsyncRelayCommand DumpDeviceLogCommand { get; }
    public RelayCommand BrowseFirmwareCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand OpenFlashLogCommand { get; }
    public AsyncRelayCommand CreateSupportPackCommand { get; }
}
