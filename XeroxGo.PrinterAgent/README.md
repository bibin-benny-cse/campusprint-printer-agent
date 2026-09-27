# 🖨️ XeroxGo Agent (.NET 8 Edition)

A high-reliability, native Windows System Tray application designed for unattended operation on college print-shop counters. It bridges the cloud-hosted **XeroxGo** queue directly with local physical Windows printers.

---

## 🛡️ Enterprise Windows Architecture

1. **System Tray Visibility (Taskbar)**
   - Persistent colored status icon:
     - 🟢 **Green / Blue:** Connected, idle, and listening.
     - 🔵 **Vibrant Blue:** Actively spooling/printing a document.
     - 🟡 **Amber:** Paused by the shopkeeper.
     - 🔴 **Red:** Printer error, paper jam, or backend disconnected.
   - Right-click context menu: *Pause/Resume*, *Printer Settings & Pairing*, *View Activity Logs*, *Exit*.
2. **Quiet By Design (Strict Error-Only Notifications)**
   - **No notification spam:** Normal operations (idle, connected, printing, completed) stay completely silent and only update the tray icon.
   - **Actionable alerts only:** Windows Action Center toast / balloon notifications trigger **strictly on critical failures**:
     - Paper jam detected in spooler.
     - Printer out of paper.
     - Physical printer offline / disconnected.
     - Document download or PDF transformation failure.
     - Consecutive backend connection failures.
   - Includes automatic 45-second duplicate throttling to prevent popup storms.
3. **Native Hardware Telemetry (`System.Printing`)**
   - Direct integration with Windows Print Spooler (`LocalPrintServer` and `PrintQueue`).
   - Reads hardware flags directly from the print driver without third-party CLI wrappers.
4. **Single-Instance Protection**
   - Protected via a system-wide OS named mutex (`Global\XeroxGo_PrinterAgent_SingleInstance_Mutex`). Duplicate executions are safely blocked.
5. **On-The-Fly PDF Imposition (`PdfSharp`)**
   - Generates 2-Up sheet imposition (side-by-side or top-bottom) locally on the fly.
   - Applies custom per-page rotation angles (90°, 180°, 270°).
   - Filters out excluded pages and enforces custom page ordering before spooling.
6. **Zero-Dependency Self-Contained Deployment**
   - Compiles into a single `.exe` file. The shop owner **does not** need to install Node.js, Python, or the .NET runtime.

---

## 🛠️ Building & Publishing

### 1. Build Single-File Executable
From a machine with the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0):

```bash
cd campusprint-printer-agent/XeroxGo.PrinterAgent

# Publish a single self-contained executable for Windows 64-bit
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The output will be generated at:
```text
bin/Release/net8.0-windows/win-x64/publish/XeroxGo.PrinterAgent.exe
```

### 2. Generate Installer (Inno Setup)
1. Install [Inno Setup 6+](https://jrsoftware.org/isdl.php).
2. Open `Installer/setup.iss` and click **Compile** (or run `iscc Installer/setup.iss`).
3. An installer named `XeroxGoAgent-Setup.exe` will be generated in `Installer/Output/`.

---

## 📂 Configuration & Storage Locations

All agent settings and logs are stored in standard Windows application folders:
- **Configuration File:** `%LocalAppData%\XeroxGo\config.json`
- **Activity Logs:** `%LocalAppData%\XeroxGo\logs\agent.log` (automatically rotates at 5 MB)
- **Temporary Cache:** `%LocalAppData%\XeroxGo\temp` (ephemeral files are shredded immediately after printing)
- **Startup Registry Key:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\XeroxGoPrinterAgent`
