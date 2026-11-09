# Git repository folders

Copying a folder that is a Git repository, or a folder inside one, gives its toast buttons for the repository itself. A
folder that is not in a repository gets **Start a Git repository** instead.

## Open in Desktop Material

Shown when Desktop Material is installed (version 4 or later, found in `%LOCALAPPDATA%\GitHubDesktop`). It starts
Desktop Material with `--cli-open=<repository folder>`. Desktop Material adds the repository to its list and selects it
straight away when it is not there yet; its **Add local repository** dialog only appears for a folder it cannot use as a
repository. A folder inside a repository opens the repository's top folder.

## Open on GitHub, GitLab or the server

Every remote of the repository that has a web page gets a button that opens that page in your browser:

- **One remote:** one button named after the service, **Open on GitHub**, **Open on GitLab**, **Open on Bitbucket**,
  **Open on Azure DevOps**, **Open on Codeberg**, **Open on Gitea**, or the server's own name for a self-hosted one
  (**Open on git.example.com**).
- **Several remotes:** a **Remote** dropdown list box beside the button lists every one as `name (host/path)`, for
  example `upstream (gitlab.com/team/app)`. `origin` is picked to start with, then the others follow in the order the
  repository lists them. The button follows the pick and opens that remote's page. Opening the list lets the toast take
  focus and holds its countdown, so the list's filter and the arrow keys work.

The remotes are read from the repository's own configuration (`.git/config`, or the main repository's for a linked
worktree) without starting Git, so the toast appears at once. `url.<base>.insteadOf` rewrites from the repository and
from your global Git configuration are applied first, as Git applies them.

| Remote address | Page |
| --- | --- |
| `https://github.com/owner/repo.git` | `https://github.com/owner/repo` |
| `git@gitlab.com:group/subgroup/project.git` | `https://gitlab.com/group/subgroup/project` |
| `ssh://git@git.example.com:2222/team/app.git` | `https://git.example.com/team/app` (an SSH port is not the web port) |
| `http://203.0.113.5:3000/me/notes.git` | `http://203.0.113.5:3000/me/notes` (an HTTP remote keeps its scheme and port) |
| `git@ssh.dev.azure.com:v3/org/project/repo` | `https://dev.azure.com/org/project/_git/repo` |
| `https://git-codecommit.eu-west-1.amazonaws.com/v1/repos/name` | the repository in the AWS console for that region |
| `https://bitbucket.example.com/scm/plat/api.git` | `https://bitbucket.example.com/projects/PLAT/repos/api` (Bitbucket Data Center, also from its SSH port 7999) |

A remote that is a local path, a network share, a `file:` address or a remote helper (`codecommit::…`) has no web page
and is left out. A repository without any remote with a page gets no button.

### Security

- A user name, password or access token in a remote address (`https://user:token@host/…`) is removed before anything is
  shown or opened, together with any query string and fragment.
- Only `http` and `https` pages are opened, through the same route as a copied link.
- Nothing is fetched to build the list.

## Start a Git repository

For a folder that is not inside a repository. It runs `git init -b main` in that folder with the bundled Git and commits
nothing. A notice then offers **Open in Desktop Material** when it is installed.

## Checks

- **Unit checks** (`tests/SmartTests`, any machine): 23 remote addresses and their pages, credentials, queries and
  fragments removed, 16 addresses with no page, the service name for 17 hosts, and a repository configuration with
  quoted sections, a trailing comment, a token, a local remote, the old `[remote.name]` form, an `insteadOf` rewrite and
  a linked worktree.
- **Self-test** (`MonitoringTools.exe --selftest`, Windows): a copied repository with three remotes offers **Open on
  GitHub** with two choices, origin first and no token anywhere; one Codeberg remote gives one button without a list; a
  local remote gives none; and the real toast shows the **Remote** dropdown list box, whose pick renames the button to
  **Open on GitLab**.
