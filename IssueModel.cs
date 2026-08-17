namespace BlackStarHydra360.Core;

public enum IssueSeverity { Info, Warning, Critical, Low, Medium, High }

public sealed class IssueModel
{
    public string RuleId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public string CodeSnippet { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public bool IsFixable { get; set; }
    public string IssueType { get; set; } = "Grep"; // Grep | Roslyn | Refactor

    public override string ToString() =>
        $"[{Severity}] Line {LineNumber} {Category}: {Description}";

    public static IssueSeverity ParseSeverity(string? s) => s?.ToLowerInvariant() switch
    {
        "critical" or "high" => IssueSeverity.Critical,
        "warning" or "medium" => IssueSeverity.Warning,
        "low" => IssueSeverity.Low,
        _ => IssueSeverity.Info
    };

    public string SeverityLabel => Severity switch
    {
        IssueSeverity.Critical => "CRITICAL",
        IssueSeverity.High => "HIGH",
        IssueSeverity.Medium => "MEDIUM",
        IssueSeverity.Warning => "WARNING",
        IssueSeverity.Low => "LOW",
        _ => "INFO"
    };
}
