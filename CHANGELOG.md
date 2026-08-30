# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [2.1.0] - 2026-08-30

### Changed (Breaking)

- Renamed the secondary-endpoint config keys again: `SecondaryEndpoint` / `SecondaryApiKey` /
  `SecondaryModel` / `SecondaryEndpoint2` (etc.) / `SecondaryTimeoutSeconds` are now
  `AlternativeEndpoint` / `AlternativeApiKey` / `AlternativeModel` / `AlternativeEndpoint2` (etc.) /
  `AlternativeTimeoutSeconds`. If you're upgrading from 2.0.0 and were using the `Secondary*`
  keys, rename them in your existing `BepInEx/config/LlmTranslateOffline.yaml` — the old
  names are no longer read.

## [2.0.0] - 2026-08-30

### Changed (Breaking)

- Renamed the secondary-endpoint config keys: `FallbackEndpoint` / `FallbackApiKey` /
  `FallbackModel` / `FallbackEndpoint2` (etc.) / `FallbackTimeoutSeconds` are now
  `SecondaryEndpoint` / `SecondaryApiKey` / `SecondaryModel` / `SecondaryEndpoint2` (etc.) /
  `SecondaryTimeoutSeconds`. This avoids confusion with `AutoTranslatorConfig.ini`'s own,
  unrelated `FallbackEndpoint` setting (which switches to a completely different translator
  service, e.g. `FallbackEndpoint=GoogleTranslateV2`). If you're upgrading from 1.2.0 and
  were using the old `Fallback*` keys, rename them in your existing
  `BepInEx/config/LlmTranslateOffline.yaml` — the old names are no longer read.

### Added

- Logs a confirmation line to BepInEx's `LogOutput.log` when the plugin initializes:
  version, primary endpoint/model, and number of secondary endpoints configured.

## [1.2.0] - 2026-08-30

### Added

- Optional fallback endpoint(s): `FallbackEndpoint` / `FallbackApiKey` / `FallbackModel`
  (and numbered `FallbackEndpoint2`, etc. for more than one), plus `FallbackTimeoutSeconds`.
  When the primary `Endpoint` fails (connection error, timeout, or non-200 response),
  each fallback is tried in order using the same prompts and sampling settings — e.g.
  LM Studio as primary and Ollama as fallback, or two instances of either.

## [1.1.0] - 2026-08-30

### Changed

- Fixed the example config (`examples/LlmTranslateOffline.example.yaml`): reverted
  `DestinationLanguage` to its correct empty default. It's an optional per-endpoint
  override of `AutoTranslatorConfig.ini`'s `Language`/`FromLanguage`, not a required
  field, and the example previously showed it set to `"english"`.

## [1.0.0] - 2026-08-30

### Added

- Initial release: an XUnity.AutoTranslator translation endpoint (`LlmTranslateOffline`)
  that talks to any locally-hosted OpenAI-compatible chat-completions server (LM Studio,
  Ollama's OpenAI-compatible surface, etc.) — no cloud/hosted LLM endpoints.
- All settings live in an auto-generated `BepInEx/config/LlmTranslateOffline.yaml`:
  endpoint URL, API key, model, sampling parameters (temperature/top_p/max tokens),
  optional per-endpoint source/destination language override, and fully editable
  system prompt / user prompt template (multi-line YAML block scalars).
- Optional stripping of `<think>...</think>` blocks emitted by local reasoning models.
- No third-party runtime dependencies — JSON and the config file's YAML subset are
  parsed with small self-contained implementations bundled in the DLL.
