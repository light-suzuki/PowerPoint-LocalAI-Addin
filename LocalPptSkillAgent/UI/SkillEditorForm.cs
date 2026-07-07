using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using LocalPptSkillAgent.Services;

namespace LocalPptSkillAgent.UI
{
    /// <summary>
    /// GUIでスキル(Markdown定義)を作成するフォーム。生成物はSkillPromptServiceの
    /// 必須セクション(目的/出力JSON schema/禁止事項/例/Context Policy)を満たす。
    /// </summary>
    public class SkillEditorForm : Form
    {
        private readonly string _directory;
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _purpose = new TextBox { Multiline = true, Height = 48, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _steps = new TextBox { Multiline = true, Height = 88, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _prohibitions = new TextBox { Multiline = true, Height = 48, ScrollBars = ScrollBars.Vertical, Text = "既存のShape・Slideの削除。対象範囲外の編集。出典のない事実の追加。" };
        private readonly CheckBox _geometry = new CheckBox { Text = "図形の座標・サイズを渡す(レイアウト系で推奨)", AutoSize = true };
        private readonly CheckBox _notes = new CheckBox { Text = "発表者ノートを渡す", AutoSize = true };
        private readonly CheckBox _deckStyle = new CheckBox { Text = "デッキのスタイル情報を渡す", AutoSize = true, Checked = true };
        private readonly CheckBox _recentChat = new CheckBox { Text = "直近の会話を渡す", AutoSize = true, Checked = true };
        private readonly CheckBox _slideImages = new CheckBox { Text = "スライド画像を渡す(Visionモデルを使用・低速)", AutoSize = true };
        private readonly NumericUpDown _recentCount = new NumericUpDown { Minimum = 0, Maximum = 30, Value = 6, Width = 60 };
        public string CreatedSkillName { get; private set; }

        public SkillEditorForm(string directory)
        {
            _directory = directory;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Text = "新しいスキルを作成";
            Font = Theme.Base; BackColor = Theme.Canvas;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 560);

            var layout = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12, 8, 12, 8) };

            var create = new Button { Text = "作成", Width = 96, Height = 32, DialogResult = DialogResult.None };
            var cancel = new Button { Text = "キャンセル", Width = 96, Height = 32, DialogResult = DialogResult.Cancel };
            Theme.StylePrimary(create); Theme.StyleSecondary(cancel);
            create.Click += (s, e) => Create();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 4), BackColor = Theme.Card };
            buttons.Controls.Add(create); buttons.Controls.Add(cancel);
            CancelButton = cancel;

            var recentRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, WrapContents = false };
            recentRow.Controls.Add(new Label { Text = "会話を渡す件数:", AutoSize = true, Padding = new Padding(0, 6, 4, 0), Font = Theme.Small, ForeColor = Theme.TextMuted });
            recentRow.Controls.Add(_recentCount);

            // Dock=Topの表示順は追加の逆順
            layout.Controls.Add(recentRow);
            layout.Controls.Add(Wrap(_slideImages));
            layout.Controls.Add(Wrap(_recentChat));
            layout.Controls.Add(Wrap(_deckStyle));
            layout.Controls.Add(Wrap(_notes));
            layout.Controls.Add(Wrap(_geometry));
            layout.Controls.Add(Section("AIに渡す情報(Context Policy)"));
            layout.Controls.Add(Labeled("禁止事項", _prohibitions, "このスキルでやってはいけないこと。安全契約(削除禁止など)は自動で追加されます。"));
            layout.Controls.Add(Labeled("手順・指示(任意)", _steps, "AIへの具体的な指示。例: 1スライド1メッセージに要約する / 箇条書きは3行以内 など"));
            layout.Controls.Add(Labeled("目的", _purpose, "このスキルが何をするか1〜2文で。コマンド一覧の説明にも表示されます。"));
            layout.Controls.Add(Labeled("スキル名(英小文字・数字・_ のみ)", _name, "チャットからは /スキル名 で呼び出します。例: summary_notes"));

            Controls.Add(layout);
            Controls.Add(buttons);
        }

        private void Create()
        {
            var name = (_name.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (!Regex.IsMatch(name, "^[a-z0-9_]{2,40}$"))
            {
                MessageBox.Show(this, "スキル名は英小文字・数字・アンダースコア(2〜40文字)で入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_purpose.Text))
            {
                MessageBox.Show(this, "目的を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var path = Path.Combine(_directory, name + ".md");
            if (File.Exists(path))
            {
                MessageBox.Show(this, $"/{name} は既に存在します。別の名前にするか、スキルタブで既存定義を編集してください。", "重複", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var content = BuildMarkdown(name);
            var errors = SkillPromptService.ValidateContent(content);
            if (errors.Count > 0)
            {
                MessageBox.Show(this, "生成した定義が検証を通りませんでした:\r\n" + string.Join("\r\n", errors), "検証エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Directory.CreateDirectory(_directory);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            CreatedSkillName = name;
            DialogResult = DialogResult.OK;
            Close();
        }

        private string BuildMarkdown(string name)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## 目的");
            builder.AppendLine(_purpose.Text.Trim());
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(_steps.Text))
            {
                builder.AppendLine("## 指示");
                builder.AppendLine(_steps.Text.Trim());
                builder.AppendLine();
            }
            builder.AppendLine("## 出力JSON schema");
            builder.AppendLine("LayoutActionPlanの標準構造に従い、summary・issues・actionsを含むJSONのみを返す。散文やMarkdownは返さない。");
            builder.AppendLine();
            builder.AppendLine("## 禁止事項");
            builder.AppendLine(_prohibitions.Text.Trim());
            builder.AppendLine();
            builder.AppendLine("## 例");
            builder.AppendLine("{\"skill\":\"" + name + "\",\"target\":\"@current\",\"summary\":\"変更概要\",\"issues\":[],\"actions\":[]}");
            builder.AppendLine();
            builder.AppendLine("## Context Policy");
            builder.AppendLine("- include_geometry: " + (_geometry.Checked ? "true" : "false"));
            builder.AppendLine("- include_notes: " + (_notes.Checked ? "true" : "false"));
            builder.AppendLine("- include_deck_style: " + (_deckStyle.Checked ? "true" : "false"));
            builder.AppendLine("- include_recent_chat: " + (_recentChat.Checked ? "true" : "false"));
            builder.AppendLine("- include_slide_images: " + (_slideImages.Checked ? "true" : "false"));
            builder.AppendLine("- recent_chat_messages: " + (int)_recentCount.Value);
            return builder.ToString();
        }

        private static Control Wrap(Control control)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 26, Padding = new Padding(2, 2, 0, 0) };
            control.Dock = DockStyle.Fill;
            panel.Controls.Add(control);
            return panel;
        }

        private static Control Section(string text) => new Label
        {
            Text = text, Dock = DockStyle.Top, Height = 30, Font = Theme.Title, ForeColor = Theme.Accent,
            TextAlign = ContentAlignment.BottomLeft
        };

        private static Control Labeled(string title, Control control, string description)
        {
            var height = 22 + 16 + control.Height + 8;
            var panel = new Panel { Dock = DockStyle.Top, Height = height };
            control.Dock = DockStyle.Top;
            panel.Controls.Add(control);
            panel.Controls.Add(new Label { Text = description, Dock = DockStyle.Top, Height = 16, Font = Theme.Small, ForeColor = Theme.TextMuted });
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 20, Font = Theme.Base, ForeColor = Theme.Text });
            return panel;
        }
    }
}
