using Verse;
using WhisperOfTheForest.Missiles;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// What a spell dart delivers: the frozen plan. The missile layer resolves the real impact - the
    /// thing it actually touched and the cell it actually reached - and this runs the plan there
    /// through the same executor the Self/Touch forms use, then lets a pierced dart fly on through
    /// <see cref="SpellPierceService"/>.
    /// </summary>
    public class SpellMissilePayload : IArcaneMissilePayload
    {
        public SpellCastPlan plan;

        public void Resolve(ArcaneMissileImpactContext context)
        {
            string report = SpellExecutor.Execute(plan, context.Caster, context.Hit, context.ImpactCell, context.Map);
            SpellDiagnostics.Message("impact '" + plan.recipeName + "' cell=" + context.ImpactCell
                                     + " hit=" + (context.Hit != null ? context.Hit.LabelShort : "ground")
                                     + " " + report);

            // Pierce keeps the same spell alive on the far side of this hit; the budget is carried by
            // the darts themselves and shrinks once per hit.
            if (plan.pierceStacks > 0)
            {
                SpellPierceService.Continue(context, plan);
            }
        }

        public IArcaneMissilePayload Copy()
        {
            return new SpellMissilePayload { plan = plan.Copy() };
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref plan, "plan");
        }
    }
}
