using System;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Infrastructure;
using Serilog;

namespace AiStackchanSetup.Steps;

public sealed class DetectPortsStep : StepBase
{
    public override int Index => 1;
    public override string Title => "接続";
    public override string Description => "USBポートを検出します。";
    public override string PrimaryActionText => "探す";

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        vm.IsBusy = true;
        vm.StatusMessage = "USBポートを探しています...";
        vm.Step1Help = "";

        try
        {
            vm.Ports.Clear();

            var ports = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.DetectPortsAsync(ct),
                context.Timeouts.PortDetect,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                token);

            foreach (var port in ports)
            {
                vm.Ports.Add(port);
            }

            vm.SelectedPort = context.SerialService.SelectBestPort(vm.Ports);

            if (vm.SelectedPort == null)
            {
                vm.Step1Help = "見つかりません。充電専用ケーブル/USBポート/ドライバを確認してください。";
                vm.StatusMessage = "未検出";
                return StepResult.Fail("ポートが見つかりません", guidance: "USB接続やドライバを確認してください。", canRetry: true);
            }

            vm.StatusMessage = $"{vm.SelectedPort.DisplayName} を検出";
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            return StepResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Detect ports failed");
            vm.ErrorMessage = "ポート検出に失敗しました";
            vm.LastError = ex.Message;
            return StepResult.Fail("ポート検出に失敗しました", guidance: "USB接続やドライバを確認してください。");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
