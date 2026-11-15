using Verse;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    internal static class Ritual_TestFixture
    {
        /// <summary>
        /// Puts an offering straight into the altar's container, where a hauler would have left it.
        /// Tracked so an unconsumed stack, dropped when cleanup destroys the altar, is cleaned up too.
        /// </summary>
        public static Thing Deliver(TestContext t, CompRitualAltar altar, ThingDef def, int count)
        {
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = count;
            t.Arena.Track(thing);
            if (!altar.GetDirectlyHeldThings().TryAdd(thing))
            {
                t.Fail("the altar container refused " + count + " " + def.defName);
            }

            return thing;
        }

        /// <summary>How many of this def the altar holds.</summary>
        public static int Held(CompRitualAltar altar, ThingDef def)
        {
            return altar.GetDirectlyHeldThings().TotalStackCountOfDef(def);
        }
    }
}
