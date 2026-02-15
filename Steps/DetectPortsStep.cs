using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AiStackchanSetup.Steps;

public sealed class DetectPortsStep : StepBase
{
    public DetectPortsStep() : base(StepDefinitions.DetectPorts)
    {
    }

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        vm.IsBusy = true;
        vm.StatusMessage = StepMessages.PortDetectionInProgress;
        vm.Step1Help = "";
        vm.UpdateCurrentFirmwareInfo(null);

        try
        {
            vm.Ports.Clear();

            var ports = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.DetectPortsAsync(ct),
                context.Timeouts.PortDetect,
                maxAttempts: 2,
                baseDelay: TimeSpan.FromMilliseconds(250),
                backoffFactor: 1.5,
                token);

            foreach (var port in ports)
            {
                vm.Ports.Add(port);
            }

            vm.SelectedPort = context.SerialService.SelectBestPort(vm.Ports);
            if (vm.SelectedPort == null)
            {
                vm.Step1Help = StepMessages.PortNotFoundHelp;
                vm.StatusMessage = StepMessages.PortNotDetected;
                return StepResult.Fail(StepMessages.PortNotFound, guidance: StepMessages.UsbConnectionAndDriverGuidance, canRetry: true);
            }

            var infoTimeout = TimeSpan.FromMilliseconds(1200);
            var candidates = vm.Ports.Take(2).ToArray();
            foreach (var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                var info = await context.SerialService.GetInfoAsync(candidate.PortName, infoTimeout, token);
                if (info.Success && info.Info != null)
                {
                    vm.SelectedPort = candidate;
                    vm.UpdateCurrentFirmwareInfo(info.Info);
                    vm.StatusMessage = $"{candidate.DisplayName} を検出しました (FW情報取得済み)";
                    return StepResult.Ok();
                }
            }

            var bannerInfo = await context.SerialService.ReadBootBannerInfoAsync(
                vm.SelectedPort.PortName,
                TimeSpan.FromMilliseconds(1200),
                token);

            if (bannerInfo != null)
            {
                vm.UpdateCurrentFirmwareInfo(bannerInfo);
                vm.StatusMessage = $"{vm.SelectedPort.DisplayName} を検出しました (起動ログからFW情報取得)";
                return StepResult.Ok();
            }

            vm.UpdateCurrentFirmwareInfo(null);
            vm.IsManualPortSelection = true;
            vm.Step1Help = StepMessages.FirmwareInfoNotAvailableHelp;
            vm.StatusMessage = $"{vm.SelectedPort.DisplayName} を検出しました";
            return StepResult.Ok();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            vm.StatusMessage = StepMessages.Cancelled;
            return StepResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Detect ports failed");
            vm.ErrorMessage = StepMessages.PortDetectionFailed;
            vm.LastError = ex.Message;
            return StepResult.Fail(StepMessages.PortDetectionFailed, guidance: StepMessages.UsbConnectionAndDriverGuidance);
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
