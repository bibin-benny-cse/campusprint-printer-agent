const api = require('./api');
const printer = require('./printer');
const logger = require('./logger');
const config = require('./config');
const fs = require('fs-extra');

let isProcessing = false;
let isOffline = false;
let pollTimer = null;
let heartbeatTimer = null;

async function sendAgentHeartbeat(currentStatus = 'Idle', currentJobId = null) {
  try {
    await api.sendHeartbeat(config.printerName || 'Printer 1', currentStatus, currentJobId);
  } catch (err) {
    // Ignore heartbeat failures
  }
}

async function processQueue() {
  if (isProcessing) return;
  isProcessing = true;

  let nextDelay = config.pollIntervalMs;

  try {
    // Send heartbeat
    await sendAgentHeartbeat(isProcessing ? 'Printing' : 'Idle');

    const queue = await api.getPrintQueue(config.printerName);
    
    // Connection succeeded! Reset offline state if coming back online
    if (isOffline) {
      isOffline = false;
      logger.info(`[RECONNECT] Connection restored! Resuming print queue polling every ${config.pollIntervalMs}ms.`);
    }

    if (!queue || queue.length === 0) {
      await sendAgentHeartbeat('Idle');
      isProcessing = false;
      scheduleNextPoll(nextDelay);
      return;
    }

    // Process single job at a time to prevent conflicts
    const job = queue[0];
    logger.info(`[JOB DETECTED] Processing Job ID=${job.id}, file="${job.filename}", copies=${job.copies}`);
    
    // 1. Update status to Printing
    await api.updateJobStatus(job.id, 'Printing').catch(err => {
      logger.warn(`Could not set status to Printing for job ${job.id}: ${err.message}`);
    });

    await sendAgentHeartbeat('Printing', job.id);
    
    let filePath = null;
    try {
      // 2. Download document
      filePath = await api.downloadPdf(job.filename);
      logger.info(`[DOWNLOAD SUCCESS] Downloaded file to ${filePath}`);

      // 3. Send to printer
      logger.info(`[PRINTING] Sending Job ID=${job.id} to printer "${config.printerName}"...`);
      await printer.printPdf(filePath, {
        copies: job.copies,
        mode: job.mode,
        sides: job.sides,
        pageRange: job.pageRange,
        pagesPerSheet: job.pagesPerSheet
      });
      logger.info(`[PRINT SUCCESS] Document printed successfully for Job ID=${job.id}`);

      // 4. Update status to Completed
      await api.updateJobStatus(job.id, 'Completed').catch(err => {
        logger.warn(`Could not set status to Completed for job ${job.id}: ${err.message}`);
      });
      await sendAgentHeartbeat('Idle', null);

    } catch (err) {
      logger.error(`[PRINT ERROR] Failed processing job ID=${job.id}`, err);
      // Unblock queue by setting job to Failed
      await api.updateJobStatus(job.id, 'Failed').catch(e => {
        logger.error(`Could not set status to Failed for job ${job.id}`, e);
      });
      await sendAgentHeartbeat('Error', null);
    } finally {
      // 5. Always cleanup temp file
      if (filePath) {
        try {
          await fs.remove(filePath);
          logger.info(`[CLEANUP] Deleted temporary file: ${filePath}`);
        } catch (err) {
          logger.error(`[CLEANUP ERROR] Failed deleting temp file ${filePath}`, err);
        }
      }
    }
  } catch (error) {
    if (!isOffline) {
      isOffline = true;
      logger.warn(`[NETWORK OFFLINE] Unable to reach backend server (${error.message}). Retrying in background...`);
    }
    // Smooth backoff delay during network outages (10 seconds)
    nextDelay = 10000;
  } finally {
    isProcessing = false;
    scheduleNextPoll(nextDelay);
  }
}

function scheduleNextPoll(delayMs) {
  if (pollTimer) clearTimeout(pollTimer);
  pollTimer = setTimeout(processQueue, delayMs);
}

function startPolling() {
  logger.info(`Starting queue polling for "${config.printerName || 'Printer 1'}" every ${config.pollIntervalMs}ms...`);
  sendAgentHeartbeat('Idle');
  heartbeatTimer = setInterval(() => sendAgentHeartbeat(isProcessing ? 'Printing' : 'Idle'), 5000);
  processQueue(); // Immediate first run
}

function stopPolling() {
  if (pollTimer) {
    clearTimeout(pollTimer);
    pollTimer = null;
  }
  if (heartbeatTimer) {
    clearInterval(heartbeatTimer);
    heartbeatTimer = null;
  }
}

module.exports = {
  startPolling,
  stopPolling
};
