# Clipboard actions: design handoff

Design canvas for the clipboard toasts and two new tool windows of Monitoring Tools (WPF, Material 3). The artboards
(`*.dc.html`) are design references, not shipped code; the app is not changed by this pass. Implement them natively
with the existing theme roles (`B.*` brushes in `Theme.xaml`), Roboto, the toast anatomy of `ToastWindow.xaml` and the
action model of `ActionProvider.cs` (`ToastAction`: label, icon, primary, chip, tip, hands-off).

Boards are plain HTML (no framework script), so they open standalone. Each has a light and dark switch (top right) and
opens in a given theme with `?theme=light` or `?theme=dark`. Frames in `frames/` are real renders of the boards in
headless Edge at 1440 px wide, scale 1, with the repository's shipped Roboto and Roboto Mono (`fonts/`, copied from
`site/src/fonts/`). Icons are Material Symbols (rounded, filled glyphs), inlined from `@material-design-icons/svg`.

## Files

| Board | Artboard | Frames |
| --- | --- | --- |
| 1 · Text toast: edit the copied text | `Text.dc.html` | `frames/text-dark.png`, `frames/text-light.png` |
| 2 · Image toast: codes found | `Image.dc.html` | `frames/image-dark.png`, `frames/image-light.png` |
| 3 · Folder and GitHub link toasts | `Folder.dc.html` | `frames/folder-dark.png`, `frames/folder-light.png` |
| 4 · Install command toast and automatic mode | `Install.dc.html` | `frames/install-dark.png`, `frames/install-light.png` |
| 5 · Archive dialog | `Archive.dc.html` | `frames/archive-dark.png`, `frames/archive-light.png` |
| 6 · Virtual hard disk dialog | `Disk.dc.html` | `frames/disk-dark.png`, `frames/disk-light.png` |

## Shared toast anatomy (boards 1 to 4)

400 px card (436 px window, 18/12/18/22 margin), radius 24, `B.Raised` fill, 1 px `B.Line` border, 3 px timer bar.
Header: 28 px icon chip (radius 8, 16 px glyph), 16 px title, mono 12 px meta (single line, ellipsis), "now" or the
held mark, 26 px close button visible on hover only. Body 10 px under the header, optional 12 px hint, then up to four
buttons (40 px pill; primary filled, others tonal, 18 px icon), one chip row (32 px, radius 8), a 12 px tip line, and
"More" for the rest. The timer is held while the pointer or keyboard focus is inside. The toast opens with
`ShowActivated=False` as today. Where this canvas drops the empty tip line of a toast, the real window keeps its 16 px
minimum height; nothing else about the anatomy changes.

Deliberate token deviation for contrast: header meta, hints, tips and helper text use `B.TextSoft` rather than
`B.TextMute`, because `TextMute` measured about 3.5:1 on the toast fill in the light theme. The implementer may keep
`TextMute` if the product decision is to stay with the current look; this is flagged, not settled.

## Board 1: text toast

States: idle, editing, reverted, long text idle, long text editing, rules card.

- Body: read-only look, `B.Sunken` fill, radius 12, 10/12 padding, 14/20 text, up to 8 lines (180 px with padding), then
  an inner vertical scroll with a 4 px thumb. Header meta: "128 chars · 4 lines".
- Idle copy: hint "Click the text to edit it. The clipboard follows your edits." Primary button "Edit text" (edit icon).
- Editing: the body becomes a focused text box (2 px primary inset ring, selection tint), header shows "held" and meta
  "edited", under it a mono count ("127 chars · 4 lines", whole text) and a pill "Copied" (check) that dims for 400 ms
  while typing and returns when the clipboard write is done. Hint "The clipboard already holds your edit." Buttons:
  "Keep" (primary, check, key hint Ctrl+Enter) and "Revert" (undo, key hint Esc).
- Reverted: hint "Reverted. The clipboard holds the original text again."; timer resumes.
- Focus rule: the toast never takes focus until the person clicks into the text. The first click in the text makes the
  window activatable and puts the caret there; before that, no focus, no caret. Tab order after that: text, Keep,
  Revert, Close. Esc in the text reverts; Esc elsewhere dismisses. Ctrl+Enter keeps and closes.
- Clipboard rules: write 400 ms after the last keystroke; the watcher must ignore the app's own write so no second toast
  opens; the history entry is updated in place and marked edited, not duplicated; Esc restores the original to clipboard
  and history.
- Accessibility: text box name "Copied text, editable", multi-line; "Copied" announced politely; icon-only controls
  carry names (Dismiss, Copy code n).
- Build: editable body control in the `ToastManager.ShowClip` text branch, debounce timer, own-write suppression in
  `ClipboardWatcher`, focus handling in `ToastWindow` (activatable on first text click), strings with Cantonese.

## Board 2: image toast with codes

States: QR with link, EAN-13, Code 128, several codes, authenticator QR, actions-by-kind card.

- Body: 96 px white thumbnail tile (the clipboard picture or the code crop; always dark on white so it scans) beside a
  kind pill, optional property pills ("Link", "Check digit valid"), the decoded text in mono (two lines, ellipsis), and
  a 12 px detail line. Header meta is the picture size. The existing ":png :jpg :webp" chips stay, labelled "Save
  picture as", on the single-code link toast only in this canvas; every image toast keeps them in the product.
- QR with a link: primary "Open link" (open_in_new), "Copy QR text". Tip "Opens in your default browser, only when you
  press it." Show the host under the URL. Never open anything automatically.
- Barcodes (EAN-13, UPC, Code 128 and others): primary "Copy number" or "Copy text", plus "Search the web". EAN and UPC
  show whether the check digit is valid.
- Several codes: picture strip with numbered marks (primary discs) matching the rows; rows 40 px (number pill, kind
  pill, text, 32 px copy icon button with name "Copy code n"); primary "Copy all n", secondary "Save as .txt".
- Authenticator QR (otpauth): amber shield icon, pill "Secret hidden", issuer and account only, info note "The secret
  is never shown or copied. It is stored encrypted when you register." One action: "Register for codes". No copy or open
  action, because the decoded text contains the secret. The secret never reaches the UI, log or history.
- Build: code reading in the image branch of `ToastManager`/`ActionProvider` (the decode already feeds "Register for
  codes"), kind detection, check-digit validation, new actions, strings with Cantonese.

## Board 3: folder and GitHub link toasts, Tools card

- Git repository folder: icon folder, meta "GitHub\name" (last two segments), path in mono, pills "Git repository",
  branch, "clean" or "n changed". Primary "Open in Desktop Material", then "Show in folder", "Copy path".
- Plain folder: pill "Not a Git repository", hint "Runs git init in this folder. Nothing is committed and no files
  change." Primary "Start a Git repository" (fork_right); nothing runs before the click.
- GitHub link: primary "Clone to <leaf of the default folder>" (label ellipsised at 18 characters), "Clone to…" (folder
  picker), "Open in browser", "Copy link". Body shows the exact clone target and the link, wrapping at path separators.
- Cloning: bar, phase and bytes, Cancel (removes the partly cloned folder). Cloned: path, size, branch, primary "Open in
  Desktop Material", "Show in folder".
- Tools page card "Default clone folder" (Tools, Git): path field (focus ring), Browse, checks (exists, writable, free
  space), switch "Name a folder after the repository" (on), switch "Open in Desktop Material when it ends" (off), text
  button "Reset to Documents\GitHub". The check runs as you type; a failed check disables the clone button with its
  reason in the tip. The field shows a start-ellipsis form of long paths so the useful tail stays visible.
- Build: folder and GitHub branches in `ActionProvider`, clone runner with progress and cancel, the Tools card and a
  setting in `Settings`, strings with Cantonese. Desktop Material hand-off is by its documented command line.

## Board 4: install command toast and automatic mode

- Toast fields (key and value grid): Packages (mono pills), Source (name and "(default)"), Permission. For choco the
  permission row is amber with a shield and reads "Windows will ask for permission (consent prompt)". For winget it is
  neutral: "The package's installer decides. Windows may ask." Buttons: primary "Install n packages" or "Install", and
  "Copy command". Tip "Nothing runs until you press Install."
- Refused command: amber icon and title "Not offered to install", the command in mono with refused tokens in amber, one
  line per refused flag with its reason, primary "Copy command". Never a silent ignore.
- Installing: steps (command checked, waiting for the Windows consent prompt, install) with a bar and Cancel; the
  consent step is named so a waiting prompt is not read as a hang. The app never answers, hides or pre-approves the
  Windows consent prompt. Installed: versions as reported by the package manager, duration, "Open log", "Copy summary".
  Declined: error tone, "The Windows consent prompt was declined or timed out. Nothing was installed.", exit code
  (1223), "Try again", "Copy details".
- Settings card "Install copied commands automatically" (Settings, Clipboard): switch off by default, warning note,
  typed numbers with steppers: wait before starting (3 to 60 s, default 10) and most packages per command (1 to 10,
  default 5), dimmed until the switch is on. Rules written next to it: exactly one plain command (`choco install <name>
  [-y]` or `winget install --id <id> [-e]`), no pipes, `&&`, `;`, redirects or variables, names limited to letters,
  digits, dots, dashes and underscores, default sources only, consent prompt every time for choco, each run logged in
  Activity with the exact command. Refused flags listed as chips: --source / -s, --ignore-checksums,
  --ignore-package-exit-codes, --force / -f, --params / --package-parameters, --install-arguments, --override,
  --pre / --prerelease, --allow-downgrade, and any flag not on the allowed list. During the start delay a toast shows
  the command with Cancel.
- Build: a strict parser (allow list, not a deny list; anything unknown refuses), source detection, the existing
  elevation route for choco (native consent prompt answered only by the person), install runner with logging, strings
  with Cantonese. Treat any clipboard text as untrusted data.

## Board 5: archive dialog

Separate window, 840 px wide, opened from the file toast (not the control centre). Focus starts in Name. Esc cancels,
Ctrl+Enter creates. Header: icon, "Create archive", "n items from <folder> · size", close. Selected items as chips with
sizes (collapses to "+n more").

- Left column, Archive: Name (suffix follows the format), Save in (default: folder of the first item, Browse), Format
  segmented Zip, 7z, tar.gz, Compression level slider with six named stops (Store, Fastest, Fast, Normal, Maximum,
  Ultra; default Normal) with tick marks, Method (zip: Deflate, Deflate64, BZip2, LZMA, Store; 7z: LZMA2, LZMA, PPMd,
  BZip2, Deflate), 7z only: Dictionary size, Solid block, CPU threads (typed number 1 to 16, "auto").
- Right column, Protection: Password with show toggle, strength bar, hint "AES-256", switch "Encrypt file names" (7z
  only, disabled with a reason on Zip). Split: switch "Split into volumes", typed Volume size and unit (MB, GB), preset
  chips (700 MB, FAT32 4 GB, DVD 4.4 GB). Contents: switch "Include the parent folder", Exclude patterns as chips
  with a Glob or Regex mode and the shared regex Builder button (the app's full builder, not a small tester).
- After: switch "Test the archive after creating" (default on); switch "Delete the originals" (default off): after the
  test passes, the existing two-key confirmation window opens (slider plus button), originals go to the Recycle Bin and
  the archive stays. "Delete originals" stays disabled until the slider is fully moved.
- Summary strip above the footer: items, size, estimated result (labelled an estimate), settings, volumes, free space
  needed, time. Footer: Cancel, primary "Create archive".
- Format rules: Zip hides dictionary, solid block and threads and disables Encrypt file names; tar.gz hides password,
  volumes and the 7z rows. Progress view (456 px): bar, bytes, speed, current file, steps Scan, Compress, Test, Finish,
  Cancel (removes the partial file). Done view: sizes, "No errors" pill, originals kept or deleted, location, "Copy
  path", primary "Show in folder".
- Open question for the implementer: which 7-Zip build the app bundles and whether tar.gz is in scope; the design
  shows all three.

## Board 6: virtual hard disk dialog

Separate window, 840 px wide. A note first: administrator rights are needed; Windows shows its consent prompt when
Create is pressed and the app never answers it. Create carries the Windows shield glyph.

- Disk file: Save in (shows free space), Name (suffix .vhdx or .vhd), Format VHDX or VHD (limits explained: 64 TB and
  2,040 GB), Size typed number plus unit (GB, TB, MB), Type segmented Dynamically expanding or Fixed size with a
  one-line consequence ("Fixed takes the full 64 GB on D: right away"); Contents: Copy, Move or Leave empty for the n
  selected items (Move deletes originals only after every file copied and matched in size, after the two-key
  confirmation).
- Volume: File system NTFS, exFAT, FAT32 as a segmented control; FAT32 is disabled above 32 GB with the explanation
  (Windows formats FAT32 only up to 32 GB here, files up to 4 GB); Volume label (32 characters NTFS, 11 for FAT32 and
  exFAT); Mount as Drive letter (next free) or Folder (empty NTFS folder); After: Keep mounted or Detach.
- Summary strip: format, type and size, file system and label, mount point, copy plan, starting size, partition style.
- Progress (456 px): named steps (permission given, created and attached, formatted, copying, finish), bar and bytes
  for the copy, Cancel (detaches and deletes what was made). Done: file, size on disk, copied, "Mounted" pill; primary
  "Open V:", "Show file", quiet "Detach". Error (permission declined): nothing created, nothing attached, details
  expander with exit code 1223, "Try again". Every failure cleans up after itself.
- Build: elevated helper for create, attach, partition, format and mount (native consent prompt only), validation of
  every combination above before the prompt, strings with Cantonese. Do not claim success until the volume is
  mounted and the copy verified.

## Keyboard order (dialogs)

Start in the first field (Name), move through the left column then the right column, then the After switches, then the
footer (Cancel, primary). Segmented controls are one tab stop with arrow keys. Toasts: see board 1 for the text toast;
other toasts tab through their buttons, then chips, then More.

## Stageshow record

- Stageshow skill: commit 299140f (installed at the session scratchpad `.claude/skills/stageshow`).
- Read: `SKILL.md`, `references/craft.md`, `references/framing.md`, `references/rehearse.md`, `references/review.md`.
- Closest gold used for structure and restraint: `controls` and `form-error` (real controls, errors on the thing that
  caused them, quiet neutral family), with `share` for anchored popover anatomy. Kept: real words only where the story
  needs them, one icon family and size per surface, one neutral family, status colours that mean status, no sparkle or
  invented logo, text insets of at least 12 px, no near-tangent edges, one accent.
- Fresh-eye review: run once by an independent reviewer who saw only the frames, `review.md` and `verify.md`.

### Review findings and what was done

Fixed: truncated header meta on install A and B (meta dropped, source row says it); install C command wrapped in the
middle of a URL (explicit break); folder C and install C crowded header (meta and timestamp removed there); folder F
path ellipsis hid the useful part (start-ellipsis form); slider labels not tied to stops (labels centred on stops, tick
dots added); Builder control and exclude placeholder cramped (tonal button, 8 px gap); unlabeled convert chips ("Save
picture as"); header meta, hint and tip contrast in both themes (token deviation above); disabled "Delete originals"
looked like a stain (neutral disabled treatment); loud error banner in dark (toned toward the surface); disk dialog
void under the left window (windows now stretch to the column height); board chrome theme button hidden in frames.

Not changed, with reason: ragged card bottoms within a row. Toasts are auto-height in the product, so equal heights
would invent empty space inside them. Void under the middle column of install card G and under folder D and E: a spec
board follows content, and filling it would add content that is not part of the design. Light-theme tonal buttons
close in value to the toast fill: these are the app's real roles; a hairline would be a new component style, so this
is listed for the implementer to judge in the built app. Dark info banner close to the card surface: same reason.
Detach text button alignment: the 12 px text-button inset is the Material standard. The re-render after the fixes was
inspected by the builder (archive, folder, disk, image frames); a second independent review round was not run.

### Stageshow rules not applicable (marketing demos, not real app screens)

Hero, satellite, crop, bleed and gutter layouts; `compact` phone-column design; stage, kit parts and `Act` scripts;
choreography, motion, loops, cursor and drag rules; `film.mjs --gate`, jump and class-clash scans (no animation, no
stage); phone and device frames; bento and page-of-demos rules; poster and reduced-motion poster (the boards have no
loop; real motion is specified by the app's own motion rules, skipped under reduced motion). Rehearsal was done as
still renders in both themes, with an overflow and clipped-text probe in the page (clean except a 3 px tick overhang
on the archive slider, inside the slider's own box).

### Not checked

125, 150 and 200 percent display scales; narrow widths (boards are 1440 px references); real WPF rendering; screen
reader output; the Cantonese strings (each new string needs one in the app); the second review round.
