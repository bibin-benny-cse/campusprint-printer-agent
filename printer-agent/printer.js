const ptp = require('pdf-to-printer');
const { PDFDocument, degrees } = require('pdf-lib');
const fs = require('fs-extra');
const path = require('path');
const config = require('./config');
const logger = require('./logger');

async function processPdfForPrinting(inputPath, jobOptions) {
  const is2Up = String(jobOptions.pagesPerSheet) === '2' || jobOptions.pagesPerSheet === 2;
  const layout = jobOptions.twoUpLayout === 'topBottom' ? 'topBottom' : 'sideBySide';

  let hasRotations = false;
  if (jobOptions.pageRotations) {
    try {
      const parsed = typeof jobOptions.pageRotations === 'string' ? JSON.parse(jobOptions.pageRotations) : jobOptions.pageRotations;
      hasRotations = parsed && Object.keys(parsed).length > 0;
    } catch (e) {}
  }
  const hasOrder = !!jobOptions.pageOrder;
  const hasExclusions = !!jobOptions.excludedPages;

  // If standard 1-Up with no page adjustments, print original directly
  if (!is2Up && !hasRotations && !hasOrder && !hasExclusions) {
    return { printPath: inputPath, isTemp: false };
  }

  const fileBytes = await fs.readFile(inputPath);
  const srcDoc = await PDFDocument.load(fileBytes);
  const totalPages = srcDoc.getPageCount();

  let pageIndices = Array.from({ length: totalPages }, (_, i) => i);

  // Parse page exclusions
  let excludedSet = new Set();
  if (jobOptions.excludedPages) {
    try {
      const arr = typeof jobOptions.excludedPages === 'string' ? JSON.parse(jobOptions.excludedPages) : jobOptions.excludedPages;
      if (Array.isArray(arr)) {
        arr.forEach(p => excludedSet.add(parseInt(p) - 1));
      }
    } catch (e) {}
  }

  pageIndices = pageIndices.filter(idx => !excludedSet.has(idx));

  // Parse custom page order
  if (jobOptions.pageOrder) {
    try {
      const orderArr = typeof jobOptions.pageOrder === 'string' ? JSON.parse(jobOptions.pageOrder) : jobOptions.pageOrder;
      if (Array.isArray(orderArr) && orderArr.length > 0) {
        pageIndices = orderArr.map(p => parseInt(p) - 1).filter(idx => idx >= 0 && idx < totalPages && !excludedSet.has(idx));
      }
    } catch (e) {}
  }

  // Parse page rotations
  let rotationsMap = {};
  if (jobOptions.pageRotations) {
    try {
      rotationsMap = typeof jobOptions.pageRotations === 'string' ? JSON.parse(jobOptions.pageRotations) : jobOptions.pageRotations;
    } catch (e) {}
  }

  const outDoc = await PDFDocument.create();

  if (is2Up) {
    logger.info(`[PRINTER AGENT] Applying 2-Up layout (${layout}) exactly ONCE for print execution...`);
    for (let i = 0; i < pageIndices.length; i += 2) {
      const isSideBySide = layout === 'sideBySide';
      const sheet = outDoc.addPage(isSideBySide ? [841.89, 595.28] : [595.28, 841.89]);
      const sheetW = sheet.getWidth();
      const sheetH = sheet.getHeight();

      // Page 1
      const idx1 = pageIndices[i];
      const [emb1] = await outDoc.embedPdf(srcDoc, [idx1]);
      const dims1 = emb1.scale(1);

      if (isSideBySide) {
        const halfW = sheetW / 2;
        const s1 = Math.min((halfW * 0.94) / dims1.width, (sheetH * 0.94) / dims1.height);
        const w1 = dims1.width * s1;
        const h1 = dims1.height * s1;
        const x1 = (halfW - w1) / 2;
        const y1 = (sheetH - h1) / 2;
        sheet.drawPage(emb1, { x: x1, y: y1, width: w1, height: h1 });

        // Page 2
        if (i + 1 < pageIndices.length) {
          const idx2 = pageIndices[i + 1];
          const [emb2] = await outDoc.embedPdf(srcDoc, [idx2]);
          const dims2 = emb2.scale(1);
          const s2 = Math.min((halfW * 0.94) / dims2.width, (sheetH * 0.94) / dims2.height);
          const w2 = dims2.width * s2;
          const h2 = dims2.height * s2;
          const x2 = halfW + (halfW - w2) / 2;
          const y2 = (sheetH - h2) / 2;
          sheet.drawPage(emb2, { x: x2, y: y2, width: w2, height: h2 });
        }
      } else {
        // Top & Bottom layout
        const halfH = sheetH / 2;
        const s1 = Math.min((sheetW * 0.94) / dims1.width, (halfH * 0.94) / dims1.height);
        const w1 = dims1.width * s1;
        const h1 = dims1.height * s1;
        const x1 = (sheetW - w1) / 2;
        const y1 = halfH + (halfH - h1) / 2;
        sheet.drawPage(emb1, { x: x1, y: y1, width: w1, height: h1 });

        if (i + 1 < pageIndices.length) {
          const idx2 = pageIndices[i + 1];
          const [emb2] = await outDoc.embedPdf(srcDoc, [idx2]);
          const dims2 = emb2.scale(1);
          const s2 = Math.min((sheetW * 0.94) / dims2.width, (halfH * 0.94) / dims2.height);
          const w2 = dims2.width * s2;
          const h2 = dims2.height * s2;
          const x2 = (sheetW - w2) / 2;
          const y2 = (halfH - h2) / 2;
          sheet.drawPage(emb2, { x: x2, y: y2, width: w2, height: h2 });
        }
      }
    }
  } else {
    // 1-Up with custom page order or rotations
    for (const idx of pageIndices) {
      const [copiedPage] = await outDoc.copyPages(srcDoc, [idx]);
      const addRot = parseInt(rotationsMap[idx] || rotationsMap[idx + 1] || 0);
      if (addRot > 0) {
        const cur = copiedPage.getRotation().angle;
        copiedPage.setRotation(degrees((cur + addRot) % 360));
      }
      outDoc.addPage(copiedPage);
    }
  }

  const outBytes = await outDoc.save();
  const tempDir = path.join(__dirname, 'temp');
  await fs.ensureDir(tempDir);
  const tempPath = path.join(tempDir, `print_temp_${Date.now()}_${Math.round(Math.random() * 1000)}.pdf`);
  await fs.writeFile(tempPath, outBytes);

  return { printPath: tempPath, isTemp: true };
}

async function printPdf(filePath, jobOptions) {
  const options = {
    printer: config.printerName,
  };

  // Number of copies
  if (jobOptions.copies && jobOptions.copies > 0) {
    options.copies = jobOptions.copies;
  }

  // Custom page ranges (e.g. '1-3')
  if (jobOptions.pageRange && jobOptions.pageRange !== 'All' && String(jobOptions.pagesPerSheet) !== '2') {
    options.pages = jobOptions.pageRange;
  }

  // Color vs Black & White mode
  const modeStr = String(jobOptions.mode || jobOptions.colorMode || '');
  if (modeStr.includes('Black') || modeStr.includes('B&W') || modeStr.includes('BW') || modeStr.includes('mono')) {
    options.monochrome = true;
  }

  // Single-sided vs Double-sided (Duplex) mode
  const sidesStr = String(jobOptions.sides || '');
  if (sidesStr.includes('Double') || sidesStr.includes('Duplex') || sidesStr.includes('double')) {
    options.side = 'duplex';
  } else if (sidesStr.includes('Single') || sidesStr.includes('Simplex') || sidesStr.includes('single')) {
    options.side = 'simplex';
  }

  const is2UpJob = String(jobOptions.pagesPerSheet) === '2' || jobOptions.pagesPerSheet === 2;
  const layout = jobOptions.twoUpLayout === 'topBottom' ? 'topBottom' : 'sideBySide';

  if (is2UpJob) {
    options.orientation = layout === 'sideBySide' ? 'landscape' : 'portrait';
  } else if (jobOptions.orientation && (jobOptions.orientation === 'portrait' || jobOptions.orientation === 'landscape')) {
    options.orientation = jobOptions.orientation;
  }

  logger.info(`Print settings mapping - Copies: ${jobOptions.copies || 1}, Pages: ${jobOptions.pageRange || 'All'}, Mode: ${jobOptions.mode}, Sides: ${jobOptions.sides}, PagesPerSheet: ${jobOptions.pagesPerSheet || '1'}, twoUpLayout: ${jobOptions.twoUpLayout || 'sideBySide'}, Printer Options: ${JSON.stringify(options)}`);

  let printPath = filePath;
  let isTemp = false;

  try {
    const processed = await processPdfForPrinting(filePath, jobOptions);
    printPath = processed.printPath;
    isTemp = processed.isTemp;

    await ptp.print(printPath, options);
  } finally {
    if (isTemp && printPath) {
      try {
        await fs.remove(printPath);
        logger.info(`[CLEANUP] Removed temporary print file: ${printPath}`);
      } catch (err) {
        logger.warn(`[CLEANUP ERROR] Could not remove temporary print file: ${err.message}`);
      }
    }
  }
}

module.exports = {
  printPdf
};
