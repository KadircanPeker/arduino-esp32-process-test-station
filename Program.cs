using System;
using System.Windows.Forms;
using ProcessTestApp.Data;

namespace ProcessTestApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                DatabaseSeeder.EnsureSchemas(null);
                var users = new UserRepository(new SqlConnectionFactory(null));
                if (!users.HasAnyAdmin())
                {
                    using (var firstRun = new FirstRunAdminForm(users))
                    {
                        if (firstRun.ShowDialog() != DialogResult.OK) return;
                    }
                }

                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() == DialogResult.OK)
                    {
                        System.Windows.Forms.Application.Run(new Form1());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uygulama başlatılamadı. SQL Server Express bağlantısını ve App.config ayarını kontrol edin.\n\n" + ex.Message,
                    "Başlatma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
