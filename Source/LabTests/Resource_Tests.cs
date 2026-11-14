using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Shared helpers for the resource tests. A test overlays its own numbers on one rune only for
    /// the moment the spell is created and puts the Def back right after: the slot keeps the frozen
    /// cost, which is exactly what is being tested.
    /// </summary>
    internal static class ResourceTestUtil
    {
        internal sealed class FreeCastingScope : System.IDisposable
        {
            private readonly bool previous = WotfMod.Settings.freeCasting;

            public FreeCastingScope()
            {
                WotfMod.Settings.freeCasting = true;
            }

            public void Dispose()
            {
                WotfMod.Settings.freeCasting = previous;
            }
        }

        public static SpellRecipe SaveCosted(TestContext t, Pawn caster, int slot, string name,
            string costedGlyph, float mana, float entropy, float experience, params string[] glyphs)
        {
            SpellGlyphDef def = TestContext.Glyph(costedGlyph);
            float oldMana = def.manaCost;
            float oldEntropy = def.entropyCost;
            float oldExperience = def.experience;
            try
            {
                def.manaCost = mana;
                def.entropyCost = entropy;
                def.experience = experience;
                bool saved = GameComponent_Spellbook.Current.SaveSlot(caster, slot,
                    TestContext.Recipe(name, glyphs), out string reason);
                t.Expect(saved, "save slot " + slot + " refused: " + reason);
            }
            finally
            {
                def.manaCost = oldMana;
                def.entropyCost = oldEntropy;
                def.experience = oldExperience;
            }

            return GameComponent_Spellbook.Current.GetSlot(caster, slot);
        }

        public static StatDef Stat(string defName)
        {
            return DefDatabase<StatDef>.GetNamed(defName);
        }

        /// <summary>Runs a cost test with the free-casting development switch off, then puts it back.</summary>
        public static void WithPaidCasting(System.Action body)
        {
            bool old = WotfMod.Settings.freeCasting;
            try
            {
                WotfMod.Settings.freeCasting = false;
                body();
            }
            finally
            {
                WotfMod.Settings.freeCasting = old;
            }
        }
    }

    /// <summary>
    /// Mana recovers by the WotF_ManaRegen stat; the worn book adds to the cap and to recovery, and
    /// a hex reduces recovery.
    /// </summary>
    [LabTest]
    public sealed class Resource_ManaRegenAndCap : LabTest
    {
        public override string Name => "resource.mana-regen-and-cap";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(1, 5);
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(caster);
            StatDef regenStat = ResourceTestUtil.Stat("WotF_ManaRegen");
            StatDef maxStat = ResourceTestUtil.Stat("WotF_ManaMax");
            Apparel book = SpellService.WornSpellbook(caster);
            float bookMax = book.def.equippedStatOffsets.GetStatOffsetFromList(maxStat);
            float bookRegen = book.def.equippedStatOffsets.GetStatOffsetFromList(regenStat);

            gene.RefreshMaxFromStats();
            float withBookMax = gene.MaxMana;
            float withBookRegen = caster.GetStatValue(regenStat);
            t.Note("maxWithBook", withBookMax);
            t.Note("regenWithBook", withBookRegen);
            t.Expect(Mathf.Abs(withBookMax - (gene.LevelManaMax + bookMax)) < 0.01f,
                "mana cap with book " + withBookMax + ", expected level " + gene.LevelManaMax + " + book " + bookMax);

            // Ten game seconds from empty: recovery is the stat per second.
            gene.Value = 0f;
            t.Step(600);
            t.Note("manaAfter10s", gene.CurrentMana);
            t.Expect(Mathf.Abs(gene.CurrentMana - withBookRegen * 10f) < withBookRegen * 0.5f,
                "mana after 10 s from empty is " + gene.CurrentMana + ", expected about " + withBookRegen * 10f);

            HediffDef hex = DefDatabase<HediffDef>.GetNamed("WotF_Hediff_Hex");
            float hexFactor = hex.stages[0].statFactors.GetStatFactorFromList(regenStat);
            caster.health.AddHediff(hex);
            float hexedRegen = caster.GetStatValue(regenStat);
            t.Note("regenHexed", hexedRegen);
            t.Expect(Mathf.Abs(hexedRegen - withBookRegen * hexFactor) < 0.01f,
                "hexed recovery " + hexedRegen + ", expected Def factor " + hexFactor + " applied to " + withBookRegen);

            caster.apparel.Remove(book);
            book.Destroy();
            gene.RefreshMaxFromStats();
            t.Note("maxWithoutBook", gene.MaxMana);
            t.Expect(Mathf.Abs(gene.MaxMana - gene.LevelManaMax) < 0.01f,
                "mana cap without book " + gene.MaxMana + ", expected the level's " + gene.LevelManaMax);
            t.Expect(Mathf.Abs(caster.GetStatValue(regenStat) - (withBookRegen - bookRegen) * hexFactor) < 0.01f,
                "recovery without book should lose exactly the book's " + bookRegen);
        }
    }

    /// <summary>
    /// The cost is frozen when the spell is created, nothing is spent before release, and at release
    /// the mana, the neural heat and the experience all move by the frozen amounts.
    /// </summary>
    [LabTest]
    public sealed class Resource_CostFrozenAndPaid : LabTest
    {
        public override string Name => "resource.cost-frozen-and-paid";

        protected override void Run(TestContext t)
        {
            ResourceTestUtil.WithPaidCasting(() => RunPaid(t));
        }

        private static void RunPaid(TestContext t)
        {
            Pawn caster = t.SpawnCaster(1, 5);
            Pawn target = t.SpawnTarget(9, 5);
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(caster);

            float defMana = TestContext.Glyph(SpellDefs.EffectHarm).manaCost;
            SpellRecipe recipe = ResourceTestUtil.SaveCosted(t, caster, 0, "costed-dart", SpellDefs.EffectHarm,
                30f, 12f, 7f, SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            if (recipe == null)
            {
                return;
            }

            t.Measure("frozenCost", recipe.cost == null ? "null" : recipe.cost.ToString());
            t.Expect(TestContext.Glyph(SpellDefs.EffectHarm).manaCost == defMana, "the Def was not restored");

            gene.RefreshMaxFromStats();
            gene.Value = gene.MaxMana;
            caster.psychicEntropy.RemoveAllEntropy();
            float manaBefore = gene.CurrentMana;
            float xpBefore = gene.Experience;
            float gain = caster.GetStatValue(StatDefOf.PsychicEntropyGain);

            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            SpellTiming timing = recipe.timing;
            if (timing.windupTicks + timing.channelTicks > 0)
            {
                t.Expect(Mathf.Approximately(gene.CurrentMana, manaBefore), "mana was spent when the cast began");
                t.Expect(caster.psychicEntropy.EntropyValue < 0.01f, "neural heat was added when the cast began");
            }

            t.StepUntil(() => gene.CurrentMana < manaBefore - 1f, 600);
            float spent = manaBefore - gene.CurrentMana;
            t.Note("manaSpent", spent);
            t.Note("entropyGained", caster.psychicEntropy.EntropyValue);
            t.Note("experienceGained", gene.Experience - xpBefore);

            // One tick of recovery may already have run after the release.
            t.Expect(Mathf.Abs(spent - recipe.cost.mana) < 0.5f,
                "mana spent " + spent + ", expected the frozen " + recipe.cost.mana);
            t.Expect(Mathf.Abs(caster.psychicEntropy.EntropyValue - recipe.cost.entropy * gain) < 0.2f,
                "neural heat " + caster.psychicEntropy.EntropyValue + ", expected " + recipe.cost.entropy + " x gain " + gain);
            t.Expect(Mathf.Abs(gene.Experience - xpBefore - recipe.cost.experience) < 0.01f,
                "experience gained " + (gene.Experience - xpBefore) + ", expected " + recipe.cost.experience);
        }
    }

    /// <summary>
    /// A cost the caster cannot pay refuses the cast up front, and a caster who can no longer pay at
    /// release loses the cast without paying anything.
    /// </summary>
    [LabTest]
    public sealed class Resource_CostRefused : LabTest
    {
        public override string Name => "resource.cost-refused";

        protected override void Run(TestContext t)
        {
            ResourceTestUtil.WithPaidCasting(() => RunPaid(t));
        }

        private static void RunPaid(TestContext t)
        {
            Pawn caster = t.SpawnCaster(1, 5);
            Pawn target = t.SpawnTarget(9, 5);
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(caster);
            gene.RefreshMaxFromStats();

            SpellRecipe manaHeavy = ResourceTestUtil.SaveCosted(t, caster, 0, "mana-heavy", SpellDefs.EffectHarm,
                50f, 0f, 0f, SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            SpellRecipe heatHeavy = ResourceTestUtil.SaveCosted(t, caster, 1, "heat-heavy", SpellDefs.EffectHarm,
                0f, 1000f, 0f, SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            if (manaHeavy == null || heatHeavy == null)
            {
                return;
            }

            gene.Value = 10f;
            bool can = SpellService.CanCast(caster, manaHeavy, out string manaReason);
            t.Measure("manaReason", manaReason ?? "null");
            t.Expect(!can, "a 50-mana spell was accepted with 10 mana");

            gene.Value = gene.MaxMana;
            can = SpellService.CanCast(caster, heatHeavy, out string heatReason);
            t.Measure("heatReason", heatReason ?? "null");
            t.Expect(!can, "a 1000-heat spell was accepted under a limit of " + caster.psychicEntropy.MaxEntropy);

            if (manaHeavy.timing.windupTicks + manaHeavy.timing.channelTicks <= 0)
            {
                t.Fail("the dart has no windup or channel; the release shortfall cannot be staged");
                return;
            }

            caster.psychicEntropy.RemoveAllEntropy();
            float before = t.InjurySeverity(target);
            if (!t.CastAt(caster, manaHeavy, target))
            {
                return;
            }

            // The caster spends the mana elsewhere while the spell is still forming.
            gene.Value = 0f;
            t.StepUntil(() => !SpellCastingService.IsCasting(caster), 600);
            t.Step(120);

            t.Note("manaAfter", gene.CurrentMana);
            t.Expect(!SpellCastingService.IsCasting(caster), "the cast never ended");
            t.Expect(Mathf.Approximately(t.InjurySeverity(target), before), "the unpaid spell still hit the target");
            t.Expect(GameComponent_Spellbook.Current.GetCooldownTicksRemaining(caster) == 0,
                "the unpaid spell started the cooldown");
        }
    }

    /// <summary>
    /// Spells belong to each pawn; the worn book only opens slots. Taking the book off stops casting
    /// without deleting anything, a smaller book seals the slots above its count, and a generated
    /// elf colonist starts with a book.
    /// </summary>
    [LabTest]
    public sealed class Spellbook_BookAndSlots : LabTest
    {
        public override string Name => "spellbook.book-and-slots";

        protected override void Run(TestContext t)
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            Pawn first = t.SpawnCaster(1, 5);
            Pawn second = t.SpawnCaster(1, 7);
            SpellRecipe dart = TestContext.Recipe("own-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm);

            Apparel book = SpellService.WornSpellbook(first);
            SpellbookExtension extension = book.def.GetModExtension<SpellbookExtension>();
            int storedSlot = extension.slotCount - 1;
            t.Expect(component.SaveSlot(first, storedSlot, dart, out string saved), "save last open slot refused: " + saved);
            t.Expect(component.GetSlot(second, storedSlot) == null, "the second caster sees the first caster's spell");

            first.apparel.Remove(book);
            bool can = SpellService.CanCast(first, dart, out string noBook);
            t.Measure("noBookReason", noBook ?? "null");
            t.Expect(!can, "a caster without a book was allowed to cast");
            t.Expect(component.GetSlot(first, storedSlot) == null, "a slot reads as open without a book");
            t.Expect(component.StoredSpellName(first, storedSlot) == "own-dart", "taking the book off lost the spell");

            first.apparel.Wear(book, false, true);
            t.Expect(component.GetSlot(first, storedSlot) != null, "the spell did not come back with the book");

            int oldCount = extension.slotCount;
            try
            {
                extension.slotCount = storedSlot;
                t.Expect(component.GetSlot(first, storedSlot) == null, "the stored slot stays open under a smaller book");
                t.Expect(!component.SaveSlot(first, extension.slotCount, dart, out string sealedReason), "a sealed slot accepted a save");
                t.Measure("sealedReason", sealedReason ?? "null");
                t.Expect(component.SaveSlot(first, extension.slotCount - 1, dart, out string openReason), "last open slot refused: " + openReason);
            }
            finally
            {
                extension.slotCount = oldCount;
            }

            t.Expect(component.GetSlot(first, storedSlot) != null, "the stored slot did not reopen with the full book");

            Pawn elf = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                DefDatabase<PawnKindDef>.GetNamed("WotF_Elf_Colonist"), Faction.OfPlayer,
                canGeneratePawnRelations: false));
            Apparel elfBook = SpellService.WornSpellbook(elf);
            t.Measure("elfBook", elfBook == null ? "null" : elfBook.def.defName);
            t.Expect(elfBook != null, "a generated elf colonist has no spellbook");
            Find.WorldPawns.PassToWorld(elf, PawnDiscardDecideMode.Discard);
        }
    }

    /// <summary>A rune that needs an unlock cannot be arranged until the colony unlocks it.</summary>
    [LabTest]
    public sealed class Spellbook_RuneUnlock : LabTest
    {
        public override string Name => "spellbook.rune-unlock";

        protected override void Run(TestContext t)
        {
            SpellGlyphDef glyph = TestContext.Glyph(SpellDefs.EffectHarm);
            SpellRecipe recipe = TestContext.Recipe("locked", SpellDefs.FormSelf, SpellDefs.EffectHarm);
            bool old = glyph.requiresUnlock;
            try
            {
                glyph.requiresUnlock = true;

                // Unlocks are permanent; an earlier run in the same game already unlocked this rune.
                if (GameComponent_Spellbook.IsUnlocked(glyph))
                {
                    t.Note("unlockedByEarlierRun", true);
                }
                else
                {
                    t.Expect(!SpellService.Validate(recipe, out string reason), "a locked rune passed validation");
                    t.Measure("lockedReason", reason ?? "null");
                    t.Expect(GameComponent_Spellbook.Current.Unlock(glyph), "Unlock reported the rune as already unlocked");
                }

                t.Expect(GameComponent_Spellbook.IsUnlocked(glyph), "Unlock did not unlock the rune");
                t.Expect(SpellService.Validate(recipe, out string after), "the unlocked rune is still refused: " + after);
            }
            finally
            {
                glyph.requiresUnlock = old;
            }
        }
    }
}
