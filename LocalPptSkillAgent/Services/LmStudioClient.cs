using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class LmStudioSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:1234/v1";
        public string ChatEndpoint { get; set; } = "http://localhost:1234/v1/chat/completions";
        public string NativeChatEndpoint { get; set; } = "http://localhost:1234/api/v1/chat";
        [JsonIgnore]
        public string ApiToken { get; set; }
        public string TextModel { get; set; }
        public string VisionModel { get; set; }
        public double Temperature { get; set; } = 0.2;
        public double TopP { get; set; } = 0.9;
        public int Seed { get; set; } = -1;
        public int MaxTokens { get; set; } = 4096;
        public int ContextLength { get; set; } = 8192;
        public int VisionContextLength { get; set; } = 82778;
        public int EvalBatchSize { get; set; } = 2048;
        public bool FlashAttention { get; set; } = true;
        public bool OffloadKvCacheToGpu { get; set; } = true;
        public int InputSafetyMarginTokens { get; set; } = 1024;
        public int RecentChatMessages { get; set; } = 12;
        public int RequestTimeoutSeconds { get; set; } = 180;
        public bool UseStructuredOutput { get; set; } = true;
        public int StructuredOutputRetryCount { get; set; } = 1;
        public string TextReasoningEffort { get; set; } = "low";
        public int TextReasoningBudgetTokens { get; set; } = 1000;
        public List<string> McpSearchIntegrationIds { get; set; } = new List<string>();
        public List<string> McpFetchIntegrationIds { get; set; } = new List<string>();
        public List<string> McpLocalDocsIntegrationIds { get; set; } = new List<string>();
    }

    public class LmStudioUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
    public class LmStudioModelInfo
    {
        public string Key { get; set; }
        public bool Vision { get; set; }
        public bool TrainedForToolUse { get; set; }
        public int MaxContextLength { get; set; }
        public int? LoadedContextLength { get; set; }
        public bool IsLoaded => LoadedContextLength.HasValue;
    }

    public interface IVisionClient
    {
        Task<string> AnalyzeSlideAsync(string systemPrompt, string userPrompt, string imagePath, string shapeJson);
    }

    public class LmStudioClient : IVisionClient, IDisposable
    {
        private readonly HttpClient _http;
        public LmStudioSettings Settings { get; }
        public LmStudioUsage LastUsage { get; private set; }
        public string LastFinishReason { get; private set; }
        public int LastReasoningContentLength { get; private set; }

        public LmStudioClient(LmStudioSettings settings = null, HttpMessageHandler handler = null)
        {
            Settings = settings ?? new LmStudioSettings();
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = Timeout.InfiniteTimeSpan;
        }

        public async Task<string> CheckConnectionAsync()
        {
            var models = await GetModelsAsync().ConfigureAwait(false);
            return $"接続成功: {models.Count} model(s)";
        }

        public async Task<IReadOnlyList<string>> GetModelsAsync()
        {
            using (var request = CreateRequest(HttpMethod.Get, Settings.BaseUrl.TrimEnd('/') + "/models"))
            using (var response = await SendAsync(request, CancellationToken.None).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(json)["data"]?.Select(x => (string)x["id"]).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
            }
        }

        public async Task<IReadOnlyList<LmStudioModelInfo>> GetRuntimeModelsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var baseUri = new Uri(Settings.BaseUrl); var rootUrl = baseUri.GetLeftPart(UriPartial.Authority);
            using (var request = CreateRequest(HttpMethod.Get, rootUrl + "/api/v1/models"))
            using (var response = await SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false); response.EnsureSuccessStatusCode();
                var models = JObject.Parse(json)["models"] as JArray ?? new JArray();
                return models.Select(model => new LmStudioModelInfo
                {
                    Key = (string)model["key"], Vision = (bool?)model["capabilities"]?["vision"] ?? false,
                    TrainedForToolUse = (bool?)model["capabilities"]?["trained_for_tool_use"] ?? false,
                    MaxContextLength = (int?)model["max_context_length"] ?? 0,
                    LoadedContextLength = (int?)model["loaded_instances"]?.FirstOrDefault()?["config"]?["context_length"]
                }).ToList();
            }
        }

        public async Task<string> CheckSelectedModelCompatibilityAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var models = await GetRuntimeModelsAsync(cancellationToken).ConfigureAwait(false);
            var text = models.FirstOrDefault(x => MatchModel(x.Key, Settings.TextModel));
            var vision = models.FirstOrDefault(x => MatchModel(x.Key, Settings.VisionModel));
            var lines = new List<string>();
            if (text == null) lines.Add("Textモデル: 一覧にありません");
            else
            {
                lines.Add($"Textモデル: {(text.IsLoaded ? "ロード済み" : "未ロード/JIT待ち")}、最大context {text.MaxContextLength:N0}");
                if (text.MaxContextLength > 0 && Settings.ContextLength > text.MaxContextLength) lines.Add("警告: 設定Context lengthがTextモデル上限を超えています。");
                if (text.LoadedContextLength.HasValue && Settings.ContextLength > text.LoadedContextLength.Value) lines.Add($"警告: Textモデルは{ text.LoadedContextLength.Value:N0} contextでロードされています。再ロードしてください。");
            }
            if (vision == null) lines.Add("Visionモデル: 一覧にありません");
            else
            {
                lines.Add($"Visionモデル: {(vision.IsLoaded ? "ロード済み" : "未ロード/JIT待ち")}、Vision capability={(vision.Vision ? "あり" : "なし")}");
                if (!vision.Vision) lines.Add("警告: 選択モデルはVision対応として報告されていません。");
                if (vision.LoadedContextLength.HasValue && Settings.VisionContextLength > vision.LoadedContextLength.Value) lines.Add($"警告: Visionモデルは{vision.LoadedContextLength.Value:N0} contextでロードされています。再ロードしてください。");
            }
            return string.Join("\r\n", lines);
        }

        public Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendChatAsync(Settings.TextModel, new JArray(
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = userPrompt }), cancellationToken, null, true);

        public Task<string> ChatStructuredAsync(string systemPrompt, string userPrompt, JObject schema, string schemaName, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendChatAsync(Settings.TextModel, new JArray(
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = userPrompt }), cancellationToken, ResponseFormat(schema, schemaName), true);

        public Task<string> ChatConversationAsync(JArray messages, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendChatAsync(Settings.TextModel, messages, cancellationToken, null, true);

        public Task<string> ChatConversationStructuredAsync(JArray messages, JObject schema, string schemaName, CancellationToken cancellationToken = default(CancellationToken)) =>
            SendChatAsync(Settings.TextModel, messages, cancellationToken, ResponseFormat(schema, schemaName), true);

        public async Task<string> TestStructuredOutputAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var schema = JObject.Parse(@"{'type':'object','properties':{'status':{'type':'string'}},'required':['status']}");
            var response = await ChatStructuredAsync("JSON schemaに従う。", "statusをokにする。", schema, "connection_test", cancellationToken).ConfigureAwait(false);
            var status = (string)JObject.Parse(JsonUtils.ExtractJson(response))["status"];
            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Structured output testのstatusがokではありません。");
            return "Structured output成功";
        }

        public async Task<string> AnalyzeSlideAsync(string systemPrompt, string userPrompt, string imagePath, string shapeJson)
            => await AnalyzeSlideAsync(systemPrompt, userPrompt, imagePath, shapeJson, CancellationToken.None).ConfigureAwait(false);

        public async Task<string> AnalyzeSlideAsync(string systemPrompt, string userPrompt, string imagePath, string shapeJson, CancellationToken cancellationToken)
        {
            var data = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            var content = new JArray(
                new JObject { ["type"] = "text", ["text"] = userPrompt + "\nShape JSON:\n" + shapeJson },
                new JObject { ["type"] = "image_url", ["image_url"] = new JObject { ["url"] = "data:image/png;base64," + data } });
            return await SendChatAsync(Settings.VisionModel, new JArray(
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = content }), cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> AnalyzeSlideStructuredAsync(string systemPrompt, string userPrompt, string imagePath, string shapeJson, JObject schema, string schemaName, CancellationToken cancellationToken)
        {
            var data = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            var content = new JArray(
                new JObject { ["type"] = "text", ["text"] = userPrompt + "\nShape JSON:\n" + shapeJson },
                new JObject { ["type"] = "image_url", ["image_url"] = new JObject { ["url"] = "data:image/png;base64," + data } });
            return await SendChatAsync(Settings.VisionModel, new JArray(
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = content }), cancellationToken, ResponseFormat(schema, schemaName)).ConfigureAwait(false);
        }

        public async Task<string> ChatWithMcpAsync(string input, IEnumerable<string> integrationIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(Settings.TextModel)) throw new InvalidOperationException("Textモデルを選択してください。");
            var integrations = new JArray((integrationIds ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
            if (integrations.Count == 0) return "MCP_NOT_CONFIGURED: LM Studio MCP integration IDがありません。";
            var request = new JObject
            {
                ["model"] = Settings.TextModel,
                ["input"] = input,
                ["integrations"] = integrations,
                ["context_length"] = Settings.ContextLength,
                ["temperature"] = 0,
                ["store"] = false
            };
            using (var message = new HttpRequestMessage(HttpMethod.Post, Settings.NativeChatEndpoint))
            {
                message.Content = Json(request);
                if (!string.IsNullOrWhiteSpace(Settings.ApiToken)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiToken);
                using (var response = await SendAsync(message, cancellationToken).ConfigureAwait(false))
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) throw new HttpRequestException($"LM Studio MCP API {(int)response.StatusCode}: {json}");
                    var root = JObject.Parse(json);
                    LastUsage = new LmStudioUsage
                    {
                        PromptTokens = (int?)root["stats"]?["input_tokens"] ?? 0,
                        CompletionTokens = (int?)root["stats"]?["total_output_tokens"] ?? 0,
                        TotalTokens = ((int?)root["stats"]?["input_tokens"] ?? 0) + ((int?)root["stats"]?["total_output_tokens"] ?? 0)
                    };
                    var toolCalls = root["output"]?.Where(x => (string)x["type"] == "tool_call").ToList() ?? new List<JToken>();
                    if (toolCalls.Count == 0) return "MCP_NO_TOOL_RESULT: LM StudioモデルがMCP toolを呼び出しませんでした。";
                    var messages = root["output"]?.Where(x => (string)x["type"] == "message").Select(x => x["content"]).ToList() ?? new List<JToken>();
                    return new JObject
                    {
                        ["response_id"] = root["response_id"],
                        ["messages"] = new JArray(messages),
                        ["tool_calls"] = new JArray(toolCalls.Select(x => new JObject
                        {
                            ["tool"] = x["tool"], ["arguments"] = x["arguments"], ["output"] = x["output"], ["provider_info"] = x["provider_info"]
                        }))
                    }.ToString(Newtonsoft.Json.Formatting.Indented);
                }
            }
        }

        public Task<string> TryLoadModelAsync(string model) => TryLoadModelAsync(model, false);

        public async Task<string> TryLoadModelAsync(string model, bool visionModel)
        {
            return await TryManagementAsync(new[] { "/api/v1/models/load", "/models/load", "/api/v0/models/load" }, new JObject
            {
                ["model"] = model,
                ["context_length"] = visionModel ? Settings.VisionContextLength : Settings.ContextLength,
                ["eval_batch_size"] = Settings.EvalBatchSize,
                ["flash_attention"] = Settings.FlashAttention,
                ["offload_kv_cache_to_gpu"] = Settings.OffloadKvCacheToGpu,
                ["echo_load_config"] = true
            }).ConfigureAwait(false);
        }

        public async Task<string> TryUnloadModelAsync(string model)
        {
            return await TryManagementAsync(new[] { "/api/v1/models/unload", "/models/unload", "/api/v0/models/unload" }, new JObject { ["model"] = model }).ConfigureAwait(false);
        }

        private async Task<string> TryManagementAsync(IEnumerable<string> paths, JObject body)
        {
            foreach (var path in paths)
            {
                try
                {
                    var baseUri = new Uri(Settings.BaseUrl);
                    var root = baseUri.GetLeftPart(UriPartial.Authority);
                    using (var request = CreateRequest(HttpMethod.Post, root + path, Json(body)))
                    using (var response = await SendAsync(request, CancellationToken.None).ConfigureAwait(false))
                        if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (HttpRequestException) { }
            }
            return "モデル管理APIが未対応です。LM Studio側で手動ロードしてください。";
        }

        private async Task<string> SendChatAsync(string model, JArray messages, CancellationToken cancellationToken, JObject responseFormat = null, bool isTextModel = false)
        {
            if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("モデルを選択してください。");
            var reasoningEnabled = isTextModel && !string.Equals(Settings.TextReasoningEffort, "none", StringComparison.OrdinalIgnoreCase);
            if (reasoningEnabled)
            {
                var system = messages.FirstOrDefault(x => string.Equals((string)x["role"], "system", StringComparison.OrdinalIgnoreCase));
                if (system != null)
                    system["content"] = $"内部検討は{Math.Max(128, Settings.TextReasoningBudgetTokens)} token以内で簡潔に完了し、必ず最終回答を返す。\n" + ((string)system["content"] ?? string.Empty);
            }
            var effectiveMaxTokens = reasoningEnabled
                ? Math.Max(Settings.MaxTokens, Math.Max(128, Settings.TextReasoningBudgetTokens) + 2048)
                : Settings.MaxTokens;
            var request = new JObject
            {
                ["model"] = model, ["messages"] = messages, ["temperature"] = Settings.Temperature,
                ["top_p"] = Settings.TopP, ["max_tokens"] = effectiveMaxTokens, ["stream"] = false
            };
            if (Settings.Seed >= 0) request["seed"] = Settings.Seed;
            if (isTextModel && !string.IsNullOrWhiteSpace(Settings.TextReasoningEffort))
                request["reasoning_effort"] = Settings.TextReasoningEffort;
            if (Settings.UseStructuredOutput && responseFormat != null) request["response_format"] = responseFormat;
            using (var message = CreateRequest(HttpMethod.Post, Settings.ChatEndpoint, Json(request)))
            using (var response = await SendAsync(message, cancellationToken).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"LM Studio {(int)response.StatusCode}: {json}");
                var root = JObject.Parse(json);
                LastUsage = new LmStudioUsage
                {
                    PromptTokens = (int?)root["usage"]?["prompt_tokens"] ?? 0,
                    CompletionTokens = (int?)root["usage"]?["completion_tokens"] ?? 0,
                    TotalTokens = (int?)root["usage"]?["total_tokens"] ?? 0
                };
                var choice = root["choices"]?[0];
                var responseMessage = choice?["message"];
                LastFinishReason = (string)choice?["finish_reason"];
                LastReasoningContentLength = ((string)responseMessage?["reasoning_content"] ?? string.Empty).Length;
                var content = StripThinkBlocks((string)responseMessage?["content"], out var inlineReasoningChars);
                LastReasoningContentLength += inlineReasoningChars;
                if (string.IsNullOrWhiteSpace(content))
                    throw new InvalidDataException($"LM Studioが本文を返しませんでした。finish_reason={LastFinishReason ?? "unknown"}, reasoning_chars={LastReasoningContentLength}, completion_tokens={LastUsage.CompletionTokens}。Text reasoning effortをlowにするか、Reasoning budget/Max tokensを調整してください。");
                return content;
            }
        }

        public static string StripThinkBlocks(string content, out int reasoningChars)
        {
            reasoningChars = 0;
            if (string.IsNullOrEmpty(content)) return content;
            var result = content;
            while (true)
            {
                var start = result.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;
                var end = result.IndexOf("</think>", start, StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                {
                    // 閉じタグなし: <think>以降すべてが内部検討とみなし切り捨てる
                    reasoningChars += result.Length - start;
                    result = result.Substring(0, start);
                    break;
                }
                reasoningChars += end + "</think>".Length - start;
                result = result.Remove(start, end + "</think>".Length - start);
            }
            return result.Trim();
        }

        private static JObject ResponseFormat(JObject schema, string name) => new JObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JObject { ["name"] = string.IsNullOrWhiteSpace(name) ? "lpsa_result" : name, ["strict"] = false, ["schema"] = schema }
        };
        private static bool MatchModel(string key, string selected) => !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(selected) &&
            (string.Equals(key, selected, StringComparison.OrdinalIgnoreCase) || selected.StartsWith(key, StringComparison.OrdinalIgnoreCase) || key.StartsWith(selected, StringComparison.OrdinalIgnoreCase));

        private static StringContent Json(JObject body) => new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        private HttpRequestMessage CreateRequest(HttpMethod method, string url, HttpContent content = null)
        {
            var request = new HttpRequestMessage(method, url) { Content = content };
            if (!string.IsNullOrWhiteSpace(Settings.ApiToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiToken);
            return request;
        }
        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, Settings.RequestTimeoutSeconds)));
                try { return await _http.SendAsync(request, timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"LM Studioの応答が{Math.Max(15, Settings.RequestTimeoutSeconds)}秒以内に完了しませんでした。", ex);
                }
            }
        }
        public void Dispose() => _http.Dispose();
    }
}
