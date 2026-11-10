using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Teleports the colony's humanlike pawns near the altar to the chosen node. The node is the
    /// anchor; the altar is where the rite is performed.
    /// </summary>
    public sealed class RitualEffect_Teleport : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            Building target = context.TeleportTarget;
            if (target == null || !target.Spawned || target.Map != context.Map)
            {
                Messages.Message(
                    "WotF_Ritual_Teleport_NoNode".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            List<Pawn> passengers = new List<Pawn>();
            foreach (Pawn pawn in context.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.IsColonistPlayerControlled
                    && pawn.Position.InHorDistOf(context.Altar.Position, context.EffectRadius))
                {
                    passengers.Add(pawn);
                }
            }

            if (passengers.Count == 0)
            {
                Messages.Message(
                    "WotF_Ritual_Teleport_NoPassengers".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            foreach (Pawn pawn in passengers)
            {
                IntVec3 cell = CellFinder.TryRandomClosewalkCellNear(
                    target.Position,
                    context.Map,
                    3,
                    out IntVec3 found,
                    c => c.GetFirstPawn(context.Map) == null)
                    ? found
                    : target.Position;
                pawn.Position = cell;
                pawn.Notify_Teleported();
            }

            Messages.Message(
                "WotF_Ritual_Teleport_Done".Translate(passengers.Count, target.LabelCap),
                target,
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
