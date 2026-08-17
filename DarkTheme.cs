using System.Drawing;
using System.Windows.Forms;

namespace BlackStarHydra360.UI;

/// <summary>
/// Dark theme color palette and control styling helpers.
/// Enhanced from PhantomAudit's DarkTheme with additional colors for the tabbed interface.
/// </summary>
public static class DarkTheme
{
    // Backgrounds
    public static readonly Color BackgroundPrimary = Color.FromArgb(8, 9, 14);
    public static readonly Color BackgroundSecondary = Color.FromArgb(18, 21, 31);
    public static readonly Color BackgroundTertiary = Color.FromArgb(28, 32, 46);
    public static readonly Color BackgroundElevated = Color.FromArgb(36, 41, 58);
    public static readonly Color BorderColor = Color.FromArgb(48, 54, 72);
    public static readonly Color BorderColorBright = Color.FromArgb(70, 78, 102);

    // Accents
    public static readonly Color AccentRed = Color.FromArgb(196, 52, 64);
    public static readonly Color AccentRedBright = Color.FromArgb(226, 74, 88);
    public static readonly Color AccentGreen = Color.FromArgb(56, 190, 108);
    public static readonly Color AccentBlue = Color.FromArgb(64, 148, 214);
    public static readonly Color AccentOrange = Color.FromArgb(212, 142, 48);
    public static readonly Color AccentViolet = Color.FromArgb(140, 108, 210);
    public static readonly Color AccentCyan = Color.FromArgb(80, 200, 220);

    // Text
    public static readonly Color TextPrimary = Color.FromArgb(226, 230, 240);
    public static readonly Color TextSecondary = Color.FromArgb(162, 172, 190);
    public static readonly Color TextDim = Color.FromArgb(96, 106, 128);

    // Glow
    public static readonly Color GlowRed = Color.FromArgb(90, 196, 52, 64);
    public static readonly Color GlowBlue = Color.FromArgb(90, 64, 148, 214);
    public static readonly Color GlowGreen = Color.FromArgb(90, 56, 190, 108);
    public static readonly Color GlowViolet = Color.FromArgb(70, 140, 108, 210);

    // Shared fonts (reused to prevent GDI handle leaks)
    private static readonly Font FormFont = new("Segoe UI", 9.0f);
    private static readonly Font TextBoxFont = new("Consolas", 9.0f);
    private static readonly Font ButtonFont = new("Segoe UI Semibold", 9.0f);
    private static readonly Font HeaderFont = new("Segoe UI", 16f, FontStyle.Bold);

    public static Font Header => HeaderFont;

    public static void ApplyToForm(Form form)
    {
        form.BackColor = BackgroundPrimary;
        form.ForeColor = TextPrimary;
        form.Font = FormFont;
    }

    public static void ApplyToButton(Button btn)
    {
        btn.BackColor = BackgroundElevated;
        btn.ForeColor = TextPrimary;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = BorderColorBright;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseDownBackColor = AccentRed;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 54, 76);
        btn.Font = ButtonFont;
        btn.Cursor = Cursors.Hand;
        btn.UseVisualStyleBackColor = false;
    }

    public static void ApplyToPrimaryButton(Button btn)
    {
        ApplyToButton(btn);
        btn.BackColor = AccentRed;
        btn.ForeColor = Color.FromArgb(245, 235, 236);
        btn.FlatAppearance.BorderColor = AccentRedBright;
        btn.FlatAppearance.MouseOverBackColor = AccentRedBright;
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 40, 50);
    }

    public static void ApplyToTextBox(TextBox tb)
    {
        tb.BackColor = BackgroundSecondary;
        tb.ForeColor = TextPrimary;
        tb.BorderStyle = BorderStyle.FixedSingle;
        tb.Font = TextBoxFont;
    }

    public static void ApplyToLabel(Label lbl)
    {
        lbl.ForeColor = TextSecondary;
        lbl.BackColor = Color.Transparent;
    }

    public static void ApplyToProgressBar(ProgressBar pb)
    {
        pb.BackColor = BackgroundSecondary;
        pb.ForeColor = AccentGreen;
        pb.Style = ProgressBarStyle.Continuous;
    }

    public static void ApplyToRichTextBox(RichTextBox rtb)
    {
        rtb.BackColor = Color.FromArgb(5, 6, 10);
        rtb.ForeColor = TextSecondary;
        rtb.Font = new Font("Consolas", 8.5f);
        rtb.BorderStyle = BorderStyle.None;
        rtb.ReadOnly = true;
    }

    public static void ApplyToComboBox(ComboBox cb)
    {
        cb.BackColor = BackgroundSecondary;
        cb.ForeColor = TextPrimary;
        cb.FlatStyle = FlatStyle.Flat;
        cb.Font = ButtonFont;
    }
}
