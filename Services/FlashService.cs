using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public partial class FlashService : IFlashService
{
    private readonly object _activeProcessesLock = new();
    private readonly HashSet<Process> _activeProcesses = new();

    public string? PlatformIoHome { get; set; }

    public async Task<FlashResult> FlashAsync(string portName, int baud, bool erase, string firmwarePath, CancellationToken token)
    {
        Directory.CreateDirectory(LogService.LogDirectory);

        if (!File.Exists(firmwarePath))
        {
            return await FailWithLogAsync(
                "ファームウェアファイルが見つかりません。Resources/firmware を確認してください。",
                "none",
                portName,
                baud,
                erase,
                firmwarePath,
                token);
        }

        var hasEsptool = IsEsptoolAvailable();
        FlashResult? lastEsptoolResult = null;

        // Try esptool with safer connection profiles first.
        if (hasEsptool)
        {
            var baudCandidates = FlashCommandPlan.BuildBaudCandidates(baud);

            if (erase)
            {
                foreach (var eraseBaud in baudCandidates)
                {
                    var eraseArgs = FlashCommandPlan.BuildEsptoolEraseArgs(portName, eraseBaud);
                    var eraseResult = await RunEsptoolAsync(eraseArgs, portName, eraseBaud, erase, firmwarePath, token);
                    lastEsptoolResult = eraseResult;
                    if (eraseResult.Success)
                    {
                        break;
                    }
                }
            }

            if (lastEsptoolResult == null || lastEsptoolResult.Success)
            {
                foreach (var writeBaud in baudCandidates)
                {
                    var writeArgs = FlashCommandPlan.BuildEsptoolWriteArgs(portName, writeBaud, firmwarePath, noStub: false);
                    var writeResult = await RunEsptoolAsync(writeArgs, portName, writeBaud, erase, firmwarePath, token);
                    lastEsptoolResult = writeResult;
                    if (writeResult.Success)
                    {
                        writeResult.Message = "書き込み成功 (esptool)";
                        return writeResult;
                    }

                    // Some adapters connect more reliably with --no-stub.
                    var noStubArgs = FlashCommandPlan.BuildEsptoolWriteArgs(portName, writeBaud, firmwarePath, noStub: true);
                    writeResult = await RunEsptoolAsync(noStubArgs, portName, writeBaud, erase, firmwarePath, token);
                    lastEsptoolResult = writeResult;
                    if (writeResult.Success)
                    {
                        writeResult.Message = "書き込み成功 (esptool --no-stub)";
                        return writeResult;
                    }
                }
            }
        }

        // Try to use bundled espflash first
        var espFlashPath = ResolveEspFlashPath();
        if (!string.IsNullOrWhiteSpace(espFlashPath))
        {
            var espflashUnavailableForThisRun = false;

            if (erase)
            {
                var eraseArgs = FlashCommandPlan.BuildEspflashEraseArgs(portName);
                var eraseResult = await RunToolAsync(espFlashPath, eraseArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (eraseResult.IsFailure)
                {
                    if (hasEsptool)
                    {
                        Log.Warning("flash.espflash.erase_failed fallback=esptool");
                        var eraseFallbackArgs = FlashCommandPlan.BuildEsptoolEraseArgs(portName, 115200);
                        var eraseFallback = await RunEsptoolAsync(eraseFallbackArgs, portName, 115200, erase, firmwarePath, token);
                        if (eraseFallback.IsFailure)
                        {
                            eraseFallback.Message = "erase_flash 失敗 (espflash + esptool)";
                            return eraseFallback;
                        }
                        espflashUnavailableForThisRun = true;
                    }
                    else
                    {
                        eraseResult.Message = "espflash erase-flash 失敗";
                        return eraseResult;
                    }
                }
            }
            // Use espflash write-bin with explicit 0x0 offset for raw firmware binaries.
            // Keep esptool fallback for compatibility across adapter/chip combinations.
            
            if (!espflashUnavailableForThisRun)
            {
                var flashArgs = FlashCommandPlan.BuildEspflashWriteArgs(portName, baud, firmwarePath);
                var result = await RunToolAsync(espFlashPath, flashArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (result.Success)
                {
                    result.Message = "書き込み成功 (espflash)";
                    return result;
                }

                if (hasEsptool)
                {
                    Log.Warning("flash.espflash.write_failed fallback=esptool");
                    var fallbackArgs = FlashCommandPlan.BuildEsptoolWriteArgs(portName, 115200, firmwarePath, noStub: true);
                    var fallback = await RunEsptoolAsync(fallbackArgs, portName, 115200, erase, firmwarePath, token);
                    fallback.Message = fallback.Success ? "書き込み成功 (esptool fallback)" : "書き込み失敗 (espflash + esptool)";
                    return fallback;
                }

                result.Message = "書き込み失敗 (espflash)";
                return result;
            }

            if (hasEsptool)
            {
                Log.Warning("flash.espflash.write_skipped_using_esptool");
                var fallbackArgs = FlashCommandPlan.BuildEsptoolWriteArgs(portName, 115200, firmwarePath, noStub: true);
                var fallback = await RunEsptoolAsync(fallbackArgs, portName, 115200, erase, firmwarePath, token);
                fallback.Message = fallback.Success ? "書き込み成功 (esptool direct)" : "書き込み失敗 (esptool direct)";
                return fallback;
            }

            return await FailWithLogAsync(
                "espflash が接続できず、esptool.py も利用できません。",
                "none",
                portName,
                baud,
                erase,
                firmwarePath,
                token);
        }

        if (lastEsptoolResult != null)
        {
            if (FlashOutputLogic.IsLikelyConnectFailure(lastEsptoolResult.Message))
            {
                lastEsptoolResult.Message = "ESP32に接続できません。USBケーブル(データ通信対応)を確認し、M5Stackを再起動して再試行してください。";
            }

            return lastEsptoolResult;
        }

        return await FailWithLogAsync(
            "書き込みツールが見つかりません。esptool.py または espflash.exe を確認してください。",
            "none",
            portName,
            baud,
            erase,
            firmwarePath,
            token);
    }

}
