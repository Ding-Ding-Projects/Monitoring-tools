# Monitoring Tools extension: reads one JSON line, writes one JSON line.
# Change $tracker to your own tracker's address.
$tracker = 'https://tracker.example/browse/'
$request = [Console]::In.ReadLine() | ConvertFrom-Json
$url = $tracker + $request.text
switch ($request.action) {
  'open' { @{ open = $url } | ConvertTo-Json -Compress }
  'link' { @{ copy = $url; toast = "Copied $url" } | ConvertTo-Json -Compress }
  default { @{ error = "Unknown action $($request.action)" } | ConvertTo-Json -Compress }
}
