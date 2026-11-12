using System.Windows.Interop;
using WinAlign.Core.Engine;
using WinAlign.Core.Models;
using WinAlign.Core.Native;

namespace WinAlign.UI.Hotkeys;

public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int HOTKEY_BASE_ID = 9000;
    private const int HOTKEY_DEFAULT_ID = 9010;

    private readonly WindowAlignerService _service;
    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _source;
    private readonly List<int> _registeredIds = new();
    private bool _disposed;

    // Mapping from Numpad VK to 9-point Anchor
    private static readonly (uint Vk, AlignmentAnchor Anchor)[] NumpadMap =
    {
        (0x67, AlignmentAnchor.TopLeft),      // Num 7
        (0x68, AlignmentAnchor.CenterTop),    // Num 8
        (0x69, AlignmentAnchor.TopRight),     // Num 9
        (0x64, AlignmentAnchor.CenterLeft),   // Num 4
        (0x65, AlignmentAnchor.CenterMiddle), // Num 5
        (0x66, AlignmentAnchor.CenterRight),  // Num 6
        (0x61, AlignmentAnchor.BottomLeft),   // Num 1
        (0x62, AlignmentAnchor.CenterBottom), // Num 2
        (0x63, AlignmentAnchor.BottomRight),  // Num 3
    };

    public HotkeyManager(WindowAlignerService service)
    {
        _service = service;
    }

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);

        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Register Ctrl + Alt + Space for Default Anchor Alignment
        if (Win32Api.RegisterHotKey(_hwnd, HOTKEY_DEFAULT_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x20)) // 0x20 = Space
        {
            _registeredIds.Add(HOTKEY_DEFAULT_ID);
        }

        // Register Ctrl + Alt + Numpad1..9
        for (int i = 0; i < NumpadMap.Length; i++)
        {
            int hotkeyId = HOTKEY_BASE_ID + i;
            if (Win32Api.RegisterHotKey(_hwnd, hotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, NumpadMap[i].Vk))
            {
                _registeredIds.Add(hotkeyId);
            }
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();

            if (id == HOTKEY_DEFAULT_ID)
            {
                _service.AlignActiveWindow();
                handled = true;
            }
            else if (id >= HOTKEY_BASE_ID && id < HOTKEY_BASE_ID + NumpadMap.Length)
            {
                int index = id - HOTKEY_BASE_ID;
                _service.AlignActiveWindow(NumpadMap[index].Anchor);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_hwnd != IntPtr.Zero)
        {
            foreach (int id in _registeredIds)
            {
                Win32Api.UnregisterHotKey(_hwnd, id);
            }
            _registeredIds.Clear();

            _source?.RemoveHook(HwndHook);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
