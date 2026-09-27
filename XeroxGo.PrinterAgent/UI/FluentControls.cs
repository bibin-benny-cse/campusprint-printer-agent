using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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

        // Backdrop types
        public const int DWMSBT_AUTO = 0;
        public const int DWMSBT_NONE = 1;
        public const int DWMSBT_MAINWINDOW = 2; // Mica
        public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        public const int DWMSBT_TABBEDWINDOW = 4; // Mica Alt

        public static void ApplyWindows11Styling(Form form)
        {
            if (Environment.OSVersion.Version.Major < 10) return;

            try
            {
                IntPtr handle = form.Handle;

                // 1. Force native Windows 11 rounded corners
                int roundCorner = DWMWCP_ROUND;
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorner, sizeof(int));

                // 2. Light mode / subtle caption bar matching the window
                int captionColor = ColorTranslator.ToWin32(Color.FromArgb(248, 250, 252));
                DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
            catch
            {
                // Silently ignore on older Windows builds that do not support dwmapi attributes
            }
        }
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
        public static readonly Color CardBorderHover = Color.FromArgb(203, 213, 225);// Slate 300

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);       // Slate 900
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);  // Slate 500
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);      // Slate 400

        public static readonly Color Accent = Color.FromArgb(0, 103, 192);           // Windows 11 Fluent Blue (#0067C0)
        public static readonly Color AccentHover = Color.FromArgb(24, 119, 211);     // Fluent Blue Hover
        public static readonly Color AccentPressed = Color.FromArgb(0, 90, 168);     // Fluent Blue Pressed

        public static readonly Color SuccessBg = Color.FromArgb(220, 252, 231);      // Emerald 100
        public static readonly Color SuccessText = Color.FromArgb(21, 128, 61);      // Emerald 700
        public static readonly Color SuccessDot = Color.FromArgb(34, 197, 94);       // Emerald 500

        public static readonly Color WarningBg = Color.FromArgb(254, 243, 199);      // Amber 100
        public static readonly Color WarningText = Color.FromArgb(180, 83, 9);       // Amber 700
        public static readonly Color WarningDot = Color.FromArgb(245, 158, 11);      // Amber 500

        public static readonly Color ErrorBg = Color.FromArgb(254, 226, 226);        // Red 100
        public static readonly Color ErrorText = Color.FromArgb(185, 28, 28);        // Red 700
        public static readonly Color ErrorDot = Color.FromArgb(239, 68, 68);         // Red 500

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

    #region Fluent Card Container
    public class FluentCard : Panel
    {
        public int CornerRadius { get; set; } = 8;
        public Color BorderColor { get; set; } = FluentTheme.CardBorder;

        public FluentCard()
        {
            DoubleBuffered = true;
            BackColor = FluentTheme.CardBackground;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, CornerRadius);

            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);

            using var pen = new Pen(BorderColor, 1f);
            e.Graphics.DrawPath(pen, path);
        }
    }
    #endregion

    #region Fluent Button
    public class FluentButton : Button
    {
        public bool IsPrimary { get; set; } = false;
        public int CornerRadius { get; set; } = 6;

        private bool _isHovered;
        private bool _isPressed;

        public FluentButton()
        {
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = FluentTheme.Font(9.5f, FontStyle.Regular);
            Height = 36;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, CornerRadius);

            Color fill;
            Color text;
            Color border;

            if (IsPrimary)
            {
                text = Color.White;
                border = Color.Transparent;
                if (!Enabled)
                {
                    fill = Color.FromArgb(190, 215, 240);
                }
                else if (_isPressed)
                {
                    fill = FluentTheme.AccentPressed;
                }
                else if (_isHovered)
                {
                    fill = FluentTheme.AccentHover;
                }
                else
                {
                    fill = FluentTheme.Accent;
                }
            }
            else
            {
                text = Enabled ? FluentTheme.TextPrimary : FluentTheme.TextMuted;
                border = _isHovered ? FluentTheme.CardBorderHover : FluentTheme.CardBorder;

                if (!Enabled)
                {
                    fill = Color.FromArgb(248, 250, 252);
                }
                else if (_isPressed)
                {
                    fill = Color.FromArgb(226, 232, 240);
                }
                else if (_isHovered)
                {
                    fill = Color.FromArgb(241, 245, 249);
                }
                else
                {
                    fill = Color.White;
                }
            }

            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }

            if (!IsPrimary || !Enabled)
            {
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }

            // Draw Text
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                rect,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            );
        }
    }
    #endregion

    #region Fluent Toggle Switch (Windows 11 Settings Style)
    public class FluentToggleSwitch : Control
    {
        private bool _checked = false;
        private bool _isHovered = false;

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public FluentToggleSwitch()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(44, 22);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int pillWidth = 40;
            int pillHeight = 20;
            var pillRect = new Rectangle(0, (Height - pillHeight) / 2, pillWidth, pillHeight);
            using var pillPath = FluentTheme.CreateRoundedPath(pillRect, pillHeight / 2);

            Color pillColor;
            Color thumbColor;
            Color borderColor;

            if (_checked)
            {
                pillColor = _isHovered ? FluentTheme.AccentHover : FluentTheme.Accent;
                thumbColor = Color.White;
                borderColor = pillColor;
            }
            else
            {
                pillColor = _isHovered ? Color.FromArgb(241, 245, 249) : Color.FromArgb(248, 250, 252);
                thumbColor = _isHovered ? Color.FromArgb(71, 85, 105) : Color.FromArgb(100, 116, 139);
                borderColor = _isHovered ? Color.FromArgb(148, 163, 184) : Color.FromArgb(203, 213, 225);
            }

            // Draw Pill
            using (var brush = new SolidBrush(pillColor))
            {
                g.FillPath(brush, pillPath);
            }
            using (var pen = new Pen(borderColor, 1f))
            {
                g.DrawPath(pen, pillPath);
            }

            // Draw Thumb Knob
            int thumbSize = 12;
            int thumbX = _checked ? (pillRect.Right - thumbSize - 4) : (pillRect.Left + 4);
            int thumbY = pillRect.Top + (pillHeight - thumbSize) / 2;

            var thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);
            using (var thumbBrush = new SolidBrush(thumbColor))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }
    }
    #endregion

    #region Fluent Text Box Container
    public class FluentTextBox : Panel
    {
        private readonly TextBox _innerTextBox;
        private readonly Button? _btnToggleMask;
        private bool _isMasked = false;
        private bool _isFocused = false;

        public TextBox TextBox => _innerTextBox;

        public override string? Text
        {
            get => _innerTextBox.Text;
            set => _innerTextBox.Text = value ?? string.Empty;
        }

        public bool IsPassword
        {
            get => _isMasked;
            set
            {
                _isMasked = value;
                _innerTextBox.UseSystemPasswordChar = value;
            }
        }

        public FluentTextBox(bool allowPasswordToggle = false)
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Height = 36;
            Padding = new Padding(10, 8, 10, 8);

            _innerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = FluentTheme.Font(9.5f),
                ForeColor = FluentTheme.TextPrimary,
                BackColor = Color.White,
                Dock = DockStyle.Fill
            };

            _innerTextBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _innerTextBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };

            if (allowPasswordToggle)
            {
                _isMasked = true;
                _innerTextBox.UseSystemPasswordChar = true;

                _btnToggleMask = new Button
                {
                    Text = "👁️",
                    Dock = DockStyle.Right,
                    Width = 32,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI Emoji", 8.5f),
                    ForeColor = FluentTheme.TextSecondary
                };
                _btnToggleMask.FlatAppearance.BorderSize = 0;
                _btnToggleMask.Click += (s, e) =>
                {
                    _isMasked = !_isMasked;
                    _innerTextBox.UseSystemPasswordChar = _isMasked;
                };

                Controls.Add(_btnToggleMask);
            }

            Controls.Add(_innerTextBox);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, 6);

            Color borderColor = _isFocused ? FluentTheme.Accent : FluentTheme.CardBorder;
            float borderWidth = _isFocused ? 1.5f : 1f;

            using var pen = new Pen(borderColor, borderWidth);
            e.Graphics.DrawPath(pen, path);
        }
    }
    #endregion

    #region Fluent Status Badge Pill
    public class FluentStatusBadge : Control
    {
        private string _status = "Connected to Cloud";
        private Color _bgColor = FluentTheme.SuccessBg;
        private Color _textColor = FluentTheme.SuccessText;
        private Color _dotColor = FluentTheme.SuccessDot;

        public FluentStatusBadge()
        {
            DoubleBuffered = true;
            Size = new Size(160, 28);
            Font = FluentTheme.Font(8.5f, FontStyle.Bold);
        }

        public void SetStatus(string text, string state)
        {
            _status = text;
            switch (state.ToLowerInvariant())
            {
                case "printing":
                    _bgColor = Color.FromArgb(224, 242, 254);
                    _textColor = Color.FromArgb(3, 105, 161);
                    _dotColor = Color.FromArgb(14, 165, 233);
                    break;
                case "paused":
                    _bgColor = FluentTheme.WarningBg;
                    _textColor = FluentTheme.WarningText;
                    _dotColor = FluentTheme.WarningDot;
                    break;
                case "offline":
                case "error":
                    _bgColor = FluentTheme.ErrorBg;
                    _textColor = FluentTheme.ErrorText;
                    _dotColor = FluentTheme.ErrorDot;
                    break;
                default:
                    _bgColor = FluentTheme.SuccessBg;
                    _textColor = FluentTheme.SuccessText;
                    _dotColor = FluentTheme.SuccessDot;
                    break;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = FluentTheme.CreateRoundedPath(rect, Height / 2);

            using (var brush = new SolidBrush(_bgColor))
            {
                g.FillPath(brush, path);
            }

            // Glowing Indicator Dot
            int dotSize = 8;
            int dotX = 10;
            int dotY = (Height - dotSize) / 2;
            using (var dotBrush = new SolidBrush(_dotColor))
            {
                g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
            }

            // Text
            var textRect = new Rectangle(dotX + dotSize + 6, 0, Width - (dotX + dotSize + 12), Height);
            TextRenderer.DrawText(
                g,
                _status,
                Font,
                textRect,
                _textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
            );
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
}
