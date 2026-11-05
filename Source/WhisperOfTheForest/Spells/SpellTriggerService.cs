using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Experimental.Missiles;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// Runs the segment a trigger rune points at, from the point where the previous segment really
    /// hit. Only a real hit reaches this service: a dart that left the map, timed out, lost its
    /// target or was cleaned up never calls it, so "on hit" means exactly that.
    ///
    /// The pointer only moves forward. A continuation never looks back at the plan it came from and
    /// never re-enters the segment that triggered it, so a chain cannot loop even when Split makes
    /// several darts of one segment.
    ///
    /// "Behind the target" means along the incoming motion, through it: the next projectile leaves
    /// from the far side of the thing that was hit and keeps going that way, never back towards the
    /// caster. <see cref="SpellTriggerGeometry"/> owns that cell math.
    /// </summary>
    public static class SpellTriggerService
    {
        /// <summary>Scale of the ring fleck that marks the hand-over.</summary>
        private const float TriggerFleckScale = 1.4f;

        /// <summary>
        /// Pierce: the same segment keeps flying through the thing it just hit and can resolve again
        /// on whatever stands behind it. It reuses the trigger hand-over's launch math (spawn past the
        /// hit, aim along the incoming heading) but keeps the segment index, so the payload is the
        /// same one and only the pierce budget shrinks. The darts carry the reduced plan, so a save
        /// mid-flight cannot restore spent pierces.
        /// </summary>
        public static void ContinuePierce(ArcaneMissileImpactContext context, SpellCastPlan plan, int index)
        {
            Map map = context.Map;
            SpellSegment segment = plan?.SegmentAt(index);
            if (map == null || segment == null || segment.pierceStacks <= 0)
            {
                return;
            }

            SpellCastPlan carried = plan.Copy();
            SpellSegment carriedSegment = carried.SegmentAt(index);
            if (carriedSegment == null)
            {
                return;
            }

            carriedSegment.pierceStacks = segment.pierceStacks - 1;
            FireNextSegment(context, carried, carriedSegment, index, map);
        }

        /// <summary>
        /// Continues the chain at <paramref name="nextIndex"/>, or reports why it could not.
        /// Self/Touch resolve on the struck Thing (the impact cell when the dart hit ground);
        /// Projectile is spawned past the struck thing and flies on through it.
        /// </summary>
        public static void Continue(ArcaneMissileImpactContext context, SpellCastPlan plan, int nextIndex)
        {
            Map map = context.Map;
            if (map == null || plan == null)
            {
                return;
            }

            SpellSegment next = plan.SegmentAt(nextIndex);
            if (next == null)
            {
                SpellDiagnostics.ErrorOnce("trigger of '" + plan.recipeName + "' points at segment " + nextIndex
                                           + ", which the plan does not have; the chain stopped there.");
                return;
            }

            PlayTriggerRing(context, map);

            if (next.form == SpellFormKind.Projectile)
            {
                FireNextSegment(context, plan, next, nextIndex, map);
                return;
            }

            // Self and Touch mean "the thing this dart hit". With no thing, the impact cell is the
            // target, which is what a ground hit of an explosion/grow spell already does.
            string report = SpellExecutor.ExecuteSegment(next, context.Caster, context.Hit, context.ImpactCell, map);
            SpellDiagnostics.Message("trigger '" + plan.recipeName + "' segment=" + nextIndex
                                     + " form=" + next.form
                                     + " cell=" + context.ImpactCell
                                     + " hit=" + (context.Hit != null ? context.Hit.LabelShort : "ground")
                                     + " " + report);
        }

        /// <summary>
        /// Spawns the next projectile segment on the far side of what the dart hit and sends it on the
        /// same way: the direction runs from the impact point through the hit thing's centre, so the
        /// segment appears behind the target and keeps penetrating instead of bouncing back. Without a
        /// live hit (ground impact, destroyed target, impact exactly on the centre) the incoming
        /// heading is used. An off-map spawn point, or no in-map cell ahead, drops the segment with a
        /// log entry instead of firing. The dart is aimed at a cell, never at the thing that was hit.
        /// </summary>
        private static void FireNextSegment(
            ArcaneMissileImpactContext context,
            SpellCastPlan plan,
            SpellSegment segment,
            int index,
            Map map)
        {
            // The segment that just ran may have killed or destroyed what it hit; such a thing has no
            // through-line any more, so it counts as "no hit" and the incoming heading takes over.
            bool liveHit = context.Hit != null && !context.Hit.Destroyed;
            float hitX = liveHit ? context.Hit.DrawPos.x : 0f;
            float hitZ = liveHit ? context.Hit.DrawPos.z : 0f;

            if (!SpellTriggerGeometry.TryDirection(
                    liveHit, hitX, hitZ,
                    context.ImpactX, context.ImpactZ,
                    context.HeadingX, context.HeadingZ,
                    out float dirX, out float dirZ))
            {
                SpellDiagnostics.Message("trigger '" + plan.recipeName + "' segment=" + index
                                         + " has neither a through-line nor a heading; nothing was fired.");
                return;
            }

            if (!SpellTriggerGeometry.TrySpawnPoint(
                    liveHit, hitX, hitZ,
                    context.ImpactX, context.ImpactZ,
                    dirX, dirZ,
                    map.Size.x, map.Size.z,
                    out float spawnX, out float spawnZ))
            {
                SpellDiagnostics.Message("trigger '" + plan.recipeName + "' segment=" + index
                                         + " not triggered: spawn point " + spawnX.ToString("0.##") + ","
                                         + spawnZ.ToString("0.##") + " is outside the map.");
                return;
            }

            if (!SpellTriggerGeometry.TryLookAhead(
                    spawnX, spawnZ, dirX, dirZ,
                    map.Size.x, map.Size.z,
                    out int targetCellX, out int targetCellZ))
            {
                SpellDiagnostics.Message("trigger '" + plan.recipeName + "' segment=" + index
                                         + " dropped: no in-bounds cell ahead of " + context.ImpactCell);
                return;
            }

            var spawn = new Vector3(spawnX, 0f, spawnZ);
            var targetCell = new IntVec3(targetCellX, 0, targetCellZ);
            ArcaneMissileSettings settings = BuildSegmentSettings(context.Settings, plan, segment, index);
            int launched = ArcaneMissileLauncher.FireSpellSegment(
                context.Caster, settings, index, spawn, dirX, dirZ, targetCell, null);

            SpellDiagnostics.Message("trigger '" + plan.recipeName + "' segment=" + index
                                     + " launched=" + launched + "/" + segment.missileCount
                                     + " from " + spawn.ToString("F2") + " towards " + targetCell
                                     + " direction=" + dirX.ToString("0.##") + "," + dirZ.ToString("0.##"));
        }

        /// <summary>
        /// Settings for one continuation: the incoming volley's envelope with this segment's own
        /// numbers. The plan travels with the dart, so a save mid-chain keeps the whole chain.
        /// </summary>
        private static ArcaneMissileSettings BuildSegmentSettings(
            ArcaneMissileSettings source,
            SpellCastPlan plan,
            SpellSegment segment,
            int index)
        {
            SpellTuningDef tuning = SpellDefs.Tuning;
            ArcaneMissileSettings settings = source != null
                ? source.Copy()
                : ArcaneMissileSettings.CreateDefault();

            settings.missileCount = Mathf.Max(1, segment.missileCount);
            settings.speedCellsPerSecond = segment.missileSpeed > 0f ? segment.missileSpeed : tuning.missileSpeed;
            settings.maxTurnDegreesPerSecond = tuning.missileTurnDegreesPerSecond;
            settings.maxRangeCells = Mathf.Max(tuning.castRange, settings.speedCellsPerSecond * 4f);

            // The next segment aims at a cell, not at a living target, so "target lost" would expire
            // it immediately. Out-of-bounds, lifetime and range ends still apply to it.
            settings.expireOnTargetLost = false;

            // Readable numbers for the lab/tooltip; the payload below is what really runs.
            settings.damageAmount = Mathf.Max(
                segment.LargestAmount(SpellEffectKind.Harm),
                segment.LargestAmount(SpellEffectKind.Explosion));
            settings.healAmount = segment.LargestAmount(SpellEffectKind.Heal);
            settings.explosionRadius = Mathf.Max(1.9f, segment.LargestRadius(SpellEffectKind.Explosion));

            // Frozen at launch: the missile deep-copies this and scribes it with itself.
            settings.spellPlan = plan.Copy();
            settings.spellSegmentIndex = index;
            return settings;
        }

        /// <summary>
        /// The ring that shows "the chain continues here". A missing Def must not cost the visual
        /// entirely, so the documented fallback pair still marks the point.
        /// </summary>
        private static void PlayTriggerRing(ArcaneMissileImpactContext context, Map map)
        {
            var point = new Vector3(context.ImpactX, 0f, context.ImpactZ);
            FleckDef ring = DefDatabase<FleckDef>.GetNamedSilentFail(SpellDefs.TriggerFleckDefName);
            if (ring != null)
            {
                FleckMaker.Static(point, map, ring, TriggerFleckScale);
                return;
            }

            SpellDiagnostics.ErrorOnce("fleck def " + SpellDefs.TriggerFleckDefName
                                       + " is missing; trigger points fall back to the vanilla flash.");
            FleckMaker.Static(point, map, FleckDefOf.ExplosionFlash, TriggerFleckScale);
            FleckMaker.ThrowLightningGlow(point, map, TriggerFleckScale);
        }
    }
}
