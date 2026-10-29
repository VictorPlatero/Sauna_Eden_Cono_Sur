using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class ShiftView : UserControl, IRefreshable
    {
        private readonly Panel body;

        public ShiftView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Canvas;
            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Green, Padding = new Padding(22, 12, 18, 12) };
            top.Controls.Add(new Label
            {
                Text = "Turno",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            Controls.Add(top);
            body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(36), AutoScroll = true };
            Controls.Add(body);
            body.BringToFront();
            RefreshView();
        }

        public void RefreshView()
        {
            if (body == null) return;
            body.SuspendLayout();
            foreach (Control control in body.Controls) control.Dispose();
            body.Controls.Clear();
            var shift = AppData.OpenShift;
            if (shift == null) RenderClosed();
            else RenderOpen(shift);
            body.ResumeLayout();
        }

        private void RenderClosed()
        {
            var card = new CardPanel { Width = 640, Height = 390, BackColor = Color.White };
            CenterCard(card);
            card.Controls.Add(new Label
            {
                Text = "◷",
                Location = new Point(270, 42),
                Size = new Size(100, 86),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Green,
                Font = new Font("Segoe UI", 44f)
            });
            card.Controls.Add(new Label
            {
                Text = "No hay un turno abierto",
                Location = new Point(40, 142),
                Size = new Size(560, 45),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Ink,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold)
            });
            card.Controls.Add(new Label
            {
                Text = "Abra la caja para registrar ventas en efectivo y movimientos de tesorería.",
                Location = new Point(80, 192),
                Size = new Size(480, 58),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Muted,
                Font = Theme.Body
            });
            var open = Theme.Button("ABRIR TURNO", true);
            open.SetBounds(190, 282, 260, 56);
            open.Click += OpenShift_Click;
            card.Controls.Add(open);
            body.Controls.Add(card);
        }

        private void RenderOpen(Shift shift)
        {
            var card = new CardPanel { Width = 850, Height = 540, BackColor = Color.White };
            CenterCard(card);

            var treasury = Theme.Button("GESTIÓN DE TESORERÍA");
            treasury.SetBounds(32, 28, 370, 56);
            treasury.ForeColor = Theme.GreenDark;
            treasury.FlatAppearance.BorderColor = Theme.Green;
            treasury.Click += (s, e) => { using (var dialog = new TreasuryDialog(shift)) dialog.ShowDialog(FindForm()); RefreshView(); };
            var close = Theme.Button("CERRAR EL TURNO");
            close.SetBounds(448, 28, 370, 56);
            close.Click += (s, e) => CloseShift(shift);
            card.Controls.Add(treasury);
            card.Controls.Add(close);

            var number = Theme.Label("Número de cierre de caja: " + shift.Number, 12f, FontStyle.Bold);
            number.SetBounds(34, 116, 430, 32); number.AutoSize = false;
            var opened = Theme.Label("Abierto: " + shift.Employee, 11f, FontStyle.Regular);
            opened.SetBounds(34, 154, 370, 28); opened.AutoSize = false;
            var when = Theme.Label(shift.OpenedAt.ToString("dd/MM/yyyy HH:mm"), 11f, FontStyle.Regular, Theme.Muted);
            when.SetBounds(540, 154, 278, 28); when.AutoSize = false; when.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(number); card.Controls.Add(opened); card.Controls.Add(when);
            card.Controls.Add(new Panel { Location = new Point(32, 198), Size = new Size(786, 1), BackColor = Theme.Border });

            var expected = AppData.CalculateExpectedCash(shift);
            var movementTotal = AppData.CashMovements.Where(x => x.ShiftId == shift.Id).Sum(x => x.Amount);
            var cashSales = AppData.Receipts.Where(x => x.CreatedAt >= shift.OpenedAt && x.PaymentMethod == "Efectivo" && x.Status == "Pagado").Sum(x => x.Total);
            AddMetric(card, "Fondo de caja anterior", shift.OpeningAmount, 230);
            AddMetric(card, "Ventas en efectivo", cashSales, 286);
            AddMetric(card, "Depósitos / salidas", movementTotal, 342);
            card.Controls.Add(new Panel { Location = new Point(32, 406), Size = new Size(786, 1), BackColor = Theme.Border });
            var drawer = Theme.Label("EFECTIVO ESPERADO", 10f, FontStyle.Bold, Theme.GreenDark);
            drawer.SetBounds(34, 430, 350, 35); drawer.AutoSize = false;
            var drawerValue = Theme.Label(Theme.Money(expected), 19f, FontStyle.Bold);
            drawerValue.SetBounds(500, 420, 318, 50); drawerValue.AutoSize = false; drawerValue.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(drawer); card.Controls.Add(drawerValue);

            var note = Theme.Label("Los pagos por Yape quedan en los recibos, pero no aumentan el cajón de efectivo.", 9f, FontStyle.Regular, Theme.Muted);
            note.SetBounds(34, 486, 770, 30); note.AutoSize = false;
            card.Controls.Add(note);
            body.Controls.Add(card);
        }

        private void AddMetric(Control parent, string title, decimal amount, int top)
        {
            var label = Theme.Label(title, 11f, FontStyle.Regular);
            label.SetBounds(34, top, 430, 34); label.AutoSize = false;
            var value = Theme.Label(Theme.Money(amount), 11f, FontStyle.Bold);
            value.SetBounds(540, top, 278, 34); value.AutoSize = false; value.TextAlign = ContentAlignment.MiddleRight;
            parent.Controls.Add(label); parent.Controls.Add(value);
        }

        private void CenterCard(Control card)
        {
            card.Left = Math.Max(body.Padding.Left, (body.ClientSize.Width - card.Width) / 2);
            card.Top = 22;
            body.Resize += (s, e) => card.Left = Math.Max(body.Padding.Left, (body.ClientSize.Width - card.Width) / 2);
        }

        private void OpenShift_Click(object sender, EventArgs e)
        {
            decimal amount;
            if (!PromptDialog.AskMoney(FindForm(), "Abrir turno", "Fondo inicial de caja", out amount)) return;
            AppData.OpenNewShift(amount);
            RefreshView();
        }

        private void CloseShift(Shift shift)
        {
            var expected = AppData.CalculateExpectedCash(shift);
            decimal counted;
            if (!PromptDialog.AskMoney(FindForm(), "Cerrar turno", "Efectivo contado (esperado " + Theme.Money(expected) + ")", out counted, expected)) return;
            var difference = counted - expected;
            var confirm = MessageBox.Show(
                "Efectivo esperado: " + Theme.Money(expected) + "\n" +
                "Efectivo contado: " + Theme.Money(counted) + "\n" +
                "Diferencia: " + Theme.Money(difference) + "\n\n¿Cerrar el turno?",
                "Resumen de cierre",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            AppData.CloseShift(shift.Id, counted);
            RefreshView();
        }
    }

    public sealed class TreasuryDialog : Form
    {
        private readonly Shift shift;
        private readonly NumericUpDown amount;
        private readonly TextBox comment;
        private readonly DataGridView grid;
        private readonly Label balance;

        public TreasuryDialog(Shift currentShift)
        {
            shift = currentShift;
            Text = "Gestión de tesorería";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 620);
            MinimumSize = new Size(760, 560);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Canvas;
            Font = Theme.Body;

            var top = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Theme.Green, Padding = new Padding(22, 10, 22, 10) };
            top.Controls.Add(new Label
            {
                Text = "Gestión de tesorería",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            Controls.Add(top);

            var entry = new Panel { Dock = DockStyle.Top, Height = 206, BackColor = Color.White, Padding = new Padding(28, 16, 28, 16) };
            Controls.Add(entry);
            entry.BringToFront();
            var amountLabel = Theme.Label("Cantidad", 9.5f, FontStyle.Regular, Theme.Muted);
            amountLabel.Location = new Point(28, 16);
            amount = new NumericUpDown
            {
                Location = new Point(28, 42), Size = new Size(764, 38), Font = new Font("Segoe UI", 15f),
                DecimalPlaces = 2, Maximum = 1000000, Minimum = 0, ThousandsSeparator = true
            };
            var commentLabel = Theme.Label("Comentario", 9.5f, FontStyle.Regular, Theme.Muted);
            commentLabel.Location = new Point(28, 86);
            comment = new TextBox { Location = new Point(28, 112), Size = new Size(764, 34), Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle };
            var deposit = Theme.Button("DEPOSITAR");
            deposit.SetBounds(28, 156, 370, 42);
            deposit.ForeColor = Theme.GreenDark;
            deposit.Click += (s, e) => SaveMovement("Depósito");
            var withdrawal = Theme.Button("PAGOS / SALIDAS");
            withdrawal.SetBounds(422, 156, 370, 42);
            withdrawal.Click += (s, e) => SaveMovement("Salida");
            entry.Resize += (s, e) =>
            {
                amount.Width = entry.ClientSize.Width - 56;
                comment.Width = entry.ClientSize.Width - 56;
                var width = (entry.ClientSize.Width - 82) / 2;
                deposit.Width = width;
                withdrawal.Left = deposit.Right + 24;
                withdrawal.Width = width;
            };
            entry.Controls.Add(amountLabel); entry.Controls.Add(amount); entry.Controls.Add(commentLabel); entry.Controls.Add(comment); entry.Controls.Add(deposit); entry.Controls.Add(withdrawal);

            var listHeader = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Theme.Canvas, Padding = new Padding(28, 12, 28, 4) };
            listHeader.Controls.Add(new Label
            {
                Text = "Depósitos / Salidas",
                Dock = DockStyle.Left,
                Width = 280,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                ForeColor = Theme.GreenDark,
                TextAlign = ContentAlignment.MiddleLeft
            });
            balance = new Label
            {
                Dock = DockStyle.Right,
                Width = 260,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                TextAlign = ContentAlignment.MiddleRight
            };
            listHeader.Controls.Add(balance);
            Controls.Add(listHeader);
            listHeader.BringToFront();

            grid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Hora", Width = 72, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Comment", HeaderText = "Empleado / comentario", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Importe", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            Controls.Add(grid);
            grid.BringToFront();
            LoadMovements();
        }

        private void SaveMovement(string type)
        {
            if (amount.Value <= 0)
            {
                MessageBox.Show("Ingrese una cantidad mayor que cero.", "Tesorería", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Validation.IsValidRequiredText(comment.Text, 3, 160))
            {
                MessageBox.Show("Ingrese un comentario de 3 a 160 caracteres para justificar el movimiento.", "Tesorería", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                comment.Focus();
                return;
            }
            AppData.AddCashMovement(type, amount.Value, comment.Text);
            amount.Value = 0;
            comment.Clear();
            LoadMovements();
        }

        private void LoadMovements()
        {
            var movements = AppData.CashMovements.Where(x => x.ShiftId == shift.Id).OrderByDescending(x => x.CreatedAt).ToList();
            grid.Rows.Clear();
            foreach (var item in movements)
            {
                var row = grid.Rows.Add(item.CreatedAt.ToString("HH:mm"), item.Employee + " - " + item.Comment, Theme.Money(item.Amount));
                grid.Rows[row].DefaultCellStyle.ForeColor = item.Amount < 0 ? Color.FromArgb(190, 75, 65) : Theme.Ink;
            }
            balance.Text = "Neto: " + Theme.Money(movements.Sum(x => x.Amount));
        }
    }
}
