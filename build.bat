@echo off
rem Builds the self-contained single-file app into out\MonitoringTools.exe with every
rem dependency bundled inside. Fetches pinned dependencies first.
rem Usage: build.bat [version]      e.g. build.bat 1.2.3   (default 1.0.0-dev)
setlocal EnableExtensions
set "ROOT=%~dp0"
set "VERSION=%~1"
if "%VERSION%"=="" if defined MT_VERSION set "VERSION=%MT_VERSION%"
if "%VERSION%"=="" set "VERSION=1.0.0-dev"

echo [build] checking the maintainer vocabulary lock
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\check-vocabulary.ps1"
if errorlevel 1 (
  echo [build] FAILED: vocabulary lock
  exit /b 1
)

echo [build] fetching dependencies
call "%ROOT%download-dependencies.bat" /s
if errorlevel 1 (
  echo [build] FAILED: dependencies could not be prepared
  exit /b 1
)

set "DOTNET=dotnet"
where dotnet >nul 2>nul || set "DOTNET=%ROOT%.deps\dotnet\dotnet.exe"

echo [build] publishing version %VERSION%
"%DOTNET%" publish "%ROOT%src\MonitoringTools\MonitoringTools.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -p:Version=%VERSION% -o "%ROOT%out"
if errorlevel 1 (
  echo [build] FAILED: dotnet publish exited with %ERRORLEVEL%
  exit /b %ERRORLEVEL%
)

rem The bundle adds FFmpeg, FFprobe and MinGit (roughly 80 to 100 MB compressed). An exe far below
rem that floor means the dependencies were not embedded, which must fail the build.
for %%F in ("%ROOT%out\MonitoringTools.exe") do set "SIZE=%%~zF"
if %SIZE% LSS 70000000 (
  echo [build] FAILED: out\MonitoringTools.exe is %SIZE% bytes, below the 70000000-byte bundle floor
  exit /b 2
)
echo [build] built out\MonitoringTools.exe (%SIZE% bytes, dependencies bundled)
exit /b 0
