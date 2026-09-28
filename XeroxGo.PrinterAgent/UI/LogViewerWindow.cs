using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using XeroxGo.PrinterAgent.Services;

namespace XeroxGo.PrinterAgent.UI
{
    /// <summary>
    /// Lightweight, native Windows 11 Fluent log viewer.
    /// Uses reverse-seek tailing to consume minimal RAM (< 1MB) even with large log files.
    /// Fully disposes and trims working set upon closure.
    /// </summary>
    public class LogViewerWindow : Form
    {
        private readonly Panel _headerPanel;
        private readonly Panel _terminalCard;
        private readonly RichTextBox _rtbLogs;
        private readonly Panel _footerPanel;

        private readonly CheckBox _chkAutoScroll;
        private readonly FluentButton _btnCopy;
        private readonly FluentButton _btnClear;
        private readonly FluentButton _btnRefresh;

        private readonly Label _lblTitle;
        private readonly Label _lblSub;
        private readonly Label _lblFilePath;
        private readonly Label _lblStatus;

        private readonly System.Windows.Forms.Timer _refreshTimer;
        private long _lastFileLength = -1;
        private DateTime _lastWriteTimeUtc = DateTime.MinValue;

        public LogViewerWindow()
        {
            SuspendLayout();

            Text = "XeroxGo Agent - Activity Logs";
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(520, 360);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = FluentTheme.Background;
            Font = FluentTheme.Font(9f);
            AutoScaleMode = AutoScaleMode.Dpi;

            if (BrandAssets.AppIcon != null)
            {
                Icon = BrandAssets.AppIcon;
            }

            DwmApi.ApplyWindows11Styling(this);

            // ==========================================
            // 1. Header Panel
            // ==========================================
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = FluentTheme.Background
            };

            _lblTitle = new Label
            {
                Text = "Activity Logs",
                Font = FluentTheme.Font(11f, FontStyle.Bold),
                ForeColor = FluentTheme.TextPrimary,
                Location = new Point(18, 10),
                AutoSize = true
            };
            _headerPanel.Controls.Add(_lblTitle);

            _lblSub = new Label
            {
                Text = "Recent system events and spooler operations",
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 31),
                AutoSize = true
            };
            _headerPanel.Controls.Add(_lblSub);

            _chkAutoScroll = new CheckBox
            {
                Text = "Auto-scroll",
                Checked = true,
                Font = FluentTheme.Font(8.5f),
                ForeColor = FluentTheme.TextSecondary,
                Size = new Size(90, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _headerPanel.Controls.Add(_chkAutoScroll);

            _btnCopy = new FluentButton
            {
                Text = "Copy",
                Size = new Size(64, 28),
                IsPrimary = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnCopy.Click += OnCopyLogs;
            _headerPanel.Controls.Add(_btnCopy);

            _btnClear = new FluentButton
            {
                Text = "Clear",
                Size = new Size(64, 28),
                IsPrimary = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnClear.Click += OnClearLogs;
            _headerPanel.Controls.Add(_btnClear);

            _btnRefresh = new FluentButton
            {
                Text = "Refresh",
                Size = new Size(72, 28),
                IsPrimary = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnRefresh.Click += (s, e) => ForceReload();
            _headerPanel.Controls.Add(_btnRefresh);

            _headerPanel.Resize += (s, e) => PositionHeaderButtons();
            PositionHeaderButtons();

            Controls.Add(_headerPanel);

            // ==========================================
            // 2. Footer Panel
            // ==========================================
            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = FluentTheme.Background
            };

            string logPath = Logger.GetLogFilePath();
            _lblFilePath = new Label
            {
                Text = "Path: " + logPath,
                Font = FluentTheme.Font(8f),
                ForeColor = FluentTheme.TextSecondary,
                Location = new Point(18, 8),
                AutoSize = true
            };
            _footerPanel.Controls.Add(_lblFilePath);

            _lblStatus = new Label
            {
                Text = "Live stream active",
                Font = FluentTheme.Font(8f),
                ForeColor = FluentTheme.TextSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true
            };
            _footerPanel.Controls.Add(_lblStatus);
            _footerPanel.Resize += (s, e) =>
            {
                _lblStatus.Location = new Point(_footerPanel.Width - _lblStatus.Width - 18, 8);
            };

            Controls.Add(_footerPanel);

            // ==========================================
            // 3. Central Terminal Card & RichTextBox
            // ==========================================
            var containerPadding = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 0),
                BackColor = FluentTheme.Background
            };

            _terminalCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42), // Slate 900 dark console
                Padding = new Padding(10)
            };

            _rtbLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Consolas", 9f, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };
            _terminalCard.Controls.Add(_rtbLogs);
            containerPadding.Controls.Add(_terminalCard);
            Controls.Add(containerPadding);

            // ==========================================
            // 4. Periodic Live Auto-Refresh (2s Timer)
            // ==========================================
            _refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000
            };
            _refreshTimer.Tick += (s, e) => CheckForFileUpdates();

            ResumeLayout(false);

            // Initial load
            ForceReload();
            _refreshTimer.Start();
        }

        private void PositionHeaderButtons()
        {
            int rightX = _headerPanel.Width - 18;

            _btnRefresh.Location = new Point(rightX - _btnRefresh.Width, 13);
            rightX -= _btnRefresh.Width + 8;

            _btnClear.Location = new Point(rightX - _btnClear.Width, 13);
            rightX -= _btnClear.Width + 8;

            _btnCopy.Location = new Point(rightX - _btnCopy.Width, 13);
            rightX -= _btnCopy.Width + 14;

            _chkAutoScroll.Location = new Point(rightX - _chkAutoScroll.Width, 15);
        }

        private void ForceReload()
        {
            _lastFileLength = -1;
            _lastWriteTimeUtc = DateTime.MinValue;
            CheckForFileUpdates();
        }

        private void CheckForFileUpdates()
        {
            string path = Logger.GetLogFilePath();
            if (!File.Exists(path))
            {
                if (_rtbLogs.TextLength == 0)
                {
                    _rtbLogs.Text = "No log entries recorded yet.";
                }
                return;
            }

            try
            {
                var fi = new FileInfo(path);
                if (fi.Length == _lastFileLength && fi.LastWriteTimeUtc == _lastWriteTimeUtc)
                {
                    // No disk changes - zero CPU/RAM allocation
                    return;
                }

                _lastFileLength = fi.Length;
                _lastWriteTimeUtc = fi.LastWriteTimeUtc;

                LoadRecentLogTail(path);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Read warning: " + ex.Message;
            }
        }

        /// <summary>
        /// Reads strictly the last ~32 KB (approx 200 lines) of the log file using reverse seek.
        /// Prevents large memory allocations when logs grow to multi-megabyte size.
        /// </summary>
        private void LoadRecentLogTail(string path)
        {
            const int maxBytesToRead = 32768; // 32 KB tail

            string content;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length <= maxBytesToRead)
                {
                    using var reader = new StreamReader(fs, Encoding.UTF8);
                    content = reader.ReadToEnd();
                }
                else
                {
                    fs.Seek(-maxBytesToRead, SeekOrigin.End);
                    using var reader = new StreamReader(fs, Encoding.UTF8);
                    // Discard partial line
                    reader.ReadLine();
                    content = reader.ReadToEnd();
                }
            }

            RenderSyntaxColoredLogs(content);
        }

        private void RenderSyntaxColoredLogs(string rawContent)
        {
            _rtbLogs.SuspendLayout();
            _rtbLogs.Clear();

            var lines = rawContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var colTimestamp = Color.FromArgb(148, 163, 184); // Slate 400
            var colInfo = Color.FromArgb(96, 165, 250);       // Blue 400
            var colWarn = Color.FromArgb(251, 191, 36);       // Amber 400
            var colError = Color.FromArgb(248, 113, 113);     // Rose 400
            var colText = Color.FromArgb(241, 245, 249);      // Slate 100

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Typical entry format: [2026-09-28 23:05:47.123] [INFO] message
                if (line.StartsWith("[") && line.Length > 28)
                {
                    int firstClose = line.IndexOf(']');
                    if (firstClose > 0)
                    {
                        string timestampPart = line.Substring(0, firstClose + 1);
                        AppendColorText(timestampPart + " ", colTimestamp);

                        int secondOpen = line.IndexOf('[', firstClose);
                        int secondClose = line.IndexOf(']', secondOpen > 0 ? secondOpen : firstClose + 1);

                        if (secondOpen > 0 && secondClose > secondOpen)
                        {
                            string levelTag = line.Substring(secondOpen, secondClose - secondOpen + 1);
                            Color levelColor = colInfo;
                            if (levelTag.Contains("WARN")) levelColor = colWarn;
                            else if (levelTag.Contains("ERROR")) levelColor = colError;

                            AppendColorText(levelTag + " ", levelColor);

                            string remainder = line.Substring(secondClose + 1);
                            AppendColorText(remainder + Environment.NewLine, colText);
                            continue;
                        }
                    }
                }

                // Default fallback coloring
                AppendColorText(line + Environment.NewLine, colText);
            }

            if (_chkAutoScroll.Checked)
            {
                _rtbLogs.SelectionStart = _rtbLogs.TextLength;
                _rtbLogs.ScrollToCaret();
            }

            _rtbLogs.ResumeLayout();
            _lblStatus.Text = $"Updated at {DateTime.Now:HH:mm:ss}";
        }

        private void AppendColorText(string text, Color color)
        {
            _rtbLogs.SelectionStart = _rtbLogs.TextLength;
            _rtbLogs.SelectionLength = 0;
            _rtbLogs.SelectionColor = color;
            _rtbLogs.AppendText(text);
        }

        private void OnCopyLogs(object? sender, EventArgs e)
        {
            try
            {
                if (_rtbLogs.TextLength > 0)
                {
                    Clipboard.SetText(_rtbLogs.Text);
                    _btnCopy.Text = "Copied!";

                    var t = new System.Windows.Forms.Timer { Interval = 1500 };
                    t.Tick += (s, args) =>
                    {
                        _btnCopy.Text = "Copy";
                        t.Stop();
                        t.Dispose();
                    };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to copy logs: " + ex.Message, "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnClearLogs(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                this,
                "Clear all activity log entries from disk?",
                "Clear Logs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    string path = Logger.GetLogFilePath();
                    if (File.Exists(path))
                    {
                        File.WriteAllText(path, string.Empty);
                    }
                    _rtbLogs.Clear();
                    _lastFileLength = 0;
                    _lblStatus.Text = "Logs cleared";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to clear log file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _rtbLogs.Clear();
            base.OnFormClosed(e);

            // Reclaim all memory back to OS upon closing the viewer window
            MemoryOptimizer.TrimMemory();
        }
    }
}
