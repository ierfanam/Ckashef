using GovernmentMiningApp.Forms;
using GovernmentMiningApp.Services;

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
            db.Initialize();

            using var login = new LoginForm(db);
            if (login.ShowDialog() != DialogResult.OK)
                return;

            Application.Run(new MainForm(db));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "خطا در راه‌اندازی برنامه:\n" + ex.Message,
                "برنامه قانونی و مجوزدار سفارشی دولت",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
