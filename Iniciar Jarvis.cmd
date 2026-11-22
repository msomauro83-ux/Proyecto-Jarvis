@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0dist\Jarvis.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo No se pudo compilar Jarvis.
    pause
    exit /b 1
  )
)
start "" "%~dp0dist\Jarvis.exe"
