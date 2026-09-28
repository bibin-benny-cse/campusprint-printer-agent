using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XeroxGo.PrinterAgent.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; } = false;
        public Version CurrentVersion { get; set; } = new Version(0, 1, 0);
        public Version? LatestVersion { get; set; }
        public string VersionString { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public long AssetSize { get; set; } = 0;
        public string PublishedAt { get; set; } = "";
    }

    public static class UpdateService
    {
        private const string RepositoryOwner = "bibin-benny-cse";
        private const string RepositoryName = "campusprint-printer-agent";
        private const string LatestReleaseUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
        private const string InstallerAssetName = "XeroxGoAgent-Setup.exe";

        private static readonly HttpClient s_httpClient;
        private static System.Threading.Timer? _periodicCheckTimer;
        private static Action<UpdateInfo>? _onUpdateAvailableCallback;

        static UpdateService()
        {
            s_httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            s_httpClient.DefaultRequestHeaders.Add("User-Agent", "XeroxGo-PrinterAgent-Updater");
            s_httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public static Version GetCurrentVersion()
        {
            return typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 1, 0);
        }

        public static string GetCurrentVersionString()
        {
            var v = GetCurrentVersion();
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }

        /// <summary>
        /// Starts the periodic background update checker.
        /// Fires an initial check 30s after startup, then repeats every 8 hours.
        /// </summary>
        public static void StartPeriodicChecker(Action<UpdateInfo> onUpdateFound)
        {
            _onUpdateAvailableCallback = onUpdateFound;
            _periodicCheckTimer ??= new System.Threading.Timer(
                async _ =>
                {
                    try
                    {
                        var info = await CheckForUpdatesAsync();
                        if (info.IsUpdateAvailable)
                        {
                            _onUpdateAvailableCallback?.Invoke(info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Background update check failed: {ex.Message}");
                    }
                },
                null,
                TimeSpan.FromSeconds(30), // Initial delay
                TimeSpan.FromHours(8)      // Recurring check
            );
        }

        /// <summary>
        /// Queries GitHub Releases API for the latest release metadata.
        /// Consumes ~2 KB JSON, zero memory impact on working set.
        /// </summary>
        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var currentVersion = GetCurrentVersion();
            var result = new UpdateInfo
            {
                CurrentVersion = currentVersion,
                IsUpdateAvailable = false
            };

            try
            {
                using var res = await s_httpClient.GetAsync(LatestReleaseUrl);
                if (!res.IsSuccessStatusCode)
                {
                    Logger.Warn($"GitHub update check returned HTTP {res.StatusCode}");
                    return result;
                }

                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                string publishedAt = root.TryGetProperty("published_at", out var pubProp) ? pubProp.GetString() ?? "" : "";

                string cleanTag = tagName.TrimStart('v', 'V').Trim();
                if (!Version.TryParse(cleanTag, out var remoteVer))
                {
                    // Fallback parse if 2-digit e.g. "0.1" -> "0.1.0"
                    if (Version.TryParse(cleanTag + ".0", out var fallbackVer))
                    {
                        remoteVer = fallbackVer;
                    }
                }

                if (remoteVer == null)
                {
                    return result;
                }

                result.LatestVersion = remoteVer;
                result.VersionString = cleanTag;
                result.ReleaseNotes = body;
                result.PublishedAt = publishedAt;

                // Locate the installer asset
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        if (name.Equals(InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                        {
                            result.DownloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() ?? "" : "";
                            result.AssetSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                            break;
                        }
                    }
                }

                // If newer version and asset exists
                if (remoteVer > currentVersion && !string.IsNullOrWhiteSpace(result.DownloadUrl))
                {
                    result.IsUpdateAvailable = true;
                    Logger.Info($"[UPDATE AVAILABLE] New version {cleanTag} available (current: {GetCurrentVersionString()}). Download URL: {result.DownloadUrl}");
                }
                else
                {
                    Logger.Info($"[UPDATE CHECK] Agent is up to date (current: {GetCurrentVersionString()}, latest remote: {cleanTag}).");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to check for updates: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Streams the installer directly to disk with an 80 KB buffer (zero RAM spike)
        /// and launches the silent Inno Setup installer.
        /// </summary>
        public static async Task DownloadAndInstallAsync(UpdateInfo updateInfo, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                throw new InvalidOperationException("No download URL available for update.");
            }

            string updateDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XeroxGo",
                "updates"
            );

            // Clean up any stale updates to keep disk usage zero
            try
            {
                if (Directory.Exists(updateDir))
                {
                    foreach (var f in Directory.GetFiles(updateDir, "*.exe"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                else
                {
                    Directory.CreateDirectory(updateDir);
                }
            }
            catch { }

            string installerPath = Path.Combine(updateDir, $"XeroxGoAgent-Setup-{updateInfo.VersionString}.exe");
            Logger.Info($"[UPDATE DOWNLOAD] Downloading update v{updateInfo.VersionString} to: {installerPath}");

            // Stream download directly to FileStream (RAM stays at ~0 MB overhead)
            using (var response = await s_httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? updateInfo.AssetSize;

                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        int pct = (int)((totalRead * 100) / totalBytes);
                        progress?.Report(Math.Min(100, pct));
                    }
                }
            }

            Logger.Info($"[UPDATE DOWNLOAD COMPLETE] File size: {new FileInfo(installerPath).Length} bytes. Handing off to Inno Setup...");

            // Launch Inno Setup installer silently
            // Inno Setup will:
            // 1. Terminate current agent process via taskkill in InitializeSetup
            // 2. Overwrite application binaries in %LOCALAPPDATA%\Programs\XeroxGo Agent
            // 3. Relaunch the new agent automatically via [Run] section
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT",
                UseShellExecute = true
            };

            Process.Start(psi);

            // Exit current agent cleanly
            Application.Exit();
        }
    }
}
