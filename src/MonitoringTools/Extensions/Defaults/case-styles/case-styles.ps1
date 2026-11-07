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
    $spaced = $t -creplace '([a-z0-9])([A-Z])', '$1 $2' -replace '[_\-]+', ' '
    $words = @($spaced.ToLowerInvariant().Split(' ', [StringSplitOptions]::RemoveEmptyEntries))
    $cap = $words | ForEach-Object { $_.Substring(0,1).ToUpperInvariant() + $_.Substring(1) }
    $camel = $words[0] + (($cap | Select-Object -Skip 1) -join '')
    $values = @(
        (V 'camelCase' $camel), (V 'PascalCase' ($cap -join '')), (V 'snake_case' ($words -join '_')),
        (V 'SCREAMING_SNAKE' (($words -join '_').ToUpperInvariant())), (V 'kebab-case' ($words -join '-')), (V 'Words' ($cap -join ' '))
    )
    if ($req.action -eq 'snake') { Reply @{ copy = ($words -join '_'); toast = 'Copied ' + ($words -join '_') } }
    else { Reply @{ values = $values; toast = 'Tap a style to copy it.' } }
} catch { Reply @{ error = $_.Exception.Message } }
