using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhisperOfTheForest.Teleport
{
    /// <summary>
    /// Marks a site part whose map a gate may reach, so the gate menu never hard-codes a defName and
    /// a third ruin only has to set the same extension (设定/05: only the ruins are reachable).
    /// </summary>
    public class RuinSiteExtension : DefModExtension
    {
        public static bool Reachable(Site site)
        {
            if (site == null || site.Destroyed || !site.Spawned)
            {
                return false;
            }

            for (int i = 0; i < site.parts.Count; i++)
            {
                SitePartDef def = site.parts[i].def;
                if (def != null && def.HasModExtension<RuinSiteExtension>())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
