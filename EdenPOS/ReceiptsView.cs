using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class ReceiptsView : UserControl, IRefreshable
    {
        private readonly TextBox search;
        private readonly DataGridView receiptGrid;
        private readonly Panel detailHost;
        private readonly Button paymentSummaryButton;
        private Receipt selected;

        public ReceiptsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Canvas;
            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Green, Padding = new Padding(22, 12, 18, 12) };
            var title = new Label
            {
                Text = "Recibos",
                Dock = DockStyle.Left,
                Width = 260,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            search = new TextBox { Dock = DockStyle.Right, Width = 280, Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle };
            search.TextChanged += (s, e) => LoadReceipts();
            var searchLabel = new Label
            {
                Text = "Buscar",
                Dock = DockStyle.Right,
                Width = 64,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 10f)
            };
            top.Controls.Add(search);
            top.Controls.Add(searchLabel);
            top.Controls.Add(title);
            var summary = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18, 9, 18, 9), WrapContents = false };
            paymentSummaryButton = Theme.Button("RESUMEN DE HOY", true); paymentSummaryButton.Size = new Size(390, 50);
            paymentSummaryButton.Click += (s, e) => { using (var dialog = new PaymentDateSummaryDialog(DateTime.Today)) dialog.ShowDialog(FindForm()); UpdatePaymentTotals(); };
            summary.Controls.Add(paymentSummaryButton);

            var chrome = new TableLayoutPanel { Dock = DockStyle.Top, Height = 136, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
            chrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            chrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f)); chrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
            top.Dock = DockStyle.Fill; chrome.Controls.Add(top, 0, 0); chrome.Controls.Add(summary, 0, 1); Controls.Add(chrome);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 1,
                BackColor = Theme.Border
            };
            var adjustingSplit = false;
            split.SizeChanged += (s, e) =>
            {
                if (adjustingSplit || split.Width < 620) return;
                adjustingSplit = true;
                try
                {
                    var scale = Math.Max(1f, DeviceDpi / 96f);
                    var listWidth = Math.Max((int)Math.Round(280 * scale), Math.Min((int)Math.Round(520 * scale), (int)(split.Width * 0.34)));
                    if (listWidth < split.Width) split.SplitterDistance = listWidth;
                }
                finally { adjustingSplit = false; }
            };
            Controls.Add(split);
            split.BringToFront();

            receiptGrid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(receiptGrid);
            receiptGrid.ColumnHeadersVisible = false;
            receiptGrid.RowTemplate.Height = 62;
            receiptGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", Width = 42, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            receiptGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Info", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = { WrapMode = DataGridViewTriState.True } });
            receiptGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Number", Width = 92, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            receiptGrid.SelectionChanged += (s, e) => SelectCurrent();
            split.Panel1.Controls.Add(receiptGrid);

            detailHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(42, 28, 42, 28), AutoScroll = true };
            split.Panel2.Controls.Add(detailHost);
            RefreshView();
        }

        public void RefreshView()
        {
            LoadReceipts();
        }

        private void LoadReceipts()
        {
            if (receiptGrid == null) return;
            UpdatePaymentTotals();
            var previousId = selected == null ? null : selected.Id;
            var term = (search.Text ?? "").Trim();
            var receipts = AppData.Receipts.AsEnumerable();
            if (term.Length > 0)
            {
                receipts = receipts.Where(x =>
                    x.Number.ToString().Contains(term) ||
                    (x.CustomerDni ?? "").Contains(term) ||
                    (x.CustomerName ?? "").IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (x.PaymentMethod ?? "").IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }

            receiptGrid.Rows.Clear();
            DateTime? currentDate = null;
            foreach (var receipt in receipts.OrderByDescending(x => x.CreatedAt))
            {
                if (currentDate == null || currentDate.Value.Date != receipt.CreatedAt.Date)
                {
                    currentDate = receipt.CreatedAt.Date;
                    var dayTitle = currentDate.Value.ToString("dddd dd/MM/yyyy", CultureInfo.GetCultureInfo("es-PE")).ToUpperInvariant();
                    var headerIndex = receiptGrid.Rows.Add("", dayTitle, "");
                    var header = receiptGrid.Rows[headerIndex];
                    header.Tag = null; header.Height = 40; header.DefaultCellStyle.BackColor = Theme.GreenSoft;
                    header.DefaultCellStyle.ForeColor = Theme.GreenDark; header.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
                    header.DefaultCellStyle.SelectionBackColor = Theme.GreenSoft; header.DefaultCellStyle.SelectionForeColor = Theme.GreenDark;
                }
                var state = receipt.Status == "Reembolsado" ? "↩" : receipt.PaymentMethod == "Efectivo" ? "▣" : "▤";
                var info = Theme.Money(receipt.Total) + "\n" + receipt.CreatedAt.ToString("dd/MM/yyyy  HH:mm");
                var index = receiptGrid.Rows.Add(state, info, "#2-" + receipt.Number);
                receiptGrid.Rows[index].Tag = receipt;
                if (receipt.Status == "Reembolsado")
                    receiptGrid.Rows[index].DefaultCellStyle.ForeColor = Theme.Muted;
                if (receipt.Id == previousId) receiptGrid.Rows[index].Selected = true;
            }

            var firstReceiptRow = receiptGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(x => x.Tag is Receipt);
            if (firstReceiptRow != null && !receiptGrid.SelectedRows.Cast<DataGridViewRow>().Any(x => x.Tag is Receipt))
            { firstReceiptRow.Selected = true; receiptGrid.CurrentCell = firstReceiptRow.Cells[1]; }
            if (firstReceiptRow != null) SelectCurrent();
            if (receiptGrid.Rows.Count == 0)
            {
                selected = null;
                ShowEmpty("Todavía no hay recibos.");
            }
        }

        private void SelectCurrent()
        {
            if (receiptGrid.SelectedRows.Count == 0) return;
            var candidate = receiptGrid.SelectedRows[0].Tag as Receipt;
            if (candidate == null) return;
            selected = candidate;
            RenderDetail();
        }

        private void RenderDetail()
        {
            detailHost.SuspendLayout();
            foreach (Control control in detailHost.Controls) control.Dispose();
            detailHost.Controls.Clear();
            if (selected == null) { ShowEmpty("Seleccione un recibo."); detailHost.ResumeLayout(); return; }

            var card = new CardPanel { Width = 620, Height = Math.Max(520, 355 + selected.Lines.Count * 42), BackColor = Color.White, Anchor = AnchorStyles.Top };
            card.Left = Math.Max(0, (detailHost.ClientSize.Width - card.Width - detailHost.Padding.Horizontal) / 2);
            card.Top = 0;
            detailHost.Resize += (s, e) => card.Left = Math.Max(0, (detailHost.ClientSize.Width - card.Width - detailHost.Padding.Horizontal) / 2);

            var receiptNo = Theme.Label("#2-" + selected.Number, 12f, FontStyle.Bold, Theme.GreenDark);
            receiptNo.Location = new Point(28, 24);
            var status = Theme.Label(selected.Status.ToUpperInvariant(), 9f, FontStyle.Bold, selected.Status == "Pagado" ? Theme.GreenDark : Color.FromArgb(220, 80, 68));
            status.Location = new Point(500, 27);
            status.AutoSize = false;
            status.Size = new Size(92, 26);
            status.TextAlign = ContentAlignment.MiddleRight;
            var total = Theme.Label(Theme.Money(selected.Total), 27f, FontStyle.Bold);
            total.AutoSize = false;
            total.SetBounds(28, 70, 564, 52);
            total.TextAlign = ContentAlignment.MiddleCenter;
            var totalHint = Theme.Label("Total", 10f, FontStyle.Regular, Theme.Muted);
            totalHint.AutoSize = false;
            totalHint.SetBounds(28, 120, 564, 24);
            totalHint.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(receiptNo);
            card.Controls.Add(status);
            card.Controls.Add(total);
            card.Controls.Add(totalHint);

            var meta = Theme.Label(
                "Cliente: " + selected.CustomerName + (string.IsNullOrWhiteSpace(selected.CustomerDni) ? "" : "  •  DNI " + selected.CustomerDni) + "\nEmpleado: " + selected.Employee + "\nTPV: Recepción",
                10.5f,
                FontStyle.Regular);
            meta.SetBounds(28, 164, 564, 76);
            meta.AutoSize = false;
            card.Controls.Add(meta);

            var separator = new Panel { BackColor = Theme.Border, Height = 1, Width = 564, Location = new Point(28, 249) };
            card.Controls.Add(separator);
            var y = 266;
            foreach (var line in selected.Lines)
            {
                var name = Theme.Label(line.Name, 10.5f, FontStyle.Bold);
                name.SetBounds(28, y, 370, 24);
                name.AutoSize = false;
                var qty = Theme.Label(line.Quantity + " x " + Theme.Money(line.UnitPrice), 9.2f, FontStyle.Regular, Theme.Muted);
                qty.SetBounds(28, y + 22, 370, 20);
                qty.AutoSize = false;
                var amount = Theme.Label(Theme.Money(line.LineTotal), 10.5f, FontStyle.Bold);
                amount.SetBounds(430, y + 5, 162, 28);
                amount.AutoSize = false;
                amount.TextAlign = ContentAlignment.MiddleRight;
                card.Controls.Add(name);
                card.Controls.Add(qty);
                card.Controls.Add(amount);
                y += 48;
            }

            var totalRow = new Panel { BackColor = Theme.Border, Height = 1, Width = 564, Location = new Point(28, y + 6) };
            card.Controls.Add(totalRow);
            var paymentText = selected.PaymentMethod == "Mixto" ? "Total\nEfectivo\nYape" : "Total\n" + selected.PaymentMethod;
            var amountText = selected.PaymentMethod == "Mixto" ? Theme.Money(selected.Total) + "\n" + Theme.Money(selected.CashPaid) + "\n" + Theme.Money(selected.YapePaid) : Theme.Money(selected.Total) + "\n" + Theme.Money(selected.Total);
            var paid = Theme.Label(paymentText, 10.5f, FontStyle.Bold);
            paid.SetBounds(28, y + 20, 360, selected.PaymentMethod == "Mixto" ? 76 : 52); paid.AutoSize = false;
            var paidAmount = Theme.Label(amountText, 10.5f, FontStyle.Bold);
            paidAmount.SetBounds(430, y + 20, 162, selected.PaymentMethod == "Mixto" ? 76 : 52); paidAmount.AutoSize = false; paidAmount.TextAlign = ContentAlignment.TopRight;
            card.Controls.Add(paid); card.Controls.Add(paidAmount);
            var date = Theme.Label(selected.CreatedAt.ToString("dd/MM/yyyy HH:mm") + "     #2-" + selected.Number, 9.5f, FontStyle.Regular, Theme.Muted);
            date.SetBounds(28, y + 82, 390, 28); date.AutoSize = false;
            card.Controls.Add(date);

            if (selected.Status == "Pagado")
            {
                var refund = Theme.Button("REEMBOLSAR");
                refund.SetBounds(430, y + 78, 162, 42);
                refund.Click += Refund_Click;
                card.Controls.Add(refund);
            }
            else
            {
                var reason = Theme.Label("Motivo: " + (selected.RefundReason ?? "Sin motivo"), 9f, FontStyle.Italic, Color.FromArgb(190, 75, 65));
                reason.SetBounds(28, y + 112, 564, 28); reason.AutoSize = false;
                card.Controls.Add(reason);
            }
            card.Height = y + (selected.PaymentMethod == "Mixto" ? 178 : 154);
            detailHost.Controls.Add(card);
            detailHost.ResumeLayout();
        }

        private void Refund_Click(object sender, EventArgs e)
        {
            string reason;
            if (!PromptDialog.AskText(FindForm(), "Reembolsar recibo", "Motivo del reembolso", out reason)) return;
            if (!Validation.IsValidRequiredText(reason, 3, 160))
            {
                MessageBox.Show("Ingrese un motivo de 3 a 160 caracteres.", "Validación del reembolso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var confirm = MessageBox.Show(
                "Se marcará el recibo #2-" + selected.Number + " como reembolsado por " + Theme.Money(selected.Total) + ".",
                "Confirmar reembolso",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            AppData.RefundReceipt(selected, reason);
            LoadReceipts();
        }

        private void ShowEmpty(string text)
        {
            detailHost.Controls.Clear();
            var label = Theme.Label(text, 12f, FontStyle.Regular, Theme.Muted);
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            detailHost.Controls.Add(label);
        }

        private void UpdatePaymentTotals()
        {
            var paidToday = AppData.Receipts.Where(x => x.Status == "Pagado" && x.CreatedAt.Date == DateTime.Today).ToList();
            paymentSummaryButton.Text = "HOY: EFECTIVO " + Theme.Money(paidToday.Sum(x => x.CashPaid)) + "   •   YAPE " + Theme.Money(paidToday.Sum(x => x.YapePaid));
        }
    }
}
