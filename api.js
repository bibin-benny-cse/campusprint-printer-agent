const axios = require('axios');
const fs = require('fs-extra');
const path = require('path');
const config = require('./config');

const apiClient = axios.create({
  baseURL: config.apiUrl,
  timeout: 15000, // 15-second request timeout
});

async function getPrintQueue() {
  try {
    const response = await apiClient.get('/print-queue');
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
  getPrintQueue,
  downloadPdf,
  updateJobStatus
};
