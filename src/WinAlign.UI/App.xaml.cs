using System.Windows;

namespace WinAlign.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
        }
        catch
        {
            // Ignore if priority adjustment is restricted
        }

        bool startMinimized = e.Args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        if (!startMinimized)
        {
            mainWindow.Show();
        }
        else
        {
            // Keep window hidden in tray
            mainWindow.Hide();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow is MainWindow mw)
        {
            mw.ShutdownApplication();
        }
        base.OnExit(e);
    }
}
