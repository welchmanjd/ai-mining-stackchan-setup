using System.Collections.ObjectModel;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using AiStackchanSetup.Steps;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel : BindableBase
{
    // Responsibility: bootstrap commands/services and initialize the step workflow state.
    public MainViewModel()
    {
        Ports = new ObservableCollection<SerialPortInfo>();
        _deviceLogPath = LogService.GetLatestDeviceLogPath() ?? "";

        _stepContext = new StepContext(this, _serialService, _flashService, _apiTestService, _supportPackService, _retryPolicy, _timeouts);
        _stepController = new StepController(this, _stepContext, StepCatalog.CreateDefaultSteps());
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

}
