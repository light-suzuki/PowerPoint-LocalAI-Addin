using LocalPptSkillAgent.Services;
using Xunit;

namespace LocalPptSkillAgent.Tests
{
    public class SelectorParserTests
    {
        private readonly SelectorParser _parser = new SelectorParser();

        [Theory]
        [InlineData("見た目を整えて", "@current", "current")]
        [InlineData("@selected 揃える", "@selected", "selected")]
        [InlineData("前置き @slide:3 後置き", "@slide:3", "slide")]
        [InlineData("@3 図解にする", "@3", "slide")]
        [InlineData("@after:5 追加", "@after:5", "insert_after")]
        [InlineData("@before:2 追加", "@before:2", "insert_before")]
        [InlineData("@figures", "@figures", "figures")]
        public void ParsesSelectors(string input, string raw, string type)
        {
            var result = _parser.Parse(input);
            Assert.Equal(raw, result.Raw);
            Assert.Equal(type, result.Type);
        }

        [Fact]
        public void NormalizesReverseRange()
        {
            var result = _parser.Parse("@5-1");
            Assert.Equal(1, result.StartSlide);
            Assert.Equal(5, result.EndSlide);
        }
    }
}
