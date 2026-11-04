# Security

Please report security problems privately through this repository's GitHub "Report a vulnerability"
(security advisory) form rather than a public issue. Include the version, what you did and what you
saw, and do not include real passwords, tokens or personal files.

Notes on how the app handles sensitive data:

- Settings, logs and scripts stay in `%LOCALAPPDATA%\MonitoringTools`; nothing is sent anywhere by
  default except the lookups named in the README (public IP, update check).
- Bundled FFmpeg, FFprobe and portable Git are pinned in `deps/manifest.json` with SHA-256 digests.
- Installers are not code-signed, so Windows will show an unknown-publisher warning; that is
  expected and is not a malware finding.
- Administrator rights are off by default and are requested through the normal Windows prompt.
