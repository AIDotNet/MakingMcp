@echo off
setlocal

echo ========================================
echo MakingMcp Web Service Installer
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

echo Installing MakingMcp Web Service...
echo Executable: %EXE_PATH%
echo.

"%EXE_PATH%" install

if %errorLevel% equ 0 (
    echo.
    echo ========================================
    echo Installation completed successfully!
    echo ========================================
    echo.
    echo Service Name: MakingMcpWebService
    echo Status: Running
    echo.
    echo You can manage the service using:
    echo   - Services.msc
    echo   - sc stop MakingMcpWebService
    echo   - sc start MakingMcpWebService
    echo   - net stop MakingMcpWebService
    echo   - net start MakingMcpWebService
    echo.
) else (
    echo.
    echo Installation failed. Please check the error messages above.
    echo.
)

pause
