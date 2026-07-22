const axios = require('axios');
const fs = require('fs-extra');
const path = require('path');
const config = require('./config');

const apiClient = axios.create({
  baseURL: config.apiUrl,
});

async function getPrintQueue() {
  try {
    const response = await apiClient.get('/print-queue');
    return response.data;
  } catch (error) {
    throw new Error(`Failed to fetch print queue: ${error.message}`);
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
      writer.on('error', (err) => reject(new Error(`Failed to write file ${filename}: ${err.message}`)));
    });
  } catch (error) {
    throw new Error(`Failed to download ${filename}: ${error.message}`);
  }
}

async function updateJobStatus(jobId, status) {
  try {
    await apiClient.patch(`/jobs/${jobId}`, { status });
  } catch (error) {
    throw new Error(`Failed to update job ${jobId} status to ${status}: ${error.message}`);
  }
}

module.exports = {
  getPrintQueue,
  downloadPdf,
  updateJobStatus
};
