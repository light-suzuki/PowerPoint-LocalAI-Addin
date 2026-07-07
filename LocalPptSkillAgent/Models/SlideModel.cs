using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class SlideModel
    {
        public int SlideIndex { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Title { get; set; }
        public string NotesText { get; set; }
        public List<ShapeModel> Shapes { get; set; } = new List<ShapeModel>();
        public string ExportedImagePath { get; set; }
    }
}
