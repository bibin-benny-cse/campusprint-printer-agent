using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XeroxGo.PrinterAgent.Models;

namespace XeroxGo.PrinterAgent.Services
{
    public class QueueWorker : IDisposable
    {
        private readonly AppConfig _config;
        private ApiClient _api;
        private CancellationTokenSource _cts = new();
        private bool _isPaused = false;
        private bool _isProcessing = false;
        private string? _activePrinterName = null;
        private long? _currentJobId = null;
        private int _consecutiveFailures = 0;

        public event Action<string, string>? StatusChanged; // (Status, SummaryOrPrinterName)

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
                StatusChanged?.Invoke(_isPaused ? "Paused" : "Idle", _activePrinterName ?? "Ready");
            }
        }

        public string ActivePrinter => _activePrinterName ?? HardwareMonitor.ResolveDefaultPrinter();

        public QueueWorker(AppConfig config)
        {
            _config = config;
            _api = new ApiClient(_config.ApiUrl, _config.AgentApiKey);
        }

        public void ReloadConfiguration(AppConfig newConfig)
        {
            _config.ApiUrl = newConfig.ApiUrl;
            _config.AgentApiKey = newConfig.AgentApiKey;
            _config.PollIntervalSeconds = newConfig.PollIntervalSeconds;
            _config.HeartbeatIntervalSeconds = newConfig.HeartbeatIntervalSeconds;

            _api = new ApiClient(_config.ApiUrl, _config.AgentApiKey);
            Logger.Info($"[CONFIG RELOADED] API: {_config.ApiUrl}, Zero-Config Multi-Printer Auto-Discovery Active");
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => QueuePollLoopAsync(_cts.Token));
            Task.Run(() => ListenToSseStreamAsync(_cts.Token));
            Task.Run(() => SendPrinterInventoryAsync());
            Logger.Info("[WORKER STARTED] Event-driven multi-printer agent running.");
        }

        public void Stop()
        {
            _cts.Cancel();
            Logger.Info("[WORKER STOPPED]");
        }

        private readonly SemaphoreSlim _inventoryLock = new(1, 1);

        /// <summary>
        /// Scans connected physical printers and pushes the inventory to the cloud on-demand.
        /// Invoked on startup, admin panel refresh, local UI refresh, and USB Plug & Play events.
        /// </summary>
        public async Task<bool> SendPrinterInventoryAsync()
        {
            if (!await _inventoryLock.WaitAsync(0)) return false;

            try
            {
                var printers = HardwareMonitor.DiscoverAllPrinters();

                bool ok = await _api.SendMultiHeartbeatAsync(
                    printers,
                    _currentJobId,
                    _activePrinterName
                );

                if (ok)
                {
                    if (_consecutiveFailures > 0)
                    {
                        Logger.Info("[BACKEND RECONNECTED] Backend connection restored.");
                        _consecutiveFailures = 0;
                    }

                    if (!_isProcessing && !_isPaused)
                    {
                        string summary = printers.Count > 0 
                            ? $"{printers.Count} Printer{(printers.Count == 1 ? "" : "s")} Ready" 
                            : "No Printers Detected";
                        StatusChanged?.Invoke("Idle", summary);
                    }
                    Logger.Info($"[PRINTER INVENTORY SYNCED] Sent {printers.Count} printer(s) to cloud.");
                }
                else
                {
                    _consecutiveFailures++;
                    if (_consecutiveFailures == 10)
                    {
                        NotificationService.ShowCritical(
                            "Backend Disconnected",
                            "Cannot connect to XeroxGo cloud server. Please check your internet connection."
                        );
                    }
                    Logger.Warn("[PRINTER INVENTORY SYNC FAILED] Could not send printer inventory to cloud.");
                }

                return ok;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Printer inventory sync exception: {ex.Message}");
                return false;
            }
            finally
            {
                _inventoryLock.Release();
            }
        }

        private bool _isSseConnected = false;

        private async Task QueuePollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (!_isPaused && !_isProcessing)
                {
                    await CheckAndProcessQueueAsync().ConfigureAwait(false);
                }

                // Event-Driven Efficiency: When SSE is active, relax poll timer to 25s safety net.
                // If SSE drops, tighten fallback polling back to configured interval (e.g. 3s).
                int delaySeconds = _isSseConnected ? 25 : _config.PollIntervalSeconds;
                await Task.Delay(delaySeconds * 1000, ct).ConfigureAwait(false);
            }
        }

        private async Task CheckAndProcessQueueAsync()
        {
            if (_isProcessing || _isPaused) return;

            try
            {
                // Fetch print queue for this store
                var queue = await _api.GetPrintQueueAsync(null);
                if (queue.Count == 0)
                {
                    return;
                }

                // Process one job at a time to prevent queue collision
                var job = queue[0];
                await ExecuteJobAsync(job).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Queue poll check failed: {ex.Message}");
            }
        }

        private async Task ExecuteJobAsync(PrintJob job)
        {
            var discoveredPrinters = HardwareMonitor.DiscoverAllPrinters();

            // Resolve target physical printer
            string targetPrinter = "";
            if (!string.IsNullOrWhiteSpace(job.TargetPrinterName))
            {
                var matched = discoveredPrinters.FirstOrDefault(p => 
                    string.Equals(p.Name, job.TargetPrinterName, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    targetPrinter = matched.Name;
                }
            }

            // Fallback: If no target printer specified by cloud or matched, use first online connected printer
            if (string.IsNullOrWhiteSpace(targetPrinter))
            {
                var firstOnline = discoveredPrinters.FirstOrDefault(p => p.IsOnline);
                targetPrinter = firstOnline?.Name ?? HardwareMonitor.ResolveDefaultPrinter();
            }

            // 1. Atomically claim/lease the job with the unified printer name
            bool claimed = await _api.ClaimJobAsync(job.Id, targetPrinter);
            if (!claimed)
            {
                Logger.Info($"[JOB SKIPPED] Job ID={job.Id} was already claimed or cancelled.");
                return;
            }

            _isProcessing = true;
            _activePrinterName = targetPrinter;
            _currentJobId = job.Id;
            StatusChanged?.Invoke("Printing", targetPrinter);
            Logger.Info($"[JOB CLAIMED] Processing Job ID={job.Id} on printer '{targetPrinter}' (Token: {job.Token}, File: {job.Filename}, Copies: {job.Copies})");

            _ = _api.SendMultiHeartbeatAsync(discoveredPrinters, _currentJobId, _activePrinterName);

            string? downloadedPath = null;
            ProcessedPdfResult? processedResult = null;

            try
            {
                // 2. Download file
                downloadedPath = await _api.DownloadFileAsync(job, _config.TempDirectory);
                Logger.Info($"[DOWNLOAD DONE] Cached at: {downloadedPath}");

                // 3. Pre-process PDF (2-Up, rotations, exclusions, ordering)
                processedResult = PdfProcessor.ProcessPdfForPrinting(downloadedPath, job, _config.TempDirectory);

                // 4. Send to physical spooler
                await PrintEngine.PrintDocumentAsync(processedResult.FilePath, job, targetPrinter);
                Logger.Info($"[PRINT COMPLETE] Job {job.Id} successfully spooled to {targetPrinter}");

                // 5. Mark status as Completed
                await _api.UpdateJobStatusAsync(job.Id, "Completed");
                _currentJobId = null;
                _activePrinterName = null;
                _ = SendPrinterInventoryAsync();

                StatusChanged?.Invoke("Idle", $"{discoveredPrinters.Count} Printers Ready");
            }
            catch (Exception ex)
            {
                Logger.Error($"[JOB FAILED] Error processing job {job.Id} on {targetPrinter}", ex);

                NotificationService.ShowError(
                    "Print Job Failed",
                    $"Document '{job.OriginalName ?? job.Filename}' failed to print on {targetPrinter}. {ex.Message}"
                );

                await _api.UpdateJobStatusAsync(job.Id, "Failed", ex.Message);
                _currentJobId = null;
                _activePrinterName = null;
                _ = SendPrinterInventoryAsync();

                StatusChanged?.Invoke("Error", "Print Error");
            }
            finally
            {
                // Clean up ephemeral files immediately to preserve storage & privacy
                CleanupFile(downloadedPath);
                if (processedResult?.IsTemporary == true)
                {
                    CleanupFile(processedResult.FilePath);
                }
                _isProcessing = false;
                _activePrinterName = null;
                _currentJobId = null;
                MemoryOptimizer.TrimMemory();
            }
        }

        private async Task ListenToSseStreamAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                    if (!string.IsNullOrWhiteSpace(_config.AgentApiKey))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.AgentApiKey);
                    }
                    string sseUrl = $"{_config.ApiUrl.TrimEnd('/')}/print-queue/stream";

                    using var stream = await client.GetStreamAsync(sseUrl, ct).ConfigureAwait(false);
                    using var reader = new StreamReader(stream);

                    _isSseConnected = true;
                    Logger.Info("[SSE CONNECTED] Listening for instant real-time print triggers.");
                    _ = SendPrinterInventoryAsync();

                    while (!reader.EndOfStream && !ct.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("data:"))
                        {
                            string data = line.Substring(5).Trim();
                            if (data.Contains("REFRESH_PRINTERS"))
                            {
                                Logger.Info("[SSE] Received REFRESH_PRINTERS instruction from cloud. Refreshing hardware inventory...");
                                _ = SendPrinterInventoryAsync();
                            }
                            else if (!_isProcessing && !_isPaused)
                            {
                                _ = CheckAndProcessQueueAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _isSseConnected = false;
                    Logger.Warn($"SSE live stream disconnected: {ex.Message}. Falling back to active {_config.PollIntervalSeconds}s polling.");
                    await Task.Delay(10000, ct).ConfigureAwait(false);
                }
            }
        }

        private static void CleanupFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}
