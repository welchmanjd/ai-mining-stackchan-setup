using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        vm.StatusMessage = "USBポートを検出しています...";
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
                vm.Step1Help = "ポートが見つかりません。USBケーブルとドライバを確認してください。";
                vm.StatusMessage = "未検出";
                return StepResult.Fail("ポートが見つかりません", guidance: "USB接続とドライバを確認してください。", canRetry: true);
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
            vm.Step1Help = "ファームウェア情報を取得できませんでした。接続先ポートを確認してください。";
            vm.StatusMessage = $"{vm.SelectedPort.DisplayName} を検出しました";
            return StepResult.Ok();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return StepResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Detect ports failed");
            vm.ErrorMessage = "ポート検出に失敗しました";
            vm.LastError = ex.Message;
            return StepResult.Fail("ポート検出に失敗しました", guidance: "USB接続とドライバを確認してください。");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
