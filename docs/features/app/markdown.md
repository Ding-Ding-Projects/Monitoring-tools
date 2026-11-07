# Markdown in the app

One renderer shows Markdown wherever the app has some: release notes on the Records page, the text of a toast, and a copied or previewed `.md` file.

## What it shows

Headings, bold, italic, strikethrough, inline code, fenced code blocks, bullet, numbered and task lists, quotes, links and bare web addresses, and `:shortcode:` emoji such as `:tada:`, `:white_check_mark:`, `:warning:` and `:rocket:`. A shortcode it does not know stays as it was written, and so do times such as 12:30:45 and quick commands.

## Links

A web, mail or Windows Settings link opens through Windows. A relative link (`docs/setup.md`, `../README.md`) is resolved against the address the text came from, so links in release notes go to the right page of the repository on GitHub. A relative link with no address to resolve against, or a link to anything else (a file, a script), is shown as text and does nothing.

## Empty text

Text with nothing in it says "No notes were provided." instead of leaving a blank space.

## Safety and access

The renderer only builds text: it runs nothing, loads nothing from the web and shows no pictures. The result is a named region for screen readers, and every link is a real link in the keyboard order.

## Checks

`tests/SmartTests/MarkdownChecks.cs` runs on any machine: a known shortcode, an unknown one, times and quick commands that are not emoji, a relative and a `../` link against a repository address, an absolute link, and a relative link with no address to resolve against.
