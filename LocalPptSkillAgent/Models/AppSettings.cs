using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class AppSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:1234/v1";
        public string ChatCompletionsEndpoint { get; set; } = "http://localhost:1234/v1/chat/completions";
        public string NativeChatEndpoint { get; set; } = "http://localhost:1234/api/v1/chat";
        public string TextModelName { get; set; }
        public string VisionModelName { get; set; }
        [JsonIgnore]
        public string ApiKey { get; set; }
        public string ProtectedApiKey { get; set; }
        public double Temperature { get; set; } = 0.2;
        public double TopP { get; set; } = 0.9;
        public int Seed { get; set; } = -1;
        public int MaxTokens { get; set; } = 4096;
        public int ContextLength { get; set; } = 32768;
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
        public string ApiMode { get; set; } = "openai_compatible";
        public string VisionInputMode { get; set; } = "image_url_base64";
        public string LastSkillMode { get; set; } = "auto";
        // 既定はキーワードによる高速・確実な意図判定。有効にすると、キーワードで判別できない曖昧な発話だけ
        // LLMに意図分類を委ねる(ローカル小型モデルでは1回あたり数十秒かかるため既定は無効)。
        public bool UseLlmRouter { get; set; } = false;
        public List<string> McpSearchIntegrationIds { get; set; } = new List<string>();
        public List<string> McpFetchIntegrationIds { get; set; } = new List<string>();
        public List<string> McpLocalDocsIntegrationIds { get; set; } = new List<string>();
    }
}
