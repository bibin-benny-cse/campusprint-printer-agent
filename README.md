# CampusPrint Printer Agent

This is a standalone Node.js application that runs on your local computer to automatically fetch print jobs from your CampusPrint backend and send them directly to your local printer. 

It completely bypasses the browser print dialog to enable a fully automated print queue system.

## 🚀 Installation

1. Make sure you have **Node.js** installed on your Windows machine.
2. Open a terminal or command prompt in this directory (`printer-agent`).
3. (Optional if already installed) Run the following command to install dependencies:
   ```bash
   npm install
   ```

## ⚙️ Configuration

Open the `config.js` file in any text editor. It contains the following properties:

```javascript
module.exports = {
  apiUrl: process.env.API_URL || 'http://localhost:3001/api', // The backend API URL
  printerName: process.env.PRINTER_NAME || 'Microsoft Print to PDF', // The name of your printer
  pollIntervalMs: 3000, // How often to check for new jobs (in milliseconds)
  tempDir: path.join(__dirname, 'temp'), // Folder where PDFs are temporarily downloaded
};
```

### Changing the Printer
By default, this is set to use **Microsoft Print to PDF** for testing. 

To change this to your **Canon printer** (or any other physical printer):
1. Go to **Windows Settings > Bluetooth & devices > Printers & scanners**.
2. Find the exact name of your Canon printer (e.g., `Canon LBP2900`).
3. Copy that EXACT name and paste it into `config.js` under `printerName`.

### Integrating with Production Backend
Once you deploy the CampusPrint backend changes to your production Render server, update the `apiUrl` in `config.js`:
```javascript
apiUrl: 'https://campusprint-backend-kzte.onrender.com/api',
```

## ▶️ Running the Agent

To start the automated printer agent, run:
```bash
npm start
```
*Or run `node index.js`.*

The application will begin polling your backend API every 3 seconds for new `ReadyToPrint` jobs. 

**Workflow:**
1. When you click **Print** on the Admin dashboard, the job goes into the `ReadyToPrint` status.
2. The agent detects it, downloads the PDF file, and automatically prints it.
3. The job status will update to `Completed` on the dashboard, and the temporary PDF is deleted locally.

If you ever need to stop the agent, simply press `Ctrl + C` in the terminal window.
