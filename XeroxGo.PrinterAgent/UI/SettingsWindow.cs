using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    /// <summary>
    /// Professional Windows 11 Fluent 2 configuration dialog for XeroxGo Agent.
    /// Uses native GDI+ rendering (zero WPF / DirectX overhead, ~10MB working set).
    /// Features Zero-Config Multi-Printer Auto-Discovery.
    /// </summary>
    public class SettingsWindow : Form
    {
        private readonly AppConfig _config;
        private readonly Action<AppConfig> _onSaveCallback;
        private readonly Font _headerTitleFont = FluentTheme.Font(12.5f, FontStyle.Bold);
        private readonly Font _headerSubFont = FluentTheme.Font(8.5f, FontStyle.Regular);

        private FluentTextBox _txtApiUrl = null!;
        private FluentTextBox _txtApiKey = null!;
        private FluentButton _btnToggleKey = null!;
        private bool _isKeyRevealed = false;

        private Panel _panelPrinters = null!;
        private FluentButton _btnRefreshPrinters = null!;

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _headerTitleFont.Dispose();
                _headerSubFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent(string currentStatus, string statusState)
        {
            SuspendLayout();

            Text = string.Empty;
            ShowIcon = false;
            ClientSize = new Size(560, 660);
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
                Size = new Size(contentWidth, 40),
                BackColor = FluentTheme.Background
            };

            _headerPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int logoSize = 34;
                int logoY = 3;
                int textX = logoSize + 12;

                if (BrandAssets.LogoGlyph != null)
                {
                    g.DrawImage(BrandAssets.LogoGlyph, new Rectangle(0, logoY, logoSize, logoSize));
                }

                float titleY = logoY;
                g.DrawString("XeroxGo Agent", _headerTitleFont, new SolidBrush(FluentTheme.TextPrimary), textX, titleY);

                float subY = logoY + 18f;
                g.DrawString("By Unnamed Enterprises", _headerSubFont, new SolidBrush(FluentTheme.TextSecondary), textX, subY);
            };

            _statusBadge = new FluentStatusBadge
            {
                Size = new Size(130, 26)
            };
            _statusBadge.SetStatus(currentStatus, statusState);
            _statusBadge.Location = new Point(contentWidth - _statusBadge.Width, 5);
            _headerPanel.Controls.Add(_statusBadge);

            Controls.Add(_headerPanel);
            currentY += 46;

            // ==========================================
            // 2. Card 1: Cloud Connection & Pairing
            // ==========================================
            var cardCloud = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 172)
            };

            var lblCloudHeader = new Label
            {
                Text = "Cloud Connection & Store Pairing",
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
                Text = "Store Agent API Key / Token",
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
            // 3. Card 2: Connected Hardware & Discovery (Zero-Config)
            // ==========================================
            var cardHardware = new FluentCard
            {
                Location = new Point(marginX, currentY),
                Size = new Size(contentWidth, 190)
            };

            var lblHwHeader = new Label
            {
                Text = "Connected Printers (Auto-Discovered)",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(18, 14),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblHwHeader);

            _btnRefreshPrinters = new FluentButton
            {
                Text = "Refresh",
                Location = new Point(contentWidth - 18 - 84, 10),
                Size = new Size(84, 26),
                IsPrimary = false
            };
            _btnRefreshPrinters.Click += (s, e) => PopulateDiscoveredPrinters();
            cardHardware.Controls.Add(_btnRefreshPrinters);

            var lblHwSub = new Label
            {
                Text = "Zero setup needed. Connected printers are auto-synced with your XeroxGo dashboard.",
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 38),
                AutoSize = true
            };
            cardHardware.Controls.Add(lblHwSub);

            _panelPrinters = new Panel
            {
                Location = new Point(18, 60),
                Size = new Size(contentWidth - 36, 116),
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            cardHardware.Controls.Add(_panelPrinters);

            Controls.Add(cardHardware);
            currentY += 202;

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
                Text = "Operational Settings",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(18, 14),
                AutoSize = true
            };
            cardSystem.Controls.Add(lblSysHeader);

            var lblPoll = new Label
            {
                Text = "Queue Check Interval",
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
                "1 second (Ultra-responsive / Kiosks)",
                "2 seconds (Fast queue polling)",
                "3 seconds (Recommended - Balanced)",
                "5 seconds (Power saving)",
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

            // Discover and populate physical printers
            PopulateDiscoveredPrinters();

            // Map configured interval to dropdown
            int pollVal = _config.PollIntervalSeconds;
            int selectedIdx = 2; // Default to 3 seconds
            for (int i = 0; i < _cmbPollInterval.Items.Count; i++)
            {
                if (_cmbPollInterval.Items[i]?.ToString()?.StartsWith($"{pollVal} second") == true)
                {
                    selectedIdx = i;
                    break;
                }
            }
            _cmbPollInterval.SelectedIndex = selectedIdx;

            _chkAutoStart.Checked = StartupManager.IsAutoStartEnabled();
        }

        private void PopulateDiscoveredPrinters()
        {
            _panelPrinters.Controls.Clear();
            var discovered = HardwareMonitor.DiscoverAllPrinters();

            if (discovered.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No physical printers detected. Plug in a USB or network printer and click Refresh.",
                    Font = FluentTheme.Font(9f),
                    ForeColor = FluentTheme.TextSecondary,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _panelPrinters.Controls.Add(lblEmpty);
                return;
            }

            int itemY = 4;
            int itemWidth = _panelPrinters.ClientSize.Width - 8;

            foreach (var printer in discovered)
            {
                var rowPanel = new Panel
                {
                    Location = new Point(4, itemY),
                    Size = new Size(itemWidth, 34),
                    BackColor = Color.White
                };

                rowPanel.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    // Status dot
                    Color dotColor = printer.IsOnline 
                        ? (printer.IsPaperJammed || printer.IsOutOfPaper ? Color.FromArgb(239, 68, 68) : Color.FromArgb(16, 185, 129))
                        : Color.FromArgb(156, 163, 175);

                    using var dotBrush = new SolidBrush(dotColor);
                    g.FillEllipse(dotBrush, 10, 12, 10, 10);

                    // Printer Name
                    using var nameFont = FluentTheme.Font(9.5f, FontStyle.Bold);
                    using var nameBrush = new SolidBrush(FluentTheme.TextPrimary);
                    g.DrawString(printer.Name, nameFont, nameBrush, 28, 8);

                    // Badges (Color / Default / Status)
                    string badgeText = printer.SupportsColor ? "Color" : "B&W";
                    if (printer.IsDefault) badgeText += " • Default";
                    if (printer.IsPaperJammed) badgeText += " • JAMMED";
                    else if (printer.IsOutOfPaper) badgeText += " • OUT OF PAPER";
                    else if (!printer.IsOnline) badgeText += " • Offline";

                    using var badgeFont = FluentTheme.Font(8f);
                    using var badgeBrush = new SolidBrush(FluentTheme.TextSecondary);
                    var badgeSize = g.MeasureString(badgeText, badgeFont);
                    g.DrawString(badgeText, badgeFont, badgeBrush, rowPanel.Width - badgeSize.Width - 10, 10);
                };

                _panelPrinters.Controls.Add(rowPanel);
                itemY += 38;
            }
        }

        private void OnToggleKeyVisibility(object? sender, EventArgs e)
        {
            _isKeyRevealed = !_isKeyRevealed;
            _txtApiKey.UseSystemPasswordChar = !_isKeyRevealed;
            _btnToggleKey.Text = _isKeyRevealed ? "Hide" : "Show";
        }

        private void OnPrintTestSlip(object? sender, EventArgs e)
        {
            string resolved = HardwareMonitor.ResolveDefaultPrinter();

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
                _statusBadge.Location = new Point(contentWidth - _statusBadge.Width, 5);
            }
        }

        public void UpdateStatusPill(string text, string state) => UpdateStatus(text, state);
    }
}
