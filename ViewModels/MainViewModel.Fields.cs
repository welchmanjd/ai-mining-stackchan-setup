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
    // Stub: save-to-PC and Azure integration are not implemented in v1 (UI only).
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
}
