# XUnity.AutoTranslator.Plugin.LlmTranslateOffline

A translation endpoint plugin for [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator)
that sends in-game text to a **locally-hosted**, OpenAI-compatible chat-completions
server for translation — [LM Studio](https://lmstudio.ai/), [Ollama](https://ollama.com/)
(via its OpenAI-compatible API surface), or anything else exposing the same API shape.

This project is offline-only by design: it never talks to a cloud/hosted LLM provider,
only to a server you run yourself.

## Features

- Works with any OpenAI-compatible `/v1/chat/completions` server (LM Studio, Ollama, etc.)
- Optional alternative endpoint(s) — e.g. run LM Studio as primary and Ollama as
  alternative (or two instances of either) — automatically tried in order if the primary fails
- Optional API key (`Authorization: Bearer <key>`) for servers that require one
- Fully configurable model name, temperature, top_p, and max tokens
- Editable system prompt and user prompt template, with `{{SourceLanguage}}`,
  `{{DestinationLanguage}}` and `{{Input}}` placeholders
- Optional per-endpoint source/destination language override
- Strips `<think>...</think>` reasoning blocks some local models emit
- No third-party runtime dependencies (JSON/YAML handling is self-contained)
- Logs a confirmation line to BepInEx's `LogOutput.log` on load (version, primary
  endpoint/model, number of alternatives configured), so you can confirm the plugin
  actually started without needing to trigger a translation first

## Requirements

- A game already modded with [BepInEx](https://github.com/BepInEx/BepInEx) and
  [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator)
- [LM Studio](https://lmstudio.ai/) or [Ollama](https://ollama.com/) running locally
  with a model loaded and its OpenAI-compatible API server enabled

## Installation

1. Download the latest release zip from the [Releases](../../releases) page.
2. Copy `LlmTranslateOffline.dll` into:
   ```
   BepInEx/plugins/XUnity.AutoTranslator/Translators/
   ```
3. Copy the example config into:
   ```
   BepInEx/config/LlmTranslateOffline.yaml
   ```
   (If you skip this step, the plugin generates this file itself with the same
   defaults the first time it runs.)
4. In `BepInEx/config/AutoTranslatorConfig.ini`, under `[Service]`, set:
   ```ini
   Endpoint=LlmTranslateOffline
   ```
5. Edit `BepInEx/config/LlmTranslateOffline.yaml` to point at your LM Studio/Ollama
   server, model, and API key, then launch the game.

## Configuration

All settings live in `BepInEx/config/LlmTranslateOffline.yaml`, generated automatically
on first run. See [`examples/LlmTranslateOffline.example.yaml`](examples/LlmTranslateOffline.example.yaml)
for the full file with comments.

| Key | Default | Description |
|---|---|---|
| `Endpoint` | `http://localhost:1234/v1/chat/completions` | Chat-completions URL. LM Studio default shown; Ollama's OpenAI-compatible default is `http://localhost:11434/v1/chat/completions`. |
| `ApiKey` | *(empty)* | Sent as `Authorization: Bearer <ApiKey>` when non-empty. |
| `Model` | `local-model` | Model name/id exactly as your server expects it. |
| `AlternativeEndpoint` / `AlternativeApiKey` / `AlternativeModel` | *(empty)* | Optional alternative server, tried when the primary `Endpoint` fails (connection error, timeout, or non-200 response). Leave `AlternativeEndpoint` empty to disable. Add more with numbered keys: `AlternativeEndpoint2`, `AlternativeApiKey2`, `AlternativeModel2`, etc. (numbering must be contiguous). Named "Alternative" rather than "Fallback" to avoid confusion with `AutoTranslatorConfig.ini`'s own, unrelated `FallbackEndpoint` setting (which switches to an entirely different translator service). |
| `AlternativeTimeoutSeconds` | `60` | How long to wait for each alternative endpoint before giving up on it. |
| `Temperature` | `0.3` | Sampling temperature. |
| `TopP` | `1.0` | Nucleus sampling parameter. |
| `MaxTokens` | `1000` | Max tokens in the completion. |
| `StripReasoning` | `true` | Strips `<think>...</think>` blocks from the response. |
| `SourceLanguage` / `DestinationLanguage` | *(empty)* | Override the language pair for this endpoint only; empty uses the values from `AutoTranslatorConfig.ini`. |
| `SystemPrompt` | translation system prompt | Sent as the `system` role message. Supports `{{SourceLanguage}}` / `{{DestinationLanguage}}`. |
| `UserPromptTemplate` | `{{Input}}` | Sent as the `user` role message. Supports `{{SourceLanguage}}`, `{{DestinationLanguage}}`, `{{Input}}`. |

Restart the game after editing the config to apply changes.

## Building from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (works on Linux, macOS,
and Windows — no Windows-only tooling needed):

```sh
dotnet build -c Release
```

The output DLL is written to `bin/Release/XUnity.AutoTranslator.Plugin.LlmTranslateOffline.dll`.

## Versioning & Changelog

This project follows [Semantic Versioning](https://semver.org/). See
[`CHANGELOG.md`](CHANGELOG.md) for a version-by-version history of changes.

## License

MIT — see [`LICENSE`](LICENSE).

---

**Note:** This project's code, CI, and documentation were developed with the help of AI (Claude).
