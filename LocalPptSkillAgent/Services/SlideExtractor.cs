using LocalPptSkillAgent.Models;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class SlideExtractor
    {
        private readonly ShapeExtractor _shapes = new ShapeExtractor();

        public SlideModel Extract(PowerPoint.Presentation presentation, PowerPoint.Slide slide, bool includeNotes = true)
        {
            var model = new SlideModel
            {
                SlideIndex = slide.SlideIndex,
                Width = presentation.PageSetup.SlideWidth,
                Height = presentation.PageSetup.SlideHeight,
                NotesText = includeNotes ? PowerPointService.GetNotes(slide) : null
            };
            foreach (PowerPoint.Shape shape in slide.Shapes)
            {
                var extracted = _shapes.Extract(shape);
                model.Shapes.Add(extracted);
                if (string.IsNullOrEmpty(model.Title) && IsTitle(shape)) model.Title = extracted.Text;
            }
            return model;
        }

        private static bool IsTitle(PowerPoint.Shape shape)
        {
            try
            {
                var type = shape.PlaceholderFormat.Type;
                return type == PowerPoint.PpPlaceholderType.ppPlaceholderTitle || type == PowerPoint.PpPlaceholderType.ppPlaceholderCenterTitle;
            }
            catch { return false; }
        }
    }
}
