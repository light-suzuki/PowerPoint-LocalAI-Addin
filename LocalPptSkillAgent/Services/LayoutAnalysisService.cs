using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class LayoutAnalysisService
    {
        public IReadOnlyList<LayoutIssue> Analyze(IReadOnlyList<SlideModel> slides)
        {
            var issues = new List<LayoutIssue>();
            foreach (var slide in slides ?? new List<SlideModel>())
            {
                var shapes = slide.Shapes ?? new List<ShapeModel>();
                foreach (var shape in shapes)
                {
                    var bounds = shape.Bounds ?? new RectModel { Left = shape.Left, Top = shape.Top, Width = shape.Width, Height = shape.Height };
                    if (GeometryUtils.IsOutsideSlide(bounds, slide.Width, slide.Height))
                        issues.Add(Issue("out_of_slide", "error", "Shapeがスライド外にはみ出しています。", slide.SlideIndex, shape.Id));
                    if (shape.HasText && shape.FontSize.HasValue && shape.FontSize.Value < 12)
                        issues.Add(Issue("unreadable_text", "error", $"文字サイズが{shape.FontSize.Value:0.#}ptです。", slide.SlideIndex, shape.Id));
                    if (shape.HasText && (shape.Text?.Length ?? 0) > 320)
                        issues.Add(Issue("text_density", "warning", "1つのShape内の文字量が多すぎます。", slide.SlideIndex, shape.Id));
                    if (shape.HasText && shape.Width * shape.Height < 1200 && (shape.Text?.Length ?? 0) > 40)
                        issues.Add(Issue("too_small_textbox", "warning", "文字量に対してTextBoxが小さい可能性があります。", slide.SlideIndex, shape.Id));
                }
                for (var i = 0; i < shapes.Count; i++) for (var j = i + 1; j < shapes.Count; j++)
                {
                    var a = shapes[i]; var b = shapes[j];
                    if (SkipPair(a, b, slide.Width, slide.Height)) continue;
                    var ra = a.Bounds ?? new RectModel { Left = a.Left, Top = a.Top, Width = a.Width, Height = a.Height };
                    var rb = b.Bounds ?? new RectModel { Left = b.Left, Top = b.Top, Width = b.Width, Height = b.Height };
                    var overlapRatio = OverlapRatio(ra, rb);
                    if (overlapRatio >= .15f && overlapRatio < .9f)
                        issues.Add(Issue("overlap", "warning", "Shape同士が大きく重なっています。意図的な重なりか確認してください。", slide.SlideIndex, a.Id, b.Id));
                    else if (overlapRatio == 0 && TooClose(ra, rb, 8))
                        issues.Add(Issue("too_close", "info", "Shape間隔が8pt未満です。", slide.SlideIndex, a.Id, b.Id));
                }
            }
            return issues;
        }

        private static bool SkipPair(ShapeModel a, ShapeModel b, float slideWidth, float slideHeight)
        {
            if (a.IsLine || b.IsLine || a.IsGroup || b.IsGroup) return true;
            return a.Width * a.Height > slideWidth * slideHeight * .75f || b.Width * b.Height > slideWidth * slideHeight * .75f;
        }
        private static float OverlapRatio(RectModel a, RectModel b)
        {
            var width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
            var height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
            var area = width * height; var smaller = Math.Min(a.Width * a.Height, b.Width * b.Height);
            return smaller <= 0 ? 0 : area / smaller;
        }
        private static bool TooClose(RectModel a, RectModel b, float gap)
        {
            var verticalProjection = a.Top < b.Bottom && a.Bottom > b.Top;
            var horizontalProjection = a.Left < b.Right && a.Right > b.Left;
            return verticalProjection && GeometryUtils.HorizontalGap(a, b) > 0 && GeometryUtils.HorizontalGap(a, b) < gap ||
                   horizontalProjection && GeometryUtils.VerticalGap(a, b) > 0 && GeometryUtils.VerticalGap(a, b) < gap;
        }
        private static LayoutIssue Issue(string type, string severity, string message, int slide, params int[] ids) => new LayoutIssue
        { IssueType = type, Severity = severity, Message = message, Slide = slide, ShapeIds = ids.ToList() };
    }
}
