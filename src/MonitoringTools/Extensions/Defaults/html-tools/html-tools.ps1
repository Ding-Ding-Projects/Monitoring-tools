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
    Add-Type -AssemblyName System.Web
    switch ($req.action) {
        'decode' { $r = [System.Web.HttpUtility]::HtmlDecode($t) }
        'escape' { $r = [System.Web.HttpUtility]::HtmlEncode($t) }
        'text'   { $r = [System.Web.HttpUtility]::HtmlDecode(($t -replace '(?is)<(script|style)\b.*?</\1>', '' -replace '(?i)<br\s*/?>|</p>|</div>|</li>', "`n" -replace '<[^>]+>', '')) -replace "[ \t]+`n", "`n" -replace "`n{3,}", "`n`n" }
        default  { throw "Unknown action $($req.action)" }
    }
    Reply @{ copy = $r.Trim(); values = @((V 'Result' $r.Trim()), (V 'Characters' $r.Trim().Length)) }
} catch { Reply @{ error = $_.Exception.Message } }
