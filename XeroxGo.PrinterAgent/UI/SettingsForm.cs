using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private readonly Action<AppConfig> _onSaveCallback;
        private readonly string _initialStatus;

        private FluentTextBox _txtApiUrl = null!;
        private FluentTextBox _txtApiKey = null!;
        private ComboBox _cmbLogicalSlot = null!;
        private ComboBox _cmbPhysicalPrinters = null!;
        private NumericUpDown _numPollInterval = null!;
        private FluentToggleSwitch _toggleAutoStart = null!;
        private Label _lblAutoStartStatus = null!;
        private FluentButton _btnSave = null!;
        private FluentButton _btnCancel = null!;
        private FluentButton _btnTestPrint = null!;
        private FluentButton _btnRefreshPrinters = null!;
        private FluentButton _btnViewLogs = null!;
        private FluentStatusBadge _statusBadge = null!;

        public SettingsForm(AppConfig config, Action<AppConfig> onSaveCallback, string currentStatus = "Connected to Cloud", string statusState = "idle")
        {
            _config = config;
            _onSaveCallback = onSaveCallback;
            _initialStatus = currentStatus;

            InitializeComponents(currentStatus, statusState);
            LoadValues();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DwmApi.ApplyWindows11Styling(this);
        }

        private void InitializeComponents(string currentStatus, string statusState)
        {
            Text = "XeroxGo — Printer Agent Settings";
            ClientSize = new Size(580, 680);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = FluentTheme.Background;
            Font = FluentTheme.Font(9.5f);

            var rootPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                AutoScroll = true
            };
            Controls.Add(rootPanel);

            // ==========================================
            // 1. Header (Icon + App Title + Status Pill)
            // ==========================================
            var headerPanel = new Panel
            {
                Location = new Point(24, 16),
                Size = new Size(532, 54),
                BackColor = Color.Transparent
            };
            rootPanel.Controls.Add(headerPanel);

            // Brand glyph
            var picLogo = new PictureBox
            {
                Size = new Size(36, 36),
                Location = new Point(0, 4),
                BackColor = Color.Transparent
            };
            picLogo.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, 35, 35);
                using var path = FluentTheme.CreateRoundedPath(rect, 8);
                using var brush = new SolidBrush(FluentTheme.Accent);
                e.Graphics.FillPath(brush, path);

                // Modern print glyph
                using var pen = new Pen(Color.White, 2f);
                e.Graphics.DrawRectangle(pen, 9, 13, 17, 13);
                e.Graphics.DrawLine(pen, 12, 9, 23, 9);
                e.Graphics.DrawLine(pen, 12, 9, 12, 13);
                e.Graphics.DrawLine(pen, 23, 9, 23, 13);
                e.Graphics.FillRectangle(Brushes.White, 13, 20, 9, 2);
            };
            headerPanel.Controls.Add(picLogo);

            var lblTitle = new Label
            {
                Text = "XeroxGo Printer Agent",
                Font = FluentTheme.Font(13f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(46, 2),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "Print shop counter pairing & hardware daemon",
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(48, 26),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblSubtitle);

            _statusBadge = new FluentStatusBadge
            {
                Location = new Point(362, 8),
                Size = new Size(170, 28)
            };
            _statusBadge.SetStatus(_initialStatus, statusState);
            headerPanel.Controls.Add(_statusBadge);

            int currentY = 80;

            // ==========================================
            // 2. Card: Cloud Backend
            // ==========================================
            var cardCloud = new FluentCard
            {
                Location = new Point(24, currentY),
                Size = new Size(532, 160)
            };
            rootPanel.Controls.Add(cardCloud);

            var lblCloudTitle = new Label
            {
                Text = "☁️  Cloud Backend Connection",
                Font = FluentTheme.Font(10f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(16, 14),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblCloudTitle);

            // Server URL
            var lblUrl = new Label
            {
                Text = "Server API Endpoint:",
                Font = FluentTheme.Font(8.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(16, 42),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblUrl);

            _txtApiUrl = new FluentTextBox
            {
                Location = new Point(16, 62),
                Size = new Size(500, 36)
            };
            cardCloud.Controls.Add(_txtApiUrl);

            // API Key
            var lblKey = new Label
            {
                Text = "Shop Agent API Key:",
                Font = FluentTheme.Font(8.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(16, 104),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblKey);

            _txtApiKey = new FluentTextBox(allowPasswordToggle: true)
            {
                Location = new Point(16, 122),
                Size = new Size(500, 36)
            };
            cardCloud.Controls.Add(_txtApiKey);

            currentY += 172;

            // ==========================================
            // 3. Card: Physical Hardware
            // ==========================================
            var cardHardware = new FluentCard
            {
                Location = new Point(24, currentY),
                Size = new Size(532, 196)
            };
            rootPanel.Controls.Add(cardHardware);

            var lblHardwareTitle = new Label
            {
                Text = "🖨️  Physical Hardware & Counter Mapping",
                Font = FluentTheme.Font(10f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(16, 14),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblHardwareTitle);

            // Dashboard slot
            var lblSlot = new Label
            {
                Text = "Logical Slot in XeroxGo Dashboard:",
                Font = FluentTheme.Font(8.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(16, 42),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblSlot);

            _cmbLogicalSlot = new ComboBox
            {
                Location = new Point(16, 62),
                Size = new Size(500, 32),
                Font = FluentTheme.Font(9.5f),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbLogicalSlot.Items.AddRange(new object[] {
                "Printer 1",
                "Printer 2",
                "Printer 3",
                "Printer 4",
                "Main B&W Counter",
                "Color Printer Counter"
            });
            cardHardware.Controls.Add(_cmbLogicalSlot);

            // Physical Windows Driver
            var lblPhysical = new Label
            {
                Text = "Local Physical Windows Printer:",
                Font = FluentTheme.Font(8.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(16, 102),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblPhysical);

            _cmbPhysicalPrinters = new ComboBox
            {
                Location = new Point(16, 122),
                Size = new Size(385, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = FluentTheme.Font(9.5f)
            };
            cardHardware.Controls.Add(_cmbPhysicalPrinters);

            _btnRefreshPrinters = new FluentButton
            {
                Text = "🔄 Refresh",
                Location = new Point(410, 120),
                Size = new Size(106, 32),
                Font = FluentTheme.Font(8.5f)
            };
            _btnRefreshPrinters.Click += (s, e) => PopulatePhysicalPrinters();
            cardHardware.Controls.Add(_btnRefreshPrinters);

            _btnTestPrint = new FluentButton
            {
                Text = "🖨️ Send Diagnostic Test Slip",
                Location = new Point(16, 160),
                Size = new Size(210, 30),
                Font = FluentTheme.Font(8.5f)
            };
            _btnTestPrint.Click += OnTestPrintClicked;
            cardHardware.Controls.Add(_btnTestPrint);

            currentY += 208;

            // ==========================================
            // 4. Card: Automation & Startup
            // ==========================================
            var cardAutomation = new FluentCard
            {
                Location = new Point(24, currentY),
                Size = new Size(532, 116)
            };
            rootPanel.Controls.Add(cardAutomation);

            var lblAutoTitle = new Label
            {
                Text = "⚡  Automation & Dispatch",
                Font = FluentTheme.Font(10f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(16, 12),
                AutoSize = true
            };
            cardAutomation.Controls.Add(lblAutoTitle);

            // Polling
            var lblPoll = new Label
            {
                Text = "Queue Polling Interval (seconds):",
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(16, 42),
                AutoSize = true
            };
            cardAutomation.Controls.Add(lblPoll);

            _numPollInterval = new NumericUpDown
            {
                Location = new Point(220, 39),
                Size = new Size(60, 26),
                Font = FluentTheme.Font(9.5f),
                Minimum = 1,
                Maximum = 60,
                Value = 3
            };
            cardAutomation.Controls.Add(_numPollInterval);

            // Toggle switch for Windows startup
            var lblStartup = new Label
            {
                Text = "Launch automatically on Windows startup",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(16, 78),
                AutoSize = true
            };
            cardAutomation.Controls.Add(lblStartup);

            _toggleAutoStart = new FluentToggleSwitch
            {
                Location = new Point(440, 76),
                Size = new Size(42, 22)
            };
            cardAutomation.Controls.Add(_toggleAutoStart);

            _lblAutoStartStatus = new Label
            {
                Text = "Enabled",
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(488, 79),
                AutoSize = true
            };
            cardAutomation.Controls.Add(_lblAutoStartStatus);

            _toggleAutoStart.CheckedChanged += (s, e) =>
            {
                _lblAutoStartStatus.Text = _toggleAutoStart.Checked ? "Active" : "Off";
                _lblAutoStartStatus.ForeColor = _toggleAutoStart.Checked ? FluentTheme.Accent : FluentTheme.TextMuted;
            };

            currentY += 128;

            // ==========================================
            // 5. Bottom Action Bar
            // ==========================================
            var actionPanel = new Panel
            {
                Location = new Point(24, currentY),
                Size = new Size(532, 44),
                BackColor = Color.Transparent
            };
            rootPanel.Controls.Add(actionPanel);

            _btnViewLogs = new FluentButton
            {
                Text = "📄 View Logs",
                Location = new Point(0, 4),
                Size = new Size(110, 36)
            };
            _btnViewLogs.Click += (s, e) =>
            {
                string path = Logger.GetLogFilePath();
                if (System.IO.File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("No log file found yet.", "XeroxGo Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            actionPanel.Controls.Add(_btnViewLogs);

            _btnCancel = new FluentButton
            {
                Text = "Cancel",
                Location = new Point(286, 4),
                Size = new Size(100, 36)
            };
            _btnCancel.Click += (s, e) => Close();
            actionPanel.Controls.Add(_btnCancel);

            _btnSave = new FluentButton
            {
                Text = "Save & Connect",
                IsPrimary = true,
                Location = new Point(396, 4),
                Size = new Size(136, 36),
                Font = FluentTheme.Font(9.5f, FontStyle.Bold)
            };
            _btnSave.Click += OnSaveClicked;
            actionPanel.Controls.Add(_btnSave);
        }

        private void LoadValues()
        {
            _txtApiUrl.Text = _config.ApiUrl;
            _txtApiKey.Text = _config.AgentApiKey;
            _cmbLogicalSlot.Text = _config.LogicalPrinterName;
            _numPollInterval.Value = Math.Max(1, Math.Min(60, _config.PollIntervalSeconds));
            _toggleAutoStart.Checked = _config.AutoStartWithWindows;
            _lblAutoStartStatus.Text = _config.AutoStartWithWindows ? "Active" : "Off";
            _lblAutoStartStatus.ForeColor = _config.AutoStartWithWindows ? FluentTheme.Accent : FluentTheme.TextMuted;

            PopulatePhysicalPrinters();
        }

        private void PopulatePhysicalPrinters()
        {
            _cmbPhysicalPrinters.Items.Clear();
            _cmbPhysicalPrinters.Items.Add("Auto (Auto-Detect Physical Windows Printer)");

            var printers = HardwareMonitor.GetInstalledPrinters();
            foreach (var p in printers)
            {
                string label = HardwareMonitor.IsVirtualPrinter(p) ? $"{p} [Virtual]" : p;
                _cmbPhysicalPrinters.Items.Add(label);
            }

            if (string.IsNullOrWhiteSpace(_config.PhysicalPrinterName) || _config.PhysicalPrinterName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                _cmbPhysicalPrinters.SelectedIndex = 0;
            }
            else
            {
                int idx = _cmbPhysicalPrinters.FindString(_config.PhysicalPrinterName);
                _cmbPhysicalPrinters.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }

        private void OnTestPrintClicked(object? sender, EventArgs e)
        {
            try
            {
                string selected = GetSelectedPhysicalPrinterName();
                string resolved = HardwareMonitor.ResolveActivePrinter(selected);

                PrintEngine.PrintDiagnosticSlip(resolved);
                MessageBox.Show(
                    $"Diagnostic test slip sent to physical printer:\n\"{resolved}\"\n\nPlease check your printer output tray.",
                    "Test Print Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to send test print: {ex.Message}",
                    "Test Print Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            string apiUrl = _txtApiUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                MessageBox.Show("Please enter a valid Backend API URL.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string logicalSlot = _cmbLogicalSlot.Text.Trim();
            if (string.IsNullOrWhiteSpace(logicalSlot))
            {
                logicalSlot = "Printer 1";
            }

            _config.ApiUrl = apiUrl;
            _config.AgentApiKey = _txtApiKey.Text.Trim();
            _config.LogicalPrinterName = logicalSlot;
            _config.PhysicalPrinterName = GetSelectedPhysicalPrinterName();
            _config.PollIntervalSeconds = (int)_numPollInterval.Value;
            _config.AutoStartWithWindows = _toggleAutoStart.Checked;

            _config.Save();
            StartupManager.SetAutoStart(_config.AutoStartWithWindows);
            _onSaveCallback?.Invoke(_config);

            MessageBox.Show(
                $"Configuration saved successfully!\nSlot: {logicalSlot}\nDriver: {_config.PhysicalPrinterName}\nAPI: {apiUrl}",
                "Settings Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            Close();
        }

        private string GetSelectedPhysicalPrinterName()
        {
            if (_cmbPhysicalPrinters.SelectedIndex <= 0) return "Auto";
            string raw = _cmbPhysicalPrinters.SelectedItem?.ToString() ?? "Auto";
            return raw.Replace(" [Virtual]", "").Trim();
        }
    }
}
