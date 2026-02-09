using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Steps;

public sealed class FlashStep : StepBase
{
    public override int Index => 2;
    public override string Title => "書き込み";
    public override string Description => "ファームウェアを書き込みます。";
    public override string PrimaryActionText => "書き込み";
    public override bool CanRetry => true;

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail("COMポートが未選択です", canRetry: false);
        }

        if (vm.FlashModeSkip)
        {
            vm.FlashStatus = "書き込みをスキップしました";
            vm.StatusMessage = "ファームウェア書き込みをスキップ";
            return StepResult.Skipped();
        }

        if (string.IsNullOrWhiteSpace(vm.FirmwarePath) || !File.Exists(vm.FirmwarePath))
        {
            vm.ErrorMessage = "ファームウェアstackchan_core2_public.binがありません";
            return StepResult.Fail("ファームウェアが見つかりません", canRetry: false);
        }

        var fwName = Path.GetFileName(vm.FirmwarePath);
        if (string.IsNullOrWhiteSpace(fwName) ||
            !fwName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
            !fwName.Contains("_public", StringComparison.OrdinalIgnoreCase))
        {
            vm.ErrorMessage = "_public を含む .bin のみ対応しています";
            return StepResult.Fail("ファームウェアが無効です", canRetry: false);
        }

        if (!int.TryParse(vm.FlashBaud, out var baud))
        {
            return StepResult.Fail("Baudが不正です", canRetry: false);
        }

        if (vm.FlashModeOverwrite)
        {
            var manifest = FirmwareManifest.FromFirmwarePath(vm.FirmwarePath);
            if (manifest != null)
            {
                var infoResult = await context.SerialService.GetInfoAsync(vm.SelectedPort.PortName, token);
                var device = infoResult.Info;
                if (infoResult.Success && device != null)
                {
                    var sameVer = !string.IsNullOrWhiteSpace(manifest.Ver) &&
                                  string.Equals(manifest.Ver, device.Ver, StringComparison.OrdinalIgnoreCase);
                    var sameBuild = !string.IsNullOrWhiteSpace(manifest.BuildId) &&
                                    string.Equals(manifest.BuildId, device.BuildId, StringComparison.OrdinalIgnoreCase);
                    var same = sameBuild || (string.IsNullOrWhiteSpace(manifest.BuildId) && sameVer);
                    if (same)
                    {
                        vm.FlashStatus = $"書き込み不要（ver={device.Ver}, build={device.BuildId}）";
                        vm.StatusMessage = "同一ファームと判定したため書き込みをスキップしました";
                        return StepResult.Skipped();
                    }
                }
            }
        }

        var erase = vm.FlashModeErase;
        vm.IsBusy = true;
        vm.FlashStatus = "書き込み中...";
        vm.StatusMessage = "";

        try
        {
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
                vm.FlashStatus = "書き込み完了";
                return StepResult.Ok();
            }

            vm.FlashStatus = "書き込み失敗";
            vm.ErrorMessage = $"書き込みに失敗しました。ログ: {result.LogPath}";
            vm.PrimaryButtonText = "再試行";
            return StepResult.Fail("書き込みに失敗しました", guidance: "書き込みログを確認してください。", canRetry: true);
        }
        catch (OperationCanceledException)
        {
            vm.FlashStatus = "中止しました";
            return StepResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Flash failed");
            vm.ErrorMessage = $"書き込みに失敗しました。ログ: {AiStackchanSetup.Services.LogService.FlashLogPath}";
            vm.LastError = ex.Message;
            vm.PrimaryButtonText = "再試行";
            return StepResult.Fail("書き込みに失敗しました", guidance: "書き込みログを確認してください。", canRetry: true);
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
