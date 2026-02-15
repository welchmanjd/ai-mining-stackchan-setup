using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AiStackchanSetup;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using AiStackchanSetup.Steps;
using Microsoft.Win32;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
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
            StatusMessage = UiText.Cancelled;
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
            PrimaryButtonText = UiText.Retry;
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

    private Task ExecuteAbortToCompleteAsync()
    {
        _abortToCompleteRequested = false;
        Step = 1;
        _stepController.SyncStepMetadata();
        StatusMessage = UiText.AbortedAndReturnedToStep1;
        ErrorMessage = string.Empty;
        return Task.CompletedTask;
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

    private void UpdatePrimaryButtonTextForCurrentStep()
    {
        if (Step == 2)
        {
            PrimaryButtonText = FlashModeSkip ? UiText.FlashSkipWrite : UiText.FlashWrite;
        }
    }

    private async Task RunTestsAsync()
    {
        if (SelectedPort == null)
        {
            ErrorMessage = StepText.ComPortNotSelected;
            return;
        }

        IsBusy = true;
        StatusMessage = UiText.RunningTests;

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
                ApiTestSummary = apiResult.Success ? UiText.Available : UiText.Unavailable(apiResult.Message);
                SetApiSummaryVisual(apiResult.Success);
                _lastApiResult = apiResult.Success ? UiText.SuccessResultCode : apiResult.Message;
                if (apiResult.Success)
                {
                    _openAiTestedKey = ConfigOpenAiKey;
                    _openAiTestedOk = true;
                    openAiOk = true;
                }
            }
            else
            {
                ApiTestSummary = UiText.AvailableChecked;
                SetApiSummaryVisual(true);
            }

            var azureOk = _azureTestedOk &&
                          _azureTestedKey == AzureKey &&
                          _azureTestedRegion == AzureRegion &&
                          _azureTestedSubdomain == AzureCustomSubdomain;
            ApiTestResult? azureResult = null;
            if (string.IsNullOrWhiteSpace(AzureKey))
            {
                AzureTestSummary = UiText.NotEntered;
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
                if (azureResult.Message == UiText.NotEntered)
                {
                    AzureTestSummary = UiText.NotEntered;
                    SetAzureSummaryVisual(null);
                    azureOk = true;
                }
                else
                {
                    AzureTestSummary = azureResult.Success ? UiText.Available : UiText.Unavailable(azureResult.Message);
                    SetAzureSummaryVisual(azureResult.Success);
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
                AzureTestSummary = UiText.AvailableChecked;
                SetAzureSummaryVisual(true);
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
                DeviceTestSummary = UiText.NotImplementedPossible;
                _lastDeviceResult = UiText.SkippedResultCode;
            }
            else
            {
                DeviceTestSummary = deviceResult.Success ? "OK" : deviceResult.Message;
                _lastDeviceResult = deviceResult.Success ? UiText.SuccessResultCode : deviceResult.Message;
            }

            if (openAiOk && azureOk && (deviceResult.Success || deviceResult.Skipped))
            {
                StatusMessage = UiText.TestCompleted;
                Step = 8;
            }
            else
            {
                ErrorMessage = UiText.RetryTestGuidance;
                PrimaryButtonText = UiText.Retry;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Test failed");
            ErrorMessage = UiText.TestFailed;
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
        StatusMessage = UiText.AzureKeyChecking;

        try
        {
            var azureResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                ct => _apiTestService.TestAzureSpeechAsync(AzureKey, AzureRegion, AzureCustomSubdomain, ct),
                TimeSpan.FromSeconds(25),
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                CancellationToken.None);
            if (azureResult.Message == UiText.NotEntered)
            {
                AzureTestSummary = UiText.NotEntered;
                SetAzureSummaryVisual(null);
            }
            else
            {
                AzureTestSummary = azureResult.Success ? UiText.Available : UiText.Unavailable(azureResult.Message);
                SetAzureSummaryVisual(azureResult.Success);
            }

            if (azureResult.Success)
            {
                _azureTestedKey = AzureKey;
                _azureTestedRegion = AzureRegion;
                _azureTestedSubdomain = AzureCustomSubdomain;
                _azureTestedOk = true;
            }

            StatusMessage = UiText.KeyStatus(UiText.AzureKeyLabel, azureResult.Success, azureResult.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Azure test failed");
            ErrorMessage = UiText.AzureKeyCheckFailed;
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
            StatusMessage = UiText.ApiValidationSkippedUsingDeviceInfo;
            return;
        }

        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = UiText.ApiKeyChecking;

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

                ApiTestSummary = apiResult.Success ? UiText.Available : UiText.Unavailable(apiResult.Message);
                SetApiSummaryVisual(apiResult.Success);
                _lastApiResult = apiResult.Success ? UiText.SuccessResultCode : apiResult.Message;
                if (apiResult.Success)
                {
                    _openAiTestedKey = ConfigOpenAiKey;
                    _openAiTestedOk = true;
                }
            }
            else
            {
                ApiTestSummary = StepText.OpenAiPrecheckNotRequired;
                SetApiSummaryVisual(null);
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

                if (azureResult.Message == UiText.NotEntered)
                {
                    AzureTestSummary = UiText.NotEntered;
                    SetAzureSummaryVisual(null);
                }
                else
                {
                    AzureTestSummary = azureResult.Success ? UiText.Available : UiText.Unavailable(azureResult.Message);
                    SetAzureSummaryVisual(azureResult.Success);
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
                AzureTestSummary = StepText.AzurePrecheckNotRequired;
                SetAzureSummaryVisual(null);
            }

            StatusMessage = UiText.ApiKeyCheckCompleted;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Validate API keys failed");
            ErrorMessage = UiText.ApiKeyCheckFailed;
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

    private void RefreshApiValidationSummaries()
    {
        if (OpenAiKeyStored && ReuseOpenAiKey)
        {
            ApiTestSummary = ReuseValidationSkippedText;
            SetApiSummaryVisual(null);
        }
        else if (ApiTestSummary == ReuseValidationSkippedText)
        {
            ApiTestSummary = UiText.NotExecuted;
            SetApiSummaryVisual(null);
        }

        if (AzureKeyStored && ReuseAzureKey)
        {
            AzureTestSummary = ReuseValidationSkippedText;
            SetAzureSummaryVisual(null);
        }
        else if (AzureTestSummary == ReuseValidationSkippedText)
        {
            AzureTestSummary = UiText.NotExecuted;
            SetAzureSummaryVisual(null);
        }
    }

    private async Task TestOpenAiAsync()
    {
        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = UiText.OpenAiKeyChecking;

        try
        {
            var apiResult = await _retryPolicy.ExecuteWithTimeoutAsync(
                ct => _apiTestService.TestAsync(ConfigOpenAiKey, ct),
                TimeSpan.FromSeconds(25),
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                CancellationToken.None);
            ApiTestSummary = apiResult.Success ? UiText.Available : UiText.Unavailable(apiResult.Message);
            SetApiSummaryVisual(apiResult.Success);
            _lastApiResult = apiResult.Success ? UiText.SuccessResultCode : apiResult.Message;
            if (apiResult.Success)
            {
                _openAiTestedKey = ConfigOpenAiKey;
                _openAiTestedOk = true;
            }
            StatusMessage = UiText.KeyStatus(UiText.OpenAiKeyLabel, apiResult.Success, apiResult.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenAI test failed");
            ErrorMessage = UiText.OpenAiKeyCheckFailed;
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
            ErrorMessage = StepText.ComPortNotSelected;
            return;
        }

        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = UiText.DeviceLogFetching;

        try
        {
            var deviceLog = await _serialService.DumpLogAsync(SelectedPort.PortName);
            if (string.IsNullOrWhiteSpace(deviceLog))
            {
                ErrorMessage = UiText.DeviceLogEmpty;
                return;
            }

            var config = BuildDeviceConfig();
            var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
            var path = LogService.CreateDeviceLogPath();
            await File.WriteAllTextAsync(path, sanitized);
            DeviceLogPath = path;
            StatusMessage = UiText.DeviceLogSaved(path);
        }
        catch (SerialCommandException ex)
        {
            Log.Warning(ex, "Device log dump not supported");
            ErrorMessage = UiText.DeviceLogUnsupported;
            _lastError = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Device log dump failed");
            ErrorMessage = UiText.DeviceLogFetchFailed;
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
        AzureTestSummary = UiText.NotExecuted;
        SetAzureSummaryVisual(null);
    }

    private void SetApiSummaryVisual(bool? success)
    {
        if (success == null)
        {
            ApiTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            ApiTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            return;
        }

        ApiTestSummaryBrush = success.Value
            ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        ApiTestSummaryBackground = success.Value
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
            : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
    }

    private void SetAzureSummaryVisual(bool? success)
    {
        if (success == null)
        {
            AzureTestSummaryBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            AzureTestSummaryBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            return;
        }

        AzureTestSummaryBrush = success.Value
            ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        AzureTestSummaryBackground = success.Value
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3))
            : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
    }

    private void BrowseFirmware()
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.BinFileDialogFilter
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
            StatusMessage = UiText.SupportPackCreated(zipPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Support pack failed");
            ErrorMessage = UiText.SupportPackCreationFailed;
        }
    }

}

