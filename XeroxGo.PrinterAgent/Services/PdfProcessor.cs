using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using XeroxGo.PrinterAgent.Models;

namespace XeroxGo.PrinterAgent.Services
{
    public class ProcessedPdfResult
    {
        public string FilePath { get; set; } = "";
        public bool IsTemporary { get; set; } = false;
    }

    public static class PdfProcessor
    {
        public static ProcessedPdfResult ProcessPdfForPrinting(string inputPath, PrintJob job, string tempDir)
        {
            bool is2Up = job.PagesPerSheet == "2";
            bool hasRotations = !string.IsNullOrWhiteSpace(job.PageRotations);
            bool hasOrder = !string.IsNullOrWhiteSpace(job.PageOrder);
            bool hasExclusions = !string.IsNullOrWhiteSpace(job.ExcludedPages);

            // Bypass if standard 1-Up with no page alterations
            if (!is2Up && !hasRotations && !hasOrder && !hasExclusions)
            {
                return new ProcessedPdfResult { FilePath = inputPath, IsTemporary = false };
            }

            string outputPath = Path.Combine(tempDir, $"processed_{Guid.NewGuid():N}.pdf");

            try
            {
                using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
                using var outputDoc = new PdfDocument();

                int totalPages = inputDoc.PageCount;
                var pageIndices = Enumerable.Range(0, totalPages).ToList();

                // 1. Parse Excluded Pages
                var excludedSet = new HashSet<int>();
                if (hasExclusions)
                {
                    try
                    {
                        var arr = JsonSerializer.Deserialize<List<int>>(job.ExcludedPages!);
                        if (arr != null)
                        {
                            foreach (int p in arr) excludedSet.Add(p - 1);
                        }
                    }
                    catch { }
                }

                pageIndices = pageIndices.Where(i => !excludedSet.Contains(i)).ToList();

                // 2. Parse Custom Page Order
                if (hasOrder)
                {
                    try
                    {
                        var orderArr = JsonSerializer.Deserialize<List<int>>(job.PageOrder!);
                        if (orderArr != null && orderArr.Count > 0)
                        {
                            var orderedList = new List<int>();
                            foreach (int p in orderArr)
                            {
                                int idx = p - 1;
                                if (idx >= 0 && idx < totalPages && !excludedSet.Contains(idx))
                                {
                                    orderedList.Add(idx);
                                }
                            }
                            if (orderedList.Count > 0)
                            {
                                pageIndices = orderedList;
                            }
                        }
                    }
                    catch { }
                }

                // 3. Parse Page Rotations
                var rotationsMap = new Dictionary<string, int>();
                if (hasRotations)
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(job.PageRotations!);
                        if (parsed != null) rotationsMap = parsed;
                    }
                    catch { }
                }

                // Standard 1-Up layout with rotations/exclusions
                if (!is2Up)
                {
                    foreach (int idx in pageIndices)
                    {
                        var page = inputDoc.Pages[idx];
                        var newPage = outputDoc.AddPage(page);

                        string key = (idx + 1).ToString();
                        if (rotationsMap.TryGetValue(key, out int angle))
                        {
                            newPage.Rotate = (newPage.Rotate + angle) % 360;
                        }
                    }
                }
                else
                {
                    // 2-Up Imposition Logic (Side-by-side or Top-bottom)
                    string layout = job.TwoUpLayout ?? "auto";

                    // Detect orientation of sample page
                    bool isLandscape = false;
                    if (pageIndices.Count > 0)
                    {
                        var p = inputDoc.Pages[pageIndices[0]];
                        isLandscape = p.Width > p.Height;
                    }

                    if (layout == "auto" || (layout != "topBottom" && layout != "sideBySide"))
                    {
                        layout = isLandscape ? "topBottom" : "sideBySide";
                    }

                    bool sideBySide = layout == "sideBySide";

                    for (int i = 0; i < pageIndices.Count; i += 2)
                    {
                        // A4 Sheet: Landscape [842 x 595] for side-by-side, Portrait [595 x 842] for top-bottom
                        var sheet = outputDoc.AddPage();
                        sheet.Size = PdfSharp.PageSize.A4;
                        sheet.Orientation = sideBySide ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;

                        using var gfx = XGraphics.FromPdfPage(sheet);

                        // Render first page on half sheet
                        RenderPageHalf(gfx, inputDoc, pageIndices[i], 0, sideBySide, sheet.Width, sheet.Height, rotationsMap);

                        // Render second page on other half if present
                        if (i + 1 < pageIndices.Count)
                        {
                            RenderPageHalf(gfx, inputDoc, pageIndices[i + 1], 1, sideBySide, sheet.Width, sheet.Height, rotationsMap);
                        }
                    }
                }

                outputDoc.Save(outputPath);
                Logger.Info($"[PDF TRANSFORM] Generated processed PDF at: {outputPath}");
                return new ProcessedPdfResult { FilePath = outputPath, IsTemporary = true };
            }
            catch (Exception ex)
            {
                Logger.Error($"PDF processing failed for job {job.Id}. Falling back to original document.", ex);
                return new ProcessedPdfResult { FilePath = inputPath, IsTemporary = false };
            }
        }

        private static void RenderPageHalf(
            XGraphics gfx,
            PdfDocument srcDoc,
            int pageIndex,
            int slot,
            bool sideBySide,
            double sheetWidth,
            double sheetHeight,
            Dictionary<string, int> rotationsMap)
        {
            var form = new XPdfForm(srcDoc);
            form.PageNumber = pageIndex + 1;

            double slotWidth = sideBySide ? (sheetWidth / 2.0) : sheetWidth;
            double slotHeight = sideBySide ? sheetHeight : (sheetHeight / 2.0);
            double offsetX = (sideBySide && slot == 1) ? slotWidth : 0;
            double offsetY = (!sideBySide && slot == 1) ? slotHeight : 0;

            // Fit page into slot preserving aspect ratio with margin
            double margin = 10.0;
            double availW = slotWidth - (margin * 2);
            double availH = slotHeight - (margin * 2);

            double scale = Math.Min(availW / form.PixelWidth, availH / form.PixelHeight);
            if (scale <= 0) scale = 1.0;

            double drawW = form.PixelWidth * scale;
            double drawH = form.PixelHeight * scale;
            double drawX = offsetX + (slotWidth - drawW) / 2.0;
            double drawY = offsetY + (slotHeight - drawH) / 2.0;

            gfx.DrawImage(form, drawX, drawY, drawW, drawH);
        }
    }
}
