# Local model manager

The Ollama page manages a local [Ollama](https://ollama.com) installation. Every request goes to `127.0.0.1` on the configured port (default 11434). Nothing is sent to a cloud service, and downloads are performed by the local Ollama server itself. The only call to the Ollama website is the optional online catalogue search described below, which runs only when you press its button.

Code: `Core/Ollama.cs` (client), `Core/OllamaCatalogue.cs` (catalogue, hardware facts, fit verdict), `Core/OllamaDiagnostics.cs` (troubleshooter checks), `Core/Settings.Ollama.cs` (settings), `UI/Pages/OllamaPage.cs` and `UI/Pages/OllamaPage.Suite.cs` (page).

## What it does

- **Status hero.** The first card shows the state in large type with a status dot that breathes while the server runs, the program path and address, and three numbers: installed models, their total size on disk, and models loaded in memory. It is mint while the server runs and amber when Ollama is stopped or missing. The Install, Start, Stop, Refresh and download-page buttons and the port field sit under it.
- **Detection.** Looks for `ollama.exe` in the per-user and Program Files install folders and on PATH, and asks `GET /api/version` whether a server answers. States shown: not installed, installed but not running, running with version.
- **Install.** Offers `winget install -e --id Ollama.Ollama` after an explicit consent dialog. If winget is missing, or the program is already installed, it says so and changes nothing. A button opens the official download page for the manual route.
- **Start and stop.** Start runs `ollama serve` hidden, with `OLLAMA_HOST` set to `127.0.0.1:<port>` so it is never exposed on the network. Stop (after a confirmation) ends the Ollama server and tray processes owned by the current user; models stay on disk.
- **Installed models.** `GET /api/tags`, one row per model with an initial tile and chips for size, family, parameter size and quantization; the rows rise in order when the list loads. Filtered through the standard search field with its regex builder.
- **Pull.** `POST /api/pull` with a streamed progress bar that glides between updates and a Cancel button. Finished layers are kept, so a later pull resumes.
- **Delete.** `DELETE /api/delete` behind the two-step confirmation.
- **Loaded models.** `GET /api/ps` with memory use, a split bar showing the GPU share (mint) and the CPU share (amber), and an Unload button (`keep_alive` 0).
- **Model catalogue.** An embedded list of about 30 well-known Ollama library tags (family, parameter count, typical download size, context length, one-line note), dated 2026-11-04 and labelled as a snapshot. Sizes are typical figures for the default 4-bit version, written by hand, not measurements. Searchable with the standard search field and regex builder.
- **Online search (optional).** A button sends your search words to `https://ollama.com/search` and reads the result page, because Ollama documents no catalogue API. It is the only action that contacts the Ollama website and runs only when pressed. The page layout is not a stable interface: the parser reads model names, descriptions and the advertised parameter sizes, estimates the download as roughly 0.6 GB per billion parameters (labelled "about"), and reports an error rather than inventing rows when nothing can be read. Its patterns were checked once against the live page on 2026-11-04.
- **Hardware-fit verdict.** Shown on every catalogue row, from facts read on this computer: total and free RAM (`GlobalMemoryStatusEx`), graphics adapter names and memory (WMI `Win32_VideoController`, using the display driver's 64-bit registry figure when it exists because WMI `AdapterRAM` cannot report above 4 GB), and free disk on the drive holding the models folder (`OLLAMA_MODELS`, otherwise `%USERPROFILE%\.ollama\models`). The need is estimated as the download size plus 15 percent and 0.5 GB for the context cache, so a reason reads, for example, "Needs about 10.8 GB; you have 6.2 GB of free RAM". Levels: fits on the GPU, runs partly on the CPU, tight, too large, not enough disk. This is a rule of thumb: real use varies with context length and quantisation.
- **Pull cart.** Add several catalogue models, see the combined size against free disk, then Pull all pulls them one after another through the local server with progress, cancel and per-model results. A finished model leaves the cart, a failed one stays. The cart persists between sessions.
- **Model details.** A Details button on each installed model calls `POST /api/show` and shows family, parameters, quantisation, context length, capabilities (when the server version reports them), default parameters, the prompt template and a licence summary. The licence summary names a recognised licence (Apache, MIT, Llama community, Gemma terms, Creative Commons) or says it is custom, and quotes the opening lines; it is an automatic summary, not legal advice.
- **Troubleshooter.** Offline checks from local facts only: installed or not, server answering, an Ollama process that is not answering, whether the port is taken and by which program (netstat plus the process list), `OLLAMA_HOST` (loopback or exposed), the models folder and free space, and the log. Actions: Run checks, Start server, Show log (tail of `%LOCALAPPDATA%\Ollama\server.log`), Open logs folder, Open models folder, Find a free port. The firewall entry is a hint only; loopback traffic is not filtered, and the tool does not read firewall rules.
- **Chat.** `POST /api/chat`, streamed token by token, multi-turn within the page, with Stop and New chat and a tokens-per-second figure when the server reports it. Attachments, saved chats, profiles, per-chat parameters and the capability badge are in [chat.md](chat.md).

## Settings (all in Settings.Ollama.cs, saved with the app settings)

| Setting | Default | Used by |
| --- | --- | --- |
| `OllamaPort` | 11434 | Every request and the started server |
| `OllamaLastModel` | empty | The chat test selection |
| `OllamaKeepAlive` | `5m` | The chat request's `keep_alive` |
| `OllamaCart` | empty | The pull cart |

## Not done

- The catalogue is a hand-written snapshot, not the whole library, and the online search depends on an undocumented page layout.
- Hardware verdicts are a rule of thumb, not a measurement; multi-GPU splitting and unified-memory machines are not modelled, and integrated graphics report whatever the driver exposes.
- The troubleshooter does not read Windows Firewall rules or any Ollama log other than `server.log`.
- Tool calling and launching other local tools; chat attachments, saved chats, profiles and the capability badge are described in [chat.md](chat.md).
- Launching other local tools through allow-listed profiles, snapshots and rollback.
- A bundled or cached installer; install relies on winget.
- Automatic periodic refresh. The page reads state when opened and on Refresh.

## Verification

Compiled only. The page was not run against a live Ollama server and the new cards were not driven in the running app. The online-search patterns were checked against one live fetch of the search page (names, descriptions and size chips came out correctly for four rows); the fit calculation, `GlobalMemoryStatusEx`, the PowerShell adapter query, `/api/show` and the troubleshooter were not exercised.
