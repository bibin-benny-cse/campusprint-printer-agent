const net = require('net');
const logger = require('./logger');

const LOCK_PORT = 39201;
let lockServer = null;

/**
 * Acquires a single-instance lock by binding to a local port.
 * Returns true if lock was acquired, or false if another instance is already running.
 */
function acquireLock() {
  return new Promise((resolve) => {
    lockServer = net.createServer();

    lockServer.once('error', (err) => {
      if (err.code === 'EADDRINUSE') {
        logger.warn(`[SINGLE INSTANCE LOCK] Another instance of CampusPrint Printer Agent is already active on port ${LOCK_PORT}.`);
        resolve(false);
      } else {
        logger.error(`[SINGLE INSTANCE LOCK] Unexpected lock server error: ${err.message}`);
        resolve(true);
      }
    });

    lockServer.once('listening', () => {
      logger.info(`[SINGLE INSTANCE LOCK] Single-instance lock acquired on localhost:${LOCK_PORT}.`);
      resolve(true);
    });

    lockServer.listen(LOCK_PORT, '127.0.0.1');
  });
}

function releaseLock() {
  if (lockServer) {
    try {
      lockServer.close();
      lockServer = null;
      logger.info('[SINGLE INSTANCE LOCK] Single-instance lock released.');
    } catch (err) {
      // Ignore cleanup errors on exit
    }
  }
}

module.exports = {
  acquireLock,
  releaseLock
};
