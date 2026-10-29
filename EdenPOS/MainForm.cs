using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class MainForm : Form
    {
        private readonly Panel sidebar;
        private readonly Panel host;
        private readonly Label employeeLabel;
        private readonly Label storeLabel;
        private readonly Label roleLabel;
        private readonly Dictionary<string, Button> navButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Control> views = new Dictionary<string, Control>();
        private string currentKey;
        public bool LogoutRequested { get; private set; }

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Text = "Edén POS";
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(800, 600);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Canvas;
            Font = Theme.Body;
            KeyPreview = true;

            sidebar = new Panel { Dock = DockStyle.Left, Width = 232, BackColor = Color.White, Padding = new Padding(0) };
            host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
            Controls.Add(host);
            Controls.Add(sidebar);

            var sidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            sidebar.Controls.Add(sidebarLayout);

            var brand = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Green, Padding = new Padding(22, 18, 16, 14), Margin = new Padding(0) };
            employeeLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            roleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 27,
                ForeColor = Color.FromArgb(228, 255, 234),
                Font = new Font("Segoe UI", 10.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            storeLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 6, 0, 0)
            };
            brand.Controls.Add(storeLabel);
            brand.Controls.Add(roleLabel);
            brand.Controls.Add(employeeLabel);
            sidebarLayout.Controls.Add(brand, 0, 0);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(250, 251, 252),
                Margin = new Padding(0),
                Padding = new Padding(10, 6, 10, 8)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            footer.Controls.Add(new Label
            {
                Text = "EDÉN POS  •  v2.3.1",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Muted,
                Font = Theme.Small
            }, 0, 0);
            var logout = Theme.Button("CERRAR SESIÓN");
            logout.Dock = DockStyle.Fill;
            logout.Margin = new Padding(4);
            logout.Click += (s, e) =>
            {
                if (MessageBox.Show("¿Cerrar la sesión de " + AppSession.UserName + "?", "Cerrar sesión", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                LogoutRequested = true;
                Close();
            };
            footer.Controls.Add(logout, 0, 1);
            sidebarLayout.Controls.Add(footer, 0, 2);

            var navigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(0, 12, 0, 0)
            };
            sidebarLayout.Controls.Add(navigation, 0, 1);

            if (AppSession.IsOwner)
            {
                AddNavigation(navigation, "inventory", "☷   Inventario", () => new ProductsView());
                AddNavigation(navigation, "settings", "⚙   Configuración", () => new SettingsView());
            }
            else
            {
                AddNavigation(navigation, "sales", "▣   Ventas", () => new SalesView());
                AddNavigation(navigation, "receipts", "▤   Recibos", () => new ReceiptsView());
                AddNavigation(navigation, "shift", "◷   Turno", () => new ShiftView());
            }

            Action updateResponsiveLayout = () =>
            {
                var dpiScale = Math.Max(1f, DeviceDpi / 96f);
                var minimumSidebar = (int)Math.Round(180 * dpiScale);
                var maximumSidebar = (int)Math.Round(300 * dpiScale);
                sidebar.Width = Math.Max(minimumSidebar, Math.Min(maximumSidebar, (int)(ClientSize.Width * 0.17)));
                foreach (var button in navButtons.Values)
                    button.Width = Math.Max(170, navigation.ClientSize.Width - 2);
            };
            Resize += (s, e) => updateResponsiveLayout();
            navigation.SizeChanged += (s, e) => updateResponsiveLayout();

            AppData.DataChanged += OnDataChanged;
            FormClosed += (s, e) => AppData.DataChanged -= OnDataChanged;
            KeyDown += MainForm_KeyDown;
            UpdateIdentity();
            updateResponsiveLayout();
            Navigate(AppSession.IsOwner ? "inventory" : "sales");
        }

        private void AddNavigation(FlowLayoutPanel navigation, string key, string text, Func<Control> factory)
        {
            var button = new Button
            {
                Text = text,
                Width = 220,
                Height = 58,
                Margin = new Padding(0),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(24, 0, 0, 0),
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = factory
            };
            button.Click += (s, e) => Navigate(key);
            navigation.Controls.Add(button);
            navButtons[key] = button;
        }

        private void Navigate(string key)
        {
            if (!navButtons.ContainsKey(key)) return;
            currentKey = key;
            foreach (var item in navButtons)
            {
                item.Value.BackColor = item.Key == key ? Theme.GreenSoft : Color.White;
                item.Value.ForeColor = item.Key == key ? Theme.GreenDark : Theme.Ink;
            }

            if (!views.ContainsKey(key))
            {
                var factory = (Func<Control>)navButtons[key].Tag;
                var control = factory();
                control.Dock = DockStyle.Fill;
                views[key] = control;
            }

            host.SuspendLayout();
            host.Controls.Clear();
            host.Controls.Add(views[key]);
            host.ResumeLayout();
            var refreshable = views[key] as IRefreshable;
            if (refreshable != null) refreshable.RefreshView();
        }

        private void UpdateIdentity()
        {
            var settings = AppData.Settings;
            employeeLabel.Text = string.IsNullOrWhiteSpace(AppSession.UserName) ? settings.EmployeeName : AppSession.UserName;
            roleLabel.Text = AppSession.RoleName;
            storeLabel.Text = settings.StoreName;
            Text = "Edén POS — " + settings.StoreName;
        }

        private void OnDataChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(UpdateCurrentView)); return; }
            UpdateCurrentView();
        }

        private void UpdateCurrentView()
        {
            UpdateIdentity();
            Control control;
            if (currentKey != null && views.TryGetValue(currentKey, out control))
            {
                var refreshable = control as IRefreshable;
                if (refreshable != null) refreshable.RefreshView();
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2) Navigate("sales");
            else if (e.KeyCode == Keys.F3) Navigate("receipts");
            else if (e.KeyCode == Keys.F4) Navigate("shift");
            else if (e.KeyCode == Keys.F5) UpdateCurrentView();
        }
    }
}
