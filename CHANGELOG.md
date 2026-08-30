# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

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
