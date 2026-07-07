using System;
using System.Windows.Forms;

namespace LocalPptSkillAgent.UI
{
    public class SkillRunnerControl : UserControl
    {
        private readonly TextBox _context = new TextBox { Multiline = true, Height = 90, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical };
        private readonly ComboBox _target = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown };
        private readonly ComboBox _skill = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _instruction = new TextBox { Multiline = true, Height = 80, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical };
        public string ContextText => _context.Text;
        public string TargetText => string.IsNullOrWhiteSpace(_target.Text) ? "@current" : _target.Text;
        public string SkillName => _skill.Text;
        public string Instruction => _instruction.Text;

        public SkillRunnerControl()
        {
            _target.Items.AddRange(new object[] { "@current", "@selected", "@1-5", "@slide:3", "@all", "@after:5", "@before:2", "@figures", "@text", "@notes" });
            _target.Text = "@current";
            _skill.Items.AddRange(new object[] { "visual_check", "visual_polish", "align_shapes", "diagram_create", "slide_generate", "proofread", "fact_check", "timing_5min", "style_match" });
            _skill.SelectedIndex = 0;
            Controls.AddRange(new Control[] { Labeled("Instruction", _instruction, 100), Labeled("Skill", _skill, 45), Labeled("Target", _target, 45), Labeled("Context", _context, 110) });
            Dock = DockStyle.Top; Height = 300;
        }

        private static Control Labeled(string text, Control control, int height)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = height };
            panel.Controls.Add(control);
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Top, Height = 18 });
            return panel;
        }
    }
}
