using RimWorld;
using Verse;
using WhisperOfTheForest.Race;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The first ritual: every bloodline pawn around the altar gets mana back. It is deliberately
    /// local and instant, so it exercises the ring, the altar cost and the job without touching
    /// terrain, weather or pawn health.
    /// </summary>
    public sealed class RitualEffect_ManaSpring : RitualEffectWorker
    {
        public static float ManaRestoreFor(RitualDef def, float ritualStrength)
        {
            return def.manaRestore * def.EffectFactorFor(ritualStrength);
        }

        public override void Apply(RitualContext context)
        {
            float amount = ManaRestoreFor(context.Def, context.RitualStrength);
            int restored = 0;
            foreach (Pawn pawn in context.Map.mapPawns.AllPawnsSpawned)
            {
                Gene_ForestBloodline gene = Gene_ForestBloodline.Get(pawn);
                if (gene == null)
                {
                    continue;
                }

                if (pawn.Position.DistanceTo(context.Altar.Position) > context.EffectRadius)
                {
                    continue;
                }

                float before = gene.CurrentMana;
                gene.OffsetMana(amount);
                if (gene.CurrentMana > before)
                {
                    restored++;
                }
            }

            Messages.Message(
                "WotF_Ritual_ManaSpring_Done".Translate(restored),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
