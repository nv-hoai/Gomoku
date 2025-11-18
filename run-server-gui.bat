@echo off
echo ================================
echo   Starting Gomoku Server (GUI)
echo ================================
echo.

cd /d "%~dp0"

if not exist "MainServer\bin\Debug\net8.0-windows\MainServer.exe" (
    echo ERROR: MainServer.exe not found!
    echo Please run build-all.bat first.
    pause
    exit /b 1
)

echo Starting GUI server on port 5000...
echo Worker port: 5001
echo.

cd MainServer\bin\Debug\net8.0-windows
start MainServer.exe

echo.
echo Server GUI launched!
echo Close the window to stop the server.
echo.
