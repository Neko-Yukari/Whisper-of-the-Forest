using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhisperOfTheForest.Court
{
    /// <summary>
    /// <see cref="StockGenerator_SingleDef"/> for a thing made from stuff, with the material written in
    /// the Def.
    ///
    /// Vanilla picks the material of a stocked item from the stuffs with allowedInStuffGeneration
    /// (RimWorld/StockGeneratorUtility.cs:44-48) and falls back to the cheapest allowed stuff, so the
    /// four forest materials, which are all allowedInStuffGeneration=false, can never come out of a
    /// shelf. A stock entry that sells court gear therefore names its stuff here and gets exactly that.
    ///
    /// Not derived from StockGenerator_SingleDef: its thingDef is private, so a subclass could neither
    /// read it nor share the XML field name. Count, price type and tech limits come from the shared
    /// <see cref="StockGenerator"/> base exactly as for the vanilla class.
    /// </summary>
    public class StockGenerator_SingleDefStuff : StockGenerator
    {
        public ThingDef thingDef;

        public ThingDef stuff;

        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            int count = RandomCountOf(thingDef);
            for (int i = 0; i < count; i++)
            {
                yield return ThingMaker.MakeThing(thingDef, stuff);
            }
        }

        public override bool HandlesThingDef(ThingDef thingDef)
        {
            return thingDef == this.thingDef;
        }

        public override IEnumerable<string> ConfigErrors(TraderKindDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (thingDef == null)
            {
                yield return "StockGenerator_SingleDefStuff has no thingDef";
                yield break;
            }

            if (!thingDef.tradeability.TraderCanSell())
            {
                yield return thingDef + " tradeability doesn't allow traders to sell this thing";
            }

            if (!thingDef.MadeFromStuff)
            {
                yield return thingDef + " is not made from stuff, so a stock material cannot apply";
            }
            else if (stuff == null)
            {
                yield return thingDef + " is made from stuff but the stock entry names none";
            }
            else if (!stuff.IsStuff)
            {
                yield return stuff + " is not a stuff";
            }
            else if (!stuff.stuffProps.CanMake(thingDef))
            {
                yield return thingDef + " cannot be made from " + stuff;
            }
        }
    }
}
