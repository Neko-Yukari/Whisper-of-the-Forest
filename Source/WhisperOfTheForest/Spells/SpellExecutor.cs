using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace WhisperOfTheForest.Spells
{
    public static class SpellExecutor
    {
        public static string Execute(SpellCastPlan plan, Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            return ExecuteStepList(plan.steps, caster, primary, cell, map);
        }
        public static string ExecuteStepList(IReadOnlyList<SpellEffect> steps,
            Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            if (steps == null || map == null) return "no-plan";
            var report = new StringBuilder();
            for (int i = 0; i < steps.Count; i++)
            {
                if (report.Length > 0) report.Append(' ');
                report.Append(steps[i].DiagnosticName).Append('=');
                report.Append(steps[i].Execute(caster, primary, cell, map));
            }
            return report.Length == 0 ? "no-effects" : report.ToString();
        }
        public static List<SpellEffect> CopySteps(IReadOnlyList<SpellEffect> steps)
        {
            var copy = new List<SpellEffect>(steps?.Count ?? 0);
            if (steps != null)
                for (int i = 0; i < steps.Count; i++) copy.Add(steps[i]);
            return copy;
        }
        public static List<Thing> CollectTargets(bool useArea, float targetRadius,
            Thing primary, IntVec3 cell, Map map, Predicate<Thing> accept)
        {
            var found = new List<Thing>();
            if (!useArea)
            {
                Thing target = primary ?? FirstAt(cell, map, accept);
                if (target != null && accept(target)) found.Add(target);
                return found;
            }
            if (!cell.InBounds(map)) return found;
            var seen = new HashSet<Thing>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, targetRadius, true))
                if (thing != null && accept(thing) && seen.Add(thing)) found.Add(thing);
            return found;
        }
        private static Thing FirstAt(IntVec3 cell, Map map, Predicate<Thing> accept)
        {
            if (!cell.InBounds(map)) return null;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (accept(things[i])) return things[i];
            return null;
        }

    }
}
