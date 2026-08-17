using BlackStarHydra360.Core;
using BlackStarHydra360.Logging;
using BlackStarHydra360.Repair;
using BlackStarHydra360.Refactor;
using BlackStarHydra360.Auditors;

namespace BlackStarHydra360.UI;

/// <summary>
/// Main application form with tabbed interface:
/// - Audit: Run grep + Roslyn audits on a directory
/// - Repair: Auto-fix or custom-fix issues with rollback
/// - Refactor: Clean architecture refactoring
/// - Console: Live log output
/// </summary>
public class MainForm : Form
{
    // Core state
    private AuditEngine? _auditEngine;
    private RepairEngine? _repairEngine;
    private CleanArchitectureRefactor? _refactorEngine;
    private AuditResult? _lastResult;
    private CancellationTokenSource? _cts;

    // UI containers
    private TabControl _tabControl = null!;
    private GlowPanel _header = null!;
    private Label _lblTitle = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progressBar = null!;

    // Audit tab
    private TextBox _txtAuditPath = null!;
    private Button _btnBrowseAudit = null!;
    private ComboBox _cmbMode = null!;
    private Button _btnScan = null!;
    private Button _btnCancel = null!;
    private AuditDataGridView _dataGrid = null!;
    private Button _btnExportReport = null!;
    private ComboBox _cmbReportFormat = null!;

    // Repair tab
    private Button _btnFixAll = null!;
    private Button _btnFixSelected = null!;
    private Button _btnRollback = null!;
    private Label _lblRepairInfo = null!;

    // Refactor tab
    private TextBox _txtRefactorPath = null!;
    private Button _btnBrowseRefactor = null!;
    private CheckBox _chkWhatIf = null!;
    private Button _btnRefactor = null!;

    // Console tab
    private RichTextBox _rtbConsole = null!;

    public MainForm()
    {
        InitializeComponent();
        SetupEvents();
        DarkTheme.ApplyToForm(this);
        SubscribeToLogger();
    }

    private void InitializeComponent()
    {
        Text = "★ Black Star Hydra 360 Pro Audit";
        Size = new Size(1200, 800);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        // Header
        _header = new GlowPanel
        {
            GlowColor = DarkTheme.GlowBlue,
            CornerRadius = 8,
            BackColor = DarkTheme.BackgroundSecondary,
            Location = new Point(10, 10),
            Size = new Size(1160, 70),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblTitle = new Label
        {
            Text = "★ BLACK STAR HYDRA 360",
            Font = DarkTheme.Header,
            ForeColor = DarkTheme.AccentBlue,
            BackColor = Color.Transparent,
            Location = new Point(20, 10),
            Size = new Size(400, 35)
        };

        var lblSub = new Label
        {
            Text = "Pro Audit — Grep + Roslyn Hybrid Code Analysis & Auto-Repair",
            Font = new Font("Segoe UI", 9f),
            ForeColor = DarkTheme.TextDim,
            BackColor = Color.Transparent,
            Location = new Point(22, 40),
            Size = new Size(500, 20)
        };

        _header.Controls.Add(_lblTitle);
        _header.Controls.Add(lblSub);

        // Tab control
        _tabControl = new TabControl
        {
            Location = new Point(10, 90),
            Size = new Size(1160, 630),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        StyleTabControl();

        // ── AUDIT TAB ──────────────────────────────────
        var auditTab = new TabPage("🔍 Audit");
        BuildAuditTab(auditTab);
        _tabControl.TabPages.Add(auditTab);

        // ── REPAIR TAB ──────────────────────────────────
        var repairTab = new TabPage("🔧 Repair & Rollback");
        BuildRepairTab(repairTab);
        _tabControl.TabPages.Add(repairTab);

        // ── REFACTOR TAB ────────────────────────────────
        var refactorTab = new TabPage("🏗 Refactor");
        BuildRefactorTab(refactorTab);
        _tabControl.TabPages.Add(refactorTab);

        // ── CONSOLE TAB ────────────────────────────────
        var consoleTab = new TabPage("📊 Console");
        BuildConsoleTab(consoleTab);
        _tabControl.TabPages.Add(consoleTab);

        // Progress bar + status
        _progressBar = new ProgressBar
        {
            Location = new Point(10, 728),
            Size = new Size(800, 16),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Style = ProgressBarStyle.Continuous,
            Minimum = 0, Maximum = 100, Value = 0, Visible = false
        };
        DarkTheme.ApplyToProgressBar(_progressBar);

        _lblStatus = new Label
        {
            Text = "Ready — select a directory and run an audit.",
            Location = new Point(10, 748),
            Size = new Size(1100, 20),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = DarkTheme.TextSecondary
        };

        Controls.Add(_header);
        Controls.Add(_tabControl);
        Controls.Add(_progressBar);
        Controls.Add(_lblStatus);
    }

    private void StyleTabControl()
    {
        _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabControl.DrawItem += (s, e) =>
        {
            var tc = (TabControl)s!;
            e.Graphics.FillRectangle(new SolidBrush(DarkTheme.BackgroundPrimary), e.Bounds);
            var tab = tc.TabPages[e.Index];
            bool selected = e.Index == tc.SelectedIndex;
            Color bg = selected ? DarkTheme.BackgroundElevated : DarkTheme.BackgroundSecondary;
            Color fg = selected ? DarkTheme.AccentBlue : DarkTheme.TextDim;
            using var brush = new SolidBrush(bg);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tab.Text, new Font("Segoe UI Semibold", 9f),
                e.Bounds, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        _tabControl.Padding = new Point(20, 8);
    }

    private void BuildAuditTab(TabPage tab)
    {
        tab.BackColor = DarkTheme.BackgroundPrimary;
        tab.Padding = new Padding(10);

        var lblPath = new Label { Text = "Target Directory:", Location = new Point(10, 15), Size = new Size(100, 25) };
        DarkTheme.ApplyToLabel(lblPath);

        _txtAuditPath = new TextBox { Location = new Point(115, 13), Size = new Size(700, 25), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        DarkTheme.ApplyToTextBox(_txtAuditPath);

        _btnBrowseAudit = new Button { Text = "📁 Browse...", Location = new Point(825, 11), Size = new Size(90, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        DarkTheme.ApplyToButton(_btnBrowseAudit);

        var lblMode = new Label { Text = "Mode:", Location = new Point(10, 50), Size = new Size(50, 25) };
        DarkTheme.ApplyToLabel(lblMode);

        _cmbMode = new ComboBox { Location = new Point(65, 48), Size = new Size(200, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        DarkTheme.ApplyToComboBox(_cmbMode);
        foreach (var mode in Enum.GetValues<AuditMode>())
            _cmbMode.Items.Add(mode);
        _cmbMode.SelectedIndex = (int)AuditMode.Full;
        _cmbMode.DisplayMember = "ToString";

        _btnScan = new Button { Text = "▶ Run Audit", Location = new Point(280, 46), Size = new Size(120, 30), Enabled = false };
        DarkTheme.ApplyToPrimaryButton(_btnScan);

        _btnCancel = new Button { Text = "✕ Cancel", Location = new Point(410, 46), Size = new Size(90, 30), Enabled = false, Visible = false };
        DarkTheme.ApplyToButton(_btnCancel);

        _btnExportReport = new Button { Text = "📄 Export Report", Location = new Point(825, 46), Size = new Size(120, 30), Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        DarkTheme.ApplyToButton(_btnExportReport);

        _cmbReportFormat = new ComboBox { Location = new Point(955, 48), Size = new Size(100, 25), DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        DarkTheme.ApplyToComboBox(_cmbReportFormat);
        _cmbReportFormat.Items.AddRange(new object[] { "TXT", "JSON", "HTML", "CSV" });
        _cmbReportFormat.SelectedIndex = 0;

        _dataGrid = new AuditDataGridView
        {
            Location = new Point(10, 90),
            Size = new Size(1140, 450),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        tab.Controls.AddRange(new Control[] { lblPath, _txtAuditPath, _btnBrowseAudit, lblMode, _cmbMode, _btnScan, _btnCancel, _btnExportReport, _cmbReportFormat, _dataGrid });
    }

    private void BuildRepairTab(TabPage tab)
    {
        tab.BackColor = DarkTheme.BackgroundPrimary;
        tab.Padding = new Padding(10);

        _lblRepairInfo = new Label
        {
            Text = "Run an audit first, then use the buttons below to automatically fix issues.\nBackups are created before any file is modified — use Rollback to restore.",
            Location = new Point(10, 15),
            Size = new Size(1100, 50),
            ForeColor = DarkTheme.TextSecondary
        };

        _btnFixAll = new Button
        {
            Text = "🔧 Fix All (Automatic Mode)",
            Location = new Point(10, 70),
            Size = new Size(220, 40),
            Enabled = false
        };
        DarkTheme.ApplyToPrimaryButton(_btnFixAll);

        _btnFixSelected = new Button
        {
            Text = "🎯 Fix Selected (Custom Mode)",
            Location = new Point(245, 70),
            Size = new Size(220, 40),
            Enabled = false
        };
        DarkTheme.ApplyToButton(_btnFixSelected);

        _btnRollback = new Button
        {
            Text = "↩ Rollback All Changes",
            Location = new Point(480, 70),
            Size = new Size(200, 40),
            Enabled = false
        };
        DarkTheme.ApplyToButton(_btnRollback);

        tab.Controls.AddRange(new Control[] { _lblRepairInfo, _btnFixAll, _btnFixSelected, _btnRollback });
    }

    private void BuildRefactorTab(TabPage tab)
    {
        tab.BackColor = DarkTheme.BackgroundPrimary;
        tab.Padding = new Padding(10);

        var lblPath = new Label { Text = "Project Directory:", Location = new Point(10, 15), Size = new Size(120, 25) };
        DarkTheme.ApplyToLabel(lblPath);

        _txtRefactorPath = new TextBox { Location = new Point(135, 13), Size = new Size(680, 25), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        DarkTheme.ApplyToTextBox(_txtRefactorPath);

        _btnBrowseRefactor = new Button { Text = "📁 Browse...", Location = new Point(825, 11), Size = new Size(90, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        DarkTheme.ApplyToButton(_btnBrowseRefactor);

        _chkWhatIf = new CheckBox
        {
            Text = "Dry Run (WhatIf) — preview only, no changes",
            Location = new Point(10, 50),
            Size = new Size(350, 25),
            ForeColor = DarkTheme.TextSecondary,
            Checked = true
        };

        _btnRefactor = new Button
        {
            Text = "🏗 Run Refactor",
            Location = new Point(370, 48),
            Size = new Size(150, 30),
            Enabled = false
        };
        DarkTheme.ApplyToPrimaryButton(_btnRefactor);

        var lblInfo = new Label
        {
            Text = "Refactors C# MVVM projects into Clean Architecture (Core/Infrastructure/Presentation).\n" +
                   "Migrates files, rewrites namespaces, fixes XAML paths, and patches .csproj XML.\n" +
                   "A full backup is created before any changes are made.",
            Location = new Point(10, 90),
            Size = new Size(1100, 60),
            ForeColor = DarkTheme.TextDim
        };

        tab.Controls.AddRange(new Control[] { lblPath, _txtRefactorPath, _btnBrowseRefactor, _chkWhatIf, _btnRefactor, lblInfo });
    }

    private void BuildConsoleTab(TabPage tab)
    {
        tab.BackColor = DarkTheme.BackgroundPrimary;
        _rtbConsole = new RichTextBox
        {
            Location = new Point(10, 10),
            Size = new Size(1140, 530),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        DarkTheme.ApplyToRichTextBox(_rtbConsole);
        tab.Controls.Add(_rtbConsole);

        var btnClear = new Button
        {
            Text = "Clear Console",
            Location = new Point(10, 550),
            Size = new Size(120, 30)
        };
        DarkTheme.ApplyToButton(btnClear);
        btnClear.Click += (s, e) => _rtbConsole.Clear();
        tab.Controls.Add(btnClear);
    }

    // ── Event Wiring ──────────────────────────────────

    private void SetupEvents()
    {
        _btnBrowseAudit.Click += OnBrowseAudit;
        _btnScan.Click += OnScan;
        _btnCancel.Click += OnCancel;
        _btnExportReport.Click += OnExportReport;
        _txtAuditPath.TextChanged += (s, e) => _btnScan.Enabled = !string.IsNullOrWhiteSpace(_txtAuditPath.Text);

        _btnFixAll.Click += OnFixAll;
        _btnFixSelected.Click += OnFixSelected;
        _btnRollback.Click += OnRollback;

        _btnBrowseRefactor.Click += OnBrowseRefactor;
        _btnRefactor.Click += OnRefactor;
        _txtRefactorPath.TextChanged += (s, e) => _btnRefactor.Enabled = !string.IsNullOrWhiteSpace(_txtRefactorPath.Text);

        FormClosing += OnFormClosing;
    }

    private void SubscribeToLogger()
    {
        AppLogger.LogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(LogEntry entry)
    {
        if (_rtbConsole.IsDisposed) return;
        if (_rtbConsole.InvokeRequired)
        {
            _rtbConsole.BeginInvoke(() => AppendLog(entry));
        }
        else
        {
            AppendLog(entry);
        }
    }

    private void AppendLog(LogEntry entry)
    {
        Color color = entry.Level switch
        {
            LogLevel.Error => DarkTheme.AccentRed,
            LogLevel.Critical => DarkTheme.AccentRedBright,
            LogLevel.Warning => DarkTheme.AccentOrange,
            LogLevel.Info => DarkTheme.TextSecondary,
            _ => DarkTheme.TextDim
        };

        _rtbConsole.SelectionColor = color;
        _rtbConsole.AppendText(entry.ToString() + "\n");
        _rtbConsole.ScrollToCaret();
    }

    private void SetStatus(string text)
    {
        if (_lblStatus.InvokeRequired)
            _lblStatus.BeginInvoke(() => _lblStatus.Text = text);
        else
            _lblStatus.Text = text;
    }

    private void SetProgress(int percent)
    {
        if (_progressBar.InvokeRequired)
            _progressBar.BeginInvoke(() =>
            {
                _progressBar.Visible = percent > 0 && percent < 100;
                _progressBar.Value = Math.Clamp(percent, 0, 100);
            });
        else
        {
            _progressBar.Visible = percent > 0 && percent < 100;
            _progressBar.Value = Math.Clamp(percent, 0, 100);
        }
    }

    // ── Audit ──────────────────────────────────────────

    private void OnBrowseAudit(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select directory to audit",
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            _txtAuditPath.Text = dialog.SelectedPath;
    }

    private async void OnScan(object? sender, EventArgs e)
    {
        string path = _txtAuditPath.Text.Trim();
        if (!Directory.Exists(path))
        {
            MessageBox.Show("Directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var mode = (AuditMode)_cmbMode.SelectedIndex;
        _cts = new CancellationTokenSource();

        _btnScan.Enabled = false;
        _btnCancel.Enabled = true;
        _btnCancel.Visible = true;
        _btnExportReport.Enabled = false;
        _btnFixAll.Enabled = false;
        _btnFixSelected.Enabled = false;
        _dataGrid.ClearIssues();
        SetStatus("Running audit...");

        _auditEngine = new AuditEngine();
        var issues = new List<IssueModel>();
        _auditEngine.IssueFound += issue =>
        {
            issues.Add(issue);
            if (_dataGrid.InvokeRequired)
                _dataGrid.BeginInvoke(() => _dataGrid.AddIssue(issue));
            else
                _dataGrid.AddIssue(issue);
        };

        var progress = new Progress<AuditProgress>(p =>
        {
            SetStatus($"{p.Phase}: {p.Message} ({p.IssuesFound} issues found)");
            if (p.TotalFiles > 0)
                SetProgress((int)(p.FilesProcessed * 100.0 / p.TotalFiles));
        });

        try
        {
            _lastResult = await _auditEngine.RunAuditAsync(path, mode, progress, _cts.Token);
            SetStatus($"Complete: {_lastResult.Summary}");
            SetProgress(100);
            _btnExportReport.Enabled = true;
            _btnFixAll.Enabled = _lastResult.FixableCount > 0;
            _btnFixSelected.Enabled = _lastResult.FixableCount > 0;
            MessageBox.Show(
                $"Audit complete!\n\n{_lastResult.Summary}\n\nFiles scanned: {_lastResult.FilesScanned}\n" +
                $"Issues: {_lastResult.TotalIssues}\nFixable: {_lastResult.FixableCount}",
                "Audit Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Audit cancelled.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Audit failed", ex);
            SetStatus($"Error: {ex.Message}");
            MessageBox.Show($"Audit error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnScan.Enabled = true;
            _btnCancel.Enabled = false;
            _btnCancel.Visible = false;
            SetProgress(0);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnCancel(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        SetStatus("Cancelling...");
    }

    private void OnExportReport(object? sender, EventArgs e)
    {
        if (_lastResult == null) return;

        string format = _cmbReportFormat.SelectedItem?.ToString() ?? "TXT";
        string ext = format.ToLowerInvariant();
        using var dialog = new SaveFileDialog
        {
            Title = "Save Audit Report",
            Filter = $"{format} Report|*.{ext}|All Files|*.*",
            FileName = $"hydra_audit_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            string content = format switch
            {
                "JSON" => ReportGenerator.GenerateJsonReport(_lastResult),
                "HTML" => ReportGenerator.GenerateHtmlReport(_lastResult),
                "CSV" => ReportGenerator.GenerateCsvReport(_lastResult),
                _ => ReportGenerator.GenerateTextReport(_lastResult)
            };
            try
            {
                ReportGenerator.SaveReport(dialog.FileName, content);
                MessageBox.Show($"Report saved to:\n{dialog.FileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ── Repair ─────────────────────────────────────────

    private async void OnFixAll(object? sender, EventArgs e)
    {
        if (_lastResult == null) return;

        _repairEngine = new RepairEngine();
        _btnFixAll.Enabled = false;
        _btnFixSelected.Enabled = false;
        _btnRollback.Enabled = false;
        SetStatus("Repairing (automatic mode)...");

        var progress = new Progress<(int fixed_, int total, string current)>(p =>
        {
            SetStatus($"Fixing: {p.fixed_}/{p.total} — {Path.GetFileName(p.current)}");
            if (p.total > 0) SetProgress(p.fixed_ * 100 / p.total);
        });

        try
        {
            var modified = await _repairEngine.FixAllAsync(_lastResult, progress, CancellationToken.None);
            SetStatus($"Repaired {modified.Count} files. Backups at: {_repairEngine.Backups.BackupRoot}");
            SetProgress(100);
            _btnRollback.Enabled = true;
            MessageBox.Show($"Fixed {modified.Count} files.\n\nBackups saved at:\n{_repairEngine.Backups.BackupRoot}\n\nUse Rollback to restore originals.",
                "Repair Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Repair failed", ex);
            SetStatus($"Repair error: {ex.Message}");
            MessageBox.Show($"Repair error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetProgress(0);
        }
    }

    private async void OnFixSelected(object? sender, EventArgs e)
    {
        if (_lastResult == null) return;

        var selectedIssues = _dataGrid.GetSelectedIssues(_lastResult.Issues);
        if (selectedIssues.Count == 0)
        {
            MessageBox.Show("Select rows in the Audit tab grid first (Ctrl+Click for multi-select).", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _repairEngine = _repairEngine ?? new RepairEngine();
        _btnFixAll.Enabled = false;
        _btnFixSelected.Enabled = false;
        SetStatus($"Repairing {selectedIssues.Count} selected issues (custom mode)...");

        var progress = new Progress<(int fixed_, int total, string current)>(p =>
        {
            SetStatus($"Custom fix: {p.fixed_}/{p.total}");
            if (p.total > 0) SetProgress(p.fixed_ * 100 / p.total);
        });

        try
        {
            var modified = await _repairEngine.FixSelectedAsync(selectedIssues, progress, CancellationToken.None);
            SetStatus($"Custom-repaired {modified.Count} files.");
            _btnRollback.Enabled = true;
            MessageBox.Show($"Fixed {modified.Count} files (custom mode).",
                "Repair Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Custom repair failed", ex);
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            SetProgress(0);
        }
    }

    private void OnRollback(object? sender, EventArgs e)
    {
        if (_repairEngine == null) return;

        var confirm = MessageBox.Show(
            "Rollback ALL changes made during this repair session?\nThis restores all files to their pre-repair state.",
            "Confirm Rollback", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        int rolledBack = _repairEngine.RollbackAll();
        SetStatus($"Rolled back {rolledBack} files.");
        _btnRollback.Enabled = false;
        MessageBox.Show($"Successfully rolled back {rolledBack} files.", "Rollback Complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Refactor ───────────────────────────────────────

    private void OnBrowseRefactor(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select project directory (where .csproj lives)",
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            _txtRefactorPath.Text = dialog.SelectedPath;
    }

    private async void OnRefactor(object? sender, EventArgs e)
    {
        string path = _txtRefactorPath.Text.Trim();
        if (!Directory.Exists(path))
        {
            MessageBox.Show("Directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool whatIf = _chkWhatIf.Checked;
        _refactorEngine = new CleanArchitectureRefactor();
        _btnRefactor.Enabled = false;
        SetStatus(whatIf ? "Running dry-run refactor..." : "Running refactor (changes will be made)...");

        var progress = new Progress<CleanArchitectureRefactor.RefactorProgress>(p =>
        {
            SetStatus($"{p.Phase}: {p.Message}");
            if (p.Total > 0) SetProgress(p.Current * 100 / p.Total);
        });

        try
        {
            string report = await _refactorEngine.RunAsync(path, null, whatIf, progress, CancellationToken.None);
            SetProgress(100);
            _rtbConsole.AppendText("\n" + report + "\n");
            _tabControl.SelectedIndex = 3; // Switch to console tab

            if (!whatIf)
                _btnRollback.Enabled = true;

            MessageBox.Show(report, whatIf ? "Dry Run Complete" : "Refactor Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Refactor failed", ex);
            SetStatus($"Refactor error: {ex.Message}");
            MessageBox.Show($"Refactor error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnRefactor.Enabled = true;
            SetProgress(0);
        }
    }

    // ── Cleanup ────────────────────────────────────────

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        AppLogger.LogEmitted -= OnLogEmitted;
        _cts?.Cancel();
        _cts?.Dispose();
        _auditEngine?.Dispose();
        _repairEngine?.Dispose();
        _refactorEngine?.Dispose();
        AppLogger.Info("Black Star Hydra 360 shutting down.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _auditEngine?.Dispose();
            _repairEngine?.Dispose();
            _refactorEngine?.Dispose();
            _cts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
