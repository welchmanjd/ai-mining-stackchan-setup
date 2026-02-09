using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class FeatureToggleStep : StepBase
{
    public override int Index => 3;
    public override string Title => "機能";
    public override string Description => "通信・マイニング・AI の有効/無効を設定します。";
    public override string PrimaryActionText => "次へ";

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail("COMポートが選択されていません", canRetry: false);
        }

        vm.IsBusy = true;
        vm.StatusMessage = "デバイス設定を読み込み中...";
        vm.DeviceStatusSummary = "読込中";
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
                return StepResult.Fail(hello.Message, guidance: "USB接続とCOMポート選択を確認してください。");
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
                return StepResult.Fail(ping.Message, guidance: "USB接続とCOMポート選択を確認してください。");
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
                return StepResult.Fail(info.Message, guidance: "USB接続とCOMポート選択を確認してください。");
            }

            vm.DeviceStatusSummary = info.Info != null ? info.Info.ToSummary() : "未取得";
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
                vm.ApplyConfigSnapshot(cfg.Json);
            }

            vm.StatusMessage = "デバイス設定の読み込みが完了しました。";
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            vm.StatusMessage = "中断しました";
            return StepResult.Cancelled();
        }
        catch (TimeoutException ex)
        {
            vm.ErrorMessage = ex.Message;
            vm.LastError = ex.Message;
            vm.StatusMessage = "タイムアウトしました";
            return StepResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "設定読み込みに失敗しました";
            vm.LastError = ex.Message;
            vm.StatusMessage = "設定読み込みに失敗しました";
            return StepResult.Fail("設定読み込みに失敗しました");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
