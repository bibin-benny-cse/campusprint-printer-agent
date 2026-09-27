# 🖨️ XeroxGo Agent

[![Release](https://img.shields.io/github/v/release/bibin-benny-cse/campusprint-printer-agent?color=blue&label=Latest%20Release)](https://github.com/bibin-benny-cse/campusprint-printer-agent/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6?logo=windows)](https://github.com/bibin-benny-cse/campusprint-printer-agent/releases/latest)
[![Framework](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

A high-reliability, native Windows System Tray application designed for unattended operation on college print-shop counters. It bridges the cloud-hosted **XeroxGo** queue directly with local physical Windows printers.

---

## ⚡ Quick Install & Clean Uninstall (Recommended)

### 📥 Install or Update
Run this one-line command in **Windows PowerShell** to automatically download, verify, and launch the installer:

```powershell
irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex
```
*(Seamless installation with zero Smart App Control friction.)*

### 🗑️ Full Clean Uninstall
The same script cleanly purges all processes, local configs, logs, shortcuts, and publisher certificates:
- **Interactive:** Simply run the command above on an installed machine and choose option **[2] Full Clean Uninstall**.
- **Or Direct Command:**
  ```powershell
  & ([scriptblock]::Create((irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1))) -Uninstall
  ```
*(Leaves zero residual files, registry keys, or certificates on the system.)*

---

## 📦 Manual Downloads & Releases

Get pre-compiled binaries from the **[Releases Page](https://github.com/bibin-benny-cse/campusprint-printer-agent/releases/latest)**:

| Asset | Format | Recommended For | Direct Download |
| :--- | :--- | :--- | :--- |
| **`XeroxGoAgent-Setup.exe`** | 1-Click Installer | Shopkeepers & Counter PCs (includes Start Menu shortcuts & auto-updater hooks) | [Download Installer](https://github.com/bibin-benny-cse/campusprint-printer-agent/releases/latest/download/XeroxGoAgent-Setup.exe) |
| **`XeroxGo.PrinterAgent.exe`** | Standalone Single-File Binary | Portable execution without installation | [Download Portable EXE](https://github.com/bibin-benny-cse/campusprint-printer-agent/releases/latest/download/XeroxGo.PrinterAgent.exe) |

> **Zero Dependencies:** Both packages are self-contained. The print shop computer **does not** require .NET 8 or any runtimes installed.

---

## 🛡️ Enterprise Windows Architecture

1. **System Tray Visibility (Taskbar)**
   - Persistent status icon:
     - 🟢 **Green:** Idle, connected, and listening for print jobs.
     - 🔵 **Vibrant Blue:** Actively spooling/printing a document.
     - 🟡 **Amber:** Paused by the shopkeeper.
     - 🔴 **Red:** Printer error, paper jam, or backend disconnected.
   - Right-click context menu: *Settings & Pairing*, *Pause/Resume*, *View Activity Logs*, *Exit*.

2. **Quiet By Design (Strict Error-Only Notifications)**
   - **No notification spam:** Normal operations (idle, connected, printing, completed) stay completely silent and only update the tray icon.
   - **Actionable alerts only:** Windows Action Center toast / balloon notifications trigger **strictly on critical failures**:
     - Paper jam detected in spooler.
     - Printer out of paper.
     - Physical printer offline / disconnected.
     - Document download or PDF transformation failure.
     - Consecutive backend connection failures.
   - Automatic 45-second duplicate throttling to prevent popup storms.

3. **Native Hardware Telemetry (`System.Printing`)**
   - Direct integration with Windows Print Spooler (`LocalPrintServer` and `PrintQueue`).
   - Reads hardware flags directly from the print driver without third-party CLI wrappers.

4. **Single-Instance Protection**
   - Protected via a system-wide OS named mutex (`Global\XeroxGo_PrinterAgent_SingleInstance_Mutex`). Duplicate executions are safely blocked.

5. **On-The-Fly PDF Imposition (`PdfSharp`)**
   - Generates 2-Up sheet imposition (side-by-side or top-bottom) locally on the fly.
   - Applies custom per-page rotation angles (90°, 180°, 270°).
   - Filters out excluded pages and enforces custom page ordering before spooling.

6. **Automatic Document Shredding**
   - Temporary spooled documents are deleted from `%LocalAppData%\XeroxGo\temp` immediately after printing.

---

## ⚙️ Quick Pairing Instructions

1. Run **`XeroxGoAgent-Setup.exe`** on the print shop PC.
2. Right-click the green XeroxGo icon in the Windows taskbar tray -> **Settings & Pairing**:
   - **Backend API URL:** `https://<your-render-backend-name>.onrender.com/api`
   - **Agent API Key:** Enter your shop counter API key (e.g. `xeroxgo_super_secret_agent_key_2026`).
   - **Select Printer:** Select the counter's physical USB or network printer from the dropdown.
3. Click **Save & Connect**.

---

## 🛠️ Building From Source

### Automated Cloud Build (GitHub Actions)
Pushes to `main` or new git tags (e.g., `v0.1.0`) automatically trigger [`.github/workflows/build-agent.yml`](.github/workflows/build-agent.yml) which builds both the self-contained executable and the Inno Setup installer.

### Local Compilation (.NET 8 SDK)
```bash
cd XeroxGo.PrinterAgent

# Publish a single self-contained executable for Windows 64-bit
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output:
```text
XeroxGo.PrinterAgent/bin/Release/net8.0-windows/win-x64/publish/XeroxGo.PrinterAgent.exe
```

### Inno Setup Installer
Open `XeroxGo.PrinterAgent/Installer/setup.iss` in [Inno Setup 6+](https://jrsoftware.org/isdl.php) and compile to generate `XeroxGoAgent-Setup.exe`.

---

## 📂 Configuration & Storage Locations

All agent settings and logs are stored in standard Windows application folders:
- **Configuration File:** `%LocalAppData%\XeroxGo\config.json`
- **Activity Logs:** `%LocalAppData%\XeroxGo\logs\agent.log` (automatically rotates at 5 MB)
- **Temporary Cache:** `%LocalAppData%\XeroxGo\temp` (ephemeral files are shredded immediately after printing)
- **Startup Registry Key:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\XeroxGoPrinterAgent`
