const axios = require('axios');
const fs = require('fs-extra');
const path = require('path');
const config = require('./config');
const logger = require('./logger');

const apiClient = axios.create({
  baseURL: config.apiUrl,
  timeout: 15000, // 15-second request timeout
});

async function sendHeartbeat(printerName, status = 'Idle', currentJobId = null) {
  try {
    await apiClient.post('/printers/heartbeat', { printerName, status, currentJobId });
  } catch (error) {
    const msg = error.response ? `HTTP ${error.response.status}` : error.message;
    logger.warn(`[HEARTBEAT FAILED] Could not send status ping for printer "${printerName}": ${msg}`);
  }
}

async function getPrintQueue(printerName) {
  try {
    const params = printerName ? { printerName } : {};
    const response = await apiClient.get('/print-queue', { params });
    return response.data;
  } catch (error) {
    const msg = error.response ? `HTTP ${error.response.status}` : error.message;
    throw new Error(`Queue fetch failed (${msg})`);
  }
}

async function downloadPdf(filename) {
  const filePath = path.join(config.tempDir, filename);
  try {
    const response = await apiClient.get(`/files/${filename}`, {
      responseType: 'stream',
    });
    
    await fs.ensureDir(config.tempDir);
    
    const writer = fs.createWriteStream(filePath);
    response.data.pipe(writer);
    
    return new Promise((resolve, reject) => {
      writer.on('finish', () => resolve(filePath));
      writer.on('error', (err) => reject(new Error(`Failed writing temp file ${filename}: ${err.message}`)));
    });
  } catch (error) {
    const msg = error.response ? `HTTP ${error.response.status}` : error.message;
    throw new Error(`Download failed for ${filename} (${msg})`);
  }
}

async function updateJobStatus(jobId, status) {
  try {
    await apiClient.patch(`/jobs/${jobId}`, { status });
  } catch (error) {
    const msg = error.response ? `HTTP ${error.response.status}` : error.message;
    throw new Error(`Status update failed for job ${jobId} -> ${status} (${msg})`);
  }
}

module.exports = {
  sendHeartbeat,
  getPrintQueue,
  downloadPdf,
  updateJobStatus
};
