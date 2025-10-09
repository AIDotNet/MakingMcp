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

set "EXE_PATH=%~dp0src\MakingMcp.Web\bin\Release\net10.0\win-x64\publish\MakingMcp.Web.exe"

if not exist "%EXE_PATH%" (
    echo Error: Executable not found at:
    echo %EXE_PATH%
    echo.
    echo Please build and publish the project first:
    echo   dotnet publish src\MakingMcp.Web\MakingMcp.Web.csproj -c Release -r win-x64 --self-contained
    echo.
    pause
    exit /b 1
)

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
