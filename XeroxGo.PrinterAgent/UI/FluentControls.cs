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

        // Backgrounds
        public static readonly Color Background = Color.FromArgb(248, 250, 252);     // Slate 50
        public static readonly Color CardBackground = Color.FromArgb(255, 255, 255); // Pure White
        public static readonly Color CardBorder = Color.FromArgb(226, 232, 240);     // Slate 200

        // Typography
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);       // Slate 900
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);  // Slate 500
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);      // Slate 400

        // Accent / Actions (Windows 11 Blue)
        public static readonly Color Accent = Color.FromArgb(0, 103, 192);           // #0067C0
        public static readonly Color AccentHover = Color.FromArgb(0, 90, 170);
        public static readonly Color AccentPressed = Color.FromArgb(0, 77, 145);

        // Input Borders
        public static readonly Color InputBorder = Color.FromArgb(203, 213, 225);    // Slate 300
        public static readonly Color InputBorderHover = Color.FromArgb(148, 163, 184);
        public static readonly Color InputBorderFocused = Color.FromArgb(0, 103, 192);

        // Status Colors
        public static readonly Color Success = Color.FromArgb(16, 185, 129);         // Emerald 500
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);         // Amber 500
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);           // Rose 500

        public static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

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

    /// <summary>
    /// Clean card container with 8px rounded corners and 1px border.
    /// </summary>
    public class FluentCard : Panel
    {
        public int CornerRadius { get; set; } = 8;
        public Color BorderColor { get; set; } = FluentTheme.CardBorder;

        public FluentCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = FluentTheme.CardBackground;
            Padding = new Padding(20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, CornerRadius);

            using var bgBrush = new SolidBrush(BackColor);
            e.Graphics.FillPath(bgBrush, path);

            using var borderPen = new Pen(BorderColor, 1f);
            e.Graphics.DrawPath(borderPen, path);
        }
    }

    /// <summary>
    /// Modern Windows 11 text input container with flat 1px border and focus ring.
    /// Eliminates legacy 3D sunken borders.
    /// </summary>
    public class FluentTextBox : Panel
    {
        private readonly TextBox _textBox;
        private bool _isHovered = false;
        private bool _isFocused = false;

        public TextBox InnerTextBox => _textBox;

        public override string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        public bool UseSystemPasswordChar
        {
            get => _textBox.UseSystemPasswordChar;
            set => _textBox.UseSystemPasswordChar = value;
        }

        public new event EventHandler? TextChanged
        {
            add => _textBox.TextChanged += value;
            remove => _textBox.TextChanged -= value;
        }

        public FluentTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.White;
            Height = 32;
            Cursor = Cursors.IBeam;

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = FluentTheme.Font(9.5f),
                ForeColor = FluentTheme.TextPrimary,
                BackColor = Color.White,
                Location = new Point(10, 7),
                Width = Width - 20,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            _textBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _textBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _textBox.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            _textBox.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };

            Controls.Add(_textBox);
            Click += (s, e) => _textBox.Focus();
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_textBox != null)
            {
                _textBox.Width = Math.Max(10, Width - 20);
                _textBox.Location = new Point(10, (Height - _textBox.PreferredHeight) / 2);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, 4);

            using var bgBrush = new SolidBrush(BackColor);
            g.FillPath(bgBrush, path);

            Color borderColor = _isFocused
                ? FluentTheme.InputBorderFocused
                : (_isHovered ? FluentTheme.InputBorderHover : FluentTheme.InputBorder);

            float borderWidth = _isFocused ? 1.5f : 1f;
            using var borderPen = new Pen(borderColor, borderWidth);
            g.DrawPath(borderPen, path);
        }

        public new bool Focus() => _textBox.Focus();
    }

    /// <summary>
    /// Modern Windows 11 button with crisp 4px corner radius and stateful hover/press rendering.
    /// </summary>
    public class FluentButton : Button
    {
        public bool IsPrimary { get; set; } = false;
        public int CornerRadius { get; set; } = 4;
        private bool _isHovered = false;
        private bool _isPressed = false;

        public FluentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = FluentTheme.Font(9.5f, FontStyle.Regular);
            Height = 32;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Clear parent background smoothly
            if (Parent != null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                g.FillRectangle(parentBrush, ClientRectangle);
            }

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
                borderColor = _isHovered ? FluentTheme.InputBorderHover : FluentTheme.CardBorder;
            }

            using (var brush = new SolidBrush(bgColor))
            {
                g.FillPath(brush, path);
            }

            using (var pen = new Pen(borderColor, 1f))
            {
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

    /// <summary>
    /// Executive-grade auto-sizing status capsule with state-tinted background and pulse dot.
    /// </summary>
    public class FluentStatusBadge : Control
    {
        private string _statusText = "Connected to Cloud";
        private Color _dotColor = FluentTheme.Success;
        private Color _badgeBg = Color.FromArgb(240, 253, 244);     // Emerald 50
        private Color _badgeBorder = Color.FromArgb(187, 247, 208); // Emerald 200
        private Color _badgeText = Color.FromArgb(22, 101, 52);     // Emerald 800

        public FluentStatusBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 28;
            Width = 160;
            Font = FluentTheme.Font(8.5f, FontStyle.Regular);
        }

        public void SetStatus(string text, string state)
        {
            _statusText = string.IsNullOrWhiteSpace(text) ? "Connected" : text;

            switch (state.ToLowerInvariant())
            {
                case "printing":
                    _dotColor = FluentTheme.Success;
                    _badgeBg = Color.FromArgb(240, 253, 244);
                    _badgeBorder = Color.FromArgb(187, 247, 208);
                    _badgeText = Color.FromArgb(22, 101, 52);
                    break;
                case "paused":
                    _dotColor = FluentTheme.Warning;
                    _badgeBg = Color.FromArgb(254, 252, 232);
                    _badgeBorder = Color.FromArgb(254, 240, 138);
                    _badgeText = Color.FromArgb(133, 77, 14);
                    break;
                case "offline":
                case "error":
                    _dotColor = FluentTheme.Danger;
                    _badgeBg = Color.FromArgb(254, 242, 242);
                    _badgeBorder = Color.FromArgb(254, 202, 202);
                    _badgeText = Color.FromArgb(153, 27, 27);
                    break;
                default:
                    _dotColor = Color.FromArgb(37, 99, 235);
                    _badgeBg = Color.FromArgb(239, 246, 255);
                    _badgeBorder = Color.FromArgb(191, 219, 254);
                    _badgeText = Color.FromArgb(30, 64, 175);
                    break;
            }

            // Auto-size width to accommodate text comfortably
            using var g = CreateGraphics();
            var textSize = TextRenderer.MeasureText(g, _statusText, Font);
            int newWidth = Math.Max(120, textSize.Width + 34);

            if (Width != newWidth)
            {
                int diff = newWidth - Width;
                Width = newWidth;
                if (Parent != null && Anchor.HasFlag(AnchorStyles.Right))
                {
                    Left -= diff;
                }
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, Height / 2);

            // Tinted pill background
            using var bgBrush = new SolidBrush(_badgeBg);
            g.FillPath(bgBrush, path);

            // Subtle border
            using var borderPen = new Pen(_badgeBorder, 1f);
            g.DrawPath(borderPen, path);

            // Status Indicator Dot
            int dotSize = 7;
            int dotY = (Height - dotSize) / 2;
            using var dotBrush = new SolidBrush(_dotColor);
            g.FillEllipse(dotBrush, 10, dotY, dotSize, dotSize);

            // Status Text
            var textRect = new Rectangle(22, 0, Width - 30, Height);
            TextRenderer.DrawText(
                g,
                _statusText,
                Font,
                textRect,
                _badgeText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
            );
        }
    }
    #endregion
}
