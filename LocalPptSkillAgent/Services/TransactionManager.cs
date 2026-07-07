using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class TransactionManager
    {
        private readonly PowerPointService _powerPoint;
        public TransactionManager(PowerPointService powerPoint) { _powerPoint = powerPoint; }

        public AiEditSession Begin(LayoutActionPlan plan, string instruction)
        {
            var session = new AiEditSession
            {
                SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_") + Guid.NewGuid().ToString("N").Substring(0, 4),
                CreatedAt = DateTime.Now,
                PresentationPath = SafePresentationPath(),
                DeckId = _powerPoint.GetOrCreateDeckId(),
                Skill = plan.Skill,
                Target = plan.Target,
                UserInstruction = instruction,
                ActionPlan = plan
            };
            var notes = new Dictionary<int, string>();
            foreach (var action in plan.Actions)
            {
                if (action.ShapeId.HasValue)
                {
                    var shape = _powerPoint.FindShape(action.Slide, action.ShapeId.Value);
                    if (shape != null && session.BeforeSnapshots.All(x => x.Slide != action.Slide || x.ShapeId != shape.Id))
                        session.BeforeSnapshots.Add(Snapshot(action.Slide, shape, false));
                }
                if (action.ShapeIds != null)
                    foreach (var id in action.ShapeIds)
                    {
                        var shape = _powerPoint.FindShape(action.Slide, id);
                        if (shape != null && session.BeforeSnapshots.All(x => x.Slide != action.Slide || x.ShapeId != id))
                            session.BeforeSnapshots.Add(Snapshot(action.Slide, shape, false));
                    }
                if (action.Type == "add_notes" || action.Type == "add_citation_note")
                    notes[action.Slide] = PowerPointService.GetNotes(_powerPoint.GetSlide(action.Slide));
            }
            session.BeforeNotesJson = JsonUtils.Serialize(notes);
            return session;
        }

        public void Complete(AiEditSession session, IEnumerable<Tuple<int, PowerPoint.Shape>> createdShapes)
        {
            foreach (var before in session.BeforeSnapshots)
            {
                var shape = _powerPoint.FindShape(before.Slide, before.ShapeId);
                if (shape != null) session.AfterSnapshots.Add(Snapshot(before.Slide, shape, false));
            }
            foreach (var item in createdShapes)
                session.AfterSnapshots.Add(Snapshot(item.Item1, item.Item2, true));
            var noteSlides = JsonUtils.Deserialize<Dictionary<int, string>>(session.BeforeNotesJson ?? "{}") ?? new Dictionary<int, string>();
            session.AfterNotesJson = JsonUtils.Serialize(noteSlides.Keys.ToDictionary(x => x, x => PowerPointService.GetNotes(_powerPoint.GetSlide(x))));
            FileStoreUtils.SaveSession(session);
        }

        public void Abort(AiEditSession session, IEnumerable<Tuple<int, PowerPoint.Shape>> createdShapes, Exception failure)
        {
            var errors = new List<string>();
            foreach (var item in (createdShapes ?? Enumerable.Empty<Tuple<int, PowerPoint.Shape>>()).Reverse())
                try { item.Item2.Delete(); } catch (Exception ex) { errors.Add("作成Shapeの削除: " + ex.Message); }
            foreach (var before in session.BeforeSnapshots)
            {
                try
                {
                    var shape = _powerPoint.FindShape(before.Slide, before.ShapeId);
                    if (shape == null) continue;
                    shape.Left = before.Left; shape.Top = before.Top; shape.Width = before.Width; shape.Height = before.Height;
                    if (before.Text != null)
                    {
                        shape.TextFrame.TextRange.Text = before.Text;
                        if (before.FontSize.HasValue) shape.TextFrame.TextRange.Font.Size = before.FontSize.Value;
                        if (!string.IsNullOrWhiteSpace(before.FontName)) shape.TextFrame.TextRange.Font.Name = before.FontName;
                    }
                }
                catch (Exception ex) { errors.Add($"Slide {before.Slide} Shape {before.ShapeId}の復元: {ex.Message}"); }
            }
            foreach (var slideId in session.CreatedSlideIds.ToList())
                try
                {
                    foreach (PowerPoint.Slide slide in _powerPoint.CurrentPresentation.Slides)
                        if (slide.SlideID == slideId) { slide.Delete(); break; }
                }
                catch (Exception ex) { errors.Add("作成Slideの削除: " + ex.Message); }
            try
            {
                var notes = JsonUtils.Deserialize<Dictionary<int, string>>(session.BeforeNotesJson ?? "{}") ?? new Dictionary<int, string>();
                foreach (var pair in notes) if (pair.Key <= _powerPoint.CurrentPresentation.Slides.Count) PowerPointService.SetNotes(_powerPoint.GetSlide(pair.Key), pair.Value);
            }
            catch (Exception ex) { errors.Add("Speaker Notesの復元: " + ex.Message); }
            session.RolledBack = errors.Count == 0;
            session.Status = errors.Count == 0 ? "failed_rolled_back" : "failed_partial_rollback";
            session.FailureMessage = failure?.Message + (errors.Count == 0 ? string.Empty : "\r\n復元エラー:\r\n" + string.Join("\r\n", errors));
            FileStoreUtils.SaveSession(session);
            DiagnosticLogService.Error("action_transaction_aborted", failure, session.Status + " session=" + session.SessionId);
            if (errors.Count > 0) throw new InvalidOperationException("編集失敗後の自動復元が一部完了しませんでした。\r\n" + string.Join("\r\n", errors), failure);
        }

        public static ShapeSnapshot Snapshot(int slide, PowerPoint.Shape shape, bool created)
        {
            string text = null; float? size = null; string name = null;
            try
            {
                if (shape.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue && shape.TextFrame.HasText == Microsoft.Office.Core.MsoTriState.msoTrue)
                { text = shape.TextFrame.TextRange.Text; size = shape.TextFrame.TextRange.Font.Size; name = shape.TextFrame.TextRange.Font.Name; }
            }
            catch { }
            return new ShapeSnapshot
            {
                Slide = slide, ShapeId = shape.Id, ShapeName = shape.Name, Left = shape.Left, Top = shape.Top,
                Width = shape.Width, Height = shape.Height, Text = text, FontSize = size, FontName = name, CreatedByAi = created
            };
        }

        private string SafePresentationPath()
        {
            try { return _powerPoint.CurrentPresentation.FullName; } catch { return string.Empty; }
        }
    }
}
