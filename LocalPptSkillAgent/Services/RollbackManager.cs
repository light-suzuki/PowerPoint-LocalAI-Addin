using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class RollbackManager
    {
        private readonly PowerPointService _powerPoint;
        public RollbackManager(PowerPointService powerPoint) { _powerPoint = powerPoint; }

        public AiEditSession RollbackLast()
        {
            var session = FileStoreUtils.ListSessions().FirstOrDefault(x => !x.RolledBack && SamePresentation(x));
            if (session == null) throw new InvalidOperationException("戻せるAI編集がありません。");
            Rollback(session);
            return session;
        }

        public void Rollback(string sessionId) => Rollback(FileStoreUtils.LoadSession(sessionId));

        public void Rollback(AiEditSession session)
        {
            if (session.RolledBack) throw new InvalidOperationException("このAI編集は既にロールバック済みです。");
            _powerPoint.StartUndoEntry();
            foreach (var created in session.AfterSnapshots.Where(x => x.CreatedByAi))
            {
                var shape = _powerPoint.FindShape(created.Slide, created.ShapeId);
                if (shape != null && PowerPointService.GetTag(shape.Tags, PowerPointService.SessionId) == session.SessionId) shape.Delete();
            }
            foreach (var before in session.BeforeSnapshots)
            {
                var shape = _powerPoint.FindShape(before.Slide, before.ShapeId) ?? FindByName(before.Slide, before.ShapeName);
                if (shape == null) continue;
                shape.Left = before.Left; shape.Top = before.Top; shape.Width = before.Width; shape.Height = before.Height;
                if (before.Text != null)
                {
                    shape.TextFrame.TextRange.Text = before.Text;
                    if (before.FontSize.HasValue) shape.TextFrame.TextRange.Font.Size = before.FontSize.Value;
                    if (!string.IsNullOrWhiteSpace(before.FontName)) shape.TextFrame.TextRange.Font.Name = before.FontName;
                }
            }
            foreach (var slideId in session.CreatedSlideIds)
            {
                var slide = FindSlideById(slideId);
                if (slide != null && PowerPointService.GetTag(slide.Tags, PowerPointService.SessionId) == session.SessionId) slide.Delete();
            }
            var notes = JsonUtils.Deserialize<Dictionary<int, string>>(session.BeforeNotesJson ?? "{}") ?? new Dictionary<int, string>();
            foreach (var pair in notes) if (pair.Key <= _powerPoint.CurrentPresentation.Slides.Count) PowerPointService.SetNotes(_powerPoint.GetSlide(pair.Key), pair.Value);
            session.RolledBack = true;
            FileStoreUtils.SaveSession(session);
        }

        private PowerPoint.Shape FindByName(int slide, string name)
        {
            try { return _powerPoint.GetSlide(slide).Shapes[name]; } catch { return null; }
        }
        private PowerPoint.Slide FindSlideById(int id)
        {
            foreach (PowerPoint.Slide slide in _powerPoint.CurrentPresentation.Slides) if (slide.SlideID == id) return slide;
            return null;
        }
        private bool SamePresentation(AiEditSession session)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(session.DeckId)) return string.Equals(session.DeckId, _powerPoint.GetOrCreateDeckId(), StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(session.PresentationPath)) return false;
                return string.Equals(session.PresentationPath, _powerPoint.CurrentPresentation.FullName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
