using System.Diagnostics;
using System.Text;
using WinAlign.Core.Models;
using WinAlign.Core.Native;

namespace WinAlign.Core.Engine;

public static class WindowFilter
{
    private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TaskListThumbnailWnd",
        "LauncherTipWnd",
        "XamlExplorerHostIslandWindow",
        "Windows.UI.Core.CoreWindow",
        "DV2ControlHost",
        "MsgrIMEWindowClass",
        "SysShadow",
        "NarratorHelperWindow",
        "EdgeUiInputTopWndClass",
        "TaskListOverlayWnd",
        "TopLevelWindowForOverflowXamlIsland"
    };

    public static bool IsEligibleWindow(IntPtr hwnd, AlignerConfig config, out string reason)
    {
        reason = string.Empty;

        if (hwnd == IntPtr.Zero || !Win32Api.IsWindow(hwnd))
        {
            reason = "Invalid window handle";
            return false;
        }

        if (!Win32Api.IsWindowVisible(hwnd))
        {
            reason = "Window is not visible";
            return false;
        }

        if (Win32Api.IsIconic(hwnd))
        {
            reason = "Window is minimized";
            return false;
        }

        if (Win32Api.IsZoomed(hwnd))
        {
            reason = "Window is maximized";
            return false;
        }

        // Must be top-level window
        if (Win32Api.GetAncestor(hwnd, Win32Api.GA_ROOT) != hwnd)
        {
            reason = "Window is not top-level root";
            return false;
        }

        long style = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_STYLE).ToInt64();
        if ((style & Win32Api.WS_CHILD) != 0)
        {
            reason = "Child window";
            return false;
        }

        long exStyle = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE).ToInt64();
        if ((exStyle & Win32Api.WS_EX_TOOLWINDOW) != 0 && (exStyle & Win32Api.WS_EX_APPWINDOW) == 0)
        {
            reason = "Tool window";
            return false;
        }

        if ((exStyle & Win32Api.WS_EX_NOACTIVATE) != 0)
        {
            reason = "No-activate window";
            return false;
        }

        if (!Win32Api.GetWindowRect(hwnd, out var rect) || rect.Width < 120 || rect.Height < 80)
        {
            reason = "Window too small or invalid dimensions";
            return false;
        }

        var classNameBuilder = new StringBuilder(256);
        Win32Api.GetClassName(hwnd, classNameBuilder, classNameBuilder.Capacity);
        string className = classNameBuilder.ToString();

        try
        {
            Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid > 0)
            {
                using var process = Process.GetProcessById((int)pid);
                string procName = process.ProcessName;

                return IsEligibleWindowClassAndProcess(className, procName, config, out reason);
            }
        }
        catch
        {
            // If process has already exited or cannot be opened, ignore
        }

        return true;
    }

    /// <summary>
    /// Pure validation helper for window class and process name, allowing direct unit testing.
    /// </summary>
    public static bool IsEligibleWindowClassAndProcess(string className, string procName, AlignerConfig config, out string reason)
    {
        reason = string.Empty;

        if (IgnoredWindowClasses.Contains(className))
        {
            reason = $"Ignored system class '{className}'";
            return false;
        }

        // Special handling for Windows Explorer (explorer.exe)
        if (string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase))
        {
            // Allow legitimate File Explorer windows and file dialogs
            bool isFileExplorer = string.Equals(className, "CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(className, "#32770", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(className, "OperationStatusWindow", StringComparison.OrdinalIgnoreCase);

            if (!isFileExplorer)
            {
                reason = $"Ignored explorer background component with class '{className}'";
                return false;
            }

            // File Explorer windows (CabinetWClass) are explicitly permitted
            return true;
        }

        // Standard process exclusion check
        if (config.ExcludedProcesses.Any(e => string.Equals(e, procName, StringComparison.OrdinalIgnoreCase)))
        {
            reason = $"Process '{procName}' is in exclusion list";
            return false;
        }

        return true;
    }
}
