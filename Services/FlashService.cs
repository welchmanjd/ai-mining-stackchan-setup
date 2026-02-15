using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public partial class FlashService
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
            var baudCandidates = new[] { baud, 460800, 115200 }.Distinct().ToArray();

            if (erase)
            {
                foreach (var eraseBaud in baudCandidates)
                {
                    var eraseArgs = $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud {eraseBaud} erase_flash";
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
                    var writeArgs = $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud {writeBaud} write_flash -z 0x0 \"{firmwarePath}\"";
                    var writeResult = await RunEsptoolAsync(writeArgs, portName, writeBaud, erase, firmwarePath, token);
                    lastEsptoolResult = writeResult;
                    if (writeResult.Success)
                    {
                        writeResult.Message = "書き込み成功 (esptool)";
                        return writeResult;
                    }

                    // Some adapters connect more reliably with --no-stub.
                    var noStubArgs = $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud {writeBaud} --no-stub write_flash -z 0x0 \"{firmwarePath}\"";
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
                var eraseArgs = $"erase-flash --non-interactive -c esp32 -p {portName}";
                var eraseResult = await RunToolAsync(espFlashPath, eraseArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (!eraseResult.Success)
                {
                    if (hasEsptool)
                    {
                        Log.Warning("espflash erase failed. Falling back to esptool.py erase_flash");
                        var eraseFallback = await RunEsptoolAsync($"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud 115200 erase_flash", portName, 115200, erase, firmwarePath, token);
                        if (!eraseFallback.Success)
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

            // Note: --no-stub might be needed for some usb-serial chips but usually standard flash is fine.
            // Specifying offset 0x0 is implied for single bin if not specified? 
            // espflash flash -p COMx -b 921600 file.bin addresses 0x0 by default for raw binaries provided as argument? 
            // Actually espflash usually expects a partition table or specific format. 
            // But if we give it a raw bin, we might need to specify address.
            // espflash write-bin 0x0 file.bin is the command for raw binaries in older versions, 
            // or `flash` command might strictly require partition table.
            // Let's check typical usage. "write-bin" is explicit.
            // However, esptool command was `write_flash -z 0x0`.
            // Let's try `write-bin 0x0` if `espflash` supports it, or `flash` regarding user's tool version.
            // Assuming modern espflash: `write-bin -p {port} -B {baud} 0x0 {firmware}`
            
            if (!espflashUnavailableForThisRun)
            {
                var flashArgs = $"write-bin --non-interactive -c esp32 -p {portName} -B {baud} 0x0 \"{firmwarePath}\"";
                var result = await RunToolAsync(espFlashPath, flashArgs, "espflash", portName, baud, erase, firmwarePath, token);
                if (result.Success)
                {
                    result.Message = "書き込み成功 (espflash)";
                    return result;
                }

                if (hasEsptool)
                {
                    Log.Warning("espflash failed. Falling back to esptool.py");
                    var fallbackArgs = $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud 115200 --no-stub write_flash -z 0x0 \"{firmwarePath}\"";
                    var fallback = await RunEsptoolAsync(fallbackArgs, portName, 115200, erase, firmwarePath, token);
                    fallback.Message = fallback.Success ? "書き込み成功 (esptool fallback)" : "書き込み失敗 (espflash + esptool)";
                    return fallback;
                }

                result.Message = "書き込み失敗 (espflash)";
                return result;
            }

            if (hasEsptool)
            {
                Log.Warning("Skipping espflash write due to prior espflash failure. Using esptool.py directly.");
                var fallbackArgs = $"--before default_reset --after hard_reset --chip esp32 --port {portName} --baud 115200 --no-stub write_flash -z 0x0 \"{firmwarePath}\"";
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
            if (lastEsptoolResult.Message.Contains("No serial data received", StringComparison.OrdinalIgnoreCase))
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
