# Changelog

All notable changes to this project are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.1] — 2026-08-30

<div align="justify">

Fixes a crash that made the plugin appear completely dead under Mono-on-Wine. The first
web request in the process threw before any translation could be attempted, taking
XUnity.AutoTranslator's own pipeline down with it, so alternative endpoints never got a
chance to run. No config changes are needed — update the DLL and the failover path works
as documented.

</div>

### Fixed

- Worked around a Mono-on-Wine bug where the first `WebRequest`/`WebClient` call in the
  process throws `NullReferenceException` from
  `System.Net.AutoWebProxyScriptEngine.InitializeRegistryGlobalProxy` while trying to read
  proxy settings from the (nonexistent) Windows registry. Fixed by explicitly setting
  `WebRequest.DefaultWebProxy = null` once, early in `Initialize()`.

## [2.1.0] — 2026-08-30

<div align="justify">

Renames the secondary-endpoint config keys one final time, to `Alternative*`. If you are
upgrading from 2.0.0 and were using the `Secondary*` keys, rename them in your existing
`BepInEx/config/LlmTranslateOffline.yaml` — the old names are no longer read, and an
unrenamed key leaves the alternative endpoint silently disabled.

</div>

### Changed (Breaking)

- `SecondaryEndpoint` / `SecondaryApiKey` / `SecondaryModel` / `SecondaryEndpoint2` (etc.) /
  `SecondaryTimeoutSeconds` are now `AlternativeEndpoint` / `AlternativeApiKey` /
  `AlternativeModel` / `AlternativeEndpoint2` (etc.) / `AlternativeTimeoutSeconds`.

## [2.0.0] — 2026-08-30

<div align="justify">

Renames the secondary-endpoint config keys away from `Fallback*`, which collided
confusingly with `AutoTranslatorConfig.ini`'s own unrelated `FallbackEndpoint` setting —
that one switches to an entirely different translator service, such as
`FallbackEndpoint=GoogleTranslateV2`. The plugin also now logs a line on load, so you can
confirm it started without having to trigger a translation first.

</div>

### Changed (Breaking)

- `FallbackEndpoint` / `FallbackApiKey` / `FallbackModel` / `FallbackEndpoint2` (etc.) /
  `FallbackTimeoutSeconds` are now `SecondaryEndpoint` / `SecondaryApiKey` /
  `SecondaryModel` / `SecondaryEndpoint2` (etc.) / `SecondaryTimeoutSeconds`. Upgrading
  from 1.2.0 requires renaming these in your existing config; the old names are no longer
  read.

### Added

- Logs a confirmation line to BepInEx's `LogOutput.log` on initialization: version,
  primary endpoint/model, and number of secondary endpoints configured.

## [1.2.0] — 2026-08-30

<div align="justify">

Adds automatic failover across multiple local servers. Configure one or more alternatives
and the plugin tries each in order whenever the primary fails, using the same prompts and
sampling settings throughout — for example LM Studio as primary with Ollama behind it, or
two instances of either.

</div>

### Added

- Optional fallback endpoint(s): `FallbackEndpoint` / `FallbackApiKey` / `FallbackModel`
  (and numbered `FallbackEndpoint2`, etc. for more than one), plus
  `FallbackTimeoutSeconds`. Triggered by connection error, timeout, or non-200 response.

## [1.1.0] — 2026-08-30

<div align="justify">

Corrects the shipped example config, which showed an optional field as though it were
required.

</div>

### Changed

- `examples/LlmTranslateOffline.example.yaml`: reverted `DestinationLanguage` to its
  correct empty default. It is an optional per-endpoint override of
  `AutoTranslatorConfig.ini`'s `Language`/`FromLanguage`, not a required field, and the
  example previously showed it set to `"english"`.

## [1.0.0] — 2026-08-30

<div align="justify">

First release. Adds an XUnity.AutoTranslator translation endpoint that sends in-game text
to any locally-hosted, OpenAI-compatible chat-completions server — LM Studio, Ollama's
OpenAI-compatible surface, or anything else exposing the same API shape. The plugin is
offline-only by design and never contacts a cloud or hosted LLM provider. It ships as a
single DLL with no third-party runtime dependencies.

</div>

### Added

- `LlmTranslateOffline` endpoint for locally-hosted OpenAI-compatible chat-completions
  servers.
- Auto-generated `BepInEx/config/LlmTranslateOffline.yaml` holding every setting: endpoint
  URL, API key, model, sampling parameters (temperature / top_p / max tokens), optional
  per-endpoint source and destination language override, and fully editable system prompt
  and user prompt template as multi-line YAML block scalars.
- Optional stripping of `<think>...</think>` blocks emitted by local reasoning models.
- Self-contained JSON and YAML-subset parsers bundled in the DLL, so there are no
  third-party runtime dependencies.
