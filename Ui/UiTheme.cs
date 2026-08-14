namespace GovernmentMiningApp.Ui;

public static class UiTheme
{
    // Shared palette: the existing application can adopt the cinematic language
    // without requiring every form to be rewritten at once.
    public static readonly Color Primary = CinematicTheme.Accent;
    public static readonly Color PrimaryDark = Color.FromArgb(10, 16, 27);
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

    public static void StylePrimaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(85, 180, 255);
        b.BackColor = Color.FromArgb(25, 83, 132);
        b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Height = 36;
        b.Padding = new Padding(10, 0, 10, 0);
    }

    public static void StyleSecondaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.BorderSize = 1;
        b.BackColor = Card;
        b.ForeColor = Text;
        b.Font = BodyFont;
        b.Cursor = Cursors.Hand;
        b.Height = 36;
        b.Padding = new Padding(10, 0, 10, 0);
    }

    public static void StyleDangerButton(Button b)
    {
        StylePrimaryButton(b);
        b.BackColor = Color.FromArgb(105, 31, 43);
        b.FlatAppearance.BorderColor = Color.FromArgb(220, 95, 105);
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
        g.ColumnHeadersHeight = 40;
        g.RowTemplate.Height = 34;
        g.DefaultCellStyle.Font = BodyFont;
        g.DefaultCellStyle.BackColor = CinematicTheme.Surface;
        g.DefaultCellStyle.ForeColor = Text;
        g.DefaultCellStyle.SelectionBackColor = CinematicTheme.AccentSoft;
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

        var lbl = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Font = HeaderFont,
            ForeColor = Primary,
            Height = 28,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight
        };
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
}
