using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using WinAlign.Core.Models;
using WinAlign.Core.Native;

namespace WinAlign.Core.Engine;

public class WindowAlignerService : IDisposable
{
    private readonly AlignerConfig _config;
    private readonly ConcurrentDictionary<IntPtr, DateTime> _recentlyAligned = new();
    private Win32Api.WinEventDelegate? _winEventDelegate;
    private IntPtr _showHookHandle = IntPtr.Zero;
    private IntPtr _foregroundHookHandle = IntPtr.Zero;
    private bool _disposed;

    public event Action<IntPtr, string, AlignmentAnchor>? WindowAligned;
    public event Action<string>? LogMessage;

    public WindowAlignerService(AlignerConfig config)
    {
        _config = config;
    }

    public void Start()
    {
        if (_showHookHandle != IntPtr.Zero) return;

        // Keep delegate alive to avoid garbage collection
        _winEventDelegate = OnWinEvent;

        _showHookHandle = Win32Api.SetWinEventHook(
            Win32Api.EVENT_OBJECT_SHOW,
            Win32Api.EVENT_OBJECT_SHOW,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

        _foregroundHookHandle = Win32Api.SetWinEventHook(
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

        LogMessage?.Invoke("Window alignment hooks started.");
    }

    public void Stop()
    {
        if (_showHookHandle != IntPtr.Zero)
        {
            Win32Api.UnhookWinEvent(_showHookHandle);
            _showHookHandle = IntPtr.Zero;
        }

        if (_foregroundHookHandle != IntPtr.Zero)
        {
            Win32Api.UnhookWinEvent(_foregroundHookHandle);
            _foregroundHookHandle = IntPtr.Zero;
        }

        LogMessage?.Invoke("Window alignment hooks stopped.");
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (!_config.AutoAlignNewWindows) return;

        // Only handle top-level window objects
        if (idObject != Win32Api.OBJID_WINDOW || idChild != Win32Api.CHILDID_SELF)
            return;

        // Debounce: don't align the same window multiple times within 2 seconds
        DateTime now = DateTime.UtcNow;
        if (_recentlyAligned.TryGetValue(hwnd, out var lastAligned) && (now - lastAligned).TotalSeconds < 2.0)
            return;

        AlignWindow(hwnd, _config.DefaultAnchor, isAuto: true);
    }

    /// <summary>
    /// Aligns the specified window handle to the given anchor.
    /// </summary>
    public bool AlignWindow(IntPtr hwnd, AlignmentAnchor anchor, bool isAuto = false)
    {
        if (!WindowFilter.IsEligibleWindow(hwnd, _config, out string reason))
        {
            return false;
        }

        if (!Win32Api.GetWindowRect(hwnd, out var windowRect))
        {
            return false;
        }

        // Determine target monitor work area
        IntPtr hMonitor;
        if (_config.AlignOnCursorMonitor && Win32Api.GetCursorPos(out var pt))
        {
            hMonitor = Win32Api.MonitorFromPoint(pt, Win32Api.MONITOR_DEFAULTTONEAREST);
        }
        else
        {
            hMonitor = Win32Api.MonitorFromWindow(hwnd, Win32Api.MONITOR_DEFAULTTONEAREST);
        }

        if (hMonitor == IntPtr.Zero) return false;

        var monitorInfo = new Win32Api.MONITORINFO
        {
            cbSize = Marshal.SizeOf<Win32Api.MONITORINFO>()
        };

        if (!Win32Api.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            return false;
        }

        // Compute DWM shadow bounds for pixel-perfect frame alignment
        WindowBoundsCalculator.ShadowMargins? shadowMargins = null;
        int dwmResult = Win32Api.DwmGetWindowAttribute(
            hwnd,
            Win32Api.DWMWA_EXTENDED_FRAME_BOUNDS,
            out var frameBounds,
            Marshal.SizeOf<Win32Api.RECT>());

        if (dwmResult == 0) // S_OK
        {
            int leftMargin = Math.Max(0, frameBounds.Left - windowRect.Left);
            int topMargin = Math.Max(0, frameBounds.Top - windowRect.Top);
            int rightMargin = Math.Max(0, windowRect.Right - frameBounds.Right);
            int bottomMargin = Math.Max(0, windowRect.Bottom - frameBounds.Bottom);
            shadowMargins = new WindowBoundsCalculator.ShadowMargins(leftMargin, topMargin, rightMargin, bottomMargin);
        }

        var (targetX, targetY) = WindowBoundsCalculator.CalculateTargetPosition(
            windowRect,
            monitorInfo.rcWork,
            anchor,
            _config.MarginPixels,
            shadowMargins);

        // Move window without resizing or altering Z-order
        bool success = Win32Api.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            targetX,
            targetY,
            0,
            0,
            Win32Api.SWP_NOSIZE | Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_FRAMECHANGED);

        if (success)
        {
            _recentlyAligned[hwnd] = DateTime.UtcNow;
            CleanRecentCache();

            var titleBuilder = new StringBuilder(256);
            Win32Api.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString();

            WindowAligned?.Invoke(hwnd, title, anchor);
            LogMessage?.Invoke($"Aligned [{(string.IsNullOrWhiteSpace(title) ? "Window" : title)}] to {anchor} at ({targetX}, {targetY})");
        }

        return success;
    }

    /// <summary>
    /// Aligns the currently active/focused foreground window.
    /// </summary>
    public bool AlignActiveWindow(AlignmentAnchor? anchor = null)
    {
        IntPtr foregroundHwnd = Win32Api.GetForegroundWindow();
        if (foregroundHwnd == IntPtr.Zero) return false;

        return AlignWindow(foregroundHwnd, anchor ?? _config.DefaultAnchor, isAuto: false);
    }

    private void CleanRecentCache()
    {
        DateTime cutoff = DateTime.UtcNow.AddSeconds(-10);
        foreach (var entry in _recentlyAligned)
        {
            if (entry.Value < cutoff)
            {
                _recentlyAligned.TryRemove(entry.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
