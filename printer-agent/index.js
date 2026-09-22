require('dotenv').config();
const queue = require('./queue');
const logger = require('./logger');
const lock = require('./lock');
const fs = require('fs-extra');
const config = require('./config');
const ptp = require('pdf-to-printer');

async function resolvePrinterName() {
  const target = config.printerName;

  // 1. If an explicit printer name is specified and is not "auto", use it
  if (target && target.trim().toLowerCase() !== 'auto') {
    logger.info(`[CONFIG] Using explicit printer name: "${target.trim()}"`);
    return target.trim();
  }

  // 2. Auto-detect Windows Default Printer
  try {
    const defaultPrinter = await ptp.getDefaultPrinter();
    if (defaultPrinter && defaultPrinter.name) {
      logger.info(`[AUTO-DETECT] Identified default Windows printer: "${defaultPrinter.name}"`);
      return defaultPrinter.name;
    }
  } catch (err) {
    logger.warn(`[AUTO-DETECT] Could not query Windows default printer: ${err.message}`);
  }

  // 3. Fallback: select first available installed printer
  try {
    const printers = await ptp.getPrinters();
    if (printers && printers.length > 0) {
      logger.info(`[AUTO-DETECT] Selected first available printer from list: "${printers[0].name}"`);
      return printers[0].name;
    }
  } catch (err) {
    logger.warn(`[AUTO-DETECT] Could not list installed printers: ${err.message}`);
  }

  // 4. Final fallback
  const fallback = 'Microsoft Print to PDF';
  logger.warn(`[AUTO-DETECT] Fallback printer selected: "${fallback}"`);
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
