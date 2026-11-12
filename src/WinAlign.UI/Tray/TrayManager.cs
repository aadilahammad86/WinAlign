using System.Windows.Forms;
using WinAlign.Core.Configuration;
using WinAlign.Core.Engine;
using WinAlign.Core.Models;

namespace WinAlign.UI.Tray;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AlignerConfig _config;
    private readonly WindowAlignerService _service;
    private readonly Action _showMainWindow;
    private bool _disposed;

    public TrayManager(AlignerConfig config, WindowAlignerService service, Action showMainWindow)
    {
        _config = config;
        _service = service;
        _showMainWindow = showMainWindow;

        _notifyIcon = new NotifyIcon
        {
            Icon = TrayIconHelper.CreateAppIcon(),
            Text = $"WinAlign - {_config.DefaultAnchor}",
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => _showMainWindow();
        RebuildContextMenu();
    }

    public void UpdateStatus()
    {
        _notifyIcon.Text = $"WinAlign - {_config.DefaultAnchor}";
        RebuildContextMenu();
    }

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(2000, title, message, ToolTipIcon.Info);
    }

    private void RebuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var headerItem = new ToolStripMenuItem($"WinAlign ({(_config.AutoAlignNewWindows ? "Active" : "Paused")})")
        {
            Enabled = false,
            Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new ToolStripSeparator());

        // Alignment presets submenu
        var alignMenu = new ToolStripMenuItem("Default Alignment");
        foreach (AlignmentAnchor anchor in Enum.GetValues<AlignmentAnchor>())
        {
            var item = new ToolStripMenuItem(FormatAnchorName(anchor))
            {
                Checked = _config.DefaultAnchor == anchor
            };
            item.Click += (s, e) =>
            {
                _config.DefaultAnchor = anchor;
                ConfigManager.Save(_config);
                UpdateStatus();
            };
            alignMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(alignMenu);

        var alignNowItem = new ToolStripMenuItem("Align Active Window Now", null, (s, e) =>
        {
            _service.AlignActiveWindow();
        });
        menu.Items.Add(alignNowItem);

        var toggleAutoItem = new ToolStripMenuItem("Auto-Align New Windows", null, (s, e) =>
        {
            _config.AutoAlignNewWindows = !_config.AutoAlignNewWindows;
            ConfigManager.Save(_config);
            UpdateStatus();
        })
        {
            Checked = _config.AutoAlignNewWindows
        };
        menu.Items.Add(toggleAutoItem);

        menu.Items.Add(new ToolStripSeparator());

        var showItem = new ToolStripMenuItem("Open Dashboard", null, (s, e) => _showMainWindow());
        menu.Items.Add(showItem);

        var exitItem = new ToolStripMenuItem("Exit WinAlign", null, (s, e) =>
        {
            System.Windows.Application.Current.Shutdown();
        });
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
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

    public void Dispose()
    {
        if (_disposed) return;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
