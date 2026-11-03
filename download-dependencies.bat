@echo off
rem Fetches every pinned, digest-verified dependency into .deps\ (see deps\manifest.json).
rem Usage: download-dependencies.bat [/s]   (/s, --silent or SILENT=1 runs without prompts)
setlocal
set "ARGS="
if /i "%~1"=="/s" set "ARGS=-Silent"
if /i "%~1"=="--silent" set "ARGS=-Silent"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\download-dependencies.ps1" %ARGS%
exit /b %ERRORLEVEL%
