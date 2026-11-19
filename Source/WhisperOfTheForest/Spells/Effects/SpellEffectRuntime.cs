using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Building blocks the new spell effects share: a timed state, a temporary light, a teleport and
    /// the vanilla flying carrier. Every number they use comes from the compiled step, which in turn
    /// came from glyph Defs, so nothing here is a tunable.
    /// </summary>
    internal static class SpellEffectRuntime
    {
        /// <summary>
        /// Builds a Def-driven hediff with its lifetime set, without adding it yet. The caller may
        /// write instance fields (a sustained wound's damage, for example) first.
        /// </summary>
        public static Hediff MakeTimedHediff(Pawn pawn, string defName, int durationTicks)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                SpellDiagnostics.Error("hediff def " + defName + " is missing; that spell state was skipped.");
                return null;
            }

            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears == null)
            {
                SpellDiagnostics.Error("hediff def " + defName + " has no Disappears comp; its duration was ignored.");
            }
            else if (durationTicks > 0)
            {
                disappears.SetDuration(durationTicks);
            }

            return hediff;
        }

        /// <summary>
        /// Adds a Def-driven hediff and sets its lifetime through the Disappears comp the Def
        /// declares. Returns the added (or merged) hediff, or null when the Def is missing - a broken
        /// catalogue entry is reported once instead of throwing away the whole cast.
        /// </summary>
        public static Hediff AddTimedHediff(Pawn pawn, string defName, int durationTicks)
        {
            Hediff hediff = MakeTimedHediff(pawn, defName, durationTicks);
            if (hediff == null)
            {
                return null;
            }

            pawn.health.AddHediff(hediff);
            return hediff;
        }

        /// <summary>The pawn's first hediff of that Def, or null.</summary>
        public static Hediff FindHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            return def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
        }

        /// <summary>
        /// A light source that expires on its own. The ThingDef owns the glower and lifespan comps;
        /// the cast supplies radius, colour and how long it lasts, all through the compiled step.
        /// </summary>
        public static bool SpawnTemporaryLight(Map map, IntVec3 cell, MagelightEffect step)
        {
            if (!GenGrid.InBounds(cell, map))
            {
                return false;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingMagelight);
            if (def == null)
            {
                SpellDiagnostics.Error("thing def " + SpellEffectDefs.ThingMagelight + " is missing; no light was placed.");
                return false;
            }

            var light = (ThingWithComps)ThingMaker.MakeThing(def);
            CompGlower glower = light.GetComp<CompGlower>();
            if (step.LightRadius > 0f)
            {
                glower.GlowRadius = step.LightRadius;
            }
            if (!step.LightColor.NullOrEmpty() && ColorUtility.TryParseHtmlString(step.LightColor, out Color color))
            {
                glower.GlowColor = new ColorInt(color);
            }

            // CompLifespan counts up from age; starting part-way through its Def-configured lifetime
            // is how one Def serves every duration the spell can freeze.
            CompLifespan lifespan = light.GetComp<CompLifespan>();
            if (step.LifetimeTicks > 0 && lifespan.Props.lifespanTicks > step.LifetimeTicks)
            {
                lifespan.age = lifespan.Props.lifespanTicks - step.LifetimeTicks;
            }

            GenSpawn.Spawn(light, cell, map);
            return true;
        }

        internal static int ApplyTimedState(int durationTicks, System.Collections.Generic.List<Thing> targets, string hediffDefName)
        {
            int affected = 0;
            foreach (Thing target in targets)
            {
                if (target is Pawn pawn)
                {
                    AddTimedHediff(pawn, hediffDefName, durationTicks);
                    affected++;
                }
            }
            return affected;
        }

        internal static int ApplyMove(float distance, System.Collections.Generic.List<Thing> targets, IntVec3 cell, Map map, bool toward)
        {
            int affected = 0;
            foreach (Thing target in targets)
                if (target is Pawn pawn && TryMovePawn(pawn, cell, distance, toward, map)) affected++;
            return affected;
        }

        internal static int SpawnField(SpellPayload payload, int lifetimeTicks, float radius, int intervalTicks,
            SpellFieldMode mode, Thing caster, IntVec3 cell, Map map)
        {
            if (!GenGrid.InBounds(cell, map) || payload == null || payload.steps.Count == 0) return 0;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingSpellField);
            if (def == null)
            {
                SpellDiagnostics.Error("thing def " + SpellEffectDefs.ThingSpellField + " is missing; the field was not placed.");
                return 0;
            }
            var field = (Thing_SpellField)ThingMaker.MakeThing(def);
            field.Configure(mode, caster, payload.steps, lifetimeTicks, radius, intervalTicks);
            GenSpawn.Spawn(field, cell, map);
            return 1;
        }

        /// <summary>Moves a pawn to a standable, revealed cell and keeps its pathing state sane.</summary>
        public static bool TryTeleport(Pawn pawn, IntVec3 cell, Map map)
        {
            if (pawn == null || map == null || !GenGrid.InBounds(cell, map))
            {
                return false;
            }

            if (!cell.Standable(map) || cell.Fogged(map))
            {
                return false;
            }

            pawn.Position = cell;
            pawn.Notify_Teleported();
            return true;
        }

        /// <summary>
        /// Knockback and pull through the vanilla flying carrier. <paramref name="toward"/> false
        /// pushes away from <paramref name="origin"/>, true drags the pawn towards it. The farthest
        /// valid cell is used so a body does not stop one step from where the spell meant to send it.
        /// </summary>
        public static bool TryMovePawn(Pawn pawn, IntVec3 origin, float distance, bool toward, Map map)
        {
            if (pawn == null || map == null || distance < 1f)
            {
                return false;
            }

            if (!TryDestination(pawn, origin, Mathf.RoundToInt(distance), toward, map, out IntVec3 dest))
            {
                return false;
            }

            ThingDef flyerDef = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingPawnFlyer);
            if (flyerDef == null)
            {
                SpellDiagnostics.Error("thing def " + SpellEffectDefs.ThingPawnFlyer + " is missing; nothing was moved.");
                return false;
            }

            PawnFlyer flyer = PawnFlyer.MakeFlyer(flyerDef, pawn, dest, null, null, false, null, null, default(LocalTargetInfo));
            if (flyer == null)
            {
                return false;
            }

            GenSpawn.Spawn(flyer, dest, map);
            return true;
        }

        private static bool TryDestination(
            Pawn pawn,
            IntVec3 origin,
            int cells,
            bool toward,
            Map map,
            out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            IntVec3 from = pawn.Position;
            int dx = from.x - origin.x;
            int dz = from.z - origin.z;
            if (dx == 0 && dz == 0)
            {
                // No direction to push along: send it the way it is facing.
                IntVec3 facing = pawn.Rotation.FacingCell;
                dx = facing.x;
                dz = facing.z;
            }

            dx = System.Math.Sign(dx);
            dz = System.Math.Sign(dz);
            if (toward)
            {
                dx = -dx;
                dz = -dz;
            }

            for (int step = cells; step >= 1; step--)
            {
                var candidate = new IntVec3(from.x + dx * step, 0, from.z + dz * step);
                if (JumpUtility.ValidJumpTarget(pawn, map, candidate))
                {
                    dest = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>The cell one step in front of the pawn, or its own cell when it faces nowhere.</summary>
        public static IntVec3 ForwardCell(Pawn pawn, int cells)
        {
            IntVec3 facing = pawn.Rotation.FacingCell;
            return new IntVec3(
                pawn.Position.x + facing.x * cells,
                0,
                pawn.Position.z + facing.z * cells);
        }
    }
}
