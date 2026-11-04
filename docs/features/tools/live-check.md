# Live check

`MonitoringTools.exe --live-check <folder>` runs the app's own code against real services on the computer and writes
`<folder>/livecheck.json`. The self-test (`--selftest`) proves logic and windows with no network; the live check proves
the parts that only mean something against the real thing. It uses a throwaway data folder, so the real settings are
never read or written.

Each check ends **passed**, **failed** or **skipped**. A check is skipped, never passed, when its service is not present,
and the reason is written beside it.

| Check | What it does |
| --- | --- |
| Narrator, English | Synthesises a sentence with the voice the narrator would pick and saves `voice-english.wav`. |
| Narrator, Cantonese | The same with a Cantonese (zh-HK) voice. Skipped when none is installed. |
| Ollama models | Asks the local server for its version, model list and a model's capabilities. |
| Ollama text attachment | Sends a text file as an attachment and checks the model reads a code word out of it. |
| Ollama image attachment | Sends a red square as an image attachment to a vision model and checks it names the colour. |
| Dim sum | Loads the live public dish catalogue and picks a dish with both names. |
| Forge | Publishes a folder to a test Gitea/Forgejo or GitLab server with a one-time token, then reads the pushed file back through the server's API. |
| Minecraft | Downloads the real server jar and Java, lists game rules, blocks and biomes from the jar's own reports, and writes a noise-settings and dimension-type edit from the jar's default files. |

## Settings

All optional, as environment variables for that one run:

| Variable | Meaning |
| --- | --- |
| `MT_LIVE_OLLAMA_MODEL` | Model to chat with. Default: the smallest installed model. |
| `MT_LIVE_MC` | Minecraft version. Default `1.21.4`. |
| `MT_LIVE_FORGE_URL`, `MT_LIVE_FORGE_KIND`, `MT_LIVE_FORGE_OWNER`, `MT_LIVE_FORGE_TOKEN` | A disposable test forge server (`gitea` or `gitlab`). The token is used for one push and never written to disk; it is masked in the step log. |

Use only a throwaway forge server and token. A local Gitea in a container works:
`docker run -d -p 127.0.0.1:3300:3000 -e GITEA__security__INSTALL_LOCK=true gitea/gitea:1.22.6`, then create a user and
token with `gitea admin user create` and `gitea admin user generate-access-token`.

## Last run on the maintainer's computer (2026-11-04)

| Check | Result |
| --- | --- |
| Narrator, English | passed (Microsoft David, 0.5 s) |
| Narrator, Cantonese | skipped: no Cantonese voice installed |
| Ollama models | passed (Ollama 0.33.3) |
| Ollama text attachment | passed (gemma4:12b, 43 s) |
| Ollama image attachment | passed (gemma4:12b, 45 s) |
| Dim sum | passed |
| Forge | passed (Gitea 1.22.6 in a local container; repository created, commit pushed, README read back) |
| Minecraft | passed (1.21.4 jar: game rules, blocks, biomes, noise and dimension edits) |

GitLab was not run: no GitLab server was available.
