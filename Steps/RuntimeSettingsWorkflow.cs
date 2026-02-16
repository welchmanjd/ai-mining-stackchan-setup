using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

internal sealed class RuntimeSettingsWorkflow
{
    private readonly StepContext _context;
    private readonly MainViewModel _vm;
    private readonly CancellationToken _token;

    public RuntimeSettingsWorkflow(StepContext context, MainViewModel vm, CancellationToken token)
    {
        _context = context;
        _vm = vm;
        _token = token;
    }

    public async Task<RuntimeWorkflowResult> RunApiPrecheckAsync()
    {
        var needOpenAi = _vm.WifiEnabled && _vm.AiEnabled;
        var needAzure = _vm.WifiEnabled && (_vm.MiningEnabled || _vm.AiEnabled);

        var openAiExcluded = needOpenAi && _vm.OpenAiKeyStored && _vm.ReuseOpenAiKey;
        var azureExcluded = needAzure && _vm.AzureKeyStored && _vm.ReuseAzureKey;

        var openAiOk = !needOpenAi;
        var azureOk = !needAzure;

        if (!needOpenAi)
        {
            ApiValidationSummaryPresenter.SetOpenAiNeutral(_vm, StepText.OpenAiPrecheckNotRequired);
        }
        else if (openAiExcluded)
        {
            ApiValidationSummaryPresenter.SetOpenAiNeutral(_vm, StepText.ApiPrecheckSkippedUsingStoredKey);
            openAiOk = true;
        }
        else
        {
            var openAiResult = await _context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => _context.ApiTestService.TestAsync(_vm.ConfigOpenAiKey, ct),
                TimeSpan.FromSeconds(25),
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                _token);

            if (openAiResult.Success)
            {
                ApiValidationSummaryPresenter.SetOpenAiOk(_vm, StepText.ApiPrecheckSuccess);
                openAiOk = true;
            }
            else
            {
                ApiValidationSummaryPresenter.SetOpenAiNg(_vm, $"{StepText.ApiPrecheckFailedPrefix}: {openAiResult.Message}");
            }
        }

        if (!needAzure)
        {
            ApiValidationSummaryPresenter.SetAzureNeutral(_vm, StepText.AzurePrecheckNotRequired);
        }
        else if (azureExcluded)
        {
            ApiValidationSummaryPresenter.SetAzureNeutral(_vm, StepText.ApiPrecheckSkippedUsingStoredKey);
            azureOk = true;
        }
        else
        {
            var azureResult = await _context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => _context.ApiTestService.TestAzureSpeechAsync(_vm.AzureKey, _vm.AzureRegion, _vm.AzureCustomSubdomain, ct),
                TimeSpan.FromSeconds(25),
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                _token);

            if (azureResult.Success)
            {
                ApiValidationSummaryPresenter.SetAzureOk(_vm, StepText.ApiPrecheckSuccess);
                azureOk = true;
            }
            else
            {
                ApiValidationSummaryPresenter.SetAzureNg(_vm, $"{StepText.ApiPrecheckFailedPrefix}: {azureResult.Message}");
            }
        }

        return openAiOk && azureOk
            ? RuntimeWorkflowResult.Ok()
            : RuntimeWorkflowResult.Fail(StepText.ApiKeyValidationRetryGuidance);
    }

    public async Task<RuntimeWorkflowResult> SendAndApplyConfigAsync(DeviceConfig config)
    {
        _vm.StatusMessage = StepText.ConfigSendInProgress;

        var setResult = await _context.RetryPolicy.ExecuteWithTimeoutAsync(
            ct => _context.SerialService.SendConfigAsync(_vm.SelectedPort!.PortName, config, ct),
            _context.Timeouts.SendConfig,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(400),
            backoffFactor: 2,
            _token);

        if (setResult.IsFailure)
        {
            _vm.ErrorMessage = $"{StepText.ConfigSendFailed}: {setResult.Message}";
            _vm.LastError = setResult.Message;
            _vm.StatusMessage = StepText.ConfigSendFailed;
            return RuntimeWorkflowResult.Fail($"{StepText.ConfigSendFailed}: {setResult.Message}");
        }

        if (!string.IsNullOrWhiteSpace(setResult.Message) && !setResult.HasMessage("OK"))
        {
            _vm.StatusMessage = setResult.Message;
        }

        var applyResult = await _context.RetryPolicy.ExecuteWithTimeoutAsync(
            ct => _context.SerialService.ApplyConfigAsync(_vm.SelectedPort!.PortName, ct),
            _context.Timeouts.ApplyConfig,
            maxAttempts: 2,
            baseDelay: TimeSpan.FromMilliseconds(600),
            backoffFactor: 2,
            _token);

        if (applyResult.IsFailure)
        {
            _vm.ErrorMessage = $"{StepText.ConfigSaveFailed}: {applyResult.Message}";
            _vm.LastError = applyResult.Message;
            _vm.StatusMessage = StepText.ConfigSaveFailed;
            return RuntimeWorkflowResult.Fail($"{StepText.ConfigSaveFailed}: {applyResult.Message}");
        }

        return RuntimeWorkflowResult.Ok();
    }

    public async Task<RuntimeWorkflowResult> VerifyPersistedFlagsWithRetryAsync(DeviceConfig config)
    {
        var verify = await VerifyFlagsAsync();
        if (verify.Succeeded)
        {
            return RuntimeWorkflowResult.Ok();
        }

        _vm.StatusMessage = StepText.ConfigReverifyInProgress;

        var retrySet = await _context.SerialService.SendConfigAsync(_vm.SelectedPort!.PortName, config, _token);
        if (retrySet.IsFailure)
        {
            return RuntimeWorkflowResult.Fail($"{StepText.ConfigVerificationFailed}: {verify.Detail}");
        }

        var retryApply = await _context.SerialService.ApplyConfigAsync(_vm.SelectedPort!.PortName, _token);
        if (retryApply.IsFailure)
        {
            return RuntimeWorkflowResult.Fail($"{StepText.ConfigVerificationFailed}: {verify.Detail}");
        }

        var verify2 = await VerifyFlagsAsync();
        return verify2.Succeeded
            ? RuntimeWorkflowResult.Ok()
            : RuntimeWorkflowResult.Fail($"{StepText.ConfigVerificationFailed}: {verify2.Detail}");
    }

    public async Task CapturePostRebootLogIfEnabledAsync()
    {
        if (!_vm.CaptureSerialLogAfterReboot)
        {
            _vm.StatusMessage = StepText.ConfigSavedAndRebooted;
            return;
        }

        const int captureSeconds = 60;
        try
        {
            var captureTask = _context.SerialService.CapturePostRebootLogAsync(
                _vm.SelectedPort!.PortName,
                TimeSpan.FromSeconds(captureSeconds),
                _token);

            for (var elapsed = 0; elapsed < captureSeconds; elapsed++)
            {
                _vm.StatusMessage = string.Format(StepText.PostRebootLogCaptureProgressFormat, elapsed + 1);
                var completed = await Task.WhenAny(captureTask, Task.Delay(1000, _token)) == captureTask;
                if (completed)
                {
                    break;
                }
            }

            var rebootLog = await captureTask;
            if (!string.IsNullOrWhiteSpace(rebootLog))
            {
                var path = LogService.CreateDeviceLogPath();
                await File.WriteAllTextAsync(path, rebootLog, _token);
                _vm.DeviceLogPath = path;
                _vm.StatusMessage = string.Format(StepText.ConfigSavedAndRebootedWithLogPathFormat, path);
            }
            else
            {
                _vm.StatusMessage = StepText.ConfigSavedAndRebootedNoLog;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = string.Format(StepText.ConfigSavedAndRebootedLogCaptureFailedFormat, ex.Message);
        }
    }

    private async Task<RuntimeWorkflowResult> VerifyFlagsAsync()
    {
        var cfg = await _context.SerialService.GetConfigJsonAsync(_vm.SelectedPort!.PortName, _token);
        if (cfg.IsFailure || string.IsNullOrWhiteSpace(cfg.Json))
        {
            return RuntimeWorkflowResult.Fail(cfg.Message);
        }

        try
        {
            using var doc = JsonDocument.Parse(cfg.Json);
            var root = doc.RootElement;
            bool? miningValue = null;
            if (root.TryGetProperty("mining_enabled", out var m))
            {
                if (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.False)
                {
                    miningValue = m.GetBoolean();
                }
                else if (m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var n))
                {
                    miningValue = n != 0;
                }
                else if (m.ValueKind == JsonValueKind.String)
                {
                    var s = m.GetString();
                    if (s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        miningValue = true;
                    }

                    if (s == "0" || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        miningValue = false;
                    }
                }
            }

            var expectedMining = _vm.WifiEnabled && _vm.MiningEnabled;
            if (miningValue.HasValue && miningValue.Value != expectedMining)
            {
                return RuntimeWorkflowResult.Fail($"mining_enabled mismatch (expected={expectedMining}, actual={miningValue.Value})");
            }

            return RuntimeWorkflowResult.Ok();
        }
        catch (Exception ex)
        {
            return RuntimeWorkflowResult.Fail(ex.Message);
        }
    }
}

