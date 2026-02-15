using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: API key checks, runtime validation, and result visuals.

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

            // Stub: treat as skipped when TEST_RUN is not implemented on the device side.
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
        _configurationStateService.RaiseApiValidationStateChanged(
            this,
            propertyName => RaisePropertyChanged(propertyName),
            (AsyncRelayCommand)ValidateApiKeysCommand);
    }

    private void RefreshApiValidationSummaries()
    {
        _configurationStateService.RefreshApiValidationSummaries(
            this,
            ReuseValidationSkippedText,
            SetApiSummaryVisual,
            SetAzureSummaryVisual);
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

}
