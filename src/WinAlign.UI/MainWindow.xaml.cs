using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WinAlign.Core.Configuration;
using WinAlign.Core.Engine;
using WinAlign.Core.Models;
using WinAlign.UI.Hotkeys;
using WinAlign.UI.Tray;

namespace WinAlign.UI;

public partial class MainWindow : Window
{
    private readonly AlignerConfig _config;
    private readonly WindowAlignerService _service;
    private TrayManager? _trayManager;
    private HotkeyManager? _hotkeyManager;
    private bool _isExplicitExit;
    private readonly Dictionary<AlignmentAnchor, Button> _anchorButtons = new();

    public MainWindow()
    {
        InitializeComponent();

        _config = ConfigManager.Load();
        _service = new WindowAlignerService(_config);

        _service.LogMessage += OnServiceLog;
        _service.WindowAligned += OnWindowAligned;

        MapAnchorButtons();
        LoadConfigIntoUI();

        _service.Start();
        UpdateEngineStatusUI();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _trayManager = new TrayManager(_config, _service, ShowAndRestoreWindow);

        if (_config.EnableHotkeys)
        {
            _hotkeyManager = new HotkeyManager(_service);
            _hotkeyManager.Initialize(hwnd);
        }
    }

    private void MapAnchorButtons()
    {
        _anchorButtons[AlignmentAnchor.TopLeft] = BtnTopLeft;
        _anchorButtons[AlignmentAnchor.CenterTop] = BtnCenterTop;
        _anchorButtons[AlignmentAnchor.TopRight] = BtnTopRight;
        _anchorButtons[AlignmentAnchor.CenterLeft] = BtnCenterLeft;
        _anchorButtons[AlignmentAnchor.CenterMiddle] = BtnCenterMiddle;
        _anchorButtons[AlignmentAnchor.CenterRight] = BtnCenterRight;
        _anchorButtons[AlignmentAnchor.BottomLeft] = BtnBottomLeft;
        _anchorButtons[AlignmentAnchor.CenterBottom] = BtnCenterBottom;
        _anchorButtons[AlignmentAnchor.BottomRight] = BtnBottomRight;
    }

    private void LoadConfigIntoUI()
    {
        ChkAutoAlign.IsChecked = _config.AutoAlignNewWindows;
        ChkCursorMonitor.IsChecked = _config.AlignOnCursorMonitor;
        ChkHotkeys.IsChecked = _config.EnableHotkeys;
        ChkStartup.IsChecked = _config.StartWithWindows;

        SliderMargin.Value = _config.MarginPixels;
        TxtMarginValue.Text = $"{_config.MarginPixels} px";

        RefreshAnchorButtonsVisual();
        RefreshExclusionsList();
    }

    private void RefreshAnchorButtonsVisual()
    {
        var accentBrush = (Brush)FindResource("AccentColor");
        var normalBrush = (Brush)FindResource("ButtonBg");
        var accentBorder = new SolidColorBrush(Color.FromRgb(96, 205, 255));
        var normalBorder = new SolidColorBrush(Color.FromRgb(56, 56, 56));

        foreach (var (anchor, button) in _anchorButtons)
        {
            if (anchor == _config.DefaultAnchor)
            {
                button.Background = accentBrush;
                button.BorderBrush = accentBorder;
                button.Foreground = Brushes.White;
            }
            else
            {
                button.Background = normalBrush;
                button.BorderBrush = normalBorder;
                button.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            }
        }

        TxtSelectedAnchorDescription.Text = $"Active Anchor: {FormatAnchorName(_config.DefaultAnchor)}";
    }

    private void RefreshExclusionsList()
    {
        ListExclusions.Items.Clear();
        foreach (var process in _config.ExcludedProcesses)
        {
            ListExclusions.Items.Add(process);
        }
    }

    private void OnAnchorButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && Enum.TryParse<AlignmentAnchor>(tagStr, out var anchor))
        {
            _config.DefaultAnchor = anchor;
            ConfigManager.Save(_config);
            RefreshAnchorButtonsVisual();
            _trayManager?.UpdateStatus();

            AddLog($"Selected default anchor: {FormatAnchorName(anchor)}");
        }
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        _config.AutoAlignNewWindows = ChkAutoAlign.IsChecked ?? true;
        _config.AlignOnCursorMonitor = ChkCursorMonitor.IsChecked ?? true;
        _config.EnableHotkeys = ChkHotkeys.IsChecked ?? true;
        _config.StartWithWindows = ChkStartup.IsChecked ?? false;

        ConfigManager.Save(_config);
        UpdateEngineStatusUI();
        _trayManager?.UpdateStatus();
    }

    private void OnMarginValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMarginValue == null) return;

        int margin = (int)e.NewValue;
        _config.MarginPixels = margin;
        TxtMarginValue.Text = $"{margin} px";
        ConfigManager.Save(_config);
    }

    private void OnAddExclusionClicked(object sender, RoutedEventArgs e)
    {
        string newProcess = TxtNewExclusion.Text.Trim().ToLowerInvariant();
        if (newProcess.EndsWith(".exe"))
        {
            newProcess = newProcess[..^4];
        }

        if (!string.IsNullOrEmpty(newProcess) && !_config.ExcludedProcesses.Contains(newProcess))
        {
            _config.ExcludedProcesses.Add(newProcess);
            ConfigManager.Save(_config);
            RefreshExclusionsList();
            TxtNewExclusion.Clear();
            AddLog($"Added '{newProcess}' to exclusion list.");
        }
    }

    private void OnRemoveExclusionClicked(object sender, RoutedEventArgs e)
    {
        if (ListExclusions.SelectedItem is string selectedProcess)
        {
            _config.ExcludedProcesses.Remove(selectedProcess);
            ConfigManager.Save(_config);
            RefreshExclusionsList();
            AddLog($"Removed '{selectedProcess}' from exclusion list.");
        }
    }

    private void OnAlignActiveNowClicked(object sender, RoutedEventArgs e)
    {
        bool success = _service.AlignActiveWindow();
        if (!success)
        {
            AddLog("No active target window found or window is ineligible.");
        }
    }

    private void OnClearLogClicked(object sender, RoutedEventArgs e)
    {
        ListActivityLog.Items.Clear();
    }

    private void OnServiceLog(string message)
    {
        Dispatcher.Invoke(() => AddLog(message));
    }

    private void OnWindowAligned(IntPtr hwnd, string title, AlignmentAnchor anchor)
    {
        Dispatcher.Invoke(() =>
        {
            string displayName = string.IsNullOrWhiteSpace(title) ? "Application Window" : title;
            AddLog($"✓ Placed '{displayName}' -> {FormatAnchorName(anchor)}");
        });
    }

    private void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string line = $"[{timestamp}] {message}";
        ListActivityLog.Items.Insert(0, line);

        while (ListActivityLog.Items.Count > 100)
        {
            ListActivityLog.Items.RemoveAt(ListActivityLog.Items.Count - 1);
        }
    }

    private void UpdateEngineStatusUI()
    {
        if (_config.AutoAlignNewWindows)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(76, 194, 122));
            StatusText.Text = "Engine Active";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(108, 203, 95));
            EngineStatusBadge.Background = new SolidColorBrush(Color.FromRgb(30, 57, 42));
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(240, 160, 40));
            StatusText.Text = "Auto-Align Paused";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 70));
            EngineStatusBadge.Background = new SolidColorBrush(Color.FromRgb(55, 45, 20));
        }
    }

    public void ShowAndRestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            // Minimize to tray instead of closing app
            e.Cancel = true;
            Hide();
            _trayManager?.ShowBalloon("WinAlign", "WinAlign is running in the system tray.");
        }
        else
        {
            _service.Dispose();
            _hotkeyManager?.Dispose();
            _trayManager?.Dispose();
            base.OnClosing(e);
        }
    }

    public void ShutdownApplication()
    {
        _isExplicitExit = true;
        Close();
    }

    private static string FormatAnchorName(AlignmentAnchor anchor) => anchor switch
    {
        AlignmentAnchor.TopLeft => "Top-Left",
        AlignmentAnchor.CenterTop => "Center-Top",
        AlignmentAnchor.TopRight => "Top-Right",
        AlignmentAnchor.CenterLeft => "Center-Left",
        AlignmentAnchor.CenterMiddle => "Center-Middle",
        AlignmentAnchor.CenterRight => "Center-Right",
        AlignmentAnchor.BottomLeft => "Bottom-Left",
        AlignmentAnchor.CenterBottom => "Center-Bottom",
        AlignmentAnchor.BottomRight => "Bottom-Right",
        _ => anchor.ToString()
    };
}
