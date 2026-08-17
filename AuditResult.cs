using BlackStarHydra360.Logging;

namespace BlackStarHydra360.Core;

public sealed class AuditResult
{
    public string TargetPath { get; set; } = string.Empty;
    public DateTime ScanDate { get; set; } = DateTime.Now;
    public AuditMode Mode { get; set; }
    public TimeSpan Duration { get; set; }
    public List<IssueModel> Issues { get; set; } = new();
    public int FilesScanned { get; set; }
    public int FilesSkipped { get; set; }
    public int TotalIssues => Issues.Count;
    public int CriticalCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }
    public int FixableCount { get; private set; }
    public string Summary => $"{TotalIssues} issues: {CriticalCount} critical, {WarningCount} warnings, {InfoCount} info ({FixableCount} fixable) in {FilesScanned} files over {Duration.TotalSeconds:F1}s";

    public void ComputeSummary()
    {
        CriticalCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        FixableCount = 0;
        foreach (var issue in Issues)
        {
            switch (issue.Severity)
            {
                case IssueSeverity.Critical or IssueSeverity.High: CriticalCount++; break;
                case IssueSeverity.Warning or IssueSeverity.Medium: WarningCount++; break;
                default: InfoCount++; break;
            }
            if (issue.IsFixable) FixableCount++;
        }
        AppLogger.Info($"Audit complete: {Summary}");
    }
}
