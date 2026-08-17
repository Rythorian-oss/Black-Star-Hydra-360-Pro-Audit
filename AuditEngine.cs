using BlackStarHydra360.Core;
using BlackStarHydra360.Logging;

namespace BlackStarHydra360.Auditors;

/// <summary>
/// Main audit orchestrator. Coordinates GrepAuditor (fast regex) and RoslynAuditor (AST semantic)
/// to produce a comprehensive AuditResult. Runs on background threads with progress reporting.
/// </summary>
public sealed class AuditEngine : IDisposable
{
    private readonly GrepAuditor _grepAuditor = new();
    private readonly RoslynAuditor _roslynAuditor = new();
    private bool _disposed;

    public event Action<IssueModel>? IssueFound;

    public AuditEngine()
    {
        _grepAuditor.IssueFound += OnIssueFound;
        _roslynAuditor.IssueFound += OnIssueFound;
        AppLogger.Info("AuditEngine initialized (Grep + Roslyn).");
    }

    public async Task<AuditResult> RunAuditAsync(
        string targetPath,
        AuditMode mode,
        IProgress<AuditProgress>? progress,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (!Directory.Exists(targetPath) && !File.Exists(targetPath))
            throw new DirectoryNotFoundException($"Target not found: {targetPath}");

        var result = new AuditResult
        {
            TargetPath = targetPath,
            ScanDate = DateTime.Now,
            Mode = mode
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            string fullTargetPath = Path.GetFullPath(targetPath);
            bool isSingleFile = File.Exists(fullTargetPath);
            string scanPath = isSingleFile
                ? Path.GetDirectoryName(fullTargetPath)!
                : fullTargetPath;

            // Phase 1: Grep Scout
            progress?.Report(new AuditProgress { Phase = "Scout", Message = "Running Grep scout scan...", TotalFiles = 0, FilesProcessed = 0, IssuesFound = 0 });
            AppLogger.Info($"Phase 1: Grep scout scan on {scanPath}");

            var grepProgress = new Progress<(string file, int processed, int total)>(p =>
                progress?.Report(new AuditProgress
                {
                    Phase = "Scout",
                    CurrentFile = p.file,
                    FilesProcessed = p.processed,
                    TotalFiles = p.total,
                    IssuesFound = result.Issues.Count,
                    Message = $"Scanning: {Path.GetFileName(p.file)}"
                }));

            var grepIssues = isSingleFile
                ? await _grepAuditor.ScanFileAsync(fullTargetPath, ct)
                : await _grepAuditor.ScanDirectoryAsync(scanPath, mode, grepProgress, ct);
            result.Issues.AddRange(grepIssues);

            // Phase 2: Roslyn semantic analysis (skip in Fast mode)
            if (mode != AuditMode.Fast)
            {
                progress?.Report(new AuditProgress
                {
                    Phase = "SemanticAnalysis",
                    Message = "Running Roslyn AST analysis...",
                    IssuesFound = result.Issues.Count
                });
                AppLogger.Info("Phase 2: Roslyn semantic analysis");

                var roslynProgress = new Progress<(string file, int processed, int total)>(p =>
                    progress?.Report(new AuditProgress
                    {
                        Phase = "SemanticAnalysis",
                        CurrentFile = p.file,
                        FilesProcessed = p.processed,
                        TotalFiles = p.total,
                        IssuesFound = result.Issues.Count,
                        Message = $"AST: {Path.GetFileName(p.file)}"
                    }));

                var roslynIssues = isSingleFile
                    ? await _roslynAuditor.AnalyzeFileAsync(fullTargetPath, ct)
                    : await _roslynAuditor.AnalyzeDirectoryAsync(scanPath, roslynProgress, ct);
                result.Issues.AddRange(roslynIssues);
            }

            result.FilesScanned = isSingleFile
                ? 1
                : CountSourceFiles(scanPath);
        }
        catch (OperationCanceledException)
        {
            AppLogger.Warning("Audit was cancelled by user.");
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Audit engine error", ex);
            throw;
        }
        finally
        {
            sw.Stop();
            result.Duration = sw.Elapsed;
            result.ComputeSummary();
        }

        progress?.Report(new AuditProgress
        {
            Phase = "Complete",
            Message = $"Complete: {result.Summary}",
            IssuesFound = result.TotalIssues
        });

        return result;
    }

    private void OnIssueFound(IssueModel issue)
    {
        try
        {
            IssueFound?.Invoke(issue);
        }
        catch (Exception ex)
        {
            // A UI subscriber must not abort a background audit.
            AppLogger.Warning("An audit issue subscriber threw an exception.", ex);
        }
    }

    private static int CountSourceFiles(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Count(IsIncludedSourceFile);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Could not count source files in {root}.", ex);
            return 0;
        }
    }

    private static bool IsIncludedSourceFile(string path)
    {
        string normalized = path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                           segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                           segment.Equals(".git", StringComparison.OrdinalIgnoreCase));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AuditEngine));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _grepAuditor.IssueFound -= OnIssueFound;
        _roslynAuditor.IssueFound -= OnIssueFound;
        _roslynAuditor.Dispose();
        AppLogger.Info("AuditEngine disposed.");
    }
}

public sealed class AuditProgress
{
    public string Phase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CurrentFile { get; set; } = string.Empty;
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
    public int IssuesFound { get; set; }
}
