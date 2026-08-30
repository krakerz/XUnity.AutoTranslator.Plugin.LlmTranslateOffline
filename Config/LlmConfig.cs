using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Yaml;

namespace XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Config
{
    // One OpenAI-chat-completions-compatible server to send translation requests to.
    internal sealed class LlmEndpointTarget
    {
        public string Endpoint;
        public string ApiKey;
        public string Model;
    }

    internal sealed class LlmConfig
    {
        public const string FileName = "LlmTranslateOffline.yaml";

        // Highest number of numbered "AlternativeEndpointN" entries that will be read from the file.
        private const int MaxAlternatives = 5;

        public string Endpoint;
        public string ApiKey;
        public string Model;
        public List<LlmEndpointTarget> Alternatives;
        public int AlternativeTimeoutSeconds;
        public float Temperature;
        public float TopP;
        public int MaxTokens;
        public bool StripReasoning;
        public string SourceLanguage;
        public string DestinationLanguage;
        public string SystemPrompt;
        public string UserPromptTemplate;

        // Resolves BepInEx/config/LLMConfig.yaml from the translator's own directory
        // (…/BepInEx/plugins/XUnity.AutoTranslator/Translators), falling back to that
        // same directory if the expected BepInEx layout isn't found.
        public static string ResolvePath(string translatorDirectory)
        {
            var bepinexRoot = Directory.GetParent(translatorDirectory)?.Parent?.Parent;
            var configDir = bepinexRoot != null ? Path.Combine(bepinexRoot.FullName, "config") : translatorDirectory;
            return Path.Combine(configDir, FileName);
        }

        public static LlmConfig LoadOrCreate(string path)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, BuildDefaultYaml());
            }

            var raw = MiniYaml.ParseFlat(File.ReadAllText(path));

            return new LlmConfig
            {
                Endpoint = GetString(raw, "Endpoint", "http://localhost:1234/v1/chat/completions"),
                ApiKey = GetString(raw, "ApiKey", ""),
                Model = GetString(raw, "Model", "local-model"),
                Alternatives = ParseAlternatives(raw),
                AlternativeTimeoutSeconds = GetInt(raw, "AlternativeTimeoutSeconds", 60),
                Temperature = GetFloat(raw, "Temperature", 0.3f),
                TopP = GetFloat(raw, "TopP", 1.0f),
                MaxTokens = GetInt(raw, "MaxTokens", 1000),
                StripReasoning = GetBool(raw, "StripReasoning", true),
                SourceLanguage = GetString(raw, "SourceLanguage", ""),
                DestinationLanguage = GetString(raw, "DestinationLanguage", ""),
                SystemPrompt = GetString(raw, "SystemPrompt", DefaultSystemPrompt),
                UserPromptTemplate = GetString(raw, "UserPromptTemplate", DefaultUserPromptTemplate),
            };
        }

        // Reads AlternativeEndpoint/AlternativeApiKey/AlternativeModel, then AlternativeEndpoint2/... up to
        // MaxAlternatives. Stops at the first missing AlternativeEndpointN so numbering must be contiguous.
        // Named "Alternative" (not "Fallback") to avoid confusion with AutoTranslatorConfig.ini's own,
        // unrelated FallbackEndpoint setting (which switches to a different translator service entirely).
        private static List<LlmEndpointTarget> ParseAlternatives(Dictionary<string, string> raw)
        {
            var defaultModel = GetString(raw, "Model", "local-model");
            var alternatives = new List<LlmEndpointTarget>();

            for (var i = 1; i <= MaxAlternatives; i++)
            {
                var suffix = i == 1 ? "" : i.ToString(CultureInfo.InvariantCulture);
                if (!raw.TryGetValue("AlternativeEndpoint" + suffix, out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
                {
                    break;
                }

                var model = GetString(raw, "AlternativeModel" + suffix, "");
                alternatives.Add(new LlmEndpointTarget
                {
                    Endpoint = endpoint,
                    ApiKey = GetString(raw, "AlternativeApiKey" + suffix, ""),
                    Model = string.IsNullOrEmpty(model) ? defaultModel : model,
                });
            }

            return alternatives;
        }

        private const string DefaultSystemPrompt =
            "You are a translation engine. Translate the given text from {{SourceLanguage}} to {{DestinationLanguage}}.\n" +
            "Only output the translated text, with no explanations, notes, quotes or extra formatting.";

        private const string DefaultUserPromptTemplate = "{{Input}}";

        private static string BuildDefaultYaml()
        {
            return
                "# XUnity.AutoTranslator LLM Translate (Offline) endpoint configuration.\n" +
                "# Generated automatically on first run. Edit this file and restart the game to apply changes.\n" +
                "# To use this endpoint, set Endpoint=LlmTranslateOffline in AutoTranslatorConfig.ini.\n" +
                "\n" +
                "# Base URL of the OpenAI-compatible chat-completions endpoint.\n" +
                "# LM Studio default:  http://localhost:1234/v1/chat/completions\n" +
                "# Ollama (OpenAI-compatible) default: http://localhost:11434/v1/chat/completions\n" +
                "Endpoint: http://localhost:1234/v1/chat/completions\n" +
                "\n" +
                "# API key sent as \"Authorization: Bearer <ApiKey>\". Leave empty if your server doesn't need one.\n" +
                "ApiKey: \"\"\n" +
                "\n" +
                "# Model name/id exactly as your server expects it.\n" +
                "Model: local-model\n" +
                "\n" +
                "# --- Alternative endpoint(s) (optional) ---\n" +
                "# If the primary Endpoint above fails (connection error, timeout, or a non-200\n" +
                "# response), this plugin retries the same request against each alternative below, in\n" +
                "# order, using the same prompts and sampling settings. Leave AlternativeEndpoint empty\n" +
                "# (default) to disable this and only ever use the primary endpoint.\n" +
                "#\n" +
                "# Named \"Alternative\", not \"Fallback\", so it isn't confused with AutoTranslatorConfig.ini's\n" +
                "# own FallbackEndpoint setting, which is a different, unrelated feature (it switches to a\n" +
                "# completely different translator service, e.g. FallbackEndpoint=GoogleTranslateV2).\n" +
                "#\n" +
                "# Example: LM Studio as primary, Ollama as alternative (or the other way around,\n" +
                "# or two LM Studio/Ollama instances on different ports).\n" +
                "AlternativeEndpoint: \"\"\n" +
                "AlternativeApiKey: \"\"\n" +
                "AlternativeModel: \"\"\n" +
                "\n" +
                "# Add more alternatives by numbering the keys (tried in order after AlternativeEndpoint):\n" +
                "# AlternativeEndpoint2: http://localhost:11434/v1/chat/completions\n" +
                "# AlternativeApiKey2: \"\"\n" +
                "# AlternativeModel2: llama3\n" +
                "\n" +
                "# How long to wait (in seconds) for an alternative endpoint to respond before giving up on it.\n" +
                "AlternativeTimeoutSeconds: 60\n" +
                "\n" +
                "# Sampling parameters passed to the LLM.\n" +
                "Temperature: 0.3\n" +
                "TopP: 1.0\n" +
                "MaxTokens: 1000\n" +
                "\n" +
                "# Strip <think>...</think> blocks that some local reasoning models emit.\n" +
                "StripReasoning: true\n" +
                "\n" +
                "# Leave empty to use the SourceLanguage/DestinationLanguage from AutoTranslatorConfig.ini.\n" +
                "# Set these to override the language pair just for this endpoint.\n" +
                "SourceLanguage: \"\"\n" +
                "DestinationLanguage: \"\"\n" +
                "\n" +
                "# System prompt sent as the \"system\" role message.\n" +
                "# Placeholders: {{SourceLanguage}}, {{DestinationLanguage}}\n" +
                "SystemPrompt: |\n" +
                "  You are a translation engine. Translate the given text from {{SourceLanguage}} to {{DestinationLanguage}}.\n" +
                "  Only output the translated text, with no explanations, notes, quotes or extra formatting.\n" +
                "\n" +
                "# User prompt template sent as the \"user\" role message.\n" +
                "# Placeholders: {{SourceLanguage}}, {{DestinationLanguage}}, {{Input}}\n" +
                "UserPromptTemplate: |\n" +
                "  {{Input}}\n";
        }

        private static string GetString(System.Collections.Generic.Dictionary<string, string> raw, string key, string fallback)
        {
            return raw.TryGetValue(key, out var value) ? value : fallback;
        }

        private static float GetFloat(System.Collections.Generic.Dictionary<string, string> raw, string key, float fallback)
        {
            return raw.TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static int GetInt(System.Collections.Generic.Dictionary<string, string> raw, string key, int fallback)
        {
            return raw.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static bool GetBool(System.Collections.Generic.Dictionary<string, string> raw, string key, bool fallback)
        {
            return raw.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}
