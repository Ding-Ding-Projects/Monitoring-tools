# Missing Node.js is acquired through Chocolatey with native user-owned elevation consent.
$ErrorActionPreference = 'Stop'
if (Get-Command npm -ErrorAction SilentlyContinue) { exit 0 }
$install = @'
$ErrorActionPreference = 'Stop'
try {
    if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
        Set-ExecutionPolicy Bypass -Scope Process -Force
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-Expression ((New-Object Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
    }
    & choco install nodejs-lts --yes --no-progress
    if ($LASTEXITCODE -notin @(0, 1641, 3010)) { exit $LASTEXITCODE }
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
    & node --version
    if ($LASTEXITCODE -ne 0) { exit 1 }
    & npm --version
    exit $LASTEXITCODE
} catch { Write-Error $_; exit 1 }
'@
try {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($install))
    $process = Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoProfile','-EncodedCommand',$encoded -Wait -PassThru
    exit $process.ExitCode
} catch { Write-Error "Node.js setup did not complete: $($_.Exception.Message)"; exit 1 }
