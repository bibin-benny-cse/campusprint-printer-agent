# CampusPrint Printer Agent (Windows Service Edition)

A high-reliability, lightweight Node.js daemon designed for continuous operation on college print-shop PCs. It automatically polls your CampusPrint backend for print queue items and sends them directly to your physical printer without browser intervention.

---

## 🛡️ Enterprise Features

- **⚡ Native Windows Service**: Installs into Windows `services.msc` to start automatically on system boot before user login.
- **🔒 Single-Instance Protection**: Prevents duplicate instances via localhost port locking (`port 39201`).
- **🔄 Auto-Reconnection & Backoff**: Automatically handles network outages, Wi-Fi drops, or Render backend cold starts with smooth exponential backoff.
- **📄 Resilient Error Recovery**: Unblocks print queue on job failures so subsequent documents print seamlessly.
- **📝 Persistent File Logging**: Writes real-time diagnostic logs to `logs/agent.log`.

---

## 🛠️ Quick Installation & Setup

1. **Install Node.js**: Ensure Node.js (v18+) is installed on your Windows shop PC ([Download Node.js](https://nodejs.org/)).
2. **Install Dependencies**:
   Open Command Prompt in `printer-agent` and run:
   ```cmd
   npm install
   ```
3. **Configure Printer & API** in `config.js`:
   ```javascript
   module.exports = {
     apiUrl: process.env.API_URL || 'https://campusprint-backend-bl6p.onrender.com/api',
     printerName: process.env.PRINTER_NAME || 'Your Shop Printer Name',
     pollIntervalMs: 3000,
   };
   ```

---

## 🚀 Installation Options

### Option 1: Native Windows Service (Recommended for Shop Production)
Runs completely silently in the background, starts on system reboot, and auto-restarts on failure.

1. Right-click **`install-service.bat`** and select **Run as administrator**.
2. To uninstall the service at any time, right-click **`uninstall-service.bat`** and select **Run as administrator**.

### Option 2: Stop / Restart Agent
- Double-click **`stop-agent.bat`** to safely terminate the running agent process or service.

---

## 📊 Viewing Logs
Diagnostic logs are written in real-time to:
```text
printer-agent/logs/agent.log
```
