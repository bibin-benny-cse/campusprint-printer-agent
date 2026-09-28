using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;

namespace XeroxGo.PrinterAgent.Services
{
    public class DiscoveredPrinter
    {
        public string Name { get; set; } = "";
        public string DriverName { get; set; } = "";
        public bool IsDefault { get; set; }
        public bool IsOnline { get; set; } = true;
        public bool SupportsColor { get; set; } = false;
        public string Status { get; set; } = "Idle";
        public bool IsPaperJammed { get; set; } = false;
        public bool IsOutOfPaper { get; set; } = false;
        public bool IsPaused { get; set; } = false;
        public bool IsInError { get; set; } = false;
        public string? ErrorMessage { get; set; }
    }

    public class PrinterHealthState
    {
        public string PrinterName { get; set; } = "";
        public string? DriverName { get; set; }
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
        #region Win32 Spooler Native Interop (Zero COM Overhead)
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetPrinter(IntPtr hPrinter, int dwLevel, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PRINTER_INFO_2
        {
            public string pServerName;
            public string pPrinterName;
            public string pShareName;
            public string pPortName;
            public string pDriverName;
            public string pComment;
            public string pLocation;
            public IntPtr pDevMode;
            public string pSepFile;
            public string pPrintProcessor;
            public string pDatatype;
            public string pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        private const uint PRINTER_STATUS_PAUSED = 0x00000001;
        private const uint PRINTER_STATUS_ERROR = 0x00000002;
        private const uint PRINTER_STATUS_PAPER_JAM = 0x00000008;
        private const uint PRINTER_STATUS_PAPER_OUT = 0x00000010;
        private const uint PRINTER_STATUS_OFFLINE = 0x00000080;
        private const uint PRINTER_STATUS_BUSY = 0x00000200;
        private const uint PRINTER_STATUS_PRINTING = 0x00000400;
        private const uint PRINTER_STATUS_NOT_AVAILABLE = 0x00001000;
        private const uint PRINTER_STATUS_DOOR_OPEN = 0x00400000;

        private const uint PRINTER_ATTRIBUTE_WORK_OFFLINE = 0x00000400;
        #endregion

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

        /// <summary>
        /// Scans all physically connected printers, checks their Win32 spooler health,
        /// color capabilities, and Windows default status. Filters out virtual drivers.
        /// </summary>
        public static List<DiscoveredPrinter> DiscoverAllPrinters()
        {
            var discovered = new List<DiscoveredPrinter>();
            var allPrinters = GetInstalledPrinters();
            var physicalPrinters = allPrinters.Where(p => !IsVirtualPrinter(p)).ToList();

            string defaultPrinterName = "";
            try
            {
                var settings = new PrinterSettings();
                defaultPrinterName = settings.PrinterName ?? "";
            }
            catch { }

            // If no physical printers found at all, fall back to whatever is installed
            var targetList = physicalPrinters.Count > 0 ? physicalPrinters : allPrinters;

            foreach (var printerName in targetList)
            {
                try
                {
                    var health = CheckPrinterHealth(printerName);
                    bool isDefault = string.Equals(printerName, defaultPrinterName, StringComparison.OrdinalIgnoreCase);

                    bool supportsColor = false;
                    try
                    {
                        var s = new PrinterSettings { PrinterName = printerName };
                        supportsColor = s.SupportsColor;
                    }
                    catch { }

                    discovered.Add(new DiscoveredPrinter
                    {
                        Name = printerName,
                        DriverName = health.DriverName ?? printerName,
                        IsDefault = isDefault,
                        IsOnline = health.IsOnline,
                        SupportsColor = supportsColor,
                        Status = health.StatusSummary,
                        IsPaperJammed = health.IsPaperJammed,
                        IsOutOfPaper = health.IsOutOfPaper,
                        IsPaused = health.IsPaused,
                        IsInError = health.IsInError,
                        ErrorMessage = health.ErrorMessage
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to discover printer '{printerName}': {ex.Message}");
                }
            }

            return discovered;
        }

        /// <summary>
        /// Resolves the primary default physical printer for the system.
        /// </summary>
        public static string ResolveDefaultPrinter()
        {
            var all = GetInstalledPrinters();
            var physical = all.Where(p => !IsVirtualPrinter(p)).ToList();

            // Check default printer
            try
            {
                var settings = new PrinterSettings();
                string defaultPrinter = settings.PrinterName;
                if (!string.IsNullOrWhiteSpace(defaultPrinter) && !IsVirtualPrinter(defaultPrinter))
                {
                    return defaultPrinter;
                }
            }
            catch { }

            // First physical printer
            if (physical.Count > 0)
            {
                return physical[0];
            }

            // Fallback
            if (all.Count > 0)
            {
                return all[0];
            }

            return "Microsoft Print to PDF";
        }

        public static string ResolveActivePrinter(string? configuredName)
        {
            if (!string.IsNullOrWhiteSpace(configuredName) && !configuredName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return configuredName.Trim();
            }

            return ResolveDefaultPrinter();
        }

        /// <summary>
        /// Inspects the native Windows Print Spooler directly via winspool.drv.
        /// Zero COM memory allocations, zero WPF overhead.
        /// </summary>
        public static PrinterHealthState CheckPrinterHealth(string printerName)
        {
            var state = new PrinterHealthState { PrinterName = printerName };

            if (string.IsNullOrWhiteSpace(printerName) || IsVirtualPrinter(printerName))
            {
                state.StatusSummary = "Idle";
                state.IsOnline = true;
                return state;
            }

            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pPrinterInfo = IntPtr.Zero;

            try
            {
                if (OpenPrinter(printerName, out hPrinter, IntPtr.Zero) && hPrinter != IntPtr.Zero)
                {
                    GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int bytesNeeded);
                    if (bytesNeeded > 0)
                    {
                        pPrinterInfo = Marshal.AllocHGlobal(bytesNeeded);
                        if (GetPrinter(hPrinter, 2, pPrinterInfo, bytesNeeded, out _))
                        {
                            var info = Marshal.PtrToStructure<PRINTER_INFO_2>(pPrinterInfo);

                            state.DriverName = info.pDriverName;

                            bool isOffline = (info.Status & PRINTER_STATUS_OFFLINE) != 0 ||
                                             (info.Status & PRINTER_STATUS_NOT_AVAILABLE) != 0 ||
                                             (info.Attributes & PRINTER_ATTRIBUTE_WORK_OFFLINE) != 0;

                            state.IsOnline = !isOffline;
                            state.IsPaperJammed = (info.Status & PRINTER_STATUS_PAPER_JAM) != 0;
                            state.IsOutOfPaper = (info.Status & PRINTER_STATUS_PAPER_OUT) != 0;
                            state.IsPaused = (info.Status & PRINTER_STATUS_PAUSED) != 0;
                            state.IsInError = (info.Status & PRINTER_STATUS_ERROR) != 0 || (info.Status & PRINTER_STATUS_DOOR_OPEN) != 0;

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
                            else if ((info.Status & PRINTER_STATUS_PRINTING) != 0 || (info.Status & PRINTER_STATUS_BUSY) != 0 || info.cJobs > 0)
                            {
                                state.StatusSummary = "Printing";
                            }
                            else
                            {
                                state.StatusSummary = "Idle";
                            }

                            return state;
                        }
                    }
                }

                state.StatusSummary = "Idle";
                state.IsOnline = true;
            }
            catch (Exception ex)
            {
                state.StatusSummary = "Idle";
                state.IsOnline = true;
                Logger.Warn($"Could not query spooler for '{printerName}': {ex.Message}");
            }
            finally
            {
                if (pPrinterInfo != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pPrinterInfo);
                }
                if (hPrinter != IntPtr.Zero)
                {
                    ClosePrinter(hPrinter);
                }
            }

            return state;
        }
    }
}
