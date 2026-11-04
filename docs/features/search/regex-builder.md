# Regex builder

The builder opens directly under the search bar it belongs to (Builder button or Alt+B) and edits that bar's pattern. The pattern box at its top and the search bar stay in step in both directions.

![Regex builder, Build view](../../screenshots/search-builder-build.png)

## Always visible

- **Engine line:** the running .NET version, `System.Text.RegularExpressions`, backtracking or non-backtracking, and the 200 ms limit.
- **Validation:** "Valid pattern · N capturing group(s)" or the engine's own error message.
- **Backtracking warning:** nested quantifiers such as `(a+)+` and repeated overlapping alternations are flagged with a plain explanation and the fixes (an atomic group or the Non-backtracking option).
- **Options:** Multiline, Singleline, Ignore whitespace, Explicit capture, Right to left, Non-backtracking, Culture invariant, ECMAScript. Case is the bar's `Aa`.

## Views

| View | What it does |
| --- | --- |
| Build | Token palette (characters, Unicode properties, sets with subtraction, anchors, groups, backreferences, conditionals, quantifiers, lookaround, inline options) inserted at the caret; group tokens wrap the selection. "Escape and insert" turns literal text into a safe pattern. A token-by-token explanation, indented by group depth. |
| Test | Sample text (defaults to the list the bar searches), every match with its position, the capture groups of the selected match including groups that did not take part, and the measured time. Selecting a match selects it in the sample. |
| Debug | Replays one match attempt step by step (match, fail, backtrack, enter and leave group) with Prev, Next and Play. The current pattern token and text position are highlighted, the step count is shown, and the trace stops at 100,000 steps with a clear message. It uses a small built-in backtracking interpreter for a stated subset; it is checked against the real .NET engine on the same subject first, and withheld with an explanation if the two disagree or the pattern uses an unsupported construct (lookaround, atomic groups, conditionals, inline options, Unicode properties, class subtraction, some options). |
| Compare | Runs the pattern over the sample in the .NET modes that exist here (default backtracking, non-backtracking, ECMAScript, compiled): whether each is supported, match count, first difference from the default engine, and time. Other languages' engines are not available in this app. |
| Benchmark | Times a subject that should match and one that should not, then repeats each 1x, 2x, 4x, 8x and 16x with a verdict on growth. Runs in the background, stops at the 200 ms limit, and can be cancelled. |
| Replace | Replacement text with `$0`, `$1`, `${name}`, `$$`, `` $` ``, `$'` and `$_` chips, a live preview of the sample, and Split mode. Nothing changes until you copy the result. |
| Code | Ready-to-use C#, PowerShell and Python with the current options and a timeout. Python output converts named groups and lists options it cannot express. |
| Library | Save the current pattern with a name, use or remove saved ones, recent patterns for this bar, and JSON export and import (imports are size-capped and validated). |

## Limits, stated plainly

- The explanation comes from a lexer inside the app, not from the engine's internal parse tree; it is labelled as an explanation.
- The Debug trace comes from a separate small interpreter, not from the .NET engine, and covers a stated subset. It never shows a trace that disagrees with the engine. Benchmark and Compare timings are short real measurements and vary from run to run.
- Only the .NET flavour is available. Code for other languages is generated, not executed.

See the [parity ledger](regexbuddy-parity.md) for every missing family.
