using System;
using System.IO;

namespace AiStackchanSetup.Services;

public partial class FlashService
{
    private string? ResolveEspFlashPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = Path.Combine(baseDir, "tools", "espflash.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private string? ResolvePythonPath()
    {
        var home = GetPlatformIoHome();
        var candidate = Path.Combine(home, "penv", "Scripts", "python.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private string? ResolveEsptoolPyPath()
    {
        var home = GetPlatformIoHome();
        var candidate = Path.Combine(home, "packages", "tool-esptoolpy", "esptool.py");
        return File.Exists(candidate) ? candidate : null;
    }

    private string GetPlatformIoHome()
    {
        if (!string.IsNullOrWhiteSpace(PlatformIoHome))
        {
            return PlatformIoHome;
        }

        var env = Environment.GetEnvironmentVariable("PLATFORMIO_HOME_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".platformio");
    }

    private bool IsEsptoolAvailable()
    {
        var pythonPath = ResolvePythonPath();
        var esptoolPyPath = ResolveEsptoolPyPath();
        return !string.IsNullOrWhiteSpace(pythonPath) && !string.IsNullOrWhiteSpace(esptoolPyPath);
    }
}
