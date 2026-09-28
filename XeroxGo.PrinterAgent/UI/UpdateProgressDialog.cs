using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    public class UpdateProgressDialog : Form
    {
        private readonly UpdateInfo _updateInfo;
        private readonly CancellationTokenSource _cts = new();
        private readonly ProgressBar _progressBar;
        private readonly Label _lblStatus;
        private readonly Label _lblPercent;
        private readonly FluentButton _btnCancel;

        public UpdateProgressDialog(UpdateInfo updateInfo)
        {
            _updateInfo = updateInfo;

            Text = "XeroxGo Agent Update";
            ClientSize = new Size(420, 160);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = FluentTheme.Background;
            Font = FluentTheme.Font(9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowInTaskbar = true;

            if (BrandAssets.AppIcon != null)
            {
                Icon = BrandAssets.AppIcon;
            }

            // Header title
            var lblTitle = new Label
            {
                Text = $"Downloading XeroxGo Agent v{updateInfo.VersionString}...",
                Font = FluentTheme.Font(10.5f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(24, 18),
                AutoSize = true
            };
            Controls.Add(lblTitle);

            // Subtitle / Status
            _lblStatus = new Label
            {
                Text = "Connecting to update server...",
                Font = FluentTheme.Font(9f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(24, 44),
                AutoSize = true
            };
            Controls.Add(_lblStatus);

            // Percentage label (Top Right)
            _lblPercent = new Label
            {
                Text = "0%",
                Font = FluentTheme.Font(9.5f, FontStyle.Bold),
                ForeColor = FluentTheme.Accent,
                Location = new Point(340, 44),
                Size = new Size(56, 18),
                TextAlign = ContentAlignment.TopRight
            };
            Controls.Add(_lblPercent);

            // Progress Bar
            _progressBar = new ProgressBar
            {
                Location = new Point(24, 72),
                Size = new Size(372, 22),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            Controls.Add(_progressBar);

            // Cancel button
            _btnCancel = new FluentButton
            {
                Text = "Cancel",
                Location = new Point(306, 110),
                Size = new Size(90, 32),
                IsPrimary = false
            };
            _btnCancel.Click += (s, e) =>
            {
                _cts.Cancel();
                Close();
            };
            Controls.Add(_btnCancel);

            FormClosing += (s, e) =>
            {
                _cts.Cancel();
            };

            Shown += (s, e) => StartDownload();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DwmApi.ApplyWindows11Styling(Handle);
        }

        private async void StartDownload()
        {
            var progress = new Progress<int>(pct =>
            {
                if (IsDisposed) return;
                _progressBar.Value = Math.Clamp(pct, 0, 100);
                _lblPercent.Text = $"{pct}%";
                _lblStatus.Text = $"Downloading update package ({pct}%)...";
            });

            try
            {
                _lblStatus.Text = "Downloading installer...";
                await UpdateService.DownloadAndInstallAsync(_updateInfo, progress, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // User cancelled
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    MessageBox.Show(
                        $"Failed to install update: {ex.Message}",
                        "Update Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Close();
                }
            }
        }
    }
}
