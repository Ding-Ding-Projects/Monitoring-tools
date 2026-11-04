# Publishing to GitHub, GitLab and Gitea

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

## GitLab and Gitea or Forgejo
Choose the host at the top of the card. GitHub is the default and works as described above.

- **GitLab**: gitlab.com, or a self-hosted server whose address you type. **Gitea or Forgejo**: any server; its address is required.
- **Existing repository**: paste its address as the existing remote. Nothing is created, and the push uses whatever sign-in Git already has on this computer. The host is recognised from the address, so errors name it correctly.
- **New repository**: only after the same confirmation dialog as GitHub. The route is chosen in this order. First the host's command-line tool, `glab` for GitLab or `tea` for Gitea, when it is installed and signed in to that server (`glab auth status`, or a matching entry in `tea login list`). If it is missing or not signed in, a personal access token you paste for this one action is used for a single REST call (GitLab `POST /api/v4/projects`, Gitea `POST /api/v1/user/repos` or `/api/v1/orgs/{org}/repos`). If neither is available, the step stops and says what to do.
- **The token** is typed into a masked box, held in memory for that one action, sent only to the server you named, passed to Git for the push through that one process's environment (an HTTP header scoped to the server, never a file or a command-line argument), and cleared as soon as the action ends or the confirmation is dismissed. It is never saved, logged or shown, and error text has it masked. The box is emptied when you press Publish.
- The owner box is optional: a GitLab group or user path, or a Gitea organization. Empty means the account the token or tool signs in as.
- The route taken is reported when it finishes, as for GitHub.

## Recovery
A failure stays at the Publish button with Retry, Copy details and, where relevant: Sign in again (rejected credentials or missing scope), Get the GitHub CLI, Get Git for Windows, Copy the commands (identity), Copy a prompt for a coding assistant, Open the folder. The assistant prompt names the folder, remote, branch and error and forbids force-pushing, rewriting or dropping commits, and switching branches.

## Not done
- Forking is not offered; this copies your own folder into your own repository.
- Other hosts (Bitbucket, Azure DevOps and so on) are not offered.
- The GitLab and Gitea routes, and the `glab` and `tea` command lines, have not been run against real servers or accounts; they are compile-checked, and the address parsing is covered by the self-test. `tea`'s account name is read from `tea login list -o csv`, whose columns can differ between versions; when it cannot be read the step says so and asks for the repository address instead.
- The accounts list and the Sign in again recovery belong to GitHub only.
- Account listing relies on parsing `gh auth status` output, which can change between GitHub CLI versions.
- Not yet run against real GitHub accounts; compile-checked only.
