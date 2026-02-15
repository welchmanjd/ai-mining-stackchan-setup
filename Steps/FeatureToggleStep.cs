using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class FeatureToggleStep : StepBase
{
    public FeatureToggleStep() : base(StepDefinitions.FeatureToggle)
    {
    }

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail(StepText.ComPortNotSelected, canRetry: false);
        }

        return await ExecuteBusyStepAsync(
            context,
            token,
            async () =>
            {
                var hello = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.SerialService.HelloAsync(vm.SelectedPort.PortName, ct),
                    context.Timeouts.Hello,
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    token);
                if (hello.IsFailure)
                {
                    return StepResult.Fail(hello.Message, guidance: StepText.UsbConnectionAndPortGuidance);
                }

                var ping = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.SerialService.PingAsync(vm.SelectedPort.PortName, ct),
                    context.Timeouts.Hello,
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    token);
                if (ping.IsFailure)
                {
                    return StepResult.Fail(ping.Message, guidance: StepText.UsbConnectionAndPortGuidance);
                }

                var info = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.SerialService.GetInfoAsync(vm.SelectedPort.PortName, ct),
                    context.Timeouts.Hello,
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    token);
                if (info.IsFailure)
                {
                    return StepResult.Fail(info.Message, guidance: StepText.UsbConnectionAndPortGuidance);
                }

                vm.DeviceStatusSummary = info.Info != null ? info.Info.ToSummary() : StepText.DeviceSettingsNotAcquired;
                vm.UpdateCurrentFirmwareInfo(info.Info);
                vm.DeviceInfoJson = info.RawJson;
                vm.LastProtocolResponse = context.SerialService.LastProtocolResponse;

                var cfg = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.SerialService.GetConfigJsonAsync(vm.SelectedPort.PortName, ct),
                    context.Timeouts.Hello,
                    maxAttempts: 2,
                    baseDelay: TimeSpan.FromMilliseconds(300),
                    backoffFactor: 2,
                    token);
                if (!cfg.IsFailure && !string.IsNullOrWhiteSpace(cfg.Json))
                {
                    // Keep user's ON/OFF selection from this screen.
                    var wifiEnabled = vm.WifiEnabled;
                    var miningEnabled = vm.MiningEnabled;
                    var aiEnabled = vm.AiEnabled;
                    vm.ApplyConfigSnapshot(cfg.Json);
                    vm.WifiEnabled = wifiEnabled;
                    vm.MiningEnabled = miningEnabled;
                    vm.AiEnabled = aiEnabled;
                }

                // Clear transient load status to avoid stale success text in later steps.
                vm.StatusMessage = string.Empty;
                return StepResult.Ok();
            },
            before: vmLocal =>
            {
                vmLocal.StatusMessage = StepText.DeviceSettingsLoadInProgress;
                vmLocal.DeviceStatusSummary = StepText.DeviceSettingsLoading;
                vmLocal.DeviceInfoJson = "";
                vmLocal.LastProtocolResponse = "";
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
                vmLocal.ErrorMessage = StepText.SettingsLoadFailed;
                vmLocal.LastError = ex.Message;
                vmLocal.StatusMessage = StepText.SettingsLoadFailed;
                return StepResult.Fail(StepText.SettingsLoadFailed);
            });
    }
}

