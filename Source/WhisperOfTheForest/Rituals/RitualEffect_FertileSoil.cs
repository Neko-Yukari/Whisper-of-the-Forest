using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The rite of fertile awakening: every plain soil cell around the altar is raised to rich
    /// soil. Constructed floors, rock and cells already occupied by a building are left alone;
    /// plants keep growing on the improved soil, so the rite improves farmland instead of
    /// bulldozing it.
    /// </summary>
    public sealed class RitualEffect_FertileSoil : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            int changed = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(context.Altar.Position, context.EffectRadius, true))
            {
                if (!cell.InBounds(context.Map))
                {
                    continue;
                }

                TerrainDef terrain = cell.GetTerrain(context.Map);
                if (terrain != TerrainDefOf.Soil)
                {
                    continue;
                }

                if (cell.GetEdifice(context.Map) != null)
                {
                    continue;
                }

                context.Map.terrainGrid.SetTerrain(cell, TerrainDefOf.SoilRich);
                changed++;
            }

            Messages.Message(
                "WotF_Ritual_FertileSoil_Done".Translate(changed),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
