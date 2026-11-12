namespace WinAlign.Core.Models;

public class AlignerConfig
{
    public AlignmentAnchor DefaultAnchor { get; set; } = AlignmentAnchor.CenterMiddle;
    
    public int MarginPixels { get; set; } = 12;
    
    public bool AutoAlignNewWindows { get; set; } = true;
    
    public bool AlignOnCursorMonitor { get; set; } = true;
    
    public bool EnableHotkeys { get; set; } = true;
    
    public bool StartWithWindows { get; set; } = false;
    
    public List<string> ExcludedProcesses { get; set; } = new()
    {
        "shellexperiencehost",
        "searchhost",
        "startmenuexperiencehost",
        "snippingtool",
        "winalign"
    };
}
