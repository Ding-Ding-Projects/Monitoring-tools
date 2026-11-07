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
    $code = @{ '.-' = 'A'; '-...' = 'B'; '-.-.' = 'C'; '-..' = 'D'; '.' = 'E'; '..-.' = 'F'; '--.' = 'G'; '....' = 'H'; '..' = 'I'; '.---' = 'J'; '-.-' = 'K'; '.-..' = 'L'; '--' = 'M'
        '-.' = 'N'; '---' = 'O'; '.--.' = 'P'; '--.-' = 'Q'; '.-.' = 'R'; '...' = 'S'; '-' = 'T'; '..-' = 'U'; '...-' = 'V'; '.--' = 'W'; '-..-' = 'X'; '-.--' = 'Y'; '--..' = 'Z'
        '-----' = '0'; '.----' = '1'; '..---' = '2'; '...--' = '3'; '....-' = '4'; '.....' = '5'; '-....' = '6'; '--...' = '7'; '---..' = '8'; '----.' = '9' }
    $words = foreach ($w in $t.Split('/')) { -join ($w.Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { if ($code.ContainsKey($_)) { $code[$_] } else { '?' } }) }
    $decoded = ($words | Where-Object { $_ }) -join ' '
    Reply @{ copy = $decoded; toast = 'Decoded: ' + $decoded; values = @((V 'Decoded' $decoded), (V 'Letters' ($decoded -replace ' ', '').Length)) }
} catch { Reply @{ error = $_.Exception.Message } }
