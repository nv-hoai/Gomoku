@echo off
title Start All - Gomoku Distributed System
echo ================================
echo Gomoku Distributed System Launcher
echo ================================
echo This will start:
echo 1. MainServer (Game Server)
echo 2. WorkerServer (AI Worker)
echo.
echo Make sure to start MainServer FIRST!
echo ================================
pause

echo Starting MainServer...
start "MainServer" "%~dp0run-server-gui.bat"

echo Waiting 3 seconds for MainServer to initialize...
timeout /t 3 /nobreak > nul

echo Starting WorkerServer...
start "WorkerServer" "%~dp0run-worker-gui.bat"

echo.
echo ================================
echo Both servers are starting!
echo ================================
echo MainServer: Game clients and workers
echo WorkerServer: AI processing
echo.
