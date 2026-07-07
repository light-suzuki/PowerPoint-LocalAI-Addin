using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LocalPptSkillAgent.UI
{
    public class ContextControl : UserControl
    {
        private readonly string _directory;
        private readonly ComboBox _template = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _purpose = Box();
        private readonly TextBox _audience = Box();
        private readonly TextBox _time = Box();
        private readonly TextBox _domain = Box();
        private readonly TextBox _citation = Box();
        private readonly TextBox _slidePolicy = Box();
        private readonly TextBox _raw = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        public event Action ContextChanged;
        public string DeckContextText => string.Join("\r\n", new[]
        {
            "目的: " + _purpose.Text, "対象: " + _audience.Text, "発表時間: " + _time.Text,
            "専門分野: " + _domain.Text, "引用方針: " + _citation.Text, "スライド方針: " + _slidePolicy.Text, _raw.Text
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        public ContextControl(string directory = null)
        {
            _directory = directory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contexts");
            var load = new Button { Text = "テンプレートを読み込む", Dock = DockStyle.Top, Height = 30 };
            load.Click += (s, e) => LoadSelected();
            var fields = new Panel { Dock = DockStyle.Top, Height = 280, AutoScroll = true };
            fields.Controls.Add(Labeled("スライド作成方針", _slidePolicy)); fields.Controls.Add(Labeled("引用方針", _citation));
            fields.Controls.Add(Labeled("専門分野", _domain)); fields.Controls.Add(Labeled("発表時間", _time));
            fields.Controls.Add(Labeled("対象者", _audience)); fields.Controls.Add(Labeled("発表目的", _purpose));
            Controls.Add(_raw); Controls.Add(fields); Controls.Add(load); Controls.Add(_template); Dock = DockStyle.Fill;
            if (Directory.Exists(_directory)) _template.Items.AddRange(Directory.GetFiles(_directory, "*.md").Select(Path.GetFileNameWithoutExtension).Cast<object>().ToArray());
            if (_template.Items.Count > 0) { _template.SelectedIndex = 0; LoadSelected(); }
            foreach (var box in new[] { _purpose, _audience, _time, _domain, _citation, _slidePolicy, _raw }) box.TextChanged += (s, e) => ContextChanged?.Invoke();
        }
        public void SetContext(string value) { _raw.Text = value ?? string.Empty; }
        private void LoadSelected()
        {
            if (_template.SelectedItem == null) return;
            var path = Path.Combine(_directory, _template.SelectedItem + ".md");
            if (File.Exists(path)) { _raw.Text = File.ReadAllText(path); ContextChanged?.Invoke(); }
        }
        private static TextBox Box() => new TextBox { Dock = DockStyle.Top };
        private static Control Labeled(string text, Control control) { var panel = new Panel { Dock = DockStyle.Top, Height = 45 }; panel.Controls.Add(control); panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Top, Height = 18 }); return panel; }
    }
}
