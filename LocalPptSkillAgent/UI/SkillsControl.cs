using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LocalPptSkillAgent.Services;

namespace LocalPptSkillAgent.UI
{
    public class SkillsControl : UserControl
    {
        private readonly string _directory;
        private readonly ListBox _skills = new ListBox { Dock = DockStyle.Left, Width = 150, BorderStyle = BorderStyle.None, Font = Theme.Base, BackColor = Theme.Card, ForeColor = Theme.Text };
        private readonly TextBox _editor = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = Theme.Base };
        public event Action<string> Status;
        public event Action SkillsChanged;

        public SkillsControl(string directory = null)
        {
            _directory = directory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills");
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            Font = Theme.Base; BackColor = Theme.Canvas;

            var create = new Button { Text = "＋ 新しいスキルを作成", AutoSize = true, Height = 30 };
            Theme.StylePrimary(create);
            create.Click += (s, e) => CreateSkill();
            var save = new Button { Text = "この定義を検証して保存", AutoSize = true, Height = 30 };
            Theme.StyleSecondary(save);
            save.Click += (s, e) => Save();
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6, 5, 6, 2), BackColor = Theme.Card };
            toolbar.Controls.Add(create); toolbar.Controls.Add(save);

            _skills.SelectedIndexChanged += (s, e) => LoadSelected();
            var right = new Panel { Dock = DockStyle.Fill }; right.Controls.Add(_editor);
            Controls.Add(right); Controls.Add(_skills); Controls.Add(toolbar); Dock = DockStyle.Fill;
            RefreshSkills();
        }

        public void RefreshSkills(string selectName = null)
        {
            _skills.Items.Clear(); if (!Directory.Exists(_directory)) return;
            _skills.Items.AddRange(Directory.GetFiles(_directory, "*.md").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).Cast<object>().ToArray());
            if (_skills.Items.Count == 0) return;
            var index = selectName == null ? -1 : _skills.Items.IndexOf(selectName);
            _skills.SelectedIndex = index >= 0 ? index : 0;
        }

        private void CreateSkill()
        {
            using (var editor = new SkillEditorForm(_directory))
            {
                if (editor.ShowDialog(FindForm()) != DialogResult.OK) return;
                RefreshSkills(editor.CreatedSkillName);
                Status?.Invoke($"スキル /{editor.CreatedSkillName} を作成しました。チャットで「/{editor.CreatedSkillName} 指示」と入力すると実行できます。");
                SkillsChanged?.Invoke();
            }
        }

        private void LoadSelected()
        {
            var path = SelectedPath();
            if (path != null && File.Exists(path)) _editor.Text = File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

        private void Save()
        {
            try
            {
                var path = SelectedPath(); if (path == null) return;
                var errors = SkillPromptService.ValidateContent(_editor.Text);
                if (errors.Count > 0) throw new InvalidDataException(string.Join(" / ", errors));
                File.WriteAllText(path, _editor.Text); Status?.Invoke("Skill定義を検証して保存しました: " + Path.GetFileName(path));
                SkillsChanged?.Invoke();
            }
            catch (Exception ex) { Status?.Invoke("Skill定義を保存できませんでした: " + ex.Message); }
        }

        private string SelectedPath() => _skills.SelectedItem == null ? null : Path.Combine(_directory, _skills.SelectedItem + ".md");
    }
}
