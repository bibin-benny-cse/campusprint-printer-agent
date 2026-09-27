using System.Text.Json.Serialization;

namespace XeroxGo.PrinterAgent.Models
{
    public class PrintJob
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("originalName")]
        public string? OriginalName { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";

        [JsonPropertyName("fileUrl")]
        public string? FileUrl { get; set; }

        [JsonPropertyName("copies")]
        public int Copies { get; set; } = 1;

        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "B&W";

        [JsonPropertyName("sides")]
        public string Sides { get; set; } = "Single-sided";

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "ReadyToPrint";

        [JsonPropertyName("pageRange")]
        public string PageRange { get; set; } = "All";

        [JsonPropertyName("fileType")]
        public string? FileType { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; } = 1;

        [JsonPropertyName("pagesPerSheet")]
        public string? PagesPerSheet { get; set; } = "1";

        [JsonPropertyName("twoUpLayout")]
        public string? TwoUpLayout { get; set; } = "sideBySide";

        [JsonPropertyName("orientation")]
        public string? Orientation { get; set; } = "auto";

        [JsonPropertyName("pageOrder")]
        public string? PageOrder { get; set; }

        [JsonPropertyName("excludedPages")]
        public string? ExcludedPages { get; set; }

        [JsonPropertyName("pageRotations")]
        public string? PageRotations { get; set; }

        [JsonPropertyName("scale")]
        public string? Scale { get; set; } = "Fit";

        [JsonPropertyName("assigned_printer_name")]
        public string? AssignedPrinterName { get; set; }
    }

    public class HeartbeatPayload
    {
        [JsonPropertyName("printerName")]
        public string PrinterName { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Idle";

        [JsonPropertyName("currentJobId")]
        public long? CurrentJobId { get; set; }

        [JsonPropertyName("driverName")]
        public string? DriverName { get; set; }

        [JsonPropertyName("isOnline")]
        public bool IsOnline { get; set; } = true;

        [JsonPropertyName("systemName")]
        public string? SystemName { get; set; }
    }
}
