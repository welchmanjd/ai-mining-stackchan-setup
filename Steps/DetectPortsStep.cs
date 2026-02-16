using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup;
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
        return await ExecuteBusyStepAsync(
            context,
            token,
            async () =>
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
                    vm.Step1Help = StepText.PortNotFoundHelp;
                    vm.StatusMessage = StepText.PortNotDetected;
                    return StepResult.Fail(StepText.PortNotFound, guidance: StepText.UsbConnectionAndDriverGuidance, canRetry: true);
                }

                try
                {
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
                            vm.StatusMessage = string.Format(UiText.PortDetectedWithFirmwareInfoFormat, candidate.DisplayName);
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
                        vm.StatusMessage = string.Format(UiText.PortDetectedWithBootLogInfoFormat, vm.SelectedPort.DisplayName);
                        return StepResult.Ok();
                    }

                    vm.UpdateCurrentFirmwareInfo(null);
                    vm.IsManualPortSelection = true;
                    vm.Step1Help = StepText.FirmwareInfoNotAvailableHelp;
                    vm.StatusMessage = string.Format(UiText.PortDetectedFormat, vm.SelectedPort.DisplayName);
                    return StepResult.Ok();
                }
                finally
                {
                    context.SerialService.Close();
                }
            },
            before: vmLocal =>
            {
                vmLocal.StatusMessage = StepText.PortDetectionInProgress;
                vmLocal.Step1Help = "";
                vmLocal.UpdateCurrentFirmwareInfo(null);
            },
            onCancelled: (vmLocal, _) =>
            {
                vmLocal.StatusMessage = StepText.Cancelled;
                return StepResult.Cancelled();
            },
            onError: (vmLocal, ex) =>
            {
                Log.Error(ex, "step.detect_ports.failed");
                vmLocal.ErrorMessage = StepText.PortDetectionFailed;
                vmLocal.LastError = ex.Message;
                return StepResult.Fail(StepText.PortDetectionFailed, guidance: StepText.UsbConnectionAndDriverGuidance);
            });
    }
}

