namespace LocalPptSkillAgent.Models
{
    public class ShapeSnapshot
    {
        public int Slide { get; set; }
        public int ShapeId { get; set; }
        public string ShapeName { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Text { get; set; }
        public float? FontSize { get; set; }
        public string FontName { get; set; }
        public bool CreatedByAi { get; set; }
        public bool DeletedByAi { get; set; }
    }
}
