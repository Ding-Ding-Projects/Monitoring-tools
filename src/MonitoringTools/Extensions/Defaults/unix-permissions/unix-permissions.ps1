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
    $sym = ''; $oct = ''
    if ($req.kind -eq 'symbolic') {
        $s = if ($t.Length -eq 10) { $t.Substring(1) } else { $t }
        $digits = for ($i = 0; $i -lt 9; $i += 3) { $v = 0; if ($s[$i] -eq 'r') { $v += 4 }; if ($s[$i + 1] -eq 'w') { $v += 2 }; if ('xst'.Contains([string]$s[$i + 2])) { $v += 1 }; $v }
        $oct = -join $digits; $sym = $s
    } else {
        $m = [regex]::Match($t, '[0-7]{3,4}'); $oct = $m.Value.Substring($m.Value.Length - 3)
        $sym = -join ($oct.ToCharArray() | ForEach-Object { $v = [int][string]$_; ($(if ($v -band 4) { 'r' } else { '-' }) + $(if ($v -band 2) { 'w' } else { '-' }) + $(if ($v -band 1) { 'x' } else { '-' })) })
    }
    function Who([string]$part) { $w = @(); if ($part[0] -eq 'r') { $w += 'read' }; if ($part[1] -eq 'w') { $w += 'write' }; if ($part[2] -ne '-') { $w += 'run' }; if ($w.Count -eq 0) { 'nothing' } else { $w -join ', ' } }
    Reply @{ toast = "$sym is $oct"; values = @((V 'Octal' $oct), (V 'Symbolic' $sym), (V 'Owner' (Who $sym.Substring(0, 3))), (V 'Group' (Who $sym.Substring(3, 3))), (V 'Everyone else' (Who $sym.Substring(6, 3))), (V 'Command' "chmod $oct")) }
} catch { Reply @{ error = $_.Exception.Message } }
