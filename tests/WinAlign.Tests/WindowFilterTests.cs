using WinAlign.Core.Engine;
using WinAlign.Core.Models;
using Xunit;

namespace WinAlign.Tests;

public class WindowFilterTests
{
    [Fact]
    public void IsEligibleWindow_ZeroHandle_ReturnsFalse()
    {
        var config = new AlignerConfig();
        bool eligible = WindowFilter.IsEligibleWindow(IntPtr.Zero, config, out string reason);

        Assert.False(eligible);
        Assert.Equal("Invalid window handle", reason);
    }

    [Fact]
    public void AlignerConfig_DefaultValues_AreCorrect()
    {
        var config = new AlignerConfig();

        Assert.Equal(AlignmentAnchor.CenterMiddle, config.DefaultAnchor);
        Assert.Equal(12, config.MarginPixels);
        Assert.True(config.AutoAlignNewWindows);
        Assert.True(config.AlignOnCursorMonitor);
        Assert.DoesNotContain("explorer", config.ExcludedProcesses);
        Assert.DoesNotContain("taskmgr", config.ExcludedProcesses);
        Assert.Contains("shellexperiencehost", config.ExcludedProcesses);
    }

    [Fact]
    public void TaskManager_IsEligibleWhenNotExcluded()
    {
        var config = new AlignerConfig();

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: "TaskManagerWindow",
            procName: "taskmgr",
            config: config,
            out string reason);

        Assert.True(eligible);
        Assert.Empty(reason);
    }

    [Fact]
    public void FileExplorer_CabinetWClass_IsEligibleEvenIfExplorerExcluded()
    {
        var config = new AlignerConfig
        {
            ExcludedProcesses = new List<string> { "explorer", "taskmgr" }
        };

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: "CabinetWClass",
            procName: "explorer",
            config: config,
            out string reason);

        Assert.True(eligible);
        Assert.Empty(reason);
    }

    [Fact]
    public void FileExplorer_DialogWindow_IsEligible()
    {
        var config = new AlignerConfig();

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: "#32770",
            procName: "explorer",
            config: config,
            out string reason);

        Assert.True(eligible);
        Assert.Empty(reason);
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("NotifyIconOverflowWindow")]
    [InlineData("TaskListThumbnailWnd")]
    public void WindowsShell_Components_AreIgnored(string shellClass)
    {
        var config = new AlignerConfig();

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: shellClass,
            procName: "explorer",
            config: config,
            out string reason);

        Assert.False(eligible);
        Assert.Contains("Ignored system class", reason);
    }

    [Fact]
    public void Explorer_BackgroundUnknownComponent_IsIgnored()
    {
        var config = new AlignerConfig();

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: "SomeExplorerWorkerHost",
            procName: "explorer",
            config: config,
            out string reason);

        Assert.False(eligible);
        Assert.Contains("Ignored explorer background component", reason);
    }

    [Fact]
    public void NonExplorer_StandardApp_IsEligible()
    {
        var config = new AlignerConfig();

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: "Notepad",
            procName: "notepad",
            config: config,
            out string reason);

        Assert.True(eligible);
        Assert.Empty(reason);
    }

    [Fact]
    public void ExcludedProcess_IsRejected()
    {
        var config = new AlignerConfig
        {
            ExcludedProcesses = new List<string> { "customtool" }
        };

        bool eligible = WindowFilter.IsEligibleWindowClassAndProcess(
            className: "CustomAppWindow",
            procName: "customtool",
            config: config,
            out string reason);

        Assert.False(eligible);
        Assert.Equal("Process 'customtool' is in exclusion list", reason);
    }
}
