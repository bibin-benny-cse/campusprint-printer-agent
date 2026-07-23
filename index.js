const queue = require('./queue');
const logger = require('./logger');
const lock = require('./lock');
const fs = require('fs-extra');
const config = require('./config');

async function init() {
  // 1. Single-Instance Verification
  const locked = await lock.acquireLock();
  if (!locked) {
    logger.warn('Shutting down duplicate process invocation.');
    process.exit(0);
  }

  try {
    logger.info('===================================================');
    logger.info('--- CampusPrint Printer Agent Service Starting ---');
    logger.info(`Backend API URL : ${config.apiUrl}`);
    logger.info(`Physical Printer: ${config.printerName}`);
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
