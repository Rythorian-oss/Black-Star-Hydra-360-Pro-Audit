using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BlackStarHydra360.UI
{
    /// <summary>
    /// Panel with a soft glow border effect. Enhanced from PhantomAudit.
    /// </summary>
    public class GlowPanel : Panel
    {
        private Color _glowColor = DarkTheme.GlowBlue;
        private int _borderWidth = 1;
        private int _cornerRadius = 8;

        public GlowPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = DarkTheme.BackgroundSecondary;
        }

        public Color GlowColor
        {
            get => _glowColor;
            set { _glowColor = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = Math.Max(0, value); Invalidate(); }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(bounds); return path; }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var bgBrush = new SolidBrush(BackColor))
                    g.FillRectangle(bgBrush, ClientRectangle);

                Rectangle rect = ClientRectangle;
                rect.Inflate(-2, -2);

                using (var path = RoundedRect(rect, _cornerRadius))
                {
                    // Soft fill
                    using (var fillBrush = new LinearGradientBrush(
                        rect, Color.FromArgb(6, 255, 255, 255), Color.FromArgb(0, 0, 0, 0),
                        LinearGradientMode.Vertical))
                    {
                        g.FillPath(fillBrush, path);
                    }

                    // Glow border
                    using (var pen = new Pen(_glowColor, _borderWidth))
                        g.DrawPath(pen, path);

                    // Inner hairline
                    var innerRect = rect;
                    innerRect.Inflate(-1, -1);
                    using (var innerPath = RoundedRect(innerRect, Math.Max(0, _cornerRadius - 1)))
                    using (var innerPen = new Pen(Color.FromArgb(18, 255, 255, 255), 1))
                        g.DrawPath(innerPen, innerPath);

                    // Outer glow rings
                    for (int i = 1; i <= 8; i++)
                    {
                        float alpha = 255f * (1f - i / 8f) * 0.15f;
                        int a = (int)alpha;
                        var fadeColor = Color.FromArgb(a, _glowColor);
                        using var glowPen = new Pen(fadeColor, 1);
                        var glowRect = rect;
                        glowRect.Inflate(i, i);
                        using var glowPath = RoundedRect(glowRect, _cornerRadius + i);
                        g.DrawPath(glowPen, glowPath);
                    }
                }
            }
            catch { /* Prevent paint crashes */ }
        }
    }
}
