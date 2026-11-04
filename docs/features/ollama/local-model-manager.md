# Local model manager

The Ollama page manages a local [Ollama](https://ollama.com) installation. Every request goes to `127.0.0.1` on the configured port (default 11434). Nothing is sent to a cloud service, and the app never contacts the Ollama website: downloads are performed by the local Ollama server itself.

Code: `Core/Ollama.cs` (client), `Core/Settings.Ollama.cs` (settings), `UI/Pages/OllamaPage.cs` (page).

## What it does

- **Detection.** Looks for `ollama.exe` in the per-user and Program Files install folders and on PATH, and asks `GET /api/version` whether a server answers. States shown: not installed, installed but not running, running with version.
- **Install.** Offers `winget install -e --id Ollama.Ollama` after an explicit consent dialog. If winget is missing, or the program is already installed, it says so and changes nothing. A button opens the official download page for the manual route.
- **Start and stop.** Start runs `ollama serve` hidden, with `OLLAMA_HOST` set to `127.0.0.1:<port>` so it is never exposed on the network. Stop (after a confirmation) ends the Ollama server and tray processes owned by the current user; models stay on disk.
- **Installed models.** `GET /api/tags`, showing size, family, parameter size and quantization. Filtered through the standard search field with its regex builder.
- **Pull.** `POST /api/pull` with a streamed progress bar and a Cancel button. Finished layers are kept, so a later pull resumes.
- **Delete.** `DELETE /api/delete` behind the two-step confirmation.
- **Loaded models.** `GET /api/ps` with memory use and whether the model sits on the GPU, plus an Unload button (`keep_alive` 0).
- **Chat test.** `POST /api/chat`, streamed token by token, multi-turn within the page, with Stop and Clear and a tokens-per-second figure when the server reports it.

## Settings (all in Settings.Ollama.cs, saved with the app settings)

| Setting | Default | Used by |
| --- | --- | --- |
| `OllamaPort` | 11434 | Every request and the started server |
| `OllamaLastModel` | empty | The chat test selection |
| `OllamaKeepAlive` | `5m` | The chat request's `keep_alive` |

## Not done

The full contract for this feature is larger than this page. These parts are not implemented, and nothing in the page pretends otherwise:

- A browsable catalogue of every official model and tag, hardware-fit verdicts and the batch-pull cart. Ollama has no documented catalogue API, so a model is pulled by typing its name.
- Chat attachments and capability gating.
- Launching other local tools through allow-listed profiles, snapshots and rollback.
- A bundled or cached installer and an offline troubleshooter; install relies on winget.
- Automatic periodic refresh. The page reads state when opened and on Refresh.

## Verification

Compiled only. The page was not run against a live Ollama server, so the install, pull and chat paths are unexercised against the real service.
