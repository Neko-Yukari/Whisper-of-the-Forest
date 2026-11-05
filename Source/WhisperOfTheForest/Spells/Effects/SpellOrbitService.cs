using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// Places the Orbit form's orbs. One orb per volley slot (Split adds more), spread evenly around
    /// the caster so several orbs do not overlap and all start at different angles.
    /// </summary>
    public static class SpellOrbitService
    {
        public static int Spawn(Map map, Pawn caster, SpellSegment segment)
        {
            if (map == null || caster == null || segment == null
                || segment.orbitDistance <= 0f || segment.orbitSpeedDegrees <= 0f
                || segment.orbitHitRadius <= 0f || segment.orbitLifetimeTicks <= 0)
            {
                SpellDiagnostics.ErrorOnce("an orbit segment is missing its radius/speed/hit-radius/lifetime; nothing was placed.");
                return 0;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingSpellOrbit);
            if (def == null)
            {
                SpellDiagnostics.ErrorOnce("thing def " + SpellEffectDefs.ThingSpellOrbit + " is missing; no orb was placed.");
                return 0;
            }

            int count = segment.missileCount > 0 ? segment.missileCount : 1;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                var orb = (Thing_SpellOrbit)ThingMaker.MakeThing(def);
                orb.Configure(caster, segment, 360f * i / count);
                GenSpawn.Spawn(orb, caster.Position, map);
                spawned++;
            }

            return spawned;
        }
    }
}
