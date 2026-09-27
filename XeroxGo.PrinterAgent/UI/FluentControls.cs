using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XeroxGo.PrinterAgent.UI
{
    #region Native Windows 11 DWM Interop
    public static class DwmApi
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Windows 11 DWM Attributes
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWA_CAPTION_COLOR = 35;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // Corner preferences
        public const int DWMWCP_DEFAULT = 0;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        public static void ApplyWindows11Styling(IntPtr handle)
        {
            if (Environment.OSVersion.Version.Major < 10) return;

            try
            {
                // 1. Force native Windows 11 rounded corners
                int roundCorner = DWMWCP_ROUND;
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorner, sizeof(int));

                // 2. Light mode / subtle caption bar matching the window (0x00BBGGRR: R=248, G=250, B=252)
                int captionColor = 0x00FCFAF8;
                DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
            catch
            {
                // Silently ignore on older Windows builds that do not support dwmapi attributes
            }
        }

        public static void ApplyWindows11Styling(Form form) => ApplyWindows11Styling(form.Handle);
    }
    #endregion

    #region Typography & Color Tokens
    public static class FluentTheme
    {
        public static readonly string PrimaryFontName =
            FontFamily.Families.Any(f => f.Name.Equals("Segoe UI Variable Text", StringComparison.OrdinalIgnoreCase))
                ? "Segoe UI Variable Text"
                : "Segoe UI";

        public static Font Font(float size, FontStyle style = FontStyle.Regular) =>
            new Font(PrimaryFontName, size, style, GraphicsUnit.Point);

        // Palette
        public static readonly Color Background = Color.FromArgb(248, 250, 252);     // Slate 50
        public static readonly Color CardBackground = Color.FromArgb(255, 255, 255); // Pure White
        public static readonly Color CardBorder = Color.FromArgb(226, 232, 240);     // Slate 200

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);       // Slate 900
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);  // Slate 500

        public static readonly Color Accent = Color.FromArgb(0, 103, 192);           // Windows 11 Fluent Blue (#0067C0)
        public static readonly Color AccentHover = Color.FromArgb(0, 90, 170);
        public static readonly Color AccentPressed = Color.FromArgb(0, 77, 145);

        public static readonly Color Success = Color.FromArgb(16, 185, 129);         // Emerald 500
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);         // Amber 500
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);           // Rose 500

        public static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
    #endregion

    #region Fluent Context Menu Renderer
    public class FluentContextMenuRenderer : ToolStripProfessionalRenderer
    {
        public FluentContextMenuRenderer() : base(new FluentColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
                using var path = FluentTheme.CreateRoundedPath(rect, 4);

                using var brush = new SolidBrush(Color.FromArgb(241, 245, 249)); // Slate 100
                g.FillPath(brush, path);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics;
            int y = e.Item.Height / 2;
            using var pen = new Pen(FluentTheme.CardBorder, 1f);
            g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }

        private class FluentColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
            public override Color MenuBorder => FluentTheme.CardBorder;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => Color.FromArgb(241, 245, 249);
            public override Color SeparatorDark => FluentTheme.CardBorder;
            public override Color SeparatorLight => Color.White;
        }
    }
    #endregion

    #region Fluent UI Controls
    public class FluentCard : Panel
    {
        public int CornerRadius { get; set; } = 8;
        public Color BorderColor { get; set; } = FluentTheme.CardBorder;

        public FluentCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = FluentTheme.CardBackground;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, CornerRadius);

            using var bgBrush = new SolidBrush(BackColor);
            e.Graphics.FillPath(bgBrush, path);

            using var borderPen = new Pen(BorderColor, 1f);
            e.Graphics.DrawPath(borderPen, path);
        }
    }

    public class FluentButton : Button
    {
        public bool IsPrimary { get; set; } = false;
        public int CornerRadius { get; set; } = 6;
        private bool _isHovered = false;
        private bool _isPressed = false;

        public FluentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = FluentTheme.Font(9.5f, FontStyle.Regular);
            Size = new Size(120, 34);
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, CornerRadius);

            Color bgColor;
            Color textColor;
            Color borderColor;

            if (IsPrimary)
            {
                bgColor = _isPressed ? FluentTheme.AccentPressed : (_isHovered ? FluentTheme.AccentHover : FluentTheme.Accent);
                textColor = Color.White;
                borderColor = bgColor;
            }
            else
            {
                bgColor = _isPressed ? Color.FromArgb(226, 232, 240) : (_isHovered ? Color.FromArgb(241, 245, 249) : Color.White);
                textColor = FluentTheme.TextPrimary;
                borderColor = FluentTheme.CardBorder;
            }

            using (var brush = new SolidBrush(bgColor))
            {
                g.FillPath(brush, path);
            }

            if (!IsPrimary)
            {
                using var pen = new Pen(borderColor, 1f);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                g,
                Text,
                Font,
                rect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            );
        }
    }

    public class FluentStatusBadge : Control
    {
        private string _statusText = "Idle";
        private Color _dotColor = FluentTheme.Success;

        public FluentStatusBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(140, 28);
            Font = FluentTheme.Font(9f, FontStyle.Regular);
        }

        public void SetStatus(string text, string state)
        {
            _statusText = text;
            _dotColor = state.ToLowerInvariant() switch
            {
                "printing" => FluentTheme.Success,
                "paused" => FluentTheme.Warning,
                "offline" or "error" => FluentTheme.Danger,
                _ => Color.FromArgb(59, 130, 246) // Blue
            };
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, Height / 2);

            // Background pill
            using var bgBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            g.FillPath(bgBrush, path);

            using var borderPen = new Pen(FluentTheme.CardBorder, 1f);
            g.DrawPath(borderPen, path);

            // Dot
            int dotSize = 8;
            int dotY = (Height - dotSize) / 2;
            using var dotBrush = new SolidBrush(_dotColor);
            g.FillEllipse(dotBrush, 12, dotY, dotSize, dotSize);

            // Text
            var textRect = new Rectangle(24, 0, Width - 30, Height);
            TextRenderer.DrawText(
                g,
                _statusText,
                Font,
                textRect,
                FluentTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );
        }
    }
    #endregion
}
