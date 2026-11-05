using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// OnHit ends the segment on its left and runs the next one at the point the dart really hit.
    /// The second segment is an AOE explosion: the projectile could only ever touch the one pawn it
    /// flew into, so an injured bystander two cells away proves the triggered segment really ran.
    /// </summary>
    [LabTest]
    public sealed class Trigger_OnHitChain : LabTest
    {
        public override string Name => "trigger.onhit-chain";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn victim = t.SpawnTarget(9, 5);
            Pawn bystander = t.SpawnTarget(9, 7);
            int victimId = victim.thingIDNumber;
            int bystanderId = bystander.thingIDNumber;
            int impactsBefore = t.MissileImpacts();

            SpellRecipe recipe = TestContext.Recipe("dart-then-blast",
                SpellDefs.FormProjectile, SpellDefs.EffectHarm,
                SpellDefs.TriggerOnHit,
                SpellDefs.FormSelf, SpellDefs.EffectExplosion, SpellDefs.AugmentAoe);

            if (!t.CastAt(caster, recipe, victim))
            {
                return;
            }

            bool hit = t.StepUntil(() => t.MissileImpacts() > impactsBefore, 600);
            t.Step(3);

            bool victimHurt = Hurt(t, victimId);
            bool bystanderHurt = Hurt(t, bystanderId);
            t.Note("hitWithin600Ticks", hit);
            t.Note("victimHurt", victimHurt);
            t.Note("bystanderHurt", bystanderHurt);
            t.Note("impacts", t.MissileImpacts() - impactsBefore);
            t.Measure("victimHediffs", Hediffs(t.Arena.Find(victimId) as Pawn));
            t.Measure("bystanderHediffs", Hediffs(t.Arena.Find(bystanderId) as Pawn));

            t.Expect(hit, "the first segment never hit, so the trigger could not run");
            t.Expect(victimHurt, "the pawn the dart hit was not hurt");
            t.Expect(bystanderHurt,
                "the bystander beside the impact was not hurt, so the triggered segment did not resolve");
        }

        /// <summary>Dead, gone, or carrying at least one injury: all three mean "the effect resolved".</summary>
        private static bool Hurt(TestContext t, int thingId)
        {
            Thing thing = t.Arena.Find(thingId);
            if (!(thing is Pawn pawn))
            {
                return true;
            }

            return pawn.Dead || t.InjurySeverity(pawn) > 0f;
        }

        private static string Hediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return "none";
            }

            var parts = new List<string>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff?.def != null && (hediff is Hediff_Injury || hediff.def.defName == "BloodLoss"))
                {
                    parts.Add(hediff.def.defName + ":" + hediff.Severity.ToString("0.##"));
                }
            }

            return parts.Count == 0 ? "none" : string.Join(",", parts.ToArray());
        }
    }
}
