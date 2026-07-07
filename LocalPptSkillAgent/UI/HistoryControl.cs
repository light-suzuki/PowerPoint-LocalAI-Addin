using System;
using System.Windows.Forms;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.UI
{
    public class HistoryControl : UserControl
    {
        private readonly ListBox _sessions = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Display" };
        private readonly ListBox _chats = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Display" };
        private readonly ChatSessionManager _chatManager;
        public string SelectedSessionId => (_sessions.SelectedItem as EditItem)?.Session.SessionId;
        public event Action<string> Selected;
        public event Action<string> RollbackSelected;
        public event Action<ChatSession> ChatSelected;

        public HistoryControl(ChatSessionManager chatManager = null)
        {
            _chatManager = chatManager;
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var editPage = new TabPage("AI編集");
            var rollback = new Button { Text = "選択したAI編集を戻す", Dock = DockStyle.Bottom, Height = 34 };
            rollback.Click += (s, e) => { if (SelectedSessionId != null) RollbackSelected?.Invoke(SelectedSessionId); };
            editPage.Controls.Add(_sessions); editPage.Controls.Add(rollback);
            tabs.TabPages.Add(editPage);
            tabs.TabPages.Add(new TabPage("チャット") { Controls = { _chats } });
            Controls.Add(tabs); Dock = DockStyle.Fill;
            _sessions.SelectedIndexChanged += (s, e) => { if (SelectedSessionId != null) Selected?.Invoke(SelectedSessionId); };
            _chats.SelectedIndexChanged += (s, e) => { if (_chats.SelectedItem is ChatItem item) ChatSelected?.Invoke(item.Session); };
        }
        public void RefreshSessions()
        {
            _sessions.Items.Clear(); foreach (var session in FileStoreUtils.ListSessions()) _sessions.Items.Add(new EditItem(session));
            _chats.Items.Clear(); if (_chatManager != null) foreach (var chat in _chatManager.List()) _chats.Items.Add(new ChatItem(chat));
        }
        private class EditItem
        {
            public AiEditSession Session { get; }
            public string Display => $"{Session.CreatedAt:MM/dd HH:mm} {Session.Skill} {Session.Target}" +
                (Session.Status != "completed" ? $" [{Session.Status}]" : Session.RolledBack ? " [戻し済み]" : string.Empty);
            public EditItem(AiEditSession session) { Session = session; }
        }
        private class ChatItem
        {
            public ChatSession Session { get; }
            public string Display => $"{Session.LastUpdatedAt:MM/dd HH:mm} {Session.ChatSessionId} ({Session.Messages.Count}件)";
            public ChatItem(ChatSession session) { Session = session; }
        }
    }
}
