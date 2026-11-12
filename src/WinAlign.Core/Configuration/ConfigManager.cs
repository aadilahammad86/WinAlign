using System.Text.Json;
using Microsoft.Win32;
using WinAlign.Core.Models;

namespace WinAlign.Core.Configuration;

public static class ConfigManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinAlign");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "WinAlign";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AlignerConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AlignerConfig>(json, JsonOptions);
                if (config != null)
                {
                    config.StartWithWindows = IsStartupEnabled();

                    // Migration: Ensure 'explorer' and 'taskmgr' are removed from ExcludedProcesses
                    // so File Explorer (CabinetWClass) and Task Manager are eligible for alignment
                    int removedCount = config.ExcludedProcesses.RemoveAll(p =>
                        string.Equals(p, "explorer", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "taskmgr", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "taskmgr.exe", StringComparison.OrdinalIgnoreCase));

                    if (removedCount > 0)
                    {
                        Save(config);
                    }

                    return config;
                }
            }
        }
        catch
        {
            // Fallback to default on read/corrupt errors
        }

        var defaultConfig = new AlignerConfig
        {
            StartWithWindows = IsStartupEnabled()
        };
        Save(defaultConfig);
        return defaultConfig;
    }

    public static void Save(AlignerConfig config)
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);

            SetStartupEnabled(config.StartWithWindows);
        }
        catch
        {
            // Ignore persistence write issues
        }
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartupEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                if (key.GetValue(AppRegistryName) != null)
                {
                    key.DeleteValue(AppRegistryName, throwOnMissingValue: false);
                }
            }
        }
        catch
        {
            // Ignore registry permissions issues
        }
    }
}
