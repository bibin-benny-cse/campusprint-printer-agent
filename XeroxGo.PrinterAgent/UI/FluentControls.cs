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

                // 2. Subtle caption bar matching the window (0x00BBGGRR: R=248, G=250, B=252)
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

        /// <summary>
        /// Generates a perfectly proportioned rounded rectangle path with sub-pixel clamp protection.
        /// </summary>
        public static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0.01f)
            {
                path.AddRectangle(rect);
                return path;
            }

            float diameter = radius * 2f;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

            // Top-Left Arc
            path.AddArc(arc, 180, 90);

            // Top-Right Arc
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-Right Arc
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-Left Arc
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        public static GraphicsPath CreateRoundedPath(Rectangle rect, int radius) =>
            CreateRoundedPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
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
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var rect = new RectangleF(4.5f, 2.5f, e.Item.Width - 9f, e.Item.Height - 5f);
                using var path = FluentTheme.CreateRoundedPath(rect, 4f);

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
    /// Anti-aliased with zero outer-corner halo.
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
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. Clear with parent background so corners blend smoothly
            if (Parent != null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                g.FillRectangle(parentBrush, ClientRectangle);
            }

            // 2. Fill rounded card background
            var fillRect = new RectangleF(0, 0, Width, Height);
            using var fillPath = FluentTheme.CreateRoundedPath(fillRect, CornerRadius);
            using var bgBrush = new SolidBrush(BackColor);
            g.FillPath(bgBrush, fillPath);

            // 3. Crisp, non-clipped 1px border
            var strokeRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var strokePath = FluentTheme.CreateRoundedPath(strokeRect, CornerRadius - 0.5f);
            using var borderPen = new Pen(BorderColor, 1f);
            g.DrawPath(borderPen, strokePath);
        }
    }

    /// <summary>
    /// Modern Windows 11 text input container with flat 1px border and focus ring.
    /// Perfectly centered text baseline with 10px horizontal padding.
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
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            UpdateInnerTextBoxLayout();

            _textBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _textBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _textBox.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            _textBox.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };

            Controls.Add(_textBox);
            Click += (s, e) => _textBox.Focus();
        }

        private void UpdateInnerTextBoxLayout()
        {
            if (_textBox == null) return;
            int padX = 10;
            _textBox.Width = Math.Max(10, Width - (padX * 2));
            int tbY = Math.Max(0, (Height - _textBox.PreferredHeight) / 2);
            _textBox.Location = new Point(padX, tbY);
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateInnerTextBoxLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (Parent != null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                g.FillRectangle(parentBrush, ClientRectangle);
            }

            var fillRect = new RectangleF(0, 0, Width, Height);
            using var fillPath = FluentTheme.CreateRoundedPath(fillRect, 4f);
            using var bgBrush = new SolidBrush(BackColor);
            g.FillPath(bgBrush, fillPath);

            Color borderColor = _isFocused
                ? FluentTheme.InputBorderFocused
                : (_isHovered ? FluentTheme.InputBorderHover : FluentTheme.InputBorder);

            var strokeRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var strokePath = FluentTheme.CreateRoundedPath(strokeRect, 3.5f);
            using var borderPen = new Pen(borderColor, 1f);
            g.DrawPath(borderPen, strokePath);

            // Windows 11 Fluent 2 bottom accent highlight when focused
            if (_isFocused)
            {
                using var focusPen = new Pen(FluentTheme.Accent, 2f);
                float r = 3.5f;
                float d = r * 2f;
                using var bottomPath = new GraphicsPath();
                bottomPath.AddArc(0.5f, Height - 1f - d, d, d, 90, 45);
                bottomPath.AddLine(0.5f + r, Height - 1f, Width - 1f - r, Height - 1f);
                bottomPath.AddArc(Width - 1f - d, Height - 1f - d, d, d, 45, 45);
                g.DrawPath(focusPen, bottomPath);
            }
        }

        public new bool Focus() => _textBox.Focus();
    }

    /// <summary>
    /// Modern Windows 11 button with crisp 4px corner radius, vector icons, and stateful hover/press rendering.
    /// </summary>
    public class FluentButton : Button
    {
        public bool IsPrimary { get; set; } = false;
        public bool HasPrinterIcon { get; set; } = false;
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

            if (Parent != null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                g.FillRectangle(parentBrush, ClientRectangle);
            }

            var fillRect = new RectangleF(0, 0, Width, Height);
            using var fillPath = FluentTheme.CreateRoundedPath(fillRect, CornerRadius);

            Color bgColor;
            Color textColor;
            Color borderColor;

            if (IsPrimary)
            {
                bgColor = _isPressed ? FluentTheme.AccentPressed : (_isHovered ? FluentTheme.AccentHover : FluentTheme.Accent);
                textColor = Color.White;
                borderColor = _isPressed ? Color.FromArgb(0, 65, 125) : Color.FromArgb(0, 90, 168);
            }
            else
            {
                bgColor = _isPressed ? Color.FromArgb(226, 232, 240) : (_isHovered ? Color.FromArgb(248, 250, 252) : Color.White);
                textColor = FluentTheme.TextPrimary;
                borderColor = _isHovered ? FluentTheme.InputBorderHover : FluentTheme.CardBorder;
            }

            using (var brush = new SolidBrush(bgColor))
            {
                g.FillPath(brush, fillPath);
            }

            var strokeRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var strokePath = FluentTheme.CreateRoundedPath(strokeRect, CornerRadius - 0.5f);
            using (var pen = new Pen(borderColor, 1f))
            {
                g.DrawPath(pen, strokePath);
            }

            // Optional vector printer icon for crisp baseline alignment (no emoji font fallback jitter)
            int textStartX = 0;
            if (HasPrinterIcon)
            {
                int iconSize = 14;
                int iconX = 14;
                int iconY = (Height - iconSize) / 2;
                DrawVectorPrinterIcon(g, iconX, iconY, textColor);
                textStartX = iconX + iconSize + 6;
            }

            var textRect = textStartX > 0
                ? new Rectangle(textStartX, 0, Width - textStartX - 8, Height)
                : new Rectangle(0, 0, Width, Height);

            var textFormat = textStartX > 0
                ? (TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix)
                : (TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, Text, Font, textRect, textColor, textFormat);

            // Subtle focus indicator
            if (Focused && ShowFocusCues)
            {
                using var focusPen = new Pen(FluentTheme.Accent, 1f) { DashStyle = DashStyle.Dot };
                var focusRect = new RectangleF(2.5f, 2.5f, Width - 5f, Height - 5f);
                using var focusPath = FluentTheme.CreateRoundedPath(focusRect, Math.Max(1, CornerRadius - 2));
                g.DrawPath(focusPen, focusPath);
            }
        }

        private static void DrawVectorPrinterIcon(Graphics g, int x, int y, Color color)
        {
            using var pen = new Pen(color, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var brush = new SolidBrush(color);

            // Paper Tray (Top)
            g.DrawRectangle(pen, x + 3, y + 1, 8, 3);

            // Printer Body (Middle)
            g.DrawRectangle(pen, x, y + 4, 14, 6);

            // Paper Output (Bottom)
            g.FillRectangle(brush, x + 3, y + 8, 8, 4);
            using var innerPen = new Pen(Color.FromArgb(200, color), 1f);
            g.DrawLine(innerPen, x + 4, y + 10, x + 10, y + 10);
        }
    }

    /// <summary>
    /// Modern Windows 11 ComboBox with 32px height, 4px rounded borders, and a sleek chevron arrow.
    /// Eliminates legacy Windows 7 3D beveled borders.
    /// </summary>
    public class FluentComboBox : ComboBox
    {
        private bool _isHovered = false;
        private bool _isFocused = false;

        public FluentComboBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            ItemHeight = 24;
            Font = FluentTheme.Font(9.5f);
            BackColor = Color.White;
            ForeColor = FluentTheme.TextPrimary;
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { _isFocused = true; Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { _isFocused = false; Invalidate(); base.OnLostFocus(e); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            bool isSelected = (e.State & DrawItemState.Selected) != 0;
            Color itemBg = isSelected ? Color.FromArgb(241, 245, 249) : Color.White; // Slate 100 on hover
            Color itemText = FluentTheme.TextPrimary;

            using (var bgBrush = new SolidBrush(itemBg))
            {
                g.FillRectangle(bgBrush, e.Bounds);
            }

            string text = Items[e.Index]?.ToString() ?? "";
            var textRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            TextRenderer.DrawText(
                g,
                text,
                Font,
                textRect,
                itemText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
            );
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // WM_PAINT
            if (m.Msg == 0x000F)
            {
                using var g = Graphics.FromHwnd(Handle);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // 1. Paint clean right-hand chevron area
                int btnWidth = 28;
                var btnRect = new Rectangle(Width - btnWidth, 1, btnWidth - 1, Height - 2);
                using (var bgBrush = new SolidBrush(BackColor))
                {
                    g.FillRectangle(bgBrush, btnRect);
                }

                // 2. Windows 11 Modern Chevron Down Arrow
                float midX = Width - 14.5f;
                float midY = Height / 2f;
                Color arrowColor = _isFocused ? FluentTheme.Accent : (_isHovered ? FluentTheme.TextPrimary : FluentTheme.TextSecondary);

                using (var arrowPen = new Pen(arrowColor, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(arrowPen, midX - 3.5f, midY - 1.5f, midX, midY + 2f);
                    g.DrawLine(arrowPen, midX, midY + 2f, midX + 3.5f, midY - 1.5f);
                }

                // 3. Crisp 1px rounded border
                Color borderColor = _isFocused
                    ? FluentTheme.InputBorderFocused
                    : (_isHovered ? FluentTheme.InputBorderHover : FluentTheme.InputBorder);

                var strokeRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                using var strokePath = FluentTheme.CreateRoundedPath(strokeRect, 3.5f);
                using var borderPen = new Pen(borderColor, 1f);
                g.DrawPath(borderPen, strokePath);
            }
        }
    }

    /// <summary>
    /// Modern Windows 11 CheckBox with 4px rounded box, smooth hover, and accent checkmark.
    /// Eliminates legacy Win32 gray beveled checkboxes.
    /// </summary>
    public class FluentCheckBox : CheckBox
    {
        private bool _isHovered = false;

        public FluentCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Font = FluentTheme.Font(9f);
            ForeColor = FluentTheme.TextPrimary;
            Height = 24;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (Parent != null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                g.FillRectangle(parentBrush, ClientRectangle);
            }

            // Checkbox glyph box (18x18, 4px corner radius)
            float boxSize = 18f;
            float boxY = (Height - boxSize) / 2f;
            var boxRect = new RectangleF(0.5f, boxY, boxSize, boxSize);

            if (Checked)
            {
                Color bg = _isHovered ? FluentTheme.AccentHover : FluentTheme.Accent;
                using var fillPath = FluentTheme.CreateRoundedPath(boxRect, 4f);
                using var brush = new SolidBrush(bg);
                g.FillPath(brush, fillPath);

                // Crisp White Checkmark Vector
                using var checkPen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(checkPen, 4.5f, boxY + 9f, 7.5f, boxY + 12.5f);
                g.DrawLine(checkPen, 7.5f, boxY + 12.5f, 13.5f, boxY + 5.5f);
            }
            else
            {
                Color bg = Color.White;
                Color border = _isHovered ? FluentTheme.InputBorderHover : FluentTheme.InputBorder;

                using var fillPath = FluentTheme.CreateRoundedPath(boxRect, 4f);
                using var bgBrush = new SolidBrush(bg);
                g.FillPath(bgBrush, fillPath);

                var strokeRect = new RectangleF(0.5f, boxY, boxSize, boxSize);
                using var strokePath = FluentTheme.CreateRoundedPath(strokeRect, 3.5f);
                using var borderPen = new Pen(border, 1f);
                g.DrawPath(borderPen, strokePath);
            }

            // Label text aligned to font baseline
            var textRect = new Rectangle(26, 0, Width - 26, Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            );
        }
    }

    /// <summary>
    /// Executive-grade auto-sizing status capsule with state-tinted background and pulse dot.
    /// Perfectly centered text and dot baseline with zero capsule distortion.
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

            using var g = CreateGraphics();
            var textSize = TextRenderer.MeasureText(g, _statusText, Font);
            int newWidth = Math.Max(120, textSize.Width + 38);

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

            if (Parent != null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                g.FillRectangle(parentBrush, ClientRectangle);
            }

            float radius = (Height - 1) / 2f;
            var fillRect = new RectangleF(0, 0, Width, Height);
            using var path = FluentTheme.CreateRoundedPath(fillRect, radius);

            using var bgBrush = new SolidBrush(_badgeBg);
            g.FillPath(bgBrush, path);

            var strokeRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var strokePath = FluentTheme.CreateRoundedPath(strokeRect, radius - 0.5f);
            using var borderPen = new Pen(_badgeBorder, 1f);
            g.DrawPath(borderPen, strokePath);

            // Centered dot
            float dotSize = 7.0f;
            float dotX = 12f;
            float dotY = (Height - dotSize) / 2.0f;
            using var dotBrush = new SolidBrush(_dotColor);
            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

            // Status Text
            var textRect = new Rectangle(26, 0, Width - 36, Height);
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
