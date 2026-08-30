using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Config;
using XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Json;

namespace XUnity.AutoTranslator.Plugin.LlmTranslateOffline
{
    // Translation endpoint that sends text to any OpenAI-chat-completions-compatible
    // server running locally (LM Studio, Ollama's "/v1/chat/completions" surface, etc.)
    // for translation. Offline-only by design: no cloud/hosted endpoints.
    // All settings live in BepInEx/config/LlmTranslateOffline.yaml, generated on first run.
    public class LlmTranslateOfflineEndpoint : HttpEndpoint
    {
        private static readonly Regex ThinkTagRegex = new Regex("<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private LlmConfig _config;

        public override string Id => "LlmTranslateOffline";

        public override string FriendlyName => "LLM Translate Offline (LM Studio / Ollama)";

        public override int MaxConcurrency => 1;

        public override void Initialize(IInitializationContext context)
        {
            var path = LlmConfig.ResolvePath(context.TranslatorDirectory);
            _config = LlmConfig.LoadOrCreate(path);
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var sourceLanguage = string.IsNullOrEmpty(_config.SourceLanguage) ? context.SourceLanguage : _config.SourceLanguage;
            var destinationLanguage = string.IsNullOrEmpty(_config.DestinationLanguage) ? context.DestinationLanguage : _config.DestinationLanguage;

            var systemPrompt = ApplyPlaceholders(_config.SystemPrompt, sourceLanguage, destinationLanguage, null);
            var userPrompt = ApplyPlaceholders(_config.UserPromptTemplate, sourceLanguage, destinationLanguage, context.UntranslatedText);

            var body = new StringBuilder();
            body.Append('{');

            body.Append("\"model\":");
            MiniJson.WriteString(body, _config.Model);

            body.Append(",\"messages\":[{\"role\":\"system\",\"content\":");
            MiniJson.WriteString(body, systemPrompt);
            body.Append("},{\"role\":\"user\",\"content\":");
            MiniJson.WriteString(body, userPrompt);
            body.Append("}]");

            body.Append(",\"temperature\":").Append(_config.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture));
            body.Append(",\"top_p\":").Append(_config.TopP.ToString(System.Globalization.CultureInfo.InvariantCulture));
            body.Append(",\"max_tokens\":").Append(_config.MaxTokens);
            body.Append(",\"stream\":false");
            body.Append('}');

            var request = new XUnityWebRequest("POST", _config.Endpoint, body.ToString());
            request.Headers = new WebHeaderCollection
            {
                { HttpRequestHeader.ContentType, "application/json" }
            };
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + _config.ApiKey;
            }

            context.Complete(request);
        }

        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            var response = context.Response;
            if (response.Code != HttpStatusCode.OK)
            {
                context.Fail($"LLM endpoint '{_config.Endpoint}' returned HTTP {(int)response.Code}: {response.Data}", null);
                return;
            }

            try
            {
                var root = MiniJson.Parse(response.Data) as Dictionary<string, object>;
                var choices = root?["choices"] as List<object>;
                var firstChoice = choices?[0] as Dictionary<string, object>;
                var message = firstChoice?["message"] as Dictionary<string, object>;
                var content = message?["content"] as string;

                if (string.IsNullOrEmpty(content))
                {
                    context.Fail("LLM response did not contain any message content.", null);
                    return;
                }

                if (_config.StripReasoning)
                {
                    content = ThinkTagRegex.Replace(content, string.Empty);
                }

                context.Complete(content.Trim());
            }
            catch (Exception ex)
            {
                context.Fail("Failed to parse LLM response as JSON.", ex);
            }
        }

        private static string ApplyPlaceholders(string template, string sourceLanguage, string destinationLanguage, string input)
        {
            var result = template
                .Replace("{{SourceLanguage}}", sourceLanguage)
                .Replace("{{DestinationLanguage}}", destinationLanguage);

            if (input != null)
            {
                result = result.Replace("{{Input}}", input);
            }

            return result;
        }
    }
}
