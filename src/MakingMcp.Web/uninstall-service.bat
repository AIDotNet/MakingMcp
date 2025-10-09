@echo off
setlocal

echo ========================================
echo MakingMcp Web Service Uninstaller
echo ========================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Error: Administrator privileges required.
    echo Please run this script as Administrator.
    echo.
    pause
    exit /b 1
)

set "EXE_PATH=%~dp0\MakingMcp.Web.exe"

if not exist "%EXE_PATH%" (
    echo Warning: Executable not found at:
    echo %EXE_PATH%
    echo.
    echo Attempting to uninstall service anyway...
    echo.
)

echo Uninstalling MakingMcp Web Service...
echo.

if exist "%EXE_PATH%" (
    "%EXE_PATH%" uninstall
) else (
    sc stop MakingMcpWebService
    timeout /t 2 /nobreak >nul
    sc delete MakingMcpWebService
)

if %errorLevel% equ 0 (
    echo.
    echo ========================================
    echo Uninstallation completed successfully!
    echo ========================================
    echo.
) else (
    echo.
    echo Uninstallation failed. Please check the error messages above.
    echo.
)

pause
