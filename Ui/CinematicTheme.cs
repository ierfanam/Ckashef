using System.Drawing.Drawing2D;

namespace GovernmentMiningApp.Ui;

/// <summary>
/// Centralized visual language for the enterprise command-center UI.
/// Keeps styling deterministic and reusable across WinForms screens.
/// </summary>
public static class CinematicTheme
{
    public static readonly Color Background = Color.FromArgb(8, 11, 18);
    public static readonly Color Surface = Color.FromArgb(15, 20, 30);
    public static readonly Color SurfaceElevated = Color.FromArgb(22, 29, 42);
    public static readonly Color Border = Color.FromArgb(43, 54, 72);
    public static readonly Color Text = Color.FromArgb(235, 240, 248);
    public static readonly Color Muted = Color.FromArgb(145, 158, 178);
    public static readonly Color Accent = Color.FromArgb(70, 166, 255);
    public static readonly Color AccentSoft = Color.FromArgb(28, 78, 122);
    public static readonly Color Success = Color.FromArgb(62, 201, 133);
    public static readonly Color Warning = Color.FromArgb(245, 184, 74);
    public static readonly Color Danger = Color.FromArgb(242, 92, 92);

    public static readonly Font TitleFont = new("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font HeadingFont = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.5f);
    public static readonly Font MetricFont = new("Segoe UI", 22f, FontStyle.Bold);

    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = BodyFont;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
    }

    public static Panel CreateGlassPanel(int radius = 12)
    {
        var panel = new RoundedPanel(radius)
        {
            BackColor = Surface,
            Padding = new Padding(16),
            Margin = new Padding(8)
        };
        return panel;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceElevated;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = HeadingFont;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = AccentSoft;
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 34;
    }

    private sealed class RoundedPanel : Panel
    {
        private readonly int _radius;
        public RoundedPanel(int radius) => _radius = radius;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var path = CreatePath(ClientRectangle, _radius);
            using var brush = new SolidBrush(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath CreatePath(Rectangle bounds, int radius)
        {
            var r = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
            var d = r * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
