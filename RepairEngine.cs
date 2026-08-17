using System.Text.RegularExpressions;
using BlackStarHydra360.Core;
using BlackStarHydra360.Logging;

namespace BlackStarHydra360.Repair;

/// <summary>
/// Automatic repair engine. Backs up files before modifying, then applies fixes:
/// - Redacts hardcoded credentials → [REDACTED_BY_HYDRA]
/// - Removes empty catch blocks → adds logging placeholder
/// - Wraps Console.WriteLine in #if DEBUG
/// - Adds GC.SuppressFinalize to Dispose methods missing it
/// Supports both "Fix All" (automatic mode) and "Fix Selected" (custom mode).
/// </summary>
public sealed class RepairEngine : IDisposable
{
    private readonly BackupManager _backupManager;
    private bool _disposed;

    public BackupManager Backups => _backupManager;

    public RepairEngine()
    {
        _backupManager = new BackupManager();
    }

    /// <summary>
    /// Fixes all fixable issues in the audit result (automatic mode).
    /// Returns the list of files that were modified.
    /// </summary>
    public Task<List<string>> FixAllAsync(
        AuditResult result,
        IProgress<(int fixedCount, int total, string current)>? progress,
        CancellationToken ct = default)
    {
        var fixable = result.Issues.Where(i => i.IsFixable).ToList();
        var modifiedFiles = new HashSet<string>();
        AppLogger.Info($"RepairEngine: fixing {fixable.Count} issues (automatic mode).");

        for (int i = 0; i < fixable.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report((i, fixable.Count, fixable[i].FilePath));

            var issue = fixable[i];
            if (!modifiedFiles.Contains(issue.FilePath))
            {
                _backupManager.BackupFile(issue.FilePath);
                modifiedFiles.Add(issue.FilePath);
            }

            ApplyFix(issue);
        }

        AppLogger.Info($"RepairEngine: modified {modifiedFiles.Count} files.");
        return Task.FromResult(modifiedFiles.ToList());
    }

    /// <summary>
    /// Fixes only the selected issues (custom mode).
    /// </summary>
    public Task<List<string>> FixSelectedAsync(
        List<IssueModel> issues,
        IProgress<(int fixedCount, int total, string current)>? progress,
        CancellationToken ct = default)
    {
        var fixable = issues.Where(i => i.IsFixable).ToList();
        var modifiedFiles = new HashSet<string>();
        AppLogger.Info($"RepairEngine: fixing {fixable.Count} selected issues (custom mode).");

        for (int i = 0; i < fixable.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report((i, fixable.Count, fixable[i].FilePath));

            var issue = fixable[i];
            if (!modifiedFiles.Contains(issue.FilePath))
            {
                _backupManager.BackupFile(issue.FilePath);
                modifiedFiles.Add(issue.FilePath);
            }

            ApplyFix(issue);
        }

        AppLogger.Info($"RepairEngine: modified {modifiedFiles.Count} files (custom mode).");
        return Task.FromResult(modifiedFiles.ToList());
    }

    /// <summary>Rolls back all changes made by this repair session.</summary>
    public int RollbackAll() => _backupManager.RollbackAll();

    /// <summary>Rolls back a single file.</summary>
    public bool RollbackFile(string filePath) => _backupManager.RollbackFile(filePath);

    private void ApplyFix(IssueModel issue)
    {
        try
        {
            string content = File.ReadAllText(issue.FilePath);
            string fixedContent = issue.RuleId switch
            {
                "SEC001" or "SEC101" => FixHardcodedSecret(content, issue.CodeSnippet),
                "SEC003" => FixRedact(content, issue.CodeSnippet),
                "SEC004" => FixRedact(content, issue.CodeSnippet),
                "SEC005" => FixRedact(content, issue.CodeSnippet),
                "QUAL005" => FixConsoleWriteLine(content),
                "QUAL006" => FixDebugWriteLine(content),
                "QUAL007" or "QUAL101" => FixEmptyCatch(content, issue.LineNumber),
                "LEAK102" => FixSuppressFinalize(content, issue.LineNumber),
                _ => content
            };

            if (fixedContent != content)
            {
                File.WriteAllText(issue.FilePath, fixedContent);
                AppLogger.Info($"Fixed [{issue.RuleId}] in {Path.GetFileName(issue.FilePath)}:L{issue.LineNumber}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to apply fix for [{issue.RuleId}] in {issue.FilePath}", ex);
        }
    }

    private static string FixHardcodedSecret(string content, string snippet)
    {
        if (string.IsNullOrEmpty(snippet)) return content;
        // Replace the secret value with [REDACTED_BY_HYDRA]
        var match = Regex.Match(snippet, @"[""']([^""']{3,})[""']");
        if (match.Success)
        {
            string secretValue = match.Groups[1].Value;
            return content.Replace("\"" + secretValue + "\"", "\"[REDACTED_BY_HYDRA]\"")
                          .Replace("'" + secretValue + "'", "'[REDACTED_BY_HYDRA]'");
        }
        return content;
    }

    private static string FixRedact(string content, string snippet)
    {
        if (string.IsNullOrEmpty(snippet)) return content;
        return content.Replace(snippet, "[REDACTED_BY_HYDRA]");
    }

    private static string FixConsoleWriteLine(string content)
    {
        // Wrap Console.WriteLine in #if DEBUG if not already wrapped
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length + 10);
        bool inDebugBlock = false;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("#if DEBUG"))
            {
                inDebugBlock = true;
                result.Add(line);
                continue;
            }
            if (inDebugBlock && trimmed.StartsWith("#endif"))
            {
                inDebugBlock = false;
                result.Add(line);
                continue;
            }

            if (!inDebugBlock && trimmed.Contains("Console.WriteLine"))
            {
                string indent = line[..^trimmed.Length];
                result.Add($"{indent}#if DEBUG");
                result.Add(line);
                result.Add($"{indent}#endif");
            }
            else
            {
                result.Add(line);
            }
        }

        return string.Join('\n', result);
    }

    private static string FixDebugWriteLine(string content)
    {
        // Replace Debug.WriteLine with a real application log call.
        return Regex.Replace(content,
            @"(?<!//\s*)Debug\.WriteLine\(([^;]+)\);",
            match => $"AppLogger.Info($\"{{{match.Groups[1].Value}}}\");");
    }

    private static string FixEmptyCatch(string content, int lineNumber)
    {
        // Replace empty catch { } with catch { AppLogger.Error("Caught exception"); }
        string fixedContent = Regex.Replace(content,
            @"catch\s*\{\s*\}",
            "catch { AppLogger.Error(\"Unhandled exception caught and logged.\"); }",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Also fix catch(Exception ex) { /* ... */ } without inventing an
        // identifier for catch blocks that do not declare one.
        fixedContent = Regex.Replace(fixedContent,
            @"catch\s*\(\s*(?<type>[^()\s]+(?:\s*<[^>]+>)?)(?:\s+(?<name>[A-Za-z_]\w*))?\s*\)\s*\{\s*/\*.*?\*/\s*\}",
            m => string.IsNullOrWhiteSpace(m.Groups["name"].Value)
                ? $"catch ({m.Groups["type"].Value}) {{ AppLogger.Error(\"Exception caught\"); }}"
                : $"catch ({m.Groups["type"].Value} {m.Groups["name"].Value}) {{ AppLogger.Error(\"Exception caught\", {m.Groups["name"].Value}); }}",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));

        return fixedContent;
    }

    private static string FixSuppressFinalize(string content, int lineNumber)
    {
        var lines = content.Split('\n');
        if (lineNumber < 1 || lineNumber > lines.Length) return content;

        // Find the Dispose method's closing brace and insert GC.SuppressFinalize before it
        for (int i = lineNumber - 1; i < Math.Min(i + 50, lines.Length); i++)
        {
            string line = lines[i];
            if (line.Contains("Dispose") && line.Contains("bool"))
            {
                // Look for closing brace of this method
                for (int j = i + 1; j < Math.Min(j + 50, lines.Length); j++)
                {
                    if (lines[j].Trim() == "}")
                    {
                        string indent = lines[j][..^1];
                        if (!content.Contains("GC.SuppressFinalize"))
                        {
                            var newLines = lines.ToList();
                            newLines.Insert(j, $"{indent}    GC.SuppressFinalize(this);");
                            return string.Join('\n', newLines);
                        }
                    }
                }
            }
        }
        return content;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _backupManager.Dispose();
        AppLogger.Info("RepairEngine disposed.");
    }
}
