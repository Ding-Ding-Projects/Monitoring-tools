@echo off
rem Invoked by build-installer.bat VERSION --companions after the main application is built.
setlocal EnableExtensions
set "COMPANION_VERSION=%~1"
if "%COMPANION_VERSION%"=="" set "COMPANION_VERSION=1.0.0"
where npm >nul 2>nul
if errorlevel 1 (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\ensure-node.ps1"
  if errorlevel 1 exit /b 1
  for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "[Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')"`) do set "PATH=%%P"
  where npm >nul 2>nul
  if errorlevel 1 exit /b 1
)
for %%A in (build-tool site-companion) do (
  pushd "%~dp0apps\%%A"
  call npm ci --no-audit --no-fund
  if errorlevel 1 (
    popd
    exit /b 1
  )
  if "%%A"=="site-companion" (
    call npm run prepare-content
    if errorlevel 1 (
      popd
      exit /b 1
    )
  )
  call npm exec electron-builder -- --win portable --x64 --publish never -c.extraMetadata.version=%COMPANION_VERSION%
  if errorlevel 1 (
    popd
    exit /b 1
  )
  popd
)
echo [companions] Both portable companions built for %COMPANION_VERSION%.
exit /b 0
