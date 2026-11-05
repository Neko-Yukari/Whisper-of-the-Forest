using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Places the Orbit form's orbs. One orb per volley slot (Split adds more), spread evenly around
    /// the caster so several orbs do not overlap and all start at different angles.
    /// </summary>
    public static class SpellOrbitService
    {
        public static int Spawn(Map map, Pawn caster, SpellCastPlan plan)
        {
            if (map == null || caster == null || plan.orbit == null)
            {
                SpellDiagnostics.ErrorOnce("an orbit cast has no orbit block; nothing was placed.");
                return 0;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingSpellOrbit);
            if (def == null)
            {
                SpellDiagnostics.ErrorOnce("thing def " + SpellEffectDefs.ThingSpellOrbit + " is missing; no orb was placed.");
                return 0;
            }

            int count = plan.orbit.count;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                var orb = (Thing_SpellOrbit)ThingMaker.MakeThing(def);
                orb.Configure(caster, plan, 360f * i / count);
                GenSpawn.Spawn(orb, caster.Position, map);
                spawned++;
            }

            return spawned;
        }
    }
}
