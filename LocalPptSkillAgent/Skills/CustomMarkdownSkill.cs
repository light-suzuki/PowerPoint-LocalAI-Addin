namespace LocalPptSkillAgent.Skills
{
    /// <summary>
    /// GUIやファイル追加で作られたMarkdown定義だけのスキル。
    /// 実行はLlmSkillBaseの共通ハーネス(Context Assembler + JSON Schema + Validator)をそのまま使い、
    /// スライド画像を使うかどうかはmd内のContext Policy(include_slide_images)に従う。
    /// </summary>
    public class CustomMarkdownSkill : LlmSkillBase
    {
        private readonly string _name;
        public CustomMarkdownSkill(string name) { _name = name; }
        public override string Name => _name;
        // 実際に画像を添付するかはRunAsync内でpolicy.IncludeSlideImagesが判定する
        protected override bool UsesVision => true;
    }
}
