using GovernmentMiningApp.Services;
using GovernmentMiningApp.Ui;

namespace GovernmentMiningApp.Forms;

public sealed class LoginForm : Form
{
    private readonly DatabaseService _db;
    private readonly TextBox _badge = new();
    private readonly TextBox _password = new();
    private readonly Label _error = new();
    private readonly Button _login = new();

    public LoginForm(DatabaseService db)
    {
        _db = db;
        Text = "ورود امن | Ckashef";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(720, 500);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        CinematicTheme.Apply(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = CinematicTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var identity = CinematicTheme.CreateGlassPanel(18);
        identity.Dock = DockStyle.Fill;
        identity.Margin = new Padding(0, 0, 10, 0);

        var brand = new Label
        {
            Text = "CKASHEF",
            Dock = DockStyle.Top,
            Height = 58,
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = CinematicTheme.Accent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var subtitle = new Label
        {
            Text = "مرکز فرمان و مدیریت عملیات",
            Dock = DockStyle.Top,
            Height = 34,
            Font = CinematicTheme.HeadingFont,
            ForeColor = CinematicTheme.Text,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var status = new Label
        {
            Text = "● سامانه آماده اتصال",
            Dock = DockStyle.Bottom,
            Height = 34,
            ForeColor = CinematicTheme.Success,
            TextAlign = ContentAlignment.MiddleCenter
        };
        identity.Controls.Add(status);
        identity.Controls.Add(subtitle);
        identity.Controls.Add(brand);

        var card = CinematicTheme.CreateGlassPanel(18);
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(10, 0, 0, 0);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(28),
            BackColor = Color.Transparent
        };
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "ورود به سامانه",
            Dock = DockStyle.Fill,
            Font = CinematicTheme.TitleFont,
            ForeColor = CinematicTheme.Text,
            TextAlign = ContentAlignment.MiddleRight
        };
        form.Controls.Add(title, 0, 0);
        form.Controls.Add(FieldLabel("شناسه پرسنلی / Badge"), 0, 1);
        ConfigureTextBox(_badge, false);
        form.Controls.Add(_badge, 0, 2);
        form.Controls.Add(FieldLabel("رمز عبور"), 0, 3);
        ConfigureTextBox(_password, true);
        form.Controls.Add(_password, 0, 4);

        _error.Dock = DockStyle.Fill;
        _error.ForeColor = CinematicTheme.Danger;
        _error.TextAlign = ContentAlignment.MiddleRight;
        form.Controls.Add(_error, 0, 5);

        _login.Text = "ورود امن";
        _login.Dock = DockStyle.Fill;
        _login.FlatStyle = FlatStyle.Flat;
        _login.FlatAppearance.BorderSize = 0;
        _login.BackColor = CinematicTheme.Accent;
        _login.ForeColor = Color.White;
        _login.Font = CinematicTheme.HeadingFont;
        _login.Cursor = Cursors.Hand;
        _login.Click += OnLogin;
        form.Controls.Add(_login, 0, 6);

        var note = new Label
        {
            Text = "داده‌های احراز هویت در رابط کاربری ذخیره یا نمایش داده نمی‌شوند.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = CinematicTheme.Muted,
            TextAlign = ContentAlignment.MiddleCenter
        };
        form.Controls.Add(note, 0, 7);

        card.Controls.Add(form);
        root.Controls.Add(identity, 0, 0);
        root.Controls.Add(card, 1, 0);
        Controls.Add(root);

        AcceptButton = _login;
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = CinematicTheme.Muted,
        TextAlign = ContentAlignment.MiddleRight
    };

    private static void ConfigureTextBox(TextBox box, bool password)
    {
        box.Dock = DockStyle.Fill;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = CinematicTheme.SurfaceElevated;
        box.ForeColor = CinematicTheme.Text;
        box.Font = CinematicTheme.BodyFont;
        box.RightToLeft = RightToLeft.Yes;
        box.Margin = new Padding(0, 2, 0, 2);
        box.UseSystemPasswordChar = password;
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        _error.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(_badge.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            _error.Text = "شناسه پرسنلی و رمز عبور الزامی است.";
            return;
        }

        try
        {
            var user = _db.Authenticate(_badge.Text.Trim(), _password.Text);
            if (user == null)
            {
                _error.Text = "شناسه یا رمز عبور نادرست است.";
                _password.Clear();
                _password.Focus();
                return;
            }

            AppSession.CurrentUser = user;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _error.Text = "خطا در احراز هویت: " + ex.Message;
        }
    }
}
