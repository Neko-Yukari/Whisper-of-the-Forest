using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The leyline rite taps the deep resource grid under the altar and brings one portion of each
    /// of the first few resources to the surface. The altar checks the grid before paying mana, so a
    /// rite over barren ground is rejected instead of wasting the network's charge.
    /// </summary>
    public sealed class RitualEffect_LeylineMining : RitualEffectWorker
    {
        private const int DefaultPortion = 20;

        public override void Apply(RitualContext context)
        {
            List<ThingDef> resources = DeepResources(context.Map, context.Altar.Position, context.EffectRadius);
            int yieldedTypes = 0;
            int total = 0;
            foreach (ThingDef resource in resources)
            {
                if (yieldedTypes >= context.Def.maxResourceTypes)
                {
                    break;
                }

                int portion = resource.deepCountPerPortion > 0 ? resource.deepCountPerPortion : DefaultPortion;
                int taken = TakeFromGrid(context.Map, context.Altar.Position, context.EffectRadius, resource, portion);
                if (taken <= 0)
                {
                    continue;
                }

                Thing thing = ThingMaker.MakeThing(resource);
                thing.stackCount = taken;
                if (!GenPlace.TryPlaceThing(thing, context.Altar.Position, context.Map, ThingPlaceMode.Near))
                {
                    thing.Destroy(DestroyMode.Vanish);
                    Messages.Message(
                        "WotF_Ritual_LeylineMining_NoRoom".Translate(),
                        MessageTypeDefOf.RejectInput,
                        false);
                    return;
                }

                total += taken;
                yieldedTypes++;
            }

            if (total == 0)
            {
                Messages.Message(
                    "WotF_Ritual_LeylineMining_NoResource".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            Messages.Message(
                "WotF_Ritual_LeylineMining_Done".Translate(total),
                MessageTypeDefOf.PositiveEvent,
                false);
        }

        public static bool HasDeepResources(Map map, IntVec3 center, float radius)
        {
            return DeepResources(map, center, radius).Count > 0;
        }

        private static List<ThingDef> DeepResources(Map map, IntVec3 center, float radius)
        {
            var found = new List<ThingDef>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                ThingDef resource = map.deepResourceGrid.ThingDefAt(cell);
                if (resource != null && map.deepResourceGrid.CountAt(cell) > 0 && !found.Contains(resource))
                {
                    found.Add(resource);
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));
            return found;
        }

        private static int TakeFromGrid(Map map, IntVec3 center, float radius, ThingDef resource, int portion)
        {
            int taken = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (taken >= portion)
                {
                    break;
                }

                if (!cell.InBounds(map) || map.deepResourceGrid.ThingDefAt(cell) != resource)
                {
                    continue;
                }

                int present = map.deepResourceGrid.CountAt(cell);
                if (present <= 0)
                {
                    continue;
                }

                int take = Math.Min(present, portion - taken);
                map.deepResourceGrid.SetAt(cell, resource, present - take);
                taken += take;
            }

            return taken;
        }
    }
}