using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>One cell that grows faster until a tick, with the multiplier the spell froze.</summary>
    public class FertilityBoost : IExposable
    {
        public IntVec3 cell;
        public int untilTick;
        public float factor;

        public void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref untilTick, "untilTick", 0);
            Scribe_Values.Look(ref factor, "factor", 1f);
        }
    }

    /// <summary>
    /// The Grow effect's lasting half: cells of soil that keep a higher growth rate for a while.
    /// Nothing here is a value - the spell sends the multiplier and the end tick it froze from the
    /// glyph Def, and this component only owns the map-wide registry and its save data.
    ///
    /// <see cref="Patch_Plant_GrowthRate"/> reads <see cref="FactorAt"/>, so the boost goes through
    /// the vanilla growth rate instead of touching a plant's growth field.
    /// </summary>
    public class MapComponent_ForestFertility : MapComponent
    {
        private const int PruneIntervalTicks = 250;

        private List<FertilityBoost> boosts = new List<FertilityBoost>();

        public MapComponent_ForestFertility(Map map) : base(map)
        {
        }

        /// <summary>Registers (or extends) one cell. Values come from the compiled spell step.</summary>
        public static void Register(Map map, IntVec3 cell, int durationTicks, float factor)
        {
            if (map == null || factor <= 1f || !GenGrid.InBounds(cell, map))
            {
                return;
            }

            MapComponent_ForestFertility component = map.GetComponent<MapComponent_ForestFertility>();
            if (component == null)
            {
                return;
            }

            component.Add(cell, Find.TickManager.TicksGame + durationTicks, factor);
        }

        /// <summary>Combined multiplier on that cell right now; 1 when nothing is boosted.</summary>
        public static float FactorAt(Map map, IntVec3 cell)
        {
            if (map == null)
            {
                return 1f;
            }

            MapComponent_ForestFertility component = map.GetComponent<MapComponent_ForestFertility>();
            return component == null ? 1f : component.Factor(cell, Find.TickManager.TicksGame);
        }

        private void Add(IntVec3 cell, int untilTick, float factor)
        {
            if (boosts == null)
            {
                boosts = new List<FertilityBoost>();
            }

            boosts.Add(new FertilityBoost { cell = cell, untilTick = untilTick, factor = factor });
        }

        private float Factor(IntVec3 cell, int now)
        {
            if (boosts == null || boosts.Count == 0)
            {
                return 1f;
            }

            float factor = 1f;
            for (int i = 0; i < boosts.Count; i++)
            {
                FertilityBoost boost = boosts[i];
                if (boost != null && boost.cell == cell && boost.untilTick > now)
                {
                    factor *= boost.factor;
                }
            }

            return factor;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (boosts == null || boosts.Count == 0 || Find.TickManager.TicksGame % PruneIntervalTicks != 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            for (int i = boosts.Count - 1; i >= 0; i--)
            {
                if (boosts[i] == null || boosts[i].untilTick <= now)
                {
                    boosts.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref boosts, "fertilityBoosts", LookMode.Deep);

            if (boosts == null)
            {
                boosts = new List<FertilityBoost>();
            }
        }
    }

    /// <summary>
    /// Grows plants faster on a boosted cell by scaling the vanilla growth rate. A zero rate (blighted,
    /// out of season, no light) stays zero: the patch never creates growth where the game decided
    /// none is possible.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(Plant), "get_GrowthRate")]
    internal static class Patch_Plant_GrowthRate
    {
        private static void Postfix(Plant __instance, ref float __result)
        {
            if (__result <= 0f || __instance == null || !__instance.Spawned)
            {
                return;
            }

            __result *= MapComponent_ForestFertility.FactorAt(__instance.Map, __instance.Position);
        }
    }
}
