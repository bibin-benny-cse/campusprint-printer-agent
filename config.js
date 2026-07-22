const path = require('path');

module.exports = {
  // Uses the local backend by default, can be updated to the production Render URL later
  apiUrl: process.env.API_URL || 'http://localhost:3001/api',
  // Default printer
  printerName: process.env.PRINTER_NAME || 'Microsoft Print to PDF',
  pollIntervalMs: 3000,
  tempDir: path.join(__dirname, 'temp'),
};
