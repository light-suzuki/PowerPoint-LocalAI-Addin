using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;

namespace LocalPptSkillAgent.Skills
{
    public class AlignShapesSkill : ISkill
    {
        public string Name => "align_shapes";
        public Task<LayoutActionPlan> RunAsync(SkillInput input, string prompt, LmStudioClient llm, IVisionClient vision, CancellationToken cancellationToken = default(CancellationToken))
        {
            var slide = input.Slides.First();
            var plan = new LayoutActionPlan { Skill = Name, Target = input.Target.Raw, Summary = "ルールベースで図形を整列します。" };
            plan.Actions.Add(new LayoutAction
            {
                Type = input.Instruction != null && input.Instruction.Contains("等間隔") ? "distribute" : "align",
                Slide = slide.SlideIndex,
                ShapeIds = slide.Shapes.Select(x => x.Id).ToList(),
                AlignMode = "center_horizontal",
                DistributeMode = "horizontal",
                Reason = "選択図形の座標をルールベースで正規化するため"
            });
            return Task.FromResult(plan);
        }
    }
}
