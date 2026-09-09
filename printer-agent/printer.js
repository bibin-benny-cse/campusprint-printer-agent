const ptp = require('pdf-to-printer');
const config = require('./config');
const logger = require('./logger');
const nup = require('./nup');
const fs = require('fs-extra');

async function printPdf(filePath, jobOptions) {
  const options = {
    printer: config.printerName,
  };

  // Number of copies
  if (jobOptions.copies && jobOptions.copies > 0) {
    options.copies = jobOptions.copies;
  }

  // Color vs Black & White mode
  if (jobOptions.mode && (jobOptions.mode.includes('Black') || jobOptions.mode.includes('B&W') || jobOptions.mode.includes('BW'))) {
    options.monochrome = true;
  }

  // Single-sided vs Double-sided (Duplex) mode
  if (jobOptions.sides && (jobOptions.sides.includes('Double') || jobOptions.sides.includes('Duplex'))) {
    options.side = 'duplex';
  } else if (jobOptions.sides && (jobOptions.sides.includes('Single') || jobOptions.sides.includes('Simplex'))) {
    options.side = 'simplex';
  }

  const is2Up = String(jobOptions.pagesPerSheet || '') === '2';
  let targetFilePath = filePath;
  let temp2UpPath = null;

  try {
    if (is2Up) {
      logger.info(`[N-UP DETECTED] Generating 2-up pre-composed PDF for file "${filePath}" (Page Range: ${jobOptions.pageRange || 'All'})...`);
      temp2UpPath = await nup.generate2UpPdf(filePath, jobOptions.pageRange);
      targetFilePath = temp2UpPath;
      // Page range has already been applied during 2-up composition
    } else {
      // Custom page ranges (e.g. '1-3') for 1-up mode
      if (jobOptions.pageRange && jobOptions.pageRange !== 'All') {
        options.pages = jobOptions.pageRange;
      }
    }

    logger.info(`Print settings mapping - Copies: ${jobOptions.copies || 1}, Pages: ${jobOptions.pageRange || 'All'}, Mode: ${jobOptions.mode}, Sides: ${jobOptions.sides}, PagesPerSheet: ${jobOptions.pagesPerSheet || '1'}, Target File: "${targetFilePath}", Printer Options: ${JSON.stringify(options)}`);

    await ptp.print(targetFilePath, options);
  } finally {
    // Cleanup 2-up temp file if created
    if (temp2UpPath) {
      try {
        await fs.remove(temp2UpPath);
        logger.info(`[CLEANUP] Deleted temporary 2-up PDF file: ${temp2UpPath}`);
      } catch (err) {
        logger.error(`[CLEANUP ERROR] Failed deleting temp 2-up file ${temp2UpPath}`, err);
      }
    }
  }
}

module.exports = {
  printPdf
};
