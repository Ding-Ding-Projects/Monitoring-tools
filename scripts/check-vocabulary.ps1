<#
  Maintainer vocabulary lock. Builds and pushes run this first.

  It hashes the maintainer's private conversation dictionary at run time and compares the
  digest with a lock kept beside the maintainer's own machine state, outside this repository:
  ~/.agents/vocabulary-locks/monitoring-tools.lock. Neither the terms nor the digest are ever
  committed here.

  - No private dictionary on this machine (outside contributors, CI): skipped, reason printed, exit 0.
  - Dictionary present and the lock matches: pass.
  - Dictionary present and the lock is missing or stale: fail, unless an interactive terminal
    renews it after the typed acknowledgement. CI, SILENT=1 or a non-interactive host never renews.

  This proves the dictionary is present and current. It cannot prove anyone used it.
#>
[CmdletBinding()]
param([switch]$Renew)
$ErrorActionPreference = 'Stop'

# Where the maintainer's instructions live is machine state, never repository content:
# the UH_HOME environment variable, or the path stored in ~/.agents/uh-home.txt.
$home_ = [Environment]::GetFolderPath('UserProfile')
$candidates = @()
if ($env:UH_HOME) { $candidates += $env:UH_HOME }
$pointer = Join-Path $home_ '.agents/uh-home.txt'
if (Test-Path $pointer) { $candidates += (Get-Content $pointer -Raw).Trim() }

$dictionary = $null
foreach ($c in $candidates) {
    $p = Join-Path $c 'memory/vocabulary-dictionary.json'
    if (Test-Path $p) { $dictionary = $p; break }
}
if (-not $dictionary) {
    Write-Host "[vocabulary] skipped: no maintainer dictionary configured on this machine (UH_HOME or ~/.agents/uh-home.txt)"
    exit 0
}

$digest = (Get-FileHash -Algorithm SHA256 $dictionary).Hash.ToLowerInvariant()
$lockDir = Join-Path $home_ '.agents/vocabulary-locks'
$lock = Join-Path $lockDir 'monitoring-tools.lock'
if ((Test-Path $lock) -and ((Get-Content $lock -Raw).Trim() -eq $digest)) {
    Write-Host "[vocabulary] lock current"
    exit 0
}

$interactive = [Environment]::UserInteractive -and -not $env:CI -and $env:SILENT -ne '1' -and -not [Console]::IsInputRedirected
if (-not $interactive) {
    Write-Host "[vocabulary] FAILED: the dictionary changed or was never locked for this repository. Re-read $dictionary, then run scripts\check-vocabulary.ps1 in a terminal to renew."
    exit 1
}
Write-Host "[vocabulary] The conversation dictionary at $dictionary is new or changed."
Write-Host "Read it now. Type: I have read the dictionary"
$answer = Read-Host
if ($answer.Trim() -ne 'I have read the dictionary') {
    Write-Host "[vocabulary] FAILED: acknowledgement not given; lock unchanged"
    exit 1
}
New-Item -ItemType Directory -Force -Path $lockDir | Out-Null
Set-Content -Path $lock -Value $digest -NoNewline
Write-Host "[vocabulary] lock renewed"
exit 0
