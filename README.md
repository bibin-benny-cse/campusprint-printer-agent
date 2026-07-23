# CampusPrint Printer Agent

This is a standalone Node.js application that runs on your local computer at the shop to automatically fetch print jobs from your CampusPrint backend and send them directly to your physical printer. 

It completely bypasses the browser print dialog to enable a fully automated print queue system.

## 🚀 Installation

1. Make sure you have **Node.js** installed on your Windows machine ([Download Node.js](https://nodejs.org/)).
2. Open a terminal or command prompt in this directory (`printer-agent`).
3. Run the following command to install dependencies:
   ```cmd
   npm install
   ```

## ⚙️ Configuration

Open the `config.js` file in any text editor. It contains the following properties:

```javascript
module.exports = {
  // Production Render Backend API URL (default)
  apiUrl: process.env.API_URL || 'https://campusprint-backend-bl6p.onrender.com/api', 

  // The name of your physical printer in Windows Settings
  printerName: process.env.PRINTER_NAME || 'Microsoft Print to PDF', 

  // How often to check for new jobs (in milliseconds)
  pollIntervalMs: 3000, 

  // Folder where files are temporarily downloaded
  tempDir: path.join(__dirname, 'temp'), 
};
```

### Changing the Printer Name
By default, this is set to use **Microsoft Print to PDF** for testing. 

To connect your **shop printer** (e.g., Canon, Epson, HP):
1. Open Windows Settings on your shop computer.
2. Go to **Bluetooth & devices > Printers & scanners**.
3. Find the exact name of your active printer (e.g., `Canon LBP2900`).
4. Copy that EXACT name and update `printerName` in `config.js`.

---

## ▶️ Running the Agent

To start the automated printer agent, run:
```cmd
npm start
```
*Or run `node index.js`.*

The application will begin polling your backend API every 3 seconds for new `ReadyToPrint` jobs. 

**Workflow:**
1. When a customer uploads a document, the job is saved as `Pending`.
2. When you click **Print** on the Admin dashboard (`/admin`), the job status updates to `ReadyToPrint`.
3. The printer agent detects the job, downloads the file, and automatically prints it on your physical printer.
4. The job status automatically updates to `Completed` on the dashboard, and the local temporary file is cleaned up.

If you ever need to stop the agent, press `Ctrl + C` in the terminal window.
