# Built-in Monitoring Tools extension: reads one JSON line on standard input, writes one JSON line.
# Runs with Windows PowerShell 5.1 or PowerShell 7. Keep it plain ASCII (Windows PowerShell reads it as ANSI). Edit freely: once changed, app updates leave it alone.
$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
function Reply($o) { [Console]::Out.WriteLine(($o | ConvertTo-Json -Compress -Depth 5)) }
function V($label, $text) { [ordered]@{ label = [string]$label; text = [string]$text } }
try {
    $req = [Console]::In.ReadLine() | ConvertFrom-Json
    $t = ([string]$req.text).Trim()
    $hex = ($t -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    $pairs = for ($i = 0; $i -lt 12; $i += 2) { $hex.Substring($i, 2) }
    $first = [Convert]::ToInt32($pairs[0], 16)
    $cisco = ($hex.Substring(0, 4) + '.' + $hex.Substring(4, 4) + '.' + $hex.Substring(8, 4)).ToLowerInvariant()
    Reply @{ values = @(
        (V 'Colons' ($pairs -join ':')), (V 'Dashes (Windows)' ($pairs -join '-')), (V 'Dots (Cisco)' $cisco), (V 'Plain' $hex),
        (V 'Kind' ($(if ($first -band 1) { 'Multicast' } else { 'Unicast' }) + ', ' + $(if ($first -band 2) { 'locally assigned' } else { 'set by the maker' }))),
        (V 'Maker prefix' ($pairs[0..2] -join ':'))) }
} catch { Reply @{ error = $_.Exception.Message } }
