require('dotenv').config();
const queue = require('./queue');
const logger = require('./logger');
const lock = require('./lock');
const fs = require('fs-extra');
const config = require('./config');
const ptp = require('pdf-to-printer');

async function resolvePrinterName() {
  const target = config.printerName;

  // 1. If explicit printer name is specified and is NOT "auto", use it directly
  if (target && target.trim().toLowerCase() !== 'auto') {
    logger.info(`[CONFIG] Using explicit printer name: "${target.trim()}"`);
    return target.trim();
  }

  // Common virtual printer names to filter out when looking for physical hardware
  const virtualKeywords = ['microsoft print to pdf', 'onenote', 'fax', 'xps document writer', 'adobe pdf'];
  const isVirtual = (name) => name && virtualKeywords.some(k => name.toLowerCase().includes(k));

  try {
    const allPrinters = await ptp.getPrinters();
    const printerNames = (allPrinters || []).map(p => p.name).filter(Boolean);
    
    logger.info(`[PRINTER SCAN] Installed printers found in Windows Settings: ${JSON.stringify(printerNames)}`);

    const physicalPrinters = printerNames.filter(name => !isVirtual(name));

    // 2. Check default printer first
    const defaultPrinterObj = await ptp.getDefaultPrinter();
    const defaultName = defaultPrinterObj ? defaultPrinterObj.name : null;

    if (defaultName) {
      if (!isVirtual(defaultName)) {
        logger.info(`[AUTO-DETECT] Auto-selected default physical Windows printer: "${defaultName}"`);
        return defaultName;
      }
      logger.info(`[AUTO-DETECT] Default printer is virtual ("${defaultName}"). Searching for connected physical printers...`);
    }

    // 3. If default printer was virtual, but physical printers exist, auto-select the physical printer!
    if (physicalPrinters.length > 0) {
      logger.info(`[AUTO-DETECT] Auto-selected physical printer: "${physicalPrinters[0]}"`);
      return physicalPrinters[0];
    }

    // 4. Fallback to default printer if no physical printer found
    if (defaultName) {
      logger.info(`[AUTO-DETECT] Using default printer: "${defaultName}"`);
      return defaultName;
    }
  } catch (err) {
    logger.warn(`[AUTO-DETECT ERROR] Could not query Windows printers: ${err.message}`);
  }

  // 5. Final fallback
  const fallback = 'Microsoft Print to PDF';
  logger.warn(`[AUTO-DETECT FALLBACK] Defaulting to: "${fallback}"`);
  return fallback;
}

async function init() {
  // 1. Single-Instance Verification
  const locked = await lock.acquireLock();
  if (!locked) {
    logger.warn('Single-instance lock check failed: Another Printer Agent process is already running on this machine (Port 39201). Shutting down duplicate invocation.');
    process.exit(0);
  }

  try {
    // Resolve target printer dynamically (Explicit or Auto-Detect)
    config.printerName = await resolvePrinterName();

    logger.info('===================================================');
    logger.info('--- CampusPrint Printer Agent Service Starting ---');
    logger.info(`Backend API URL : ${config.apiUrl}`);
    logger.info(`Active Printer  : ${config.printerName}`);
    logger.info('===================================================');
    
    // Ensure temporary directory exists
    await fs.ensureDir(config.tempDir);
    logger.info(`Temp directory ready at: ${config.tempDir}`);

    // Start automated queue listener
    queue.startPolling();
    
  } catch (error) {
    logger.error('Startup initialization failed', error);
    lock.releaseLock();
    process.exit(1);
  }
}

// Graceful Shutdown Handlers
function handleShutdown(signal) {
  logger.info(`Received ${signal}. Shutting down CampusPrint Printer Agent...`);
  queue.stopPolling();
  lock.releaseLock();
  process.exit(0);
}

process.on('SIGINT', () => handleShutdown('SIGINT (Ctrl+C)'));
process.on('SIGTERM', () => handleShutdown('SIGTERM'));

// Prevent uncaught exceptions from crashing the service daemon
process.on('uncaughtException', (err) => {
  logger.error('Uncaught Exception caught in agent main loop:', err);
});

process.on('unhandledRejection', (reason, promise) => {
  logger.error('Unhandled Promise Rejection caught in agent main loop:', reason);
});

init();
