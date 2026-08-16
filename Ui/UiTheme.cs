using System.Drawing.Drawing2D;

namespace GovernmentMiningApp.Ui;

public static class UiTheme
{
    public static readonly Color Primary = CinematicTheme.Accent;
    public static readonly Color PrimaryDark = Color.FromArgb(8, 12, 22);
    public static readonly Color Accent = CinematicTheme.Accent;
    public static readonly Color Danger = CinematicTheme.Danger;
    public static readonly Color Warning = CinematicTheme.Warning;
    public static readonly Color Success = CinematicTheme.Success;
    public static readonly Color Surface = CinematicTheme.Background;
    public static readonly Color Card = CinematicTheme.SurfaceElevated;
    public static readonly Color Text = CinematicTheme.Text;
    public static readonly Color TextMuted = CinematicTheme.Muted;
    public static readonly Color Border = CinematicTheme.Border;

    public static Font TitleFont => CinematicTheme.TitleFont;
    public static Font HeaderFont => CinematicTheme.HeadingFont;
    public static Font BodyFont => CinematicTheme.BodyFont;

    public static void ApplyWindow(Form form)
    {
        form.BackColor = Surface;
        form.ForeColor = Text;
        form.Font = BodyFont;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
        form.DoubleBuffered(true);
    }

    public static void StylePrimaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(75, 185, 255);
        b.BackColor = Color.FromArgb(18, 74, 120);
        b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Height = 38;
        b.Padding = new Padding(12, 0, 12, 0);
        b.MouseEnter += (_, _) => b.BackColor = Color.FromArgb(25, 100, 158);
        b.MouseLeave += (_, _) => b.BackColor = Color.FromArgb(18, 74, 120);
    }

    public static void StyleSecondaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.BorderSize = 1;
        b.BackColor = Color.FromArgb(24, 32, 46);
        b.ForeColor = Text;
        b.Font = BodyFont;
        b.Cursor = Cursors.Hand;
        b.Height = 38;
        b.Padding = new Padding(12, 0, 12, 0);
        b.MouseEnter += (_, _) => b.BackColor = Color.FromArgb(34, 46, 64);
        b.MouseLeave += (_, _) => b.BackColor = Color.FromArgb(24, 32, 46);
    }

    public static void StyleDangerButton(Button b)
    {
        StylePrimaryButton(b);
        b.BackColor = Color.FromArgb(105, 31, 43);
        b.FlatAppearance.BorderColor = Color.FromArgb(220, 95, 105);
        b.MouseEnter += (_, _) => b.BackColor = Color.FromArgb(140, 40, 55);
        b.MouseLeave += (_, _) => b.BackColor = Color.FromArgb(105, 31, 43);
    }

    public static void StyleDataGrid(DataGridView g)
    {
        g.BackgroundColor = Surface;
        g.BorderStyle = BorderStyle.None;
        g.GridColor = Border;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        g.ColumnHeadersDefaultCellStyle.BackColor = Card;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        g.ColumnHeadersHeight = 42;
        g.RowTemplate.Height = 36;
        g.DefaultCellStyle.Font = BodyFont;
        g.DefaultCellStyle.BackColor = Surface;
        g.DefaultCellStyle.ForeColor = Text;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 105, 160);
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(18, 24, 36);
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.ReadOnly = true;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.MultiSelect = false;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.RowHeadersVisible = false;
        g.RightToLeft = RightToLeft.Yes;
    }

    public static Panel MakeCard(string title, int height = 100)
    {
        var p = CinematicTheme.CreateGlassPanel(12);
        p.Height = height;
        p.Margin = new Padding(8);
        p.Dock = DockStyle.Top;
        var lbl = new Label { Text = title, Dock = DockStyle.Top, Font = HeaderFont, ForeColor = Primary, Height = 28, TextAlign = ContentAlignment.MiddleRight };
        p.Controls.Add(lbl);
        return p;
    }

    public static void StyleTextBox(TextBox box)
    {
        box.BackColor = CinematicTheme.SurfaceElevated;
        box.ForeColor = Text;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = BodyFont;
        box.RightToLeft = RightToLeft.Yes;
    }

    public static void StyleComboBox(ComboBox box)
    {
        box.BackColor = CinematicTheme.SurfaceElevated;
        box.ForeColor = Text;
        box.FlatStyle = FlatStyle.Flat;
        box.Font = BodyFont;
        box.RightToLeft = RightToLeft.Yes;
    }

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        Height = 42,
        Font = TitleFont,
        ForeColor = Text,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 8, 0)
    };

    public static Panel MetricCard(string title, string value, Color accent)
    {
        var p = new Panel { Width = 190, Height = 112, Margin = new Padding(6), BackColor = Card };
        p.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 12);
            using var pen = new Pen(Color.FromArgb(55, 255, 255, 255));
            e.Graphics.DrawPath(pen, path);
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 12, 4, p.Height - 24);
        };
        p.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 23, FontStyle.Bold), ForeColor = accent, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 8, 0, 20) });
        p.Controls.Add(new Label { Text = title, Dock = DockStyle.Bottom, Height = 30, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleCenter });
        return p;
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); var d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }

    private static void DoubleBuffered(this Control control, bool enabled)
    {
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(control, enabled);
    }
}
