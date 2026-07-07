using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class PowerPointService
    {
        public const string AiTouched = "LPSA_AI_TOUCHED";
        public const string SessionId = "LPSA_SESSION_ID";
        public const string ActionId = "LPSA_ACTION_ID";
        public const string CreatedByAi = "LPSA_CREATED_BY_AI";
        public const string DeckIdProperty = "LPSA_DECK_ID";
        private readonly PowerPoint.Application _app;
        private readonly SlideExtractor _extractor = new SlideExtractor();
        private readonly SlideImageExporter _exporter = new SlideImageExporter();

        public PowerPointService(PowerPoint.Application app) { _app = app ?? throw new ArgumentNullException(nameof(app)); }
        public PowerPoint.Application Application => _app;
        public PowerPoint.Presentation CurrentPresentation => _app.ActivePresentation ?? throw new InvalidOperationException("開いているプレゼンテーションがありません。");
        public PowerPoint.Slide CurrentSlide => _app.ActiveWindow?.View?.Slide as PowerPoint.Slide ?? throw new InvalidOperationException("現在スライドを取得できません。");
        public PowerPoint.Slide GetSlide(int index) => CurrentPresentation.Slides[index];

        public string GetOrCreateDeckId()
        {
            try
            {
                var properties = (Office.DocumentProperties)CurrentPresentation.CustomDocumentProperties;
                try
                {
                    var existing = Convert.ToString(properties[DeckIdProperty].Value);
                    if (!string.IsNullOrWhiteSpace(existing)) return existing;
                }
                catch { }
                var id = Guid.NewGuid().ToString("N");
                properties.Add(DeckIdProperty, false, Office.MsoDocProperties.msoPropertyTypeString, id);
                return id;
            }
            catch
            {
                if (CurrentPresentation.Slides.Count == 0) throw new InvalidOperationException("デッキ識別子を作成できません。スライドを1枚以上用意してください。");
                var tags = CurrentPresentation.Slides[1].Tags;
                var existing = GetTag(tags, DeckIdProperty);
                if (!string.IsNullOrWhiteSpace(existing)) return existing;
                var id = Guid.NewGuid().ToString("N");
                SetTag(tags, DeckIdProperty, id);
                return id;
            }
        }

        public IReadOnlyList<PowerPoint.Slide> ResolveSlides(TargetSelector target)
        {
            var presentation = CurrentPresentation;
            if (target.Type == "all" || target.Type == "figures" || target.Type == "text" || target.Type == "notes")
                return Enumerable.Range(1, presentation.Slides.Count).Select(i => presentation.Slides[i]).ToList();
            if (target.Type == "slide") return new[] { GetSlide(target.SlideIndex.Value) };
            if (target.Type == "slide_range")
                return Enumerable.Range(target.StartSlide.Value, target.EndSlide.Value - target.StartSlide.Value + 1).Select(GetSlide).ToList();
            return new[] { CurrentSlide };
        }

        public IReadOnlyList<PowerPoint.Shape> SelectedShapes()
        {
            var result = new List<PowerPoint.Shape>();
            var selection = _app.ActiveWindow?.Selection;
            if (selection == null || selection.Type != PowerPoint.PpSelectionType.ppSelectionShapes) return result;
            for (var i = 1; i <= selection.ShapeRange.Count; i++) result.Add(selection.ShapeRange[i]);
            return result;
        }

        public SlideModel ExtractSlide(PowerPoint.Slide slide, bool exportImage = false)
        {
            var model = _extractor.Extract(CurrentPresentation, slide);
            if (exportImage) model.ExportedImagePath = _exporter.Export(slide);
            return model;
        }

        public PowerPoint.Shape FindShape(int slide, int id)
        {
            foreach (PowerPoint.Shape shape in GetSlide(slide).Shapes) if (shape.Id == id) return shape;
            return null;
        }

        public void Move(PowerPoint.Shape shape, float dx, float dy) { shape.Left += dx; shape.Top += dy; }
        public void Resize(PowerPoint.Shape shape, float? left, float? top, float? width, float? height)
        {
            if (left.HasValue) shape.Left = left.Value;
            if (top.HasValue) shape.Top = top.Value;
            if (width.HasValue) shape.Width = width.Value;
            if (height.HasValue) shape.Height = height.Value;
        }

        public void Align(IReadOnlyList<PowerPoint.Shape> shapes, string mode)
        {
            if (shapes.Count < 2) return;
            var left = shapes.Min(x => x.Left); var right = shapes.Max(x => x.Left + x.Width);
            var top = shapes.Min(x => x.Top); var bottom = shapes.Max(x => x.Top + x.Height);
            foreach (var shape in shapes)
            {
                switch ((mode ?? string.Empty).ToLowerInvariant())
                {
                    case "left": shape.Left = left; break;
                    case "right": shape.Left = right - shape.Width; break;
                    case "top": shape.Top = top; break;
                    case "bottom": shape.Top = bottom - shape.Height; break;
                    case "center_horizontal": shape.Left = (left + right - shape.Width) / 2f; break;
                    case "center_vertical": shape.Top = (top + bottom - shape.Height) / 2f; break;
                    default: throw new InvalidOperationException("未対応の整列モードです: " + mode);
                }
            }
        }

        public void Distribute(IReadOnlyList<PowerPoint.Shape> shapes, string mode)
        {
            if (shapes.Count < 3) return;
            if ((mode ?? string.Empty).ToLowerInvariant().Contains("horizontal"))
            {
                var ordered = shapes.OrderBy(x => x.Left).ToList();
                var gap = (ordered.Last().Left + ordered.Last().Width - ordered.First().Left - ordered.Sum(x => x.Width)) / (ordered.Count - 1);
                var cursor = ordered.First().Left;
                foreach (var shape in ordered) { shape.Left = cursor; cursor += shape.Width + gap; }
            }
            else
            {
                var ordered = shapes.OrderBy(x => x.Top).ToList();
                var gap = (ordered.Last().Top + ordered.Last().Height - ordered.First().Top - ordered.Sum(x => x.Height)) / (ordered.Count - 1);
                var cursor = ordered.First().Top;
                foreach (var shape in ordered) { shape.Top = cursor; cursor += shape.Height + gap; }
            }
        }

        public PowerPoint.Shape AddTextBox(PowerPoint.Slide slide, string text, float left, float top, float width, float height)
        {
            var shape = slide.Shapes.AddTextbox(Office.MsoTextOrientation.msoTextOrientationHorizontal, left, top, width, height);
            shape.TextFrame.TextRange.Text = text ?? string.Empty;
            return shape;
        }

        public PowerPoint.Shape AddShape(PowerPoint.Slide slide, Office.MsoAutoShapeType type, string text, float left, float top, float width, float height)
        {
            var shape = slide.Shapes.AddShape(type, left, top, width, height);
            if (!string.IsNullOrEmpty(text)) shape.TextFrame.TextRange.Text = text;
            return shape;
        }

        public PowerPoint.Shape AddLine(PowerPoint.Slide slide, float x1, float y1, float x2, float y2, bool arrow)
        {
            var line = slide.Shapes.AddLine(x1, y1, x2, y2);
            if (arrow) line.Line.EndArrowheadStyle = Office.MsoArrowheadStyle.msoArrowheadTriangle;
            return line;
        }

        public PowerPoint.Slide AddSlide(int index)
        {
            var slide = CurrentPresentation.Slides.Add(index, PowerPoint.PpSlideLayout.ppLayoutBlank);
            return slide;
        }

        public void ReorderSlide(int slideIndex, int targetIndex) => GetSlide(slideIndex).MoveTo(targetIndex);
        public string ExportPng(PowerPoint.Slide slide) => _exporter.Export(slide);
        public static void SetTag(PowerPoint.Tags tags, string name, string value) { tags.Delete(name); tags.Add(name, value ?? string.Empty); }
        public static string GetTag(PowerPoint.Tags tags, string name) { try { return tags[name]; } catch { return null; } }

        public static string GetNotes(PowerPoint.Slide slide)
        {
            try
            {
                foreach (PowerPoint.Shape shape in slide.NotesPage.Shapes)
                    if (shape.Type == Office.MsoShapeType.msoPlaceholder && shape.PlaceholderFormat.Type == PowerPoint.PpPlaceholderType.ppPlaceholderBody)
                        return shape.TextFrame.TextRange.Text;
            }
            catch { }
            return string.Empty;
        }

        public static void SetNotes(PowerPoint.Slide slide, string text)
        {
            foreach (PowerPoint.Shape shape in slide.NotesPage.Shapes)
                if (shape.Type == Office.MsoShapeType.msoPlaceholder && shape.PlaceholderFormat.Type == PowerPoint.PpPlaceholderType.ppPlaceholderBody)
                { shape.TextFrame.TextRange.Text = text ?? string.Empty; return; }
            throw new InvalidOperationException("ノート本文プレースホルダーがありません。");
        }

        public void StartUndoEntry() { try { _app.StartNewUndoEntry(); } catch { } }
    }
}
