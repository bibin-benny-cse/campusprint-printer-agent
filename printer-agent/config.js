require('dotenv').config();
const path = require('path');

module.exports = {
  // Uses the live Render backend URL by default, can be overridden by API_URL env variable
  apiUrl: process.env.API_URL || 'https://campusprint-backend-bl6p.onrender.com/api',
  // Default printer
  printerName: process.env.PRINTER_NAME || 'Microsoft Print to PDF',
  pollIntervalMs: 3000,
  tempDir: path.join(__dirname, 'temp'),
};
