# Built-in Monitoring Tools extension: reads one JSON line on standard input, writes one JSON line.
# Runs with Windows PowerShell 5.1 or PowerShell 7. Edit freely: once changed, app updates leave it alone.
$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
function Reply($o) { [Console]::Out.WriteLine(($o | ConvertTo-Json -Compress -Depth 5)) }
function V($label, $text) { [ordered]@{ label = [string]$label; text = [string]$text } }
try {
    $req = [Console]::In.ReadLine() | ConvertFrom-Json
    $t = ([string]$req.text).Trim()
    $ci = [Globalization.CultureInfo]::InvariantCulture
    if ($t -match '^(-?[\d.]+)\s*,\s*(-?[\d.]+)$') { $lat = [double]::Parse($matches[1], $ci); $lon = [double]::Parse($matches[2], $ci) }
    else {
        $m = [regex]::Matches($t, '(\d+)°\s*(\d+)[''′]\s*([\d.]+)["″]?\s*([NSEW])')
        if ($m.Count -ne 2) { throw 'These coordinates could not be read.' }
        $vals = foreach ($x in $m) { $d = [double]$x.Groups[1].Value + [double]$x.Groups[2].Value / 60 + [double]::Parse($x.Groups[3].Value, $ci) / 3600; if ('SW'.Contains($x.Groups[4].Value)) { -$d } else { $d } }
        $lat, $lon = $vals
    }
    if ([math]::Abs($lat) -gt 90 -or [math]::Abs($lon) -gt 180) { throw 'Latitude must be within 90 and longitude within 180.' }
    function Dms([double]$v, [string]$pos, [string]$neg) { $a = [math]::Abs($v); $d = [math]::Floor($a); $mm = [math]::Floor(($a - $d) * 60); $s = (($a - $d) * 60 - $mm) * 60; '{0}°{1}′{2}″{3}' -f $d, $mm, $s.ToString('0.0', $ci), $(if ($v -ge 0) { $pos } else { $neg }) }
    $dec = $lat.ToString('0.######', $ci) + ', ' + $lon.ToString('0.######', $ci)
    $url = 'https://www.openstreetmap.org/?mlat=' + $lat.ToString('0.######', $ci) + '&mlon=' + $lon.ToString('0.######', $ci) + '#map=16/' + $lat.ToString('0.######', $ci) + '/' + $lon.ToString('0.######', $ci)
    if ($req.action -eq 'map') { Reply @{ open = $url } }
    else { Reply @{ values = @((V 'Decimal' $dec), (V 'Degrees, minutes, seconds' ((Dms $lat 'N' 'S') + ' ' + (Dms $lon 'E' 'W'))), (V 'Map link' $url)) } }
} catch { Reply @{ error = $_.Exception.Message } }
