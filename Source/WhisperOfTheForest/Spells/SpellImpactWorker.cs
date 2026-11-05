using Verse;
using WhisperOfTheForest.Experimental.Missiles;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// Hit strategy for a spell dart. The missile machinery resolves the real impact - the thing it
    /// actually touched and the cell it actually reached - and this worker runs the frozen segment
    /// there through the same executor the Self/Touch forms use, then hands the chain over to
    /// <see cref="SpellTriggerService"/> when a trigger rune closed that segment.
    ///
    /// A null payload is reported instead of silently doing nothing: only
    /// <c>WotF_Impact_SpellCast</c> selects this worker, so a missing plan means a real defect (the
    /// old impact defs never reach this code and keep their own behaviour).
    /// </summary>
    public class SpellImpactWorker_SpellCast : ArcaneMissileImpactWorker
    {
        public override void Resolve(ArcaneMissileImpactContext context)
        {
            SpellCastPlan plan = context.Settings?.spellPlan;
            if (plan == null)
            {
                SpellDiagnostics.ErrorOnce("a spell impact arrived without a payload plan; nothing was applied "
                                           + "(this def must only be used by a SpellService cast).");
                return;
            }

            if (context.Map == null)
            {
                return;
            }

            int index = context.Settings.spellSegmentIndex;
            SpellSegment segment = plan.SegmentAt(index);
            if (segment == null)
            {
                SpellDiagnostics.ErrorOnce("spell '" + plan.recipeName + "' flew with segment index " + index
                                           + " but its plan has " + plan.SegmentCount + " segment(s); nothing was applied.");
                return;
            }

            string report = SpellExecutor.ExecuteSegment(
                segment, context.Caster, context.Hit, context.ImpactCell, context.Map);

            SpellDiagnostics.Message("impact '" + plan.recipeName + "' segment=" + index + "/" + plan.SegmentCount
                                     + " form=" + segment.form + " cell=" + context.ImpactCell
                                     + " hit=" + (context.Hit != null ? context.Hit.LabelShort : "ground")
                                     + " " + report);

            // Only a real hit gets here, and the pointer only ever moves forward: the next segment
            // runs once, and only when the plan actually has one. A dart that left the map, expired
            // or lost its target never reaches this code at all.
            if (segment.triggerOnHit)
            {
                if (plan.SegmentAt(index + 1) == null)
                {
                    SpellDiagnostics.ErrorOnce("trigger of '" + plan.recipeName + "' closes segment " + index
                                               + ", which is the last one in the plan; nothing was continued.");
                    return;
                }

                SpellTriggerService.Continue(context, plan, index + 1);
                return;
            }

            // Pierce keeps the same segment alive on the far side of this hit; the budget is carried
            // by the darts themselves and shrinks once per hit.
            if (segment.pierceStacks > 0)
            {
                SpellTriggerService.ContinuePierce(context, plan, index);
            }
        }
    }
}
