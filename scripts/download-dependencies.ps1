<#
  Fetches every pinned dependency listed in deps/manifest.json into .deps/ (ignored by git),
  verifies each archive's SHA-256, and extracts the files the app bundles.

  - Idempotent: a warm run re-verifies what is present and skips the download.
  - Silent: -Silent (or SILENT=1) never prompts.
  - Honest: reports per dependency what was found, installed and how long it took;
    any failure names the dependency, pin, source and error, and exits non-zero.
  - An empty sha256 in the manifest is an unrecorded pin: the digest seen is printed
    and the script fails, so nothing unverified is ever bundled.
  - Also ensures a .NET 8 SDK (user-scoped under .deps/dotnet when none is installed).
#>
[CmdletBinding()]
param([switch]$Silent, [switch]$SkipSdk)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if ($env:SILENT -eq '1') { $Silent = $true }

$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'deps/manifest.json'
$depsDir = Join-Path $root '.deps'
$cacheDir = Join-Path $depsDir 'cache'
New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

function Say($m) { Write-Host "[dependencies] $m" }

function Get-AssetUrl($dep) {
    $api = "https://api.github.com/repos/$($dep.repository)/releases/tags/$($dep.tag)"
    $headers = @{ 'User-Agent' = 'monitoring-tools-dependencies' }
    if ($env:GITHUB_TOKEN) { $headers.Authorization = "Bearer $($env:GITHUB_TOKEN)" }
    $rel = Invoke-RestMethod -Uri $api -Headers $headers
    $asset = $rel.assets | Where-Object {
        if ($dep.asset) { $_.name -eq $dep.asset } else { $_.name -match $dep.assetPattern }
    } | Sort-Object name | Select-Object -First 1
    if (-not $asset) {
        $names = ($rel.assets | Where-Object { $_.name -match 'win64' } | ForEach-Object { $_.name }) -join ', '
        throw "No asset matching '$($dep.assetPattern)' in $($dep.repository) $($dep.tag). Windows assets there: $names"
    }
    return $asset
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$failed = $false

foreach ($dep in $manifest.dependencies) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $target = Join-Path $depsDir $dep.target
    $stamp = Join-Path $target '.pinned'
    try {
        if ((Test-Path $stamp) -and $dep.sha256 -and ((Get-Content $stamp -Raw).Trim() -eq "$($dep.tag) $($dep.sha256)")) {
            Say "$($dep.name): present at $target (pinned $($dep.tag)), verified earlier; skipped"
            continue
        }

        $asset = Get-AssetUrl $dep
        $archive = Join-Path $cacheDir $asset.name
        if (-not (Test-Path $archive)) {
            Say "$($dep.name): downloading $($asset.name) ($([math]::Round($asset.size / 1MB, 1)) MB) from $($dep.repository) $($dep.tag)"
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile "$archive.part" -Headers @{ 'User-Agent' = 'monitoring-tools-dependencies' }
            Move-Item -Force "$archive.part" $archive
        }

        $digest = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
        if (-not $dep.sha256) {
            Write-Host "PIN $($dep.name) asset=$($asset.name) sha256=$digest"
            throw "No sha256 is recorded for $($dep.name) in deps/manifest.json. Record the value printed above, then run again."
        }
        if ($digest -ne $dep.sha256.ToLowerInvariant()) {
            Remove-Item -Force $archive
            throw "SHA-256 mismatch for $($asset.name): expected $($dep.sha256), got $digest. The download was removed."
        }

        if (Test-Path $target) { Remove-Item -Recurse -Force $target }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        if ($dep.copyAs) {
            # A single file (an exe) that is used as it is: no archive to open.
            Copy-Item -Force $archive (Join-Path $target $dep.copyAs)
            Set-Content -Path $stamp -Value "$($dep.tag) $($dep.sha256)" -NoNewline
            Say "$($dep.name): installed $($dep.tag) to $target in $([math]::Round($sw.Elapsed.TotalSeconds, 1)) s"
            continue
        }
        $tmp = Join-Path $cacheDir "$($dep.name)-extract"
        if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
        if ($dep.extractWith) {
            # A .7z archive or a 7-Zip installer exe (itself a 7z archive): opened with the pinned 7zr.exe that an earlier entry installed (build time only).
            $opener = Join-Path (Join-Path $depsDir $dep.extractWith) "$($dep.extractWith).exe"
            if (-not (Test-Path $opener)) { throw "$($dep.extractWith).exe is needed to open $($asset.name) but is not installed. It must be listed before $($dep.name) in deps/manifest.json." }
            & $opener x "-o$tmp" $archive -y | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "$($dep.extractWith) could not open $($asset.name) (exit $LASTEXITCODE)" }
        } else {
            Expand-Archive -Path $archive -DestinationPath $tmp -Force
        }

        $extract = $dep.extract.PSObject.Properties
        if ($extract.Name -contains '*') {
            Copy-Item -Recurse -Force (Join-Path $tmp '*') $target
        } else {
            $top = if ($dep.extractWith) { Get-Item $tmp } else { Get-ChildItem $tmp -Directory | Select-Object -First 1 }
            foreach ($p in $extract) {
                if ($p.Name.EndsWith('/*')) {
                    $dir = Join-Path $top.FullName $p.Name.TrimEnd('*').TrimEnd('/')
                    if (-not (Test-Path $dir)) { throw "Expected folder $($p.Name) inside $($asset.name)" }
                    Copy-Item -Force (Join-Path $dir '*') $target
                    continue
                }
                $src = Join-Path $top.FullName $p.Name
                if (-not (Test-Path $src)) { throw "Expected $($p.Name) inside $($asset.name)" }
                Copy-Item -Force $src (Join-Path $target $p.Value)
            }
        }
        Remove-Item -Recurse -Force $tmp
        Set-Content -Path $stamp -Value "$($dep.tag) $($dep.sha256)" -NoNewline
        Say "$($dep.name): installed $($dep.tag) to $target in $([math]::Round($sw.Elapsed.TotalSeconds, 1)) s"
    }
    catch {
        Write-Host "[dependencies] FAILED $($dep.name) (pin $($dep.tag), source $($dep.repository)): $($_.Exception.Message)"
        $failed = $true
    }
}

if (-not $SkipSdk) {
    $hasSdk = $false
    try { $hasSdk = (& dotnet --list-sdks 2>$null) -match '^8\.' } catch { }
    if ($hasSdk) { Say ".NET 8 SDK: present on this machine" }
    else {
        $dotnetDir = Join-Path $depsDir 'dotnet'
        if (Test-Path (Join-Path $dotnetDir 'dotnet.exe')) { Say ".NET 8 SDK: present at $dotnetDir" }
        else {
            try {
                Say ".NET 8 SDK: installing user-scoped to $dotnetDir with Microsoft's dotnet-install script"
                $installer = Join-Path $cacheDir 'dotnet-install.ps1'
                Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
                & $installer -Channel 8.0 -InstallDir $dotnetDir -NoPath
                Say ".NET 8 SDK: installed"
            }
            catch { Write-Host "[dependencies] FAILED .NET 8 SDK (channel 8.0, source dot.net): $($_.Exception.Message)"; $failed = $true }
        }
    }
}

if ($failed) { exit 1 }
Say "all dependencies ready"
exit 0
