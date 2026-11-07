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
    $browser = 'Unknown browser'; $ver = ''
    foreach ($b in @(@('Edg/', 'Microsoft Edge'), @('OPR/', 'Opera'), @('Firefox/', 'Firefox'), @('Chrome/', 'Chrome'), @('Version/', 'Safari'))) {
        if ($t -match [regex]::Escape($b[0]) + '([\d.]+)') { $browser = $b[1]; $ver = $matches[1]; break }
    }
    $os = 'Unknown system'
    if ($t -match 'Windows NT 10\.0') { $os = 'Windows 10 or 11' } elseif ($t -match 'Windows NT ([\d.]+)') { $os = 'Windows NT ' + $matches[1] }
    elseif ($t -match 'iPhone OS ([\d_]+)') { $os = 'iOS ' + $matches[1].Replace('_', '.') } elseif ($t -match 'iPad') { $os = 'iPadOS' }
    elseif ($t -match 'Android ([\d.]+)') { $os = 'Android ' + $matches[1] } elseif ($t -match 'Mac OS X ([\d_]+)') { $os = 'macOS ' + $matches[1].Replace('_', '.') }
    elseif ($t -match 'CrOS') { $os = 'ChromeOS' } elseif ($t -match 'Linux') { $os = 'Linux' }
    $device = if ($t -match 'Mobile|iPhone|Android.+Mobile') { 'Phone' } elseif ($t -match 'iPad|Tablet') { 'Tablet' } else { 'Computer' }
    $bot = $t -match 'bot|crawler|spider'
    Reply @{ toast = "$browser $ver on $os"; values = @((V 'Browser' "$browser $ver".Trim()), (V 'System' $os), (V 'Device' $(if ($bot) { 'Automated crawler' } else { $device }))) }
} catch { Reply @{ error = $_.Exception.Message } }
