using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    public class LlmTranslateOfflineEndpoint : HttpEndpoint
    {
        private static readonly Regex ThinkTagRegex = new Regex("<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Short TCP-connect timeout used only to pick which target OnCreateRequest hands to
        // the framework — not the real request timeout (see AlternativeTimeoutSeconds).
        private const int PreflightTimeoutMs = 800;

        private LlmConfig _config;

        // The target actually handed to the framework for the request currently in flight.
        // Safe as instance state: MaxConcurrency is 1, so only one request is ever in flight.
        private LlmEndpointTarget _attemptedTarget;

        public override string Id => "LlmTranslateOffline";

        public override string FriendlyName => "LLM Translate Offline (LM Studio / Ollama)";

        public override int MaxConcurrency => 1;

        public override void Initialize(IInitializationContext context)
        {
            WorkAroundMonoWineProxyBug();

            var path = LlmConfig.ResolvePath(context.TranslatorDirectory);
            _config = LlmConfig.LoadOrCreate(path);

            var version = GetType().Assembly.GetName().Version;
            XuaLogger.AutoTranslator.Info(
                $"LlmTranslateOffline v{version} loaded. Primary endpoint: {_config.Endpoint} (model: {_config.Model}). " +
                $"Alternative endpoints configured: {_config.Alternatives.Count}.");
        }

        // Under Mono-on-Wine, the very first WebRequest/WebClient call in the process lazily
        // triggers WebRequest.InternalDefaultWebProxy, which tries to read proxy settings from
        // the (nonexistent) Windows registry and throws a NullReferenceException from
        // AutoWebProxyScriptEngine.InitializeRegistryGlobalProxy. That crash happens inside
        // XUnity.AutoTranslator's own request pipeline too, before our endpoint even gets a
        // chance to fail over to an alternative. Explicitly assigning DefaultWebProxy short-
        // circuits that lazy lookup for the whole process, fixing both the framework's request
        // and our own HttpWebRequest calls in TrySendSync.
        private static void WorkAroundMonoWineProxyBug()
        {
            try
            {
                WebRequest.DefaultWebProxy = null;
            }
            catch
            {
                // Best-effort only; if this host doesn't have the bug (or doesn't allow the
                // assignment), just fall through and let requests behave as they normally would.
            }
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var (systemPrompt, userPrompt) = BuildPrompts(context);

            // A connection-level failure (refused/unreachable) throws inside the framework's
            // own request pipeline, before OnExtractTranslation is ever called — so the
            // alternative-endpoint failover below never gets a chance to run for that case.
            // Picking a reachable target up front (a cheap TCP probe, not a full request)
            // covers that gap; OnExtractTranslation still handles "got a response but it's bad".
            _attemptedTarget = ChooseReachableTarget();

            context.Complete(BuildRequest(_attemptedTarget, systemPrompt, userPrompt));
        }

        private LlmEndpointTarget ChooseReachableTarget()
        {
            var primary = new LlmEndpointTarget { Endpoint = _config.Endpoint, ApiKey = _config.ApiKey, Model = _config.Model };
            if (IsTcpReachable(primary.Endpoint))
            {
                return primary;
            }

            foreach (var alternative in _config.Alternatives)
            {
                if (IsTcpReachable(alternative.Endpoint))
                {
                    return alternative;
                }
            }

            // Nothing reachable: use primary anyway so the normal error-reporting path below
            // still produces a clean, aggregated failure instead of no target at all.
            return primary;
        }

        private static bool IsTcpReachable(string url)
        {
            try
            {
                var uri = new Uri(url);
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(uri.Host, uri.Port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(PreflightTimeoutMs))
                    {
                        return false;
                    }

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
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

            var attemptedError = response.Code == HttpStatusCode.OK
                ? $"Endpoint '{_attemptedTarget.Endpoint}' returned an unparsable response: {parseError}"
                : $"Endpoint '{_attemptedTarget.Endpoint}' returned HTTP {(int)response.Code}: {response.Data}";

            var remaining = RemainingCandidates();
            if (remaining.Count == 0)
            {
                context.Fail(attemptedError, null);
                return;
            }

            var (systemPrompt, userPrompt) = BuildPrompts(context);
            var errors = new List<string> { attemptedError };

            foreach (var candidate in remaining)
            {
                if (TrySendSync(candidate, systemPrompt, userPrompt, out var candidateContent, out var candidateError))
                {
                    context.Complete(FinalizeContent(candidateContent));
                    return;
                }

                errors.Add(candidateError);
            }

            context.Fail("All LLM endpoints failed:\n" + string.Join("\n", errors), null);
        }

        // Every configured target except whichever one OnCreateRequest already attempted
        // (ChooseReachableTarget may have already picked an alternative as the effective
        // primary, so this isn't always just "all the alternatives").
        private List<LlmEndpointTarget> RemainingCandidates()
        {
            var candidates = new List<LlmEndpointTarget>();

            var configuredPrimary = new LlmEndpointTarget { Endpoint = _config.Endpoint, ApiKey = _config.ApiKey, Model = _config.Model };
            if (!SameEndpoint(configuredPrimary, _attemptedTarget))
            {
                candidates.Add(configuredPrimary);
            }

            foreach (var alternative in _config.Alternatives)
            {
                if (!SameEndpoint(alternative, _attemptedTarget))
                {
                    candidates.Add(alternative);
                }
            }

            return candidates;
        }

        private static bool SameEndpoint(LlmEndpointTarget a, LlmEndpointTarget b)
        {
            return string.Equals(a.Endpoint, b.Endpoint, StringComparison.OrdinalIgnoreCase);
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
