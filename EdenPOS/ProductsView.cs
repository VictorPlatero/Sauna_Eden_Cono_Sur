using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class ProductsView : UserControl, IRefreshable
    {
        private readonly DataGridView grid;
        private readonly TextBox search;
        private readonly ComboBox category;
        private readonly Button entryButton;
        private readonly Button addVariantButton;
        private readonly Button editButton;
        private readonly Button deleteButton;

        private sealed class InventorySelection
        {
            public Product Product { get; set; }
            public ProductOption Option { get; set; }
            public bool IsOption { get { return Option != null; } }
            public string Key { get { return Product.Id + "|" + (Option == null ? "" : Option.Id); } }
        }

        public ProductsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Canvas;
            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Green, Padding = new Padding(22, 12, 18, 12) };
            top.Controls.Add(new Label
            {
                Text = "Productos e inventario",
                Dock = DockStyle.Left,
                Width = 330,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            search = new TextBox { Dock = DockStyle.Right, Width = 260, Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle };
            search.TextChanged += (s, e) => LoadProducts();
            top.Controls.Add(search);
            top.Controls.Add(new Label { Text = "Buscar", Dock = DockStyle.Right, Width = 64, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 10f) });

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 126, BackColor = Color.White, Padding = new Padding(12, 10, 12, 8), WrapContents = true, AutoScroll = true };
            category = new ComboBox
            {
                Width = 210, Height = 50, Margin = new Padding(4), DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat, Font = Theme.Body
            };
            category.SelectedIndexChanged += (s, e) => LoadProducts();
            entryButton = Theme.Button("+  REGISTRAR ENTRADA", true); entryButton.Size = new Size(205, 50); entryButton.Click += (s, e) => RegisterEntry();
            var add = Theme.Button("NUEVO ARTÍCULO", true); add.Size = new Size(175, 50); add.Click += (s, e) => AddProduct();
            addVariantButton = Theme.Button("+  NUEVA VARIANTE"); addVariantButton.Size = new Size(185, 50); addVariantButton.Click += (s, e) => AddVariant();
            editButton = Theme.Button("EDITAR"); editButton.Size = new Size(115, 50); editButton.Click += (s, e) => EditSelected();
            deleteButton = Theme.Button("ELIMINAR"); deleteButton.Size = new Size(125, 50); deleteButton.Click += (s, e) => DeleteSelected();
            toolbar.Controls.Add(category); toolbar.Controls.Add(entryButton); toolbar.Controls.Add(add); toolbar.Controls.Add(addVariantButton); toolbar.Controls.Add(editButton); toolbar.Controls.Add(deleteButton);
            var chrome = new TableLayoutPanel { Dock = DockStyle.Top, Height = 194, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0), BackColor = Color.White };
            chrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            chrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
            chrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 126f));
            top.Dock = DockStyle.Fill; toolbar.Dock = DockStyle.Fill;
            chrome.Controls.Add(top, 0, 0); chrome.Controls.Add(toolbar, 0, 1);
            Controls.Add(chrome);

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = Theme.Canvas };
            grid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(grid);
            grid.ColumnHeadersHeight = 58;
            grid.RowTemplate.Height = 52;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "CATEGORÍA", Width = 145, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "PRODUCTO / VARIANTE", FillWeight = 190 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "TIPO", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entry", HeaderText = "ENTRADA", Width = 92, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Exit", HeaderText = "SALIDAS", Width = 92, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Balance", HeaderText = "SALDO", Width = 82, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold) } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "PRECIO / DESDE", Width = 115, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.SelectionChanged += (s, e) => UpdateActionButtons();
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelected(); };
            host.Controls.Add(grid);
            Controls.Add(host);
            host.BringToFront();
            RefreshView();
        }

        public void RefreshView()
        {
            var current = category.SelectedItem == null ? "TODAS LAS CATEGORÍAS" : category.SelectedItem.ToString();
            category.Items.Clear();
            category.Items.Add("TODAS LAS CATEGORÍAS");
            foreach (var item in AppData.Products.Select(x => x.Category).Distinct().OrderBy(x => x)) category.Items.Add(item);
            var index = category.Items.IndexOf(current);
            category.SelectedIndex = index >= 0 ? index : 0;
            LoadProducts();
        }

        private void LoadProducts()
        {
            if (grid == null || category.SelectedItem == null) return;
            var selectedItem = SelectedItem();
            var selectedKey = selectedItem == null ? null : selectedItem.Key;
            var query = AppData.Products.AsEnumerable();
            var selectedCategory = category.SelectedItem.ToString();
            if (selectedCategory != "TODAS LAS CATEGORÍAS") query = query.Where(x => x.Category == selectedCategory);
            var term = (search.Text ?? "").Trim();
            if (term.Length > 0)
                query = query.Where(product =>
                    product.Name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    ProductOptionCatalog.GetOptions(product).Any(option =>
                        option.Name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0));
            var products = query.OrderBy(x => x.Category).ThenBy(x => x.Name).ToList();

            grid.SuspendLayout();
            grid.Rows.Clear();
            var today = DateTime.Today;
            var entries = AppData.InventoryMovements.Where(x => x.CreatedAt >= today && x.Type == "Entrada").GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(m => m.Quantity));
            var paidLines = AppData.Receipts.Where(x => x.CreatedAt >= today && x.Status == "Pagado").SelectMany(x => x.Lines ?? new List<ReceiptLine>()).ToList();
            var exits = paidLines.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(m => m.Quantity));
            foreach (var product in products)
            {
                var options = ProductOptionCatalog.GetOptions(product).ToList();
                var entryCount = entries.ContainsKey(product.Id) ? entries[product.Id] : 0;
                var exitCount = exits.ContainsKey(product.Id) ? exits[product.Id] : 0;
                var balance = product.TrackInventory ? product.Stock.ToString() : "—";
                var type = options.Count > 0 ? "GRUPO (" + options.Count + ")" : "ARTÍCULO";
                var row = grid.Rows.Add(product.Category, product.Name, type, entryCount, exitCount, balance, Theme.Money(product.Price));
                var productSelection = new InventorySelection { Product = product };
                grid.Rows[row].Tag = productSelection;
                if (options.Count > 0)
                {
                    grid.Rows[row].DefaultCellStyle.BackColor = Theme.GreenSoft;
                    grid.Rows[row].DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
                    grid.Rows[row].Cells["Type"].Style.ForeColor = Theme.GreenDark;
                }
                if (!product.Active) grid.Rows[row].DefaultCellStyle.ForeColor = Theme.Muted;
                if (product.TrackInventory && product.Stock <= 3) grid.Rows[row].Cells["Balance"].Style.ForeColor = Color.Firebrick;
                if (productSelection.Key == selectedKey) grid.Rows[row].Selected = true;

                var visibleOptions = term.Length > 0 &&
                                     product.Name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) < 0
                    ? options.Where(option => option.Name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    : options;
                foreach (var option in visibleOptions)
                {
                    var optionEntry = entries.ContainsKey(option.Id) ? entries[option.Id] : 0;
                    var optionExit = paidLines
                        .Where(line => line.ProductId == product.Id &&
                                      ((!string.IsNullOrWhiteSpace(line.OptionId) && line.OptionId == option.Id) ||
                                       (string.IsNullOrWhiteSpace(line.OptionId) && line.Name == product.Name + " · " + option.Name)))
                        .Sum(line => line.Quantity);
                    var optionBalance = option.TrackInventory ? option.Stock.ToString() : "—";
                    var optionRow = grid.Rows.Add("", "    ↳  " + option.Name, "VARIANTE", optionEntry, optionExit, optionBalance, Theme.Money(option.Price));
                    var optionSelection = new InventorySelection { Product = product, Option = option };
                    grid.Rows[optionRow].Tag = optionSelection;
                    grid.Rows[optionRow].DefaultCellStyle.BackColor = Color.FromArgb(252, 253, 253);
                    grid.Rows[optionRow].Cells["Name"].Style.ForeColor = Theme.GreenDark;
                    grid.Rows[optionRow].Cells["Type"].Style.ForeColor = Theme.Muted;
                    if (option.TrackInventory && option.Stock <= 3) grid.Rows[optionRow].Cells["Balance"].Style.ForeColor = Color.Firebrick;
                    if (optionSelection.Key == selectedKey) grid.Rows[optionRow].Selected = true;
                }
            }
            if (grid.Rows.Count > 0 && grid.SelectedRows.Count == 0) grid.Rows[0].Selected = true;
            grid.ResumeLayout();
            UpdateActionButtons();
        }

        private InventorySelection SelectedItem()
        {
            return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as InventorySelection;
        }

        private Product SelectedProduct()
        {
            var item = SelectedItem();
            return item == null ? null : item.Product;
        }

        private void UpdateActionButtons()
        {
            var item = SelectedItem();
            var hasSelection = item != null;
            entryButton.Enabled = hasSelection;
            addVariantButton.Enabled = hasSelection;
            editButton.Enabled = hasSelection;
            deleteButton.Enabled = hasSelection;
            editButton.Text = hasSelection && item.IsOption ? "EDITAR VARIANTE" : "EDITAR";
            deleteButton.Text = hasSelection && item.IsOption ? "QUITAR VARIANTE" : "ELIMINAR";
        }

        private void AddProduct()
        {
            using (var dialog = new ProductEditorDialog(null, AppData.Products.Select(x => x.Category).Distinct()))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                var products = AppData.Products;
                if (products.Any(x => x.Name == dialog.Product.Name && x.Category == dialog.Product.Category))
                {
                    MessageBox.Show("Ya existe un artículo con el mismo nombre en esa categoría.", "Validación de artículos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                products.Add(dialog.Product);
                AppData.SaveProducts(products);
                RefreshView();
                SelectProduct(dialog.Product.Id);
                RegisterEntry();
                RefreshView();
            }
        }

        private void AddVariant()
        {
            var selected = SelectedItem();
            if (selected == null) return;
            using (var dialog = new ProductOptionEditorDialog(selected.Product.Name, null))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                var products = AppData.Products;
                var product = products.FirstOrDefault(x => x.Id == selected.Product.Id);
                if (product == null) return;
                if (product.Options == null) product.Options = new List<ProductOption>();
                if (product.Options.Any(x => string.Equals(x.Name, dialog.Option.Name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    MessageBox.Show("Ya existe una variante con ese nombre.", "Variantes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                product.Options.Add(dialog.Option);
                product.Price = product.Options.Min(x => x.Price);
                product.TrackInventory = false;
                AppData.SaveProducts(products);
                RefreshView();
                SelectItem(product.Id, dialog.Option.Id);
            }
        }

        private void EditSelected()
        {
            var selection = SelectedItem();
            if (selection == null) return;
            if (selection.IsOption)
            {
                using (var dialog = new ProductOptionEditorDialog(selection.Product.Name, selection.Option))
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                    var products = AppData.Products;
                    var product = products.FirstOrDefault(x => x.Id == selection.Product.Id);
                    if (product == null || product.Options == null) return;
                    if (product.Options.Any(x => x.Id != selection.Option.Id &&
                                                  string.Equals(x.Name, dialog.Option.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        MessageBox.Show("Ya existe otra variante con ese nombre.", "Variantes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var storedOption = product.Options.FirstOrDefault(x => x.Id == selection.Option.Id);
                    if (storedOption == null) return;
                    storedOption.Name = dialog.Option.Name;
                    storedOption.Price = dialog.Option.Price;
                    product.Price = product.Options.Min(x => x.Price);
                    AppData.SaveProducts(products);
                    RefreshView();
                    SelectItem(product.Id, storedOption.Id);
                }
                return;
            }

            var selected = selection.Product;
            using (var dialog = new ProductEditorDialog(selected, AppData.Products.Select(x => x.Category).Distinct()))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                var products = AppData.Products;
                if (products.Any(x => x.Id != selected.Id && x.Name == dialog.Product.Name && x.Category == dialog.Product.Category))
                {
                    MessageBox.Show("Ya existe otro artículo con el mismo nombre en esa categoría.", "Validación de artículos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var stored = products.FirstOrDefault(x => x.Id == selected.Id);
                if (stored != null)
                {
                    stored.Name = dialog.Product.Name;
                    stored.Category = dialog.Product.Category;
                    stored.Price = stored.Options != null && stored.Options.Count > 0 ? stored.Options.Min(x => x.Price) : dialog.Product.Price;
                    stored.ColorHex = dialog.Product.ColorHex;
                    stored.ImageFile = dialog.Product.ImageFile;
                    stored.Active = dialog.Product.Active;
                    stored.TrackInventory = dialog.Product.TrackInventory;
                }
                AppData.SaveProducts(products);
                RefreshView();
            }
        }

        private void ToggleSelected()
        {
            var selected = SelectedProduct();
            if (selected == null) return;
            var products = AppData.Products;
            var stored = products.FirstOrDefault(x => x.Id == selected.Id);
            if (stored == null) return;
            stored.Active = !stored.Active;
            AppData.SaveProducts(products);
            RefreshView();
        }

        private void SelectProduct(string id)
        {
            SelectItem(id, null);
        }

        private void SelectItem(string productId, string optionId)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                var item = row.Tag as InventorySelection;
                if (item == null || item.Product.Id != productId) continue;
                if ((item.Option == null ? null : item.Option.Id) != optionId) continue;
                row.Selected = true;
                grid.CurrentCell = row.Cells["Name"];
                return;
            }
        }

        private void RegisterEntry()
        {
            var item = SelectedItem();
            var selected = item == null ? null : item.Product;
            if (selected == null) { MessageBox.Show("Seleccione primero un producto.", "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var displayName = item.IsOption ? selected.Name + " - " + item.Option.Name : selected.Name;
            var currentStock = item.IsOption ? item.Option.Stock : selected.Stock;
            using (var dialog = new InventoryEntryDialog(displayName, currentStock))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (item.IsOption)
                    AppData.AddInventoryEntry(selected.Id, item.Option.Id, dialog.Quantity, dialog.Note);
                else
                    AppData.AddInventoryEntry(selected.Id, dialog.Quantity, dialog.Note);
                RefreshView();
                SelectItem(selected.Id, item.IsOption ? item.Option.Id : null);
            }
        }

        private void DeleteSelected()
        {
            var item = SelectedItem();
            if (item == null) return;
            if (item.IsOption)
            {
                if (MessageBox.Show("¿Quitar la variante “" + item.Option.Name + "” de " + item.Product.Name + "?", "Confirmar variante", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                var products = AppData.Products;
                var product = products.FirstOrDefault(x => x.Id == item.Product.Id);
                if (product == null || product.Options == null) return;
                product.Options.RemoveAll(x => x.Id == item.Option.Id);
                if (product.Options.Count > 0) product.Price = product.Options.Min(x => x.Price);
                AppData.SaveProducts(products);
                RefreshView();
                SelectProduct(product.Id);
                return;
            }
            var selected = item.Product;
            if (MessageBox.Show("¿Desea eliminar “" + selected.Name + "”?", "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { AppData.DeleteProduct(selected.Id); }
            catch (InvalidOperationException ex) { MessageBox.Show(ex.Message, "No se puede eliminar", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
    }

    public sealed class InventoryEntryDialog : Form
    {
        private readonly NumericUpDown quantity;
        private readonly TextBox note;
        public int Quantity { get { return (int)quantity.Value; } }
        public string Note { get { return note.Text.Trim(); } }

        public InventoryEntryDialog(Product product)
            : this(product.Name, product.Stock)
        {
        }

        public InventoryEntryDialog(string displayName, int currentStock)
        {
            Text = "Registrar entrada";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(480, 315);
            MaximizeBox = false; MinimizeBox = false; BackColor = Color.White; Font = Theme.Body;
            var title = Theme.Label(displayName, 16f, FontStyle.Bold); title.SetBounds(28, 22, 420, 34); title.AutoSize = false;
            var current = Theme.Label("Saldo actual: " + currentStock, 11f, FontStyle.Regular, Theme.GreenDark); current.SetBounds(28, 60, 420, 28); current.AutoSize = false;
            var qLabel = Theme.Label("Cantidad que ingresa", 10f, FontStyle.Bold); qLabel.Location = new Point(28, 104);
            quantity = new NumericUpDown { Location = new Point(28, 132), Size = new Size(424, 40), Font = new Font("Segoe UI", 14f), Minimum = 1, Maximum = 1000000, Value = 1, ThousandsSeparator = true };
            var nLabel = Theme.Label("Comentario (opcional)", 10f, FontStyle.Bold); nLabel.Location = new Point(28, 184);
            note = new TextBox { Location = new Point(28, 212), Size = new Size(424, 34), Font = Theme.Body, MaxLength = 120 };
            var cancel = Theme.Button("CANCELAR"); cancel.SetBounds(202, 260, 118, 44); cancel.DialogResult = DialogResult.Cancel;
            var save = Theme.Button("GUARDAR", true); save.SetBounds(330, 260, 122, 44); save.DialogResult = DialogResult.OK;
            Controls.Add(title); Controls.Add(current); Controls.Add(qLabel); Controls.Add(quantity); Controls.Add(nLabel); Controls.Add(note); Controls.Add(cancel); Controls.Add(save);
            AcceptButton = save; CancelButton = cancel;
            Theme.FitDialogToWorkingArea(this);
        }
    }

    public sealed class ProductOptionEditorDialog : Form
    {
        private readonly TextBox name;
        private readonly NumericUpDown price;
        private readonly string optionId;
        public ProductOption Option { get; private set; }

        public ProductOptionEditorDialog(string productName, ProductOption existing)
        {
            optionId = existing == null || string.IsNullOrWhiteSpace(existing.Id)
                ? Guid.NewGuid().ToString("N")
                : existing.Id;
            Text = existing == null ? "Nueva variante" : "Editar variante";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(520, 350);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = Theme.Body;

            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Theme.Green, Padding = new Padding(24, 10, 24, 10) };
            header.Controls.Add(new Label
            {
                Text = productName,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            Controls.Add(header);

            var nameLabel = Theme.Label("Nombre de la variante", 10f, FontStyle.Bold);
            nameLabel.Location = new Point(30, 100);
            name = new TextBox
            {
                Location = new Point(30, 130),
                Size = new Size(460, 36),
                Font = new Font("Segoe UI", 11f),
                MaxLength = 80,
                Text = existing == null ? "" : existing.Name
            };
            var priceLabel = Theme.Label("Precio", 10f, FontStyle.Bold);
            priceLabel.Location = new Point(30, 190);
            price = new NumericUpDown
            {
                Location = new Point(30, 220),
                Size = new Size(220, 40),
                Font = new Font("Segoe UI", 13f),
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 1000000,
                ThousandsSeparator = true,
                Value = existing == null ? 0 : existing.Price
            };
            var cancel = Theme.Button("CANCELAR");
            cancel.SetBounds(174, 286, 150, 48);
            cancel.DialogResult = DialogResult.Cancel;
            var save = Theme.Button("GUARDAR", true);
            save.SetBounds(336, 286, 154, 48);
            save.Click += Save_Click;
            Controls.Add(nameLabel);
            Controls.Add(name);
            Controls.Add(priceLabel);
            Controls.Add(price);
            Controls.Add(cancel);
            Controls.Add(save);
            CancelButton = cancel;
            AcceptButton = save;
            Shown += (s, e) => { name.SelectAll(); name.Focus(); };
            Theme.FitDialogToWorkingArea(this);
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if (!Validation.IsValidRequiredText(name.Text, 2, 80))
            {
                MessageBox.Show("El nombre debe tener entre 2 y 80 caracteres.", "Variante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                name.Focus();
                return;
            }
            Option = new ProductOption
            {
                Id = optionId,
                Name = name.Text.Trim().ToUpperInvariant(),
                Price = price.Value
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public sealed class ProductEditorDialog : Form
    {
        private readonly TextBox name;
        private readonly ComboBox category;
        private readonly NumericUpDown price;
        private readonly PictureBox preview;
        private readonly Panel colorSwatch;
        private string imageFile;
        private string selectedSource;
        private Color selectedColor;
        private readonly bool existingActive;
        private readonly bool existingTrackInventory;
        private readonly int existingStock;
        private readonly int existingInitialStock;

        public Product Product { get; private set; }

        public ProductEditorDialog(Product existing, IEnumerable<string> categories)
        {
            Text = existing == null ? "Nuevo artículo" : "Editar artículo";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(650, 500);
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = Theme.Body;
            existingActive = existing == null || existing.Active;
            existingTrackInventory = existing == null || existing.TrackInventory;
            existingStock = existing == null ? 0 : existing.Stock;
            existingInitialStock = existing == null ? 0 : existing.InitialStock;
            imageFile = existing == null ? null : existing.ImageFile;
            selectedColor = Theme.ParseColor(existing == null ? "#25AE4A" : existing.ColorHex, Theme.Green);

            var left = new Panel { Location = new Point(0, 0), Size = new Size(238, 500), BackColor = Theme.Canvas, Padding = new Padding(24) };
            preview = new PictureBox
            {
                Location = new Point(24, 32), Size = new Size(190, 190), BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, Padding = new Padding(18), Image = AppData.LoadImage(imageFile)
            };
            var choose = Theme.Button("ELEGIR IMAGEN"); choose.SetBounds(24, 240, 190, 48); choose.Click += ChooseImage;
            colorSwatch = new Panel { Location = new Point(24, 322), Size = new Size(190, 48), BackColor = selectedColor, Cursor = Cursors.Hand };
            colorSwatch.Click += ChooseColor;
            var colorLabel = Theme.Label("Color del mosaico", 9f, FontStyle.Regular, Theme.Muted); colorLabel.Location = new Point(24, 298);
            left.Controls.Add(preview); left.Controls.Add(choose); left.Controls.Add(colorLabel); left.Controls.Add(colorSwatch);
            Controls.Add(left);

            var nameLabel = Theme.Label("Nombre del artículo", 9.5f, FontStyle.Bold); nameLabel.Location = new Point(270, 36);
            name = new TextBox { Location = new Point(270, 65), Size = new Size(344, 35), Font = Theme.Body, Text = existing == null ? "" : existing.Name };
            var categoryLabel = Theme.Label("Categoría", 9.5f, FontStyle.Bold); categoryLabel.Location = new Point(270, 124);
            category = new ComboBox { Location = new Point(270, 153), Size = new Size(344, 35), Font = Theme.Body, DropDownStyle = ComboBoxStyle.DropDown };
            foreach (var item in categories.OrderBy(x => x)) category.Items.Add(item);
            category.Text = existing == null ? (category.Items.Count > 0 ? category.Items[0].ToString() : "GENERAL") : existing.Category;
            var priceLabel = Theme.Label("Precio", 9.5f, FontStyle.Bold); priceLabel.Location = new Point(270, 214);
            price = new NumericUpDown
            {
                Location = new Point(270, 243), Size = new Size(344, 38), Font = new Font("Segoe UI", 14f),
                DecimalPlaces = 2, Maximum = 1000000, Minimum = 0, ThousandsSeparator = true,
                Value = existing == null ? 0 : existing.Price
            };
            var hint = Theme.Label("La imagen elegida se copia a la carpeta local de datos del programa.", 9f, FontStyle.Regular, Theme.Muted);
            hint.SetBounds(270, 302, 344, 46); hint.AutoSize = false;
            var cancel = Theme.Button("CANCELAR"); cancel.SetBounds(270, 407, 156, 52); cancel.DialogResult = DialogResult.Cancel;
            var save = Theme.Button("GUARDAR", true); save.SetBounds(438, 407, 176, 52); save.Click += Save_Click;
            Controls.Add(nameLabel); Controls.Add(name); Controls.Add(categoryLabel); Controls.Add(category); Controls.Add(priceLabel); Controls.Add(price);
            Controls.Add(hint); Controls.Add(cancel); Controls.Add(save);
            CancelButton = cancel;
            AcceptButton = save;
            Theme.FitDialogToWorkingArea(this);
        }

        private void ChooseImage(object sender, EventArgs e)
        {
            using (var open = new OpenFileDialog { Filter = "Imágenes (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) return;
                selectedSource = open.FileName;
                if (preview.Image != null) preview.Image.Dispose();
                preview.Image = AppData.LoadImage(open.FileName);
            }
        }

        private void ChooseColor(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog { Color = selectedColor, FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                selectedColor = dialog.Color;
                colorSwatch.BackColor = selectedColor;
            }
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if (!Validation.IsValidRequiredText(name.Text, 2, 60))
            {
                MessageBox.Show("El nombre debe tener entre 2 y 60 caracteres.", "Validación del artículo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                name.Focus();
                return;
            }
            if (!Validation.IsValidRequiredText(category.Text, 2, 40))
            {
                MessageBox.Show("La categoría debe tener entre 2 y 40 caracteres.", "Validación del artículo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                category.Focus();
                return;
            }
            if (!string.IsNullOrWhiteSpace(selectedSource))
            {
                var folder = Path.Combine(AppData.DataDirectory, "Images");
                Directory.CreateDirectory(folder);
                var destination = Path.Combine(folder, Guid.NewGuid().ToString("N") + Path.GetExtension(selectedSource).ToLowerInvariant());
                File.Copy(selectedSource, destination, true);
                imageFile = "data:" + Path.GetFileName(destination);
            }
            Product = new Product
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name.Text.Trim().ToUpperInvariant(),
                Category = category.Text.Trim().ToUpperInvariant(),
                Price = price.Value,
                ImageFile = imageFile,
                ColorHex = ColorTranslator.ToHtml(selectedColor),
                Active = existingActive,
                TrackInventory = existingTrackInventory,
                Stock = existingStock,
                InitialStock = existingInitialStock
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
