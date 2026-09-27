using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    /// <summary>
    /// Professional Windows 11 Fluent 2 configuration dialog for XeroxGo Agent.
    /// Uses native GDI+ rendering (zero WPF / DirectX overhead, ~10MB working set).
    /// </summary>
    public class SettingsWindow : Form
    {
        private readonly AppConfig _config;
        private readonly Action<AppConfig> _onSaveCallback;

        private FluentTextBox _txtApiUrl = null!;
        private FluentTextBox _txtApiKey = null!;
        private FluentButton _btnToggleKey = null!;
        private bool _isKeyRevealed = false;

        private FluentComboBox _cmbLogicalSlot = null!;
        private FluentComboBox _cmbPhysicalPrinters = null!;

        private FluentComboBox _cmbPollInterval = null!;
        private FluentCheckBox _chkAutoStart = null!;

        private FluentStatusBadge _statusBadge = null!;
        private FluentButton _btnTestSlip = null!;
        private FluentButton _btnSave = null!;
        private FluentButton _btnCancel = null!;
        private Panel _headerPanel = null!;

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

            Text = string.Empty;
            ShowIcon = false;
            ClientSize = new Size(560, 638);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = FluentTheme.Background;
            Font = FluentTheme.Font(9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;

            if (BrandAssets.AppIcon != null)
            {
                Icon = BrandAssets.AppIcon;
            }

            int marginX = 24;
            int contentWidth = ClientSize.Width - (marginX * 2);
            int currentY = 2;

            // ==========================================
            // 1. Header (Logo + Title + Status Pill)
            // ==========================================
            _headerPanel = new Panel
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 36),
                BackColor = FluentTheme.Background
            };

            // Official XeroxGo Logo (Transparent Emblem)
            var logoBox = new PictureBox
            {
                Location = new Point(0, 2),
                Size = new Size(63, 32),
                BackColor = Color.Transparent
            };
            logoBox.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var bgBrush = new SolidBrush(FluentTheme.Background))
                {
                    g.FillRectangle(bgBrush, logoBox.ClientRectangle);
                }

                var logo = BrandAssets.LogoGlyph ?? BrandAssets.Logo;
                if (logo != null)
                {
                    g.DrawImage(logo, new Rectangle(0, 0, logoBox.Width, logoBox.Height));
                }
            };
            _headerPanel.Controls.Add(logoBox);

            // Title
            var lblTitle = new Label
            {
                Text = "XeroxGo Agent",
                Font = FluentTheme.Font(14f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(74, 6),
                AutoSize = true
            };
            _headerPanel.Controls.Add(lblTitle);

            // Status Badge (Top Right)
            _statusBadge = new FluentStatusBadge();
            _statusBadge.SetStatus(currentStatus, statusState);
            _statusBadge.Location = new Point(contentWidth - _statusBadge.Width, 4);
            _headerPanel.Controls.Add(_statusBadge);

            Controls.Add(_headerPanel);
            currentY += 48;

            // ==========================================
            // 2. Card 1: Cloud Backend
            // ==========================================
            var cardCloud = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 172)
            };

            var lblCloudHeader = new Label
            {
                Text = "Cloud Backend",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(18, 14),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblCloudHeader);

            var lblApiUrl = new Label
            {
                Text = "API Base URL",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 40),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblApiUrl);

            _txtApiUrl = new FluentTextBox
            {
                Location = new Point(18, 60),
                Size = new Size(contentWidth - 36, 32)
            };
            cardCloud.Controls.Add(_txtApiUrl);

            var lblApiKey = new Label
            {
                Text = "Agent API Key / Secret",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 102),
                AutoSize = true
            };
            cardCloud.Controls.Add(lblApiKey);

            int btnWidth = 76;
            _txtApiKey = new FluentTextBox
            {
                Location = new Point(18, 122),
                Size = new Size(contentWidth - 36 - btnWidth - 8, 32),
                UseSystemPasswordChar = true
            };
            cardCloud.Controls.Add(_txtApiKey);

            _btnToggleKey = new FluentButton
            {
                Text = "Show",
                Location = new Point(contentWidth - 18 - btnWidth, 122),
                Size = new Size(btnWidth, 32),
                IsPrimary = false
            };
            _btnToggleKey.Click += OnToggleKeyVisibility;
            cardCloud.Controls.Add(_btnToggleKey);

            Controls.Add(cardCloud);
            currentY += 184;

            // ==========================================
            // 3. Card 2: Hardware & Routing
            // ==========================================
            var cardHardware = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 172)
            };

            var lblHwHeader = new Label
            {
                Text = "Hardware & Routing",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(18, 14),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblHwHeader);

            var lblSlot = new Label
            {
                Text = "Logical Printer Slot (Cloud Station)",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 40),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblSlot);

            _cmbLogicalSlot = new FluentComboBox
            {
                Location = new Point(18, 60),
                Size = new Size(contentWidth - 36, 32),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbLogicalSlot.Items.AddRange(new object[] { "Primary", "Secondary", "Counter 1", "Counter 2", "Color Station", "BW Station" });
            cardHardware.Controls.Add(_cmbLogicalSlot);

            var lblPhysical = new Label
            {
                Text = "Physical Windows Spooler Driver",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 102),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblPhysical);

            _cmbPhysicalPrinters = new FluentComboBox
            {
                Location = new Point(18, 122),
                Size = new Size(contentWidth - 36, 32),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cardHardware.Controls.Add(_cmbPhysicalPrinters);

            Controls.Add(cardHardware);
            currentY += 184;

            // ==========================================
            // 4. Card 3: Operational Telemetry
            // ==========================================
            var cardSystem = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 144)
            };

            var lblSysHeader = new Label
            {
                Text = "Operational Telemetry",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(18, 14),
                AutoSize = true
            };
            cardSystem.Controls.Add(lblSysHeader);

            var lblPoll = new Label
            {
                Text = "Queue Poll Interval",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 40),
                AutoSize = true
            };
            cardSystem.Controls.Add(lblPoll);

            _cmbPollInterval = new FluentComboBox
            {
                Location = new Point(18, 60),
                Size = new Size(contentWidth - 36, 32),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbPollInterval.Items.AddRange(new object[]
            {
                "1 second (High speed / Kiosk testing)",
                "2 seconds (Fast queue polling)",
                "3 seconds (Recommended - Responsive)",
                "5 seconds (Balanced - Power saving)",
                "10 seconds (Low bandwidth)",
                "15 seconds (Minimal polling)"
            });
            cardSystem.Controls.Add(_cmbPollInterval);

            _chkAutoStart = new FluentCheckBox
            {
                Text = "Launch XeroxGo Agent automatically on Windows startup",
                Font = FluentTheme.Font(9f),
                Location = new Point(18, 104),
                Size = new Size(contentWidth - 36, 24)
            };
            cardSystem.Controls.Add(_chkAutoStart);

            Controls.Add(cardSystem);
            currentY += 156;

            // ==========================================
            // 5. Footer Action Bar
            // ==========================================
            var footerPanel = new Panel
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 36),
                BackColor = FluentTheme.Background
            };

            _btnTestSlip = new FluentButton
            {
                Text = "Print Test Slip",
                HasPrinterIcon = true,
                Location = new Point(0, 0),
                Size = new Size(148, 34),
                IsPrimary = false
            };
            _btnTestSlip.Click += OnPrintTestSlip;
            footerPanel.Controls.Add(_btnTestSlip);

            _btnCancel = new FluentButton
            {
                Text = "Cancel",
                Location = new Point(contentWidth - 228, 0),
                Size = new Size(96, 34),
                IsPrimary = false
            };
            _btnCancel.Click += (s, e) => Close();
            footerPanel.Controls.Add(_btnCancel);

            _btnSave = new FluentButton
            {
                Text = "Save Changes",
                Location = new Point(contentWidth - 122, 0),
                Size = new Size(122, 34),
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

            // Logical Slot selection
            string slot = string.IsNullOrWhiteSpace(_config.LogicalPrinterName) ? "Primary" : _config.LogicalPrinterName;
            if (!_cmbLogicalSlot.Items.Contains(slot))
            {
                _cmbLogicalSlot.Items.Add(slot);
            }
            _cmbLogicalSlot.SelectedItem = slot;
            if (_cmbLogicalSlot.SelectedIndex < 0 && _cmbLogicalSlot.Items.Count > 0)
            {
                _cmbLogicalSlot.SelectedIndex = 0;
            }

            // Populate installed physical printers
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

            // Map configured interval to dropdown
            int pollVal = _config.PollIntervalSeconds;
            int selectedIdx = 2; // Default to 3 seconds
            for (int i = 0; i < _cmbPollInterval.Items.Count; i++)
            {
                if (_cmbPollInterval.Items[i].ToString()!.StartsWith($"{pollVal} second"))
                {
                    selectedIdx = i;
                    break;
                }
            }
            _cmbPollInterval.SelectedIndex = selectedIdx;

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
            _config.LogicalPrinterName = _cmbLogicalSlot.SelectedItem?.ToString() ?? "Primary";

            string physicalSelection = _cmbPhysicalPrinters.SelectedItem?.ToString() ?? "Auto";
            _config.PhysicalPrinterName = physicalSelection.StartsWith("Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : physicalSelection;

            // Parse selected poll interval
            int pollSeconds = 3;
            if (_cmbPollInterval.SelectedItem is string sel)
            {
                if (sel.StartsWith("1 second")) pollSeconds = 1;
                else if (sel.StartsWith("2 seconds")) pollSeconds = 2;
                else if (sel.StartsWith("3 seconds")) pollSeconds = 3;
                else if (sel.StartsWith("5 seconds")) pollSeconds = 5;
                else if (sel.StartsWith("10 seconds")) pollSeconds = 10;
                else if (sel.StartsWith("15 seconds")) pollSeconds = 15;
            }
            _config.PollIntervalSeconds = pollSeconds;

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
            if (_headerPanel != null)
            {
                int contentWidth = ClientSize.Width - (24 * 2);
                _statusBadge.Location = new Point(contentWidth - _statusBadge.Width, 13);
            }
        }

        public void UpdateStatusPill(string text, string state) => UpdateStatus(text, state);
    }
}
