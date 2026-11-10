using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>Stun duration carried by the short staff's own Def.</summary>
    public class CompProperties_ShortStaff : CompProperties
    {
        public int stunTicks = 600;

        public CompProperties_ShortStaff()
        {
            compClass = typeof(CompShortStaff);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (stunTicks <= 0)
            {
                yield return parentDef.defName + " has stunTicks <= 0";
            }
        }
    }

    public class CompShortStaff : ThingComp
    {
        public CompProperties_ShortStaff Props => (CompProperties_ShortStaff)props;
    }
}
