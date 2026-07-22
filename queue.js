const api = require('./api');
const printer = require('./printer');
const logger = require('./logger');
const config = require('./config');
const fs = require('fs-extra');

let isProcessing = false;

async function processQueue() {
  if (isProcessing) return;
  isProcessing = true;

  try {
    const queue = await api.getPrintQueue();
    
    if (!queue || queue.length === 0) {
      isProcessing = false;
      return;
    }

    // Process only one job at a time to prevent overlapping
    const job = queue[0];
    logger.info(`Job received: ID=${job.id}, file=${job.filename}`);
    
    // 1. Update status to Printing
    await api.updateJobStatus(job.id, 'Printing');
    
    let filePath = null;
    try {
      // 2. Download file
      filePath = await api.downloadPdf(job.filename);
      logger.info(`Download success: ${filePath}`);

      // 3. Print
      logger.info(`Printing started for Job ID=${job.id}`);
      await printer.printPdf(filePath, {
        copies: job.copies,
        mode: job.mode,
        sides: job.sides,
        pageRange: job.pageRange
      });
      logger.info(`Printing completed for Job ID=${job.id}`);

      // 4. Update status to Completed
      await api.updateJobStatus(job.id, 'Completed');

    } catch (err) {
      logger.error(`Error processing job ID=${job.id}`, err);
      // Update status to Failed on error
      await api.updateJobStatus(job.id, 'Failed').catch(e => {
        logger.error(`Could not update job ${job.id} to Failed`, e);
      });
    } finally {
      // 5. Cleanup temporary file
      if (filePath) {
        try {
          await fs.remove(filePath);
          logger.info(`Temporary file deleted: ${filePath}`);
        } catch (err) {
          logger.error(`Failed to delete temporary file ${filePath}`, err);
        }
      }
    }
  } catch (error) {
    logger.error('Error polling queue (Network or Backend might be unreachable)', error.message);
  } finally {
    isProcessing = false;
  }
}

function startPolling() {
  logger.info(`Starting queue polling every ${config.pollIntervalMs}ms...`);
  setInterval(processQueue, config.pollIntervalMs);
  processQueue(); // First run
}

module.exports = {
  startPolling
};
