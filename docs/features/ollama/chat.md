# Chat, attachments, saved chats and profiles

The chat card on the Ollama page is a local conversation with an installed model. Like the rest of the page it talks only to `127.0.0.1` on the configured port.

Code: `Core/OllamaChatKit.cs` (attachments, chat request body, capability reading, saved chats, profiles), `Core/Ollama.cs` (`ChatAsync` with options), `UI/Pages/OllamaPage.Chat.cs` (the card).

## Capability badge

When a model is selected the card calls `POST /api/show` and shows the `capabilities` list the server returns, for example completion, vision, tools, embedding or thinking. Vision, tools and embedding are always shown as present or absent once the server has reported a list. Nothing is inferred from a model name.

- If the server's reply has no capabilities list at all (an older Ollama), the badge says capabilities are unknown, and does not claim or deny vision or tools.
- A model that reports only the embedding capability cannot chat; Send refuses with that reason.
- The stated context length from `model_info` is shown when present, and a context value above it is flagged.

## Attachments

Use **Attach file** or drop files on the card. At most 6 files per message.

| Kind | How it is handled | Limits |
| --- | --- | --- |
| Image (PNG, JPEG, GIF, WebP, BMP, recognised by the file's first bytes, not its name) | Base64 of the original bytes goes in the message's `images` array, as the `/api/chat` documentation describes. Nothing is resized or converted. | 12 MB per image |
| Text file | Read as text (UTF-8, or UTF-16 with a byte order mark, otherwise Latin-1) and pasted into the message in a labelled fenced block. | File up to 1 MB; the first 24,000 characters are included and the message says when a file was cut; 60,000 characters of attached text per message |

Binary files that are not images are refused. An image is refused when the server reports capabilities and vision is not among them; when capabilities are unknown the image is attached with a warning. Whether a given model really understands an image is up to the model.

## Saved chats

Chats are stored only when you press **Save chat**, or when **Save chats automatically** is on (default off). Each chat is one JSON file in `ollama-chats` under the app data folder, holding the model, system prompt, parameters, profile name and messages. Image bytes are not saved, only the names of attached files; text attachments are part of the saved message. A reopened chat therefore does not re-send earlier images.

The saved-chats list has the standard search field with the regex builder (it searches titles, models, profiles and message text). Each row has Open (restores messages, system prompt and parameters, and selects the model when it is installed), Export (Markdown) and Delete (two-step confirmation; removes the file). **Export as Markdown** on the card writes the current chat to a file you choose.

## Profiles (system prompt presets)

A profile is a name, a system prompt and optional temperature, top-p and context length. Three editable starter profiles (Plain, Concise, Careful reviewer) are shown until you save your own list. Clicking a profile fills the system prompt and parameter boxes; you can still edit them for this chat. **Save as profile** stores the current boxes under the name in the name box (an existing name is replaced); **Delete profile** removes the named one after confirmation. Profiles live in `ollama-profiles.json` in the app data folder. A profile holds no tool definitions: the Ollama tool-calling feature is not driven from here.

## Parameters per chat

Temperature (0 to 2), top-p (above 0, up to 1), context length in tokens (128 to 1,048,576) and keep-alive (such as `5m`, `1h`, `30s`, or `0` to unload at once). An empty field is left out of the request, so the model's own default applies. They apply to the open chat and are saved with it. **Default keep-alive** (5 minutes unless changed) is used when the chat sets none. Invalid values are explained under the fields and block Send.

## Settings

| Setting | Default | Used by |
| --- | --- | --- |
| `OllamaAutoSaveChats` | off | Saves after every reply |
| `OllamaLastProfile` | empty | The profile selected when the page opens |
| `OllamaKeepAlive` | `5m` | Chats that set no keep-alive of their own |

## Not done

- No tool calling, no function definitions and no launching of other local programs.
- No image resizing or format conversion, and no PDF or office document reading; only images and plain text are attachable.
- Image bytes are not stored in saved chats.
- Capability reading depends on the server version; older servers give "unknown".

## Verification

Compiled, and the self-test covers the pure parts with no network: image encoding into the `images` array, refusal of non-images, binary files and oversized images, the text cap and fence widening, the chat request body (system prompt first, only set options, `keep_alive` 0 as a number), parameter validation, capability gating, saved-chat round trip with Markdown export and deletion, and profile upsert rules. The card was not driven in the running app and was not run against a live Ollama server.
