using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.UI
{
    /// <summary>
    /// Copilot風のチャットファーストUI。バブル形式の会話、単一の入力欄、
    /// 提案チップ、修正案カードで構成する。
    /// </summary>
    public class ChatControl : UserControl
    {
        private readonly BubbleListPanel _conversation = new BubbleListPanel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
        private readonly TextBox _input = new TextBox { Multiline = true, BorderStyle = BorderStyle.None, Font = Theme.Base, BackColor = Theme.Card, ForeColor = Theme.Text };
        private readonly Label _placeholder = new Label { Text = "メッセージを入力 — 相談・質問・編集依頼をそのまま書けます", Font = Theme.Base, ForeColor = Theme.TextMuted, BackColor = Theme.Card, AutoSize = false };
        private readonly Button _send = new Button { Text = "送信 ➤", Width = 88, Height = 30 };
        private readonly Button _cancel = new Button { Text = "中止", Width = 64, Height = 30, Visible = false };
        private readonly ComboBox _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = Theme.Small, Width = 128 };
        private readonly Label _budgetLabel = new Label { Font = Theme.Small, ForeColor = Theme.TextMuted, AutoSize = true };
        private readonly Panel _budgetTrack = new Panel { Height = 3, BackColor = Theme.Border, Dock = DockStyle.Bottom };
        private readonly Panel _budgetFill = new Panel { Width = 0, Height = 3, BackColor = Theme.Ok };
        private readonly Panel _resultCard = new Panel { Dock = DockStyle.Bottom, BackColor = Theme.Card, Visible = false, Padding = new Padding(10, 8, 10, 8) };
        private readonly Label _resultTitle = new Label { Text = "提案された変更", Font = Theme.Title, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = 24 };
        private readonly TextBox _resultText = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.Small, Dock = DockStyle.Fill };
        private readonly CheckedListBox _actions = new CheckedListBox { CheckOnClick = true, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.Small, Dock = DockStyle.Bottom, Height = 84, Visible = false };
        private readonly Button _applyAll = new Button { Text = "すべて適用", Width = 96, Height = 28, Enabled = false };
        private readonly Button _applyPartial = new Button { Text = "選択のみ適用", Width = 104, Height = 28, Enabled = false };
        private readonly Button _closeResult = new Button { Text = "✕", Width = 26, Height = 22 };
        private readonly FlowLayoutPanel _suggestions = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Color.Transparent };
        private readonly Timer _typingTimer = new Timer { Interval = 400 };
        private int _typingPhase;
        private bool _hasActions;
        public string SelectedMode { get; private set; } = "auto";

        public event Action<string, string> SendRequested;
        public event Action<IReadOnlyList<int>> ApplyRequested;
        public event Action RollbackRequested;
        public event Action NewChatRequested;
        public event Action SummarizeNewRequested;
        public event Action HistoryRequested;
        public event Action CancelRequested;
        public event Action LoadTextModelRequested;
        public event Action LoadVisionModelRequested;
        public event Action CheckModelsRequested;

        private static readonly (string Label, string Token)[] Modes =
        {
            ("モード: 自動判定", "auto"), ("モード: 会話のみ", "chat"), ("モード: 見た目調整", "見た目"), ("モード: 図解作成", "図解"),
            ("モード: 校閲", "校閲"), ("モード: 翻訳", "翻訳"), ("モード: 要約", "要約"),
            ("モード: ファクトチェック", "ファクトチェック"), ("モード: スライド生成", "スライド生成"), ("モード: 5分再構成", "5分再構成")
        };

        public ChatControl()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            BackColor = Theme.Canvas;
            Font = Theme.Base;

            // ── ヘッダー ─────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Card, Padding = new Padding(10, 0, 6, 0) };
            var title = new Label { Text = "Local AI", Font = Theme.Title, ForeColor = Theme.Accent, AutoSize = true, Location = new Point(10, 10) };
            var newChat = new Button { Text = "＋ 新規", AutoSize = true };
            var menu = new Button { Text = "⋯", Width = 34, Height = 26 };
            Theme.StyleGhost(newChat); Theme.StyleGhost(menu);
            var menuStrip = new ContextMenuStrip { Font = Theme.Base };
            menuStrip.Items.Add("要約して新しいチャット", null, (s, e) => SummarizeNewRequested?.Invoke());
            menuStrip.Items.Add("チャット履歴を開く", null, (s, e) => HistoryRequested?.Invoke());
            menuStrip.Items.Add("直前のAI編集を戻す", null, (s, e) => RollbackRequested?.Invoke());
            menuStrip.Items.Add(new ToolStripSeparator());
            menuStrip.Items.Add("Textモデルをロード", null, (s, e) => LoadTextModelRequested?.Invoke());
            menuStrip.Items.Add("Visionモデルをロード", null, (s, e) => LoadVisionModelRequested?.Invoke());
            menuStrip.Items.Add("モデルの状態を確認", null, (s, e) => CheckModelsRequested?.Invoke());
            newChat.Click += (s, e) => NewChatRequested?.Invoke();
            menu.Click += (s, e) => menuStrip.Show(menu, new Point(0, menu.Height));
            header.Controls.Add(title); header.Controls.Add(newChat); header.Controls.Add(menu);
            header.Resize += (s, e) => { menu.Location = new Point(header.Width - menu.Width - 6, 7); newChat.Location = new Point(menu.Left - newChat.Width - 2, 7); };
            var headerBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };

            // ── コンテキスト使用量(細いバー+ラベル) ──────
            var budgetPanel = new Panel { Dock = DockStyle.Top, Height = 21, BackColor = Theme.Card, Padding = new Padding(10, 0, 10, 0) };
            _budgetLabel.Location = new Point(10, 2);
            _budgetTrack.Controls.Add(_budgetFill);
            budgetPanel.Controls.Add(_budgetLabel);
            budgetPanel.Controls.Add(_budgetTrack);

            // ── 会話エリア(空のときは提案チップ) ─────────
            BuildSuggestions();
            _conversation.Controls.Add(_suggestions);
            _conversation.Resize += (s, e) => CenterSuggestions();

            // ── 修正案カード ─────────────────────────
            var resultButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 3, 0, 0) };
            Theme.StylePrimary(_applyAll); Theme.StyleSecondary(_applyPartial); Theme.StyleGhost(_closeResult);
            _applyAll.Click += (s, e) => ApplyRequested?.Invoke(Enumerable.Range(0, _actions.Items.Count).ToList());
            _applyPartial.Click += (s, e) => ApplyRequested?.Invoke(_actions.CheckedIndices.Cast<int>().ToList());
            _closeResult.Click += (s, e) => ClearResult();
            resultButtons.Controls.Add(_applyAll); resultButtons.Controls.Add(_applyPartial);
            var resultHeader = new Panel { Dock = DockStyle.Top, Height = 24 };
            _resultTitle.Dock = DockStyle.Fill;
            _closeResult.Dock = DockStyle.Right;
            resultHeader.Controls.Add(_resultTitle); resultHeader.Controls.Add(_closeResult);
            _resultCard.Height = 240;
            _resultCard.Controls.Add(_resultText);
            _resultCard.Controls.Add(_actions);
            _resultCard.Controls.Add(resultButtons);
            _resultCard.Controls.Add(resultHeader);
            var resultBorder = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border };

            // ── 入力エリア ───────────────────────────
            var composer = new Panel { Dock = DockStyle.Bottom, Height = 152, BackColor = Theme.Card, Padding = new Padding(10, 6, 10, 6) };
            var chipRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            AddTargetChip(chipRow, "対象: 現在のスライド", "@current");
            AddTargetChip(chipRow, "対象: 選択中", "@selected");
            AddTargetChip(chipRow, "対象: 全スライド", "@all");
            _mode.Items.AddRange(Modes.Select(x => (object)x.Label).ToArray());
            _mode.SelectedIndex = 0;
            _mode.SelectedIndexChanged += (s, e) => SelectedMode = Modes[Math.Max(0, _mode.SelectedIndex)].Token;
            chipRow.Controls.Add(_mode);

            // 送信ボタンは独立した最下段の行に置く(入力欄の背面に隠れないようにする)
            var buttonRow = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Theme.Card, Padding = new Padding(0, 6, 0, 2) };
            var hint = new Label { Text = "Ctrl+Enterで送信 ・ @で対象指定 ・ /でコマンド", Font = Theme.Small, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(0, 13) };
            Theme.StylePrimary(_send); Theme.StyleSecondary(_cancel);
            _send.Click += (s, e) => Send();
            _cancel.Click += (s, e) => CancelRequested?.Invoke();
            buttonRow.Controls.Add(hint);
            buttonRow.Controls.Add(_send);
            buttonRow.Controls.Add(_cancel);
            buttonRow.Resize += (s, e) =>
            {
                _send.Location = new Point(buttonRow.Width - _send.Width, 6);
                _cancel.Location = new Point(_send.Left - _cancel.Width - 6, 6);
            };

            // 枠付きの入力欄。フォーカスでアクセント色の枠にする
            var inputOuter = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border, Padding = new Padding(1) };
            var inputInner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(9, 7, 9, 7) };
            _input.Dock = DockStyle.Fill;
            _input.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.Enter) { Send(); e.SuppressKeyPress = true; } };
            _input.TextChanged += (s, e) => _placeholder.Visible = _input.TextLength == 0 && !_input.Focused;
            _input.GotFocus += (s, e) => { _placeholder.Visible = false; inputOuter.BackColor = Theme.Accent; };
            _input.LostFocus += (s, e) => { _placeholder.Visible = _input.TextLength == 0; inputOuter.BackColor = Theme.Border; };
            _placeholder.Dock = DockStyle.Fill;
            _placeholder.Click += (s, e) => _input.Focus();
            inputInner.Controls.Add(_input);
            inputInner.Controls.Add(_placeholder);
            _placeholder.BringToFront();
            inputOuter.Controls.Add(inputInner);

            composer.Controls.Add(inputOuter);
            composer.Controls.Add(buttonRow);
            composer.Controls.Add(chipRow);
            var composerBorder = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border };

            Controls.Add(_conversation);
            Controls.Add(budgetPanel);
            Controls.Add(headerBorder);
            Controls.Add(header);
            Controls.Add(composer);
            Controls.Add(composerBorder);
            Controls.Add(_resultCard);
            Controls.Add(resultBorder);
            Dock = DockStyle.Fill;

            _typingTimer.Tick += (s, e) =>
            {
                _typingPhase = (_typingPhase + 1) % 3;
                _conversation.UpdateTyping("考えています" + new string('・', _typingPhase + 1));
            };
        }

        private void BuildSuggestions()
        {
            var caption = new Label { Text = "こんなふうに話しかけられます", Font = Theme.Small, ForeColor = Theme.TextMuted, AutoSize = true, Margin = new Padding(4, 0, 4, 6) };
            _suggestions.Controls.Add(caption);
            foreach (var example in new[]
            {
                "このデッキの改善点を教えて",
                "@current 見た目を整えて",
                "@all 5分発表向けに再構成して",
                "実験手順を図解するスライドを作って",
                "/proofread @all 誤字と表記ゆれを直して"
            })
            {
                var chip = new Button { Text = example, AutoSize = true, Height = 28 };
                Theme.StyleChip(chip);
                chip.Click += (s, e) => { _input.Text = example; _input.SelectionStart = _input.TextLength; _input.Focus(); };
                _suggestions.Controls.Add(chip);
            }
        }

        private void CenterSuggestions()
        {
            if (!_suggestions.Visible) return;
            _suggestions.Location = new Point(
                Math.Max(8, (_conversation.ClientSize.Width - _suggestions.Width) / 2),
                Math.Max(16, (_conversation.ClientSize.Height - _suggestions.Height) / 3));
        }

        private void AddTargetChip(Control parent, string label, string selector)
        {
            var chip = new Button { Text = label, AutoSize = true, Height = 24 };
            Theme.StyleChip(chip);
            chip.Click += (s, e) =>
            {
                var text = _input.Text.TrimStart();
                if (text.StartsWith("@")) { var firstSpace = text.IndexOf(' '); text = firstSpace < 0 ? string.Empty : text.Substring(firstSpace + 1); }
                _input.Text = selector + " " + text;
                _input.SelectionStart = _input.TextLength;
                _input.Focus();
            };
            parent.Controls.Add(chip);
        }

        public void AddMessage(string role, string text)
        {
            _suggestions.Visible = false;
            _conversation.AddBubble(role, text ?? string.Empty);
        }

        public void ClearConversation()
        {
            _conversation.ClearBubbles();
            _suggestions.Visible = true;
            CenterSuggestions();
            ClearResult();
        }

        public void ShowResult(string text, LayoutActionPlan plan = null)
        {
            _resultText.Text = text ?? string.Empty;
            _actions.Items.Clear();
            if (plan?.Actions != null)
                for (var i = 0; i < plan.Actions.Count; i++)
                    _actions.Items.Add($"{i + 1}. {plan.Actions[i].Slide}枚目: {ActionLabel(plan.Actions[i].Type)} — {plan.Actions[i].Reason}", true);
            _hasActions = _actions.Items.Count > 0;
            _actions.Visible = _hasActions;
            _applyAll.Enabled = _hasActions;
            _applyPartial.Enabled = _hasActions;
            _resultTitle.Text = _hasActions ? $"提案された変更({_actions.Items.Count}件)" : "実行結果";
            _resultCard.Height = _hasActions ? 250 : 170;
            _resultCard.Visible = !string.IsNullOrWhiteSpace(text) || _hasActions;
        }

        public void ClearResult()
        {
            _resultText.Clear(); _actions.Items.Clear(); _hasActions = false;
            _applyAll.Enabled = false; _applyPartial.Enabled = false;
            _resultCard.Visible = false;
        }

        public void SetBusy(bool busy)
        {
            _send.Visible = !busy;
            _cancel.Visible = busy;
            _input.Enabled = !busy;
            _mode.Enabled = !busy;
            _applyAll.Enabled = !busy && _hasActions;
            _applyPartial.Enabled = !busy && _hasActions;
            if (busy)
            {
                _typingPhase = 0;
                _conversation.ShowTyping("考えています・");
                _typingTimer.Start();
            }
            else
            {
                _typingTimer.Stop();
                _conversation.HideTyping();
            }
        }

        public void UpdateBudget(ContextBudget budget)
        {
            var color = budget.Status == "安全" ? Theme.Ok : budget.Status == "注意" ? Theme.Warn : Theme.Danger;
            _budgetLabel.Text = $"コンテキスト {budget.UsageRatio:P0} 使用({budget.EstimatedUsedTokens:N0}/{budget.MaxContextTokens:N0} tokens)"
                + (string.IsNullOrEmpty(budget.WarningMessage) ? string.Empty : " ⚠ 新しいチャットを推奨");
            _budgetLabel.ForeColor = budget.Status == "安全" ? Theme.TextMuted : color;
            _budgetFill.BackColor = color;
            _budgetFill.Width = (int)(_budgetTrack.ClientSize.Width * Math.Min(1.0, budget.UsageRatio));
        }

        public void SetInputText(string text)
        {
            _input.Text = text ?? string.Empty;
            _input.SelectionStart = _input.TextLength;
            _input.Focus();
        }

        private void Send()
        {
            var text = _input.Text.Trim();
            if (text.Length == 0) return;
            _input.Clear();
            SendRequested?.Invoke(text, SelectedMode);
        }

        private static string ActionLabel(string type)
        {
            switch (type ?? string.Empty)
            {
                case "move": return "移動";
                case "resize": return "サイズ変更";
                case "set_font_size": return "文字サイズ変更";
                case "set_text": return "文言修正";
                case "align": return "整列";
                case "distribute": return "等間隔配置";
                case "create_slide": return "スライド作成";
                case "create_shape": return "図形作成";
                case "create_textbox": return "テキストボックス作成";
                case "create_diagram": return "図解作成";
                default: return type;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _typingTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>バブルを縦に積むスクロールパネル。手動レイアウトで左右整列を制御する。</summary>
    internal class BubbleListPanel : Panel
    {
        private readonly List<ChatBubble> _bubbles = new List<ChatBubble>();
        private ChatBubble _typing;

        public BubbleListPanel()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            Resize += (s, e) => Relayout();
        }

        public void AddBubble(string role, string text)
        {
            var bubble = new ChatBubble(role, text);
            _bubbles.Add(bubble);
            Controls.Add(bubble);
            Relayout(scrollToBottom: true);
        }

        public void ClearBubbles()
        {
            HideTyping();
            foreach (var bubble in _bubbles) { Controls.Remove(bubble); bubble.Dispose(); }
            _bubbles.Clear();
            AutoScrollMinSize = Size.Empty;
            Invalidate();
        }

        public void ShowTyping(string text)
        {
            if (_typing != null) return;
            _typing = new ChatBubble("assistant", text);
            Controls.Add(_typing);
            Relayout(scrollToBottom: true);
        }

        public void UpdateTyping(string text)
        {
            if (_typing == null) return;
            _typing.SetText(text);
            Relayout(scrollToBottom: true);
        }

        public void HideTyping()
        {
            if (_typing == null) return;
            Controls.Remove(_typing);
            _typing.Dispose();
            _typing = null;
            Relayout();
        }

        private void Relayout(bool scrollToBottom = false)
        {
            if (ClientSize.Width < 60) return;
            SuspendLayout();
            const int pad = 10;
            var y = pad;
            var maxWidth = (int)(ClientSize.Width * 0.82) - pad;
            var all = new List<ChatBubble>(_bubbles);
            if (_typing != null) all.Add(_typing);
            foreach (var bubble in all)
            {
                bubble.Measure(maxWidth);
                var x = bubble.IsUser ? ClientSize.Width - bubble.Width - pad : pad;
                bubble.Location = new Point(x, y + AutoScrollPosition.Y);
                y += bubble.Height + 8;
            }
            AutoScrollMinSize = new Size(0, y + pad);
            if (scrollToBottom)
            {
                AutoScrollPosition = new Point(0, Math.Max(0, y + pad - ClientSize.Height));
                // スクロール後にAutoScrollPosition.Yが変わるため再配置する
                var y2 = pad;
                foreach (var bubble in all)
                {
                    var x = bubble.IsUser ? ClientSize.Width - bubble.Width - pad : pad;
                    bubble.Location = new Point(x, y2 + AutoScrollPosition.Y);
                    y2 += bubble.Height + 8;
                }
            }
            ResumeLayout();
        }
    }

    /// <summary>角丸の吹き出し1件。役割ごとに配色を変える。</summary>
    internal class ChatBubble : Control
    {
        private const int Radius = 10;
        private static readonly Padding Inner = new Padding(11, 8, 11, 8);
        public bool IsUser { get; }
        private readonly bool _isError;

        public ChatBubble(string role, string text)
        {
            IsUser = role == "user";
            _isError = role == "error";
            Text = text;
            Font = Theme.Base;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void SetText(string text) { Text = text; Invalidate(); }

        public void Measure(int maxWidth)
        {
            var proposed = new Size(Math.Max(60, maxWidth - Inner.Horizontal), int.MaxValue);
            var size = TextRenderer.MeasureText(Text, Font, proposed, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            Width = Math.Min(maxWidth, size.Width + Inner.Horizontal);
            Height = size.Height + Inner.Vertical;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Canvas);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var back = _isError ? Theme.ErrorBack : IsUser ? Theme.Accent : Theme.Card;
            var fore = _isError ? Theme.ErrorText : IsUser ? Color.White : Theme.Text;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedPath(bounds, Radius))
            {
                using (var brush = new SolidBrush(back)) e.Graphics.FillPath(brush, path);
                if (!IsUser && !_isError)
                    using (var pen = new Pen(Theme.Border)) e.Graphics.DrawPath(pen, path);
            }
            var textBounds = new Rectangle(Inner.Left, Inner.Top, Width - Inner.Horizontal, Height - Inner.Vertical);
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, fore, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
        }
    }
}
