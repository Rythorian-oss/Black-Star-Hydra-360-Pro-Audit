using System.Text.RegularExpressions;
using BlackStarHydra360.Logging;
using BlackStarHydra360.Repair;

namespace BlackStarHydra360.Refactor;

/// <summary>
/// Refactors C# MVVM projects (.NET Core or .NET Framework/WPF) into
/// Clean Architecture structure. Ported from Refactor-CleanArchitectureWpf.ps1.
/// Creates backup, builds folder hierarchy, migrates files, rewrites namespaces/XAML,
/// patches .csproj XML, and cleans up. Supports dry-run (WhatIf) mode.
/// </summary>
public sealed class CleanArchitectureRefactor : IDisposable
{
    private readonly BackupManager _backupManager;
    private bool _disposed;

    private static readonly Dictionary<string, string> DirectoryMap = new()
    {
        ["Models"] = "Core\\Models",
        ["Interfaces"] = "Core\\Interfaces",
        ["Services"] = "Infrastructure\\Services",
        ["ViewModels"] = "Presentation\\ViewModels",
        ["Converters"] = "Presentation\\Converters",
        ["Views"] = "Presentation\\Views",
        ["Themes"] = "Presentation\\Resources\\Themes",
        ["Images"] = "Presentation\\Resources\\Images",
        ["Controls"] = "Presentation\\Views\\Controls"
    };

    public BackupManager Backups => _backupManager;

    public CleanArchitectureRefactor()
    {
        _backupManager = new BackupManager();
    }

    public class RefactorProgress
    {
        public string Phase { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Total { get; set; }
    }

    /// <summary>
    /// Runs the full clean architecture refactoring pipeline.
    /// If whatIf is true, only reports what would be done without making changes.
    /// </summary>
    public Task<string> RunAsync(
        string projectPath,
        string? baseNamespace,
        bool whatIf,
        IProgress<RefactorProgress>? progress,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException($"Project directory not found: {projectPath}");

        // Auto-detect base namespace from .csproj
        if (string.IsNullOrWhiteSpace(baseNamespace))
        {
            var csproj = Directory.GetFiles(projectPath, "*.csproj").FirstOrDefault();
            if (csproj == null)
                throw new FileNotFoundException("No .csproj file found in project directory.");
            baseNamespace = Path.GetFileNameWithoutExtension(csproj);
            AppLogger.Info($"Auto-detected base namespace: {baseNamespace}");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"⚡ Starting Universal Architectural Refactor for [{baseNamespace}]");
        sb.AppendLine($"   Path: {projectPath}");
        sb.AppendLine($"   Mode: {(whatIf ? "DRY RUN (WhatIf)" : "LIVE")}");
        sb.AppendLine();

        // Phase 1: Backup
        progress?.Report(new RefactorProgress { Phase = "Backup", Message = "Creating safety backup..." });
        if (!whatIf)
        {
            _backupManager.BackupDirectory(projectPath);
            sb.AppendLine($"📦 Backup created at: {_backupManager.BackupRoot}");
        }
        else
        {
            sb.AppendLine("📦 [WhatIf] Would create backup");
        }

        // Phase 2: Create directory hierarchy
        progress?.Report(new RefactorProgress { Phase = "Structure", Message = "Building folder hierarchy..." });
        sb.AppendLine("📁 Building new enterprise folder hierarchy...");
        foreach (var newDir in DirectoryMap.Values)
        {
            string targetPath = Path.Combine(projectPath, newDir);
            if (!Directory.Exists(targetPath))
            {
                if (!whatIf)
                    Directory.CreateDirectory(targetPath);
                sb.AppendLine($"   + {newDir}");
            }
        }
        sb.AppendLine();

        // Phase 3: Migrate files
        progress?.Report(new RefactorProgress { Phase = "Migration", Message = "Migrating files to new sectors..." });
        sb.AppendLine("🚚 Migrating files...");
        int totalMaps = DirectoryMap.Count;
        int step = 0;
        foreach (var map in DirectoryMap)
        {
            ct.ThrowIfCancellationRequested();
            step++;
            progress?.Report(new RefactorProgress
            {
                Phase = "Migration",
                Message = $"Moving {map.Key} → {map.Value}",
                Current = step,
                Total = totalMaps
            });

            string oldDir = Path.Combine(projectPath, map.Key);
            string newDir = Path.Combine(projectPath, map.Value);

            if (Directory.Exists(oldDir))
            {
                if (whatIf)
                {
                    var files = Directory.GetFiles(oldDir, "*", SearchOption.AllDirectories);
                    sb.AppendLine($"   [WhatIf] {map.Key} → {map.Value} ({files.Length} files)");
                }
                else
                {
                    foreach (string file in Directory.GetFiles(oldDir, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(oldDir, file);
                        string dest = Path.Combine(newDir, relativePath);
                        string? dir = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        File.Move(file, dest, overwrite: true);
                    }
                    sb.AppendLine($"   ✓ {map.Key} → {map.Value}");
                }
            }
        }
        sb.AppendLine();

        // Phase 4: Semantic rewrite
        progress?.Report(new RefactorProgress { Phase = "Rewrite", Message = "Rewriting namespaces and paths..." });
        sb.AppendLine("📝 Rewriting namespaces, XAML paths, and .csproj bindings...");

        var filesToProcess = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml") || f.EndsWith(".csproj"))
            .ToList();

        var namespaceUpdates = new Dictionary<string, string>
        {
            [$@"\b{Regex.Escape(baseNamespace)}\.Models\b"] = $"{baseNamespace}.Core.Models",
            [$@"\b{Regex.Escape(baseNamespace)}\.Interfaces\b"] = $"{baseNamespace}.Core.Interfaces",
            [$@"\b{Regex.Escape(baseNamespace)}\.Services\b"] = $"{baseNamespace}.Infrastructure.Services",
            [$@"\b{Regex.Escape(baseNamespace)}\.ViewModels\b"] = $"{baseNamespace}.Presentation.ViewModels",
            [$@"\b{Regex.Escape(baseNamespace)}\.Views\b"] = $"{baseNamespace}.Presentation.Views",
            [$@"\b{Regex.Escape(baseNamespace)}\.Converters\b"] = $"{baseNamespace}.Presentation.Converters",
            [$@"\b{Regex.Escape(baseNamespace)}\.Controls\b"] = $"{baseNamespace}.Presentation.Views.Controls"
        };

        int fileStep = 0;
        int totalFiles = filesToProcess.Count;
        foreach (string file in filesToProcess)
        {
            ct.ThrowIfCancellationRequested();
            fileStep++;
            progress?.Report(new RefactorProgress
            {
                Phase = "Rewrite",
                Message = $"Processing {Path.GetFileName(file)}",
                Current = fileStep,
                Total = totalFiles
            });

            if (whatIf)
            {
                sb.AppendLine($"   [WhatIf] Would update: {Path.GetFileName(file)}");
                continue;
            }

            if (!File.Exists(file)) continue;

            string content = File.ReadAllText(file);
            string original = content;

            // 1. Namespace updates
            foreach (var ns in namespaceUpdates)
                content = Regex.Replace(content, ns.Key, ns.Value);

            // 2. App.xaml StartupUri
            if (Path.GetFileName(file) == "App.xaml")
            {
                content = content.Replace("StartupUri=\"Views/", "StartupUri=\"Presentation/Views/");
                content = content.Replace("StartupUri=\"MainWindow.xaml\"",
                    "StartupUri=\"Presentation/Views/MainWindow.xaml\"");
            }

            // 3. .csproj XML path patching
            if (file.EndsWith(".csproj"))
            {
                foreach (var map in DirectoryMap)
                {
                    string oldXml = $"Include=\"{map.Key}\\";
                    string newXml = $"Include=\"{map.Value}\\";
                    content = content.Replace(oldXml, newXml);

                    string oldPage = $"Page Include=\"{map.Key}\\";
                    string newPage = $"Page Include=\"{map.Value}\\";
                    content = content.Replace(oldPage, newPage);
                }
            }

            // 4. XAML ResourceDictionary Source paths
            if (file.EndsWith(".xaml"))
            {
                foreach (var map in DirectoryMap)
                {
                    string oldPath = $"Source=\"{map.Key}/";
                    string newPath = $"Source=\"{map.Value.Replace('\\', '/')}/";
                    content = content.Replace(oldPath, newPath);
                }
            }

            if (content != original)
            {
                File.WriteAllText(file, content);
                sb.AppendLine($"   ✓ Updated: {Path.GetFileName(file)}");
            }
        }
        sb.AppendLine();

        // Phase 5: Cleanup
        if (!whatIf)
        {
            progress?.Report(new RefactorProgress { Phase = "Cleanup", Message = "Cleaning up stale directories..." });
            sb.AppendLine("🧹 Cleaning up stale directories...");
            foreach (string oldDir in DirectoryMap.Keys)
            {
                string targetDir = Path.Combine(projectPath, oldDir);
                if (Directory.Exists(targetDir) && !Directory.EnumerateFileSystemEntries(targetDir).Any())
                {
                    Directory.Delete(targetDir, recursive: true);
                    sb.AppendLine($"   - Removed empty: {oldDir}");
                }
            }

            foreach (string buildDir in new[] { "bin", "obj" })
            {
                string path = Path.Combine(projectPath, buildDir);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                    sb.AppendLine($"   - Purged: {buildDir}");
                }
            }
        }

        progress?.Report(new RefactorProgress { Phase = "Complete", Message = "Refactoring complete!" });
        sb.AppendLine();
        sb.AppendLine($"🎉 SUCCESS: Clean Architecture refactoring complete for [{baseNamespace}]");
        sb.AppendLine($"   New structure: Core (Domain) | Infrastructure (IO) | Presentation (UI)");
        if (!whatIf)
            sb.AppendLine($"   Backup at: {_backupManager.BackupRoot}");
        sb.AppendLine($"   Next Step: Open project in Visual Studio and Rebuild Solution");

        AppLogger.Info("CleanArchitectureRefactor completed successfully.");
        return Task.FromResult(sb.ToString());
    }

    public int RollbackAll() => _backupManager.RollbackAll();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _backupManager.Dispose();
        AppLogger.Info("CleanArchitectureRefactor disposed.");
    }
}
