using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Right-clicking an altar with a pawn selected: one [优先主持 X] per rite of the altar, with or without
    /// offerings. Before Assess it runs the checks vanilla's prioritize menu runs, in vanilla's order and
    /// with vanilla's keys (FloatMenuOptionProvider_WorkGivers.cs:136-168), and the order is taken as
    /// prioritized work under WotF_HostRitual (Pawn_JobTracker.TryTakeOrderedJobPrioritizedWork).
    /// Rites needing a choice a right-click cannot make (gate, item and page, ruin, an unprepared rune)
    /// point at the panel. Vanilla discovers providers by reflection (FloatMenuMakerMap.cs:21);
    /// WotF_HostRitual is not directOrderable so the vanilla prioritize menu does not list the altar a
    /// second time.
    /// </summary>
    public class FloatMenuOptionProvider_HostRitual : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            CompRitualAltar altar = clickedThing.TryGetComp<CompRitualAltar>();
            if (altar == null)
            {
                yield break;
            }

            Pawn pawn = context.FirstSelectedPawn;
            WorkGiver giver = DefDatabase<WorkGiverDef>.GetNamed("WotF_HostRitual").Worker;
            string pawnProblem = VanillaProblem(pawn, (WorkGiver_Scanner)giver, clickedThing);
            foreach (RitualDef def in altar.Rituals)
            {
                RitualRequest request = altar.DirectRequest(def, pawn);
                string label = "WotF_Ritual_PrioritizeHost".Translate(request?.Rune == null
                    ? (string)def.LabelCap
                    : def.LabelCap + ": " + request.Rune.LabelCap);
                if (pawnProblem != null)
                {
                    yield return new FloatMenuOption(label + ": " + pawnProblem, null);
                    continue;
                }

                if (request == null)
                {
                    yield return new FloatMenuOption(label + ": " + "WotF_Ritual_ChooseInPanel".Translate(), null);
                    continue;
                }

                RitualAssessment assessment = altar.Assess(request);
                if (!assessment.CanStart)
                {
                    yield return new FloatMenuOption(label + ": " + assessment.FirstProblem, null);
                    continue;
                }

                yield return FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(label, delegate { altar.TryStart(request, prioritizedBy: giver); }),
                    pawn, clickedThing);
            }
        }

        private static string VanillaProblem(Pawn pawn, WorkGiver_Scanner giver, Thing altar)
        {
            PawnCapacityDef capacity = giver.MissingRequiredCapacity(pawn);
            if (capacity != null)
            {
                return "CannotMissingHealthActivities".Translate(capacity.label);
            }

            if (pawn.WorkTagIsDisabled(giver.def.workTags))
            {
                return "CannotPrioritizeWorkGiverDisabled".Translate(giver.def.label);
            }

            if (pawn.CurJob != null && pawn.CurJob.def.defName == "WotF_PerformRitual"
                && pawn.CurJob.targetA.Thing == altar)
            {
                return "CannotGenericAlreadyAm".Translate(giver.def.gerund, altar.LabelShort, altar);
            }

            WorkTypeDef workType = giver.def.workType;
            if (pawn.workSettings.GetPriority(workType) == 0)
            {
                if (pawn.WorkTypeIsDisabled(workType) || !"CannotPrioritizeNotAssignedToWorkType".CanTranslate())
                {
                    return "CannotPrioritizeWorkTypeDisabled".Translate(
                        pawn.WorkTypeIsDisabled(workType) ? workType.gerundLabel : workType.pawnLabel);
                }

                return "CannotPrioritizeNotAssignedToWorkType".Translate(workType.gerundLabel);
            }

            if (altar.IsForbidden(pawn))
            {
                return altar.Position.InAllowedArea(pawn)
                    ? (string)"CannotPrioritizeForbidden".Translate(altar.Label, altar)
                    : "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() + " ("
                        + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label + ")";
            }

            if (!pawn.CanReach(altar, giver.PathEndMode, Danger.Deadly))
            {
                return "NoPath".Translate().CapitalizeFirst();
            }

            return null;
        }
    }
}
