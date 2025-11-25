@echo off
title WorkerServer GUI - Gomoku Worker Server
echo ================================
echo   Gomoku Worker Server (GUI)
echo ================================
echo - Starting WPF GUI Application
echo - Connects to MainServer at port 5001
echo - Handles AI calculations and move validation
echo ================================
echo.

cd /d "%~dp0WorkerServer"
dotnet run

pause
