using System.Text;
using System.Text.Json;
using BlackStarHydra360.Logging;

namespace BlackStarHydra360.Core;

/// <summary>
/// Generates audit reports in TXT, JSON, HTML, and CSV formats.
/// </summary>
public static class ReportGenerator
{
    public static string GenerateTextReport(AuditResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════");
        sb.AppendLine("  ★ BLACK STAR HYDRA 360 — AUDIT REPORT ★");
        sb.AppendLine("══════════════════════════════════════════════════");
        sb.AppendLine($"  Target  : {result.TargetPath}");
        sb.AppendLine($"  Date    : {result.ScanDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Mode    : {result.Mode}");
        sb.AppendLine($"  Duration: {result.Duration.TotalSeconds:F2}s");
        sb.AppendLine($"  Files   : {result.FilesScanned} scanned");
        sb.AppendLine($"  Issues  : {result.TotalIssues} ({result.CriticalCount} critical, {result.WarningCount} warnings, {result.InfoCount} info)");
        sb.AppendLine($"  Fixable : {result.FixableCount}");
        sb.AppendLine("──────────────────────────────────────────────────");
        sb.AppendLine();

        if (result.Issues.Count == 0)
        {
            sb.AppendLine("  ✅ No issues detected — codebase looks clean.");
        }
        else
        {
            // Group by file
            var byFile = result.Issues.GroupBy(i => i.FilePath).OrderBy(g => g.Key);
            foreach (var group in byFile)
            {
                sb.AppendLine($"  📄 {Path.GetFileName(group.Key)} ({group.Count()} issues)");
                foreach (var issue in group.OrderBy(i => i.LineNumber))
                {
                    sb.AppendLine($"    [{issue.SeverityLabel}] Line {issue.LineNumber} ({issue.RuleId}) {issue.Description}");
                    if (!string.IsNullOrEmpty(issue.CodeSnippet))
                        sb.AppendLine($"      Code: {issue.CodeSnippet.Truncate(100)}");
                    if (!string.IsNullOrEmpty(issue.SuggestedFix))
                        sb.AppendLine($"      Fix:  {issue.SuggestedFix}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("══════════════════════════════════════════════════");
        return sb.ToString();
    }

    public static string GenerateJsonReport(AuditResult result)
    {
        var dto = new
        {
            targetPath = result.TargetPath,
            scanDate = result.ScanDate.ToString("O"),
            mode = result.Mode.ToString(),
            duration = result.Duration.TotalSeconds,
            filesScanned = result.FilesScanned,
            totalIssues = result.TotalIssues,
            criticalCount = result.CriticalCount,
            warningCount = result.WarningCount,
            infoCount = result.InfoCount,
            fixableCount = result.FixableCount,
            issues = result.Issues.Select(i => new
            {
                ruleId = i.RuleId,
                file = i.FilePath,
                line = i.LineNumber,
                column = i.Column,
                category = i.Category,
                description = i.Description,
                severity = i.Severity.ToString(),
                snippet = i.CodeSnippet,
                fix = i.SuggestedFix,
                isFixable = i.IsFixable,
                type = i.IssueType
            })
        };
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string GenerateHtmlReport(AuditResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>Black Star Hydra 360 — Audit Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;background:#08090e;color:#e2e6f0;margin:0;padding:20px}");
        sb.AppendLine("h1{color:#c43440;border-bottom:2px solid #c43440;padding-bottom:10px}");
        sb.AppendLine("h2{color:#64a4d6;margin-top:30px}");
        sb.AppendLine(".summary{background:#12151f;padding:15px;border-radius:8px;margin:15px 0;border:1px solid #303648}");
        sb.AppendLine(".summary span{display:inline-block;margin-right:20px}");
        sb.AppendLine(".critical{color:#e24a58}.warning{color:#d48e30}.info{color:#606a80}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin:10px 0}");
        sb.AppendLine("th{background:#24293a;padding:8px;text-align:left;color:#a2acbe}");
        sb.AppendLine("td{padding:8px;border-bottom:1px solid #1a1f2d}");
        sb.AppendLine("tr:hover{background:#1c2130}");
        sb.AppendLine(".snippet{font-family:Consolas,monospace;color:#c43440;font-size:0.85em}");
        sb.AppendLine(".fix{color:#38be6c;font-size:0.85em}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>★ Black Star Hydra 360 — Audit Report</h1>");
        sb.AppendLine($"<div class='summary'>");
        sb.AppendLine($"<span>Target: <strong>{result.TargetPath}</strong></span>");
        sb.AppendLine($"<span>Date: {result.ScanDate:yyyy-MM-dd HH:mm:ss}</span>");
        sb.AppendLine($"<span>Mode: {result.Mode}</span>");
        sb.AppendLine($"<span>Duration: {result.Duration.TotalSeconds:F2}s</span>");
        sb.AppendLine($"<span class='critical'>Critical: {result.CriticalCount}</span>");
        sb.AppendLine($"<span class='warning'>Warnings: {result.WarningCount}</span>");
        sb.AppendLine($"<span class='info'>Info: {result.InfoCount}</span>");
        sb.AppendLine($"<span>Fixable: {result.FixableCount}</span>");
        sb.AppendLine("</div>");

        if (result.Issues.Count == 0)
        {
            sb.AppendLine("<p>✅ No issues detected — codebase looks clean.</p>");
        }
        else
        {
            sb.AppendLine("<table><tr><th>Line</th><th>Rule</th><th>Category</th><th>Severity</th><th>Description</th><th>Snippet</th></tr>");
            foreach (var issue in result.Issues.OrderBy(i => i.FilePath).ThenBy(i => i.LineNumber))
            {
                string sevClass = issue.Severity switch
                {
                    IssueSeverity.Critical or IssueSeverity.High => "critical",
                    IssueSeverity.Warning or IssueSeverity.Medium => "warning",
                    _ => "info"
                };
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{issue.LineNumber}</td>");
                sb.AppendLine($"<td>{issue.RuleId}</td>");
                sb.AppendLine($"<td>{issue.Category}</td>");
                sb.AppendLine($"<td class='{sevClass}'>{issue.SeverityLabel}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(issue.Description)}</td>");
                sb.AppendLine($"<td class='snippet'>{System.Net.WebUtility.HtmlEncode(issue.CodeSnippet.Truncate(60))}</td>");
                sb.AppendLine($"</tr>");
            }
            sb.AppendLine("</table>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public static string GenerateCsvReport(AuditResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RuleId,File,Line,Column,Category,Severity,Description,IsFixable,IssueType,CodeSnippet,SuggestedFix");
        foreach (var issue in result.Issues)
        {
            string Escape(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
            sb.AppendLine(string.Join(",",
                issue.RuleId,
                Escape(issue.FilePath),
                issue.LineNumber,
                issue.Column,
                Escape(issue.Category),
                issue.SeverityLabel,
                Escape(issue.Description),
                issue.IsFixable,
                issue.IssueType,
                Escape(issue.CodeSnippet),
                Escape(issue.SuggestedFix)));
        }
        return sb.ToString();
    }

    public static void SaveReport(string filePath, string content)
    {
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, content, Encoding.UTF8);
            AppLogger.Info($"Report saved: {filePath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to save report to {filePath}", ex);
            throw;
        }
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max] + "...");
}
