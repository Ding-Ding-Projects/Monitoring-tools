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
    $map = @{ 'I' = 1; 'V' = 5; 'X' = 10; 'L' = 50; 'C' = 100; 'D' = 500; 'M' = 1000 }
    $n = 0
    for ($i = 0; $i -lt $t.Length; $i++) {
        $v = $map[[string]$t[$i]]
        if ($i + 1 -lt $t.Length -and $map[[string]$t[$i + 1]] -gt $v) { $n -= $v } else { $n += $v }
    }
    Reply @{ copy = [string]$n; toast = "$t is $n."; values = @((V 'Number' $n), (V 'Roman' $t)) }
} catch { Reply @{ error = $_.Exception.Message } }
