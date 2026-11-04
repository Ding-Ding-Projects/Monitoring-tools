# Publishing to GitHub

Publishes a folder (or the IP history repository when it is set up) in five visible steps: check tools, prepare the repository, commit, choose the remote, upload.

## Rules it follows
- Git comes from the bundled MinGit. The GitHub CLI (`gh`) is optional and detected; without it, only pushing to an existing empty repository works.
- A repository is created only after you switch on "Create a new repository" and confirm a dialog that names the owner, the name and whether it is private or public. New repositories default to private.
- No token is ever read, stored or logged. Sign-in lives in the GitHub CLI's own credential store; this app reads only account names.
- Accounts: the list shows every account signed in to the GitHub CLI, with its own search bar (with the regex builder), Set active, Refresh sign-in and Sign out (two-key confirmation). Add account opens a terminal for the sign-in.
- The owner picker offers the active account and its organizations.
- If the folder is inside another repository, publishing stops and says which one, so nothing outside the chosen folder is published.
- If Git has no name or email, the commit step stops and shows the commands to run. The app does not invent an identity.
- "Upload after committing" can be switched off to commit locally only; the result then says nothing was uploaded.
- The result states the route taken: new repository created, existing remote used, or local commit only.

## Recovery
A failure stays at the Publish button with Retry, Copy details and, where relevant: Sign in again (rejected credentials or missing scope), Get the GitHub CLI, Get Git for Windows, Copy the commands (identity), Copy a prompt for a coding assistant, Open the folder. The assistant prompt names the folder, remote, branch and error and forbids force-pushing, rewriting or dropping commits, and switching branches.

## Not done
- Forking is not offered; this copies your own folder into your own repository.
- Only github.com is supported. Other forges and self-hosted instances are not.
- Account listing relies on parsing `gh auth status` output, which can change between GitHub CLI versions.
- Not yet run against real GitHub accounts; compile-checked only.
