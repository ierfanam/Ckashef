namespace GovernmentMiningApp.Ui;

public static class UiTheme
{
    public static readonly Color Primary = Color.FromArgb(25, 55, 95);
    public static readonly Color PrimaryDark = Color.FromArgb(15, 35, 65);
    public static readonly Color Accent = Color.FromArgb(0, 120, 100);
    public static readonly Color Danger = Color.FromArgb(180, 50, 50);
    public static readonly Color Warning = Color.FromArgb(200, 130, 20);
    public static readonly Color Surface = Color.FromArgb(245, 247, 250);
    public static readonly Color Card = Color.White;
    public static readonly Color TextMuted = Color.FromArgb(90, 100, 110);

    public static Font TitleFont => new("Segoe UI", 14f, FontStyle.Bold);
    public static Font HeaderFont => new("Segoe UI", 11f, FontStyle.Bold);
    public static Font BodyFont => new("Segoe UI", 9.5f);

    public static void StylePrimaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = Accent;
        b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.Height = 36;
        b.Padding = new Padding(8, 0, 8, 0);
    }

    public static void StyleSecondaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Primary;
        b.FlatAppearance.BorderSize = 1;
        b.BackColor = Color.White;
        b.ForeColor = Primary;
        b.Font = new Font("Segoe UI", 9.5f);
        b.Cursor = Cursors.Hand;
        b.Height = 36;
    }

    public static void StyleDangerButton(Button b)
    {
        StylePrimaryButton(b);
        b.BackColor = Danger;
    }

    public static void StyleDataGrid(DataGridView g)
    {
        g.BackgroundColor = Color.White;
        g.BorderStyle = BorderStyle.None;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Primary;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        g.ColumnHeadersHeight = 36;
        g.RowTemplate.Height = 30;
        g.DefaultCellStyle.Font = BodyFont;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 245);
        g.DefaultCellStyle.SelectionForeColor = Color.Black;
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
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
        var p = new Panel
        {
            BackColor = Card,
            Height = height,
            Margin = new Padding(8),
            Padding = new Padding(12),
            Dock = DockStyle.Top
        };
        p.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(220, 225, 230));
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        var lbl = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Font = HeaderFont,
            ForeColor = Primary,
            Height = 24,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight
        };
        p.Controls.Add(lbl);
        return p;
    }
}
