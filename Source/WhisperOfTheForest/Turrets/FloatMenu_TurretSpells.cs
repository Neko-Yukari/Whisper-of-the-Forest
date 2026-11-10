using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Turrets
{
    public sealed class FloatMenu_TurretSpells : FloatMenu
    {
        public IReadOnlyList<FloatMenuOption> Options => options.AsReadOnly();

        public FloatMenu_TurretSpells(CompSpellTurret turret) : this(CreateOptions(turret))
        {
        }

        private FloatMenu_TurretSpells(List<FloatMenuOption> rows) : base(rows)
        {
            // Vanilla sorts disabled rows last; retain colonist/slot order.
            options = rows;
        }

        private static List<FloatMenuOption> CreateOptions(CompSpellTurret turret)
        {
            var result = new List<FloatMenuOption>();
            foreach (TurretSpellCandidate candidate in turret.GetInscriptionCandidates())
            {
                string label = SpellText.TF("WotF_Turret_Candidate", candidate.Owner.LabelShort,
                    candidate.Slot + 1, candidate.Recipe.name);
                if (candidate.UnavailableReason != null)
                {
                    result.Add(new FloatMenuOption(label + " — " + candidate.UnavailableReason, null));
                    continue;
                }
                result.Add(new FloatMenuOption(label, () =>
                {
                    if (!turret.TryRequestInscription(candidate.Recipe, out string reason))
                        Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }));
            }
            return result;
        }
    }
}
