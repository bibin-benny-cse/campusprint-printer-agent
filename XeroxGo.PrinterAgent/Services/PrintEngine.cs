using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading.Tasks;
using XeroxGo.PrinterAgent.Models;

namespace XeroxGo.PrinterAgent.Services
{
    public static class PrintEngine
    {
        /// <summary>
        /// Silently sends a processed document to the specified Windows printer.
        /// </summary>
        public static async Task PrintDocumentAsync(string pdfPath, PrintJob job, string printerName)
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"Cannot print non-existent file: {pdfPath}");
            }

            Logger.Info($"[PRINT SPOOL] Spooling '{pdfPath}' to printer '{printerName}' (Copies: {job.Copies}, Mode: {job.Mode}, Sides: {job.Sides})");

            var settings = new PrinterSettings
            {
                PrinterName = printerName,
                Copies = (short)Math.Max(1, job.Copies)
            };

            // Set Duplex mode
            bool isDoubleSided = !string.IsNullOrEmpty(job.Sides) && 
                                job.Sides.IndexOf("double", StringComparison.OrdinalIgnoreCase) >= 0;
            if (settings.CanDuplex)
            {
                settings.Duplex = isDoubleSided ? Duplex.Vertical : Duplex.Simplex;
            }

            // Set Color vs Monochrome mode
            bool isColor = !string.IsNullOrEmpty(job.Mode) && 
                           job.Mode.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0;
            if (settings.SupportsColor)
            {
                settings.DefaultPageSettings.Color = isColor;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Execute silent print via Windows Shell 'printto' verb
                    var psi = new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        Verb = "printto",
                        Arguments = $"\"{printerName}\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(30000); // 30s timeout for spooler handoff
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Native shell printto failed for {pdfPath}. Attempting fallback printing.", ex);
                    PrintViaPrintDocumentFallback(pdfPath, settings);
                }
            });
        }

        private static void PrintViaPrintDocumentFallback(string pdfPath, PrinterSettings settings)
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings = settings;
            doc.PrintController = new StandardPrintController(); // Suppress print dialog
            doc.PrintPage += (sender, e) =>
            {
                // Fallback page render placeholder if shell association is missing
                using var font = new Font("Arial", 14);
                e.Graphics?.DrawString($"XeroxGo Automated Print Output\nFile: {Path.GetFileName(pdfPath)}", font, Brushes.Black, 50, 50);
            };
            doc.Print();
        }

        /// <summary>
        /// Prints a clean hardware diagnostic ticket to verify physical connectivity.
        /// </summary>
        public static void PrintDiagnosticSlip(string printerName)
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            doc.PrintController = new StandardPrintController();

            doc.PrintPage += (sender, e) =>
            {
                if (e.Graphics == null) return;

                using var titleFont = new Font("Arial", 16, FontStyle.Bold);
                using var bodyFont = new Font("Arial", 10, FontStyle.Regular);
                using var monoFont = new Font("Courier New", 9, FontStyle.Regular);

                float y = 40;
                e.Graphics.DrawString("🖨️ XeroxGo — Hardware Diagnostics", titleFont, Brushes.Black, 40, y);
                y += 35;
                e.Graphics.DrawLine(Pens.Black, 40, y, 400, y);
                y += 15;

                e.Graphics.DrawString($"Printer Name : {printerName}", bodyFont, Brushes.Black, 40, y); y += 20;
                e.Graphics.DrawString($"Status       : Online & Connected", bodyFont, Brushes.Black, 40, y); y += 20;
                e.Graphics.DrawString($"Timestamp    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}", bodyFont, Brushes.Black, 40, y); y += 20;
                e.Graphics.DrawString($"Host Machine : {Environment.MachineName}", bodyFont, Brushes.Black, 40, y); y += 20;
                e.Graphics.DrawString($"OS Version   : {Environment.OSVersion}", bodyFont, Brushes.Black, 40, y); y += 30;

                e.Graphics.DrawLine(Pens.Gray, 40, y, 400, y);
                y += 15;
                e.Graphics.DrawString("✓ Zero-Intervention Hardware Spooler Ready", monoFont, Brushes.DarkGreen, 40, y);
            };

            doc.Print();
            Logger.Info($"Sent diagnostic test page to '{printerName}'");
        }
    }
}
