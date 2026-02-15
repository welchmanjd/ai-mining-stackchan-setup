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
            return StepResult.Fail(StepMessages.ComPortNotSelected, canRetry: false);
        }

        vm.IsBusy = true;
        vm.StatusMessage = StepMessages.DeviceSettingsLoadInProgress;
        vm.DeviceStatusSummary = StepMessages.DeviceSettingsLoading;
        vm.DeviceInfoJson = "";
        vm.LastProtocolResponse = "";

        try
        {
            var hello = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.HelloAsync(vm.SelectedPort.PortName, ct),
                context.Timeouts.Hello,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                token);
            if (!hello.Success)
            {
                return StepResult.Fail(hello.Message, guidance: StepMessages.UsbConnectionAndPortGuidance);
            }

            var ping = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.PingAsync(vm.SelectedPort.PortName, ct),
                context.Timeouts.Hello,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                token);
            if (!ping.Success)
            {
                return StepResult.Fail(ping.Message, guidance: StepMessages.UsbConnectionAndPortGuidance);
            }

            var info = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.GetInfoAsync(vm.SelectedPort.PortName, ct),
                context.Timeouts.Hello,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                token);
            if (!info.Success)
            {
                return StepResult.Fail(info.Message, guidance: StepMessages.UsbConnectionAndPortGuidance);
            }

            vm.DeviceStatusSummary = info.Info != null ? info.Info.ToSummary() : StepMessages.DeviceSettingsNotAcquired;
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
            if (cfg.Success && !string.IsNullOrWhiteSpace(cfg.Json))
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

            vm.StatusMessage = StepMessages.DeviceSettingsLoadCompleted;
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
            vm.ErrorMessage = StepMessages.SettingsLoadFailed;
            vm.LastError = ex.Message;
            vm.StatusMessage = StepMessages.SettingsLoadFailed;
            return StepResult.Fail(StepMessages.SettingsLoadFailed);
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
