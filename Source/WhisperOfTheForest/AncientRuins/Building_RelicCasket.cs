using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// A relic casket carries its own fixed loot table instead of the room-reward RNG. The contents are
    /// generated when the building lands on the map, so the guarantee (core + rubbing) lives in this
    /// building's ThingSetMakerDef and cannot be eaten by the complex's room crate count.
    /// </summary>
    public class Building_RelicCasket : Building_Crate
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad || innerContainer.Count > 0)
            {
                return;
            }

            ThingSetMakerDef contents = def.GetModExtension<RelicCasketExtension>().contents;
            List<Thing> things = contents.root.Generate(default(ThingSetMakerParams));
            for (int i = 0; i < things.Count; i++)
            {
                if (!TryAcceptThing(things[i], allowSpecialEffects: false))
                {
                    things[i].Destroy();
                }
            }

            WotfLog.Info(WotfLog.ModuleRuin, "filled " + def.defName + " with " + innerContainer.Count
                + " things at " + Position);
        }
    }
}
