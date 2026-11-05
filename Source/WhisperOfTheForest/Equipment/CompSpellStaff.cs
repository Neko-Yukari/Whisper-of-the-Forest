using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// The staff's saved memory: which bookmark of its wielder's spells its attack casts. Only the
    /// slot index is stored, never a copy of the recipe, so editing that bookmark changes what the
    /// staff casts, and a staff handed to another caster casts that caster's spell in the slot.
    ///
    /// This is the equipment's <see cref="CompEquippable"/> on purpose. An equipped item reaches the
    /// pawn's gizmo row through <c>Pawn_EquipmentTracker.GetGizmos</c>, which asks the primary
    /// equipment's <c>CompGetEquippedGizmosExtra</c>; a plain ThingComp on the same weapon would only
    /// show gizmos while the weapon itself is selected on the ground.
    /// </summary>
    public class CompSpellStaff : CompEquippable
    {
        /// <summary>No bookmark chosen yet; the weapon then has no spell and cannot attack.</summary>
        public const int NoSlot = -1;

        private int slot = NoSlot;

        /// <summary>Bookmark index this staff casts, or <see cref="NoSlot"/>.</summary>
        public int Slot => slot;

        /// <summary>
        /// The wielder's recipe in the chosen bookmark, or null when nobody holds the staff or the
        /// slot is empty or sealed.
        /// </summary>
        public SpellRecipe SelectedRecipe => GameComponent_Spellbook.Current?.GetSlot(Holder, slot);

        public void SetSlot(int index)
        {
            if (index < 0 || index >= GameComponent_Spellbook.SlotCount)
            {
                return;
            }

            slot = index;
            WotfLog.Debug(StaffDefs.LogModule, "staff slot set to " + (index + 1));
        }

        public void ClearSlot()
        {
            slot = NoSlot;
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            yield return SlotCommand();
        }

        public override string CompInspectStringExtra()
        {
            SpellRecipe recipe = SelectedRecipe;
            return recipe == null
                ? SpellText.T("WotF_Equipment.Inspect.NoSpell")
                : SpellText.TF("WotF_Equipment.Inspect.Current", recipe.name);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref slot, "spellSlot", NoSlot);

            // A save from a build with a different bookmark count must not leave the staff pointing
            // at a slot that no longer exists.
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && (slot < 0 || slot >= GameComponent_Spellbook.SlotCount))
            {
                slot = NoSlot;
            }
        }

        private Gizmo SlotCommand()
        {
            SpellRecipe recipe = SelectedRecipe;
            return new Command_Action
            {
                defaultLabel = recipe == null
                    ? SpellText.T("WotF_Equipment.Gizmo.NoSpell")
                    : SpellText.TF("WotF_Equipment.Gizmo.Current", recipe.name),
                defaultDesc = SpellText.T("WotF_Equipment.Gizmo.Desc"),
                icon = parent.def.uiIcon,
                action = OpenSlotMenu
            };
        }

        private void OpenSlotMenu()
        {
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            var options = new List<FloatMenuOption>();

            if (book == null)
            {
                options.Add(new FloatMenuOption(SpellText.T("WotF_Equipment.Gizmo.NoBook"), null));
            }
            else
            {
                for (int i = 0; i < GameComponent_Spellbook.SlotCount; i++)
                {
                    int index = i;
                    SpellRecipe recipe = book.GetSlot(Holder, index);
                    if (recipe == null)
                    {
                        continue;
                    }

                    options.Add(new FloatMenuOption(
                        SpellText.TF("WotF_Equipment.Menu.Slot", index + 1, recipe.name),
                        delegate { SetSlot(index); }));
                }
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption(SpellText.T("WotF_Equipment.Gizmo.NoSavedSpell"), null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
