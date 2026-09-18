# Changelog

All notable changes to this project are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.2] — 2026-09-18

### Fixed

- Alternative endpoints were never tried when the primary was completely unreachable
  (connection refused / host down) — that failure happens inside XUnity.AutoTranslator's
  own request pipeline before this plugin's failover code ever runs, and repeated failures
  could trip XUnity's own consecutive-error limit and shut the endpoint down entirely.
  `OnCreateRequest` now probes each target with a short TCP connect and hands the framework
  whichever one answers first, primary first. A target that connects but returns a bad
  response is still handled by the existing failover in `OnExtractTranslation`.

## [2.1.1] — 2026-08-30

### Fixed

- Worked around a Mono-on-Wine bug where the first `WebRequest`/`WebClient` call in the
  process threw `NullReferenceException` from
  `System.Net.AutoWebProxyScriptEngine.InitializeRegistryGlobalProxy` while reading proxy
  settings from the (nonexistent) Windows registry — crashing XUnity.AutoTranslator's own
  pipeline before any translation, alternatives included. Fixed by setting
  `WebRequest.DefaultWebProxy = null` once, early in `Initialize()`.

## [2.1.0] — 2026-08-30

### Changed (Breaking)

- `SecondaryEndpoint` / `SecondaryApiKey` / `SecondaryModel` / `SecondaryEndpoint2` (etc.) /
  `SecondaryTimeoutSeconds` are now `AlternativeEndpoint` / `AlternativeApiKey` /
  `AlternativeModel` / `AlternativeEndpoint2` (etc.) / `AlternativeTimeoutSeconds`. Old
  names are no longer read.

## [2.0.0] — 2026-08-30

### Changed (Breaking)

- `FallbackEndpoint` / `FallbackApiKey` / `FallbackModel` / `FallbackEndpoint2` (etc.) /
  `FallbackTimeoutSeconds` are now `SecondaryEndpoint` / `SecondaryApiKey` /
  `SecondaryModel` / `SecondaryEndpoint2` (etc.) / `SecondaryTimeoutSeconds` — avoids
  colliding with `AutoTranslatorConfig.ini`'s own unrelated `FallbackEndpoint` setting,
  which switches to an entirely different translator service (e.g.
  `FallbackEndpoint=GoogleTranslateV2`). Old names are no longer read.

### Added

- Logs a confirmation line to BepInEx's `LogOutput.log` on initialization: version,
  primary endpoint/model, and number of secondary endpoints configured.

## [1.2.0] — 2026-08-30

### Added

- Optional fallback endpoint(s): `FallbackEndpoint` / `FallbackApiKey` / `FallbackModel`
  (and numbered `FallbackEndpoint2`, etc. for more than one), plus
  `FallbackTimeoutSeconds`. Triggered by connection error, timeout, or non-200 response,
  tried in order using the same prompts and sampling settings.

## [1.1.0] — 2026-08-30

### Changed

- `examples/LlmTranslateOffline.example.yaml`: reverted `DestinationLanguage` to its
  correct empty default. It's an optional per-endpoint override of
  `AutoTranslatorConfig.ini`'s `Language`/`FromLanguage`, not a required field.

## [1.0.0] — 2026-08-30

### Added

- `LlmTranslateOffline` endpoint for locally-hosted OpenAI-compatible chat-completions
  servers (LM Studio, Ollama's OpenAI-compatible surface, etc.). Offline-only by design.
- Auto-generated `BepInEx/config/LlmTranslateOffline.yaml`: endpoint URL, API key, model,
  sampling parameters (temperature/top_p/max tokens), optional per-endpoint
  source/destination language override, and fully editable system/user prompt templates.
- Optional stripping of `<think>...</think>` blocks emitted by local reasoning models.
- No third-party runtime dependencies — JSON and YAML are hand-rolled and bundled in the
  DLL.
