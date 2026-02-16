using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup;
using Serilog;

namespace AiStackchanSetup.Steps;

public sealed class FlashStep : StepBase
{
    public FlashStep() : base(StepDefinitions.Flash)
    {
    }

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail(StepText.ComPortNotSelected, canRetry: false);
        }

        if (vm.FlashModeSkip)
        {
            vm.FlashStatus = StepText.FlashSkipped;
            vm.StatusMessage = StepText.FlashSkipStatus;
            return StepResult.Skipped();
        }

        if (string.IsNullOrWhiteSpace(vm.FirmwarePath) || !File.Exists(vm.FirmwarePath))
        {
            vm.ErrorMessage = StepText.FirmwareNotFoundError;
            return StepResult.Fail(StepText.FirmwareNotFound, canRetry: false);
        }

        var fwName = Path.GetFileName(vm.FirmwarePath);
        if (string.IsNullOrWhiteSpace(fwName) ||
            !fwName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
            !fwName.Contains("_public", StringComparison.OrdinalIgnoreCase))
        {
            vm.ErrorMessage = StepText.FirmwarePublicBinOnly;
            return StepResult.Fail(StepText.FirmwareFormatInvalid, canRetry: false);
        }

        if (!int.TryParse(vm.FlashBaud, out var baud))
        {
            return StepResult.Fail(StepText.FlashBaudNotNumeric, canRetry: false);
        }

        var erase = vm.FlashModeErase;
        return await ExecuteBusyStepAsync(
            context,
            token,
            async () =>
            {
                context.SerialService.Close();
                await Task.Delay(800, token);

                var result = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.FlashService.FlashAsync(vm.SelectedPort.PortName, baud, erase, vm.FirmwarePath, ct),
                    context.Timeouts.Flash,
                    maxAttempts: 1,
                    baseDelay: TimeSpan.Zero,
                    backoffFactor: 1,
                    token);

                vm.LastFlashResult = result.Success ? UiText.SuccessResultCode : UiText.FailResultCode;

                if (result.Success)
                {
                    vm.FlashStatus = StepText.FlashCompleted;
                    return StepResult.Ok();
                }

                vm.FlashStatus = StepText.FlashStatusFailed;
                vm.ErrorMessage = $"{StepText.FlashWriteFailed} / Port: {vm.SelectedPort.PortName} / Log: {result.LogPath}";
                vm.PrimaryButtonText = UiText.Retry;
                return StepResult.Fail(StepText.FlashWriteFailed, guidance: StepText.RetryByCheckingFlashLog, canRetry: true);
            },
            before: vmLocal =>
            {
                vmLocal.FlashStatus = StepText.FlashInProgress;
                vmLocal.StatusMessage = "";
            },
            onCancelled: (vmLocal, _) =>
            {
                vmLocal.FlashStatus = StepText.Cancelled;
                return StepResult.Cancelled();
            },
            onError: (vmLocal, ex) =>
            {
                Log.Error(ex, "step.flash.failed");
                vmLocal.ErrorMessage = $"{StepText.FlashWriteFailed} / Log: {Services.LogService.FlashLogPath}";
                vmLocal.LastError = ex.Message;
                vmLocal.PrimaryButtonText = UiText.Retry;
                return StepResult.Fail(StepText.FlashWriteFailed, guidance: StepText.RetryByCheckingFlashLog, canRetry: true);
            });
    }
}

