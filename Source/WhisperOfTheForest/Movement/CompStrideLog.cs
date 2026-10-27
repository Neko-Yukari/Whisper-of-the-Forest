using Verse;

namespace WhisperOfTheForest.Experimental.Movement
{
    /// <summary>
    /// Logs equip/unequip of the movement experiment items together with the pawn's
    /// counters so a real test can show "worn = effect, removed = restored" without
    /// adding any persistent movement state.
    /// </summary>
    public class CompProperties_StrideLog : CompProperties
    {
        public CompProperties_StrideLog()
        {
            compClass = typeof(CompStrideLog);
        }
    }

    public class CompStrideLog : ThingComp
    {
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            Log("equip", pawn);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            Log("unequip", pawn);
        }

        private void Log(string evt, Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            string defName = parent != null && parent.def != null ? parent.def.defName : "<none>";
            string defLabel = parent != null && parent.def != null ? parent.def.label : defName;
            string key = evt == "equip" ? "WotF_Movement_Equipped" : "WotF_Movement_Unequipped";
            string localized = key.Translate(defLabel, StrideDiagnostics.DescribeLocalized(pawn));
            StrideDiagnostics.LogDirect(
                pawn,
                "apparel",
                evt + " " + defName + " | " + localized + " | " + StrideDiagnostics.Describe(pawn));
        }
    }
}
