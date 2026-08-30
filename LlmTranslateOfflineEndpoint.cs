using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Config;
using XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Json;
using XUnity.Common.Logging;

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

            var version = GetType().Assembly.GetName().Version;
            XuaLogger.AutoTranslator.Info(
                $"LlmTranslateOffline v{version} loaded. Primary endpoint: {_config.Endpoint} (model: {_config.Model}). " +
                $"Alternative endpoints configured: {_config.Alternatives.Count}.");
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var primary = new LlmEndpointTarget { Endpoint = _config.Endpoint, ApiKey = _config.ApiKey, Model = _config.Model };
            var (systemPrompt, userPrompt) = BuildPrompts(context);

            context.Complete(BuildRequest(primary, systemPrompt, userPrompt));
        }

        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            var response = context.Response;
            string content = null, parseError = null;
            if (response.Code == HttpStatusCode.OK && TryExtractContent(response.Data, out content, out parseError))
            {
                context.Complete(FinalizeContent(content));
                return;
            }

            var primaryError = response.Code == HttpStatusCode.OK
                ? $"Endpoint '{_config.Endpoint}' returned an unparsable response: {parseError}"
                : $"Endpoint '{_config.Endpoint}' returned HTTP {(int)response.Code}: {response.Data}";

            if (_config.Alternatives.Count == 0)
            {
                context.Fail(primaryError, null);
                return;
            }

            var (systemPrompt, userPrompt) = BuildPrompts(context);
            var errors = new List<string> { primaryError };

            foreach (var alternative in _config.Alternatives)
            {
                if (TrySendSync(alternative, systemPrompt, userPrompt, out var alternativeContent, out var alternativeError))
                {
                    context.Complete(FinalizeContent(alternativeContent));
                    return;
                }

                errors.Add(alternativeError);
            }

            context.Fail("All LLM endpoints failed:\n" + string.Join("\n", errors), null);
        }

        private (string systemPrompt, string userPrompt) BuildPrompts(ITranslationContextBase context)
        {
            var sourceLanguage = string.IsNullOrEmpty(_config.SourceLanguage) ? context.SourceLanguage : _config.SourceLanguage;
            var destinationLanguage = string.IsNullOrEmpty(_config.DestinationLanguage) ? context.DestinationLanguage : _config.DestinationLanguage;

            var systemPrompt = ApplyPlaceholders(_config.SystemPrompt, sourceLanguage, destinationLanguage, null);
            var userPrompt = ApplyPlaceholders(_config.UserPromptTemplate, sourceLanguage, destinationLanguage, context.UntranslatedText);
            return (systemPrompt, userPrompt);
        }

        private string BuildRequestBody(LlmEndpointTarget target, string systemPrompt, string userPrompt)
        {
            var body = new StringBuilder();
            body.Append('{');

            body.Append("\"model\":");
            MiniJson.WriteString(body, target.Model);

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

            return body.ToString();
        }

        private XUnityWebRequest BuildRequest(LlmEndpointTarget target, string systemPrompt, string userPrompt)
        {
            var request = new XUnityWebRequest("POST", target.Endpoint, BuildRequestBody(target, systemPrompt, userPrompt));
            request.Headers = new WebHeaderCollection
            {
                { HttpRequestHeader.ContentType, "application/json" }
            };
            if (!string.IsNullOrEmpty(target.ApiKey))
            {
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + target.ApiKey;
            }

            return request;
        }

        // Synchronously calls an alternative endpoint. Safe to block here: this endpoint's
        // MaxConcurrency is 1, and the framework already runs HTTP work off the main thread.
        private bool TrySendSync(LlmEndpointTarget target, string systemPrompt, string userPrompt, out string content, out string error)
        {
            content = null;
            error = null;

            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(target.Endpoint);
                webRequest.Method = "POST";
                webRequest.ContentType = "application/json";
                webRequest.Timeout = Math.Max(1, _config.AlternativeTimeoutSeconds) * 1000;
                if (!string.IsNullOrEmpty(target.ApiKey))
                {
                    webRequest.Headers[HttpRequestHeader.Authorization] = "Bearer " + target.ApiKey;
                }

                var bytes = Encoding.UTF8.GetBytes(BuildRequestBody(target, systemPrompt, userPrompt));
                webRequest.ContentLength = bytes.Length;
                using (var requestStream = webRequest.GetRequestStream())
                {
                    requestStream.Write(bytes, 0, bytes.Length);
                }

                using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                using (var responseStream = webResponse.GetResponseStream())
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var data = reader.ReadToEnd();
                    if (TryExtractContent(data, out content, out var parseError))
                    {
                        return true;
                    }

                    error = $"Endpoint '{target.Endpoint}' returned an unparsable response: {parseError}";
                    return false;
                }
            }
            catch (WebException ex)
            {
                string body = null;
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    using (var responseStream = errorResponse.GetResponseStream())
                    using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        body = reader.ReadToEnd();
                    }
                }

                error = body != null
                    ? $"Endpoint '{target.Endpoint}' failed: {ex.Message} - {body}"
                    : $"Endpoint '{target.Endpoint}' failed: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Endpoint '{target.Endpoint}' failed: {ex.Message}";
                return false;
            }
        }

        private bool TryExtractContent(string json, out string content, out string error)
        {
            content = null;
            error = null;

            try
            {
                var root = MiniJson.Parse(json) as Dictionary<string, object>;
                var choices = root?["choices"] as List<object>;
                var firstChoice = choices?[0] as Dictionary<string, object>;
                var message = firstChoice?["message"] as Dictionary<string, object>;
                var text = message?["content"] as string;

                if (string.IsNullOrEmpty(text))
                {
                    error = "response did not contain any message content";
                    return false;
                }

                content = text;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private string FinalizeContent(string content)
        {
            if (_config.StripReasoning)
            {
                content = ThinkTagRegex.Replace(content, string.Empty);
            }

            return content.Trim();
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
