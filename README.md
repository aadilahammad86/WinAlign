# WinAlign — Native Windows 11 Window Opening Alignment Utility

**WinAlign** is a lightweight, high-performance native Windows 11 background utility that automatically aligns newly opened application windows to user-selected screen anchors across your displays.

---

## ✨ Features

- **9-Point Screen Anchor Grid**:
  - `↖ Top-Left`
  - `↑ Center-Top` (Top-Middle)
  - `↗ Top-Right`
  - `← Center-Left` (Middle-Left)
  - `⦿ Center-Middle` (Dead Center)
  - `→ Center-Right` (Middle-Right)
  - `↙ Bottom-Left`
  - `↓ Center-Bottom` (Bottom-Middle)
  - `↘ Bottom-Right`
- **Low-Overhead WinEvent Engine**:
  - Hooks system-level `EVENT_OBJECT_SHOW` and `EVENT_SYSTEM_FOREGROUND` via native Win32 accessibility hooks.
  - Operates passively with negligible CPU and RAM (~15-25 MB).
- **Multi-Monitor & Taskbar Intelligence**:
  - Respects the visible `rcWork` desktop bounds on any monitor, accounting for taskbars placed at bottom, top, left, or right.
  - Optional mouse-cursor tracking: aligns new windows to whichever display the mouse is actively on.
- **Windows 11 DWM Shadow Margin Compensation**:
  - Uses `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` to measure and eliminate the invisible 7-pixel drop shadow border, achieving pixel-perfect docking against screen margins.
- **System Tray Integration**:
  - Minimizes seamlessly to the Windows 11 notification area.
  - Context menu allows one-click switching between any of the 9 alignment anchors without opening the settings window.
- **Global Keyboard Shortcuts**:
  - `Ctrl + Alt + Space`: Instantly align the active foreground window to your configured default anchor.
  - `Ctrl + Alt + Num 1..9`: Instantly snap the active foreground window to the corresponding spatial numpad anchor (7 = Top-Left, 5 = Center-Middle, 3 = Bottom-Right, etc.).
- **Smart Filtering & Exclusions**:
  - Automatically filters out tooltips, ghost windows, popup menus, and Windows Shell components (`explorer`, `taskmgr`, `SearchHost`, `StartMenuExperienceHost`).
  - Customizable user exclusion list.
- **Start with Windows**:
  - Optional automatic startup via Windows Run registry key.

---

## 🏗️ Architecture

```
C:\Users\aadil\projects\WinAlign
├── WinAlign.slnx
├── README.md
├── publish/
│   └── WinAlign.UI.exe
├── src/
│   ├── WinAlign.Core/
│   │   ├── Configuration/
│   │   │   └── ConfigManager.cs
│   │   ├── Engine/
│   │   │   ├── WindowAlignerService.cs
│   │   │   ├── WindowBoundsCalculator.cs
│   │   │   └── WindowFilter.cs
│   │   ├── Models/
│   │   │   ├── AlignerConfig.cs
│   │   │   └── AlignmentAnchor.cs
│   │   └── Native/
│   │       └── Win32Api.cs
│   └── WinAlign.UI/
│       ├── app.manifest
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
│       ├── Hotkeys/
│       │   └── HotkeyManager.cs
│       └── Tray/
│           ├── TrayIconHelper.cs
│           └── TrayManager.cs
└── tests/
    └── WinAlign.Tests/
        ├── WindowBoundsCalculatorTests.cs
        └── WindowFilterTests.cs
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- .NET 10.0 / .NET 8.0 SDK (for compiling from source)

### Building & Running
```powershell
# Restore & build solution
dotnet build WinAlign.slnx

# Run automated tests
dotnet test WinAlign.slnx

# Launch application
dotnet run --project src/WinAlign.UI/WinAlign.UI.csproj
```

### Precompiled Release Binary
You can run the compiled binary directly from:
```powershell
.\publish\WinAlign.UI.exe
```

---

## ⚙️ Configuration File
Settings are persisted locally at:
`%APPDATA%\WinAlign\config.json`
