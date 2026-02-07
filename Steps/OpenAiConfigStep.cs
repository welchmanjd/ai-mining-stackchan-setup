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
    public override string Description => "設定をデバイスに送信します。";
    public override string PrimaryActionText => "保存してデバイスに送る";

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
        vm.StatusMessage = "デバイスに設定を送信中...";

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
                return StepResult.Fail(hello.Message, guidance: "接続を確認し再試行してください。");
            }

            if (hello.Info != null)
            {
                vm.DeviceStatusSummary = hello.Info.ToSummary();
            }

            var config = new DeviceConfig
            {
                WifiSsid = vm.ConfigWifiSsid,
                WifiPassword = vm.ConfigWifiPassword,
                DucoUser = vm.DucoUser,
                DucoMinerKey = vm.DucoMinerKey,
                OpenAiKey = vm.ConfigOpenAiKey,
                AzureKey = vm.AzureKey,
                AzureRegion = vm.AzureRegion,
                AzureCustomSubdomain = vm.AzureCustomSubdomain
            };

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
                return StepResult.Fail($"設定送信失敗: {setResult.Message}");
            }

            var applyResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.ApplyConfigAsync(vm.SelectedPort.PortName, ct),
                context.Timeouts.ApplyConfig,
                maxAttempts: 2,
                baseDelay: TimeSpan.FromMilliseconds(600),
                backoffFactor: 2,
                token);

            if (!applyResult.Success)
            {
                vm.ErrorMessage = $"設定適用失敗: {applyResult.Message}";
                vm.LastError = applyResult.Message;
                return StepResult.Fail($"設定適用失敗: {applyResult.Message}");
            }

            vm.StatusMessage = "設定送信完了";
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            vm.StatusMessage = "中止しました";
            return StepResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Send config failed");
            vm.ErrorMessage = "設定送信に失敗しました";
            vm.LastError = ex.Message;
            return StepResult.Fail("設定送信に失敗しました");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
