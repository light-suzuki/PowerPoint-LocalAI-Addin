using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class ActionValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();
    }

    public class ActionValidator
    {
        private const int MaxActions = 200;
        private const int MaxDiagramObjects = 120;
        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "move", "resize", "align", "distribute", "set_font_size", "set_text", "create_shape", "create_textbox", "create_line", "create_arrow", "create_diagram", "create_slide", "reorder_slide", "add_notes", "add_citation_note", "tag_shape" };
        private static readonly HashSet<string> DiagramTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "rectangle", "rounded_rect", "oval", "text", "arrow", "line", "connector", "table" };

        public ActionValidationResult Validate(LayoutActionPlan plan, TargetSelector target, PowerPointService powerPoint)
        {
            var result = new ActionValidationResult();
            if (plan == null) { result.Errors.Add("ActionPlanがnullです。"); return result; }
            if (plan.Actions == null) { result.Errors.Add("Actionsがありません。"); return result; }
            if (plan.Actions.Count > MaxActions) { result.Errors.Add($"Action数が上限{MaxActions}件を超えています。"); return result; }
            var allowedSlides = new HashSet<int>(powerPoint.ResolveSlides(target).Select(x => x.SlideIndex));
            var presentation = powerPoint.CurrentPresentation;
            var slideWidth = presentation.PageSetup.SlideWidth; var slideHeight = presentation.PageSetup.SlideHeight;
            foreach (var action in plan.Actions)
            {
                if (!Allowed.Contains(action.Type ?? string.Empty)) { result.Errors.Add($"未許可Action: {action.Type}"); continue; }
                var createsSlide = action.Type.Equals("create_slide", StringComparison.OrdinalIgnoreCase);
                if (!createsSlide && !allowedSlides.Contains(action.Slide)) result.Errors.Add($"Slide {action.Slide} は指定範囲外です。");
                if (action.FontSize.HasValue && action.FontSize.Value < 12) result.Errors.Add($"Slide {action.Slide}: 12pt未満は適用できません。");
                if (action.FontSize.HasValue && (!Finite(action.FontSize.Value) || action.FontSize.Value > 200)) result.Errors.Add($"Slide {action.Slide}: FontSizeが不正です。");
                if ((action.Text?.Length ?? 0) > 50000) result.Errors.Add($"Slide {action.Slide}: Textが長すぎます。");
                foreach (var value in new[] { action.Dx, action.Dy, action.Left, action.Top, action.Width, action.Height })
                    if (value.HasValue && !Finite(value.Value)) result.Errors.Add($"Slide {action.Slide}: 座標にNaNまたはInfinityが含まれます。");
                if (action.ShapeId.HasValue)
                {
                    var shape = powerPoint.FindShape(action.Slide, action.ShapeId.Value);
                    if (shape == null) result.Errors.Add($"Slide {action.Slide}: shape_id {action.ShapeId} が存在しません。");
                    else
                    {
                        try { if (shape.LockAspectRatio == Microsoft.Office.Core.MsoTriState.msoTrue && action.Type == "resize") result.Errors.Add($"shape_id {action.ShapeId} は縦横比がロックされています。"); } catch { }
                        var candidate = new RectModel
                        {
                            Left = action.Left ?? shape.Left + (action.Dx ?? 0), Top = action.Top ?? shape.Top + (action.Dy ?? 0),
                            Width = action.Width ?? shape.Width, Height = action.Height ?? shape.Height
                        };
                        if ((action.Type == "move" || action.Type == "resize") && GeometryUtils.IsOutsideSlide(candidate, slideWidth, slideHeight))
                            result.Errors.Add($"shape_id {action.ShapeId} がスライド外に出ます。");
                    }
                }
                foreach (var id in action.ShapeIds ?? new List<int>())
                    if (powerPoint.FindShape(action.Slide, id) == null) result.Errors.Add($"Slide {action.Slide}: shape_id {id} が存在しません。");
                if ((action.Type == "set_text" || action.Type == "set_font_size" || action.Type == "tag_shape") && !action.ShapeId.HasValue)
                    result.Errors.Add($"Slide {action.Slide}: {action.Type}にはshape_idが必要です。");
                if (action.Type == "set_text" && action.Text == null) result.Errors.Add($"Slide {action.Slide}: set_textにtextがありません。");
                if (action.Type == "set_font_size" && !action.FontSize.HasValue) result.Errors.Add($"Slide {action.Slide}: set_font_sizeにfont_sizeがありません。");
                if (action.Type == "align")
                {
                    var modes = new[] { "left", "right", "top", "bottom", "center_horizontal", "center_vertical" };
                    if (!modes.Contains(action.AlignMode ?? string.Empty, StringComparer.OrdinalIgnoreCase)) result.Errors.Add($"Slide {action.Slide}: align_modeが不正です。");
                    if ((action.ShapeIds?.Count ?? 0) + (action.ShapeId.HasValue ? 1 : 0) < 2) result.Errors.Add($"Slide {action.Slide}: alignには2個以上のShapeが必要です。");
                }
                if (action.Type == "distribute")
                {
                    var modes = new[] { "horizontal", "vertical", "distribute_horizontal", "distribute_vertical" };
                    if (!modes.Contains(action.DistributeMode ?? string.Empty, StringComparer.OrdinalIgnoreCase)) result.Errors.Add($"Slide {action.Slide}: distribute_modeが不正です。");
                    if ((action.ShapeIds?.Count ?? 0) + (action.ShapeId.HasValue ? 1 : 0) < 3) result.Errors.Add($"Slide {action.Slide}: distributeには3個以上のShapeが必要です。");
                }
                if ((action.Type == "add_notes" || action.Type == "add_citation_note") && action.Text == null) result.Errors.Add($"Slide {action.Slide}: Notes textがありません。");
                if (action.Type == "create_shape" || action.Type == "create_textbox")
                {
                    var rect = new RectModel { Left = action.Left ?? 36, Top = action.Top ?? 90, Width = action.Width ?? (action.Type == "create_textbox" ? 240 : 180), Height = action.Height ?? 60 };
                    if (rect.Width <= 0 || rect.Height <= 0 || GeometryUtils.IsOutsideSlide(rect, slideWidth, slideHeight)) result.Errors.Add($"Slide {action.Slide}: 新規Shapeの配置が不正です。");
                }
                if (action.Type == "create_line" || action.Type == "create_arrow")
                {
                    var x1 = action.Left ?? 0; var y1 = action.Top ?? 0; var x2 = action.Width ?? 100; var y2 = action.Height ?? 100;
                    if (!InsidePoint(x1, y1, slideWidth, slideHeight) || !InsidePoint(x2, y2, slideWidth, slideHeight)) result.Errors.Add($"Slide {action.Slide}: 線の端点がスライド外です。");
                }
                if (action.Type == "create_diagram") ValidateDiagram(action, slideWidth, slideHeight, result);
                if (createsSlide) ValidateSlidePlan(action, presentation.Slides.Count, result);
                if (action.Type == "reorder_slide")
                {
                    var destination = action.Top ?? action.Slide;
                    if (!Finite(destination) || destination != Math.Truncate(destination) || destination < 1 || destination > presentation.Slides.Count)
                        result.Errors.Add($"Slide {action.Slide}: 並べ替え先が不正です。");
                }
            }
            return result;
        }

        private static void ValidateDiagram(LayoutAction action, float slideWidth, float slideHeight, ActionValidationResult result)
        {
            var plan = action.Diagram;
            if (plan == null) { result.Errors.Add($"Slide {action.Slide}: DiagramPlanがありません。"); return; }
            if (plan.Slide != action.Slide) result.Errors.Add($"Slide {action.Slide}: DiagramPlanのslideが一致しません。");
            if (plan.Objects == null || plan.Objects.Count == 0) { result.Errors.Add($"Slide {action.Slide}: Diagram objectがありません。"); return; }
            if (plan.Objects.Count > MaxDiagramObjects) result.Errors.Add($"Slide {action.Slide}: Diagram objectが上限{MaxDiagramObjects}件を超えています。");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in plan.Objects)
            {
                if (!DiagramTypes.Contains(item.Type ?? string.Empty)) result.Errors.Add($"Slide {action.Slide}: 未対応Diagram object type: {item.Type}");
                if (!Finite(item.X) || !Finite(item.Y) || !Finite(item.W) || !Finite(item.H)) { result.Errors.Add($"Slide {action.Slide}: Diagram座標が不正です。"); continue; }
                if (!string.IsNullOrWhiteSpace(item.Id) && !ids.Add(item.Id)) result.Errors.Add($"Slide {action.Slide}: Diagram object idが重複しています: {item.Id}");
                if (item.Type != "line" && item.Type != "arrow" && item.Type != "connector")
                {
                    var rect = new RectModel { Left = item.X, Top = item.Y, Width = item.W, Height = item.H };
                    if (rect.Width <= 0 || rect.Height <= 0 || GeometryUtils.IsOutsideSlide(rect, slideWidth, slideHeight)) result.Errors.Add($"Slide {action.Slide}: Diagram object {item.Id}がスライド外です。");
                }
            }
            foreach (var item in plan.Objects.Where(x => x.Type == "line" || x.Type == "arrow" || x.Type == "connector"))
            {
                if (!string.IsNullOrWhiteSpace(item.From) && !ids.Contains(item.From)) result.Errors.Add($"Slide {action.Slide}: 接続元 {item.From} が存在しません。");
                if (!string.IsNullOrWhiteSpace(item.To) && !ids.Contains(item.To)) result.Errors.Add($"Slide {action.Slide}: 接続先 {item.To} が存在しません。");
                if (string.IsNullOrWhiteSpace(item.From) && !InsidePoint(item.X, item.Y, slideWidth, slideHeight)) result.Errors.Add($"Slide {action.Slide}: 線の開始点がスライド外です。");
                if (string.IsNullOrWhiteSpace(item.To) && !InsidePoint(item.W, item.H, slideWidth, slideHeight)) result.Errors.Add($"Slide {action.Slide}: 線の終了点がスライド外です。");
            }
        }

        private static void ValidateSlidePlan(LayoutAction action, int slideCount, ActionValidationResult result)
        {
            var plan = action.SlidePlan;
            if (plan == null) { result.Errors.Add("create_slideにSlideGenerationPlanがありません。"); return; }
            if (plan.InsertAfter.HasValue && plan.InsertBefore.HasValue) result.Errors.Add("InsertAfterとInsertBeforeは同時指定できません。");
            if (plan.InsertAfter.HasValue && (plan.InsertAfter < 0 || plan.InsertAfter > slideCount)) result.Errors.Add("InsertAfterが範囲外です。");
            if (plan.InsertBefore.HasValue && (plan.InsertBefore < 1 || plan.InsertBefore > slideCount + 1)) result.Errors.Add("InsertBeforeが範囲外です。");
            if (string.IsNullOrWhiteSpace(plan.Title)) result.Errors.Add("新規スライドのTitleがありません。");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool InsidePoint(float x, float y, float width, float height) => x >= 0 && y >= 0 && x <= width && y <= height;
    }
}
