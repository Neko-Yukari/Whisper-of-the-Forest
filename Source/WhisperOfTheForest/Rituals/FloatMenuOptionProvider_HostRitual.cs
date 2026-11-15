using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Right-clicking an altar with a pawn selected: one [优先主持 X] per rite of the altar, with or without
    /// offerings. An option hosts through the same order as the panel's start (CompRitualAltar.TryStart,
    /// then Commit when the host job starts); one that cannot start is disabled with Assess's first
    /// problem. Rites needing a choice a right-click cannot make (gate, item and page, ruin, an
    /// unprepared rune) point at the panel. Vanilla discovers providers by reflection
    /// (FloatMenuMakerMap.cs:21); WotF_HostRitual is not directOrderable so the vanilla
    /// prioritize menu does not list the altar a second time.
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
            foreach (RitualDef def in altar.Rituals)
            {
                RitualRequest request = altar.DirectRequest(def, pawn);
                string label = "WotF_Ritual_PrioritizeHost".Translate(request?.Rune == null
                    ? (string)def.LabelCap
                    : def.LabelCap + ": " + request.Rune.LabelCap);
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
                    new FloatMenuOption(label, delegate { altar.TryStart(request); }), pawn, clickedThing);
            }
        }
    }
}
