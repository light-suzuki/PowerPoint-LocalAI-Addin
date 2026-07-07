using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class ChatSessionManager
    {
        private readonly string _directory;
        public ChatSession Current { get; private set; }
        public ChatSessionManager(string directory = null) { _directory = directory ?? FileStoreUtils.Chats; Directory.CreateDirectory(_directory); }

        public ChatSession StartNew(string presentationPath, string deckContext, ChatSummary summary = null)
        {
            if (Current != null) Save(Current);
            Current = new ChatSession
            {
                ChatSessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_") + Guid.NewGuid().ToString("N").Substring(0, 4),
                CreatedAt = DateTime.Now, LastUpdatedAt = DateTime.Now, PresentationPath = presentationPath,
                DeckContext = deckContext, Summary = summary
            };
            if (summary != null && !string.IsNullOrWhiteSpace(summary.RawSummary))
                Current.Messages.Add(new ChatMessage { Role = "system", Kind = "summary", Content = summary.RawSummary });
            Save(Current);
            return Current;
        }

        public void AddMessage(string role, string content, string kind = "message", string metadataJson = null)
        {
            if (Current == null) StartNew(string.Empty, string.Empty);
            Current.Messages.Add(new ChatMessage { Role = role, Content = content, Kind = kind, MetadataJson = metadataJson });
            Save(Current);
        }
        public void Save(ChatSession session)
        {
            session.LastUpdatedAt = DateTime.Now;
            File.WriteAllText(Path.Combine(_directory, "chat_" + session.ChatSessionId + ".json"), JsonUtils.Serialize(session));
        }
        public IReadOnlyList<ChatSession> List() => Directory.GetFiles(_directory, "chat_*.json").OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(x => { try { return JsonUtils.Deserialize<ChatSession>(File.ReadAllText(x)); } catch { return null; } }).Where(x => x != null).ToList();
    }
}
