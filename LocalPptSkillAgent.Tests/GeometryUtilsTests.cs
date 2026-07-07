using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;
using Xunit;

namespace LocalPptSkillAgent.Tests
{
    public class GeometryUtilsTests
    {
        [Fact]
        public void DetectsOverlapAndOutside()
        {
            var a = new RectModel { Left = 0, Top = 0, Width = 100, Height = 100 };
            var b = new RectModel { Left = 90, Top = 20, Width = 50, Height = 20 };
            Assert.True(GeometryUtils.IsOverlap(a, b));
            Assert.True(GeometryUtils.IsOutsideSlide(b, 120, 100));
        }

        [Fact]
        public void ComputesGaps()
        {
            var a = new RectModel { Left = 0, Top = 0, Width = 10, Height = 10 };
            var b = new RectModel { Left = 20, Top = 30, Width = 10, Height = 10 };
            Assert.Equal(10, GeometryUtils.HorizontalGap(a, b));
            Assert.Equal(20, GeometryUtils.VerticalGap(a, b));
        }
    }
}
