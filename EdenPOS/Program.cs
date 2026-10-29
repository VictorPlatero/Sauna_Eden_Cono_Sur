using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace EdenPOS
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var culture = new CultureInfo("es-PE");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                AppData.Initialize();
                Application.ThreadException += (s, e) =>
                {
                    AppData.LogError(e.Exception, "Error no controlado de interfaz");
                    MessageBox.Show("Ocurrió un error inesperado. El detalle se guardó en errors.log.", "Edén POS", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => AppData.LogError(e.ExceptionObject as Exception, "Error no controlado de aplicación");
                while (true)
                {
                    using (var login = new LoginForm())
                    {
                        if (login.ShowDialog() != DialogResult.OK) break;
                    }

                    bool returnToLogin;
                    using (var main = new MainForm())
                    {
                        Application.Run(main);
                        returnToLogin = main.LogoutRequested;
                    }
                    AppSession.SignOut();
                    if (!returnToLogin) break;
                }
            }
            catch (Exception ex)
            {
                AppData.LogError(ex, "Error de inicio");
                MessageBox.Show(
                    "No se pudo iniciar Edén POS.\n\n" + ex.Message,
                    "Error de inicio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
