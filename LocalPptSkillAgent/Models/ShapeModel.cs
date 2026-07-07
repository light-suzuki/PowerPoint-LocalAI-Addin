namespace LocalPptSkillAgent.Models
{
    public class ShapeModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool HasText { get; set; }
        public string Text { get; set; }
        public float? FontSize { get; set; }
        public string FontName { get; set; }
        public int ZOrderPosition { get; set; }
        public bool IsPicture { get; set; }
        public bool IsTextBox { get; set; }
        public bool IsLine { get; set; }
        public bool IsGroup { get; set; }
        public bool IsChart { get; set; }
        public bool IsTable { get; set; }
        public RectModel Bounds { get; set; }
    }
}
