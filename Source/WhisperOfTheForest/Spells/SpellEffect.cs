using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    public abstract class SpellEffect : IExposable
    {
        public abstract string DiagnosticName { get; }
        public virtual SoundDef ResolveSound => null;
        public abstract int Execute(Thing caster, Thing primary, IntVec3 cell, Map map);
        public abstract void ExposeData();

        public virtual void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + DiagnosticName);
        }

        protected static string SummaryIndent(int indent)
        {
            if (indent <= 0)
            {
                return string.Empty;
            }

            return new string(' ', indent * 2) + "↳ ";
        }

        protected static string FormatSeconds(int ticks)
        {
            return ((float)ticks / GenTicks.TicksPerRealSecond).ToString("0.#");
        }

        protected static string FormatAreaSuffix(bool useArea, float radius)
        {
            return useArea ? SpellText.TF("WotF_SpellPlan.AreaSuffix", radius.ToString("0.#")) : string.Empty;
        }

        protected static string FormatApSuffix(float armorPenetration)
        {
            return armorPenetration > 0f
                ? SpellText.TF("WotF_SpellPlan.ApSuffix", armorPenetration.ToStringPercent("0"))
                : string.Empty;
        }

        protected static void AppendPayloadSummary(SpellPayload payload, List<string> lines, int indent)
        {
            if (payload?.steps == null)
            {
                return;
            }

            for (int i = 0; i < payload.steps.Count; i++)
            {
                payload.steps[i].AppendSummary(lines, indent + 1);
            }
        }

        internal static void CheckLoaded(List<SpellEffect> effects, string owner)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars || effects == null) return;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (effects[i] != null) continue;
                SpellDiagnostics.Error(owner + " contains a null spell effect at index " + i);
                effects.RemoveAt(i);
            }
        }
    }
}
