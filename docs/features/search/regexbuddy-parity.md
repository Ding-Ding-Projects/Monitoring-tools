# Regex builder parity ledger

Baseline: the official [RegexBuddy manual](https://www.regexbuddy.com/manual.html), family list reviewed 2026-11-04. Product baseline: Monitoring Tools 1.0.8, .NET 8 `System.Text.RegularExpressions`.

Status words: **done** (implemented and rendered from the built app), **partial** (some subfeatures work, the rest are listed), **missing**. Nothing is marked done from source reading alone. A complete per-topic ledger covering every manual topic is still to be written; this family ledger is the first step, not that ledger.

| Family | Status | What works | What is missing |
| --- | --- | --- | --- |
| Construction and syntax | partial | Raw editing; token palette for classes, Unicode properties, subtraction, anchors, named and numbered groups, atomic and non-capturing groups, backreferences, conditionals, quantifiers (greedy and lazy), lookaround, inline options, comments; selection-wrapping insertion; literal escaping; all .NET options | Balancing groups and POSIX classes in the palette; guided editing of an existing token; recursion and subroutines (not supported by .NET, which must be shown as unavailable, not hidden) |
| Replacement construction | partial | Replacement field with group, named group, `$$`, before, after and whole-input tokens; live preview | Separate replacement explanation; replacement conditionals and case conversion (not in .NET; shown as unavailable is still owed) |
| Analysis and exports | partial | Token-by-token explanation with group depth; validation with the engine's own message; backtracking-risk warning | Editable syntax tree, two-way selection between tree and pattern, adjustable detail, analysis export |
| Flavour comparison and conversion | missing | | Only .NET is available; no comparison or conversion |
| Testing and data | partial | Match and split; match list with positions; capture table including unmatched groups; empty-match handling; selection in the sample; measured time; sample from the searched list | Expected-outcome test cases, line and page scopes, file and clipboard input, hex view, encodings and line-ending controls |
| Debugger | missing | | No engine trace; .NET does not expose one, so this needs its own instrumented engine or stays unavailable |
| Benchmark and diagnostics | partial | Static risk detection for nested quantifiers and overlapping alternation; 200 ms timeout reported | Benchmark runs on matching and non-matching input, input-growth analysis |
| Code and escaping | partial | C#, PowerShell and Python snippets with options and timeout; Python named-group conversion and stated limits | Editable templates, replace and split snippets, more languages |
| Saved collections and parameters | partial | Named library saved to settings, use, remove, JSON import and export | Descriptions, test data per entry, parameters with defaults, collection search |
| File and folder GREP | missing | | No file search or replace; would need preview, deliberate execution and backups |
| Multiple regexes and history | partial | Per-bar recent patterns; independent state for every bar | Saved history across restarts, side-by-side experiments |
| Integration and automation | missing | | No command line or automation entry for the builder |
| Help, editing, preferences, layout | partial | Tooltips on every token and option, keyboard shortcuts, Esc returns focus | Contextual syntax help pages, configurable layout, builder preferences, localization |
