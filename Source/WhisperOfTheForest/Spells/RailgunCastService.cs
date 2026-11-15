using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The composite form's payload: it hands the semantic charge request to the visual layer when the
    /// channel starts, and sweeps the full-penetration corridor on the tick the channel ends.
    ///
    /// The corridor is swept once from the caster's position at the moment it fires, through
    /// everything in its width, up to the spell range or the map edge. Obstacles are simply targets:
    /// nothing stops the ray, and each thing is settled at most once.
    ///
    /// The cast itself (when it starts, how long the channel lasts, when the shot fires) belongs to
    /// <see cref="SpellCastingService"/>; this service never keeps a timer of its own.
    /// </summary>
    public static class RailgunCastService
    {
        /// <summary>
        /// Asks the visual layer for the charge effect of the channel that is starting. The aim cell is
        /// already locked by the cast; the beam itself is derived by the effect from the caster's live
        /// position when the charge ends, so walking during the channel changes the origin and not the
        /// aim. A visual failure never changes what the cast will do.
        /// </summary>
        public static void PlayCharge(Map map, Pawn caster, IntVec3 aimCell, SpellRailgunSpec spec, int channelTicks)
        {
            if (map == null || caster == null || spec == null)
            {
                return;
            }

            IntVec3 origin = caster.Position;
            if (!TryAim(map, origin, aimCell, caster, spec, out float dirX, out float dirZ, out float length))
            {
                return;
            }

            var request = new VfxRequest
            {
                map = map,
                follow = caster,
                origin = origin.ToVector3Shifted(),
                target = aimCell.ToVector3Shifted(),
                direction = new Vector3(dirX, 0f, dirZ),
                length = length,
                width = spec.hitWidth,
                chargeTicks = channelTicks,
                vfxDefName = spec.vfxDef
            };

            VfxService.Play(request);
        }

        /// <summary>
        /// Resolves one shot. Called by <see cref="SpellCastingService"/> exactly once per cast, on the
        /// tick the channel ends, and never from the visual layer.
        /// </summary>
        public static string Resolve(Map map, Pawn caster, IntVec3 aimCell, SpellRailgunSpec spec)
        {
            if (map == null || spec == null)
            {
                return "railgun dropped: missing cast";
            }

            if (caster == null || caster.Destroyed || !caster.Spawned || caster.Map != map)
            {
                // The caster died, left the map or was carried away during the channel. There is no
                // firing point and no caster to exclude, so the shot is dropped instead of guessed.
                SpellDiagnostics.Warning("railgun dropped: the caster left the map during the channel");
                return "railgun dropped: caster gone";
            }

            if (!GenGrid.InBounds(aimCell, map))
            {
                SpellDiagnostics.Warning("railgun dropped: the locked aim cell is outside the map");
                return "railgun dropped: aim out of bounds";
            }

            IntVec3 origin = caster.Position;
            if (!TryAim(map, origin, aimCell, caster, spec, out float dirX, out float dirZ, out float length))
            {
                return "railgun dropped: no direction";
            }

            float halfWidth = spec.hitWidth * 0.5f;

            // Snapshot before settling. Damage despawns the hit thing and spawns a corpse, filth or
            // other new things, so iterating map.listerThings.AllThings while settling would shift
            // the indices: later things get skipped and new ones may be hit twice.
            List<Thing> targets = CollectTargets(map, caster, origin, dirX, dirZ, halfWidth, length);

            var counts = new Dictionary<string, int>();
            int hits = 0;
            foreach (Thing thing in targets)
            {
                string defName = thing.def.defName;
                IntVec3 position = thing.Position;
                int hpBefore = thing.HitPoints;

                SpellDamage.Deal(thing, spec.damage, spec.armorPenetration, caster);

                int hpAfter = thing.Destroyed ? 0 : thing.HitPoints;
                hits++;
                counts.TryGetValue(defName, out int seen);
                counts[defName] = seen + 1;

                SpellDiagnostics.Debug("railgun hit " + defName + "@" + position + " hp " + hpBefore
                                        + "->" + hpAfter);
            }

            string byDef = Describe(counts);
            SpellDiagnostics.Message("railgun fired origin=" + origin + " aim=" + aimCell + " length="
                                     + length.ToString("0.##") + " hits=" + hits + " by def " + byDef);
            return "railgun hits=" + hits;
        }

        /// <summary>
        /// Everything the corridor crosses, snapped into its own list while the map is untouched.
        /// What counts as hittable is the shared <see cref="SpellTargets.CanTakeDamage"/> rule; a
        /// multi-cell building crosses once.
        /// </summary>
        private static List<Thing> CollectTargets(
            Map map,
            Pawn caster,
            IntVec3 origin,
            float dirX,
            float dirZ,
            float halfWidth,
            float length)
        {
            var targets = new List<Thing>();
            var seen = new HashSet<Thing>();
            List<Thing> things = map.listerThings?.AllThings;
            if (things == null)
            {
                return targets;
            }

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || thing == caster || !SpellTargets.CanTakeDamage(thing))
                {
                    continue;
                }

                if (!Crosses(thing, origin, dirX, dirZ, halfWidth, length))
                {
                    continue;
                }

                if (seen.Add(thing))
                {
                    targets.Add(thing);
                }
            }

            return targets;
        }

        /// <summary>"Wall×1, Human×1, Plant_Grass×37": most frequent first, then by name.</summary>
        private static string Describe(Dictionary<string, int> counts)
        {
            var pairs = new List<KeyValuePair<string, int>>(counts);
            pairs.Sort((left, right) => left.Value != right.Value
                ? right.Value.CompareTo(left.Value)
                : string.CompareOrdinal(left.Key, right.Key));

            if (pairs.Count == 0)
            {
                return "none";
            }

            var parts = new string[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                parts[i] = pairs[i].Key + "×" + pairs[i].Value;
            }

            return string.Join(", ", parts);
        }

        /// <summary>True when any cell of the thing lies inside the corridor (a multi-cell building hits once).</summary>
        private static bool Crosses(Thing thing, IntVec3 origin, float dirX, float dirZ, float halfWidth, float length)
        {
            CellRect occupied = thing.OccupiedRect();
            foreach (IntVec3 cell in occupied.Cells)
            {
                if (RailgunGeometry.WithinCorridor(origin.x + 0.5f, origin.z + 0.5f, dirX, dirZ,
                        cell.x + 0.5f, cell.z + 0.5f, halfWidth, length, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Direction and usable length from the firing point towards the locked aim cell. When the
        /// caster stands on the aim cell there is no direction towards it, so the caster's facing is
        /// used; with no facing either, the cast is dropped instead of firing somewhere arbitrary.
        /// </summary>
        private static bool TryAim(
            Map map,
            IntVec3 origin,
            IntVec3 aimCell,
            Pawn caster,
            SpellRailgunSpec spec,
            out float dirX,
            out float dirZ,
            out float length)
        {
            length = 0f;
            if (!RailgunGeometry.TryDirection(origin.x + 0.5f, origin.z + 0.5f, aimCell.x + 0.5f, aimCell.z + 0.5f,
                    out dirX, out dirZ))
            {
                IntVec3 facing = caster.Rotation.FacingCell;
                if (!RailgunGeometry.TryDirection(0f, 0f, facing.x, facing.z, out dirX, out dirZ))
                {
                    return false;
                }
            }

            float maxRange = spec.range > 0f
                ? spec.range
                : Mathf.Sqrt(map.Size.x * (float)map.Size.x + map.Size.z * (float)map.Size.z);
            return RailgunGeometry.TryLength(origin.x + 0.5f, origin.z + 0.5f, dirX, dirZ, maxRange,
                map.Size.x, map.Size.z, out length);
        }
    }
}
