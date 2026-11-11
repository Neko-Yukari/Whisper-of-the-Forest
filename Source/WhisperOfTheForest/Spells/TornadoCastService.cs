using Verse;

namespace WhisperOfTheForest.Spells
{
    public static class TornadoCastService
    {
        public static string Resolve(Map map, Pawn caster, IntVec3 aimCell, SpellTornadoSpec spec)
        {
            var tornado = (Thing_SpellTornado)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WotF_SpellTornado"));
            tornado.Configure(caster, spec);
            GenSpawn.Spawn(tornado, aimCell, map);
            return "tornado spawned at " + aimCell;
        }
    }
}
