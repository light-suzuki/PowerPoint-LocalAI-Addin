using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    public class SettingsService
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LocalPptSkillAgent.Settings.v1");
        public string SettingsPath { get; }

        public SettingsService(string settingsPath = null) { SettingsPath = settingsPath ?? FileStoreUtils.SettingsPath; }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return CreateDefaults();
            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonUtils.Deserialize<AppSettings>(json) ?? CreateDefaults();
                settings.ApiKey = Unprotect(settings.ProtectedApiKey);
                MigrateLegacyFields(settings, JObject.Parse(json));
                ApplyDefaults(settings);
                return settings;
            }
            catch { return CreateDefaults(); }
        }

        public void SaveSettings(AppSettings settings)
        {
            var errors = ValidateSettings(settings);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\r\n", errors));
            settings.ProtectedApiKey = Protect(settings.ApiKey);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonUtils.Serialize(settings));
        }

        public AppSettings ResetSettings()
        {
            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            return CreateDefaults();
        }

        public IReadOnlyList<string> ValidateSettings(AppSettings settings)
        {
            var errors = new List<string>();
            if (settings == null) { errors.Add("設定がありません。"); return errors; }
            if (!IsHttpUrl(settings.BaseUrl)) errors.Add("Base URLはhttp://またはhttps://で指定してください。");
            if (!IsHttpUrl(settings.ChatCompletionsEndpoint)) errors.Add("Chat endpointが正しくありません。");
            if (!IsHttpUrl(settings.NativeChatEndpoint)) errors.Add("Native MCP endpointが正しくありません。");
            if (settings.Temperature < 0 || settings.Temperature > 2) errors.Add("Temperatureは0〜2で指定してください。");
            if (settings.TopP <= 0 || settings.TopP > 1) errors.Add("Top Pは0より大きく1以下で指定してください。");
            if (settings.MaxTokens < 128) errors.Add("Max tokensは128以上で指定してください。");
            if (settings.ContextLength < 1024) errors.Add("Context lengthは1024以上で指定してください。");
            if (settings.VisionContextLength < 1024) errors.Add("Vision context lengthは1024以上で指定してください。");
            if (settings.EvalBatchSize < 32 || settings.EvalBatchSize > 8192) errors.Add("Eval batch sizeは32〜8192で指定してください。");
            if (settings.InputSafetyMarginTokens < 256) errors.Add("Input safety marginは256以上で指定してください。");
            if (settings.RecentChatMessages < 0 || settings.RecentChatMessages > 100) errors.Add("Recent chat messagesは0〜100で指定してください。");
            if (settings.RequestTimeoutSeconds < 15 || settings.RequestTimeoutSeconds > 1800) errors.Add("Request timeoutは15〜1800秒で指定してください。");
            if (settings.StructuredOutputRetryCount < 0 || settings.StructuredOutputRetryCount > 3) errors.Add("Structured output retryは0〜3で指定してください。");
            var reasoning = new[] { "none", "minimal", "low", "medium", "high", "xhigh" };
            if (!reasoning.Contains(settings.TextReasoningEffort ?? string.Empty, StringComparer.OrdinalIgnoreCase)) errors.Add("Text reasoning effortはnone/minimal/low/medium/high/xhighから選択してください。");
            if (settings.TextReasoningBudgetTokens < 128 || settings.TextReasoningBudgetTokens > 16384) errors.Add("Text reasoning budgetは128〜16384 tokensで指定してください。");
            if (!string.Equals(settings.TextReasoningEffort, "none", StringComparison.OrdinalIgnoreCase) && settings.MaxTokens < settings.TextReasoningBudgetTokens + 1024) errors.Add("Thinking使用時はMax tokensをReasoning budgetより1024以上大きくしてください。");
            if (settings.MaxTokens + settings.InputSafetyMarginTokens + 512 > settings.ContextLength) errors.Add("Context lengthにはMax tokens、safety margin、入力512 tokens以上を確保してください。");
            return errors;
        }

        public string MaskApiKeyForDisplay(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return string.Empty;
            if (apiKey.Length <= 8) return new string('*', apiKey.Length);
            return apiKey.Substring(0, 4) + new string('*', Math.Max(4, apiKey.Length - 8)) + apiKey.Substring(apiKey.Length - 4);
        }

        public LmStudioSettings ToLmStudioSettings(AppSettings settings) => new LmStudioSettings
        {
            BaseUrl = settings.BaseUrl,
            ChatEndpoint = settings.ChatCompletionsEndpoint,
            NativeChatEndpoint = settings.NativeChatEndpoint,
            TextModel = settings.TextModelName,
            VisionModel = settings.VisionModelName,
            ApiToken = settings.ApiKey,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            Seed = settings.Seed,
            MaxTokens = settings.MaxTokens,
            ContextLength = settings.ContextLength,
            VisionContextLength = settings.VisionContextLength,
            EvalBatchSize = settings.EvalBatchSize,
            FlashAttention = settings.FlashAttention,
            OffloadKvCacheToGpu = settings.OffloadKvCacheToGpu,
            InputSafetyMarginTokens = settings.InputSafetyMarginTokens,
            RecentChatMessages = settings.RecentChatMessages,
            RequestTimeoutSeconds = settings.RequestTimeoutSeconds,
            UseStructuredOutput = settings.UseStructuredOutput,
            StructuredOutputRetryCount = settings.StructuredOutputRetryCount,
            TextReasoningEffort = settings.TextReasoningEffort,
            TextReasoningBudgetTokens = settings.TextReasoningBudgetTokens,
            McpSearchIntegrationIds = settings.McpSearchIntegrationIds ?? new List<string>(),
            McpFetchIntegrationIds = settings.McpFetchIntegrationIds ?? new List<string>(),
            McpLocalDocsIntegrationIds = settings.McpLocalDocsIntegrationIds ?? new List<string>()
        };

        public void ApplyTo(AppSettings source, LmStudioSettings target)
        {
            var mapped = ToLmStudioSettings(source);
            target.BaseUrl = mapped.BaseUrl; target.ChatEndpoint = mapped.ChatEndpoint; target.NativeChatEndpoint = mapped.NativeChatEndpoint;
            target.TextModel = mapped.TextModel; target.VisionModel = mapped.VisionModel; target.ApiToken = mapped.ApiToken;
            target.Temperature = mapped.Temperature; target.MaxTokens = mapped.MaxTokens; target.ContextLength = mapped.ContextLength;
            target.VisionContextLength = mapped.VisionContextLength; target.EvalBatchSize = mapped.EvalBatchSize;
            target.FlashAttention = mapped.FlashAttention; target.OffloadKvCacheToGpu = mapped.OffloadKvCacheToGpu;
            target.TopP = mapped.TopP; target.Seed = mapped.Seed; target.InputSafetyMarginTokens = mapped.InputSafetyMarginTokens;
            target.RecentChatMessages = mapped.RecentChatMessages; target.RequestTimeoutSeconds = mapped.RequestTimeoutSeconds;
            target.UseStructuredOutput = mapped.UseStructuredOutput; target.StructuredOutputRetryCount = mapped.StructuredOutputRetryCount;
            target.TextReasoningEffort = mapped.TextReasoningEffort;
            target.TextReasoningBudgetTokens = mapped.TextReasoningBudgetTokens;
            target.McpSearchIntegrationIds = mapped.McpSearchIntegrationIds; target.McpFetchIntegrationIds = mapped.McpFetchIntegrationIds;
            target.McpLocalDocsIntegrationIds = mapped.McpLocalDocsIntegrationIds;
        }

        private static AppSettings CreateDefaults() { var settings = new AppSettings(); ToTemporary(settings); return settings; }
        private static LmStudioSettings ToTemporary(AppSettings settings)
        {
            var temp = new LmStudioSettings { McpSearchIntegrationIds = settings.McpSearchIntegrationIds, McpFetchIntegrationIds = settings.McpFetchIntegrationIds, McpLocalDocsIntegrationIds = settings.McpLocalDocsIntegrationIds };
            LmStudioMcpClient.ApplyDiscoveredDefaults(temp);
            settings.McpSearchIntegrationIds = temp.McpSearchIntegrationIds; settings.McpFetchIntegrationIds = temp.McpFetchIntegrationIds; settings.McpLocalDocsIntegrationIds = temp.McpLocalDocsIntegrationIds;
            return temp;
        }
        private static void ApplyDefaults(AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.BaseUrl)) s.BaseUrl = "http://localhost:1234/v1";
            if (string.IsNullOrWhiteSpace(s.ChatCompletionsEndpoint)) s.ChatCompletionsEndpoint = s.BaseUrl.TrimEnd('/') + "/chat/completions";
            if (string.IsNullOrWhiteSpace(s.NativeChatEndpoint)) s.NativeChatEndpoint = new Uri(s.BaseUrl).GetLeftPart(UriPartial.Authority) + "/api/v1/chat";
            if (s.ContextLength < 1024) s.ContextLength = 32768;
            if (s.VisionContextLength < 1024) s.VisionContextLength = 82778;
            if (s.EvalBatchSize < 32) s.EvalBatchSize = 2048;
            if (s.MaxTokens < 128) s.MaxTokens = 4096;
            if (s.TopP <= 0 || s.TopP > 1) s.TopP = 0.9;
            if (s.InputSafetyMarginTokens < 256) s.InputSafetyMarginTokens = 1024;
            if (s.RequestTimeoutSeconds < 15) s.RequestTimeoutSeconds = 180;
            if (string.IsNullOrWhiteSpace(s.TextReasoningEffort)) s.TextReasoningEffort = "low";
            if (s.TextReasoningBudgetTokens < 128) s.TextReasoningBudgetTokens = 1000;
            if (!string.Equals(s.TextReasoningEffort, "none", StringComparison.OrdinalIgnoreCase) && s.MaxTokens < s.TextReasoningBudgetTokens + 2048)
                s.MaxTokens = Math.Max(4096, s.TextReasoningBudgetTokens + 2048);
            ToTemporary(s);
        }
        private static void MigrateLegacyFields(AppSettings s, JObject root)
        {
            s.ChatCompletionsEndpoint = (string)root["ChatCompletionsEndpoint"] ?? (string)root["ChatEndpoint"] ?? s.ChatCompletionsEndpoint;
            s.TextModelName = (string)root["TextModelName"] ?? (string)root["TextModel"] ?? s.TextModelName;
            s.VisionModelName = (string)root["VisionModelName"] ?? (string)root["VisionModel"] ?? s.VisionModelName;
            s.ApiKey = s.ApiKey ?? (string)root["ApiKey"] ?? (string)root["ApiToken"];
            if (root["FlashAttention"] == null) s.FlashAttention = true;
            if (root["OffloadKvCacheToGpu"] == null) s.OffloadKvCacheToGpu = true;
        }
        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
        }
        private static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser)); }
            catch { return string.Empty; }
        }
        private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
