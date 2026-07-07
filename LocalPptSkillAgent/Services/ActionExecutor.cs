using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class ActionExecutor
    {
        private readonly PowerPointService _powerPoint;
        private readonly ActionValidator _validator;
        private readonly TransactionManager _transactions;

        public ActionExecutor(PowerPointService powerPoint)
        {
            _powerPoint = powerPoint;
            _validator = new ActionValidator();
            _transactions = new TransactionManager(powerPoint);
        }

        public AiEditSession Execute(LayoutActionPlan plan, TargetSelector target, string instruction)
        {
            var validation = _validator.Validate(plan, target, _powerPoint);
            if (!validation.IsValid) throw new InvalidOperationException("ActionPlanを適用できません:\r\n" + string.Join("\r\n", validation.Errors));
            var session = _transactions.Begin(plan, instruction);
            var created = new List<Tuple<int, PowerPoint.Shape>>();
            var insertionOffsets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _powerPoint.StartUndoEntry();
            try
            {
                for (var i = 0; i < plan.Actions.Count; i++) ExecuteOne(plan.Actions[i], session, i + 1, created, insertionOffsets);
                _transactions.Complete(session, created);
                return session;
            }
            catch (Exception ex)
            {
                _transactions.Abort(session, created, ex);
                throw new InvalidOperationException("AI編集の途中で問題が発生したため、適用済みの変更を自動的に戻しました。", ex);
            }
        }

        private void ExecuteOne(LayoutAction action, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created, Dictionary<string, int> insertionOffsets)
        {
            var shape = action.ShapeId.HasValue ? _powerPoint.FindShape(action.Slide, action.ShapeId.Value) : null;
            switch (action.Type)
            {
                case "move": _powerPoint.Move(shape, action.Dx ?? 0, action.Dy ?? 0); break;
                case "resize": _powerPoint.Resize(shape, action.Left, action.Top, action.Width, action.Height); break;
                case "set_font_size": shape.TextFrame.TextRange.Font.Size = action.FontSize.Value; break;
                case "set_text": shape.TextFrame.TextRange.Text = action.Text ?? string.Empty; break;
                case "align": _powerPoint.Align(GetShapes(action), action.AlignMode); break;
                case "distribute": _powerPoint.Distribute(GetShapes(action), action.DistributeMode); break;
                case "create_textbox": AddCreated(action.Slide, _powerPoint.AddTextBox(_powerPoint.GetSlide(action.Slide), action.Text, action.Left ?? 36, action.Top ?? 90, action.Width ?? 240, action.Height ?? 60), session, actionNumber, created); break;
                case "create_shape": AddCreated(action.Slide, _powerPoint.AddShape(_powerPoint.GetSlide(action.Slide), Office.MsoAutoShapeType.msoShapeRoundedRectangle, action.Text, action.Left ?? 36, action.Top ?? 90, action.Width ?? 180, action.Height ?? 60), session, actionNumber, created); break;
                case "create_line": AddCreated(action.Slide, _powerPoint.AddLine(_powerPoint.GetSlide(action.Slide), action.Left ?? 0, action.Top ?? 0, action.Width ?? 100, action.Height ?? 100, false), session, actionNumber, created); break;
                case "create_arrow": AddCreated(action.Slide, _powerPoint.AddLine(_powerPoint.GetSlide(action.Slide), action.Left ?? 0, action.Top ?? 0, action.Width ?? 100, action.Height ?? 100, true), session, actionNumber, created); break;
                case "create_diagram": CreateDiagram(action.Diagram, session, actionNumber, created); break;
                case "create_slide": CreateSlide(action.SlidePlan, session, actionNumber, created, insertionOffsets); break;
                case "reorder_slide": _powerPoint.ReorderSlide(action.Slide, (int)(action.Top ?? action.Slide)); break;
                case "add_notes": PowerPointService.SetNotes(_powerPoint.GetSlide(action.Slide), action.Text); break;
                case "add_citation_note": PowerPointService.SetNotes(_powerPoint.GetSlide(action.Slide), PowerPointService.GetNotes(_powerPoint.GetSlide(action.Slide)) + "\r\n" + action.Text); break;
                case "tag_shape": break;
                default: throw new InvalidOperationException("未対応Action: " + action.Type);
            }
            if (shape != null) Tag(shape.Tags, session.SessionId, actionNumber, false);
            if (action.Slide > 0 && action.Slide <= _powerPoint.CurrentPresentation.Slides.Count)
            {
                var slide = _powerPoint.GetSlide(action.Slide);
                PowerPointService.SetTag(slide.Tags, PowerPointService.AiTouched, "true");
                PowerPointService.SetTag(slide.Tags, PowerPointService.SessionId, session.SessionId);
            }
        }

        private IReadOnlyList<PowerPoint.Shape> GetShapes(LayoutAction action)
        {
            var ids = action.ShapeIds?.ToList() ?? new List<int>();
            if (action.ShapeId.HasValue && !ids.Contains(action.ShapeId.Value)) ids.Add(action.ShapeId.Value);
            return ids.Select(x => _powerPoint.FindShape(action.Slide, x)).Where(x => x != null).ToList();
        }

        private void CreateDiagram(DiagramPlan plan, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            if (plan == null) throw new InvalidOperationException("DiagramPlanがありません。");
            var slide = _powerPoint.GetSlide(plan.Slide);
            var byId = new Dictionary<string, PowerPoint.Shape>(StringComparer.OrdinalIgnoreCase);
            var definitions = plan.Objects.Where(x => x.Type != "line" && x.Type != "arrow" && x.Type != "connector" && !string.IsNullOrWhiteSpace(x.Id))
                .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var item in plan.Objects.Where(x => x.Type == "line" || x.Type == "arrow" || x.Type == "connector"))
            {
                definitions.TryGetValue(item.From ?? string.Empty, out var from);
                definitions.TryGetValue(item.To ?? string.Empty, out var to);
                var x1 = from == null ? item.X : from.X + from.W / 2;
                var y1 = from == null ? item.Y : from.Y + from.H / 2;
                var x2 = to == null ? item.W : to.X + to.W / 2;
                var y2 = to == null ? item.H : to.Y + to.H / 2;
                AddCreated(plan.Slide, _powerPoint.AddLine(slide, x1, y1, x2, y2, item.Type != "line"), session, actionNumber, created);
            }
            foreach (var item in plan.Objects.Where(x => x.Type != "line" && x.Type != "arrow" && x.Type != "connector"))
            {
                PowerPoint.Shape shape;
                if (item.Type == "text") shape = _powerPoint.AddTextBox(slide, item.Text, item.X, item.Y, item.W, item.H);
                else shape = _powerPoint.AddShape(slide, ShapeType(item.Type), item.Text, item.X, item.Y, item.W, item.H);
                if (!string.IsNullOrWhiteSpace(item.Id)) byId[item.Id] = shape;
                ApplyStyle(shape, item.StyleRole);
                AddCreated(plan.Slide, shape, session, actionNumber, created);
            }
        }

        private void CreateSlide(SlideGenerationPlan plan, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created, Dictionary<string, int> insertionOffsets)
        {
            if (plan == null) throw new InvalidOperationException("SlideGenerationPlanがありません。");
            var count = _powerPoint.CurrentPresentation.Slides.Count;
            int index;
            if (plan.InsertAfter.HasValue)
            {
                var key = "after:" + plan.InsertAfter.Value; insertionOffsets.TryGetValue(key, out var offset);
                index = Math.Min(count + 1, plan.InsertAfter.Value + 1 + offset); insertionOffsets[key] = offset + 1;
            }
            else if (plan.InsertBefore.HasValue)
            {
                var key = "before:" + plan.InsertBefore.Value; insertionOffsets.TryGetValue(key, out var offset);
                index = Math.Min(count + 1, plan.InsertBefore.Value + offset); insertionOffsets[key] = offset + 1;
            }
            else index = count + 1;
            var slide = _powerPoint.AddSlide(index);
            session.CreatedSlideIds.Add(slide.SlideID);
            PowerPointService.SetTag(slide.Tags, PowerPointService.AiTouched, "true");
            PowerPointService.SetTag(slide.Tags, PowerPointService.SessionId, session.SessionId);
            var slideWidth = _powerPoint.CurrentPresentation.PageSetup.SlideWidth;
            var slideHeight = _powerPoint.CurrentPresentation.PageSetup.SlideHeight;
            var title = _powerPoint.AddTextBox(slide, plan.Title, 42, 26, slideWidth - 84, 54);
            title.TextFrame.TextRange.Font.Size = 34;
            AddCreated(slide.SlideIndex, title, session, actionNumber, created);
            var items = SplitBody(plan.Body);
            var mode = ((plan.Composition ?? string.Empty) + " " + (plan.VisualType ?? string.Empty) + " " + (plan.Layout ?? string.Empty)).ToLowerInvariant();
            if (mode.Contains("three_step") || mode.Contains("process") || mode.Contains("method_flow"))
                RenderProcess(slide, items, plan.Message, slideWidth, slideHeight, session, actionNumber, created);
            else if (mode.Contains("three_cards") || mode.Contains("summary"))
                RenderCards(slide, items, plan.Message, slideWidth, slideHeight, session, actionNumber, created);
            else if (mode.Contains("two_column") || mode.Contains("comparison") || mode.Contains("text_visual_split") || mode.Contains("result_with_figure"))
                RenderTwoColumn(slide, items, plan.Message, plan.VisualBrief, slideWidth, slideHeight, session, actionNumber, created);
            else if (mode.Contains("title_only") || mode.Contains("section_header") || mode.Contains("single_focus") || mode.Contains("key_takeaway"))
                RenderSingleFocus(slide, plan.Message, slideWidth, slideHeight, session, actionNumber, created);
            else
                RenderStandard(slide, items, plan.Message, slideWidth, slideHeight, session, actionNumber, created);
            if (plan.Diagram != null) { plan.Diagram.Slide = slide.SlideIndex; CreateDiagram(plan.Diagram, session, actionNumber, created); }
            if (!string.IsNullOrWhiteSpace(plan.Notes) || !string.IsNullOrWhiteSpace(plan.CitationNote))
                PowerPointService.SetNotes(slide, (plan.Notes ?? string.Empty) + "\r\n" + (plan.CitationNote ?? string.Empty));
        }

        private void RenderStandard(PowerPoint.Slide slide, IList<string> items, string message, float width, float height, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            AddText(slide, message, 48, 100, width - 96, 48, 22, session, actionNumber, created);
            AddText(slide, BulletText(items), 56, 168, width - 112, height - 210, 18, session, actionNumber, created);
        }

        private void RenderSingleFocus(PowerPoint.Slide slide, string message, float width, float height, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            AddText(slide, message, width * 0.12f, height * 0.32f, width * 0.76f, height * 0.28f, 28, session, actionNumber, created);
        }

        private void RenderTwoColumn(PowerPoint.Slide slide, IList<string> items, string message, string visualBrief, float width, float height, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            var gap = 28f; var column = (width - 96 - gap) / 2;
            AddText(slide, message, 48, 100, column, 78, 22, session, actionNumber, created);
            AddText(slide, BulletText(items.Take((items.Count + 1) / 2)), 48, 190, column, height - 230, 18, session, actionNumber, created);
            var rightItems = items.Skip((items.Count + 1) / 2).ToList();
            var rightText = rightItems.Count > 0 ? BulletText(rightItems) : visualBrief;
            AddCard(slide, rightText, 48 + column + gap, 112, column, height - 170, 18, session, actionNumber, created);
        }

        private void RenderCards(PowerPoint.Slide slide, IList<string> items, string message, float width, float height, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            AddText(slide, message, 48, 96, width - 96, 52, 22, session, actionNumber, created);
            var count = Math.Max(1, Math.Min(3, items.Count)); var gap = 18f; var cardWidth = (width - 96 - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++) AddCard(slide, items[i], 48 + i * (cardWidth + gap), 178, cardWidth, height - 230, 18, session, actionNumber, created);
        }

        private void RenderProcess(PowerPoint.Slide slide, IList<string> items, string message, float width, float height, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            AddText(slide, message, 48, 96, width - 96, 52, 22, session, actionNumber, created);
            var count = Math.Max(1, Math.Min(4, items.Count)); var gap = 30f; var cardWidth = (width - 96 - gap * (count - 1)) / count; var y = height * 0.38f;
            for (var i = 0; i < count - 1; i++) AddCreated(slide.SlideIndex, _powerPoint.AddLine(slide, 48 + cardWidth + i * (cardWidth + gap), y + 48, 48 + cardWidth + gap + i * (cardWidth + gap), y + 48, true), session, actionNumber, created);
            for (var i = 0; i < count; i++) AddCard(slide, items[i], 48 + i * (cardWidth + gap), y, cardWidth, 96, 17, session, actionNumber, created);
        }

        private void AddText(PowerPoint.Slide slide, string text, float left, float top, float width, float height, float fontSize, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var shape = _powerPoint.AddTextBox(slide, text, left, top, width, height); shape.TextFrame.TextRange.Font.Size = fontSize;
            AddCreated(slide.SlideIndex, shape, session, actionNumber, created);
        }

        private void AddCard(PowerPoint.Slide slide, string text, float left, float top, float width, float height, float fontSize, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var shape = _powerPoint.AddShape(slide, Office.MsoAutoShapeType.msoShapeRectangle, text, left, top, width, height);
            shape.Fill.ForeColor.RGB = 0xEDEDED; shape.Line.Visible = Office.MsoTriState.msoFalse; shape.TextFrame.TextRange.Font.Size = fontSize;
            AddCreated(slide.SlideIndex, shape, session, actionNumber, created);
        }

        private static List<string> SplitBody(string body) => (body ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('•', '-', ' ')).Where(x => x.Length > 0).ToList();
        private static string BulletText(IEnumerable<string> items) => string.Join("\r\n", (items ?? Enumerable.Empty<string>()).Select(x => "• " + x));

        private static Office.MsoAutoShapeType ShapeType(string type)
        {
            switch (type)
            {
                case "oval": return Office.MsoAutoShapeType.msoShapeOval;
                case "rectangle": return Office.MsoAutoShapeType.msoShapeRectangle;
                default: return Office.MsoAutoShapeType.msoShapeRoundedRectangle;
            }
        }
        private static void ApplyStyle(PowerPoint.Shape shape, string role)
        {
            if (shape.HasTextFrame == Office.MsoTriState.msoTrue) shape.TextFrame.TextRange.Font.Size = 16;
            if (role == "emphasis") shape.Fill.ForeColor.RGB = 0xE6A23C;
        }
        private static void Tag(PowerPoint.Tags tags, string sessionId, int actionNumber, bool created)
        {
            PowerPointService.SetTag(tags, PowerPointService.AiTouched, "true");
            PowerPointService.SetTag(tags, PowerPointService.SessionId, sessionId);
            PowerPointService.SetTag(tags, PowerPointService.ActionId, actionNumber.ToString());
            PowerPointService.SetTag(tags, PowerPointService.CreatedByAi, created ? "true" : "false");
        }
        private static void AddCreated(int slide, PowerPoint.Shape shape, AiEditSession session, int actionNumber, List<Tuple<int, PowerPoint.Shape>> created)
        {
            Tag(shape.Tags, session.SessionId, actionNumber, true);
            created.Add(Tuple.Create(slide, shape));
        }
    }
}
