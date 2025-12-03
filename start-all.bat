@echo off
title Auto Start - Gomoku Servers
echo ================================
echo Gomoku Auto-Start (Optimized)
echo ================================
echo Starting servers in optimal order...
echo.

REM Start Worker GUI (background, no CMD window)
echo [1/2] Starting Worker Server GUI...
cd /d "%~dp0WorkerServer"
start /B "" dotnet run
cd /d "%~dp0"

REM Wait for Worker to initialize
echo [2/2] Waiting for Worker to initialize (2 sec)...
timeout /t 2 /nobreak > nul

REM Start Main Server GUI
echo Starting Main Server GUI...
start "" "%~dp0run-server-gui.bat"

echo.
echo ================================
echo Servers Started Successfully!
echo ================================
echo Worker: Auto-starting in background
echo MainServer: Opening GUI...
echo ================================
echo.
exit
