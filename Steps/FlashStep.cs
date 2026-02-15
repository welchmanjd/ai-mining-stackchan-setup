using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            return StepResult.Fail(StepMessages.ComPortNotSelected, canRetry: false);
        }

        if (vm.FlashModeSkip)
        {
            vm.FlashStatus = StepMessages.FlashSkipped;
            vm.StatusMessage = StepMessages.FlashSkipStatus;
            return StepResult.Skipped();
        }

        if (string.IsNullOrWhiteSpace(vm.FirmwarePath) || !File.Exists(vm.FirmwarePath))
        {
            vm.ErrorMessage = StepMessages.FirmwareNotFoundError;
            return StepResult.Fail(StepMessages.FirmwareNotFound, canRetry: false);
        }

        var fwName = Path.GetFileName(vm.FirmwarePath);
        if (string.IsNullOrWhiteSpace(fwName) ||
            !fwName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
            !fwName.Contains("_public", StringComparison.OrdinalIgnoreCase))
        {
            vm.ErrorMessage = StepMessages.FirmwarePublicBinOnly;
            return StepResult.Fail(StepMessages.FirmwareFormatInvalid, canRetry: false);
        }

        if (!int.TryParse(vm.FlashBaud, out var baud))
        {
            return StepResult.Fail(StepMessages.FlashBaudNotNumeric, canRetry: false);
        }

        var erase = vm.FlashModeErase;
        vm.IsBusy = true;
        vm.FlashStatus = StepMessages.FlashInProgress;
        vm.StatusMessage = "";

        try
        {
            context.SerialService.Close();
            await Task.Delay(300, token);

            var result = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.FlashService.FlashAsync(vm.SelectedPort.PortName, baud, erase, vm.FirmwarePath, ct),
                context.Timeouts.Flash,
                maxAttempts: 1,
                baseDelay: TimeSpan.Zero,
                backoffFactor: 1,
                token);

            vm.LastFlashResult = result.Success ? "success" : "fail";

            if (result.Success)
            {
                vm.FlashStatus = StepMessages.FlashCompleted;
                return StepResult.Ok();
            }

            vm.FlashStatus = StepMessages.FlashStatusFailed;
            vm.ErrorMessage = $"{StepMessages.FlashWriteFailed}（ポート: {vm.SelectedPort.PortName}）。接続手順に戻ってポートを確認してください。ログ: {result.LogPath}";
            vm.PrimaryButtonText = "再試行";
            return StepResult.Fail(StepMessages.FlashWriteFailed, guidance: StepMessages.RetryByCheckingFlashLog, canRetry: true);
        }
        catch (OperationCanceledException)
        {
            vm.FlashStatus = StepMessages.Cancelled;
            return StepResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Flash failed");
            vm.ErrorMessage = $"{StepMessages.FlashWriteFailed}。ログ: {AiStackchanSetup.Services.LogService.FlashLogPath}";
            vm.LastError = ex.Message;
            vm.PrimaryButtonText = "再試行";
            return StepResult.Fail(StepMessages.FlashWriteFailed, guidance: StepMessages.RetryByCheckingFlashLog, canRetry: true);
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
