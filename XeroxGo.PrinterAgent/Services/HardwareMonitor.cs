using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Printing;

namespace XeroxGo.PrinterAgent.Services
{
    public class PrinterHealthState
    {
        public string PrinterName { get; set; } = "";
        public bool IsOnline { get; set; } = true;
        public bool IsPaperJammed { get; set; } = false;
        public bool IsOutOfPaper { get; set; } = false;
        public bool IsPaused { get; set; } = false;
        public bool IsInError { get; set; } = false;
        public string StatusSummary { get; set; } = "Idle";
        public string? ErrorMessage { get; set; }
    }

    public static class HardwareMonitor
    {
        private static readonly string[] VirtualKeywords = new[]
        {
            "microsoft print to pdf",
            "onenote",
            "fax",
            "xps document writer",
            "adobe pdf"
        };

        public static bool IsVirtualPrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return true;
            string lower = printerName.ToLowerInvariant();
            return VirtualKeywords.Any(k => lower.Contains(k));
        }

        public static List<string> GetInstalledPrinters()
        {
            var list = new List<string>();
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    if (!string.IsNullOrWhiteSpace(printer))
                    {
                        list.Add(printer);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to enumerate installed printers", ex);
            }
            return list;
        }

        public static string ResolveActivePrinter(string configuredName)
        {
            if (!string.IsNullOrWhiteSpace(configuredName) && !configuredName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return configuredName.Trim();
            }

            var all = GetInstalledPrinters();
            var physical = all.Where(p => !IsVirtualPrinter(p)).ToList();

            // Check default printer
            try
            {
                var settings = new PrinterSettings();
                string defaultPrinter = settings.PrinterName;
                if (!string.IsNullOrWhiteSpace(defaultPrinter) && !IsVirtualPrinter(defaultPrinter))
                {
                    Logger.Info($"[PRINTER DETECT] Auto-selected default physical printer: {defaultPrinter}");
                    return defaultPrinter;
                }
            }
            catch { }

            // If default was virtual, pick first physical printer
            if (physical.Count > 0)
            {
                Logger.Info($"[PRINTER DETECT] Auto-selected first physical printer: {physical[0]}");
                return physical[0];
            }

            // Fallback
            if (all.Count > 0)
            {
                Logger.Warn($"[PRINTER DETECT] No physical printer found. Falling back to: {all[0]}");
                return all[0];
            }

            return "Microsoft Print to PDF";
        }

        /// <summary>
        /// Inspects the native Windows Print Spooler for physical health telemetry.
        /// Fires critical notification only if an actionable failure (paper jam, out of paper) is detected.
        /// </summary>
        public static PrinterHealthState CheckPrinterHealth(string printerName)
        {
            var state = new PrinterHealthState { PrinterName = printerName };

            try
            {
                using var printServer = new LocalPrintServer();
                using var queue = printServer.GetPrintQueue(printerName);

                queue.Refresh();

                state.IsOnline = !queue.IsOffline;
                state.IsPaperJammed = queue.IsPaperJammed;
                state.IsOutOfPaper = queue.IsOutOfPaper;
                state.IsPaused = queue.IsPaused;
                state.IsInError = queue.IsInError;

                if (state.IsPaperJammed)
                {
                    state.StatusSummary = "Error";
                    state.ErrorMessage = $"Paper jam detected on printer '{printerName}'. Please clear the paper path.";
                    NotificationService.ShowError("Paper Jam", state.ErrorMessage);
                }
                else if (state.IsOutOfPaper)
                {
                    state.StatusSummary = "Error";
                    state.ErrorMessage = $"Printer '{printerName}' is out of paper. Please reload the tray.";
                    NotificationService.ShowError("Out of Paper", state.ErrorMessage);
                }
                else if (!state.IsOnline)
                {
                    state.StatusSummary = "Offline";
                    state.ErrorMessage = $"Printer '{printerName}' is offline or disconnected.";
                    NotificationService.ShowError("Printer Offline", state.ErrorMessage);
                }
                else if (state.IsPaused)
                {
                    state.StatusSummary = "Paused";
                }
                else if (queue.IsBusy || queue.NumberOfJobs > 0)
                {
                    state.StatusSummary = "Printing";
                }
                else
                {
                    state.StatusSummary = "Idle";
                }
            }
            catch (Exception ex)
            {
                // Print queue might not be accessible if it's a virtual printer or permissions issue
                state.StatusSummary = "Idle";
                state.IsOnline = true;
                Logger.Warn($"Could not query PrintQueue for '{printerName}': {ex.Message}");
            }

            return state;
        }
    }
}
