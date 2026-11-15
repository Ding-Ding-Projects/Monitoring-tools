# One-click packing

Quick packing starts immediately. The task handoff records the exact built checks and remaining
verification limits.

## Pack a copied selection

Copy a file, folder, several files or folders, or quoted paths. Choose **Pack to ZIP** or
**Pack to VHDX** on the file notice. The action starts immediately in a dedicated progress
dialog. It does not open the control centre or ask for another Pack click. The full archive
and virtual-disk tools remain available when you need custom options.

One output contains the selected items. It is placed in their parent folder; selections from
different folders use the first item's parent. The output name comes from the single item or
the parent folder. A number is added when the name is occupied. Existing output is never
overwritten. The dialog shows the actual destination.

## Follow progress and keep the originals

The operation counts the selection, writes the output and verifies it. Progress reports its
actual stage, current item, bytes and file counts. A percentage, speed or remaining time is
shown only when measurable. Closing the running dialog hides it without cancelling the job;
use the operation's activity action to reopen it.

ZIP uses normal Deflate compression without a password. VHDX uses a dynamic GPT/NTFS disk,
grows when necessary, verifies copied files with SHA-256 and finishes detached. Creating and
attaching that disk requires Windows' native elevation consent. Declining consent is an
incomplete operation, never success.

Originals remain unchanged. Duplicate and overlapping selections are counted once. Missing,
unreadable or changing inputs, insufficient space and verification failures are reported
explicitly. Cancellation removes only incomplete output owned by the operation. If a disk
cannot detach or an output cannot be removed, the retained path and recovery action are shown.

Links are skipped with an explicit partial-result warning. Inventory stops at 250,000 entries
or 128 nested directory levels rather than silently publishing a truncated selection.

## Finish

The result remains visible, with **Show in folder** and **Copy output path**. Explorer and the
main workspace do not open automatically. A partial result lists what did not complete.

## Related

- [Virtual disks](virtual-disks.md)
- [File tools](file-tools.md)
- [Docker actions](docker-actions.md)
