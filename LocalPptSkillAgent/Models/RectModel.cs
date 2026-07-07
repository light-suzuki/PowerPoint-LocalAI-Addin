namespace LocalPptSkillAgent.Models
{
    public class RectModel
    {
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Right => Left + Width;
        public float Bottom => Top + Height;
    }
}
