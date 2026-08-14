using GovernmentMiningApp.Models;
using GovernmentMiningApp.Services;
using GovernmentMiningApp.Ui;

namespace GovernmentMiningApp.Forms;

public sealed class LoginForm : Form
{
    private readonly DatabaseService _db;
    private readonly TextBox _badge = new();
    private readonly TextBox _password = new();
    private readonly Label _error = new();

    public LoginForm(DatabaseService db)
    {
        _db = db;
        Text = "ورود — برنامه قانونی و مجوزدار سفارشی دولت";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 380);
        BackColor = UiTheme.Surface;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = UiTheme.BodyFont;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = UiTheme.Primary
        };
        header.Controls.Add(new Label
        {
            Text = "برنامه قانونی و مجوزدار سفارشی دولت",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(40, 20, 40, 20) };

        var hint = new Label
        {
            Text = "شناسه پرسنلی (Badge) و رمز عبور را وارد کنید",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var badgeLbl = new Label { Text = "شناسه پرسنلی / Badge", Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight };
        _badge.Dock = DockStyle.Top;
        _badge.Height = 32;
        _badge.Text = "ADMIN-001";
        _badge.Margin = new Padding(0, 0, 0, 8);

        var passLbl = new Label { Text = "رمز عبور", Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight };
        _password.Dock = DockStyle.Top;
        _password.Height = 32;
        _password.UseSystemPasswordChar = true;
        _password.Text = "Admin@123";

        _error.Dock = DockStyle.Top;
        _error.Height = 28;
        _error.ForeColor = UiTheme.Danger;
        _error.TextAlign = ContentAlignment.MiddleCenter;

        var loginBtn = new Button { Text = "ورود به سامانه", Dock = DockStyle.Top, Height = 40 };
        UiTheme.StylePrimaryButton(loginBtn);
        loginBtn.Click += OnLogin;
        AcceptButton = loginBtn;

        var demo = new Label
        {
            Text = "پیش‌فرض: ADMIN-001 / Admin@123",
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Add in reverse for Dock Top stacking
        body.Controls.Add(demo);
        body.Controls.Add(loginBtn);
        body.Controls.Add(_error);
        body.Controls.Add(_password);
        body.Controls.Add(passLbl);
        body.Controls.Add(_badge);
        body.Controls.Add(badgeLbl);
        body.Controls.Add(hint);

        // Fix order with flow
        body.Controls.Clear();
        var flow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(10)
        };
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        flow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        flow.Controls.Add(hint, 0, 0);
        flow.Controls.Add(badgeLbl, 0, 1);
        flow.Controls.Add(_badge, 0, 2);
        flow.Controls.Add(passLbl, 0, 3);
        flow.Controls.Add(_password, 0, 4);
        flow.Controls.Add(_error, 0, 5);
        flow.Controls.Add(loginBtn, 0, 6);
        flow.Controls.Add(demo, 0, 7);
        _badge.Dock = DockStyle.Fill;
        _password.Dock = DockStyle.Fill;
        loginBtn.Dock = DockStyle.Fill;

        body.Controls.Add(flow);
        Controls.Add(body);
        Controls.Add(header);
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        var user = _db.Authenticate(_badge.Text, _password.Text);
        if (user == null)
        {
            _error.Text = "شناسه یا رمز عبور نادرست است.";
            return;
        }
        AppSession.CurrentUser = user;
        DialogResult = DialogResult.OK;
        Close();
    }
}
