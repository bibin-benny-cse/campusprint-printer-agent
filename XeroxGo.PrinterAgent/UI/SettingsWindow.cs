using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    /// <summary>
    /// Lightweight, modern Windows Forms configuration dialog.
    /// Uses native GDI+ rendering (zero WPF / DirectX overhead, ~5MB working set).
    /// </summary>
    public class SettingsWindow : Form
    {
        private readonly AppConfig _config;
        private readonly Action<AppConfig> _onSaveCallback;

        private TextBox _txtApiUrl = null!;
        private TextBox _txtApiKey = null!;
        private Button _btnToggleKey = null!;
        private bool _isKeyRevealed = false;

        private ComboBox _cmbLogicalSlot = null!;
        private ComboBox _cmbPhysicalPrinters = null!;

        private TrackBar _trackPoll = null!;
        private Label _lblPollValue = null!;
        private CheckBox _chkAutoStart = null!;

        private FluentStatusBadge _statusBadge = null!;
        private FluentButton _btnTestSlip = null!;
        private FluentButton _btnSave = null!;
        private FluentButton _btnCancel = null!;

        public SettingsWindow(AppConfig config, Action<AppConfig> onSaveCallback, string currentStatus = "Connected to Cloud", string statusState = "idle")
        {
            _config = config;
            _onSaveCallback = onSaveCallback;

            InitializeComponent(currentStatus, statusState);
            LoadConfiguration();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DwmApi.ApplyWindows11Styling(this.Handle);
        }

        private void InitializeComponent(string currentStatus, string statusState)
        {
            SuspendLayout();

            Text = "XeroxGo — Agent Settings";
            ClientSize = new Size(580, 690);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = FluentTheme.Background;
            Font = FluentTheme.Font(9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;

            int currentY = 16;
            int marginX = 24;
            int contentWidth = ClientSize.Width - (marginX * 2);

            // ==========================================
            // 1. Header (Logo + Title + Status Badge)
            // ==========================================
            var headerPanel = new Panel
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 52),
                BackColor = Color.Transparent
            };

            // Modern Blue Logo Icon
            var logoBox = new PictureBox
            {
                Location = new Point(0, 4),
                Size = new Size(44, 44),
                BackColor = Color.Transparent
            };
            logoBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, 43, 43);
                using var path = FluentTheme.CreateRoundedPath(rect, 10);
                using var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 103, 192), Color.FromArgb(37, 99, 235), 90);
                e.Graphics.FillPath(brush, path);

                // Simple White Printer Glyph
                using var whiteBrush = new SolidBrush(Color.White);
                using var whitePen = new Pen(Color.White, 2f);
                e.Graphics.FillRectangle(whiteBrush, 11, 20, 22, 13);
                e.Graphics.DrawRectangle(whitePen, 15, 11, 14, 8);
                using var blueBrush = new SolidBrush(Color.FromArgb(0, 103, 192));
                e.Graphics.FillRectangle(blueBrush, 14, 27, 16, 2);
            };
            headerPanel.Controls.Add(logoBox);

            // Title & Subtitle
            var lblTitle = new Label
            {
                Text = "XeroxGo Agent",
                Font = FluentTheme.Font(14f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(54, 4),
                AutoSize = true
            };
            var lblSubtitle = new Label
            {
                Text = "High-reliability counter printer daemon for Windows",
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(55, 28),
                AutoSize = true
            };
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);

            // Status Badge (Top Right)
            _statusBadge = new FluentStatusBadge
            {
                Location = new Point(contentWidth - 145, 12),
                Size = new Size(145, 28)
            };
            _statusBadge.SetStatus(currentStatus, statusState);
            headerPanel.Controls.Add(_statusBadge);

            Controls.Add(headerPanel);
            currentY += 62;

            // ==========================================
            // 2. Card 1: Cloud Backend
            // ==========================================
            var cardCloud = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 150),
                CornerRadius = 8
            };

            var lblCloudHeader = new Label
            {
                Text = "Cloud Backend",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(14, 12),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblCloudHeader);

            var lblApiUrl = new Label
            {
                Text = "API Base URL:",
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(14, 40),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblApiUrl);

            _txtApiUrl = new TextBox
            {
                Location = new Point(16, 58),
                Size = new Size(contentWidth - 32, 26),
                Font = FluentTheme.Font(9.5f)
            };
            cardCloud.Controls.Add(_txtApiUrl);

            var lblApiKey = new Label
            {
                Text = "Agent API Key / Secret:",
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(14, 90),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblApiKey);

            _txtApiKey = new TextBox
            {
                Location = new Point(16, 108),
                Size = new Size(contentWidth - 110, 26),
                Font = FluentTheme.Font(9.5f),
                UseSystemPasswordChar = true
            };
            cardCloud.Controls.Add(_txtApiKey);

            _btnToggleKey = new Button
            {
                Text = "Show",
                Location = new Point(contentWidth - 86, 107),
                Size = new Size(70, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = FluentTheme.Font(8.5f),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = FluentTheme.TextPrimary
            };
            _btnToggleKey.FlatAppearance.BorderColor = FluentTheme.CardBorder;
            _btnToggleKey.Click += OnToggleKeyVisibility;
            cardCloud.Controls.Add(_btnToggleKey);

            Controls.Add(cardCloud);
            currentY += 162;

            // ==========================================
            // 3. Card 2: Hardware & Routing
            // ==========================================
            var cardHardware = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 150),
                CornerRadius = 8
            };

            var lblHwHeader = new Label
            {
                Text = "Hardware & Routing",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(14, 12),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblHwHeader);

            var lblSlot = new Label
            {
                Text = "Logical Printer Slot (Cloud Station):",
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(14, 40),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblSlot);

            _cmbLogicalSlot = new ComboBox
            {
                Location = new Point(16, 58),
                Size = new Size(contentWidth - 32, 26),
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = FluentTheme.Font(9.5f)
            };
            _cmbLogicalSlot.Items.AddRange(new object[] { "Primary", "Secondary", "Counter 1", "Counter 2", "Color Station", "BW Station" });
            cardHardware.Controls.Add(_cmbLogicalSlot);

            var lblPhysical = new Label
            {
                Text = "Physical Windows Spooler Driver:",
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(14, 90),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblPhysical);

            _cmbPhysicalPrinters = new ComboBox
            {
                Location = new Point(16, 108),
                Size = new Size(contentWidth - 32, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = FluentTheme.Font(9.5f)
            };
            cardHardware.Controls.Add(_cmbPhysicalPrinters);

            Controls.Add(cardHardware);
            currentY += 162;

            // ==========================================
            // 4. Card 3: Operational Telemetry & System
            // ==========================================
            var cardSystem = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 140),
                CornerRadius = 8
            };

            var lblSysHeader = new Label
            {
                Text = "Operational Telemetry & System",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(14, 12),
                AutoSize = true
            };
            cardSystem.Controls.Add(lblSysHeader);

            _lblPollValue = new Label
            {
                Text = "Queue Poll Interval: 3 seconds",
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(14, 38),
                AutoSize = true
            };
            cardSystem.Controls.Add(_lblPollValue);

            _trackPoll = new TrackBar
            {
                Location = new Point(12, 58),
                Size = new Size(contentWidth - 24, 32),
                Minimum = 1,
                Maximum = 15,
                Value = 3,
                TickFrequency = 1,
                AutoSize = false
            };
            _trackPoll.ValueChanged += (s, e) =>
            {
                _lblPollValue.Text = $"Queue Poll Interval: {_trackPoll.Value} second{(_trackPoll.Value == 1 ? "" : "s")}";
            };
            cardSystem.Controls.Add(_trackPoll);

            _chkAutoStart = new CheckBox
            {
                Text = "Launch XeroxGo Agent automatically on Windows startup",
                Location = new Point(16, 98),
                Size = new Size(contentWidth - 32, 24),
                ForeColor = FluentTheme.TextPrimary,
                Cursor = Cursors.Hand
            };
            cardSystem.Controls.Add(_chkAutoStart);

            Controls.Add(cardSystem);
            currentY += 152;

            // ==========================================
            // 5. Footer Buttons
            // ==========================================
            var footerPanel = new Panel
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 42),
                BackColor = Color.Transparent
            };

            _btnTestSlip = new FluentButton
            {
                Text = "🖨️  Print Test Slip",
                Location = new Point(0, 4),
                Size = new Size(140, 34),
                IsPrimary = false
            };
            _btnTestSlip.Click += OnPrintTestSlip;
            footerPanel.Controls.Add(_btnTestSlip);

            _btnCancel = new FluentButton
            {
                Text = "Cancel",
                Location = new Point(contentWidth - 240, 4),
                Size = new Size(100, 34),
                IsPrimary = false
            };
            _btnCancel.Click += (s, e) => Close();
            footerPanel.Controls.Add(_btnCancel);

            _btnSave = new FluentButton
            {
                Text = "Save Changes",
                Location = new Point(contentWidth - 130, 4),
                Size = new Size(130, 34),
                IsPrimary = true
            };
            _btnSave.Click += OnSave;
            footerPanel.Controls.Add(_btnSave);

            Controls.Add(footerPanel);

            ResumeLayout(false);
        }

        private void LoadConfiguration()
        {
            _txtApiUrl.Text = _config.ApiUrl;
            _txtApiKey.Text = _config.AgentApiKey;
            _cmbLogicalSlot.Text = _config.LogicalPrinterName;

            // Populate installed printers
            _cmbPhysicalPrinters.Items.Clear();
            _cmbPhysicalPrinters.Items.Add("Auto (Detect Default)");

            var installed = HardwareMonitor.GetInstalledPrinters();
            foreach (var p in installed)
            {
                _cmbPhysicalPrinters.Items.Add(p);
            }

            if (string.IsNullOrWhiteSpace(_config.PhysicalPrinterName) || _config.PhysicalPrinterName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                _cmbPhysicalPrinters.SelectedIndex = 0;
            }
            else
            {
                int idx = _cmbPhysicalPrinters.Items.IndexOf(_config.PhysicalPrinterName);
                _cmbPhysicalPrinters.SelectedIndex = idx >= 0 ? idx : 0;
            }

            int pollVal = Math.Clamp(_config.PollIntervalSeconds, 1, 15);
            _trackPoll.Value = pollVal;
            _lblPollValue.Text = $"Queue Poll Interval: {pollVal} second{(pollVal == 1 ? "" : "s")}";

            _chkAutoStart.Checked = StartupManager.IsAutoStartEnabled();
        }

        private void OnToggleKeyVisibility(object? sender, EventArgs e)
        {
            _isKeyRevealed = !_isKeyRevealed;
            _txtApiKey.UseSystemPasswordChar = !_isKeyRevealed;
            _btnToggleKey.Text = _isKeyRevealed ? "Hide" : "Show";
        }

        private void OnPrintTestSlip(object? sender, EventArgs e)
        {
            string selected = _cmbPhysicalPrinters.SelectedItem?.ToString() ?? "Auto";
            string resolved = HardwareMonitor.ResolveActivePrinter(selected);

            try
            {
                PrintEngine.PrintDiagnosticSlip(resolved);
                MessageBox.Show(
                    $"Hardware diagnostic ticket sent to spooler:\n'{resolved}'",
                    "Diagnostic Print Sent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Diagnostic print failed:\n{ex.Message}",
                    "Spooler Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void OnSave(object? sender, EventArgs e)
        {
            string url = _txtApiUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show("Please enter a valid absolute HTTP/HTTPS API URL.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtApiUrl.Focus();
                return;
            }

            _config.ApiUrl = url;
            _config.AgentApiKey = _txtApiKey.Text.Trim();
            _config.LogicalPrinterName = string.IsNullOrWhiteSpace(_cmbLogicalSlot.Text) ? "Primary" : _cmbLogicalSlot.Text.Trim();

            string physicalSelection = _cmbPhysicalPrinters.SelectedItem?.ToString() ?? "Auto";
            _config.PhysicalPrinterName = physicalSelection.StartsWith("Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : physicalSelection;

            _config.PollIntervalSeconds = _trackPoll.Value;
            _config.AutoStartWithWindows = _chkAutoStart.Checked;

            // Apply autostart registry state
            StartupManager.SetAutoStart(_config.AutoStartWithWindows);

            // Save to disk and invoke runtime callback
            _config.Save();
            _onSaveCallback?.Invoke(_config);

            DialogResult = DialogResult.OK;
            Close();
        }

        public void UpdateStatus(string text, string state)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateStatus(text, state)));
                return;
            }
            _statusBadge.SetStatus(text, state);
        }

        public void UpdateStatusPill(string text, string state) => UpdateStatus(text, state);
    }
}
