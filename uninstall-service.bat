@echo off
:: Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] This uninstaller requires Administrator privileges.
    echo Right-click "uninstall-service.bat" and select "Run as administrator".
    pause
    exit /b 1
)

echo ===============================================================
echo    Uninstalling CampusPrint Printer Agent Windows Service
echo ===============================================================
echo.
node uninstall-service.js
echo.
pause
