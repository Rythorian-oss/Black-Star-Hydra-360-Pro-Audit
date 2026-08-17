using System.Text.RegularExpressions;
using BlackStarHydra360.Core;
using BlackStarHydra360.Logging;

namespace BlackStarHydra360.Auditors
{
    /// <summary>
    /// Fast regex-based scanner — the "Grep/Scout" phase.
    /// Equivalent to PowerShell Select-String pre-filtering. Streams files line-by-line.
    /// </summary>
    public sealed class GrepAuditor
    {
        public event Action<IssueModel>? IssueFound;

        private static readonly AuditRule[] Rules =
        {
            // Security
            new("SEC001", "Hardcoded credential candidate", "Security Risk", IssueSeverity.Critical,
                @"(?i)\b(password|passwd|pwd|secret|client_secret|api[_-]?key|access[_-]?token|refresh[_-]?token|auth[_-]?token)\b\s*(=|:)\s*[""'][^""']{3,}[""']",
                "Replace hardcoded credential with secure configuration or environment variable.", true),
            new("SEC002", "Connection string with credential", "Security Risk", IssueSeverity.Critical,
                @"(?i)(Server|Data Source|Host)\s*=.*?(Password|Pwd)\s*=",
                "Move connection string to appsettings or environment variable.", true),
            new("SEC003", "AWS access key candidate", "Security Risk", IssueSeverity.Critical,
                @"\bAKIA[0-9A-Z]{16}\b",
                "Remove AWS access key from source code.", true),
            new("SEC004", "Private key embedded", "Security Risk", IssueSeverity.Critical,
                @"-----BEGIN (RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----",
                "Remove embedded private key — load from secure store.", true),
            new("SEC005", "JWT token candidate", "Security Risk", IssueSeverity.Critical,
                @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b",
                "Remove JWT token from source code.", true),
            new("SEC013", "Process execution candidate", "Security Risk", IssueSeverity.Warning,
                @"(?i)\b(Process\.Start|ProcessStartInfo|cmd\.exe|powershell\.exe|pwsh\.exe)\b",
                "Validate and sanitize all process execution inputs.", false),
            new("SEC014", "Dynamic SQL candidate", "Security Risk", IssueSeverity.Warning,
                @"(?i)(SELECT|INSERT|UPDATE|DELETE)\b.{0,120}(\+|\$\{|string\.Concat)",
                "Use parameterized queries instead of string concatenation.", true),
            // Code Quality
            new("QUAL001", "TODO marker", "Unfinished Code", IssueSeverity.Warning,
                @"(?i)\bTODO\b", "Complete or remove TODO marker.", false),
            new("QUAL002", "FIXME marker", "Unfinished Code", IssueSeverity.Warning,
                @"(?i)\bFIXME\b", "Fix or remove FIXME marker.", false),
            new("QUAL003", "HACK marker", "Unfinished Code", IssueSeverity.Warning,
                @"(?i)\bHACK\b", "Remove hack and implement proper solution.", false),
            new("QUAL004", "NotImplementedException", "Unfinished Code", IssueSeverity.Critical,
                @"(?i)\bNotImplementedException\b", "Implement the method or use NotSupportedException.", false),
            new("QUAL005", "Console.WriteLine in production", "Debug Code", IssueSeverity.Warning,
                @"(?i)\bConsole\.WriteLine\b", "Remove Console.WriteLine or wrap in #if DEBUG.", true),
            new("QUAL006", "Debug.WriteLine in production", "Debug Code", IssueSeverity.Info,
                @"(?i)\bDebug\.WriteLine\b", "Remove debug output or use proper logger.", true),
            // Error Handling
            new("QUAL007", "Empty catch block", "Error Handling", IssueSeverity.Critical,
                @"catch\s*\{\s*\}", "Add proper error handling or logging in catch block.", true),
            new("QUAL008", "Catch with silent swallow", "Error Handling", IssueSeverity.Warning,
                @"catch\s*\([^)]*\)\s*\{\s*/\*.*\*/\s*\}", "Log the exception instead of silently swallowing.", true),
            // Memory Leaks
            new("LEAK001", "Event handler subscription without unsubscribe", "Memory Leak", IssueSeverity.Warning,
                @"(?i)\.(\w+Event|\w+)\s*\+=\s*", "Unsubscribe event handler in Dispose to prevent memory leaks.", false),
            new("LEAK002", "Timer created without Dispose", "Memory Leak", IssueSeverity.Warning,
                @"(?i)new\s+(System\.Windows\.Forms\.)?Timer\s*(\(|{)", "Dispose timer in container's Dispose method.", false),
            new("LEAK003", "Bitmap created without using/Dispose", "Memory Leak", IssueSeverity.Warning,
                @"(?i)new\s+Bitmap\s*\(", "Wrap Bitmap in using statement or dispose explicitly.", false),
        };

        private static readonly string[] ExcludedDirs = { "bin", "obj", ".git", ".vs", "packages", "node_modules", "TestResults" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        public Task<List<IssueModel>> ScanFileAsync(string filePath, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var issues = new List<IssueModel>();
                try
                {
                    var info = new FileInfo(filePath);
                    if (!info.Exists || info.Length > MaxFileSize || info.Length == 0)
                        return issues;

                    int lineNum = 0;
                    foreach (string line in File.ReadLines(filePath))
                    {
                        ct.ThrowIfCancellationRequested();
                        lineNum++;
                        foreach (var rule in Rules)
                        {
                            var match = rule.Regex.Match(line);
                            if (match.Success)
                            {
                                var issue = new IssueModel
                                {
                                    RuleId = rule.RuleId,
                                    FilePath = filePath,
                                    LineNumber = lineNum,
                                    Category = rule.Category,
                                    Description = $"{rule.Name}: '{match.Value.Trim()}'",
                                    Severity = rule.Severity,
                                    CodeSnippet = line.Trim(),
                                    SuggestedFix = rule.SuggestedFix,
                                    IsFixable = rule.IsFixable,
                                    IssueType = "Grep"
                                };
                                issues.Add(issue);
                                IssueFound?.Invoke(issue);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    AppLogger.Warning($"GrepAuditor could not scan {filePath}", ex);
                }
                return issues;
            }, ct);
        }

        public async Task<List<IssueModel>> ScanDirectoryAsync(string dirPath, AuditMode mode,
            IProgress<(string file, int processed, int total)>? progress, CancellationToken ct = default)
        {
            var allIssues = new List<IssueModel>();

            var files = GetCSharpFiles(dirPath);
            int total = files.Count;
            AppLogger.Info($"GrepAuditor scanning {total} files in {dirPath} (mode={mode})");

            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((files[i], i + 1, total));

                var fileIssues = await ScanFileAsync(files[i], ct);
                allIssues.AddRange(fileIssues);
            }

            AppLogger.Info($"GrepAuditor found {allIssues.Count} issues across {total} files.");
            return allIssues;
        }

        private static List<string> GetCSharpFiles(string root)
        {
            var result = new List<string>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (IsExcluded(file)) continue;
                    result.Add(file);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Failed to enumerate files in {root}", ex);
            }
            return result;
        }

        private static bool IsExcluded(string path)
        {
            foreach (string dir in ExcludedDirs)
                if (path.Contains($"\\{dir}\\", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains($"/{dir}/", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private sealed record AuditRule(
            string RuleId, string Name, string Category,
            IssueSeverity Severity, string Pattern, string SuggestedFix, bool IsFixable)
        {
            public Regex Regex { get; } = new(Pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        }
    }
}
