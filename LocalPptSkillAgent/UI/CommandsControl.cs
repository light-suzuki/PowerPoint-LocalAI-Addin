using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LocalPptSkillAgent.Services;

namespace LocalPptSkillAgent.UI
{
    /// <summary>
    /// コマンドのリファレンスタブ。@セレクタと/コマンド(スキル)の一覧を表示し、
    /// ダブルクリックでチャット入力欄へ挿入する。GUIからの新規スキル作成も担う。
    /// </summary>
    public class CommandsControl : UserControl
    {
        private readonly SkillRouter _router;
        private readonly ListView _selectors = MakeList();
        private readonly ListView _commands = MakeList();
        public event Action<string> InsertRequested;
        public event Action<string> Status;

        private static readonly (string Command, string Description)[] SelectorReference =
        {
            ("@current", "現在表示中のスライド"),
            ("@selected", "選択中の図形だけを対象にする"),
            ("@all", "全スライド"),
            ("@3", "3枚目のスライド"),
            ("@1-5", "1〜5枚目のスライド範囲"),
            ("@slide:3", "3枚目(明示形式)"),
            ("@after:2", "2枚目の直後に挿入"),
            ("@before:4", "4枚目の直前に挿入"),
            ("@figures", "図形・図表を対象にする"),
            ("@text", "テキストを対象にする"),
            ("@notes", "発表者ノートを対象にする")
        };

        public CommandsControl(SkillRouter router)
        {
            _router = router;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = Theme.Base; BackColor = Theme.Canvas; Dock = DockStyle.Fill;

            var create = new Button { Text = "＋ 新しいスキルを作成", AutoSize = true, Height = 32 };
            Theme.StylePrimary(create);
            create.Click += (s, e) => CreateSkill();
            var reload = new Button { Text = "一覧を更新", AutoSize = true, Height = 32 };
            Theme.StyleSecondary(reload);
            reload.Click += (s, e) => RefreshCommands();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 2), BackColor = Theme.Card };
            buttons.Controls.Add(create); buttons.Controls.Add(reload);

            var help = new Label
            {
                Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 4, 8, 0), BackColor = Theme.Card,
                Font = Theme.Small, ForeColor = Theme.TextMuted,
                Text = "チャット欄で使える記法の一覧です。項目をダブルクリックすると入力欄に挿入されます。"
            };

            // 上下2段: @セレクタ / /コマンド
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Theme.Canvas };
            split.Panel1.Controls.Add(_selectors);
            split.Panel1.Controls.Add(SectionHeader("@ 対象の指定"));
            split.Panel2.Controls.Add(_commands);
            split.Panel2.Controls.Add(SectionHeader("/ コマンド(スキルの直接実行)"));

            Controls.Add(split);
            Controls.Add(help);
            Controls.Add(buttons);

            _selectors.Columns.Add("記法", 110); _selectors.Columns.Add("意味", 260);
            _commands.Columns.Add("コマンド", 160); _commands.Columns.Add("説明", 260);
            foreach (var (command, description) in SelectorReference)
                _selectors.Items.Add(new ListViewItem(new[] { command, description }));
            _selectors.DoubleClick += (s, e) => InsertSelected(_selectors, 0, " ");
            _commands.DoubleClick += (s, e) => InsertSelected(_commands, 0, " ");
            RefreshCommands();
            Resize += (s, e) => { try { split.SplitterDistance = Math.Max(120, Height * 4 / 10); } catch { } };
        }

        public void RefreshCommands()
        {
            _router.ReloadCustomSkills();
            _commands.Items.Clear();
            foreach (var name in _router.SkillNames.OrderBy(x => x))
                _commands.Items.Add(new ListViewItem(new[] { "/" + name, DescribeSkill(name) }));
        }

        private string DescribeSkill(string name)
        {
            try
            {
                var path = Path.Combine(_router.PromptDirectory, name + ".md");
                if (!File.Exists(path)) return string.Empty;
                var lines = File.ReadAllLines(path);
                var purposeIndex = Array.FindIndex(lines, x => x.TrimStart().StartsWith("## 目的"));
                if (purposeIndex < 0) return string.Empty;
                for (var i = purposeIndex + 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim().TrimStart('-', '*', ' ');
                    if (line.StartsWith("#")) break;
                    if (line.Length > 0) return line.Length > 60 ? line.Substring(0, 60) + "…" : line;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        private void InsertSelected(ListView list, int column, string suffix)
        {
            if (list.SelectedItems.Count == 0) return;
            InsertRequested?.Invoke(list.SelectedItems[0].SubItems[column].Text + suffix);
        }

        private void CreateSkill()
        {
            using (var editor = new SkillEditorForm(_router.PromptDirectory))
            {
                if (editor.ShowDialog(FindForm()) != DialogResult.OK) return;
                RefreshCommands();
                Status?.Invoke($"スキル /{editor.CreatedSkillName} を作成しました。チャットで「/{editor.CreatedSkillName} 指示」と入力すると実行できます。");
            }
        }

        private static ListView MakeList() => new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.Base, MultiSelect = false
        };

        private static Control SectionHeader(string text) => new Label
        {
            Text = text, Dock = DockStyle.Top, Height = 26, Font = Theme.Title, ForeColor = Theme.Accent,
            Padding = new Padding(8, 4, 0, 0), BackColor = Theme.Canvas
        };
    }
}
