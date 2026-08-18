namespace PageArc.Models;

public sealed class ShellSessionState
{
    public int SchemaVersion { get; set; } = 1;
    public string? SelectedTabId { get; set; }
    public List<ShellTabSession> Tabs { get; set; } = [];
}
