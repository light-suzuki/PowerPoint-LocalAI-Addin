using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    public class LmStudioMcpClient : IMcpClient
    {
        private readonly LmStudioClient _client;

        public LmStudioMcpClient(LmStudioClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            ApplyDiscoveredDefaults(_client.Settings);
        }

        public Task<string> SearchWebAsync(string query) => Search(
            "Web検索MCPを必ず1回以上呼び出し、主張検証に使える結果だけをURL・タイトル・抜粋付きで返してください。検索せず知識だけで回答しないでください。\nQuery: " + query,
            _client.Settings.McpSearchIntegrationIds);

        public Task<string> SearchPapersAsync(string query) => Search(
            "検索MCPを必ず使い、査読論文、DOI、PubMed、出版社ページ、大学リポジトリを優先して探してください。論文として確認できない結果は区別してください。\nScholarly query: " + query,
            _client.Settings.McpSearchIntegrationIds);

        public Task<string> SearchLocalDocsAsync(string query) => Search(
            "ローカル文書検索MCPを必ず使い、指定主張と直接関連する文書断片とファイル識別子だけを返してください。見つからなければ見つからないと返してください。\nQuery: " + query,
            _client.Settings.McpLocalDocsIntegrationIds);

        public Task<string> FetchUrlAsync(string url) => Search(
            "取得MCPを必ず使ってURL本文を取得し、主張検証に関係する箇所、ページタイトル、URLを返してください。\nURL: " + url,
            _client.Settings.McpFetchIntegrationIds);

        public Task<string> FetchPdfTextAsync(string pathOrUrl) => Search(
            "取得MCPまたはローカル文書MCPを必ず使ってPDF本文を取得し、関連箇所とページ情報を返してください。\nPDF: " + pathOrUrl,
            _client.Settings.McpFetchIntegrationIds.Concat(_client.Settings.McpLocalDocsIntegrationIds));

        public Task<string> SearchBibliographyAsync(string query) => Search(
            "検索MCPを必ず使い、引用候補の著者、年、題名、雑誌名、DOIまたは恒久URLを返してください。不明な書誌情報を補完しないでください。\nBibliography query: " + query,
            _client.Settings.McpSearchIntegrationIds);

        private Task<string> Search(string prompt, IEnumerable<string> integrations) => _client.ChatWithMcpAsync(prompt, integrations);

        public static void ApplyDiscoveredDefaults(LmStudioSettings settings)
        {
            var names = DiscoverServerIds();
            if (settings.McpSearchIntegrationIds == null || settings.McpSearchIntegrationIds.Count == 0)
                settings.McpSearchIntegrationIds = names.Where(x => IsSearch(x)).ToList();
            if (settings.McpFetchIntegrationIds == null || settings.McpFetchIntegrationIds.Count == 0)
                settings.McpFetchIntegrationIds = names.Where(x => IsFetch(x)).ToList();
            if (settings.McpLocalDocsIntegrationIds == null || settings.McpLocalDocsIntegrationIds.Count == 0)
                settings.McpLocalDocsIntegrationIds = names.Where(x => x.IndexOf("filesystem", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        public static IReadOnlyList<string> DiscoverServerIds(string path = null)
        {
            path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lmstudio", "mcp.json");
            if (!File.Exists(path)) return new List<string>();
            try
            {
                var servers = JObject.Parse(File.ReadAllText(path))["mcpServers"] as JObject;
                return servers?.Properties()
                    .Where(x => x.Value["disabled"] == null || x.Value.Value<bool?>("disabled") != true)
                    .Select(x => "mcp/" + x.Name).ToList() ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        private static bool IsSearch(string id) => id.StartsWith("mcp/S_", StringComparison.OrdinalIgnoreCase);
        private static bool IsFetch(string id) => id.StartsWith("mcp/E_fetch", StringComparison.OrdinalIgnoreCase) || id.StartsWith("mcp/E_jina", StringComparison.OrdinalIgnoreCase);
    }
}
