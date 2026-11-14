using RimWorld;
using Verse;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Writes one of the host's spellbook pages into the chosen weapon or armour. The altar checked
    /// in the same tick that the item still lies in range and the page still holds an injectable
    /// spell (CompRitualAltar.CompleteRitual), so this only stores the copy.
    /// </summary>
    public sealed class RitualEffect_InscribeEquipment : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            CompEnchantedEquipment enchant = context.Equipment.TryGetComp<CompEnchantedEquipment>();
            SpellRecipe recipe = GameComponent_Spellbook.Current.GetSlot(context.Caster, context.BookSlot);

            int slot = enchant.Inscribe(recipe);
            WotfLog.Info(WotfLog.ModuleRitual, "inscribed '" + recipe.name + "' into "
                + context.Equipment.LabelShort + " slot " + (slot + 1) + " by " + context.Caster.LabelShort);
            Messages.Message(
                "WotF_Ritual_Inscribe_Done".Translate(context.Equipment.LabelShort, recipe.name, slot + 1),
                context.Equipment, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
