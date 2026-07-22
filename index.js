const queue = require('./queue');
const logger = require('./logger');
const fs = require('fs-extra');
const config = require('./config');

async function init() {
  try {
    logger.info('--- CampusPrint Printer Agent Startup ---');
    logger.info(`Configured API URL: ${config.apiUrl}`);
    logger.info(`Configured Printer: ${config.printerName}`);
    
    // Ensure temp directory exists at startup
    await fs.ensureDir(config.tempDir);
    logger.info(`Temporary directory ready at ${config.tempDir}`);

    queue.startPolling();
    
  } catch (error) {
    logger.error('Startup failed', error);
    process.exit(1);
  }
}

// Handle graceful shutdown
process.on('SIGINT', () => {
  logger.info('Shutting down gracefully (Ctrl+C received)...');
  process.exit(0);
});

process.on('unhandledRejection', (reason, promise) => {
  logger.error('Unhandled Rejection at:', promise, 'reason:', reason);
});

init();
