using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class ProductOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int InitialStock { get; set; }
        public bool TrackInventory { get; set; }
    }

    public static class ProductOptionCatalog
    {
        private static readonly Dictionary<string, List<ProductOption>> Templates =
            new Dictionary<string, List<ProductOption>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "GASEOSAS", NewOptions(
                        "INCA COLA / 600 ML", 4m,
                        "INCA COLA / 2.5 L", 13m,
                        "COCA COLA / 600 ML", 4m,
                        "COCA COLA / 2.5 L", 13m)
                },
                {
                    "HIDRATANTE", NewOptions(
                        "CIELO / 625 ML", 1.50m,
                        "CIELO / 1 L", 3m,
                        "CIELO / 2.5 L", 5m,
                        "VOLT / 300 ML", 4m,
                        "SPORADE / 1 L", 5m,
                        "SPORADE / 500 ML", 3.50m)
                },
                {
                    "JARRAS", NewOptions(
                        "MARACUYÁ / 1.5 L", 7m,
                        "MARACUYÁ / 2 L", 9m,
                        "LIMONADA / 1.5 L", 7m,
                        "LIMONADA / 2 L", 9m,
                        "LIMONADA / 1 L", 5m,
                        "PIÑA / 1.5 L", 10m,
                        "PIÑA / 2 L", 13m)
                },
                {
                    "SANDWICHES", NewOptions(
                        "TOSTADA DE QUESO", 2.50m,
                        "SÁNDWICH DE POLLO", 5m,
                        "CHORIPÁN", 6m)
                },
                {
                    "SNACKS", NewOptions(
                        "PAPRIKA", 2m,
                        "TENTACIÓN", 1.50m,
                        "NIK", 1.50m,
                        "OREO", 1.50m,
                        "RITZ", 1.50m)
                },
                {
                    "ACONDICIONADOR", NewOptions(
                        "BONAWELL", 2m,
                        "NUTRIBELA", 2m)
                },
                {
                    "JABONES", NewOptions(
                        "HOTELERO", 1m,
                        "FRUTAL", 2.50m,
                        "REXONA", 4.50m,
                        "PALMOLIVE", 4.50m)
                },
                {
                    "SHAMPOOS", NewOptions(
                        "H&S", 2m,
                        "NUTRIBELA", 2m,
                        "PANTENE", 2m,
                        "SEDAL", 2m,
                        "AMARAS", 2m,
                        "H&S MINI", 1m)
                }
            };

        public static bool HasOptions(Product product)
        {
            return product != null &&
                   (product.Options != null ? product.Options.Count > 0 : Templates.ContainsKey(product.Name ?? ""));
        }

        public static IReadOnlyList<ProductOption> GetOptions(Product product)
        {
            if (product == null) return new List<ProductOption>();
            if (product.Options != null) return product.Options;
            List<ProductOption> options;
            return Templates.TryGetValue(product.Name ?? "", out options) ? options : new List<ProductOption>();
        }

        public static bool HasTemplate(string productName)
        {
            return Templates.ContainsKey(productName ?? "");
        }

        public static List<ProductOption> CreateDefaultOptions(string productName)
        {
            List<ProductOption> templates;
            if (!Templates.TryGetValue(productName ?? "", out templates)) return null;
            return templates.Select(option => new ProductOption
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = option.Name,
                Price = option.Price
            }).ToList();
        }

        private static List<ProductOption> NewOptions(params object[] values)
        {
            var result = new List<ProductOption>();
            for (var index = 0; index + 1 < values.Length; index += 2)
                result.Add(new ProductOption { Name = (string)values[index], Price = (decimal)values[index + 1] });
            return result;
        }
    }

    public static class OccupancyCalculator
    {
        public static void Count(IEnumerable<ReceiptLine> lines, out int adults, out int children)
        {
            adults = 0;
            children = 0;
            foreach (var line in lines ?? Enumerable.Empty<ReceiptLine>())
            {
                var name = (line.Name ?? "").ToUpperInvariant();
                var quantity = Math.Max(0, line.Quantity);
                if (name.Contains("NIÑO"))
                {
                    children += quantity;
                    continue;
                }
                if (name.Contains("FAMILIA 1"))
                {
                    adults += quantity * 2;
                    children += quantity;
                }
                else if (name.Contains("FAMILIA 2"))
                {
                    adults += quantity * 2;
                    children += quantity * 2;
                }
                else if (name.Contains("AMIGOS X5")) adults += quantity * 5;
                else if (name.Contains("AMIGOS X4") || name.Contains("MUJER 4")) adults += quantity * 4;
                else if (name.Contains("AMIGOS X3") || name.Contains("MUJER 3")) adults += quantity * 3;
                else if (name.Contains("PAREJA") || name.Contains("MUJER 2")) adults += quantity * 2;
                else if (name.Contains("GENERAL") || name.Contains("ADULTO MAYOR") ||
                         name.Contains("VIP") || name.Contains("PASE CORTESÍA"))
                    adults += quantity;
            }
        }
    }

    public sealed class ProductOptionsDialog : Form
    {
        private readonly Product product;
        private readonly FlowLayoutPanel optionList;
        private readonly NumericUpDown quantity;
        private readonly TextBox comment;
        private readonly Button save;
        private readonly Dictionary<Control, ProductOption> optionRows = new Dictionary<Control, ProductOption>();
        private ProductOption selected;

        public ProductOption SelectedOption { get { return selected; } }
        public int SelectedQuantity { get { return (int)quantity.Value; } }
        public string SelectedComment { get { return (comment.Text ?? "").Trim(); } }

        public ProductOptionsDialog(Product item)
        {
            product = item ?? throw new ArgumentNullException(nameof(item));
            Text = "Elegir " + product.Name;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(720, 650);
            MinimumSize = new Size(520, 480);
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Canvas;
            Font = Theme.Body;

            var head = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.Green, Padding = new Padding(24, 10, 24, 10) };
            head.Controls.Add(new Label
            {
                Text = product.Name,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Color.White, Padding = new Padding(24, 14, 24, 14) };
            var quantityLabel = Theme.Label("Cantidad", 10f, FontStyle.Bold, Theme.GreenDark);
            quantityLabel.SetBounds(24, 14, 100, 24);
            quantity = new NumericUpDown
            {
                Location = new Point(24, 42),
                Size = new Size(112, 36),
                Minimum = 1,
                Maximum = 999,
                Value = 1,
                Font = new Font("Segoe UI", 12f),
                TextAlign = HorizontalAlignment.Center
            };
            var commentLabel = Theme.Label("Comentario opcional", 10f, FontStyle.Bold, Theme.GreenDark);
            commentLabel.SetBounds(158, 14, 180, 24);
            comment = new TextBox
            {
                Location = new Point(158, 42),
                Size = new Size(538, 36),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 120
            };
            var cancel = Theme.Button("CANCELAR");
            cancel.SetBounds(368, 116, 150, 54);
            cancel.DialogResult = DialogResult.Cancel;
            save = Theme.Button("AGREGAR AL TICKET", true);
            save.SetBounds(530, 116, 166, 54);
            save.Enabled = false;
            save.Click += (s, e) =>
            {
                if (selected == null) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            footer.Controls.Add(quantityLabel);
            footer.Controls.Add(quantity);
            footer.Controls.Add(commentLabel);
            footer.Controls.Add(comment);
            footer.Controls.Add(cancel);
            footer.Controls.Add(save);
            footer.Resize += (s, e) =>
            {
                comment.Width = Math.Max(180, footer.ClientSize.Width - comment.Left - 24);
                save.Left = footer.ClientSize.Width - save.Width - 24;
                cancel.Left = save.Left - cancel.Width - 12;
            };

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 16, 22, 16), BackColor = Theme.Canvas };
            optionList = new BufferedFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(2),
                BackColor = Theme.Canvas
            };
            optionList.SizeChanged += (s, e) => ResizeOptionRows();
            body.Controls.Add(optionList);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(head);
            foreach (var option in ProductOptionCatalog.GetOptions(product)) AddOptionRow(option);
            ResizeOptionRows();
            CancelButton = cancel;
            AcceptButton = save;
            Theme.FitDialogToWorkingArea(this);
        }

        private void AddOptionRow(ProductOption option)
        {
            var row = new CardPanel
            {
                Height = 66,
                Width = 630,
                Margin = new Padding(4, 4, 4, 8),
                Cursor = Cursors.Hand,
                BackColor = Color.White,
                Tag = option
            };
            var price = new Label
            {
                Text = Theme.Money(option.Price),
                Dock = DockStyle.Right,
                Width = 130,
                ForeColor = Theme.Ink,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 18, 0),
                Cursor = Cursors.Hand
            };
            var name = new Label
            {
                Text = option.Name,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Ink,
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            row.Controls.Add(name);
            row.Controls.Add(price);
            EventHandler choose = (s, e) => SelectOption(row, option);
            row.Click += choose;
            name.Click += choose;
            price.Click += choose;
            optionRows[row] = option;
            optionList.Controls.Add(row);
        }

        private void SelectOption(Control row, ProductOption option)
        {
            selected = option;
            foreach (var item in optionRows)
            {
                var selectedRow = item.Key == row;
                item.Key.BackColor = selectedRow ? Theme.GreenSoft : Color.White;
                var card = item.Key as CardPanel;
                if (card != null) card.BorderColor = selectedRow ? Theme.Green : Theme.Border;
                foreach (Label label in item.Key.Controls.OfType<Label>())
                    label.ForeColor = selectedRow ? Theme.GreenDark : Theme.Ink;
                item.Key.Invalidate();
            }
            save.Enabled = true;
            save.Text = "AGREGAR  " + Theme.Money(option.Price);
        }

        private void ResizeOptionRows()
        {
            var width = Math.Max(300, optionList.ClientSize.Width - optionList.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 12);
            foreach (Control row in optionRows.Keys) row.Width = width;
        }
    }
}
