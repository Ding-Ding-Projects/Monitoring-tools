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
    $ip, $bits = $t.Split('/'); $bits = [int]$bits
    $o = $ip.Split('.') | ForEach-Object { [int]$_ }
    if ($o | Where-Object { $_ -gt 255 }) { throw 'Each part of the address must be 0 to 255.' }
    [uint64]$addr = [uint64]$o[0] * 16777216 + [uint64]$o[1] * 65536 + [uint64]$o[2] * 256 + [uint64]$o[3]
    [uint64]$size = [uint64][math]::Pow(2, 32 - $bits)
    [uint64]$mask = [uint64]4294967296 - $size
    [uint64]$net = [uint64]([math]::Floor($addr / $size)) * $size
    [uint64]$bc = $net + $size - 1
    function Ip([uint64]$n) { '{0}.{1}.{2}.{3}' -f ([math]::Floor($n / 16777216) % 256), ([math]::Floor($n / 65536) % 256), ([math]::Floor($n / 256) % 256), ($n % 256) }
    $total = $size
    $hosts = if ($bits -ge 31) { $total } else { $total - 2 }
    $word = if ($hosts -eq 1) { "address" } else { "addresses" }
    $first = if ($bits -ge 31) { $net } else { $net + 1 }
    $last = if ($bits -ge 31) { $bc } else { $bc - 1 }
    Reply @{ toast = "$(Ip $net)/$bits holds $hosts usable $word."; values = @(
        (V 'Network' (Ip $net)), (V 'Mask' (Ip $mask)), (V 'Broadcast' (Ip $bc)),
        (V 'First host' (Ip $first)), (V 'Last host' (Ip $last)), (V 'Usable hosts' $hosts)) }
} catch { Reply @{ error = $_.Exception.Message } }
