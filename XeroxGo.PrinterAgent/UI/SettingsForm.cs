using System;
using System.Drawing;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private readonly Action<AppConfig> _onSaveCallback;

        private TextBox _txtApiUrl = null!;
        private TextBox _txtApiKey = null!;
        private ComboBox _cmbLogicalSlot = null!;
        private ComboBox _cmbPhysicalPrinters = null!;
        private NumericUpDown _numPollInterval = null!;
        private CheckBox _chkAutoStart = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Button _btnTestPrint = null!;
        private Button _btnRefreshPrinters = null!;

        public SettingsForm(AppConfig config, Action<AppConfig> onSaveCallback)
        {
            _config = config;
            _onSaveCallback = onSaveCallback;

            InitializeComponents();
            LoadValues();
        }

        private void InitializeComponents()
        {
            Text = "XeroxGo — Printer Agent Settings";
            Size = new Size(540, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 250, 252);

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };
            Controls.Add(mainPanel);

            // Title Header
            var lblTitle = new Label
            {
                Text = "🖨️ XeroxGo Printer Agent Setup",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(24, 18)
            };
            mainPanel.Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "Configure your cloud backend pairing and physical printer driver.",
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(25, 46)
            };
            mainPanel.Controls.Add(lblSubtitle);

            int currentY = 80;

            // 1. API URL
            var lblApi = new Label
            {
                Text = "Backend API URL:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(25, currentY),
                AutoSize = true
            };
            mainPanel.Controls.Add(lblApi);
            currentY += 22;

            _txtApiUrl = new TextBox
            {
                Location = new Point(25, currentY),
                Width = 470,
                Font = new Font("Segoe UI", 9.5f)
            };
            mainPanel.Controls.Add(_txtApiUrl);
            currentY += 36;

            // 2. Agent API Key (Bearer Auth)
            var lblKey = new Label
            {
                Text = "Agent Security Key (Optional / Bearer Token):",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(25, currentY),
                AutoSize = true
            };
            mainPanel.Controls.Add(lblKey);
            currentY += 22;

            _txtApiKey = new TextBox
            {
                Location = new Point(25, currentY),
                Width = 470,
                Font = new Font("Segoe UI", 9.5f),
                UseSystemPasswordChar = true
            };
            mainPanel.Controls.Add(_txtApiKey);
            currentY += 38;

            // 3. Logical Printer Slot (in XeroxGo)
            var lblLogical = new Label
            {
                Text = "Logical Slot in XeroxGo Dashboard:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(25, currentY),
                AutoSize = true
            };
            mainPanel.Controls.Add(lblLogical);
            currentY += 22;

            _cmbLogicalSlot = new ComboBox
            {
                Location = new Point(25, currentY),
                Width = 470,
                Font = new Font("Segoe UI", 9.5f)
            };
            _cmbLogicalSlot.Items.AddRange(new object[] {
                "Printer 1",
                "Printer 2",
                "Printer 3",
                "Printer 4",
                "Main B&W Counter",
                "Color Printer Counter"
            });
            mainPanel.Controls.Add(_cmbLogicalSlot);
            currentY += 38;

            // 4. Physical Windows Driver
            var lblPhysical = new Label
            {
                Text = "Local Physical Windows Printer:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(25, currentY),
                AutoSize = true
            };
            mainPanel.Controls.Add(lblPhysical);
            currentY += 22;

            _cmbPhysicalPrinters = new ComboBox
            {
                Location = new Point(25, currentY),
                Width = 350,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            mainPanel.Controls.Add(_cmbPhysicalPrinters);

            _btnRefreshPrinters = new Button
            {
                Text = "🔄 Refresh",
                Location = new Point(385, currentY - 1),
                Width = 110,
                Height = 29,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            _btnRefreshPrinters.Click += (s, e) => PopulatePhysicalPrinters();
            mainPanel.Controls.Add(_btnRefreshPrinters);
            currentY += 40;

            // 5. Polling Interval
            var lblPoll = new Label
            {
                Text = "Queue Poll Interval (seconds):",
                Location = new Point(25, currentY + 3),
                AutoSize = true
            };
            mainPanel.Controls.Add(lblPoll);

            _numPollInterval = new NumericUpDown
            {
                Location = new Point(240, currentY),
                Width = 80,
                Minimum = 1,
                Maximum = 60,
                Value = 3
            };
            mainPanel.Controls.Add(_numPollInterval);
            currentY += 36;

            // 6. Windows Startup Checkbox
            _chkAutoStart = new CheckBox
            {
                Text = "Start automatically when Windows boots up",
                Location = new Point(25, currentY),
                AutoSize = true
            };
            mainPanel.Controls.Add(_chkAutoStart);
            currentY += 40;

            // 7. Test Print Button
            _btnTestPrint = new Button
            {
                Text = "🖨️ Send Hardware Test Slip",
                Location = new Point(25, currentY),
                Width = 220,
                Height = 34,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            _btnTestPrint.Click += OnTestPrintClicked;
            mainPanel.Controls.Add(_btnTestPrint);
            currentY += 48;

            // 8. Action Buttons (Cancel & Save)
            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(275, currentY),
                Width = 100,
                Height = 36,
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.Click += (s, e) => Close();
            mainPanel.Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = "Save & Connect",
                Location = new Point(385, currentY),
                Width = 110,
                Height = 36,
                BackColor = Color.FromArgb(37, 99, 235), // Primary Blue
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;
            mainPanel.Controls.Add(_btnSave);
        }

        private void LoadValues()
        {
            _txtApiUrl.Text = _config.ApiUrl;
            _txtApiKey.Text = _config.AgentApiKey;
            _cmbLogicalSlot.Text = _config.LogicalPrinterName;
            _numPollInterval.Value = Math.Max(1, Math.Min(60, _config.PollIntervalSeconds));
            _chkAutoStart.Checked = _config.AutoStartWithWindows;

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

            // Select active configured physical printer
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
            _config.AutoStartWithWindows = _chkAutoStart.Checked;

            // Persist to disk
            _config.Save();

            // Update Registry auto-startup
            StartupManager.SetAutoStart(_config.AutoStartWithWindows);

            // Notify runtime
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
