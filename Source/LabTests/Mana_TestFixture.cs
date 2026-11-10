using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    internal static class Mana_TestFixture
    {
        public static CompManaBattery AddBattery(TestContext t, Building node)
        {
            var battery = (Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WotF_ManaBattery"));
            battery.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(battery, node.Position + new IntVec3(2, 0, 2), t.Map, WipeMode.Vanish);
            t.Arena.Track(battery);
            var comp = battery.GetComp<CompManaBattery>();
            MapComponent_ManaNetwork.For(t.Map).NetworkNear(battery.Position, comp.LinkRadius);
            return comp;
        }
    }
}
