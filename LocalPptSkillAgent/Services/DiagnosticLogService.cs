using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LocalPptSkillAgent.Utils;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    public static class DiagnosticLogService
    {
        private static readonly object Sync = new object();
        private static bool _cleaned;

        public static void Info(string eventName, string message = null) => Write("info", eventName, message, null);
        public static void Error(string eventName, Exception exception, string message = null) => Write("error", eventName, message, exception);

        public static string ReadRecent(int maxLines = 200)
        {
            try
            {
                var file = Directory.GetFiles(FileStoreUtils.Logs, "lpsa_*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (file == null) return "診断ログはまだありません。";
                return string.Join("\r\n", File.ReadLines(file).Reverse().Take(Math.Max(1, maxLines)).Reverse());
            }
            catch (Exception ex) { return "診断ログを読み込めません: " + ex.Message; }
        }

        private static void Write(string level, string eventName, string message, Exception exception)
        {
            try
            {
                lock (Sync)
                {
                    Cleanup();
                    var record = new JObject
                    {
                        ["timestamp"] = DateTimeOffset.Now.ToString("o"), ["level"] = level,
                        ["event"] = eventName ?? string.Empty, ["message"] = Redact(message),
                        ["exception_type"] = exception?.GetType().FullName, ["technical"] = Redact(exception?.ToString())
                    };
                    File.AppendAllText(Path.Combine(FileStoreUtils.Logs, "lpsa_" + DateTime.Now.ToString("yyyyMMdd") + ".jsonl"), record.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine);
                }
            }
            catch { }
        }

        private static void Cleanup()
        {
            if (_cleaned) return; _cleaned = true;
            foreach (var file in Directory.GetFiles(FileStoreUtils.Logs, "lpsa_*.jsonl"))
                try { if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-14)) File.Delete(file); } catch { }
        }

        private static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return Regex.Replace(value, @"(?i)(bearer\s+|api[_ -]?key[\""'=:\s]+|sk-lm-)[^\s\""',}]+", "$1***");
        }
    }
}
