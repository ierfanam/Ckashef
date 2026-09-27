using GovernmentMiningApp.Forms;
using GovernmentMiningApp.Services;
using GovernmentMiningApp.Ui;

namespace GovernmentMiningApp;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "خطای برنامه", MessageBoxButtons.OK, MessageBoxIcon.Error);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                MessageBox.Show(ex.Message, "خطای بحرانی", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
            var db = new DatabaseService();
            var firstRun = db.EnsureAdminAccount();
            if (firstRun.Created)
                ShowFirstRunCredentials(firstRun);

            using var login = new LoginForm(db);
            if (login.ShowDialog() != DialogResult.OK)
                return;

            Application.Run(new MainForm(db));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "خطا در راه‌اندازی برنامه:\n" + ex.Message,
                "سامانه ردیابی دستگاه‌های ماینر",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// در اولین اجرا حساب مدیر با رمز تصادفی ساخته می‌شود؛ رمز تنها یک‌بار
    /// همین‌جا نمایش داده می‌شود و سامانه هیچ رمز پیش‌فرض ثابتی ندارد.
    /// </summary>
    private static void ShowFirstRunCredentials(FirstRunInfo info)
    {
        using var dlg = new Form
        {
            Text = "راه‌اندازی اولیه",
            ClientSize = new Size(520, 300),
            StartPosition = FormStartPosition.CenterScreen,
            RightToLeft = RightToLeft.Yes,
            Font = UiTheme.BodyFont,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Consolas", 11f),
            Text =
                "حساب مدیر ساخته شد.\r\n\r\n" +
                $"شناسه پرسنلی : {info.Badge}\r\n" +
                $"رمز عبور    : {info.Password}\r\n\r\n" +
                "این رمز تنها یک‌بار نمایش داده می‌شود.\r\n" +
                "پس از ورود، سامانه تغییر رمز را الزامی می‌کند.\r\n" +
                "لطفاً آن را در جای امن یادداشت کنید."
        };
        var ok = new Button { Text = "متوجه شدم", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK };
        UiTheme.StylePrimaryButton(ok);
        dlg.Controls.Add(box);
        dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;
        dlg.ShowDialog();
    }
}
