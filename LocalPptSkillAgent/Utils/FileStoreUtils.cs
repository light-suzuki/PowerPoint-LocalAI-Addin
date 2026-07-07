using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Utils
{
    public static class FileStoreUtils
    {
        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalPptSkillAgent");
        public static string Exports => Ensure(Path.Combine(Root, "exports"));
        public static string Sessions => Ensure(Path.Combine(Root, "sessions"));
        public static string Chats => Ensure(Path.Combine(Root, "chats"));
        public static string Logs => Ensure(Path.Combine(Root, "logs"));
        public static string SettingsPath => Path.Combine(Ensure(Root), "settings.json");

        public static string SaveSession(AiEditSession session)
        {
            var path = Path.Combine(Sessions, "session_" + session.SessionId + ".json");
            File.WriteAllText(path, JsonUtils.Serialize(session));
            return path;
        }

        public static AiEditSession LoadSession(string id) =>
            JsonUtils.Deserialize<AiEditSession>(File.ReadAllText(Path.Combine(Sessions, "session_" + id + ".json")));

        public static IReadOnlyList<AiEditSession> ListSessions() => Directory.GetFiles(Sessions, "session_*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(path => { try { return JsonUtils.Deserialize<AiEditSession>(File.ReadAllText(path)); } catch { return null; } })
            .Where(x => x != null).ToList();

        private static string Ensure(string path) { Directory.CreateDirectory(path); return path; }
    }
}
