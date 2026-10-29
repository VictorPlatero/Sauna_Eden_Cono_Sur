using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace EdenPOS
{
    public static class Theme
    {
        public static readonly Color Green = Color.FromArgb(37, 174, 74);
        public static readonly Color GreenDark = Color.FromArgb(29, 142, 60);
        public static readonly Color GreenSoft = Color.FromArgb(232, 247, 236);
        public static readonly Color Lime = Color.FromArgb(104, 184, 37);
        public static readonly Color Ink = Color.FromArgb(37, 44, 52);
        public static readonly Color Muted = Color.FromArgb(112, 119, 126);
        public static readonly Color Border = Color.FromArgb(224, 228, 232);
        public static readonly Color Canvas = Color.FromArgb(247, 249, 250);
        public static readonly Font Body = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        public static readonly Font Small = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 18f, FontStyle.Bold);

        public static Button Button(string text, bool primary = false)
        {
            var button = new Button
            {
                Text = text,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Green : Color.White,
                ForeColor = primary ? Color.White : Ink,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(6),
                Padding = new Padding(12, 0, 12, 0),
                AutoSize = false
            };
            button.FlatAppearance.BorderColor = primary ? Green : Border;
            button.FlatAppearance.MouseOverBackColor = primary ? GreenDark : GreenSoft;
            return button;
        }

        public static Label Label(string text, float size = 10.5f, FontStyle style = FontStyle.Regular, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size, style),
                ForeColor = color ?? Ink,
                AutoSize = true,
                Margin = new Padding(0)
            };
        }

        public static TextBox TextBox(string placeholder = "")
        {
            var box = new TextBox
            {
                Font = Body,
                BorderStyle = BorderStyle.FixedSingle,
                Height = 36,
                Tag = placeholder
            };
            return box;
        }

        public static Color ParseColor(string value, Color fallback)
        {
            try { return ColorTranslator.FromHtml(value); }
            catch { return fallback; }
        }

        public static void StyleGrid(DataGridView grid)
        {
            EnableDoubleBuffer(grid);
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Border;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 42;
            grid.RowTemplate.Height = 42;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 248);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            grid.DefaultCellStyle.Font = Body;
            grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = GreenSoft;
            grid.DefaultCellStyle.SelectionForeColor = Ink;
        }

        public static void EnableDoubleBuffer(Control control)
        {
            if (control == null || SystemInformation.TerminalServerSession) return;
            try
            {
                typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(control, true, null);
            }
            catch { }
        }

        public static void FitDialogToWorkingArea(Form form, int margin = 24)
        {
            if (form == null) return;
            form.Shown += (s, e) =>
            {
                var area = Screen.FromControl(form).WorkingArea;
                var maximumWidth = Math.Max(360, area.Width - margin * 2);
                var maximumHeight = Math.Max(300, area.Height - margin * 2);
                if (form.Width > maximumWidth || form.Height > maximumHeight)
                {
                    var factor = Math.Min(maximumWidth / (float)form.Width, maximumHeight / (float)form.Height);
                    form.MinimumSize = Size.Empty;
                    form.Scale(new SizeF(factor, factor));
                    form.Size = new Size(
                        Math.Min(maximumWidth, Math.Max(320, form.Width)),
                        Math.Min(maximumHeight, Math.Max(260, form.Height)));
                }
                form.Location = new Point(
                    area.Left + Math.Max(0, (area.Width - form.Width) / 2),
                    area.Top + Math.Max(0, (area.Height - form.Height) / 2));
            };
        }

        public static string Money(decimal amount) { return AppData.Money(amount); }
    }

    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
    }

    public class CardPanel : BufferedPanel
    {
        public int Radius { get; set; } = 8;
        public Color BorderColor { get; set; } = Theme.Border;

        public CardPanel()
        {
            BackColor = Color.White;
            Padding = new Padding(1);
            Resize += (s, e) => UpdateRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, path);
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (var path = RoundedRectangle(new Rectangle(0, 0, Width, Height), Radius))
                Region = new Region(path);
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
    }

    public interface IRefreshable
    {
        void RefreshView();
    }
}
