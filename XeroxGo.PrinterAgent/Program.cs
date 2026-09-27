using System;
using System.Threading;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Services;
using XeroxGo.PrinterAgent.UI;

namespace XeroxGo.PrinterAgent
{
    internal static class Program
    {
        private const string MutexName = "Global\\XeroxGo_PrinterAgent_SingleInstance_Mutex";
        private static Mutex? _mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // 1. Single Instance Protection via Named OS Mutex
            _mutex = new Mutex(true, MutexName, out bool isOnlyInstance);
            if (!isOnlyInstance)
            {
                MessageBox.Show(
                    "XeroxGo Printer Agent is already running in your Windows taskbar system tray.",
                    "XeroxGo Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            // 2. Unhandled Exception Trapping (Prevents silent death)
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) =>
            {
                Logger.Error("Unhandled UI Thread Exception", e.Exception);
                NotificationService.ShowCritical("Unexpected Error", e.Exception.Message);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Logger.Error("Fatal AppDomain Exception", ex);
                    NotificationService.ShowCritical("Fatal Hardware Error", ex.Message);
                }
            };

            // 3. High-DPI and Modern Windows Styling (Per-Monitor V2 prevents blur on scaled displays)
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Initialize WPF Application Context for Fluent vector windows
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            try
            {
                Logger.Info("=================================================");
                Logger.Info("--- XeroxGo Windows Printer Agent (.NET 8) ---");
                Logger.Info($"Machine: {Environment.MachineName}, User: {Environment.UserName}");
                Logger.Info("=================================================");

                // 4. Launch System Tray Application
                Application.Run(new TrayApplicationContext());
            }
            catch (Exception ex)
            {
                Logger.Error("Fatal startup failure in Application.Run", ex);
                MessageBox.Show($"XeroxGo Agent crashed on startup: {ex.Message}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }
    }
}
