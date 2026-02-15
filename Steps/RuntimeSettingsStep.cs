using System;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

public sealed class RuntimeSettingsStep : StepBase
{
    public RuntimeSettingsStep() : base(StepDefinitions.RuntimeSettings)
    {
    }

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        var validationError = ValidateRequiredInputs(vm);
        if (validationError != null)
        {
            return validationError;
        }

        vm.IsBusy = true;
        vm.StatusMessage = StepMessages.ApiPrecheckInProgress;
        vm.ErrorMessage = "";

        try
        {
            var workflow = new RuntimeSettingsWorkflow(context, vm, token);
            var precheck = await workflow.RunApiPrecheckAsync();
            if (!precheck.Succeeded)
            {
                vm.StatusMessage = StepMessages.ApiKeyValidationFailed;
                return StepResult.Fail(precheck.Detail, canRetry: true);
            }

            var config = vm.BuildDeviceConfig();
            Serilog.Log.Information(
                "Config send flags wifi_enabled={Wifi} mining_enabled={Mining} ai_enabled={Ai}",
                config.WifiEnabled,
                config.MiningEnabled,
                config.AiEnabled);

            var sendAndApply = await workflow.SendAndApplyConfigAsync(config);
            if (!sendAndApply.Succeeded)
            {
                return StepResult.Fail(sendAndApply.Detail);
            }

            var verify = await workflow.VerifyPersistedFlagsWithRetryAsync(config);
            if (!verify.Succeeded)
            {
                return StepResult.Fail(verify.Detail, canRetry: true);
            }

            await workflow.CapturePostRebootLogIfEnabledAsync();
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            vm.StatusMessage = StepMessages.Cancelled;
            return StepResult.Cancelled();
        }
        catch (TimeoutException ex)
        {
            vm.ErrorMessage = ex.Message;
            vm.LastError = ex.Message;
            vm.StatusMessage = StepMessages.Timeout;
            return StepResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = StepMessages.ConfigApplyFailed;
            vm.LastError = ex.Message;
            vm.StatusMessage = StepMessages.ConfigApplyFailed;
            return StepResult.Fail(StepMessages.ConfigApplyFailed);
        }
        finally
        {
            vm.IsBusy = false;
        }
    }

    private static StepResult? ValidateRequiredInputs(MainViewModel vm)
    {
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail(StepMessages.ComPortNotSelected, canRetry: false);
        }

        if (vm.WifiEnabled && vm.AiEnabled && string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey) && !(vm.OpenAiKeyStored && vm.ReuseOpenAiKey))
        {
            return StepResult.Fail(StepMessages.OpenAiApiKeyRequired, canRetry: false);
        }

        if (vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled) && string.IsNullOrWhiteSpace(vm.AzureKey) && !(vm.AzureKeyStored && vm.ReuseAzureKey))
        {
            return StepResult.Fail(StepMessages.AzureKeyRequired, canRetry: false);
        }

        return null;
    }
}
