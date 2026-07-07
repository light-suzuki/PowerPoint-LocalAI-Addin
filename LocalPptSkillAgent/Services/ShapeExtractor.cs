using System;
using LocalPptSkillAgent.Models;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class ShapeExtractor
    {
        public ShapeModel Extract(PowerPoint.Shape shape)
        {
            var hasText = false;
            var text = string.Empty;
            float? fontSize = null;
            string fontName = null;
            try
            {
                hasText = shape.HasTextFrame == Office.MsoTriState.msoTrue && shape.TextFrame.HasText == Office.MsoTriState.msoTrue;
                if (hasText)
                {
                    text = shape.TextFrame.TextRange.Text;
                    fontSize = shape.TextFrame.TextRange.Font.Size;
                    fontName = shape.TextFrame.TextRange.Font.Name;
                }
            }
            catch { hasText = false; }

            var type = shape.Type;
            return new ShapeModel
            {
                Id = shape.Id, Name = shape.Name, Type = type.ToString(), Left = shape.Left, Top = shape.Top,
                Width = shape.Width, Height = shape.Height, HasText = hasText, Text = text, FontSize = fontSize,
                FontName = fontName, ZOrderPosition = shape.ZOrderPosition,
                IsPicture = type == Office.MsoShapeType.msoPicture || type == Office.MsoShapeType.msoLinkedPicture,
                IsTextBox = type == Office.MsoShapeType.msoTextBox,
                IsLine = type == Office.MsoShapeType.msoLine,
                IsGroup = type == Office.MsoShapeType.msoGroup,
                IsChart = type == Office.MsoShapeType.msoChart,
                IsTable = SafeHasTable(shape),
                Bounds = new RectModel { Left = shape.Left, Top = shape.Top, Width = shape.Width, Height = shape.Height }
            };
        }

        private static bool SafeHasTable(PowerPoint.Shape shape)
        {
            try { return shape.HasTable == Office.MsoTriState.msoTrue; } catch { return false; }
        }
    }
}
