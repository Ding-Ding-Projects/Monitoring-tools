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
    $m = [regex]::Match($t, '^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)W)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$')
    $names = 'years', 'months', 'weeks', 'days', 'hours', 'minutes', 'seconds'
    $parts = @(); $nums = @()
    for ($i = 1; $i -le 7; $i++) { $g = $m.Groups[$i].Value; $nums += $(if ($g) { [double]::Parse($g, [Globalization.CultureInfo]::InvariantCulture) } else { 0 }); if ($g) { $parts += "$g " + $(if ($g -eq '1') { $names[$i - 1].TrimEnd('s') } else { $names[$i - 1] }) } }
    $values = @((V 'In words' ($parts -join ', ')))
    if ($nums[0] -eq 0 -and $nums[1] -eq 0) {
        $secs = $nums[2] * 604800 + $nums[3] * 86400 + $nums[4] * 3600 + $nums[5] * 60 + $nums[6]
        $ts = [TimeSpan]::FromSeconds($secs)
        $values += V 'Seconds' $secs
        $values += V 'Clock' ('{0}:{1:00}:{2:00}' -f [math]::Floor($ts.TotalHours), $ts.Minutes, $ts.Seconds)
    } else { $values += V 'Note' 'Years and months vary in length, so no exact seconds.' }
    Reply @{ values = $values; toast = ($parts -join ', ') }
} catch { Reply @{ error = $_.Exception.Message } }
