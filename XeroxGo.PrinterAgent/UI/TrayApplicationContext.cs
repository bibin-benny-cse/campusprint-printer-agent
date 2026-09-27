using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _statusHeaderItem;
        private readonly ToolStripMenuItem _pauseResumeItem;
        private readonly AppConfig _config;
        private readonly QueueWorker _worker;
        private SettingsWindow? _settingsWindow;

        private string _currentStatusText = "Connecting...";
        private string _currentStatusState = "idle";

        public TrayApplicationContext()
        {
            _config = AppConfig.Load();

            // Set up Windows 11 Fluent context menu
            _contextMenu = new ContextMenuStrip
            {
                Renderer = new FluentContextMenuRenderer(),
                Font = FluentTheme.Font(9.5f),
                ShowImageMargin = false
            };

            _statusHeaderItem = new ToolStripMenuItem("● XeroxGo: Connecting...")
            {
                Enabled = false,
                Font = FluentTheme.Font(9.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary
            };
            _contextMenu.Items.Add(_statusHeaderItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _pauseResumeItem = new ToolStripMenuItem("⏸️  Pause Printing", null, OnTogglePause);
            _contextMenu.Items.Add(_pauseResumeItem);

            _contextMenu.Items.Add(new ToolStripMenuItem("⚙️  Printer Settings & Pairing...", null, OnOpenSettings));
            _contextMenu.Items.Add(new ToolStripMenuItem("📄  View Activity Logs", null, OnViewLogs));
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(new ToolStripMenuItem("❌  Exit Agent", null, OnExit));

            // Set up NotifyIcon
            _trayIcon = new NotifyIcon
            {
                Icon = CreateStatusIcon(Color.FromArgb(59, 130, 246)), // Default Blue
                ContextMenuStrip = _contextMenu,
                Text = "XeroxGo Agent",
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => OnOpenSettings(s, e);

            // Initialize notification service strictly for errors
            NotificationService.Initialize(_trayIcon);

            // Initialize & start worker
            _worker = new QueueWorker(_config);
            _worker.StatusChanged += OnWorkerStatusChanged;
            _worker.Start();

            // Sync startup registry
            if (_config.AutoStartWithWindows != StartupManager.IsAutoStartEnabled())
            {
                StartupManager.SetAutoStart(_config.AutoStartWithWindows);
            }

            // Immediately flush startup/JIT memory pages to maintain minimal RAM footprint (~15-25 MB)
            MemoryOptimizer.TrimMemory();
        }

        private void OnWorkerStatusChanged(string status, string printerName)
        {
            if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
            {
                _trayIcon.ContextMenuStrip.BeginInvoke(new Action(() => OnWorkerStatusChanged(status, printerName)));
                return;
            }

            Color iconColor;
            string symbol;
            _currentStatusState = status.ToLowerInvariant();

            switch (_currentStatusState)
            {
                case "printing":
                    iconColor = Color.FromArgb(16, 185, 129); // Vibrant Green
                    symbol = "🔵 Printing";
                    _currentStatusText = "Actively Printing";
                    break;
                case "paused":
                    iconColor = Color.FromArgb(245, 158, 11); // Amber
                    symbol = "🟡 Paused";
                    _currentStatusText = "Printing Paused";
                    break;
                case "error":
                case "offline":
                    iconColor = Color.FromArgb(239, 68, 68); // Red
                    symbol = "🔴 " + status;
                    _currentStatusText = status.ToUpperInvariant();
                    break;
                default:
                    iconColor = Color.FromArgb(59, 130, 246); // Blue
                    symbol = "🟢 Connected";
                    _currentStatusText = "Connected to Cloud";
                    break;
            }

            _trayIcon.Icon = CreateStatusIcon(iconColor);
            _statusHeaderItem.Text = $"{symbol} ({printerName})";
            _trayIcon.Text = $"XeroxGo: {status} ({printerName})".Substring(0, Math.Min(63, $"XeroxGo: {status} ({printerName})".Length));

            // Sync open SettingsWindow in real-time
            if (_settingsWindow != null && !_settingsWindow.IsDisposed && _settingsWindow.Visible)
            {
                _settingsWindow.UpdateStatus(_currentStatusText, _currentStatusState);
            }
        }

        private void OnTogglePause(object? sender, EventArgs e)
        {
            _worker.IsPaused = !_worker.IsPaused;
            _pauseResumeItem.Text = _worker.IsPaused ? "▶️  Resume Printing" : "⏸️  Pause Printing";
        }

        private void OnOpenSettings(object? sender, EventArgs e)
        {
            if (_settingsWindow == null || _settingsWindow.IsDisposed)
            {
                _settingsWindow = new SettingsWindow(
                    _config,
                    (newConfig) =>
                    {
                        _worker.ReloadConfiguration(newConfig);
                    },
                    _currentStatusText,
                    _currentStatusState
                );
                _settingsWindow.FormClosed += (s, ev) =>
                {
                    _settingsWindow = null;
                    MemoryOptimizer.TrimMemory();
                };
            }

            if (_settingsWindow.WindowState == FormWindowState.Minimized)
            {
                _settingsWindow.WindowState = FormWindowState.Normal;
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void OnViewLogs(object? sender, EventArgs e)
        {
            string path = Logger.GetLogFilePath();
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("No log file found yet.", "XeroxGo Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _worker.Dispose();
            Application.Exit();
        }

        private static Icon CreateStatusIcon(Color ringColor)
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Draw modern round badge
                using var brush = new SolidBrush(ringColor);
                g.FillEllipse(brush, 2, 2, 28, 28);

                // Draw printer glyph outline in white
                using var pen = new Pen(Color.White, 2.5f);
                g.DrawRectangle(pen, 8, 12, 16, 12);
                g.DrawLine(pen, 11, 8, 21, 8);
                g.DrawLine(pen, 11, 8, 11, 12);
                g.DrawLine(pen, 21, 8, 21, 12);
                g.FillRectangle(Brushes.White, 12, 18, 8, 2);
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trayIcon.Dispose();
                _contextMenu.Dispose();
                _worker.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
