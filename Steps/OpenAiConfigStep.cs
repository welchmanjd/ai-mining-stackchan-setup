using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class OpenAiConfigStep : StepBase
{
    public override int Index => 6;
    public override string Title => "OpenAI";
    public override string Description => "OpenAI APIキーと機能ON/OFFを確認します。";
    public override string PrimaryActionText => "次へ";

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail("COMポートが未選択です", canRetry: false);
        }

        if (vm.AiEnabled && string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey))
        {
            return StepResult.Fail("OpenAI APIキーが未入力です", canRetry: false);
        }

        vm.IsBusy = true;
        vm.StatusMessage = "デバイス接続確認中...";
        vm.DeviceStatusSummary = "確認中";
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
                vm.ErrorMessage = hello.Message;
                vm.LastError = hello.Message;
                vm.StatusMessage = "デバイス確認に失敗しました";
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
                vm.ErrorMessage = ping.Message;
                vm.LastError = ping.Message;
                vm.StatusMessage = "デバイス確認に失敗しました";
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
                vm.ErrorMessage = info.Message;
                vm.LastError = info.Message;
                vm.StatusMessage = "デバイス確認に失敗しました";
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
            vm.StatusMessage = "OpenAI設定の確認が完了しました。";
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            vm.StatusMessage = "中止しました";
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
            vm.ErrorMessage = "設定確認に失敗しました";
            vm.LastError = ex.Message;
            vm.StatusMessage = "設定確認に失敗しました";
            return StepResult.Fail("設定確認に失敗しました");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
