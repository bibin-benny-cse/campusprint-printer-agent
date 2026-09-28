using System;
using System.Collections.Concurrent;
using System.Windows.Forms;

namespace XeroxGo.PrinterAgent.Services
{
    /// <summary>
    /// Delivers notifications strictly for errors and critical hardware failures.
    /// Normal operations (idle, connected, printing, completed) remain silent to avoid spamming the user.
    /// </summary>
    public static class NotificationService
    {
        private static NotifyIcon? _notifyIcon;
        private static readonly ConcurrentDictionary<string, DateTime> _lastNotificationTime = new();
        private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(45);

        public static void Initialize(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon;
        }

        /// <summary>
        /// Displays an error alert for hardware issues (paper jam, out of paper, spooler failure).
        /// Throttles identical messages to prevent popup spam.
        /// </summary>
        public static void ShowError(string title, string message, bool force = false)
        {
            if (_notifyIcon == null) return;

            string key = $"{title}:{message}";
            DateTime now = DateTime.UtcNow;

            if (!force && _lastNotificationTime.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < ThrottleInterval)
                {
                    // Suppress repeat popups within throttle interval
                    return;
                }
            }

            _lastNotificationTime[key] = now;
            Logger.Warn($"[ALERT TRIGGERED] {title}: {message}");

            try
            {
                _notifyIcon.ShowBalloonTip(
                    6000,
                    $"⚠️ XeroxGo: {title}",
                    message,
                    ToolTipIcon.Error
                );
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to display notification balloon", ex);
            }
        }

        /// <summary>
        /// Displays a critical failure notification (e.g., persistent backend disconnect, unhandled crash).
        /// </summary>
        public static void ShowCritical(string title, string message)
        {
            ShowError(title, message, force: true);
        }

        /// <summary>
        /// Displays an informational notification (e.g., update available).
        /// </summary>
        public static void ShowInfo(string title, string message)
        {
            if (_notifyIcon == null) return;
            try
            {
                _notifyIcon.ShowBalloonTip(
                    6000,
                    $"XeroxGo: {title}",
                    message,
                    ToolTipIcon.Info
                );
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to display notification balloon", ex);
            }
        }
    }
}
