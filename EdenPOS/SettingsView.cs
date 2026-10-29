using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class SettingsView : UserControl, IRefreshable
    {
        private readonly TextBox store;
        private readonly TextBox employee;
        private readonly TextBox currency;
        private readonly Label dataPath;
        private readonly Label storageMode;
        private readonly Button portableButton;
        private readonly CheckBox dniEnabled;
        private readonly TextBox dniToken;
        private readonly Label dniStatus;
        private readonly UserManagementPanel usersPanel;

        public SettingsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Canvas;
            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Green, Padding = new Padding(22, 12, 18, 12) };
            top.Controls.Add(new Label
            {
                Text = "Configuración",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            Controls.Add(top);

            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(850, 1660), Padding = new Padding(38), BackColor = Theme.Canvas };
            Controls.Add(host);
            host.BringToFront();
            var card = new CardPanel { Location = new Point(38, 34), Size = new Size(760, 620), BackColor = Color.White, Padding = new Padding(32) };
            host.Controls.Add(card);
            var title = Theme.Label("Datos del negocio", 17f, FontStyle.Bold); title.Location = new Point(32, 28); card.Controls.Add(title);
            var subtitle = Theme.Label("Estos datos aparecen en la barra lateral y en los recibos.", 9.5f, FontStyle.Regular, Theme.Muted); subtitle.Location = new Point(32, 64); card.Controls.Add(subtitle);

            AddFieldLabel(card, "Nombre del negocio", 112);
            store = new TextBox { Location = new Point(32, 140), Size = new Size(696, 35), Font = Theme.Body };
            card.Controls.Add(store);
            AddFieldLabel(card, "Usuario con sesión abierta", 194);
            employee = new TextBox { Location = new Point(32, 222), Size = new Size(440, 35), Font = Theme.Body, ReadOnly = true, BackColor = Color.FromArgb(247, 249, 250) };
            card.Controls.Add(employee);
            AddFieldLabel(card, "Símbolo de moneda", 276);
            currency = new TextBox { Location = new Point(32, 304), Size = new Size(150, 35), Font = Theme.Body };
            card.Controls.Add(currency);

            var save = Theme.Button("GUARDAR CAMBIOS", true); save.SetBounds(512, 292, 216, 54); save.Click += Save_Click; card.Controls.Add(save);
            var businessSeparator = new Panel { Location = new Point(32, 374), Size = new Size(696, 1), BackColor = Theme.Border };
            card.Controls.Add(businessSeparator);
            var dataTitle = Theme.Label("Archivos locales", 11f, FontStyle.Bold); dataTitle.Location = new Point(32, 394); card.Controls.Add(dataTitle);
            dataPath = Theme.Label(AppData.DataDirectory, 9f, FontStyle.Regular, Theme.Muted); dataPath.SetBounds(32, 424, 500, 48); dataPath.AutoSize = false; card.Controls.Add(dataPath);
            var open = Theme.Button("ABRIR CARPETA"); open.SetBounds(548, 414, 180, 48); open.Click += (s, e) => AppData.OpenDataFolder(); card.Controls.Add(open);

            var export = Theme.Button("EXPORTAR COPIA"); export.SetBounds(32, 478, 216, 50); export.Click += ExportBackup_Click; card.Controls.Add(export);
            var import = Theme.Button("IMPORTAR COPIA"); import.SetBounds(256, 478, 216, 50); import.Click += ImportBackup_Click; card.Controls.Add(import);
            portableButton = Theme.Button("ACTIVAR PORTABLE"); portableButton.SetBounds(480, 478, 248, 50); portableButton.Click += TogglePortable_Click; card.Controls.Add(portableButton);
            storageMode = Theme.Label("", 9.2f, FontStyle.Regular, Theme.Muted); storageMode.SetBounds(32, 544, 696, 44); storageMode.AutoSize = false; card.Controls.Add(storageMode);

            usersPanel = new UserManagementPanel { Location = new Point(38, 676), Size = new Size(760, 510), BackColor = Color.White };
            host.Controls.Add(usersPanel);

            var dniCard = new CardPanel { Location = new Point(38, 1208), Size = new Size(760, 390), BackColor = Color.White, Padding = new Padding(32) };
            host.Controls.Add(dniCard);
            var dniTitle = Theme.Label("Consulta de clientes por DNI", 17f, FontStyle.Bold); dniTitle.Location = new Point(32, 26); dniCard.Controls.Add(dniTitle);
            var dniSubtitle = Theme.Label("Proveedor opcional: ApiPeruDev, basado en padrón reducido SUNAT y otras fuentes públicas.", 9.3f, FontStyle.Regular, Theme.Muted);
            dniSubtitle.SetBounds(32, 62, 680, 24); dniSubtitle.AutoSize = false; dniCard.Controls.Add(dniSubtitle);
            dniEnabled = new CheckBox
            {
                Text = "Activar consulta automática al completar 8 dígitos",
                Location = new Point(32, 100),
                Size = new Size(470, 30),
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                BackColor = Color.White
            };
            dniCard.Controls.Add(dniEnabled);
            var tokenLabel = Theme.Label("Token privado de ApiPeruDev", 9.5f, FontStyle.Bold); tokenLabel.Location = new Point(32, 145); dniCard.Controls.Add(tokenLabel);
            dniToken = new TextBox { Location = new Point(32, 174), Size = new Size(528, 35), Font = Theme.Body, UseSystemPasswordChar = true, MaxLength = 500 };
            dniCard.Controls.Add(dniToken);
            var showToken = Theme.Button("MOSTRAR"); showToken.SetBounds(572, 169, 156, 44);
            showToken.MouseDown += (s, e) => dniToken.UseSystemPasswordChar = false;
            showToken.MouseUp += (s, e) => dniToken.UseSystemPasswordChar = true;
            showToken.MouseLeave += (s, e) => dniToken.UseSystemPasswordChar = true;
            dniCard.Controls.Add(showToken);
            dniStatus = Theme.Label("", 9f, FontStyle.Regular, Theme.Muted); dniStatus.SetBounds(32, 222, 696, 24); dniStatus.AutoSize = false; dniCard.Controls.Add(dniStatus);
            var privacy = Theme.Label("El token se cifra para el usuario actual de Windows. El nombre obtenido debe verificarse con el DNI físico del cliente.", 9f, FontStyle.Regular, Theme.Muted);
            privacy.SetBounds(32, 258, 696, 48); privacy.AutoSize = false; dniCard.Controls.Add(privacy);
            var saveDni = Theme.Button("GUARDAR CONFIGURACIÓN", true); saveDni.SetBounds(486, 320, 242, 50); saveDni.Click += Save_Click; dniCard.Controls.Add(saveDni);
            var clearCustomers = Theme.Button("BORRAR CLIENTES LOCALES"); clearCustomers.SetBounds(32, 320, 230, 50);
            clearCustomers.Click += (s, e) =>
            {
                if (AppData.Customers.Count == 0) { MessageBox.Show("No hay clientes guardados localmente.", "Clientes", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                if (MessageBox.Show("Se eliminarán los DNI y nombres guardados localmente. Los recibos existentes no se modificarán. ¿Continuar?", "Privacidad de clientes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                AppData.ClearCustomers();
                RefreshView();
            };
            dniCard.Controls.Add(clearCustomers);

            Action layoutBusinessCard = () =>
            {
                var innerWidth = Math.Max(400, card.ClientSize.Width - 64);
                store.Width = innerWidth;
                employee.Width = Math.Min(innerWidth, 520);
                save.Left = card.ClientSize.Width - save.Width - 32;
                businessSeparator.Width = innerWidth;
                open.Left = card.ClientSize.Width - open.Width - 32;
                dataPath.Width = Math.Max(180, open.Left - dataPath.Left - 16);
                var gap = 8;
                var actionWidth = Math.Max(145, (innerWidth - gap * 2) / 3);
                export.SetBounds(32, 478, actionWidth, 50);
                import.SetBounds(export.Right + gap, 478, actionWidth, 50);
                portableButton.SetBounds(import.Right + gap, 478, Math.Max(145, card.ClientSize.Width - import.Right - gap - 32), 50);
                storageMode.Width = innerWidth;
            };
            card.SizeChanged += (s, e) => layoutBusinessCard();

            Action layoutDniCard = () =>
            {
                var innerWidth = Math.Max(400, dniCard.ClientSize.Width - 64);
                dniSubtitle.Width = innerWidth;
                dniEnabled.Width = innerWidth;
                showToken.Left = dniCard.ClientSize.Width - showToken.Width - 32;
                dniToken.Width = Math.Max(180, showToken.Left - dniToken.Left - 12);
                dniStatus.Width = innerWidth;
                privacy.Width = innerWidth;
                saveDni.Left = dniCard.ClientSize.Width - saveDni.Width - 32;
            };
            dniCard.SizeChanged += (s, e) => layoutDniCard();

            Action layoutCards = () =>
            {
                var scrollbar = SystemInformation.VerticalScrollBarWidth;
                var available = Math.Max(560, host.ClientSize.Width - scrollbar - 40);
                var cardWidth = Math.Max(560, Math.Min(1100, available));
                var left = Math.Max(20, (host.ClientSize.Width - scrollbar - cardWidth) / 2);
                card.SetBounds(left, 34, cardWidth, 620);
                usersPanel.SetBounds(left, 676, cardWidth, 510);
                dniCard.SetBounds(left, 1208, cardWidth, 390);
                host.AutoScrollMinSize = new Size(cardWidth + left + 20, 1640);
                layoutBusinessCard();
                layoutDniCard();
            };
            host.SizeChanged += (s, e) => layoutCards();
            layoutCards();
            RefreshView();
        }

        public void RefreshView()
        {
            var settings = AppData.Settings;
            store.Text = settings.StoreName;
            employee.Text = AppSession.UserName + " — " + AppSession.RoleName;
            currency.Text = settings.CurrencySymbol;
            dataPath.Text = AppData.DataDirectory;
            portableButton.Text = AppData.IsPortableMode ? "DESACTIVAR PORTABLE" : "ACTIVAR PORTABLE";
            storageMode.Text = AppData.IsPortableMode
                ? "Modo portable activo: datos e imágenes viajan junto al ejecutable. El token DNI debe configurarse nuevamente en otra cuenta de Windows."
                : "Modo local: datos protegidos en el perfil de Windows. Use Exportar/Importar para trasladarlos a otra computadora.";
            dniEnabled.Checked = settings.DniLookupEnabled;
            usersPanel.RefreshUsers();
            var environmentToken = Environment.GetEnvironmentVariable("EDENPOS_DNI_TOKEN");
            if (!string.IsNullOrWhiteSpace(environmentToken))
            {
                dniToken.Text = "";
                dniToken.ReadOnly = true;
                dniStatus.Text = "Token suministrado mediante EDENPOS_DNI_TOKEN • " + AppData.Customers.Count + " clientes locales";
            }
            else
            {
                dniToken.ReadOnly = false;
                dniToken.Text = AppData.UnprotectSecret(settings.DniApiTokenProtected);
                dniStatus.Text = (dniToken.TextLength > 0 ? "Token configurado" : "Token pendiente") + " • " + AppData.Customers.Count + " clientes locales";
            }
        }

        private void AddFieldLabel(Control parent, string text, int top)
        {
            var label = Theme.Label(text, 9.5f, FontStyle.Bold);
            label.Location = new Point(32, top);
            parent.Controls.Add(label);
        }

        private void ExportBackup_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "Copia de Edén POS (*.edenbackup)|*.edenbackup",
                FileName = "EdenPOS-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".edenbackup",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    AppData.ExportBackup(dialog.FileName);
                    MessageBox.Show("Copia creada correctamente. Por seguridad, el token DNI no fue incluido.", "Copia de seguridad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppData.LogError(ex, "Exportar copia de seguridad");
                    MessageBox.Show(ex.Message, "No se pudo exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportBackup_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Copia de Edén POS (*.edenbackup)|*.edenbackup|Todos los archivos (*.*)|*.*" })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (MessageBox.Show("Los datos de la copia reemplazarán los registros locales que tengan el mismo nombre. ¿Continuar?", "Importar copia", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    AppData.ImportBackup(dialog.FileName);
                    RefreshView();
                    MessageBox.Show("Copia importada correctamente. Configure el token DNI si esta es una computadora nueva.", "Importación completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppData.LogError(ex, "Importar copia de seguridad");
                    MessageBox.Show(ex.Message, "No se pudo importar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TogglePortable_Click(object sender, EventArgs e)
        {
            var enable = !AppData.IsPortableMode;
            var message = enable
                ? "Se copiarán los datos junto al ejecutable. Use esta opción solamente si la carpeta del programa es escribible. ¿Activar modo portable?"
                : "Los datos se copiarán al perfil local de Windows. ¿Desactivar modo portable?";
            if (MessageBox.Show(message, "Modo portable", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                AppData.SetPortableMode(enable);
                RefreshView();
                MessageBox.Show("Modo de almacenamiento actualizado. Reinicie Edén POS antes de continuar trabajando.", "Edén POS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppData.LogError(ex, "Cambiar modo portable");
                MessageBox.Show(ex.Message, "Modo portable", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if (!Validation.IsValidRequiredText(store.Text, 3, 80))
            {
                MessageBox.Show("El nombre del negocio debe tener entre 3 y 80 caracteres.", "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                store.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(currency.Text) || currency.Text.Length > 5 || currency.Text.Any(char.IsControl))
            {
                MessageBox.Show("El símbolo de moneda debe tener entre 1 y 5 caracteres.", "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                currency.Focus();
                return;
            }
            var environmentToken = Environment.GetEnvironmentVariable("EDENPOS_DNI_TOKEN");
            if (dniEnabled.Checked && string.IsNullOrWhiteSpace(environmentToken) && string.IsNullOrWhiteSpace(dniToken.Text))
            {
                MessageBox.Show("Para activar la consulta de DNI debe ingresar un token de ApiPeruDev.", "Configuración de DNI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dniToken.Focus();
                return;
            }
            var settings = AppData.Settings;
            settings.StoreName = store.Text.Trim().ToUpperInvariant();
            settings.CurrencySymbol = currency.Text;
            settings.DniLookupEnabled = dniEnabled.Checked;
            if (string.IsNullOrWhiteSpace(environmentToken)) settings.DniApiTokenProtected = AppData.ProtectSecret(dniToken.Text);
            AppData.SaveSettings(settings);
            MessageBox.Show("Configuración guardada.", "Edén POS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
