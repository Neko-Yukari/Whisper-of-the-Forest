using System.Linq;
using Verse;

namespace WhisperOfTheForest.Turrets
{
    /// <summary>
    /// Draws the range of the turret gun's own primary verb. Vanilla's
    /// PlaceWorker_ShowTurretRadius only accepts a Verb_Shoot or Verb_Spray, so it dereferences a
    /// null verb and throws inside UIRootUpdate every frame for a turret with a custom verb.
    /// </summary>
    public class PlaceWorker_SpellTurretRadius : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef checkingDef,
            IntVec3 loc,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            VerbProperties verb = ((ThingDef)checkingDef).building.turretGunDef.Verbs.First(v => v.isPrimary);
            if (verb.range > 0f)
            {
                GenDraw.DrawRadiusRing(loc, verb.range);
            }

            if (verb.minRange > 0f)
            {
                GenDraw.DrawRadiusRing(loc, verb.minRange);
            }

            return true;
        }
    }
}
