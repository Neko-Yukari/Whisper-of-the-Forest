using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Life restoration in three grades, chosen by the ring the ritual was drawn with: a white ring
    /// closes ordinary wounds, a green ring also clears scars and regrows lost limbs, and a star
    /// ring additionally regrows organs and removes every bad condition, including the ones a healer
    /// mech serum cannot touch.
    /// </summary>
    public sealed class RitualEffect_Healing : RitualEffectWorker
    {
        private enum HealingTier
        {
            Minor,
            Medium,
            Grand
        }

        public override void Apply(RitualContext context)
        {
            HealingTier tier = TierFor(context.Def.ringSize);
            int healed = 0;
            foreach (Pawn pawn in context.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || !pawn.RaceProps.Humanlike || pawn.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                if (pawn.Position.DistanceTo(context.Altar.Position) > context.EffectRadius)
                {
                    continue;
                }

                if (HealPawn(pawn, tier))
                {
                    healed++;
                }
            }

            Messages.Message(
                "WotF_Ritual_Healing_Done".Translate(healed),
                MessageTypeDefOf.PositiveEvent,
                false);
        }

        private static HealingTier TierFor(int ringSize)
        {
            if (ringSize >= 7)
            {
                return HealingTier.Grand;
            }

            return ringSize >= 5 ? HealingTier.Medium : HealingTier.Minor;
        }

        private static bool HealPawn(Pawn pawn, HealingTier tier)
        {
            bool changed = false;
            var injuries = new List<Hediff>();
            var missingParts = new List<Hediff_MissingPart>();
            var badConditions = new List<Hediff>();
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury)
                {
                    if (tier >= HealingTier.Medium || !injury.IsPermanent())
                    {
                        injuries.Add(injury);
                    }
                }
                else if (hediff is Hediff_MissingPart missing)
                {
                    if (tier == HealingTier.Grand || (tier == HealingTier.Medium && !IsOrgan(missing.Part)))
                    {
                        missingParts.Add(missing);
                    }
                }
                else if (tier == HealingTier.Grand && IsCurableBadCondition(hediff))
                {
                    badConditions.Add(hediff);
                }
            }

            foreach (Hediff injury in injuries)
            {
                pawn.health.RemoveHediff(injury);
                changed = true;
            }

            foreach (Hediff_MissingPart missing in missingParts)
            {
                pawn.health.RestorePart(missing.Part);
                changed = true;
            }

            foreach (Hediff condition in badConditions)
            {
                pawn.health.RemoveHediff(condition);
                changed = true;
            }

            Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                pawn.health.RemoveHediff(bloodLoss);
                changed = true;
            }

            return changed;
        }

        private static bool IsCurableBadCondition(Hediff hediff)
        {
            if (!hediff.def.isBad)
            {
                return false;
            }

            if (hediff is Hediff_AddedPart || hediff is Hediff_Implant || hediff is Hediff_MissingPart || hediff is Hediff_Injury)
            {
                return false;
            }

            // Psychic entropy is the caster's resource, and anesthetic is an active surgical state.
            return hediff.def != HediffDefOf.PsychicEntropy && hediff.def != HediffDefOf.Anesthetic;
        }

        private static bool IsOrgan(BodyPartRecord part)
        {
            foreach (BodyPartTagDef tag in part.def.tags)
            {
                if (tag == BodyPartTagDefOf.BloodFiltrationSource
                    || tag == BodyPartTagDefOf.BloodFiltrationLiver
                    || tag == BodyPartTagDefOf.BloodFiltrationKidney
                    || tag == BodyPartTagDefOf.BloodPumpingSource
                    || tag == BodyPartTagDefOf.BreathingSource
                    || tag == BodyPartTagDefOf.BreathingSourceCage
                    || tag == BodyPartTagDefOf.BreathingPathway
                    || tag == BodyPartTagDefOf.ConsciousnessSource
                    || tag == BodyPartTagDefOf.MetabolismSource)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
