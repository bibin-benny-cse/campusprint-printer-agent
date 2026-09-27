using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XeroxGo.PrinterAgent.Models;

namespace XeroxGo.PrinterAgent.Services
{
    public class ApiClient
    {
        private static readonly HttpClient s_cdnClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public ApiClient(string baseUrl, string? apiKey = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl + "/"),
                Timeout = TimeSpan.FromSeconds(20)
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _http.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        public async Task<bool> SendHeartbeatAsync(
            string logicalPrinterName, 
            string status = "Idle", 
            long? currentJobId = null, 
            bool isOnline = true, 
            string? physicalDriverName = null)
        {
            try
            {
                var payload = new HeartbeatPayload
                {
                    PrinterName = logicalPrinterName,
                    Status = status,
                    CurrentJobId = currentJobId,
                    IsOnline = isOnline,
                    SystemName = physicalDriverName ?? Environment.MachineName,
                    DriverName = physicalDriverName ?? logicalPrinterName
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync("printers/heartbeat", content);

                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[HEARTBEAT FAILED] Could not send ping for '{logicalPrinterName}': {ex.Message}");
                return false;
            }
        }

        public async Task<List<PrintJob>> GetPrintQueueAsync(string printerName)
        {
            try
            {
                string endpoint = "print-queue";
                if (!string.IsNullOrWhiteSpace(printerName) && !printerName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint += $"?printerName={Uri.EscapeDataString(printerName)}";
                }

                using var res = await _http.GetAsync(endpoint);
                res.EnsureSuccessStatusCode();

                string json = await res.Content.ReadAsStringAsync();
                var jobs = JsonSerializer.Deserialize<List<PrintJob>>(json);
                return jobs ?? new List<PrintJob>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch print queue from backend: {ex.Message}", ex);
            }
        }

        public async Task<string> DownloadFileAsync(PrintJob job, string destinationDir)
        {
            string destPath = Path.Combine(destinationDir, $"{Guid.NewGuid():N}_{job.Filename}");

            try
            {
                // Prefer direct CDN URL if provided by Supabase Storage
                if (!string.IsNullOrWhiteSpace(job.FileUrl) && Uri.IsWellFormedUriString(job.FileUrl, UriKind.Absolute))
                {
                    using var stream = await s_cdnClient.GetStreamAsync(job.FileUrl);
                    using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileStream);
                    return destPath;
                }

                // Fallback to backend /api/files/:filename endpoint
                using var res = await _http.GetAsync($"files/{Uri.EscapeDataString(job.Filename)}", HttpCompletionOption.ResponseHeadersRead);
                res.EnsureSuccessStatusCode();

                using (var stream = await res.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }

                return destPath;
            }
            catch (Exception ex)
            {
                if (File.Exists(destPath)) File.Delete(destPath);
                throw new Exception($"Failed to download document '{job.Filename}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Atomically leases/claims a job to prevent race conditions in multi-printer or multi-agent environments.
        /// Falls back to UpdateJobStatusAsync if the claim endpoint is unavailable.
        /// </summary>
        public async Task<bool> ClaimJobAsync(long jobId, string logicalPrinterName, string? physicalDriverName = null)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["printerName"] = logicalPrinterName,
                    ["systemName"] = physicalDriverName ?? logicalPrinterName
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync($"jobs/{jobId}/claim", content);

                if (res.IsSuccessStatusCode)
                {
                    return true;
                }

                // If backend does not support /claim endpoint (404), fall back to PATCH status
                if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return await UpdateJobStatusAsync(jobId, "Printing");
                }

                // Another agent already claimed it (409 Conflict)
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Claim job failed for ID {jobId}: {ex.Message}. Attempting status fallback.");
                return await UpdateJobStatusAsync(jobId, "Printing");
            }
        }

        public async Task<bool> UpdateJobStatusAsync(long jobId, string status, string? errorMessage = null)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["status"] = status
                };
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    payload["error"] = errorMessage;
                }

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Using PATCH method
                var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"jobs/{jobId}")
                {
                    Content = content
                };

                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to update job {jobId} status to '{status}': {ex.Message}");
                return false;
            }
        }
    }
}
