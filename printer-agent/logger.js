const fs = require('fs');
const path = require('path');

const logsDir = path.join(__dirname, 'logs');
if (!fs.existsSync(logsDir)) {
  fs.mkdirSync(logsDir, { recursive: true });
}

const logFilePath = path.join(logsDir, 'agent.log');

function formatTime() {
  return new Date().toISOString();
}

function writeToFile(level, message) {
  const line = `[${formatTime()}] [${level}] ${message}\n`;
  fs.appendFile(logFilePath, line, (err) => {
    if (err) console.error('Failed to write log file:', err);
  });
}

function info(message) {
  console.log(`[${formatTime()}] [INFO] ${message}`);
  writeToFile('INFO', message);
}

function warn(message) {
  console.warn(`[${formatTime()}] [WARN] ${message}`);
  writeToFile('WARN', message);
}

function error(message, err) {
  const errDetails = err ? (err.stack || err.message || err) : '';
  const fullMsg = errDetails ? `${message} - ${errDetails}` : message;
  console.error(`[${formatTime()}] [ERROR] ${fullMsg}`);
  writeToFile('ERROR', fullMsg);
}

module.exports = {
  info,
  warn,
  error
};
