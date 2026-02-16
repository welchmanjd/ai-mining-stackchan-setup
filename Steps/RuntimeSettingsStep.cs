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

        return await ExecuteBusyStepAsync(
            context,
            token,
            async () =>
            {
                var workflow = new RuntimeSettingsWorkflow(context, vm, token);
                var precheck = await workflow.RunApiPrecheckAsync();
                if (!precheck.Succeeded)
                {
                    vm.StatusMessage = StepText.ApiKeyValidationFailed;
                    return StepResult.Fail(precheck.Detail, canRetry: true);
                }

                var config = vm.BuildDeviceConfig();
                Serilog.Log.Information(
                    "step.runtime_settings.config_flags wifi_enabled={WifiEnabled} mining_enabled={MiningEnabled} ai_enabled={AiEnabled}",
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
            },
            before: vmLocal =>
            {
                vmLocal.StatusMessage = StepText.ApiPrecheckInProgress;
                vmLocal.ErrorMessage = "";
            },
            onCancelled: (vmLocal, _) =>
            {
                vmLocal.StatusMessage = StepText.Cancelled;
                return StepResult.Cancelled();
            },
            onTimeout: (vmLocal, ex) =>
            {
                vmLocal.ErrorMessage = ex.Message;
                vmLocal.LastError = ex.Message;
                vmLocal.StatusMessage = StepText.Timeout;
                return StepResult.Fail(ex.Message);
            },
            onError: (vmLocal, ex) =>
            {
                vmLocal.ErrorMessage = StepText.ConfigApplyFailed;
                vmLocal.LastError = ex.Message;
                vmLocal.StatusMessage = StepText.ConfigApplyFailed;
                return StepResult.Fail(StepText.ConfigApplyFailed);
            });
    }

    private static StepResult? ValidateRequiredInputs(MainViewModel vm)
    {
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail(StepText.ComPortNotSelected, canRetry: false);
        }

        if (vm.WifiEnabled && vm.AiEnabled && string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey) && !(vm.OpenAiKeyStored && vm.ReuseOpenAiKey))
        {
            return StepResult.Fail(StepText.OpenAiApiKeyRequired, canRetry: false);
        }

        if (vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled) && string.IsNullOrWhiteSpace(vm.AzureKey) && !(vm.AzureKeyStored && vm.ReuseAzureKey))
        {
            return StepResult.Fail(StepText.AzureKeyRequired, canRetry: false);
        }

        return null;
    }
}

