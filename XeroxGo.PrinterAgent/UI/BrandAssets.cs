using System;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace XeroxGo.PrinterAgent.UI
{
    /// <summary>
    /// Centralized provider for official XeroxGo branding assets (Logo and Application Icons).
    /// Dynamically loads embedded high-resolution assets with thread-safe caching.
    /// </summary>
    public static class BrandAssets
    {
        private static Image? _logo;
        private static Image? _logoGlyph;
        private static Icon? _appIcon;
        private static readonly object _lock = new object();

        /// <summary>
        /// Official transparent XeroxGo emblem logo (1024x1024 centered).
        /// </summary>
        public static Image? Logo
        {
            get
            {
                if (_logo == null)
                {
                    lock (_lock)
                    {
                        if (_logo == null)
                        {
                            _logo = LoadEmbeddedImage("logo.png");
                        }
                    }
                }
                return _logo;
            }
        }

        /// <summary>
        /// Official tight-cropped transparent XeroxGo glyph (aspect ratio ~1.96).
        /// Ideal for header and inline display without wasted vertical padding.
        /// </summary>
        public static Image? LogoGlyph
        {
            get
            {
                if (_logoGlyph == null)
                {
                    lock (_lock)
                    {
                        if (_logoGlyph == null)
                        {
                            _logoGlyph = LoadEmbeddedImage("logo_glyph.png") ?? Logo;
                        }
                    }
                }
                return _logoGlyph;
            }
        }

        /// <summary>
        /// Official multi-resolution Windows application icon (16x16 up to 256x256).
        /// </summary>
        public static Icon? AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    lock (_lock)
                    {
                        if (_appIcon == null)
                        {
                            try
                            {
                                var assembly = Assembly.GetExecutingAssembly();
                                var resName = assembly.GetManifestResourceNames()
                                    .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));

                                if (!string.IsNullOrEmpty(resName))
                                {
                                    using var stream = assembly.GetManifestResourceStream(resName);
                                    if (stream != null)
                                    {
                                        _appIcon = new Icon(stream);
                                    }
                                }
                            }
                            catch
                            {
                                // Graceful fallback
                            }
                        }
                    }
                }
                return _appIcon;
            }
        }

        private static Image? LoadEmbeddedImage(string fileName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(resName))
                {
                    using var stream = assembly.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var temp = Image.FromStream(stream);
                        return new Bitmap(temp);
                    }
                }
            }
            catch
            {
                // Graceful fallback
            }
            return null;
        }
    }
}
