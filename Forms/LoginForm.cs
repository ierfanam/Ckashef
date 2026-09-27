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
        Text = "ورود — سامانه ردیابی دستگاه‌های ماینر";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 340);
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
            Text = "سامانه ردیابی دستگاه‌های ماینر",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 16, 30, 16) };

        var flow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(10)
        };
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var hint = new Label
        {
            Text = "شناسه پرسنلی (Badge) و رمز عبور را وارد کنید",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var badgeLbl = new Label { Text = "شناسه پرسنلی / Badge", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _badge.Dock = DockStyle.Fill;
        var passLbl = new Label { Text = "رمز عبور", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _password.Dock = DockStyle.Fill;
        _password.UseSystemPasswordChar = true;
        _error.Dock = DockStyle.Fill;
        _error.ForeColor = UiTheme.Danger;
        _error.TextAlign = ContentAlignment.MiddleCenter;

        var loginBtn = new Button { Text = "ورود به سامانه", Dock = DockStyle.Fill };
        UiTheme.StylePrimaryButton(loginBtn);
        loginBtn.Click += OnLogin;
        AcceptButton = loginBtn;

        flow.Controls.Add(hint, 0, 0);
        flow.Controls.Add(badgeLbl, 0, 1);
        flow.Controls.Add(_badge, 0, 2);
        flow.Controls.Add(passLbl, 0, 3);
        flow.Controls.Add(_password, 0, 4);
        flow.Controls.Add(_error, 0, 5);
        flow.Controls.Add(loginBtn, 0, 6);

        body.Controls.Add(flow);
        Controls.Add(body);
        Controls.Add(header);
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        _error.Text = "";
        var user = _db.Authenticate(_badge.Text.Trim(), _password.Text);
        if (user == null)
        {
            _error.Text = "شناسه یا رمز عبور نادرست است.";
            _password.Clear();
            return;
        }

        if (user.MustChangePassword && !ForcePasswordChange(user))
        {
            _error.Text = "برای ادامه باید رمز عبور را تغییر دهید.";
            return;
        }

        AppSession.CurrentUser = user;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>تغییر اجباری رمز در اولین ورود یا پس از بازیابی حساب.</summary>
    private bool ForcePasswordChange(Personnel user)
    {
        while (true)
        {
            using var dlg = new Form
            {
                Text = "تغییر اجباری رمز عبور",
                ClientSize = new Size(440, 300),
                StartPosition = FormStartPosition.CenterParent,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.BodyFont,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };
            var current = new TextBox { Left = 140, Top = 20, Width = 260, UseSystemPasswordChar = true };
            var next = new TextBox { Left = 140, Top = 80, Width = 260, UseSystemPasswordChar = true };
            var again = new TextBox { Left = 140, Top = 140, Width = 260, UseSystemPasswordChar = true };
            dlg.Controls.Add(new Label { Text = "رمز فعلی", Left = 20, Top = 22, Width = 110, TextAlign = ContentAlignment.MiddleRight });
            dlg.Controls.Add(current);
            dlg.Controls.Add(new Label { Text = "رمز جدید", Left = 20, Top = 82, Width = 110, TextAlign = ContentAlignment.MiddleRight });
            dlg.Controls.Add(next);
            dlg.Controls.Add(new Label { Text = "تکرار رمز جدید", Left = 20, Top = 142, Width = 110, TextAlign = ContentAlignment.MiddleRight });
            dlg.Controls.Add(again);
            var ok = new Button { Text = "ثبت", Left = 20, Top = 200, Width = 120, DialogResult = DialogResult.OK };
            UiTheme.StylePrimaryButton(ok);
            dlg.Controls.Add(ok);
            dlg.AcceptButton = ok;
            var err = new Label
            {
                Text = "رمز جدید باید حداقل ۸ نویسه و شامل حرف و رقم باشد.",
                Left = 20,
                Top = 250,
                Width = 400,
                ForeColor = UiTheme.Danger
            };
            dlg.Controls.Add(err);

            if (dlg.ShowDialog(this) != DialogResult.OK) return false;

            var newPassword = next.Text;
            if (newPassword.Length < 8 || !newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
            {
                MessageBox.Show("رمز جدید باید حداقل ۸ نویسه و شامل حرف و رقم باشد.", "خطا");
                continue;
            }
            if (newPassword != again.Text)
            {
                MessageBox.Show("تکرار رمز مطابقت ندارد.", "خطا");
                continue;
            }
            if (!_db.ChangePassword(user.PersonnelID, current.Text, newPassword))
            {
                MessageBox.Show("رمز فعلی نادرست است.", "خطا");
                continue;
            }
            user.MustChangePassword = false;
            return true;
        }
    }
}
