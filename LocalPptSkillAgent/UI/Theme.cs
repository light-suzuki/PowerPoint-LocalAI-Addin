using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalPptSkillAgent.UI
{
    /// <summary>タスクペイン全体で使う配色・フォント・スタイルヘルパー。</summary>
    public static class Theme
    {
        // PowerPointブランドのオレンジをアクセントにしたライトテーマ
        public static readonly Color Canvas = Color.FromArgb(245, 244, 242);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(225, 223, 221);
        public static readonly Color Accent = Color.FromArgb(196, 62, 28);
        public static readonly Color AccentDark = Color.FromArgb(169, 53, 22);
        public static readonly Color AccentSoft = Color.FromArgb(252, 236, 231);
        public static readonly Color Text = Color.FromArgb(32, 31, 30);
        public static readonly Color TextMuted = Color.FromArgb(96, 94, 92);
        public static readonly Color ErrorBack = Color.FromArgb(253, 231, 233);
        public static readonly Color ErrorText = Color.FromArgb(164, 38, 44);
        public static readonly Color Ok = Color.FromArgb(16, 124, 65);
        public static readonly Color Warn = Color.FromArgb(157, 93, 0);
        public static readonly Color Danger = Color.FromArgb(196, 62, 28);

        public static readonly Font Base = new Font("Yu Gothic UI", 9.5F);
        public static readonly Font Small = new Font("Yu Gothic UI", 8.25F);
        public static readonly Font Title = new Font("Yu Gothic UI", 10.5F, FontStyle.Bold);

        public static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            if (bounds.Width <= 0 || bounds.Height <= 0) { path.AddRectangle(new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height))); return path; }
            d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void StylePrimary(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.Font = Base;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = AccentDark;
        }

        public static void StyleSecondary(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Card;
            button.ForeColor = Text;
            button.Font = Base;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = Canvas;
        }

        public static void StyleGhost(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.Transparent;
            button.ForeColor = TextMuted;
            button.Font = Small;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = Canvas;
        }

        public static void StyleChip(Button button, bool selected = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = selected ? Accent : Border;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = selected ? AccentSoft : Card;
            button.ForeColor = selected ? AccentDark : TextMuted;
            button.Font = Small;
            button.Cursor = Cursors.Hand;
            button.AutoSize = true;
            button.Padding = new Padding(6, 2, 6, 2);
            button.Margin = new Padding(3, 2, 3, 2);
            button.FlatAppearance.MouseOverBackColor = selected ? AccentSoft : Canvas;
        }

        public static void RoundCorners(Control control, int radius)
        {
            control.Region = new Region(RoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius));
            control.Resize += (s, e) => control.Region = new Region(RoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius));
        }
    }
}
