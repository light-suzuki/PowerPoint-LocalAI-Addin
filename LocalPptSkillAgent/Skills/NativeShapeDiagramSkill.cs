namespace LocalPptSkillAgent.Skills
{
    public class NativeShapeDiagramSkill : LlmSkillBase
    {
        public override string Name => "native_shape_diagram";
        protected override bool UsesVision => true;
    }
}
