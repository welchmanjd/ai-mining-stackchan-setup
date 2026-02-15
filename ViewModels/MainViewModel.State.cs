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
}
