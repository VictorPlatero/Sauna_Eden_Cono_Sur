using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class SalesView : UserControl, IRefreshable
    {
        private readonly ComboBox categories;
        private readonly TextBox searchBox;
        private readonly FlowLayoutPanel productFlow;
        private readonly DataGridView cartGrid;
        private readonly Label totalLabel;
        private readonly Label itemCountLabel;
        private readonly TextBox customerBox;
        private readonly TextBox dniBox;
        private readonly Button lookupDniButton;
        private readonly Label customerStatus;
        private readonly Button borrowedLockersLabel;
        private readonly Timer dniLookupTimer;
        private readonly Button chargeButton;
        private readonly Button saveButton;
        private readonly Button savedTicketsButton;
        private readonly Timer productSearchTimer;
        private readonly List<ReceiptLine> cart = new List<ReceiptLine>();
        private List<Product> availableProducts = new List<Product>();
        private bool loadingCategories;
        private bool lookupInProgress;
        private string lastAutoLookup;
        private string loadedSavedTicketId;
        private List<int> loadedLockerNumbers = new List<int>();

        public SalesView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Canvas;

            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Green, Padding = new Padding(20, 12, 18, 12) };
            categories = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = Theme.Ink,
                IntegralHeight = false,
                DropDownHeight = 260
            };
            categories.SelectedIndexChanged += (s, e) => { if (!loadingCategories) RenderProducts(); };
            searchBox = new TextBox
            {
                Dock = DockStyle.Right,
                Width = 250,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle
            };
            productSearchTimer = new Timer { Interval = 160 };
            productSearchTimer.Tick += (s, e) =>
            {
                productSearchTimer.Stop();
                RenderProducts();
            };
            searchBox.TextChanged += (s, e) =>
            {
                productSearchTimer.Stop();
                productSearchTimer.Start();
            };
            var searchLabel = new Label
            {
                Text = "Buscar",
                Dock = DockStyle.Right,
                Width = 64,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            top.Controls.Add(searchBox);
            top.Controls.Add(searchLabel);
            top.Controls.Add(categories);
            Controls.Add(top);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                IsSplitterFixed = false,
                SplitterWidth = 1,
                BackColor = Theme.Border
            };
            var adjustingSplit = false;
            split.SizeChanged += (s, e) =>
            {
                if (adjustingSplit || split.Width < 560) return;
                adjustingSplit = true;
                try
                {
                    var scale = Math.Max(1f, DeviceDpi / 96f);
                    var minimumTicket = (int)Math.Round(300 * scale);
                    var maximumTicket = (int)Math.Round(520 * scale);
                    var ticketWidth = Math.Max(minimumTicket, Math.Min(maximumTicket, (int)(split.Width * 0.34)));
                    var distance = Math.Max(250, split.Width - ticketWidth);
                    if (distance < split.Width) split.SplitterDistance = distance;
                }
                finally { adjustingSplit = false; }
            };
            Controls.Add(split);
            split.BringToFront();

            var productArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(14) };
            productFlow = new BufferedFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Theme.Canvas,
                Padding = new Padding(2)
            };
            productFlow.SizeChanged += (s, e) => ResizeProductTiles();
            productArea.Controls.Add(productFlow);
            split.Panel1.Controls.Add(productArea);

            var ticket = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            split.Panel2.Controls.Add(ticket);
            var ticketLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            ticketLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            ticketLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 242f));
            ticketLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            ticketLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 142f));
            ticket.Controls.Add(ticketLayout);
            var ticketHead = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 10, 18, 10), BackColor = Color.White, Margin = new Padding(0) };
            var title = Theme.Label("Ticket", 16f, FontStyle.Bold);
            title.AutoSize = false;
            title.SetBounds(18, 7, 105, 36);
            savedTicketsButton = Theme.Button("GUARDADOS (0)");
            savedTicketsButton.SetBounds(150, 7, 130, 36);
            savedTicketsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            savedTicketsButton.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            savedTicketsButton.Click += (s, e) => OpenSavedTicket();
            dniBox = new TextBox { Location = new Point(18, 62), Height = 32, Width = 148, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, MaxLength = 8 };
            lookupDniButton = Theme.Button("CONSULTAR");
            lookupDniButton.SetBounds(174, 58, 108, 38);
            lookupDniButton.Padding = new Padding(0);
            lookupDniButton.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            lookupDniButton.Click += async (s, e) => await LookupDniAsync(false);
            var dniHint = Theme.Label("DNI (8 dígitos)", 8.5f, FontStyle.Regular, Theme.Muted);
            dniHint.SetBounds(18, 43, 140, 18); dniHint.AutoSize = false;
            customerStatus = Theme.Label("Cliente opcional", 8.2f, FontStyle.Regular, Theme.Muted);
            customerStatus.SetBounds(18, 99, 264, 18); customerStatus.AutoSize = false;
            customerBox = new TextBox { Location = new Point(18, 137), Height = 32, Width = 264, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, MaxLength = 120 };
            var customerHint = Theme.Label("Nombre completo", 8.5f, FontStyle.Regular, Theme.Muted);
            customerHint.SetBounds(18, 118, 150, 18); customerHint.AutoSize = false;
            borrowedLockersLabel = new Button
            {
                Location = new Point(18, 180), Size = new Size(264, 50), AutoSize = false, Visible = false,
                BackColor = Theme.GreenSoft, ForeColor = Theme.GreenDark,
                Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold),
                Padding = new Padding(9, 4, 9, 4), TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            borrowedLockersLabel.FlatAppearance.BorderColor = Theme.Green;
            borrowedLockersLabel.Click += (s, e) => EditLoadedTicketLockers();
            ticketHead.Resize += (s, e) =>
            {
                savedTicketsButton.Left = Math.Max(126, ticketHead.ClientSize.Width - savedTicketsButton.Width - 18);
                lookupDniButton.Left = Math.Max(134, ticketHead.ClientSize.Width - lookupDniButton.Width - 18);
                dniBox.Width = Math.Max(105, lookupDniButton.Left - dniBox.Left - 8);
                customerStatus.Width = Math.Max(170, ticketHead.ClientSize.Width - 36);
                customerBox.Width = Math.Max(170, ticketHead.ClientSize.Width - 36);
                borrowedLockersLabel.Width = Math.Max(170, ticketHead.ClientSize.Width - 36);
            };
            dniBox.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            dniBox.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LookupDniAsync(false); } };
            dniLookupTimer = new Timer { Interval = 650 };
            dniLookupTimer.Tick += async (s, e) => { dniLookupTimer.Stop(); await LookupDniAsync(true); };
            dniBox.TextChanged += DniBox_TextChanged;
            Disposed += (s, e) =>
            {
                dniLookupTimer.Stop();
                dniLookupTimer.Dispose();
                productSearchTimer.Stop();
                productSearchTimer.Dispose();
            };
            ticketHead.Controls.Add(customerBox);
            ticketHead.Controls.Add(borrowedLockersLabel);
            ticketHead.Controls.Add(customerHint);
            ticketHead.Controls.Add(dniBox);
            ticketHead.Controls.Add(dniHint);
            ticketHead.Controls.Add(lookupDniButton);
            ticketHead.Controls.Add(customerStatus);
            ticketHead.Controls.Add(savedTicketsButton);
            ticketHead.Controls.Add(title);
            ticketLayout.Controls.Add(ticketHead, 0, 0);

            cartGrid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Theme.StyleGrid(cartGrid);
            cartGrid.ColumnHeadersVisible = false;
            cartGrid.RowTemplate.Height = 50;
            cartGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            cartGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Product", HeaderText = "Artículo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            cartGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Minus", Text = "−", UseColumnTextForButtonValue = true, Width = 36, FlatStyle = FlatStyle.Flat });
            cartGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Cant.", Width = 42, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            cartGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Plus", Text = "+", UseColumnTextForButtonValue = true, Width = 36, FlatStyle = FlatStyle.Flat });
            cartGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", Width = 82, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            cartGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Remove", Text = "×", UseColumnTextForButtonValue = true, Width = 36, FlatStyle = FlatStyle.Flat });
            cartGrid.CellContentClick += CartGrid_CellContentClick;
            ticketLayout.Controls.Add(cartGrid, 0, 1);

            var ticketFooter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 14), BackColor = Color.White, Margin = new Padding(0) };
            itemCountLabel = Theme.Label("Ticket vacío", 9.5f, FontStyle.Regular, Theme.Muted);
            itemCountLabel.Location = new Point(16, 12);
            totalLabel = Theme.Label(Theme.Money(0), 20f, FontStyle.Bold);
            totalLabel.AutoSize = false;
            totalLabel.TextAlign = ContentAlignment.MiddleRight;
            totalLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            totalLabel.SetBounds(150, 6, 165, 42);
            var clear = Theme.Button("LIMPIAR");
            clear.SetBounds(10, 67, 70, 52);
            clear.Padding = new Padding(0);
            clear.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            clear.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            clear.Click += (s, e) => ClearCurrentTicket();
            saveButton = Theme.Button("GUARDAR");
            saveButton.SetBounds(86, 67, 88, 52);
            saveButton.Padding = new Padding(0);
            saveButton.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            saveButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            saveButton.Click += (s, e) => SaveCurrentTicket();
            chargeButton = Theme.Button("COBRAR", true);
            chargeButton.SetBounds(180, 67, 135, 52);
            chargeButton.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            chargeButton.Click += ChargeButton_Click;
            ticketFooter.Resize += (s, e) =>
            {
                var scale = Math.Max(1f, DeviceDpi / 96f);
                var clearWidth = (int)Math.Round(70 * scale);
                var saveWidth = (int)Math.Round(88 * scale);
                var gap = (int)Math.Round(6 * scale);
                var buttonHeight = (int)Math.Round(52 * scale);
                var bottom = (int)Math.Round(14 * scale);
                totalLabel.Left = ticketFooter.ClientSize.Width - totalLabel.Width - 16;
                clear.SetBounds(10, ticketFooter.ClientSize.Height - buttonHeight - bottom, clearWidth, buttonHeight);
                saveButton.SetBounds(clear.Right + gap, clear.Top, saveWidth, buttonHeight);
                chargeButton.SetBounds(saveButton.Right + gap, clear.Top, Math.Max((int)Math.Round(105 * scale), ticketFooter.ClientSize.Width - saveButton.Right - gap - 10), buttonHeight);
            };
            ticketFooter.Controls.Add(itemCountLabel);
            ticketFooter.Controls.Add(totalLabel);
            ticketFooter.Controls.Add(clear);
            ticketFooter.Controls.Add(saveButton);
            ticketFooter.Controls.Add(chargeButton);
            ticketLayout.Controls.Add(ticketFooter, 0, 2);

            RefreshView();
        }

        public void RefreshView()
        {
            availableProducts = AppData.Products.Where(x => x.Active).ToList();
            var previous = categories.SelectedItem == null ? null : categories.SelectedItem.ToString();
            loadingCategories = true;
            categories.Items.Clear();
            categories.Items.Add("TODOS LOS ARTÍCULOS");
            foreach (var category in availableProducts.Select(x => x.Category).Distinct().OrderBy(x => x))
                categories.Items.Add(category);
            var index = previous == null ? -1 : categories.Items.IndexOf(previous);
            categories.SelectedIndex = index >= 0 ? index : Math.Min(1, categories.Items.Count - 1);
            loadingCategories = false;
            RenderProducts();
            RenderCart();
            UpdateSavedTicketButton();
        }

        private void RenderProducts()
        {
            if (productFlow == null || categories.SelectedItem == null) return;
            var selected = categories.SelectedItem.ToString();
            var query = availableProducts.AsEnumerable();
            if (selected != "TODOS LOS ARTÍCULOS") query = query.Where(x => x.Category == selected);
            var term = (searchBox.Text ?? "").Trim();
            if (term.Length > 0) query = query.Where(x => x.Name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0);

            productFlow.SuspendLayout();
            var previousControls = productFlow.Controls.Cast<Control>().ToArray();
            productFlow.Controls.Clear();
            foreach (var control in previousControls) control.Dispose();
            foreach (var product in query.OrderBy(x => x.Name))
            {
                var tile = new ProductTile(product);
                tile.ProductSelected += (s, e) => AddProduct(e);
                productFlow.Controls.Add(tile);
            }
            ResizeProductTiles();
            if (productFlow.Controls.Count == 0)
            {
                var empty = Theme.Label("No hay artículos que coincidan con la búsqueda.", 11f, FontStyle.Regular, Theme.Muted);
                empty.Margin = new Padding(20);
                productFlow.Controls.Add(empty);
            }
            productFlow.ResumeLayout();
        }

        private void AddProduct(Product product)
        {
            if (ProductOptionCatalog.HasOptions(product))
            {
                using (var dialog = new ProductOptionsDialog(product))
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.SelectedOption == null) return;
                    var option = dialog.SelectedOption;
                    var lineName = product.Name + " · " + option.Name;
                    var optionLine = cart.FirstOrDefault(x =>
                        x.ProductId == product.Id &&
                        x.OptionId == option.Id &&
                        x.Name == lineName &&
                        x.UnitPrice == option.Price &&
                        string.Equals(x.Comment ?? "", dialog.SelectedComment ?? "", StringComparison.OrdinalIgnoreCase));
                    if (optionLine == null)
                    {
                        cart.Add(new ReceiptLine
                        {
                            ProductId = product.Id,
                            OptionId = option.Id,
                            Name = lineName,
                            Comment = dialog.SelectedComment,
                            UnitPrice = option.Price,
                            Quantity = dialog.SelectedQuantity
                        });
                    }
                    else
                    {
                        var newQuantity = optionLine.Quantity + dialog.SelectedQuantity;
                        optionLine.Quantity = Math.Min(999, newQuantity);
                        if (newQuantity > 999)
                            MessageBox.Show("La cantidad máxima por artículo es 999.", "Validación del ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                RenderCart();
                return;
            }

            var line = cart.FirstOrDefault(x => x.ProductId == product.Id && string.IsNullOrWhiteSpace(x.Comment));
            if (line == null)
            {
                cart.Add(new ReceiptLine { ProductId = product.Id, Name = product.Name, UnitPrice = product.Price, Quantity = 1 });
            }
            else if (line.Quantity < 999) line.Quantity++;
            else MessageBox.Show("La cantidad máxima por artículo es 999.", "Validación del ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RenderCart();
        }

        private void RenderCart()
        {
            cartGrid.SuspendLayout();
            cartGrid.Rows.Clear();
            foreach (var line in cart)
            {
                var description = line.Name + "\n" + Theme.Money(line.UnitPrice);
                if (!string.IsNullOrWhiteSpace(line.Comment)) description += "  •  " + line.Comment;
                var row = cartGrid.Rows.Add(description, "−", line.Quantity, "+", Theme.Money(line.LineTotal), "×");
                cartGrid.Rows[row].Tag = line;
            }
            var count = cart.Sum(x => x.Quantity);
            var total = cart.Sum(x => x.LineTotal);
            itemCountLabel.Text = count == 0 ? "Ticket vacío" : count + (count == 1 ? " artículo" : " artículos");
            totalLabel.Text = Theme.Money(total);
            chargeButton.Text = count == 0 ? "COBRAR" : "COBRAR  " + Theme.Money(total);
            chargeButton.Enabled = count > 0;
            saveButton.Enabled = count > 0;
            saveButton.Text = string.IsNullOrWhiteSpace(loadedSavedTicketId) ? "GUARDAR" : "ACTUALIZAR";
            chargeButton.BackColor = count > 0 ? Theme.Green : Color.FromArgb(190, 205, 194);
            saveButton.BackColor = count > 0 ? Color.White : Color.FromArgb(242, 244, 245);
            UpdateSavedTicketButton();
            UpdateBorrowedLockersSummary();
            cartGrid.ResumeLayout();
        }

        private void ResizeProductTiles()
        {
            if (productFlow == null || productFlow.ClientSize.Width <= 0) return;
            var available = productFlow.ClientSize.Width - productFlow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4;
            if (available <= 0) return;
            var scale = Math.Max(1f, DeviceDpi / 96f);
            var preferred = Math.Max(150, (int)(180 * scale));
            var min = Math.Max(125, (int)(140 * scale));
            var max = Math.Max(190, (int)(230 * scale));
            var columns = Math.Max(1, available / preferred);
            if (columns == 1 && available >= (min * 2 + 16)) columns = 2;
            var width = (available - columns * 8) / columns;
            width = Math.Max(Math.Min(width, max), Math.Min(min, available - 8));
            foreach (ProductTile tile in productFlow.Controls.OfType<ProductTile>()) tile.SetResponsiveWidth(width, scale);
        }

        private void UpdateSavedTicketButton()
        {
            if (savedTicketsButton == null) return;
            var count = AppData.SavedTickets.Count;
            savedTicketsButton.Text = "GUARDADOS (" + count + ")";
            savedTicketsButton.ForeColor = count > 0 ? Theme.GreenDark : Theme.Muted;
        }

        private void DniBox_TextChanged(object sender, EventArgs e)
        {
            var normalized = Validation.NormalizeDni(dniBox.Text);
            if (dniBox.Text != normalized)
            {
                var position = dniBox.SelectionStart;
                dniBox.Text = normalized;
                dniBox.SelectionStart = Math.Min(position, dniBox.TextLength);
                return;
            }
            dniLookupTimer.Stop();
            if (normalized.Length == 8 && normalized != lastAutoLookup)
            {
                customerStatus.Text = "DNI válido; preparando consulta…";
                customerStatus.ForeColor = Theme.Muted;
                dniLookupTimer.Start();
            }
            else if (normalized.Length > 0 && normalized.Length < 8)
            {
                customerStatus.Text = "Faltan " + (8 - normalized.Length) + " dígitos.";
                customerStatus.ForeColor = Color.FromArgb(190, 105, 30);
            }
            else if (normalized.Length == 0)
            {
                customerStatus.Text = "Cliente opcional";
                customerStatus.ForeColor = Theme.Muted;
                lastAutoLookup = null;
            }
        }

        private async System.Threading.Tasks.Task LookupDniAsync(bool automatic)
        {
            if (lookupInProgress) return;
            var dni = Validation.NormalizeDni(dniBox.Text);
            if (!Validation.IsValidDni(dni))
            {
                customerStatus.Text = "Ingrese un DNI válido de 8 dígitos.";
                customerStatus.ForeColor = Color.FromArgb(190, 75, 65);
                if (!automatic) MessageBox.Show(customerStatus.Text, "Validación de DNI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            lookupInProgress = true;
            lastAutoLookup = dni;
            lookupDniButton.Enabled = false;
            lookupDniButton.Text = "BUSCANDO…";
            customerStatus.Text = "Consultando de forma segura…";
            customerStatus.ForeColor = Theme.Muted;
            try
            {
                var result = await DniLookupService.LookupAsync(dni);
                if (result.Success)
                {
                    customerBox.Text = result.FullName;
                    customerStatus.Text = "✓ " + result.Source;
                    customerStatus.ForeColor = Theme.GreenDark;
                    UpdateBorrowedLockersSummary();
                }
                else
                {
                    customerStatus.Text = result.Message;
                    customerStatus.ForeColor = Color.FromArgb(190, 105, 30);
                    if (!automatic && result.Message.IndexOf("desactivada", StringComparison.OrdinalIgnoreCase) < 0)
                        MessageBox.Show(result.Message, "Consulta de DNI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                lookupInProgress = false;
                lookupDniButton.Enabled = true;
                lookupDniButton.Text = "CONSULTAR";
            }
        }

        private bool ValidateCustomer()
        {
            var dni = Validation.NormalizeDni(dniBox.Text);
            var name = customerBox.Text.Trim();
            if (dni.Length > 0 && !Validation.IsValidDni(dni))
            {
                MessageBox.Show("El DNI debe tener exactamente 8 dígitos.", "Datos del cliente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dniBox.Focus();
                return false;
            }
            if (dni.Length > 0 && !Validation.IsValidPersonName(name))
            {
                MessageBox.Show("Ingrese o consulte el nombre completo correspondiente al DNI.", "Datos del cliente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                customerBox.Focus();
                return false;
            }
            if (name.Length > 0 && !Validation.IsValidPersonName(name))
            {
                MessageBox.Show("El nombre del cliente debe tener entre 3 y 120 caracteres.", "Datos del cliente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                customerBox.Focus();
                return false;
            }
            if (dni.Length == 8 && name.Length > 0)
            {
                var existing = AppData.FindCustomer(dni);
                if (existing == null || !string.Equals(existing.FullName, Validation.NormalizeName(name), StringComparison.OrdinalIgnoreCase))
                    AppData.SaveCustomer(dni, name, "Ingreso manual");
            }
            return true;
        }

        private void ClearCurrentTicket()
        {
            cart.Clear();
            customerBox.Clear();
            dniBox.Clear();
            lastAutoLookup = null;
            customerStatus.Text = "Cliente opcional";
            customerStatus.ForeColor = Theme.Muted;
            loadedSavedTicketId = null;
            loadedLockerNumbers.Clear();
            RenderCart();
        }

        private void SaveCurrentTicket()
        {
            if (cart.Count == 0) return;
            if (!ValidateCustomer()) return;
            var existing = string.IsNullOrWhiteSpace(loadedSavedTicketId)
                ? null
                : AppData.SavedTickets.FirstOrDefault(x => x.Id == loadedSavedTicketId);
            var suggested = existing != null
                ? existing.Name
                : string.IsNullOrWhiteSpace(customerBox.Text) ? "Ticket " + DateTime.Now.ToString("HH:mm") : customerBox.Text.Trim();
            var lockers = existing == null ? loadedLockerNumbers : AppData.GetTicketLockers(existing);
            int inferredAdults;
            int inferredChildren;
            OccupancyCalculator.Count(cart, out inferredAdults, out inferredChildren);
            var adults = existing == null ? inferredAdults : Math.Max(AppData.GetTicketAdultCount(existing), inferredAdults);
            var children = existing == null ? inferredChildren : Math.Max(AppData.GetTicketChildCount(existing), inferredChildren);
            using (var dialog = new SaveTicketDialog(suggested, lockers, adults, children, existing == null ? null : existing.Id))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    if (existing == null)
                        AppData.SaveTicket(cart, customerBox.Text, dniBox.Text, dialog.TicketName, dialog.LockerNumbers, dialog.AdultCount, dialog.ChildCount);
                    else
                        AppData.UpdateSavedTicket(existing.Id, cart, customerBox.Text, dniBox.Text, dialog.TicketName, dialog.LockerNumbers, dialog.AdultCount, dialog.ChildCount);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    MessageBox.Show(ex.Message, "Guardar ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            ClearCurrentTicket();
        }

        private void OpenSavedTicket()
        {
            using (var dialog = new SavedTicketsDialog())
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.SelectedTicket == null) return;
                if (cart.Count > 0 && MessageBox.Show("El ticket actual será reemplazado. ¿Continuar?", "Abrir ticket guardado", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                cart.Clear();
                foreach (var line in dialog.SelectedTicket.Lines)
                    cart.Add(new ReceiptLine { ProductId = line.ProductId, OptionId = line.OptionId, Name = line.Name, Comment = line.Comment, UnitPrice = line.UnitPrice, Quantity = line.Quantity });
                customerBox.Text = dialog.SelectedTicket.CustomerName == "Público general" ? "" : dialog.SelectedTicket.CustomerName;
                dniBox.Text = dialog.SelectedTicket.CustomerDni ?? "";
                lastAutoLookup = Validation.IsValidDni(dniBox.Text) ? dniBox.Text : null;
                customerStatus.Text = Validation.IsValidDni(dniBox.Text) ? "✓ Ticket recuperado" : "Cliente opcional";
                customerStatus.ForeColor = Validation.IsValidDni(dniBox.Text) ? Theme.GreenDark : Theme.Muted;
                loadedLockerNumbers = AppData.GetTicketLockers(dialog.SelectedTicket);
                loadedSavedTicketId = dialog.SelectedTicket.Id;
                RenderCart();
            }
        }

        private void UpdateBorrowedLockersSummary()
        {
            if (string.IsNullOrWhiteSpace(loadedSavedTicketId))
            {
                borrowedLockersLabel.Text = "";
                borrowedLockersLabel.Visible = false;
                return;
            }
            var displayName = string.IsNullOrWhiteSpace(customerBox.Text) ? "Público general" : customerBox.Text.Trim();
            var ticket = AppData.SavedTickets.FirstOrDefault(x => x.Id == loadedSavedTicketId);
            var people = ticket == null ? "" : "   •   Adultos: " + AppData.GetTicketAdultCount(ticket) + "   Niños: " + AppData.GetTicketChildCount(ticket);
            borrowedLockersLabel.Text = "Cliente: " + displayName + people + "\r\nLlaves: " + (loadedLockerNumbers.Count == 0 ? "Ninguna" : string.Join(", ", loadedLockerNumbers)) + "   • EDITAR";
            borrowedLockersLabel.Visible = true;
            borrowedLockersLabel.BringToFront();
        }

        private void EditLoadedTicketLockers()
        {
            if (string.IsNullOrWhiteSpace(loadedSavedTicketId)) return;
            var ticket = AppData.SavedTickets.FirstOrDefault(x => x.Id == loadedSavedTicketId);
            if (ticket == null)
            {
                MessageBox.Show("El ticket guardado ya no existe.", "Editar llaves", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearCurrentTicket();
                return;
            }

            var occupied = AppData.SavedTickets
                .Where(x => x.Id != ticket.Id)
                .SelectMany(AppData.GetTicketLockers)
                .Distinct()
                .ToList();
            using (var dialog = new LockerSelectionDialog(occupied, AppData.GetTicketLockers(ticket)))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    AppData.UpdateSavedTicketLockers(ticket.Id, dialog.SelectedLockers);
                    loadedLockerNumbers = dialog.SelectedLockers.ToList();
                    UpdateBorrowedLockersSummary();
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    MessageBox.Show(ex.Message, "Editar llaves", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void CartGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var line = cartGrid.Rows[e.RowIndex].Tag as ReceiptLine;
            if (line == null) return;
            var name = cartGrid.Columns[e.ColumnIndex].Name;
            if (name == "Minus") { line.Quantity--; if (line.Quantity <= 0) cart.Remove(line); }
            else if (name == "Plus" && line.Quantity < 999) line.Quantity++;
            else if (name == "Remove") cart.Remove(line);
            RenderCart();
        }

        private void ChargeButton_Click(object sender, EventArgs e)
        {
            if (cart.Count == 0) return;
            if (!ValidateCustomer()) return;
            if (AppData.OpenShift == null)
            {
                MessageBox.Show("Debe abrir un turno antes de cobrar una venta.", "Turno requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var saleTotal = cart.Sum(x => x.LineTotal);
            if (saleTotal == 0m && MessageBox.Show("Este ticket tiene total S/ 0.00. ¿Registrar como cortesía?", "Venta sin costo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using (var reminder = new RentalReminderDialog())
                if (reminder.ShowDialog(FindForm()) != DialogResult.OK) return;
            using (var dialog = new CheckoutDialog(saleTotal))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                Receipt receipt;
                try
                {
                    chargeButton.Enabled = false;
                    receipt = AppData.AddReceipt(cart, customerBox.Text, dniBox.Text, dialog.PaymentMethod, dialog.AmountReceived, dialog.CashAmount, dialog.YapeAmount);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    MessageBox.Show(ex.Message, "No se pudo cobrar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RenderCart();
                    return;
                }
                catch (Exception ex)
                {
                    AppData.LogError(ex, "Registrar cobro");
                    MessageBox.Show("No se pudo registrar el cobro. Revise que la carpeta de datos tenga espacio y permiso de escritura.", "No se pudo cobrar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    RenderCart();
                    return;
                }
                var change = dialog.PaymentMethod == "Efectivo" ? Math.Max(0m, dialog.AmountReceived - receipt.Total) : 0m;
                var completedSavedTicketId = loadedSavedTicketId;
                ClearCurrentTicket();
                if (!string.IsNullOrWhiteSpace(completedSavedTicketId))
                {
                    try { AppData.DeleteSavedTicket(completedSavedTicketId); }
                    catch (Exception ex)
                    {
                        AppData.LogError(ex, "Liberar llaves después del cobro");
                        MessageBox.Show("La venta sí fue registrada, pero no se pudo eliminar el ticket pendiente. Elimínelo manualmente desde Guardados para liberar sus llaves.", "Venta registrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                using (var complete = new SaleCompleteDialog(receipt, change)) complete.ShowDialog(FindForm());
            }
        }
    }

    public sealed class ProductTile : UserControl
    {
        private readonly Product product;
        private readonly Panel footer;
        public event EventHandler<Product> ProductSelected;

        public ProductTile(Product item)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            product = item;
            Width = 164;
            Height = 148;
            Margin = new Padding(4);
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            Padding = new Padding(1);

            var image = AppData.LoadImage(item.ImageFile);
            var picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(246, 248, 249),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = image,
                Padding = new Padding(12)
            };
            footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.FromArgb(225, 35, 44, 52), Padding = new Padding(8, 4, 8, 3) };
            var name = new Label
            {
                Text = item.Name,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            var price = new Label
            {
                Text = ProductOptionCatalog.HasOptions(item) ? "DESDE " + Theme.Money(item.Price) : Theme.Money(item.Price),
                Dock = DockStyle.Right,
                Width = ProductOptionCatalog.HasOptions(item) ? 88 : 61,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            footer.Controls.Add(name);
            footer.Controls.Add(price);
            Controls.Add(picture);
            Controls.Add(footer);
            foreach (Control control in new Control[] { this, picture, footer, name, price }) control.Click += SelectProduct;
            Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Theme.Border, ButtonBorderStyle.Solid);
        }

        public void SetResponsiveWidth(int width, float scale)
        {
            Width = Math.Max(110, width);
            Height = Math.Max((int)(132 * scale), Math.Min((int)(205 * scale), (int)(Width * 0.88)));
            footer.Height = Math.Max((int)(50 * scale), (int)(Height * 0.34));
        }

        private void SelectProduct(object sender, EventArgs e)
        {
            var handler = ProductSelected;
            if (handler != null) handler(this, product);
        }
    }
}
