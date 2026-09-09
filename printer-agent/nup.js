const fs = require('fs-extra');
const path = require('path');
const { PDFDocument } = require('pdf-lib');
const logger = require('./logger');

/**
 * Parse a page range string (e.g. "1-3", "1,3,5", "All") into 0-indexed page numbers.
 */
function parsePageRange(rangeStr, totalPages) {
  if (!rangeStr || rangeStr === 'All') {
    return Array.from({ length: totalPages }, (_, i) => i);
  }

  const pages = new Set();
  const parts = rangeStr.split(',');

  for (let part of parts) {
    part = part.trim();
    if (!part) continue;

    if (part.includes('-')) {
      const [startStr, endStr] = part.split('-');
      const start = parseInt(startStr, 10);
      const end = parseInt(endStr, 10);
      if (!isNaN(start) && !isNaN(end) && start <= end) {
        for (let p = Math.max(1, start); p <= Math.min(totalPages, end); p++) {
          pages.add(p - 1);
        }
      }
    } else {
      const p = parseInt(part, 10);
      if (!isNaN(p) && p >= 1 && p <= totalPages) {
        pages.add(p - 1);
      }
    }
  }

  const result = Array.from(pages).sort((a, b) => a - b);
  return result.length > 0 ? result : Array.from({ length: totalPages }, (_, i) => i);
}

/**
 * Generates a 2-up (2 pages per sheet) PDF document.
 * - Landscape source pages: Arranged Top & Bottom on a Portrait A4 sheet (1x2 grid).
 * - Portrait source pages: Arranged Left & Right on a Landscape A4 sheet (2x1 grid).
 * 
 * @param {string} inputPath - Path to the original PDF file.
 * @param {string} [pageRange] - Optional page range string (e.g. "1-3").
 * @returns {Promise<string>} - Path to the newly created 2-up PDF file.
 */
async function generate2UpPdf(inputPath, pageRange) {
  const pdfBytes = await fs.readFile(inputPath);
  const srcDoc = await PDFDocument.load(pdfBytes);
  const totalPages = srcDoc.getPageCount();

  if (totalPages === 0) {
    throw new Error('Input PDF has 0 pages.');
  }

  const targetPageIndices = parsePageRange(pageRange, totalPages);
  const newPdf = await PDFDocument.create();

  // A4 Standard Dimensions (in points: 1 pt = 1/72 inch)
  const A4_WIDTH = 595.28;
  const A4_HEIGHT = 841.89;
  const PADDING = 12; // 12pt margin inside each slot

  // Process selected pages 2 at a time
  for (let i = 0; i < targetPageIndices.length; i += 2) {
    const pageIndex1 = targetPageIndices[i];
    const pageIndex2 = i + 1 < targetPageIndices.length ? targetPageIndices[i + 1] : null;

    // Embed Page 1
    const embedded1 = await newPdf.embedPage(srcDoc.getPage(pageIndex1));
    const embedded2 = pageIndex2 !== null ? await newPdf.embedPage(srcDoc.getPage(pageIndex2)) : null;

    // Determine orientation based on Page 1
    const isLandscape = embedded1.width > embedded1.height;

    if (isLandscape) {
      // =========================================================================
      // LANDSCAPE SOURCE: 1x2 Vertical Grid (Top & Bottom) on Portrait Sheet
      // =========================================================================
      const sheetWidth = A4_WIDTH;
      const sheetHeight = A4_HEIGHT;
      const targetSheet = newPdf.addPage([sheetWidth, sheetHeight]);

      const slotWidth = sheetWidth;
      const slotHeight = sheetHeight / 2;
      const availWidth = slotWidth - (PADDING * 2);
      const availHeight = slotHeight - (PADDING * 2);

      // --- Top Slot (Page 1) ---
      const scale1 = Math.min(availWidth / embedded1.width, availHeight / embedded1.height);
      const drawW1 = embedded1.width * scale1;
      const drawH1 = embedded1.height * scale1;
      const x1 = (slotWidth - drawW1) / 2;
      const y1 = slotHeight + ((slotHeight - drawH1) / 2);

      targetSheet.drawPage(embedded1, {
        x: x1,
        y: y1,
        width: drawW1,
        height: drawH1
      });

      // --- Bottom Slot (Page 2) ---
      if (embedded2) {
        const scale2 = Math.min(availWidth / embedded2.width, availHeight / embedded2.height);
        const drawW2 = embedded2.width * scale2;
        const drawH2 = embedded2.height * scale2;
        const x2 = (slotWidth - drawW2) / 2;
        const y2 = (slotHeight - drawH2) / 2;

        targetSheet.drawPage(embedded2, {
          x: x2,
          y: y2,
          width: drawW2,
          height: drawH2
        });
      }
    } else {
      // =========================================================================
      // PORTRAIT SOURCE: 2x1 Horizontal Grid (Left & Right) on Landscape Sheet
      // =========================================================================
      const sheetWidth = A4_HEIGHT; // 841.89
      const sheetHeight = A4_WIDTH; // 595.28
      const targetSheet = newPdf.addPage([sheetWidth, sheetHeight]);

      const slotWidth = sheetWidth / 2;
      const slotHeight = sheetHeight;
      const availWidth = slotWidth - (PADDING * 2);
      const availHeight = slotHeight - (PADDING * 2);

      // --- Left Slot (Page 1) ---
      const scale1 = Math.min(availWidth / embedded1.width, availHeight / embedded1.height);
      const drawW1 = embedded1.width * scale1;
      const drawH1 = embedded1.height * scale1;
      const x1 = (slotWidth - drawW1) / 2;
      const y1 = (slotHeight - drawH1) / 2;

      targetSheet.drawPage(embedded1, {
        x: x1,
        y: y1,
        width: drawW1,
        height: drawH1
      });

      // --- Right Slot (Page 2) ---
      if (embedded2) {
        const scale2 = Math.min(availWidth / embedded2.width, availHeight / embedded2.height);
        const drawW2 = embedded2.width * scale2;
        const drawH2 = embedded2.height * scale2;
        const x2 = slotWidth + ((slotWidth - drawW2) / 2);
        const y2 = (slotHeight - drawH2) / 2;

        targetSheet.drawPage(embedded2, {
          x: x2,
          y: y2,
          width: drawW2,
          height: drawH2
        });
      }
    }
  }

  // Save composed PDF to temp directory
  const tempDir = path.join(__dirname, 'temp');
  await fs.ensureDir(tempDir);
  const outputPath = path.join(tempDir, `2up_${Date.now()}_${path.basename(inputPath)}`);

  const savedBytes = await newPdf.save();
  await fs.writeFile(outputPath, savedBytes);

  logger.info(`[N-UP GENERATION] Successfully generated 2-up PDF at ${outputPath} (${targetPageIndices.length} original pages -> ${newPdf.getPageCount()} sheet(s))`);
  return outputPath;
}

module.exports = {
  parsePageRange,
  generate2UpPdf
};
