@echo off
rem Starts the local game server if it is not running, then opens the game.
rem The server reads dist/ from disk with no-cache, so this always shows the latest changes.
cd /d "%~dp0"
netstat -ano | findstr /r /c:"127.0.0.1:4173 .*LISTENING" >nul
if errorlevel 1 (
  start "AiOn game server" /min node server.mjs
  timeout /t 2 /nobreak >nul
)
start "" "http://127.0.0.1:4173/"
