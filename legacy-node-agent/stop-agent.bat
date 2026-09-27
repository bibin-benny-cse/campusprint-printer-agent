@echo off
cd /d "%~dp0"
echo Stopping CampusPrint Printer Agent process and Windows Service...

net stop CampusPrintAgent 2>nul
wmic process where "name='node.exe' and commandline like '%%index.js%%'" call terminate 2>nul

echo Done! CampusPrint Agent has been safely stopped.
pause

