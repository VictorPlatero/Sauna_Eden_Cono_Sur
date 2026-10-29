using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EdenPOS
{
    public sealed class CheckoutDialog : Form
    {
        private readonly decimal total;
        private readonly NumericUpDown received;
        private readonly NumericUpDown yapeAmount;
        private readonly Label receivedLabel;
        private readonly Label yapeLabel;
        private readonly Label changeLabel;
        private readonly Button confirm;
        private readonly Button[] paymentButtons;

        public string PaymentMethod { get; private set; } = "Efectivo";
        public decimal AmountReceived { get { return received.Value; } }
        public decimal CashAmount { get { return PaymentMethod == "Mixto" ? received.Value : PaymentMethod == "Efectivo" ? total : 0m; } }
        public decimal YapeAmount { get { return PaymentMethod == "Mixto" ? yapeAmount.Value : PaymentMethod == "Yape" ? total : 0m; } }

        public CheckoutDialog(decimal totalAmount)
        {
            total = totalAmount;
            Text = "Cobrar ticket";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 600);
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Canvas;
            Font = Theme.Body;

            var head = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Theme.Green, Padding = new Padding(24, 16, 24, 12) };
            head.Controls.Add(new Label
            {
                Text = Theme.Money(total),
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 30f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            head.Controls.Add(new Label
            {
                Text = "TOTAL A COBRAR",
                Dock = DockStyle.Top,
                Height = 26,
                ForeColor = Color.FromArgb(225, 255, 232),
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            Controls.Add(head);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 18, 28, 18), BackColor = Color.White };
            Controls.Add(body);
            var methodLabel = Theme.Label("Método de pago", 10f, FontStyle.Bold);
            methodLabel.Location = new Point(28, 20);
            body.Controls.Add(methodLabel);

            var cash = PaymentButton("EFECTIVO", 28, 176);
            var yape = PaymentButton("YAPE", 222, 176);
            var mixed = PaymentButton("MIXTO", 416, 176);
            paymentButtons = new[] { cash, yape, mixed };
            body.Controls.Add(cash);
            body.Controls.Add(yape);
            body.Controls.Add(mixed);

            receivedLabel = Theme.Label("Importe recibido en efectivo", 10f, FontStyle.Bold);
            receivedLabel.Location = new Point(28, 118);
            body.Controls.Add(receivedLabel);
            received = new NumericUpDown
            {
                Location = new Point(28, 148),
                Size = new Size(564, 42),
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                DecimalPlaces = 2,
                Maximum = 1000000,
                Minimum = 0,
                Value = total,
                ThousandsSeparator = true
            };
            received.ValueChanged += (s, e) => UpdateChange();
            body.Controls.Add(received);

            yapeLabel = Theme.Label("Monto pagado por Yape", 10f, FontStyle.Bold);
            yapeLabel.Location = new Point(28, 202);
            body.Controls.Add(yapeLabel);
            yapeAmount = new NumericUpDown
            {
                Location = new Point(28, 230), Size = new Size(564, 42), Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                DecimalPlaces = 2, Maximum = 1000000, Minimum = 0, Value = 0, ThousandsSeparator = true
            };
            yapeAmount.ValueChanged += (s, e) => UpdateChange();
            body.Controls.Add(yapeAmount);

            changeLabel = new Label
            {
                Location = new Point(28, 282),
                Size = new Size(564, 48),
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.GreenDark
            };
            body.Controls.Add(changeLabel);

            var cancel = Theme.Button("CANCELAR");
            cancel.SetBounds(28, 354, 170, 56);
            cancel.DialogResult = DialogResult.Cancel;
            confirm = Theme.Button("CONFIRMAR COBRO", true);
            confirm.SetBounds(210, 354, 382, 56);
            confirm.Click += Confirm_Click;
            body.Controls.Add(cancel);
            body.Controls.Add(confirm);
            CancelButton = cancel;
            AcceptButton = confirm;

            Controls.Remove(head); Controls.Remove(body);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            head.Dock = DockStyle.Fill; body.Dock = DockStyle.Fill;
            layout.Controls.Add(head, 0, 0); layout.Controls.Add(body, 0, 1); Controls.Add(layout);
            SelectPayment(cash);
            UpdateChange();
            Theme.FitDialogToWorkingArea(this);
        }

        private Button PaymentButton(string text, int left, int width)
        {
            var button = Theme.Button(text);
            button.SetBounds(left, 52, width, 50);
            button.Tag = text;
            button.Click += (s, e) => SelectPayment((Button)s);
            return button;
        }

        private void SelectPayment(Button selected)
        {
            var tag = selected.Tag.ToString();
            PaymentMethod = tag == "EFECTIVO" ? "Efectivo" : tag == "YAPE" ? "Yape" : "Mixto";
            foreach (var button in paymentButtons ?? new Button[0])
            {
                var active = button == selected;
                button.BackColor = active ? Theme.GreenSoft : Color.White;
                button.ForeColor = active ? Theme.GreenDark : Theme.Ink;
                button.FlatAppearance.BorderColor = active ? Theme.Green : Theme.Border;
            }
            var cash = PaymentMethod == "Efectivo";
            var mixed = PaymentMethod == "Mixto";
            receivedLabel.Text = mixed ? "Monto pagado en efectivo" : "Importe recibido en efectivo";
            receivedLabel.Visible = cash || mixed;
            received.Visible = cash || mixed;
            received.Enabled = cash || mixed;
            yapeLabel.Visible = mixed;
            yapeAmount.Visible = mixed;
            yapeAmount.Enabled = mixed;
            if (cash) { received.Value = Math.Min(received.Maximum, total); yapeAmount.Value = 0m; }
            else if (mixed) { received.Value = Math.Min(received.Maximum, Math.Round(total / 2m, 2)); yapeAmount.Value = total - received.Value; }
            else { received.Value = Math.Min(received.Maximum, total); yapeAmount.Value = PaymentMethod == "Yape" ? total : 0m; }
            UpdateChange();
        }

        private void UpdateChange()
        {
            var change = PaymentMethod == "Efectivo" ? Math.Max(0m, received.Value - total) : 0m;
            if (PaymentMethod == "Mixto")
            {
                var difference = total - received.Value - yapeAmount.Value;
                changeLabel.Text = difference == 0m ? "Pago completo:  " + Theme.Money(total) : difference > 0m ? "Falta:  " + Theme.Money(difference) : "Excede:  " + Theme.Money(-difference);
                changeLabel.ForeColor = difference == 0m ? Theme.GreenDark : Color.Firebrick;
                if (confirm != null) confirm.Enabled = difference == 0m && received.Value > 0m && yapeAmount.Value > 0m;
            }
            else
            {
                changeLabel.Text = PaymentMethod == "Efectivo" ? "Vuelto:  " + Theme.Money(change) : "Pago por " + PaymentMethod + ":  " + Theme.Money(total);
                changeLabel.ForeColor = Theme.GreenDark;
                if (confirm != null) confirm.Enabled = PaymentMethod != "Efectivo" || received.Value >= total;
            }
        }

        private void Confirm_Click(object sender, EventArgs e)
        {
            if (PaymentMethod == "Efectivo" && received.Value < total)
            {
                MessageBox.Show("El importe recibido es menor que el total.", "Cobro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (PaymentMethod == "Mixto" && (received.Value <= 0m || yapeAmount.Value <= 0m || received.Value + yapeAmount.Value != total))
            {
                MessageBox.Show("En pago mixto, los montos de Efectivo y Yape deben ser mayores que cero y sumar exactamente " + Theme.Money(total) + ".", "Cobro mixto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public sealed class SaleCompleteDialog : Form
    {
        public SaleCompleteDialog(Receipt receipt, decimal change)
        {
            Text = "Venta completada";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(500, 410);
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = Theme.Body;

            var check = new Label
            {
                Text = "✓",
                ForeColor = Color.White,
                BackColor = Theme.Green,
                Font = new Font("Segoe UI", 30f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(210, 24),
                Size = new Size(80, 80)
            };
            Controls.Add(check);
            Controls.Add(new Label
            {
                Text = "Venta completada",
                Location = new Point(20, 120),
                Size = new Size(460, 38),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
                ForeColor = Theme.Ink
            });
            Controls.Add(new Label
            {
                Text = "Recibo #2-" + receipt.Number + "\n" + Theme.Money(receipt.Total) + "  •  " + receipt.PaymentMethod +
                       (change > 0 ? "\nVuelto: " + Theme.Money(change) : ""),
                Location = new Point(20, 164),
                Size = new Size(460, 88),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Theme.Muted
            });
            var export = Theme.Button("GUARDAR RECIBO");
            export.SetBounds(48, 300, 190, 54);
            export.Click += (s, e) => ExportReceipt(receipt);
            var done = Theme.Button("LISTO", true);
            done.SetBounds(250, 300, 202, 54);
            done.DialogResult = DialogResult.OK;
            Controls.Add(export);
            Controls.Add(done);
            AcceptButton = done;
            Theme.FitDialogToWorkingArea(this);
        }

        private void ExportReceipt(Receipt receipt)
        {
            using (var save = new SaveFileDialog
            {
                Filter = "Archivo de texto (*.txt)|*.txt",
                FileName = "Recibo-2-" + receipt.Number + ".txt"
            })
            {
                if (save.ShowDialog(this) != DialogResult.OK) return;
                var text = new StringBuilder();
                text.AppendLine(AppData.Settings.StoreName);
                text.AppendLine("RECIBO #2-" + receipt.Number);
                text.AppendLine(receipt.CreatedAt.ToString("dd/MM/yyyy HH:mm"));
                text.AppendLine(new string('-', 42));
                foreach (var line in receipt.Lines)
                    text.AppendLine(line.Quantity + " x " + line.Name + "  " + Theme.Money(line.LineTotal));
                text.AppendLine(new string('-', 42));
                text.AppendLine("TOTAL: " + Theme.Money(receipt.Total));
                text.AppendLine("PAGO: " + receipt.PaymentMethod);
                text.AppendLine("CLIENTE: " + receipt.CustomerName);
                if (!string.IsNullOrWhiteSpace(receipt.CustomerDni)) text.AppendLine("DNI: " + receipt.CustomerDni);
                File.WriteAllText(save.FileName, text.ToString(), Encoding.UTF8);
                MessageBox.Show("Recibo guardado correctamente.", "Recibo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    public sealed class SaveTicketDialog : Form
    {
        private readonly TextBox nameBox;
        private readonly Button lockerButton;
        private readonly Button acceptButton;
        private readonly int adultCount;
        private readonly int childCount;
        private readonly string editingTicketId;
        public string TicketName { get { return nameBox.Text.Trim(); } }
        public List<int> LockerNumbers { get; private set; } = new List<int>();
        public int AdultCount { get { return adultCount; } }
        public int ChildCount { get { return childCount; } }

        public SaveTicketDialog(
            string suggestedName,
            IEnumerable<int> initialLockers = null,
            int suggestedAdults = 0,
            int suggestedChildren = 0,
            string currentTicketId = null)
        {
            editingTicketId = currentTicketId;
            adultCount = Math.Max(0, Math.Min(999, suggestedAdults));
            childCount = Math.Max(0, Math.Min(999, suggestedChildren));
            LockerNumbers = (initialLockers ?? Enumerable.Empty<int>()).Distinct().OrderBy(x => x).ToList();
            Text = string.IsNullOrWhiteSpace(editingTicketId) ? "Guardar ticket" : "Actualizar ticket";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(560, 330); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi; BackColor = Color.White; Font = Theme.Body;

            var title = Theme.Label("Nombre para identificarlo", 11f, FontStyle.Bold); title.Location = new Point(28, 28);
            nameBox = new TextBox { Location = new Point(28, 62), Size = new Size(504, 36), Font = new Font("Segoe UI", 11f), Text = suggestedName ?? "", MaxLength = 60 };
            var lockerLabel = Theme.Label("Candado asignado", 11f, FontStyle.Bold); lockerLabel.Location = new Point(28, 122);
            lockerButton = Theme.Button("▦  SELECCIONAR CANDADO", true); lockerButton.SetBounds(28, 154, 320, 56); lockerButton.Click += SelectLocker;
            if (LockerNumbers.Count > 0) UpdateLockerButton();
            var cancel = Theme.Button("CANCELAR"); cancel.SetBounds(210, 248, 150, 54); cancel.DialogResult = DialogResult.Cancel;
            acceptButton = Theme.Button(string.IsNullOrWhiteSpace(editingTicketId) ? "GUARDAR" : "GUARDAR CAMBIOS", true);
            acceptButton.SetBounds(372, 248, 160, 54);
            acceptButton.Enabled = LockerNumbers.Count > 0;
            acceptButton.Click += Accept_Click;
            Controls.Add(title); Controls.Add(nameBox); Controls.Add(lockerLabel); Controls.Add(lockerButton); Controls.Add(cancel); Controls.Add(acceptButton);
            CancelButton = cancel; AcceptButton = acceptButton;
            Shown += (s, e) => { nameBox.SelectAll(); nameBox.Focus(); };
            Theme.FitDialogToWorkingArea(this);
        }

        private void SelectLocker(object sender, EventArgs e)
        {
            var occupied = AppData.SavedTickets
                .Where(x => x.Id != editingTicketId)
                .SelectMany(AppData.GetTicketLockers)
                .Distinct()
                .ToList();
            using (var dialog = new LockerSelectionDialog(occupied, LockerNumbers))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                LockerNumbers = dialog.SelectedLockers.ToList();
                UpdateLockerButton();
                acceptButton.Enabled = true;
            }
        }

        private void UpdateLockerButton()
        {
            lockerButton.Text = "✓  " + LockerNumbers.Count +
                (LockerNumbers.Count == 1 ? " LLAVE: " : " LLAVES: ") +
                string.Join(", ", LockerNumbers);
        }

        private void Accept_Click(object sender, EventArgs e)
        {
            if (!Validation.IsValidRequiredText(nameBox.Text, 2, 60))
            {
                MessageBox.Show("Ingrese un nombre de 2 a 60 caracteres.", "Guardar ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning); nameBox.Focus(); return;
            }
            if (LockerNumbers == null || LockerNumbers.Count == 0)
            {
                MessageBox.Show("Seleccione uno de los 35 candados.", "Guardar ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            DialogResult = DialogResult.OK; Close();
        }
    }

    public sealed class LockerSelectionDialog : Form
    {
        private readonly HashSet<int> selected = new HashSet<int>();
        private readonly Label selectionLabel;
        public List<int> SelectedLockers { get { return selected.OrderBy(x => x).ToList(); } }

        public LockerSelectionDialog(System.Collections.Generic.IEnumerable<int> occupiedLockers, System.Collections.Generic.IEnumerable<int> selectedLockers = null)
        {
            Text = "Seleccionar candado";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(690, 520); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi; BackColor = Theme.Canvas; Font = Theme.Body;
            var occupied = new System.Collections.Generic.HashSet<int>(occupiedLockers ?? new int[0]);
            selected.UnionWith((selectedLockers ?? new int[0]).Where(x => x >= 1 && x <= 35));
            occupied.ExceptWith(selected);
            var head = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.Green, Padding = new Padding(24, 10, 24, 10) };
            head.Controls.Add(new Label { Text = "Seleccione uno o más candados", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 5, Padding = new Padding(20), BackColor = Theme.Canvas };
            for (var column = 0; column < 7; column++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
            for (var row = 0; row < 5; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            for (var number = 1; number <= 35; number++)
            {
                var button = Theme.Button(number.ToString(), !occupied.Contains(number));
                button.Dock = DockStyle.Fill; button.Margin = new Padding(6); button.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold); button.Tag = number;
                if (occupied.Contains(number))
                {
                    button.Text = number + "\nOCUPADO"; button.BackColor = Color.FromArgb(211, 65, 54); button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(185, 48, 40); button.FlatAppearance.MouseOverBackColor = Color.FromArgb(185, 48, 40);
                    button.Click += (s, e) => MessageBox.Show("El candado " + ((Button)s).Tag + " está ocupado. Seleccione uno de color verde.", "Candado ocupado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    button.Click += ToggleLocker;
                    if (selected.Contains(number))
                    {
                        button.Text = "✓  " + number;
                        button.BackColor = Theme.GreenDark;
                        button.ForeColor = Color.White;
                    }
                }
                grid.Controls.Add(button, (number - 1) % 7, (number - 1) / 7);
            }
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White };
            selectionLabel = Theme.Label("", 10f, FontStyle.Bold, Theme.Muted); selectionLabel.SetBounds(22, 21, 330, 28); selectionLabel.AutoSize = false;
            UpdateSelectionLabel();
            var cancel = Theme.Button("CANCELAR"); cancel.SetBounds(378, 10, 136, 48); cancel.DialogResult = DialogResult.Cancel;
            var confirm = Theme.Button("CONFIRMAR", true); confirm.SetBounds(526, 10, 140, 48); confirm.Click += ConfirmSelection;
            footer.Controls.Add(selectionLabel); footer.Controls.Add(cancel); footer.Controls.Add(confirm);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
            head.Dock = DockStyle.Fill; grid.Dock = DockStyle.Fill; footer.Dock = DockStyle.Fill;
            layout.Controls.Add(head, 0, 0); layout.Controls.Add(grid, 0, 1); layout.Controls.Add(footer, 0, 2); Controls.Add(layout); CancelButton = cancel;
            Theme.FitDialogToWorkingArea(this);
        }

        private void ToggleLocker(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var number = (int)button.Tag;
            if (selected.Contains(number))
            {
                selected.Remove(number); button.Text = number.ToString(); button.BackColor = Theme.Green; button.ForeColor = Color.White;
            }
            else
            {
                selected.Add(number); button.Text = "✓  " + number; button.BackColor = Theme.GreenDark; button.ForeColor = Color.White;
            }
            UpdateSelectionLabel();
        }

        private void UpdateSelectionLabel()
        {
            selectionLabel.Text = selected.Count == 0 ? "Ningún candado seleccionado" : selected.Count + (selected.Count == 1 ? " candado seleccionado" : " candados seleccionados");
            selectionLabel.ForeColor = selected.Count == 0 ? Theme.Muted : Theme.GreenDark;
        }

        private void ConfirmSelection(object sender, EventArgs e)
        {
            if (selected.Count == 0) { MessageBox.Show("Seleccione al menos un candado verde.", "Seleccionar candados", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DialogResult = DialogResult.OK; Close();
        }
    }

    public sealed class RentalReminderDialog : Form
    {
        public RentalReminderDialog()
        {
            Text = "Verificación antes de cobrar";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(650, 430); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi; BackColor = Color.White; Font = Theme.Body;
            var head = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Theme.Green };
            head.Controls.Add(new Label { Text = "ANTES DE COBRAR", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            var question = new Label { Text = "¿ALQUILARON ALGUNA PRENDA?", Location = new Point(30, 112), Size = new Size(590, 58), ForeColor = Theme.Ink, Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            var reminder = new Label { Text = "PEDIR LAS LLAVES", Location = new Point(30, 182), Size = new Size(590, 72), ForeColor = Color.FromArgb(200, 55, 45), Font = new Font("Segoe UI Semibold", 27f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            var hint = new Label { Text = "Verifique la devolución antes de continuar con el pago.", Location = new Point(30, 263), Size = new Size(590, 34), ForeColor = Theme.Muted, Font = new Font("Segoe UI", 11f), TextAlign = ContentAlignment.MiddleCenter };
            var back = Theme.Button("VOLVER"); back.SetBounds(32, 330, 190, 64); back.DialogResult = DialogResult.Cancel;
            var confirm = Theme.Button("✓  YA VERIFIQUÉ", true); confirm.SetBounds(238, 330, 380, 64); confirm.DialogResult = DialogResult.OK;
            Controls.Add(head); Controls.Add(question); Controls.Add(reminder); Controls.Add(hint); Controls.Add(back); Controls.Add(confirm);
            CancelButton = back; AcceptButton = confirm;
            Theme.FitDialogToWorkingArea(this);
        }
    }

    public sealed class PaymentDateSummaryDialog : Form
    {
        private readonly DateTimePicker datePicker;
        private readonly Label cashValue;
        private readonly Label yapeValue;
        private readonly Label ticketCount;

        public PaymentDateSummaryDialog(DateTime initialDate)
        {
            Text = "Resumen de cobros por fecha";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(610, 455); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi; BackColor = Theme.Canvas; Font = Theme.Body;
            var head = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Theme.Green };
            head.Controls.Add(new Label { Text = "Resumen de Efectivo y Yape", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            var dateLabel = Theme.Label("Seleccione la fecha", 10.5f, FontStyle.Bold); dateLabel.Location = new Point(34, 110);
            datePicker = new DateTimePicker { Location = new Point(34, 140), Size = new Size(542, 40), Font = new Font("Segoe UI", 12f), Format = DateTimePickerFormat.Custom, CustomFormat = "dddd dd/MM/yyyy", Value = initialDate.Date };
            datePicker.ValueChanged += (s, e) => UpdateTotals();
            var cashCard = new Panel { Location = new Point(34, 205), Size = new Size(258, 112), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            cashCard.Controls.Add(new Label { Text = "EFECTIVO", Location = new Point(8, 8), Size = new Size(240, 30), ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            cashValue = new Label { Location = new Point(8, 40), Size = new Size(240, 62), ForeColor = Theme.GreenDark, Font = new Font("Segoe UI Semibold", 23f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter }; cashCard.Controls.Add(cashValue);
            var yapeCard = new Panel { Location = new Point(318, 205), Size = new Size(258, 112), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            yapeCard.Controls.Add(new Label { Text = "YAPE", Location = new Point(8, 8), Size = new Size(240, 30), ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            yapeValue = new Label { Location = new Point(8, 40), Size = new Size(240, 62), ForeColor = Theme.GreenDark, Font = new Font("Segoe UI Semibold", 23f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter }; yapeCard.Controls.Add(yapeValue);
            ticketCount = new Label { Location = new Point(34, 330), Size = new Size(542, 30), ForeColor = Theme.Muted, Font = new Font("Segoe UI", 10f), TextAlign = ContentAlignment.MiddleCenter };
            var close = Theme.Button("CERRAR", true); close.SetBounds(396, 378, 180, 52); close.DialogResult = DialogResult.OK;
            Controls.Add(head); Controls.Add(dateLabel); Controls.Add(datePicker); Controls.Add(cashCard); Controls.Add(yapeCard); Controls.Add(ticketCount); Controls.Add(close);
            AcceptButton = close; CancelButton = close; UpdateTotals();
            Theme.FitDialogToWorkingArea(this);
        }

        private void UpdateTotals()
        {
            var date = datePicker.Value.Date;
            var paid = AppData.Receipts.Where(x => x.Status == "Pagado" && x.CreatedAt.Date == date).ToList();
            cashValue.Text = Theme.Money(paid.Sum(x => x.CashPaid));
            yapeValue.Text = Theme.Money(paid.Sum(x => x.YapePaid));
            ticketCount.Text = paid.Count + (paid.Count == 1 ? " recibo pagado" : " recibos pagados") + " en la fecha seleccionada";
        }
    }

    public sealed class SavedTicketsDialog : Form
    {
        private readonly DataGridView grid;
        private readonly TextBox searchBox;
        private readonly Label occupancySummary;
        public SavedTicket SelectedTicket { get; private set; }

        public SavedTicketsDialog()
        {
            Text = "Tickets guardados";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(920, 610);
            MinimumSize = new Size(700, 480);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Canvas;
            Font = Theme.Body;

            var head = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Theme.Green, Padding = new Padding(22, 10, 22, 10) };
            head.Controls.Add(new Label
            {
                Text = "Clientes dentro del local",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            Controls.Add(head);

            var occupancyPanel = new CardPanel
            {
                Dock = DockStyle.Top,
                Height = 76,
                Margin = new Padding(0),
                Padding = new Padding(20, 8, 20, 8),
                BackColor = Color.White
            };
            occupancySummary = new Label
            {
                Dock = DockStyle.Fill,
                Text = "EN ESTE MOMENTO   •   ADULTOS: 0   •   NIÑOS: 0   •   TOTAL: 0",
                ForeColor = Theme.GreenDark,
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            occupancyPanel.Controls.Add(occupancySummary);
            Controls.Add(occupancyPanel);

            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White, Padding = new Padding(18, 10, 18, 10) };
            var searchLabel = new Label
            {
                Text = "BUSCAR",
                Location = new Point(18, 10),
                Size = new Size(100, 38),
                ForeColor = Theme.GreenDark,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            searchBox = new TextBox
            {
                Location = new Point(118, 12),
                Size = new Size(770, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 11.5f),
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = "Buscar por nombre, DNI o número de candado"
            };
            searchBox.TextChanged += (s, e) => LoadTickets();
            searchPanel.Controls.Add(searchLabel);
            searchPanel.Controls.Add(searchBox);
            Controls.Add(searchPanel);

            var actions = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
            var cancel = Theme.Button("CANCELAR");
            cancel.SetBounds(16, 12, 108, 50);
            cancel.DialogResult = DialogResult.Cancel;
            var delete = Theme.Button("ELIMINAR");
            delete.SetBounds(134, 12, 108, 50);
            delete.Click += (s, e) => DeleteSelected();
            var editLockers = Theme.Button("EDITAR LLAVES");
            editLockers.SetBounds(252, 12, 176, 50);
            editLockers.Click += (s, e) => EditSelectedLockers();
            var open = Theme.Button("ABRIR TICKET", true);
            open.SetBounds(438, 12, 226, 50);
            open.Click += (s, e) => OpenSelected();
            actions.Controls.Add(cancel);
            actions.Controls.Add(delete);
            actions.Controls.Add(editLockers);
            actions.Controls.Add(open);
            Action resizeActions = () =>
            {
                const int left = 16;
                const int gap = 8;
                var buttonWidth = Math.Max(100, (actions.ClientSize.Width - (left * 2) - (gap * 3)) / 4);
                cancel.SetBounds(left, 12, buttonWidth, 50);
                delete.SetBounds(left + buttonWidth + gap, 12, buttonWidth, 50);
                editLockers.SetBounds(left + (buttonWidth + gap) * 2, 12, buttonWidth, 50);
                open.SetBounds(left + (buttonWidth + gap) * 3, 12, buttonWidth, 50);
            };
            actions.SizeChanged += (s, e) => resizeActions();
            resizeActions();
            Controls.Add(actions);

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Theme.Canvas };
            grid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(grid);
            grid.RowTemplate.Height = 48;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Locker", HeaderText = "Candado", Width = 92, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold) } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Ticket", FillWeight = 85 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "Cliente", FillWeight = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Adults", HeaderText = "Adultos", Width = 72, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Children", HeaderText = "Niños", Width = 68, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Hora", Width = 116, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", Width = 96, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenSelected(); };
            host.Controls.Add(grid);
            Controls.Clear();
            var responsiveLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Canvas
            };
            responsiveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            responsiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74f));
            responsiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
            responsiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
            responsiveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            responsiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
            head.Dock = DockStyle.Fill;
            occupancyPanel.Dock = DockStyle.Fill;
            searchPanel.Dock = DockStyle.Fill;
            host.Dock = DockStyle.Fill;
            actions.Dock = DockStyle.Fill;
            responsiveLayout.Controls.Add(head, 0, 0);
            responsiveLayout.Controls.Add(occupancyPanel, 0, 1);
            responsiveLayout.Controls.Add(searchPanel, 0, 2);
            responsiveLayout.Controls.Add(host, 0, 3);
            responsiveLayout.Controls.Add(actions, 0, 4);
            Controls.Add(responsiveLayout);
            LoadTickets();
            CancelButton = cancel;
            AcceptButton = open;
            Theme.FitDialogToWorkingArea(this);
        }

        private SavedTicket Current()
        {
            return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as SavedTicket;
        }

        private void LoadTickets()
        {
            var allTickets = AppData.SavedTickets.OrderByDescending(x => x.CreatedAt).ToList();
            var adults = allTickets.Sum(AppData.GetTicketAdultCount);
            var children = allTickets.Sum(AppData.GetTicketChildCount);
            occupancySummary.Text = "EN ESTE MOMENTO   •   ADULTOS: " + adults +
                                    "   •   NIÑOS: " + children +
                                    "   •   TOTAL: " + (adults + children);
            var term = (searchBox.Text ?? "").Trim();
            var tickets = allTickets.AsEnumerable();
            if (term.Length > 0)
            {
                int lockerQuery;
                var searchesLocker = int.TryParse(term, out lockerQuery);
                tickets = tickets.Where(ticket =>
                    (ticket.Name ?? "").IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (ticket.CustomerName ?? "").IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (ticket.CustomerDni ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (searchesLocker && AppData.GetTicketLockers(ticket).Contains(lockerQuery)));
            }

            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (var ticket in tickets)
            {
                var customer = ticket.CustomerName + (string.IsNullOrWhiteSpace(ticket.CustomerDni) ? "" : " • " + ticket.CustomerDni);
                var lockers = AppData.GetTicketLockers(ticket);
                var row = grid.Rows.Add(
                    lockers.Count > 0 ? string.Join(", ", lockers) : "—",
                    ticket.Name,
                    customer,
                    AppData.GetTicketAdultCount(ticket),
                    AppData.GetTicketChildCount(ticket),
                    ticket.CreatedAt.ToString("dd/MM HH:mm"),
                    Theme.Money(ticket.Total));
                grid.Rows[row].Tag = ticket;
            }
            if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
            grid.ResumeLayout();
        }

        private void DeleteSelected()
        {
            var ticket = Current();
            if (ticket == null) return;
            if (MessageBox.Show("¿Eliminar el ticket guardado ‘" + ticket.Name + "’?", "Tickets guardados", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            AppData.DeleteSavedTicket(ticket.Id);
            LoadTickets();
        }

        private void EditSelectedLockers()
        {
            var ticket = Current();
            if (ticket == null)
            {
                MessageBox.Show("Seleccione un ticket para editar su llave.", "Tickets guardados", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var occupied = AppData.SavedTickets
                .Where(x => x.Id != ticket.Id)
                .SelectMany(AppData.GetTicketLockers)
                .Distinct()
                .ToList();
            using (var dialog = new LockerSelectionDialog(occupied, AppData.GetTicketLockers(ticket)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    AppData.UpdateSavedTicketLockers(ticket.Id, dialog.SelectedLockers);
                    LoadTickets();
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    MessageBox.Show(ex.Message, "Editar llave", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenSelected()
        {
            SelectedTicket = Current();
            if (SelectedTicket == null)
            {
                MessageBox.Show("No hay un ticket seleccionado.", "Tickets guardados", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public static class PromptDialog
    {
        public static bool AskText(IWin32Window owner, string title, string label, out string value, string initial = "")
        {
            using (var form = BaseForm(title, 450, 220))
            {
                var caption = Theme.Label(label, 10f, FontStyle.Bold);
                caption.Location = new Point(24, 24);
                var box = new TextBox { Location = new Point(24, 58), Size = new Size(402, 34), Font = Theme.Body, Text = initial };
                var cancel = Theme.Button("CANCELAR"); cancel.SetBounds(154, 125, 128, 46); cancel.DialogResult = DialogResult.Cancel;
                var ok = Theme.Button("ACEPTAR", true); ok.SetBounds(292, 125, 134, 46); ok.DialogResult = DialogResult.OK;
                form.Controls.Add(caption); form.Controls.Add(box); form.Controls.Add(cancel); form.Controls.Add(ok);
                form.AcceptButton = ok; form.CancelButton = cancel;
                var result = form.ShowDialog(owner) == DialogResult.OK;
                value = box.Text.Trim();
                return result;
            }
        }

        public static bool AskMoney(IWin32Window owner, string title, string label, out decimal value, decimal initial = 0m)
        {
            using (var form = BaseForm(title, 450, 230))
            {
                var caption = Theme.Label(label, 10f, FontStyle.Bold);
                caption.Location = new Point(24, 24);
                var input = new NumericUpDown
                {
                    Location = new Point(24, 58), Size = new Size(402, 38), Font = new Font("Segoe UI", 15f),
                    DecimalPlaces = 2, Maximum = 1000000, Minimum = 0, Value = Math.Min(1000000, Math.Max(0, initial)), ThousandsSeparator = true
                };
                var cancel = Theme.Button("CANCELAR"); cancel.SetBounds(154, 135, 128, 46); cancel.DialogResult = DialogResult.Cancel;
                var ok = Theme.Button("ACEPTAR", true); ok.SetBounds(292, 135, 134, 46); ok.DialogResult = DialogResult.OK;
                form.Controls.Add(caption); form.Controls.Add(input); form.Controls.Add(cancel); form.Controls.Add(ok);
                form.AcceptButton = ok; form.CancelButton = cancel;
                var result = form.ShowDialog(owner) == DialogResult.OK;
                value = input.Value;
                return result;
            }
        }

        private static Form BaseForm(string title, int width, int height)
        {
            var form = new Form
            {
                Text = title, ClientSize = new Size(width, height), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi, BackColor = Color.White, Font = Theme.Body, ShowInTaskbar = false
            };
            Theme.FitDialogToWorkingArea(form);
            return form;
        }
    }
}
