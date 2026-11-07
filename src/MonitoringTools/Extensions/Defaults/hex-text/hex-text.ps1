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
    $hex = $t -replace '[^0-9A-Fa-f]', ''
    if ($hex.Length % 2 -ne 0) { throw 'An odd number of hex digits cannot be bytes.' }
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16) }
    $text = (New-Object System.Text.UTF8Encoding $false, $true).GetString($bytes)
    if ($text -match '[\x00-\x08\x0E-\x1F]') { throw 'These bytes are not readable text.' }
    Reply @{ copy = $text; toast = 'Decoded: ' + $text; values = @((V 'Text' $text), (V 'Bytes' $bytes.Length)) }
} catch { Reply @{ error = $_.Exception.Message } }
