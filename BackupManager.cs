using BlackStarHydra360.Logging;

namespace BlackStarHydra360.Repair;

/// <summary>
/// Creates timestamped backups of files before any modifications.
/// Supports full directory backup and per-file backup with rollback.
/// </summary>
public sealed class BackupManager : IDisposable
{
    private readonly string _backupRoot;
    private readonly List<FileBackup> _backups = new();
    private readonly List<DirectoryBackup> _directoryBackups = new();
    private readonly object _lock = new();
    private bool _disposed;

    public string BackupRoot => _backupRoot;
    public int BackupCount => _backups.Count;

    public BackupManager()
    {
        _backupRoot = Path.Combine(Path.GetTempPath(), "BlackStarHydra", "backups",
            $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(_backupRoot);
        AppLogger.Info($"BackupManager initialized: {_backupRoot}");
    }

    /// <summary>Backs up a single file before modification. Returns the backup path.</summary>
    public string BackupFile(string filePath)
    {
        ThrowIfDisposed();
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Cannot backup — file not found: {filePath}");

        lock (_lock)
        {
            // Never derive a backup path directly from an untrusted absolute
            // path: rooted segments and ".." can escape the backup directory.
            string backupName = $"{_backups.Count:D6}_{Path.GetFileName(fullPath)}";
            string backupPath = Path.Combine(_backupRoot, backupName);
            string? dir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(fullPath, backupPath, overwrite: true);

            _backups.Add(new FileBackup
            {
                OriginalPath = fullPath,
                BackupPath = backupPath,
                Timestamp = DateTime.Now
            });

            AppLogger.Info($"Backed up: {Path.GetFileName(filePath)} → {backupPath}");
            return backupPath;
        }
    }

    /// <summary>Backs up an entire directory tree before bulk modifications.</summary>
    public void BackupDirectory(string sourceDir)
    {
        ThrowIfDisposed();
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Cannot backup — directory not found: {sourceDir}");

        string dest = Path.Combine(_backupRoot, new DirectoryInfo(sourceDir).Name);
        CopyDirectory(sourceDir, dest);
        lock (_lock)
        {
            _directoryBackups.Add(new DirectoryBackup
            {
                OriginalPath = Path.GetFullPath(sourceDir),
                BackupPath = dest,
                Timestamp = DateTime.Now
            });
        }
        AppLogger.Info($"Directory backed up: {sourceDir} → {dest}");
    }

    /// <summary>Rolls back a single file to its backup.</summary>
    public bool RollbackFile(string filePath)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            var backup = _backups.LastOrDefault(
                b => string.Equals(b.OriginalPath, Path.GetFullPath(filePath),
                    StringComparison.OrdinalIgnoreCase));
            if (backup == null || !File.Exists(backup.BackupPath))
            {
                AppLogger.Warning($"No backup found for rollback: {filePath}");
                return false;
            }

            string? directory = Path.GetDirectoryName(backup.OriginalPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.Copy(backup.BackupPath, backup.OriginalPath, overwrite: true);
            AppLogger.Info($"Rolled back: {Path.GetFileName(filePath)}");
            return true;
        }
    }

    /// <summary>Rolls back ALL files that were backed up.</summary>
    public int RollbackAll()
    {
        ThrowIfDisposed();
        int rolledBack = 0;
        lock (_lock)
        {
            foreach (var backup in _backups.ToList())
            {
                try
                {
                    if (File.Exists(backup.BackupPath))
                    {
                        string? directory = Path.GetDirectoryName(backup.OriginalPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);
                        File.Copy(backup.BackupPath, backup.OriginalPath, overwrite: true);
                        rolledBack++;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"Failed to rollback {backup.OriginalPath}", ex);
                }
            }
            foreach (var backup in _directoryBackups.AsEnumerable().Reverse())
            {
                try
                {
                    if (!Directory.Exists(backup.BackupPath))
                        continue;

                    if (Directory.Exists(backup.OriginalPath))
                        Directory.Delete(backup.OriginalPath, recursive: true);
                    CopyDirectory(backup.BackupPath, backup.OriginalPath);
                    rolledBack++;
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"Failed to rollback directory {backup.OriginalPath}", ex);
                }
            }
            AppLogger.Info($"Rolled back {rolledBack}/{_backups.Count} files.");
        }
        return rolledBack;
    }

    /// <summary>Returns a list of all backups with their original paths and timestamps.</summary>
    public IReadOnlyList<FileBackup> GetBackups()
    {
        lock (_lock)
        { return _backups.AsReadOnly(); }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BackupManager));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Note: we do NOT delete backups — user may need them for manual rollback
        AppLogger.Info($"BackupManager disposed. { _backups.Count} backups at {_backupRoot}");
    }
}

public sealed class FileBackup
{
    public string OriginalPath { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public sealed class DirectoryBackup
{
    public string OriginalPath { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
