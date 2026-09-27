@echo off
cd /d "%~dp0"
echo =========================================================
echo    CampusPrint Printer Agent - Windows Auto-Start Setup
echo =========================================================
echo.


set STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
set SCRIPT_DIR=%~dp0

:: Create a VBScript launcher shortcut in Windows Startup folder
echo Set WshShell = CreateObject("WScript.Shell") > "%STARTUP_DIR%\CampusPrintAgent.vbs"
echo WshShell.CurrentDirectory = "%SCRIPT_DIR:~0,-1%" >> "%STARTUP_DIR%\CampusPrintAgent.vbs"
echo WshShell.Run "cmd /c node index.js", 0, False >> "%STARTUP_DIR%\CampusPrintAgent.vbs"

echo SUCCESS!
echo The printer agent has been added to Windows Startup.
echo It will now run silently in the background whenever your PC turns on.
echo.
echo Launching background agent now...
wscript "%STARTUP_DIR%\CampusPrintAgent.vbs"
echo Agent started in background!
echo.
pause
