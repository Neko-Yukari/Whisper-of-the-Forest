using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Teleports the colony's humanlike pawns near the altar to the chosen gate. The gate is the
    /// anchor; the altar is where the rite is performed. The altar has already refused a closed or
    /// destroyed gate both at assessment and right before paying (CompRitualAltar), and it has already
    /// settled where every passenger lands: this worker only carries out that plan, so it never has to
    /// invent a cell after the mana is gone.
    /// </summary>
    public sealed class RitualEffect_Teleport : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            Building target = context.TeleportTarget;
            CompTeleportGate gate = target?.GetComp<CompTeleportGate>();
            if (target == null || gate == null || !target.Spawned || target.Map != context.Map)
            {
                Messages.Message(
                    "WotF_Ritual_Teleport_NoGate".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            Dictionary<Pawn, IntVec3> landing = context.TeleportLanding;
            if (landing == null || landing.Count == 0)
            {
                // The altar settles every landing cell before it charges and refuses a rite with nobody to
                // carry. A rite that arrives here without a plan was started outside that path, and
                // inventing cells now would be papering over it.
                WotfLog.Error(WotfLog.ModuleRitual,
                    "teleport rite applied without a landing plan; nobody was moved");
                return;
            }

            foreach (KeyValuePair<Pawn, IntVec3> step in landing)
            {
                step.Key.Position = step.Value;
                step.Key.Notify_Teleported();
            }

            Messages.Message(
                "WotF_Ritual_Teleport_Done".Translate(landing.Count, target.LabelCap),
                target,
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
