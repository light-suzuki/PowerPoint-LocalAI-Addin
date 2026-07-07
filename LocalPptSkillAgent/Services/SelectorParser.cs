using System;
using System.Text.RegularExpressions;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Services
{
    public class SelectorParser
    {
        private static readonly Regex Selector = new Regex(@"@(current|selected|all|figures|text|notes|slide:\d+|after:\d+|before:\d+|\d+(?:\s*-\s*\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public TargetSelector Parse(string input)
        {
            var match = Selector.Match(input ?? string.Empty);
            var raw = match.Success ? match.Value.Replace(" ", string.Empty) : "@current";
            var token = raw.Substring(1).ToLowerInvariant();
            var result = new TargetSelector { Raw = raw };
            if (token == "current") result.Type = "current";
            else if (token == "selected") { result.Type = "selected"; result.IncludeSelectedShapes = true; }
            else if (token == "all") result.Type = "all";
            else if (token == "figures") { result.Type = "figures"; result.IncludeFigures = true; }
            else if (token == "text") { result.Type = "text"; result.IncludeText = true; }
            else if (token == "notes") { result.Type = "notes"; result.IncludeNotes = true; }
            else if (token.StartsWith("slide:")) { result.Type = "slide"; result.SlideIndex = Positive(token.Substring(6)); }
            else if (token.StartsWith("after:")) { result.Type = "insert_after"; result.InsertAfter = Positive(token.Substring(6)); }
            else if (token.StartsWith("before:")) { result.Type = "insert_before"; result.InsertBefore = Positive(token.Substring(7)); }
            else
            {
                var parts = token.Split('-');
                if (parts.Length == 1)
                {
                    result.Type = "slide";
                    result.SlideIndex = Positive(parts[0]);
                    return result;
                }
                result.Type = "slide_range";
                result.StartSlide = Positive(parts[0]);
                result.EndSlide = Positive(parts[1]);
                if (result.StartSlide > result.EndSlide) { var temp = result.StartSlide; result.StartSlide = result.EndSlide; result.EndSlide = temp; }
            }
            return result;
        }

        private static int Positive(string value)
        {
            if (!int.TryParse(value, out var result) || result < 1) throw new FormatException("スライド番号は1以上で指定してください。");
            return result;
        }
    }
}
