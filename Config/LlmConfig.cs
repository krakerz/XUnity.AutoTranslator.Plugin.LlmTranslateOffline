using System.Globalization;
using System.IO;
using XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Yaml;

namespace XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Config
{
    internal sealed class LlmConfig
    {
        public const string FileName = "LlmTranslateOffline.yaml";

        public string Endpoint;
        public string ApiKey;
        public string Model;
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
