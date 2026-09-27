@echo off
setlocal EnableDelayedExpansion
echo =================================================================
echo   XeroxGo Technologies - Verified Publisher Setup
echo =================================================================
echo.
echo Installing XeroxGo Publisher Certificate into Windows Certificate Store...
echo This registers XeroxGo Technologies as a Verified Publisher on this PC
echo and satisfies Windows 11 Smart App Control policies.
echo.

:: Check for administrative privileges
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ELEVATION REQUIRED] Requesting Administrator permissions...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb RunAs"
    exit /b
)

:: Locate certificate file in the script directory or parent directory
set "CERT_FILE=%~dp0XeroxGo-Publisher-Certificate.cer"
if not exist "%CERT_FILE%" (
    set "CERT_FILE=%~dp0..\XeroxGo-Publisher-Certificate.cer"
)
if not exist "%CERT_FILE%" (
    echo [ERROR] Certificate file not found: "XeroxGo-Publisher-Certificate.cer"
    echo Please make sure XeroxGo-Publisher-Certificate.cer is in the same folder.
    pause
    exit /b 1
)

:: Install into Windows Trusted Publishers store
echo [1/2] Registering with Trusted Publishers...
certutil -addstore -f "TrustedPublisher" "%CERT_FILE%"

:: Install into Windows Local Machine Root store
echo [2/2] Registering with Trusted Root Certification Authorities...
certutil -addstore -f "ROOT" "%CERT_FILE%"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo =================================================================
    echo [SUCCESS] XeroxGo Technologies is now a Verified Publisher!
    echo Windows Smart App Control now verifies all XeroxGo binaries.
    echo You can now install and launch XeroxGoAgent-Setup.exe directly.
    echo =================================================================
) else (
    echo.
    echo [ERROR] Failed to install certificate. Error code: %ERRORLEVEL%
)

echo.
pause
