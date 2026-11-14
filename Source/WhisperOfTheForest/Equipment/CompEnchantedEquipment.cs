using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    public class CompProperties_EnchantedEquipment : CompProperties
    {
        /// <summary>Slots of the ordinary material; mithril opens <see cref="mithrilSlots"/> instead.</summary>
        public int slots;

        public int mithrilSlots;

        /// <summary>The one material that opens the second slot; read from the item's current Stuff.</summary>
        public ThingDef mithrilStuff;

        /// <summary>Reaction mana multiplier of that material. Applied alone, never together with the staff formula.</summary>
        public float mithrilCostFactor;

        /// <summary>Chance per hit or per damage taken that the reaction fires; 1 for weapons.</summary>
        public float triggerChance;

        /// <summary>Ticks the item waits after a reaction before it may fire again; 0 for weapons.</summary>
        public int triggerCooldownTicks;

        public CompProperties_EnchantedEquipment()
        {
            compClass = typeof(CompEnchantedEquipment);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (slots < 1)
                yield return parentDef.defName + " enchanted equipment needs at least one slot.";
            if (mithrilSlots < slots)
                yield return parentDef.defName + " mithrilSlots must not be smaller than slots.";
            if (mithrilStuff == null)
                yield return parentDef.defName + " enchanted equipment must name the mithrilStuff that opens its extra slots.";
            if (mithrilCostFactor <= 0f)
                yield return parentDef.defName + " mithrilCostFactor must be positive.";
            if (triggerChance <= 0f || triggerChance > 1f)
                yield return parentDef.defName + " triggerChance must be in (0, 1].";
            if (triggerCooldownTicks < 0)
                yield return parentDef.defName + " triggerCooldownTicks must not be negative.";
        }
    }

    /// <summary>
    /// The spells written into one weapon or one piece of armour. Each slot keeps its own frozen
    /// recipe copy, like <see cref="WhisperOfTheForest.Turrets.CompSpellTurret"/>: the equipment does
    /// not remember a book slot, so taking the book off or handing the item to somebody else changes
    /// nothing. The reaction pays out of the user's own bloodline mana and resolves instantly through
    /// <see cref="SpellExecutor"/>, never through the three-phase casting service
    /// (技术/02-魔法实现.md:133, 134).
    /// </summary>
    public class CompEnchantedEquipment : ThingComp
    {
        private List<SpellRecipe> spells;
        private int activeSlot;
        private int lastTriggerTick = -999999;

        // One reaction per damage chain: two reaction armours must not ping-pong each other through
        // the damage they deal. The depth is raised for the whole execution of a reaction and any
        // reaction asked for inside it is refused, then it drops back to zero.
        private static int reactionDepth;

        public CompProperties_EnchantedEquipment Props => (CompProperties_EnchantedEquipment)props;

        /// <summary>Slots this very item has: the material decides, so it is read every time.</summary>
        public int SlotCount => Props.mithrilStuff != null && parent.Stuff == Props.mithrilStuff
            ? Props.mithrilSlots
            : Props.slots;

        public bool IsMithril => Props.mithrilStuff != null && parent.Stuff == Props.mithrilStuff;

        public int ActiveSlot => activeSlot >= 0 && activeSlot < SlotCount ? activeSlot : 0;

        public SpellRecipe ActiveSpell => SpellAt(ActiveSlot);

        public bool HasSpell => ActiveSpell != null;

        /// <summary>An independent copy of one slot, or null when it is empty or out of range.</summary>
        public SpellRecipe SpellAt(int index)
        {
            return spells != null && index >= 0 && index < spells.Count ? spells[index]?.Copy() : null;
        }

        public void SetActiveSlot(int index)
        {
            if (index >= 0 && index < SlotCount)
            {
                activeSlot = index;
            }
        }

        /// <summary>
        /// Stores one written spell with its timing, cost and target categories frozen the same way
        /// <see cref="GameComponent_Spellbook.SaveSlot"/> freezes a book page. The first empty slot
        /// takes it; when every slot is taken the active slot is overwritten and nothing is refunded.
        /// </summary>
        public int Inscribe(SpellRecipe recipe)
        {
            EnsureCapacity();
            int index = spells.FindIndex(stored => stored == null);
            if (index < 0 || index >= SlotCount)
            {
                index = ActiveSlot;
            }

            SpellRecipe stored = recipe.Copy();
            stored.timing = SpellTiming.Derive(stored.glyphs);
            stored.cost = SpellCost.Derive(stored.glyphs);
            stored.targets = SpellCompiler.DeriveTargets(stored);
            spells[index] = stored;
            return index;
        }

        /// <summary>Forms that may be written into equipment: the hit or the wearer supplies the target.</summary>
        public static bool AcceptsRecipe(SpellRecipe recipe, out string reason)
        {
            SpellCastPlan plan = SpellCompiler.Compile(recipe, out reason);
            if (plan == null)
            {
                return false;
            }

            if (plan.form == SpellFormKind.Touch || plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit)
            {
                return true;
            }

            reason = "WotF_Ritual_EquipmentForm".Translate();
            return false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref spells, "enchantedSpells", LookMode.Deep);
            Scribe_Values.Look(ref activeSlot, "enchantedActiveSlot");
            Scribe_Values.Look(ref lastTriggerTick, "enchantedLastTriggerTick", -999999);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && spells == null)
            {
                spells = new List<SpellRecipe>();
            }
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            // Worn apparel is the only way an armour exposes gizmos; weapons go through CompEnchantedWeapon.
            foreach (Gizmo gizmo in SlotGizmos())
            {
                yield return gizmo;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (SlotCount <= 1)
            {
                SpellRecipe only = ActiveSpell;
                return only == null
                    ? SpellText.T("WotF_Enchant.Inspect.None")
                    : SpellText.TF("WotF_Enchant.Inspect.One", only.name);
            }

            var names = new List<string>(SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                SpellRecipe recipe = SpellAt(i);
                names.Add((i + 1) + ". " + (recipe == null ? SpellText.T("WotF_Enchant.Inspect.Empty") : recipe.name));
            }

            return SpellText.TF("WotF_Enchant.Inspect.Slots", string.Join("  ", names.ToArray()));
        }

        /// <summary>A weapon hit resolved its damage already: the wielder's reaction fires here.</summary>
        public void TriggerOnHit(Pawn user, Thing target)
        {
            if (user == null || target == null || !HasSpell)
            {
                return;
            }

            if (!TryOpenReaction())
            {
                return;
            }

            try
            {
                React(user, target);
            }
            finally
            {
                EndReaction();
            }
        }

        /// <summary>Armour: the wearer has just been hit, before the damage was applied to the body.</summary>
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            Pawn wearer = (parent as Apparel)?.Wearer;
            if (wearer == null || !HasSpell)
            {
                return;
            }

            // Only blows dealt by someone else answer: fire, falls and self-inflicted damage have no attacker
            // to retaliate against (技术/02, user ruling 2026-11-14).
            Thing attacker = dinfo.Instigator;
            if (attacker == null || attacker == wearer)
            {
                return;
            }

            if (!TryOpenReaction())
            {
                return;
            }

            try
            {
                React(wearer, attacker);
            }
            finally
            {
                EndReaction();
            }
        }

        public IEnumerable<Gizmo> SlotGizmos()
        {
            if (SlotCount <= 1)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = ActiveSpell == null
                    ? SpellText.TF("WotF_Enchant.Gizmo.None", ActiveSlot + 1)
                    : SpellText.TF("WotF_Enchant.Gizmo.Current", ActiveSlot + 1, ActiveSpell.name),
                defaultDesc = SpellText.T("WotF_Enchant.Gizmo.Desc"),
                icon = parent.def.uiIcon,
                action = OpenSlotMenu
            };
        }

        private void OpenSlotMenu()
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < SlotCount; i++)
            {
                int index = i;
                SpellRecipe recipe = SpellAt(index);
                options.Add(new FloatMenuOption(
                    SpellText.TF("WotF_Enchant.Menu.Slot", index + 1,
                        recipe == null ? SpellText.T("WotF_Enchant.Inspect.Empty") : recipe.name),
                    delegate { SetActiveSlot(index); }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// One attempt: chance, cooldown, bloodline mana, then the spell. Anything missing means this
        /// hit simply stays a plain hit (技术/02-魔法实现.md:133) and nothing is spent.
        /// </summary>
        private void React(Pawn user, Thing target)
        {
            int now = Find.TickManager.TicksGame;
            if (Props.triggerCooldownTicks > 0 && now - lastTriggerTick < Props.triggerCooldownTicks)
            {
                return;
            }

            if (!Rand.Chance(Props.triggerChance))
            {
                return;
            }

            SpellRecipe recipe = ActiveSpell;
            if (recipe == null)
            {
                return;
            }

            if (Gene_ForestBloodline.Get(user) == null)
            {
                WotfLog.Debug(WotfLog.ModuleSpell, parent.LabelShort + " reaction skipped: no forest bloodline");
                return;
            }

            SpellRecipe cast = recipe.Copy();
            if (IsMithril)
            {
                cast.cost.mana *= Props.mithrilCostFactor;
            }

            SpellCastPlan plan = SpellCompiler.Compile(cast, out string reason);
            if (plan == null)
            {
                SpellDiagnostics.Warning("enchanted " + parent.def.defName + " kept a spell that no longer compiles: " + reason);
                return;
            }

            if (!SpellService.CanAfford(user, plan.cost, out reason))
            {
                WotfLog.Debug(WotfLog.ModuleSpell, parent.LabelShort + " reaction skipped: " + reason);
                return;
            }

            if (plan.form == SpellFormKind.Orbit)
            {
                SpellOrbitService.Spawn(user.Map, user, plan);
            }
            else if (plan.form == SpellFormKind.Self)
            {
                SpellExecutor.Execute(plan, user, user, user.Position, user.Map);
            }
            else if (target != null && target.Spawned && target != user && target.Map == user.Map)
            {
                SpellExecutor.Execute(plan, user, target, target.Position, user.Map);
            }
            else
            {
                return;
            }

            SpellService.PayFor(user, plan.cost);
            lastTriggerTick = now;
            WotfLog.Debug(WotfLog.ModuleSpell, parent.LabelShort + " reaction '" + plan.recipeName
                + "' (slot " + (ActiveSlot + 1) + ") on " + user.LabelShort
                + (target == null ? " around the wearer" : " at " + target.LabelShort));
        }

        private static bool TryOpenReaction()
        {
            if (reactionDepth > 0)
            {
                return false;
            }

            reactionDepth = 1;
            return true;
        }

        private static void EndReaction()
        {
            reactionDepth = 0;
        }

        private void EnsureCapacity()
        {
            if (spells == null)
            {
                spells = new List<SpellRecipe>();
            }

            while (spells.Count < SlotCount)
            {
                spells.Add(null);
            }
        }
    }

    public class CompProperties_EnchantedWeapon : CompProperties
    {
        public CompProperties_EnchantedWeapon()
        {
            compClass = typeof(CompEnchantedWeapon);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (parentDef.comps == null || !parentDef.comps.Exists(comp => comp is CompProperties_EnchantedEquipment))
                yield return parentDef.defName + " CompEnchantedWeapon needs a CompProperties_EnchantedEquipment on the same weapon.";
        }
    }

    /// <summary>
    /// A weapon's <see cref="CompEquippable"/>, replaced on purpose: an equipped weapon only reaches
    /// the pawn's gizmo row through its own equipment comp
    /// (Verse/Pawn_EquipmentTracker.cs:235 asks PrimaryEq.CompGetEquippedGizmosExtra), so the mithril
    /// slot switch has to hang off this comp rather than off the plain ThingComp that holds the data.
    /// It adds no behaviour of its own.
    /// </summary>
    public class CompEnchantedWeapon : CompEquippable
    {
        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            CompEnchantedEquipment enchant = parent.GetComp<CompEnchantedEquipment>();
            if (enchant == null)
            {
                yield break;
            }

            foreach (Gizmo gizmo in enchant.SlotGizmos())
            {
                yield return gizmo;
            }
        }
    }
}
