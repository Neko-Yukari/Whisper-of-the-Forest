using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    public class CompProperties_SpellStaff : CompProperties
    {
        public bool isOneHanded;
        public bool allowsSuperGlyphs;
        public bool allowsShieldBelt;
        public float rangeFactor;
        public float windupFactor;
        public float spellPowerFactor;
        public float bluntArmorCostReduction;
        public float minManaFactor;
        public float heatArmorDurationBonus;
        public float maxDurationFactor;
        public float maxDamageFactor;

        public CompProperties_SpellStaff()
        {
            compClass = typeof(CompSpellStaff);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (rangeFactor <= 0f)
            {
                yield return parentDef.defName + " rangeFactor must be greater than 0";
            }

            if (windupFactor <= 0f)
            {
                yield return parentDef.defName + " windupFactor must be greater than 0";
            }

            if (spellPowerFactor <= 0f)
            {
                yield return parentDef.defName + " spellPowerFactor must be greater than 0";
            }

            if (bluntArmorCostReduction < 0f || bluntArmorCostReduction > 1f)
            {
                yield return parentDef.defName + " bluntArmorCostReduction must be in [0, 1]";
            }

            if (minManaFactor <= 0f || minManaFactor > 1f)
            {
                yield return parentDef.defName + " minManaFactor must be in (0, 1]";
            }

            if (heatArmorDurationBonus < 0f)
            {
                yield return parentDef.defName + " heatArmorDurationBonus must not be negative";
            }

            if (maxDurationFactor < 1f)
            {
                yield return parentDef.defName + " maxDurationFactor must be at least 1";
            }

            if (maxDamageFactor <= 0f || maxDamageFactor < spellPowerFactor)
            {
                yield return parentDef.defName + " maxDamageFactor must be positive and at least spellPowerFactor";
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }

            ThingDef stuff = req.StuffDef;
            SpellStaffModifiers mod = SpellStaffModifiers.For(this, stuff);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffWielding".Translate(),
                (isOneHanded ? "WotF_Stat_StaffWielding_Offhand" : "WotF_Stat_StaffWielding_TwoHanded").Translate(),
                "WotF_Stat_StaffWielding_Desc".Translate(),
                5399);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffAllowsSuper".Translate(),
                allowsSuperGlyphs ? "Yes".Translate() : "No".Translate(),
                "WotF_Stat_StaffAllowsSuper_Desc".Translate(),
                5398);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffAllowsShieldBelt".Translate(),
                allowsShieldBelt ? "Yes".Translate() : "No".Translate(),
                "WotF_Stat_StaffAllowsShieldBelt_Desc".Translate(),
                5397);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffRangeFactor".Translate(),
                mod.rangeFactor.ToStringPercent(),
                "WotF_Stat_StaffRangeFactor_Desc".Translate(),
                5396);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffWindupFactor".Translate(),
                mod.windupFactor.ToStringPercent(),
                "WotF_Stat_StaffWindupFactor_Desc".Translate(),
                5395);

            string damageReport = string.Format(
                "WotF_Stat_StaffDamageFactor_Desc".Translate(),
                spellPowerFactor.ToStringPercent(),
                maxDamageFactor.ToStringPercent());
            string manaReport = string.Format(
                "WotF_Stat_StaffManaFactor_Desc".Translate(),
                minManaFactor.ToStringPercent());
            string durationReport = string.Format(
                "WotF_Stat_StaffDurationFactor_Desc".Translate(),
                maxDurationFactor.ToStringPercent());

            if (stuff != null)
            {
                float sharp = stuff.GetStatValueAbstract(StatDefOf.SharpDamageMultiplier);
                float bluntDmg = stuff.GetStatValueAbstract(StatDefOf.BluntDamageMultiplier);
                float avgDmg = (sharp + bluntDmg) * 0.5f;
                float bluntArmor = stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Blunt);
                float heatArmor = stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Heat);

                damageReport += "\n\n" + string.Format(
                    "WotF_Stat_StaffDamageFactor_Material".Translate(),
                    stuff.LabelCap,
                    sharp.ToStringPercent(),
                    bluntDmg.ToStringPercent(),
                    avgDmg.ToStringPercent());
                manaReport += "\n\n" + string.Format(
                    "WotF_Stat_StaffManaFactor_Material".Translate(),
                    stuff.LabelCap,
                    bluntArmor.ToStringPercent());
                durationReport += "\n\n" + string.Format(
                    "WotF_Stat_StaffDurationFactor_Material".Translate(),
                    stuff.LabelCap,
                    heatArmor.ToStringPercent());
            }
            else
            {
                string noStuff = "\n\n" + "WotF_Stat_StaffNoMaterial".Translate();
                damageReport += noStuff;
                manaReport += noStuff;
                durationReport += noStuff;
            }

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffDamageFactor".Translate(),
                mod.damageFactor.ToStringPercent(),
                damageReport,
                5394);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffManaFactor".Translate(),
                mod.manaFactor.ToStringPercent(),
                manaReport,
                5393);

            yield return new StatDrawEntry(
                StatCategoryDefOf.Weapon_Ranged,
                "WotF_Stat_StaffDurationFactor".Translate(),
                mod.durationFactor.ToStringPercent(),
                durationReport,
                5392);
        }
    }

    /// <summary>
    /// The staff or wand's saved memory: which bookmark of its wielder's spells its attack casts.
    /// Only the slot index is stored, never a copy of the recipe, so editing that bookmark changes
    /// what the staff casts, and a staff handed to another caster casts that caster's spell in the
    /// slot.
    ///
    /// This is the equipment's <see cref="CompEquippable"/> on purpose. Primary staves surface
    /// their slot gizmo natively through <c>Pawn_EquipmentTracker.GetGizmos</c>; offhand wands
    /// (<see cref="CompProperties_SpellStaff.isOneHanded"/>) surface theirs through
    /// <see cref="Patch_EquipmentTracker_GetGizmos"/>.
    /// </summary>
    public class CompSpellStaff : CompEquippable
    {
        /// <summary>No bookmark chosen yet; the weapon then has no spell and cannot attack.</summary>
        public const int NoSlot = -1;

        private int slot = NoSlot;

        public CompProperties_SpellStaff Props => (CompProperties_SpellStaff)props;

        /// <summary>Bookmark index this staff casts, or <see cref="NoSlot"/>.</summary>
        public int Slot => slot;

        /// <summary>
        /// What this staff's Def and material apply to one cast. Recomputed from current props and
        /// stuff so stuff changes are visible immediately and nothing is copied into a save.
        /// </summary>
        public SpellStaffModifiers Modifiers => SpellStaffModifiers.For(Props, parent.Stuff);

        /// <summary>
        /// The wielder's recipe in the chosen bookmark, or null when nobody holds the staff or the
        /// slot is empty or sealed.
        /// </summary>
        public SpellRecipe SelectedRecipe => GameComponent_Spellbook.Current?.GetSlot(Holder, slot);

        /// <summary>
        /// True when this staff does not allow shield belts and <paramref name="pawn"/> is wearing
        /// apparel with a ranged-blocking <see cref="CompShield"/>.
        /// </summary>
        public bool IsBlockedByShieldBelt(Pawn pawn)
        {
            return !Props.allowsShieldBelt && HasRangedBlockingShield(pawn);
        }

        public bool CanCastThrough(Pawn pawn, out string reason)
        {
            if (IsBlockedByShieldBelt(pawn))
            {
                reason = "WotF_Equipment.Reason.TwoHandedShieldBlock".Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public static bool HasRangedBlockingShield(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null)
            {
                return false;
            }

            for (int i = 0; i < worn.Count; i++)
            {
                CompShield shield = worn[i].GetComp<CompShield>();
                if (shield != null && shield.Props.blocksRangedWeapons)
                {
                    return true;
                }
            }

            return false;
        }

        public static CompSpellStaff FindPrimary(Pawn pawn)
        {
            return pawn?.equipment?.Primary?.GetComp<CompSpellStaff>();
        }

        public static CompSpellStaff FindOffhand(Pawn pawn)
        {
            Pawn_EquipmentTracker eq = pawn?.equipment;
            if (eq == null)
            {
                return null;
            }

            ThingWithComps primary = eq.Primary;
            List<ThingWithComps> list = eq.AllEquipmentListForReading;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == primary)
                {
                    continue;
                }

                CompSpellStaff staff = list[i].GetComp<CompSpellStaff>();
                if (staff != null && staff.Props.isOneHanded)
                {
                    return staff;
                }
            }

            return null;
        }

        public static CompSpellStaff FindEquipped(Pawn pawn)
        {
            return FindPrimary(pawn) ?? FindOffhand(pawn);
        }

        public static CompSpellStaff FindForSuper(Pawn pawn)
        {
            Pawn_EquipmentTracker eq = pawn?.equipment;
            if (eq == null)
            {
                return null;
            }

            CompSpellStaff primary = FindPrimary(pawn);
            if (primary != null && primary.Props.allowsSuperGlyphs)
            {
                return primary;
            }

            List<ThingWithComps> list = eq.AllEquipmentListForReading;
            for (int i = 0; i < list.Count; i++)
            {
                CompSpellStaff staff = list[i].GetComp<CompSpellStaff>();
                if (staff != null && staff.Props.allowsSuperGlyphs)
                {
                    return staff;
                }
            }

            return null;
        }

        public static CompSpellStaff FindForCast(Pawn pawn, SpellRecipe recipe)
        {
            if (recipe?.glyphs != null && recipe.glyphs.Count > 0)
            {
                SpellGlyphDef first = SpellDefs.Glyph(recipe.glyphs[0]);
                if (first != null && first.kind == SpellGlyphKind.Super)
                {
                    return FindForSuper(pawn);
                }
            }

            return FindEquipped(pawn);
        }

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
            string line = recipe == null
                ? SpellText.T("WotF_Equipment.Inspect.NoSpell")
                : SpellText.TF("WotF_Equipment.Inspect.Current", recipe.name);
            if (Holder != null && IsBlockedByShieldBelt(Holder))
            {
                line += "\n" + "WotF_Equipment.Reason.TwoHandedShieldBlock".Translate();
            }

            return line;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref slot, "spellSlot", NoSlot);
        }

        private Gizmo SlotCommand()
        {
            SpellRecipe recipe = SelectedRecipe;
            string staffLabel = parent.def.LabelCap;
            return new Command_Action
            {
                defaultLabel = recipe == null
                    ? SpellText.TF("WotF_Equipment.Gizmo.NoSpell", staffLabel)
                    : SpellText.TF("WotF_Equipment.Gizmo.Current", staffLabel, recipe.name),
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
