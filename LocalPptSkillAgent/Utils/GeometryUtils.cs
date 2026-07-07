using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Utils
{
    public static class GeometryUtils
    {
        public static bool IsOverlap(RectModel a, RectModel b, float margin = 0) =>
            a.Left < b.Right + margin && a.Right > b.Left - margin &&
            a.Top < b.Bottom + margin && a.Bottom > b.Top - margin;

        public static bool IsOutsideSlide(RectModel r, float slideWidth, float slideHeight) =>
            r.Left < 0 || r.Top < 0 || r.Right > slideWidth || r.Bottom > slideHeight;

        public static float HorizontalGap(RectModel a, RectModel b)
        {
            if (a.Right <= b.Left) return b.Left - a.Right;
            if (b.Right <= a.Left) return a.Left - b.Right;
            return 0;
        }

        public static float VerticalGap(RectModel a, RectModel b)
        {
            if (a.Bottom <= b.Top) return b.Top - a.Bottom;
            if (b.Bottom <= a.Top) return a.Top - b.Bottom;
            return 0;
        }
    }
}
