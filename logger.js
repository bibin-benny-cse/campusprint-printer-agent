function formatTime() {
  return new Date().toISOString();
}

function info(message) {
  console.log(`[${formatTime()}] [INFO] ${message}`);
}

function error(message, err) {
  console.error(`[${formatTime()}] [ERROR] ${message}`, err ? (err.message || err) : '');
}

module.exports = {
  info,
  error
};
