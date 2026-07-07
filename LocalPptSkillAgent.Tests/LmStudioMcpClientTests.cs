using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalPptSkillAgent.Services;
using Xunit;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Tests
{
    public class LmStudioMcpClientTests
    {
        [Fact]
        public void DiscoversEnabledLmStudioServers()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "{\"mcpServers\":{\"S_web\":{\"command\":\"npx\"},\"disabled\":{\"disabled\":true}}}");
                var ids = LmStudioMcpClient.DiscoverServerIds(path);
                Assert.Contains("mcp/S_web", ids);
                Assert.DoesNotContain("mcp/disabled", ids);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task NativeChatRequiresAndReturnsToolEvidence()
        {
            var handler = new StubHandler();
            var client = new LmStudioClient(new LmStudioSettings
            {
                TextModel = "test-model",
                NativeChatEndpoint = "http://localhost:1234/api/v1/chat"
            }, handler);
            var result = await client.ChatWithMcpAsync("search", new[] { "mcp/S_web" });
            Assert.Contains("web_search", result);
            Assert.Contains("mcp/S_web", handler.RequestBody);
        }

        [Fact]
        public async Task ModelListUsesConfiguredBearerToken()
        {
            var handler = new AuthHandler();
            var client = new LmStudioClient(new LmStudioSettings { BaseUrl = "http://localhost:12345/v1", ApiToken = "secret" }, handler);
            var models = await client.GetModelsAsync();
            Assert.Equal("secret", handler.Token);
            Assert.Contains("model-a", models);
        }

        [Fact]
        public async Task TextChatSendsConfiguredReasoningEffort()
        {
            var handler = new ChatHandler("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"ok\",\"reasoning_content\":\"\"}}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":1,\"total_tokens\":3}}");
            using (var client = new LmStudioClient(new LmStudioSettings
            {
                TextModel = "test-model", ChatEndpoint = "http://localhost/v1/chat/completions", TextReasoningEffort = "low", TextReasoningBudgetTokens = 1000, MaxTokens = 128
            }, handler))
            {
                Assert.Equal("ok", await client.ChatAsync("system", "user"));
                var request = JObject.Parse(handler.RequestBody);
                Assert.Equal("low", (string)request["reasoning_effort"]);
                Assert.Equal(3048, (int)request["max_tokens"]);
                Assert.StartsWith("内部検討は1000 token以内", (string)request["messages"]?[0]?["content"]);
                Assert.Equal("stop", client.LastFinishReason);
                Assert.Equal(0, client.LastReasoningContentLength);
            }
        }

        [Fact]
        public async Task EmptyTextResponseReportsReasoningDiagnostics()
        {
            var handler = new ChatHandler("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"\",\"reasoning_content\":\"thinking\"}}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":128,\"total_tokens\":130}}");
            using (var client = new LmStudioClient(new LmStudioSettings
            {
                TextModel = "test-model", ChatEndpoint = "http://localhost/v1/chat/completions", TextReasoningEffort = "high"
            }, handler))
            {
                var error = await Assert.ThrowsAsync<InvalidDataException>(() => client.ChatAsync("system", "user"));
                Assert.Contains("finish_reason=length", error.Message);
                Assert.Contains("reasoning_chars=8", error.Message);
            }
        }

        [Fact]
        public async Task VisionLoadUsesVisionGpuPreset()
        {
            var handler = new ChatHandler("{\"status\":\"loaded\"}");
            using (var client = new LmStudioClient(new LmStudioSettings
            {
                BaseUrl = "http://localhost:1234/v1", VisionContextLength = 82778, EvalBatchSize = 2048,
                FlashAttention = true, OffloadKvCacheToGpu = true
            }, handler))
            {
                await client.TryLoadModelAsync("vision-model", true);
                var request = JObject.Parse(handler.RequestBody);
                Assert.Equal(82778, (int)request["context_length"]);
                Assert.Equal(2048, (int)request["eval_batch_size"]);
                Assert.True((bool)request["flash_attention"]);
                Assert.True((bool)request["offload_kv_cache_to_gpu"]);
            }
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            public string RequestBody { get; private set; }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestBody = await request.Content.ReadAsStringAsync();
                var json = "{\"response_id\":\"r1\",\"output\":[{\"type\":\"tool_call\",\"tool\":\"web_search\",\"arguments\":{\"q\":\"x\"},\"output\":\"evidence\",\"provider_info\":{\"plugin_id\":\"mcp/S_web\"}},{\"type\":\"message\",\"content\":\"done\"}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class AuthHandler : HttpMessageHandler
        {
            public string Token { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Token = request.Headers.Authorization?.Parameter;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[{\"id\":\"model-a\"}]}", Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class ChatHandler : HttpMessageHandler
        {
            private readonly string _response;
            public string RequestBody { get; private set; }
            public ChatHandler(string response) { _response = response; }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestBody = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_response, Encoding.UTF8, "application/json") };
            }
        }
    }
}
