using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Npgsql;
using NpgsqlTypes;

namespace EdenPOS
{
    internal static class SupabaseSetup
    {
        private const string Project = "qnnfetkrqaaotkqsfivp";
        private const string Pooler = "aws-0-us-east-2.pooler.supabase.com";
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdenPOS");
        private static readonly string ConfigPath = Path.Combine(DirectoryPath, "supabase.protected");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EdenPOS-PostgreSQL-v1");

        internal static string ReadConnection()
        {
            if (!File.Exists(ConfigPath)) return null;
            var encrypted = File.ReadAllBytes(ConfigPath);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser));
        }

        internal static bool Show()
        {
            using (var dialog = new Form
            {
                Text = "Configurar Supabase en Edén POS",
                StartPosition = FormStartPosition.CenterScreen,
                AutoScaleMode = AutoScaleMode.Dpi,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ClientSize = new Size(560, 297),
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                dialog.Controls.Add(new Label
                {
                    Text = "Conecta este equipo al proyecto EdenPOS en Supabase. Usa la contraseña de PostgreSQL para crear un usuario exclusivo para esta caja.",
                    Location = new Point(18, 16), Size = new Size(520, 50)
                });
                dialog.Controls.Add(new Label { Text = "Contraseña de la base de datos", Location = new Point(18, 75), AutoSize = true });
                var password = new TextBox { Location = new Point(18, 98), Width = 515, UseSystemPasswordChar = true };
                dialog.Controls.Add(password);
                dialog.Controls.Add(new Label { Text = "Certificado CA de Supabase", Location = new Point(18, 135), AutoSize = true });
                var certificate = new TextBox
                {
                    Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "supabase-ca.crt"),
                    Location = new Point(18, 157), Width = 407, ReadOnly = true
                };
                var browse = new Button { Text = "Seleccionar…", Location = new Point(432, 155), Width = 102 };
                browse.Click += (sender, args) =>
                {
                    using (var file = new OpenFileDialog
                    {
                        Title = "Seleccione el certificado descargado de Database Settings",
                        Filter = "Certificado (*.crt;*.cer;*.pem)|*.crt;*.cer;*.pem|Todos los archivos|*.*"
                    })
                        if (file.ShowDialog(dialog) == DialogResult.OK) certificate.Text = file.FileName;
                };
                dialog.Controls.Add(certificate);
                dialog.Controls.Add(browse);
                var import = new CheckBox
                {
                    Text = "Cargar los datos locales de esta caja si la base está vacía",
                    Location = new Point(18, 200), AutoSize = true
                };
                dialog.Controls.Add(import);
                var connect = new Button { Text = "Conectar", Location = new Point(340, 251), Width = 92 };
                var cancel = new Button { Text = "Cancelar", Location = new Point(441, 251), Width = 92, DialogResult = DialogResult.Cancel };
                connect.Click += (sender, args) =>
                {
                    if (password.TextLength == 0 || !File.Exists(certificate.Text))
                    {
                        MessageBox.Show(dialog, "Ingrese la contraseña PostgreSQL y seleccione el certificado CA.", "Datos requeridos",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try
                    {
                        dialog.UseWaitCursor = true;
                        connect.Enabled = false;
                        Configure(password.Text, certificate.Text, import.Checked);
                        password.Clear();
                        dialog.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        AppData.LogError(ex, "Configurar Supabase");
                        MessageBox.Show(dialog, ex.Message, "No se pudo conectar a Supabase",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        connect.Enabled = true;
                        dialog.UseWaitCursor = false;
                    }
                };
                dialog.Controls.Add(connect);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = connect;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog() == DialogResult.OK;
            }
        }

        private static void Configure(string adminPassword, string certificate, bool import)
        {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(ConfigPath))
                throw new InvalidOperationException("Esta cuenta de Windows ya tiene Supabase configurado.");

            var login = "edenpos_pc_" + Guid.NewGuid().ToString("N");
            var installedCertificate = Path.Combine(DirectoryPath, login + ".crt");
            File.Copy(certificate, installedCertificate);
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var terminalPassword = Convert.ToBase64String(bytes);
            Array.Clear(bytes, 0, bytes.Length);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = Pooler,
                Port = 5432,
                Database = "postgres",
                Username = "postgres." + Project,
                Password = adminPassword,
                RootCertificate = installedCertificate,
                SslMode = SslMode.VerifyFull,
                Timeout = 8,
                CommandTimeout = 15,
                Pooling = false
            };

            var ready = false;
            try
            {
                using (var source = SupabaseStore.CreateDataSource(builder))
                using (var admin = source.CreateConnection())
                {
                    admin.Open();
                    ProvisionTerminal(admin, login, terminalPassword);
                    try
                    {
                        builder.Username = login + "." + Project;
                        builder.Password = terminalPassword;
                        builder.Pooling = true;
                        builder.MaxPoolSize = 4;
                        var connection = builder.ConnectionString;
                        var protectedConnection = ProtectedData.Protect(
                            Encoding.UTF8.GetBytes(connection), Entropy, DataProtectionScope.CurrentUser);
                        AppData.InitializeSupabase(connection, import);
                        var pending = Path.Combine(DirectoryPath, login + ".pending");
                        try
                        {
                            File.WriteAllBytes(pending, protectedConnection);
                            File.Move(pending, ConfigPath);
                        }
                        finally
                        {
                            if (File.Exists(pending)) File.Delete(pending);
                        }
                        ready = true;
                    }
                    catch
                    {
                        NpgsqlConnection.ClearAllPools();
                        try
                        {
                            using (var cleanup = new NpgsqlCommand("drop role " + login, admin))
                                cleanup.ExecuteNonQuery();
                        }
                        catch (Exception cleanupError) { AppData.LogError(cleanupError, "Eliminar usuario de caja sin configurar"); }
                        throw;
                    }
                }
            }
            finally
            {
                if (!ready) File.Delete(installedCertificate);
            }
        }

        private static void ProvisionTerminal(NpgsqlConnection admin, string login, string password)
        {
            using (var transaction = admin.BeginTransaction())
            {
                using (var init = new NpgsqlCommand(@"
select pg_catalog.pg_advisory_xact_lock(1346720561, 1);
do $$
begin
    if not exists (select 1 from pg_catalog.pg_roles where rolname = 'edenpos_desktop') then
        create role edenpos_desktop nologin;
    end if;
end $$;
grant usage on schema edenpos to edenpos_desktop;
do $$
declare
    table_name text;
begin
    foreach table_name in array array['settings', 'app_users', 'products', 'product_options', 'customers',
        'shifts', 'cash_movements', 'receipts', 'receipt_lines', 'saved_tickets', 'saved_ticket_lines',
        'saved_ticket_lockers', 'inventory_movements']
    loop
        execute format('grant select, insert, update, delete on edenpos.%I to edenpos_desktop', table_name);
        if not exists (select 1 from pg_catalog.pg_policies where schemaname = 'edenpos'
            and tablename = table_name and policyname = 'desktop_access') then
            execute format('create policy desktop_access on edenpos.%I to edenpos_desktop using (true) with check (true)', table_name);
        end if;
    end loop;
end $$;", admin, transaction))
                    init.ExecuteNonQuery();
                using (var parameters = new NpgsqlCommand(
                    "select set_config('edenpos.setup_login', @login, true), set_config('edenpos.setup_password', @password, true)",
                    admin, transaction))
                {
                    parameters.Parameters.AddWithValue("login", NpgsqlDbType.Text, login);
                    parameters.Parameters.AddWithValue("password", NpgsqlDbType.Text, password);
                    parameters.ExecuteNonQuery();
                }
                using (var createLogin = new NpgsqlCommand(@"
do $$
begin
    execute format('create role %I login inherit password %L in role edenpos_desktop',
        current_setting('edenpos.setup_login'), current_setting('edenpos.setup_password'));
end $$;", admin, transaction))
                    createLogin.ExecuteNonQuery();
                transaction.Commit();
            }
        }
    }
}
