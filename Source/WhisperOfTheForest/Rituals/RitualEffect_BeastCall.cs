using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The beast-call rite summons one animal of the Def's kind beside the altar and makes a single
    /// taming attempt. A failed attempt sends the animal away; a successful one recruits it.
    /// </summary>
    public sealed class RitualEffect_BeastCall : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            RitualDef def = context.Def;
            if (!CellFinder.TryRandomClosewalkCellNear(context.Altar.Position, context.Map, 2, out IntVec3 cell))
            {
                Messages.Message(
                    "WotF_Ritual_BeastCall_NoRoom".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            Pawn animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                def.summonedAnimal,
                null,
                PawnGenerationContext.NonPlayer,
                canGeneratePawnRelations: false));
            GenSpawn.Spawn(animal, cell, context.Map, WipeMode.Vanish);

            int skill = context.Caster.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
            float chance = def.tameChanceBase
                + def.tameChancePerSkill * skill
                + def.tameChancePerStrength * context.RitualStrength
                - def.tameWildnessFactor * animal.GetStatValue(StatDefOf.Wildness);
            bool tamed = TryTame(context.Caster, animal, chance);

            Messages.Message(
                (tamed ? "WotF_Ritual_BeastCall_Tamed" : "WotF_Ritual_BeastCall_Left").Translate(animal.LabelShort),
                tamed ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent,
                false);
        }

        /// <summary>
        /// One taming attempt at the given chance. On failure the animal leaves the map instead of
        /// staying around wild.
        /// </summary>
        public static bool TryTame(Pawn handler, Pawn animal, float chance)
        {
            if (!TameUtility.CanTame(animal) || !Rand.Chance(Mathf.Clamp01(chance)))
            {
                animal.DeSpawn();
                return false;
            }

            InteractionWorker_RecruitAttempt.DoRecruit(handler, animal, out _, out _, useAudiovisualEffects: true, sendLetter: false);
            return true;
        }
    }
}
