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
}
