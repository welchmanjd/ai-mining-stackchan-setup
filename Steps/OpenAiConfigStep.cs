using System;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Steps;

public sealed class OpenAiConfigStep : StepBase
{
    public override int Index => 6;
    public override string Title => "OpenAI";
    public override string Description => "デバイスへ設定を送信します。";
    public override string PrimaryActionText => "送信";

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail("COMポートが未選択です", canRetry: false);
        }

        if (string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey))
        {
            return StepResult.Fail("OpenAI APIキーが未入力です", canRetry: false);
        }

        vm.IsBusy = true;
        vm.StatusMessage = "接続確認中...";
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
                vm.StatusMessage = "接続確認に失敗しました";
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
                vm.StatusMessage = "接続確認に失敗しました";
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
                vm.StatusMessage = "接続確認に失敗しました";
                return StepResult.Fail(info.Message, guidance: "USB接続とCOMポート選択を確認してください。");
            }

            if (info.Info != null)
            {
                vm.DeviceStatusSummary = info.Info.ToSummary();
            }
            else
            {
                vm.DeviceStatusSummary = "未取得";
            }

            vm.DeviceInfoJson = info.RawJson;
            vm.LastProtocolResponse = context.SerialService.LastProtocolResponse;

            vm.StatusMessage = "設定送信中...";
            var config = vm.BuildDeviceConfig();

            var setResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.SendConfigAsync(vm.SelectedPort.PortName, config, ct),
                context.Timeouts.SendConfig,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                token);

            if (!setResult.Success)
            {
                vm.ErrorMessage = $"設定送信失敗: {setResult.Message}";
                vm.LastError = setResult.Message;
                vm.StatusMessage = "設定送信に失敗しました";
                return StepResult.Fail($"設定送信失敗: {setResult.Message}");
            }
            if (!string.IsNullOrWhiteSpace(setResult.Message) && !setResult.Message.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                vm.StatusMessage = setResult.Message;
            }

            vm.StatusMessage = "設定保存中...";
            var applyResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.ApplyConfigAsync(vm.SelectedPort.PortName, ct),
                context.Timeouts.ApplyConfig,
                maxAttempts: 2,
                baseDelay: TimeSpan.FromMilliseconds(600),
                backoffFactor: 2,
                token);

            if (!applyResult.Success)
            {
                vm.ErrorMessage = $"設定保存失敗: {applyResult.Message}";
                vm.LastError = applyResult.Message;
                vm.StatusMessage = "設定保存に失敗しました";
                return StepResult.Fail($"設定保存失敗: {applyResult.Message}");
            }

            vm.StatusMessage = "設定送信完了";
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
            Log.Error(ex, "Send config failed");
            vm.ErrorMessage = "設定送信に失敗しました";
            vm.LastError = ex.Message;
            vm.StatusMessage = "設定送信に失敗しました";
            return StepResult.Fail("設定送信に失敗しました");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
