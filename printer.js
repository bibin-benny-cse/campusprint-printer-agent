const ptp = require('pdf-to-printer');
const config = require('./config');
const logger = require('./logger');

async function printPdf(filePath, jobOptions) {
  const options = {
    printer: config.printerName,
  };

  // Number of copies
  if (jobOptions.copies && jobOptions.copies > 0) {
    options.copies = jobOptions.copies;
  }

  // Custom page ranges (e.g. '1-3')
  if (jobOptions.pageRange && jobOptions.pageRange !== 'All') {
    options.pages = jobOptions.pageRange;
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

  logger.info(`Print settings mapping - Copies: ${jobOptions.copies || 1}, Pages: ${jobOptions.pageRange || 'All'}, Mode: ${jobOptions.mode}, Sides: ${jobOptions.sides}, Printer Options: ${JSON.stringify(options)}`);
  
  await ptp.print(filePath, options);
}

module.exports = {
  printPdf
};
