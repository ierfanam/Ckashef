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
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) ShowError(ex); };
        try
        {
            var db = new DatabaseService();
            db.Initialize();
            using var login = new LoginForm(db);
            UiTheme.ApplyWindow(login);
            if (login.ShowDialog() != DialogResult.OK) { db.Dispose(); return; }
            using var main = new MainForm(db);
            Application.Run(main);
            db.Dispose();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private static void ShowError(Exception ex) => MessageBox.Show($"خطا در اجرای سامانه:\n{ex.Message}", "Ckashef", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
