using System.Drawing;
using System.Windows.Forms;
using BlackStarHydra360.Core;

namespace BlackStarHydra360.UI;

/// <summary>
/// DataGridView specialized for displaying audit issues with color-coded severity.
/// Enhanced from PhantomAudit's AuditDataGridView.
/// </summary>
public class AuditDataGridView : DataGridView
{
    private ToolTip? _toolTip;

    public AuditDataGridView()
    {
        _toolTip = new ToolTip
        {
            IsBalloon = true,
            ToolTipTitle = "Issue Details",
            BackColor = DarkTheme.BackgroundSecondary,
            ForeColor = DarkTheme.TextPrimary
        };

        SetupStyle();
        SetupColumns();

        CellMouseEnter += OnCellMouseEnter;
        CellMouseLeave += OnCellMouseLeave;
    }

    private void SetupStyle()
    {
        EnableHeadersVisualStyles = false;
        BackgroundColor = DarkTheme.BackgroundPrimary;
        GridColor = DarkTheme.BorderColor;
        DefaultCellStyle.BackColor = DarkTheme.BackgroundPrimary;
        DefaultCellStyle.ForeColor = DarkTheme.TextPrimary;
        DefaultCellStyle.SelectionBackColor = DarkTheme.BackgroundElevated;
        DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        DefaultCellStyle.Font = new Font("Consolas", 8.5f);
        DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);

        ColumnHeadersDefaultCellStyle.BackColor = DarkTheme.BackgroundElevated;
        ColumnHeadersDefaultCellStyle.ForeColor = DarkTheme.TextSecondary;
        ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f);
        ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        ColumnHeadersHeight = 36;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        RowHeadersDefaultCellStyle.BackColor = DarkTheme.BackgroundSecondary;
        RowHeadersDefaultCellStyle.ForeColor = DarkTheme.TextDim;
        RowHeadersWidth = 36;
        RowTemplate.Height = 30;
        AlternatingRowsDefaultCellStyle.BackColor = DarkTheme.BackgroundSecondary;

        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = true; // Allow multi-select for custom repair mode
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        DoubleBuffered = true;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        ReadOnly = true;
    }

    private void SetupColumns()
    {
        Columns.Clear();
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Select", HeaderText = "✓", Width = 30, FillWeight = 4, ReadOnly = false });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "LineNumber", HeaderText = "Line", Width = 50, FillWeight = 6 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "RuleId", HeaderText = "Rule ID", Width = 70, FillWeight = 8 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", Width = 100, FillWeight = 12 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", MinimumWidth = 200, FillWeight = 40 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Severity", HeaderText = "Severity", Width = 70, FillWeight = 10 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", Width = 50, FillWeight = 6 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "File", MinimumWidth = 100, FillWeight = 14 });
    }

    public void AddIssue(IssueModel issue)
    {
        int idx = Rows.Add(
            issue.IsFixable ? "🔧" : "",
            issue.LineNumber,
            issue.RuleId,
            issue.Category,
            issue.Description,
            issue.SeverityLabel,
            issue.IssueType,
            Path.GetFileName(issue.FilePath)
        );

        var row = Rows[idx];
        var sevCell = row.Cells["Severity"];
        sevCell.Style.Font = new Font("Segoe UI Semibold", 8f);
        sevCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        switch (issue.Severity)
        {
            case IssueSeverity.Critical or IssueSeverity.High:
                row.DefaultCellStyle.ForeColor = DarkTheme.TextSecondary;
                sevCell.Style.BackColor = Color.FromArgb(48, 22, 24);
                sevCell.Style.ForeColor = DarkTheme.AccentRedBright;
                row.Cells["LineNumber"].Style.ForeColor = DarkTheme.AccentRedBright;
                break;
            case IssueSeverity.Warning or IssueSeverity.Medium:
                row.DefaultCellStyle.ForeColor = DarkTheme.TextSecondary;
                sevCell.Style.BackColor = Color.FromArgb(46, 34, 16);
                sevCell.Style.ForeColor = DarkTheme.AccentOrange;
                row.Cells["LineNumber"].Style.ForeColor = DarkTheme.AccentOrange;
                break;
            default:
                row.DefaultCellStyle.ForeColor = DarkTheme.TextDim;
                sevCell.Style.BackColor = DarkTheme.BackgroundTertiary;
                sevCell.Style.ForeColor = DarkTheme.TextDim;
                break;
        }
    }

    public void LoadIssues(List<IssueModel> issues)
    {
        Rows.Clear();
        foreach (var issue in issues)
            AddIssue(issue);
    }

    public void ClearIssues() => Rows.Clear();
    public int IssueCount => Rows.Count;

    public List<IssueModel> GetSelectedIssues(List<IssueModel> allIssues)
    {
        var selected = new List<IssueModel>();
        foreach (DataGridViewRow row in SelectedRows)
        {
            if (row.Index >= 0 && row.Index < allIssues.Count)
                selected.Add(allIssues[row.Index]);
        }
        return selected;
    }

    private void OnCellMouseEnter(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= Rows.Count || _toolTip == null) return;
        var row = Rows[e.RowIndex];
        string desc = row.Cells["Description"].Value?.ToString() ?? "";
        string cat = row.Cells["Category"].Value?.ToString() ?? "";
        string sev = row.Cells["Severity"].Value?.ToString() ?? "";
        if (!string.IsNullOrEmpty(desc))
        {
            _toolTip.ToolTipTitle = $"{sev} — {cat}";
            _toolTip.Show(desc, this, Cursor.Position.X - Location.X - 10,
                Cursor.Position.Y - Location.Y - 20, 3000);
        }
    }

    private void OnCellMouseLeave(object? sender, DataGridViewCellEventArgs e)
    {
        _toolTip?.Hide(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CellMouseEnter -= OnCellMouseEnter;
            CellMouseLeave -= OnCellMouseLeave;
            _toolTip?.Dispose();
            _toolTip = null;
        }
        base.Dispose(disposing);
    }
}
