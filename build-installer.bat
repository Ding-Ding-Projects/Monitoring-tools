@echo off
rem Produces dist\MonitoringTools-Setup.exe: the bundled single-file app, which installs
rem itself per user on first run (no administrator rights), plus a zip of the same file.
rem Usage: build-installer.bat [version]
setlocal EnableExtensions
set "ROOT=%~dp0"

call "%ROOT%build.bat" %*
if errorlevel 1 exit /b %ERRORLEVEL%

if not exist "%ROOT%dist" mkdir "%ROOT%dist"
copy /y "%ROOT%out\MonitoringTools.exe" "%ROOT%dist\MonitoringTools-Setup.exe" >nul
if errorlevel 1 (
  echo [installer] FAILED: could not write dist\MonitoringTools-Setup.exe
  exit /b 1
)
powershell -NoProfile -Command "Compress-Archive -Force -Path '%ROOT%dist\MonitoringTools-Setup.exe' -DestinationPath '%ROOT%dist\MonitoringTools-win-x64.zip'"
if errorlevel 1 (
  echo [installer] FAILED: could not write the zip
  exit /b 1
)
echo [installer] dist\MonitoringTools-Setup.exe and dist\MonitoringTools-win-x64.zip are ready
exit /b 0
