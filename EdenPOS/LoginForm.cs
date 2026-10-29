using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class LoginForm : Form
    {
        private readonly ComboBox userBox;
        private readonly TextBox pinBox;
        private readonly Label statusLabel;
        private readonly Button loginButton;
        private readonly Timer unlockTimer;
        private int failedAttempts;

        public LoginForm()
        {
            Text = "Ingresar a Edén POS";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(560, 680);
            MaximizeBox = false;
            MinimizeBox = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Canvas;
            Font = Theme.Body;
            KeyPreview = true;

            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Theme.GreenDark };
            var storeName = new Label
            {
                Text = AppData.Settings.StoreName,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            header.Controls.Add(storeName);
            Controls.Add(header);

            var card = new CardPanel
            {
                Location = new Point(34, 82),
                Size = new Size(492, 586),
                BackColor = Color.White,
                Radius = 14,
                BorderColor = Color.FromArgb(216, 225, 219)
            };
            Controls.Add(card);

            var logo = new PictureBox
            {
                Location = new Point(176, 10),
                Size = new Size(140, 140),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            logo.Image = LoadLoginLogo();
            card.Controls.Add(logo);

            var welcome = new Label
            {
                Text = "BIENVENIDO",
                Location = new Point(30, 150),
                Size = new Size(432, 32),
                ForeColor = Theme.GreenDark,
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(welcome);

            var subtitle = new Label
            {
                Text = "Seleccione su usuario e ingrese su PIN",
                Location = new Point(30, 181),
                Size = new Size(432, 24),
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(subtitle);

            var userLabel = Theme.Label("USUARIO", 9f, FontStyle.Bold, Theme.Muted);
            userLabel.Location = new Point(46, 214);
            card.Controls.Add(userLabel);
            userBox = new ComboBox
            {
                Location = new Point(46, 238),
                Size = new Size(400, 38),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                DisplayMember = "Name",
                IntegralHeight = false,
                DropDownHeight = 180
            };
            userBox.SelectedIndexChanged += (s, e) => ClearPin();
            card.Controls.Add(userBox);

            var pinLabel = Theme.Label("PIN", 9f, FontStyle.Bold, Theme.Muted);
            pinLabel.Location = new Point(46, 287);
            card.Controls.Add(pinLabel);
            pinBox = new TextBox
            {
                Location = new Point(46, 311),
                Size = new Size(400, 40),
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                UseSystemPasswordChar = true,
                MaxLength = 8,
                BackColor = Color.White
            };
            card.Controls.Add(pinBox);

            var keypad = new TableLayoutPanel
            {
                Location = new Point(96, 363),
                Size = new Size(300, 136),
                ColumnCount = 3,
                RowCount = 4,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            for (var column = 0; column < 3; column++) keypad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            for (var row = 0; row < 4; row++) keypad.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
            var keys = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "←" };
            foreach (var key in keys)
            {
                var button = Theme.Button(key, key.All(char.IsDigit));
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(3);
                button.Padding = new Padding(0);
                button.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
                button.Tag = key;
                button.Click += Keypad_Click;
                keypad.Controls.Add(button);
            }
            card.Controls.Add(keypad);

            statusLabel = new Label
            {
                Location = new Point(40, 505),
                Size = new Size(412, 22),
                ForeColor = Color.Firebrick,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(statusLabel);

            loginButton = Theme.Button("INGRESAR", true);
            loginButton.SetBounds(96, 531, 300, 46);
            loginButton.Click += (s, e) => PerformLogin();
            card.Controls.Add(loginButton);
            AcceptButton = loginButton;

            unlockTimer = new Timer { Interval = 10000 };
            unlockTimer.Tick += (s, e) =>
            {
                unlockTimer.Stop();
                failedAttempts = 0;
                SetLoginEnabled(true);
                statusLabel.Text = "";
            };

            LoadUsers();
            ApplyMonitorScale();
            KeyPress += LoginForm_KeyPress;
            KeyDown += LoginForm_KeyDown;
            FormClosed += (s, e) =>
            {
                unlockTimer.Stop();
                unlockTimer.Dispose();
                if (logo.Image != null) logo.Image.Dispose();
            };
        }

        private void LoadUsers()
        {
            userBox.Items.Clear();
            foreach (var user in AppData.Users.Where(x => x.Active)
                         .OrderBy(x => x.Role == UserRoles.Owner ? 0 : 1)
                         .ThenBy(x => x.Name))
                userBox.Items.Add(user);
            if (userBox.Items.Count > 0) userBox.SelectedIndex = 0;
            else
            {
                statusLabel.Text = "No existen usuarios activos.";
                SetLoginEnabled(false);
            }
        }

        private void Keypad_Click(object sender, EventArgs e)
        {
            var key = Convert.ToString(((Button)sender).Tag);
            if (key == "C") ClearPin();
            else if (key == "←") RemoveLastDigit();
            else AddDigit(key);
        }

        private void LoginForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar))
            {
                AddDigit(e.KeyChar.ToString());
                e.Handled = true;
            }
        }

        private void LoginForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
            {
                RemoveLastDigit();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ClearPin();
                e.SuppressKeyPress = true;
            }
        }

        private void AddDigit(string digit)
        {
            if (!loginButton.Enabled || pinBox.TextLength >= pinBox.MaxLength) return;
            pinBox.Text += digit;
            statusLabel.Text = "";
        }

        private void RemoveLastDigit()
        {
            if (pinBox.TextLength > 0) pinBox.Text = pinBox.Text.Substring(0, pinBox.TextLength - 1);
            statusLabel.Text = "";
        }

        private void ClearPin()
        {
            pinBox.Clear();
            statusLabel.Text = "";
        }

        private void PerformLogin()
        {
            var selected = userBox.SelectedItem as AppUser;
            if (selected == null)
            {
                statusLabel.Text = "Seleccione un usuario.";
                return;
            }
            if (!UserSecurity.IsValidPin(pinBox.Text))
            {
                statusLabel.Text = "Ingrese un PIN de 4 a 8 dígitos.";
                return;
            }

            var authenticated = AppData.AuthenticateUser(selected.Id, pinBox.Text);
            if (authenticated == null)
            {
                failedAttempts++;
                ClearPin();
                statusLabel.Text = failedAttempts >= 5
                    ? "Demasiados intentos. Espere 10 segundos."
                    : "PIN incorrecto. Intente nuevamente.";
                if (failedAttempts >= 5)
                {
                    SetLoginEnabled(false);
                    unlockTimer.Start();
                }
                return;
            }

            AppSession.SignIn(authenticated);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetLoginEnabled(bool enabled)
        {
            loginButton.Enabled = enabled;
            userBox.Enabled = enabled;
            foreach (Button button in Controls.OfType<Control>()
                         .SelectMany(Descendants)
                         .OfType<Button>()
                         .Where(x => x != loginButton))
                button.Enabled = enabled;
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private static Image LoadLoginLogo()
        {
            var assemblyDirectory = Path.GetDirectoryName(typeof(LoginForm).Assembly.Location);
            var path = Path.Combine(assemblyDirectory ?? AppDomain.CurrentDomain.BaseDirectory, "Assets", "EdenPOS-Login-v2.png");
            try
            {
                if (!File.Exists(path)) return null;
                using (var stream = File.OpenRead(path))
                using (var image = Image.FromStream(stream))
                    return new Bitmap(image);
            }
            catch
            {
                return null;
            }
        }

        private void ApplyMonitorScale()
        {
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            var availableWidth = Math.Max(420, workingArea.Width - 48);
            var availableHeight = Math.Max(520, workingArea.Height - 32);
            var dpiScale = Math.Max(1f, DeviceDpi / 96f);
            var maximumScale = Math.Min(1.6f, Math.Max(1.25f, dpiScale));
            var scale = Math.Min(availableWidth / 560f, availableHeight / 680f);
            scale = Math.Max(0.55f, Math.Min(maximumScale, scale));
            if (Math.Abs(scale - 1f) > 0.01f)
            {
                ScaleControlTree(this, scale);
                ClientSize = new Size((int)Math.Round(560 * scale), (int)Math.Round(680 * scale));
            }

            StartPosition = FormStartPosition.Manual;
            Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2));
        }

        private static void ScaleControlTree(Control parent, float scale)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Dock == DockStyle.None)
                {
                    child.Bounds = new Rectangle(
                        (int)Math.Round(child.Left * scale),
                        (int)Math.Round(child.Top * scale),
                        Math.Max(1, (int)Math.Round(child.Width * scale)),
                        Math.Max(1, (int)Math.Round(child.Height * scale)));
                }
                else if (child.Dock == DockStyle.Top || child.Dock == DockStyle.Bottom)
                {
                    child.Height = Math.Max(1, (int)Math.Round(child.Height * scale));
                }
                else if (child.Dock == DockStyle.Left || child.Dock == DockStyle.Right)
                {
                    child.Width = Math.Max(1, (int)Math.Round(child.Width * scale));
                }

                child.Margin = ScalePadding(child.Margin, scale);
                child.Padding = ScalePadding(child.Padding, scale);
                if (child.Font != null)
                    child.Font = new Font(child.Font.FontFamily, Math.Max(7f, child.Font.Size * scale), child.Font.Style, GraphicsUnit.Point);
                ScaleControlTree(child, scale);
            }
        }

        private static Padding ScalePadding(Padding value, float scale)
        {
            return new Padding(
                (int)Math.Round(value.Left * scale),
                (int)Math.Round(value.Top * scale),
                (int)Math.Round(value.Right * scale),
                (int)Math.Round(value.Bottom * scale));
        }
    }
}
