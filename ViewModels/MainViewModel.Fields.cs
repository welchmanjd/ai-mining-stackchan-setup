using System.Threading;
using System.Windows.Media;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using AiStackchanSetup.Steps;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: own private fields and shared mutable state for all partials.
    private const string ReuseValidationSkippedText = "再利用するため検証対象外";

    private readonly ISerialService _serialService;
    private readonly IFlashService _flashService;
    private readonly IApiTestService _apiTestService;
    private readonly ISupportPackService _supportPackService;
    private readonly RetryPolicy _retryPolicy;
    private readonly StepTimeouts _timeouts;
    private readonly ConfigurationStateService _configurationStateService = new();
    private readonly StepController _stepController;
    private readonly StepContext _stepContext;
    private CancellationTokenSource? _stepCts;

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
    private readonly FlashSettingsState _flashState = new()
    {
        FirmwarePath = ResolveDefaultFirmwarePath()
    };
    private string _deviceStatusSummary = "未取得";
    private string _deviceInfoJson = "";
    private string _lastProtocolResponse = "";
    private string _deviceLogPath = "";

    private readonly WifiSettingsState _wifiState = new();
    private readonly AiSettingsState _aiState = new();
    private string _maskedOpenAiKey = "";
    // Stub: save-to-PC and Azure integration are not implemented in v1 (UI only).
    private bool _saveToPc;

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
}
