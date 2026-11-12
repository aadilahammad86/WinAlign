using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace WinAlign.Installer;

public static class Program
{
    private const string AppName = "WinAlign";
    private static readonly string TargetDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        AppName);

    private static readonly string ExePath = Path.Combine(TargetDir, "WinAlign.UI.exe");

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // 1. Terminate running instances
            KillRunningInstances();

            // 2. Prepare installation directory
            if (!Directory.Exists(TargetDir))
            {
                Directory.CreateDirectory(TargetDir);
            }

            // 3. Extract payload
            ExtractPayload();

            // 4. Update configuration to ensure taskmgr and explorer are enabled
            SanitizeConfig();

            // 5. Create Windows Scheduled Task (Elevated, High Priority at Logon)
            RegisterScheduledTask();

            // 6. Create Shortcuts
            CreateShortcuts();

            // 7. Write Uninstaller script and register in Windows Add/Remove programs
            CreateUninstaller();

            // 8. Launch the installed app immediately as Admin
            LaunchInstalledApp();

            // 9. Notify user of success
            ShowMessage(
                "WinAlign has been successfully installed!\n\n" +
                $"• Location: {TargetDir}\n" +
                "• Privileges: Administrator (Highest Available)\n" +
                "• Startup: Windows Task Scheduler (Elevated, High Priority)\n" +
                "• Task Manager & Elevated Apps: Fully Supported\n" +
                "• Shortcuts: Start Menu & Desktop\n\n" +
                "WinAlign is now running in your system tray.",
                "WinAlign Setup Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowMessage(
                $"Installation encountered an error:\n\n{ex.Message}",
                "WinAlign Setup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void KillRunningInstances()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("WinAlign.UI"))
            {
                proc.Kill();
                proc.WaitForExit(3000);
            }
        }
        catch
        {
            // Ignore if already dead or access restricted
        }
    }

    private static void ExtractPayload()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("payload.zip");
        if (stream == null)
        {
            throw new InvalidOperationException("Embedded installation payload not found.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry

            string destFile = Path.Combine(TargetDir, entry.FullName);
            string? dir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            entry.ExtractToFile(destFile, overwrite: true);
        }
    }

    private static void SanitizeConfig()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configPath = Path.Combine(appData, "WinAlign", "config.json");

            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;
                var dict = new Dictionary<string, object?>();

                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("ExcludedProcesses"))
                    {
                        var list = new List<string>();
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            string? str = item.GetString();
                            if (!string.IsNullOrEmpty(str) &&
                                !string.Equals(str, "explorer", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(str, "explorer.exe", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(str, "taskmgr", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(str, "taskmgr.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(str);
                            }
                        }
                        dict[prop.Name] = list;
                    }
                    else
                    {
                        dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
                    }
                }

                string updatedJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, updatedJson);
            }
        }
        catch
        {
            // Ignore config formatting issues
        }
    }

    private static void RegisterScheduledTask()
    {
        // Delete existing task if present
        RunProcess("schtasks.exe", "/delete /f /tn \"WinAlign\"", wait: true);

        // Create elevated task at logon with Highest Privileges
        string createArgs = $"/create /f /tn \"WinAlign\" /tr \"\\\"{ExePath}\\\" --minimized\" /sc onlogon /rl highest /it";
        RunProcess("schtasks.exe", createArgs, wait: true);

        // Adjust Task Priority to High (Priority 4) via temporary XML
        try
        {
            string tempXml = Path.Combine(Path.GetTempPath(), "WinAlignTask.xml");
            RunProcess("schtasks.exe", $"/query /tn \"WinAlign\" /xml > \"{tempXml}\"", wait: true);

            if (File.Exists(tempXml))
            {
                string xml = File.ReadAllText(tempXml);
                if (xml.Contains("<Priority>7</Priority>"))
                {
                    xml = xml.Replace("<Priority>7</Priority>", "<Priority>4</Priority>");
                    File.WriteAllText(tempXml, xml);
                    RunProcess("schtasks.exe", $"/create /f /tn \"WinAlign\" /xml \"{tempXml}\"", wait: true);
                }
                File.Delete(tempXml);
            }
        }
        catch
        {
            // Priority default is acceptable if XML adjustment fails
        }
    }

    private static void CreateShortcuts()
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            // Start Menu Shortcut
            string commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            string startMenuLink = Path.Combine(commonPrograms, "WinAlign.lnk");
            dynamic shortcut1 = shell.CreateShortcut(startMenuLink);
            shortcut1.TargetPath = ExePath;
            shortcut1.WorkingDirectory = TargetDir;
            shortcut1.Description = "WinAlign - Windows 11 Window Opening Aligner";
            shortcut1.IconLocation = $"{ExePath},0";
            shortcut1.Save();

            // Public Desktop Shortcut
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            string desktopLink = Path.Combine(publicDesktop, "WinAlign.lnk");
            dynamic shortcut2 = shell.CreateShortcut(desktopLink);
            shortcut2.TargetPath = ExePath;
            shortcut2.WorkingDirectory = TargetDir;
            shortcut2.Description = "WinAlign - Windows 11 Window Opening Aligner";
            shortcut2.IconLocation = $"{ExePath},0";
            shortcut2.Save();
        }
        catch
        {
            // Ignore shortcut creation failures
        }
    }

    private static void CreateUninstaller()
    {
        try
        {
            string uninstallScript = Path.Combine(TargetDir, "Uninstall.ps1");
            string scriptContent = 
@"Stop-Process -Name 'WinAlign.UI' -Force -ErrorAction SilentlyContinue
schtasks.exe /delete /f /tn 'WinAlign' | Out-Null
Remove-Item 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\WinAlign.lnk' -ErrorAction SilentlyContinue
Remove-Item 'C:\Users\Public\Desktop\WinAlign.lnk' -ErrorAction SilentlyContinue
Remove-Item 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WinAlign' -Recurse -ErrorAction SilentlyContinue
[System.Windows.Forms.MessageBox]::Show('WinAlign has been uninstalled.', 'WinAlign Uninstaller')
";
            File.WriteAllText(uninstallScript, scriptContent);

            // Register in Add/Remove programs
            using var key = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinAlign");
            if (key != null)
            {
                key.SetValue("DisplayName", "WinAlign (Administrator)");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "WinAlign");
                key.SetValue("InstallLocation", TargetDir);
                key.SetValue("DisplayIcon", $"{ExePath},0");
                key.SetValue("UninstallString", $"powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{uninstallScript}\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch
        {
            // Ignore registry writing issues
        }
    }

    private static void LaunchInstalledApp()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ExePath,
                WorkingDirectory = TargetDir,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore launch error
        }
    }

    private static void RunProcess(string fileName, string args, bool wait = true)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var proc = Process.Start(psi);
            if (wait && proc != null)
            {
                proc.WaitForExit(5000);
            }
        }
        catch
        {
            // Ignore process execution errors
        }
    }

    private static void ShowMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        // Simple P/Invoke MessageBox to avoid large Windows.Forms dependency
        uint uType = 0x00000000; // MB_OK
        if (icon == MessageBoxIcon.Information) uType |= 0x00000040; // MB_ICONINFORMATION
        if (icon == MessageBoxIcon.Error) uType |= 0x00000010; // MB_ICONERROR

        MessageBox(IntPtr.Zero, message, title, uType);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private enum MessageBoxButtons { OK }
    private enum MessageBoxIcon { Information, Error }
}
