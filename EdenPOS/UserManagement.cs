using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class UserManagementPanel : CardPanel
    {
        private readonly DataGridView grid;
        private readonly Button toggleButton;

        public UserManagementPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size(760, 510);
            BackColor = Color.White;
            Padding = new Padding(32);

            var title = Theme.Label("Usuarios y permisos", 17f, FontStyle.Bold);
            title.Location = new Point(32, 26);
            Controls.Add(title);
            var subtitle = Theme.Label("La dueña administra Inventario y Configuración. Los trabajadores usan Ventas, Recibos y Turno.", 9.3f, FontStyle.Regular, Theme.Muted);
            subtitle.SetBounds(32, 62, 696, 24);
            subtitle.AutoSize = false;
            Controls.Add(subtitle);

            grid = new DataGridView { Location = new Point(32, 96), Size = new Size(696, 270), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Theme.StyleGrid(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Nombre", FillWeight = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Rol", FillWeight = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = "Estado", Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            grid.SelectionChanged += (s, e) => UpdateActions();
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelected(); };
            Controls.Add(grid);

            var add = Theme.Button("AGREGAR USUARIO", true);
            add.SetBounds(32, 386, 160, 50);
            add.Click += (s, e) => AddUser();
            Controls.Add(add);

            var edit = Theme.Button("EDITAR");
            edit.SetBounds(200, 386, 160, 50);
            edit.Click += (s, e) => EditSelected();
            Controls.Add(edit);

            toggleButton = Theme.Button("DESACTIVAR");
            toggleButton.SetBounds(368, 386, 176, 50);
            toggleButton.Click += (s, e) => ToggleSelected();
            Controls.Add(toggleButton);

            var delete = Theme.Button("ELIMINAR");
            delete.SetBounds(552, 386, 176, 50);
            delete.Click += (s, e) => DeleteSelected();
            Controls.Add(delete);

            var note = Theme.Label("La cuenta inicial es DUEÑA con PIN 0000. Cambie ese PIN desde Editar cuando termine de configurar a los trabajadores.", 9f, FontStyle.Regular, Theme.Muted);
            note.SetBounds(32, 449, 696, 38);
            note.AutoSize = false;
            Controls.Add(note);

            Action layoutActions = () =>
            {
                var innerWidth = Math.Max(400, ClientSize.Width - 64);
                subtitle.Width = innerWidth;
                grid.Width = innerWidth;
                note.Width = innerWidth;
                const int gap = 8;
                var actionWidth = Math.Max(100, (innerWidth - gap * 3) / 4);
                add.SetBounds(32, 386, actionWidth, 50);
                edit.SetBounds(add.Right + gap, 386, actionWidth, 50);
                toggleButton.SetBounds(edit.Right + gap, 386, actionWidth, 50);
                delete.SetBounds(toggleButton.Right + gap, 386, Math.Max(100, ClientSize.Width - toggleButton.Right - gap - 32), 50);
            };
            SizeChanged += (s, e) => layoutActions();
            layoutActions();
            RefreshUsers();
        }

        public void RefreshUsers()
        {
            if (grid == null) return;
            var selectedId = Current() == null ? null : Current().Id;
            grid.Rows.Clear();
            foreach (var user in AppData.Users.OrderBy(x => x.Role == UserRoles.Owner ? 0 : 1).ThenBy(x => x.Name))
            {
                var row = grid.Rows.Add(user.Name, UserRoles.DisplayName(user.Role), user.Active ? "ACTIVO" : "INACTIVO");
                grid.Rows[row].Tag = user;
                if (!user.Active) grid.Rows[row].DefaultCellStyle.ForeColor = Theme.Muted;
                if (user.Id == selectedId) grid.Rows[row].Selected = true;
            }
            if (grid.Rows.Count > 0 && grid.SelectedRows.Count == 0) grid.Rows[0].Selected = true;
            UpdateActions();
        }

        private AppUser Current()
        {
            return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as AppUser;
        }

        private void UpdateActions()
        {
            var user = Current();
            toggleButton.Enabled = user != null;
            toggleButton.Text = user != null && user.Active ? "DESACTIVAR" : "ACTIVAR";
        }

        private void AddUser()
        {
            using (var dialog = new UserEditorDialog(null))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    AppData.SaveUser(null, dialog.UserName, dialog.Pin, dialog.Role);
                    RefreshUsers();
                    MessageBox.Show("Usuario agregado correctamente.", "Usuarios", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    MessageBox.Show(ex.Message, "No se pudo agregar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void EditSelected()
        {
            var user = Current();
            if (user == null) return;
            using (var dialog = new UserEditorDialog(user))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    AppData.SaveUser(user.Id, dialog.UserName, dialog.Pin, dialog.Role);
                    RefreshUsers();
                    MessageBox.Show("Usuario actualizado. Los permisos se aplicarán completamente en el próximo inicio de sesión.", "Usuarios", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    MessageBox.Show(ex.Message, "No se pudo actualizar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ToggleSelected()
        {
            var user = Current();
            if (user == null) return;
            try
            {
                AppData.SetUserActive(user.Id, !user.Active);
                RefreshUsers();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Usuarios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteSelected()
        {
            var user = Current();
            if (user == null) return;
            if (MessageBox.Show("¿Eliminar al usuario “" + user.Name + "”?", "Usuarios", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                AppData.DeleteUser(user.Id);
                RefreshUsers();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Usuarios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    public sealed class UserEditorDialog : Form
    {
        private readonly TextBox nameBox;
        private readonly TextBox pinBox;
        private readonly ComboBox roleBox;
        private readonly bool editing;

        public string UserName { get { return nameBox.Text.Trim(); } }
        public string Pin { get { return pinBox.Text; } }
        public string Role { get { return roleBox.SelectedIndex == 0 ? UserRoles.Worker : UserRoles.Owner; } }

        public UserEditorDialog(AppUser user)
        {
            editing = user != null;
            Text = editing ? "Editar usuario" : "Agregar usuario";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(520, 420);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = Theme.Body;

            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Theme.Green, Padding = new Padding(24, 12, 24, 12) };
            header.Controls.Add(new Label
            {
                Text = editing ? "EDITAR USUARIO" : "NUEVO USUARIO",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            Controls.Add(header);

            var nameLabel = Theme.Label("Nombre", 9.5f, FontStyle.Bold);
            nameLabel.Location = new Point(30, 98);
            Controls.Add(nameLabel);
            nameBox = new TextBox
            {
                Location = new Point(30, 126),
                Size = new Size(460, 36),
                Font = new Font("Segoe UI", 11f),
                MaxLength = 60,
                Text = user == null ? "" : user.Name
            };
            Controls.Add(nameBox);

            var roleLabel = Theme.Label("Rol", 9.5f, FontStyle.Bold);
            roleLabel.Location = new Point(30, 180);
            Controls.Add(roleLabel);
            roleBox = new ComboBox
            {
                Location = new Point(30, 208),
                Size = new Size(220, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5f)
            };
            roleBox.Items.Add("TRABAJADOR");
            roleBox.Items.Add("DUEÑA");
            roleBox.SelectedIndex = user != null && user.Role == UserRoles.Owner ? 1 : 0;
            Controls.Add(roleBox);

            var pinLabel = Theme.Label(editing ? "Nuevo PIN (vacío conserva el actual)" : "PIN de acceso", 9.5f, FontStyle.Bold);
            pinLabel.Location = new Point(30, 262);
            Controls.Add(pinLabel);
            pinBox = new TextBox
            {
                Location = new Point(30, 290),
                Size = new Size(300, 36),
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                MaxLength = 8,
                UseSystemPasswordChar = true
            };
            pinBox.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            Controls.Add(pinBox);

            var show = Theme.Button("MOSTRAR");
            show.SetBounds(342, 286, 148, 44);
            show.MouseDown += (s, e) => pinBox.UseSystemPasswordChar = false;
            show.MouseUp += (s, e) => pinBox.UseSystemPasswordChar = true;
            show.MouseLeave += (s, e) => pinBox.UseSystemPasswordChar = true;
            Controls.Add(show);

            var hint = Theme.Label("Use entre 4 y 8 dígitos.", 9f, FontStyle.Regular, Theme.Muted);
            hint.Location = new Point(30, 334);
            Controls.Add(hint);

            var cancel = Theme.Button("CANCELAR");
            cancel.SetBounds(174, 360, 150, 48);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            var save = Theme.Button("GUARDAR", true);
            save.SetBounds(336, 360, 154, 48);
            save.Click += Save_Click;
            Controls.Add(save);
            CancelButton = cancel;
            AcceptButton = save;
            Shown += (s, e) => nameBox.Focus();
            Theme.FitDialogToWorkingArea(this);
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if (!Validation.IsValidRequiredText(nameBox.Text, 3, 60))
            {
                MessageBox.Show("El nombre debe tener entre 3 y 60 caracteres.", "Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameBox.Focus();
                return;
            }
            if ((!editing || pinBox.TextLength > 0) && !UserSecurity.IsValidPin(pinBox.Text))
            {
                MessageBox.Show("El PIN debe contener entre 4 y 8 dígitos.", "Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                pinBox.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
