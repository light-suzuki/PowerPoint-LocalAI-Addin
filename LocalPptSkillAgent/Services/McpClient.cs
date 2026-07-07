using System;
using System.Threading.Tasks;

namespace LocalPptSkillAgent.Services
{
    public interface IMcpClient
    {
        Task<string> SearchWebAsync(string query);
        Task<string> SearchPapersAsync(string query);
        Task<string> SearchLocalDocsAsync(string query);
        Task<string> FetchUrlAsync(string url);
        Task<string> FetchPdfTextAsync(string pathOrUrl);
        Task<string> SearchBibliographyAsync(string query);
    }

    public class MockMcpClient : IMcpClient
    {
        private static Task<string> Missing(string operation) => Task.FromResult($"MCP_NOT_CONFIGURED: {operation}");
        public Task<string> SearchWebAsync(string query) => Missing("search_web " + query);
        public Task<string> SearchPapersAsync(string query) => Missing("search_papers " + query);
        public Task<string> SearchLocalDocsAsync(string query) => Missing("search_local_docs " + query);
        public Task<string> FetchUrlAsync(string url) => Missing("fetch_url " + url);
        public Task<string> FetchPdfTextAsync(string pathOrUrl) => Missing("fetch_pdf_text " + pathOrUrl);
        public Task<string> SearchBibliographyAsync(string query) => Missing("search_bibliography " + query);
    }

    public class DelegateMcpClient : IMcpClient
    {
        public Func<string, Task<string>> SearchWeb { get; set; }
        public Func<string, Task<string>> SearchPapers { get; set; }
        public Func<string, Task<string>> SearchLocalDocs { get; set; }
        public Func<string, Task<string>> FetchUrl { get; set; }
        public Func<string, Task<string>> FetchPdfText { get; set; }
        public Func<string, Task<string>> SearchBibliography { get; set; }
        private static Task<string> Call(Func<string, Task<string>> fn, string arg) => fn == null ? Task.FromResult("MCP_NOT_CONFIGURED") : fn(arg);
        public Task<string> SearchWebAsync(string query) => Call(SearchWeb, query);
        public Task<string> SearchPapersAsync(string query) => Call(SearchPapers, query);
        public Task<string> SearchLocalDocsAsync(string query) => Call(SearchLocalDocs, query);
        public Task<string> FetchUrlAsync(string url) => Call(FetchUrl, url);
        public Task<string> FetchPdfTextAsync(string pathOrUrl) => Call(FetchPdfText, pathOrUrl);
        public Task<string> SearchBibliographyAsync(string query) => Call(SearchBibliography, query);
    }
}
