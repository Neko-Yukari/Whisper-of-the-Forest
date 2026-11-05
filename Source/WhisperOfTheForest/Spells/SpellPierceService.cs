using UnityEngine;
using Verse;
using WhisperOfTheForest.Missiles;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Pierce: a spell dart keeps flying through the thing it just hit and can resolve again on
    /// whatever stands behind it. Only a real hit reaches this service: a dart that left the map,
    /// timed out, lost its target or was cleaned up never calls it.
    ///
    /// "Behind the target" means along the incoming motion: the dart leaves from the far side of the
    /// thing that was hit with the heading it hit with, never back towards the caster.
    /// <see cref="SpellPierceGeometry"/> owns that cell math. The new dart is the same projectile:
    /// its settings are the hitting dart's own copy, so no flight number is made up here.
    /// </summary>
    public static class SpellPierceService
    {
        /// <summary>
        /// Fires the same payload on past the hit with one pierce fewer. The darts carry the reduced
        /// plan, so a save mid-flight cannot restore spent pierces.
        /// </summary>
        public static void Continue(ArcaneMissileImpactContext context, SpellCastPlan plan)
        {
            Map map = context.Map;
            if (map == null || plan.pierceStacks <= 0)
            {
                return;
            }

            SpellCastPlan carried = plan.Copy();
            carried.pierceStacks = plan.pierceStacks - 1;

            if (!SpellPierceGeometry.TryDirection(context.HeadingX, context.HeadingZ, out float dirX, out float dirZ))
            {
                SpellDiagnostics.Message("pierce '" + plan.recipeName + "' has no heading; nothing was fired.");
                return;
            }

            // The spell that just ran may have killed or destroyed what it hit; then the dart leaves
            // from the impact point instead of the far side of the thing.
            bool liveHit = context.Hit != null && !context.Hit.Destroyed;
            float hitX = liveHit ? context.Hit.DrawPos.x : 0f;
            float hitZ = liveHit ? context.Hit.DrawPos.z : 0f;

            if (!SpellPierceGeometry.TrySpawnPoint(
                    liveHit, hitX, hitZ,
                    context.ImpactX, context.ImpactZ,
                    dirX, dirZ,
                    map.Size.x, map.Size.z,
                    out float spawnX, out float spawnZ))
            {
                SpellDiagnostics.Message("pierce '" + plan.recipeName + "' stopped: spawn point " + spawnX.ToString("0.##") + ","
                                         + spawnZ.ToString("0.##") + " is outside the map.");
                return;
            }

            if (!SpellPierceGeometry.TryLookAhead(
                    spawnX, spawnZ, dirX, dirZ,
                    map.Size.x, map.Size.z,
                    out int targetCellX, out int targetCellZ))
            {
                SpellDiagnostics.Message("pierce '" + plan.recipeName + "' stopped: no in-bounds cell ahead of " + context.ImpactCell);
                return;
            }

            ArcaneMissileSettings settings = context.Settings.Copy();

            // The dart aims at a cell, not at a living target, so "target lost" would expire it at once.
            settings.expireOnTargetLost = false;

            // Frozen at launch: the missile deep-copies this and scribes it with itself.
            settings.payload = new SpellMissilePayload { plan = carried };

            var spawn = new Vector3(spawnX, 0f, spawnZ);
            var targetCell = new IntVec3(targetCellX, 0, targetCellZ);
            int launched = ArcaneMissileLauncher.FireFrom(
                context.Caster, settings, spawn, dirX, dirZ, targetCell, null);

            SpellDiagnostics.Message("pierce '" + plan.recipeName + "' launched=" + launched + "/" + settings.missileCount
                                     + " from " + spawn.ToString("F2") + " towards " + targetCell
                                     + " direction=" + dirX.ToString("0.##") + "," + dirZ.ToString("0.##"));
        }
    }
}
