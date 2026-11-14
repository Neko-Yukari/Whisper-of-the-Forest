using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The saved spells of every caster plus the colony's rune unlocks. Each pawn owns ten recipe
    /// slots, its selected slot and its last cast tick; which runes may be arranged at all is shared
    /// by the whole colony. Everything here is scribed with the game.
    ///
    /// Spells belong to the pawn, not to the worn book. The book only decides how many of the pawn's
    /// slots are open (<see cref="SpellService.OpenSlotCount"/>): a slot beyond that keeps its spell
    /// but reads as empty until a book with more slots is worn again, so taking a book off or
    /// swapping it never loses a spell.
    ///
    /// This component is the only owner of saved spells. The book page edits a private draft and
    /// calls <see cref="SaveSlot"/>; the runtime reads <see cref="GetSlot"/> copies. Neither side
    /// keeps a second copy of a recipe.
    /// </summary>
    public class GameComponent_Spellbook : GameComponent
    {
        /// <summary>Most slots a pawn can own; the book draws exactly this many bookmarks.</summary>
        public const int SlotCount = 10;

        /// <summary>How often the carrier ability is granted to newly eligible pawns, in ticks.</summary>
        private const int GrantIntervalTicks = 250;

        private static GameComponent_Spellbook current;

        private Dictionary<Pawn, PawnSpellbook> books = new Dictionary<Pawn, PawnSpellbook>();
        private List<string> unlockedGlyphs = new List<string>();

        // Scribe_Collections requires the same working lists across LoadingVars and
        // ResolvingCrossRefs when a dictionary has Reference keys; without them a save with
        // vanished pawn references logs "Cannot fill dictionary because there are no values".
        [Unsaved] private List<Pawn> bookKeys = new List<Pawn>();
        [Unsaved] private List<PawnSpellbook> bookValues = new List<PawnSpellbook>();

        public GameComponent_Spellbook(Game game)
        {
            current = this;
        }

        /// <summary>The live component, or null before a game is loaded. Never a second instance.</summary>
        public static GameComponent_Spellbook Current
        {
            get
            {
                if (current == null)
                {
                    current = Verse.Current.Game?.GetComponent<GameComponent_Spellbook>();
                }

                return current;
            }
        }

        // ---------------------------------------------------------------- slots

        /// <summary>True when the slot exists and the pawn's worn book opens it.</summary>
        public static bool IsSlotOpen(Pawn pawn, int slot)
        {
            return IsValidSlot(slot) && slot < SpellService.OpenSlotCount(pawn);
        }

        /// <summary>
        /// An independent copy of the pawn's saved recipe, or null when the slot is empty or sealed
        /// by the worn book.
        /// </summary>
        public SpellRecipe GetSlot(Pawn pawn, int slot)
        {
            if (!IsSlotOpen(pawn, slot))
            {
                return null;
            }

            return BookOf(pawn, false)?.slots[slot]?.Copy();
        }

        /// <summary>Name of the spell kept in a slot, sealed or not; for the bookmark strip only.</summary>
        public string StoredSpellName(Pawn pawn, int slot)
        {
            return IsValidSlot(slot) ? BookOf(pawn, false)?.slots[slot]?.name : null;
        }

        /// <summary>
        /// Validates first, then stores a copy with its timing and cost frozen. A refused save never
        /// touches the current value and always explains itself with player-facing text.
        /// </summary>
        public bool SaveSlot(Pawn pawn, int slot, SpellRecipe recipe, out string reason)
        {
            reason = null;
            if (pawn == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoPawn");
                return false;
            }

            if (!IsValidSlot(slot))
            {
                reason = SpellText.TF("WotF_Spells.Reason.SlotOutOfRange", SlotCount);
                return false;
            }

            if (!IsSlotOpen(pawn, slot))
            {
                reason = SpellText.TF("WotF_Spells.Reason.SlotSealed", slot + 1);
                return false;
            }

            if (!SpellService.Validate(recipe, out reason))
            {
                return false;
            }

            foreach (string name in recipe.glyphs)
            {
                if (!SpellService.CanUseGlyph(pawn, SpellDefs.Glyph(name), out reason))
                    return false;
            }

            if (recipe.name.NullOrEmpty())
            {
                reason = SpellText.T("WotF_Spells.Reason.NeedName");
                return false;
            }

            // Creation is what freezes the numbers: the chain's timing, cost and target categories
            // become this spell's own, so a later Def edit cannot change a spell already in the book.
            SpellRecipe stored = recipe.Copy();
            stored.timing = SpellTiming.Derive(stored.glyphs);
            stored.cost = SpellCost.Derive(stored.glyphs);
            stored.targets = SpellCompiler.DeriveTargets(stored);
            BookOf(pawn, true).slots[slot] = stored;
            return true;
        }

        public void ClearSlot(Pawn pawn, int slot)
        {
            PawnSpellbook book = BookOf(pawn, false);
            if (book != null && IsValidSlot(slot))
            {
                book.slots[slot] = null;
            }
        }

        /// <summary>Selected slot for this pawn, or -1 when the pawn has not chosen one.</summary>
        public int GetActiveSlot(Pawn pawn)
        {
            PawnSpellbook book = BookOf(pawn, false);
            return book != null && IsValidSlot(book.activeSlot) ? book.activeSlot : -1;
        }

        public void SetActiveSlot(Pawn pawn, int slot)
        {
            if (pawn == null || !IsValidSlot(slot))
            {
                return;
            }

            BookOf(pawn, true).activeSlot = slot;
        }

        /// <summary>The pawn's currently selected recipe, or null when there is none or it is sealed.</summary>
        public SpellRecipe GetActiveRecipe(Pawn pawn)
        {
            return GetSlot(pawn, GetActiveSlot(pawn));
        }

        // ---------------------------------------------------------------- cooldown

        /// <summary>Ticks still to wait before this pawn may cast again; 0 when ready.</summary>
        public int GetCooldownTicksRemaining(Pawn pawn)
        {
            PawnSpellbook book = BookOf(pawn, false);
            if (book == null || book.lastCastTick < 0)
            {
                return 0;
            }

            int elapsed = Find.TickManager.TicksGame - book.lastCastTick;
            int remaining = SpellDefs.Tuning.cooldownTicks - elapsed;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>Starts the casting cooldown for this pawn only; nobody else is affected.</summary>
        public void NotifyCast(Pawn pawn)
        {
            if (pawn != null)
            {
                BookOf(pawn, true).lastCastTick = Find.TickManager.TicksGame;
            }
        }

        // ---------------------------------------------------------------- unlocks

        /// <summary>
        /// True when the colony may arrange this rune: it needs no unlock, or it has been unlocked
        /// in the current game. Without a game nothing that needs an unlock is open.
        /// </summary>
        public static bool IsUnlocked(SpellGlyphDef glyph)
        {
            if (glyph == null || !glyph.requiresUnlock)
            {
                return true;
            }

            WotfSettings settings = WotfMod.Settings;
            if (settings != null && settings.unlockAllGlyphs)
            {
                return true;
            }

            return IsUnlockedInData(glyph);
        }

        /// <summary>
        /// The colony's own unlock list, ignoring the development override.
        /// </summary>
        public static bool IsUnlockedInData(SpellGlyphDef glyph)
        {
            if (glyph == null || !glyph.requiresUnlock)
            {
                return true;
            }

            GameComponent_Spellbook component = Current;
            return component != null && component.unlockedGlyphs.Contains(glyph.defName);
        }

        /// <summary>
        /// Unlocks a rune for the whole colony. Research, ruins, quests and rituals all unlock
        /// through here. Returns false when it was already unlocked.
        /// </summary>
        public bool Unlock(SpellGlyphDef glyph)
        {
            if (glyph == null || unlockedGlyphs.Contains(glyph.defName))
            {
                return false;
            }

            unlockedGlyphs.Add(glyph.defName);
            SpellDiagnostics.Message("rune unlocked for the colony: " + glyph.defName);
            return true;
        }

        // ---------------------------------------------------------------- lifecycle

        public override void ExposeData()
        {
            base.ExposeData();
            // A loaded save may drop a reference whose pawn is gone; the explicit working lists and
            // logNullErrors=false keep that normal case out of the error log.
            Scribe_Collections.Look(ref books, "pawnBooks", LookMode.Reference, LookMode.Deep,
                ref bookKeys, ref bookValues, false);
            Scribe_Collections.Look(ref unlockedGlyphs, "unlockedGlyphs", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (books == null)
                {
                    books = new Dictionary<Pawn, PawnSpellbook>();
                }

                if (unlockedGlyphs == null)
                {
                    unlockedGlyphs = new List<string>();
                }
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            current = this;
            PruneMissingPawns();
            SpellService.GrantCastAbilityToEligiblePawns();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            current = this;
            SpellService.GrantCastAbilityToEligiblePawns();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            current = this;
            SpellService.GrantCastAbilityToEligiblePawns();
        }

        /// <summary>
        /// Keeps the carrier ability on every pawn that becomes eligible later (a new colony member,
        /// a gene implant). One scan every few seconds; no per-tick work.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager.TicksGame % GrantIntervalTicks != 0)
            {
                return;
            }

            SpellService.GrantCastAbilityToEligiblePawns();
        }

        private PawnSpellbook BookOf(Pawn pawn, bool create)
        {
            if (pawn == null)
            {
                return null;
            }

            if (books.TryGetValue(pawn, out PawnSpellbook book))
            {
                return book;
            }

            if (!create)
            {
                return null;
            }

            book = new PawnSpellbook();
            books[pawn] = book;
            return book;
        }

        /// <summary>
        /// Drops the spells of pawns that no longer exist. A dead pawn keeps them: it can still be
        /// resurrected. Rebuilt rather than Remove(null): a save that lost a pawn reference yields a
        /// null key, and Dictionary.Remove(null) throws.
        /// </summary>
        private void PruneMissingPawns()
        {
            var kept = new Dictionary<Pawn, PawnSpellbook>(books.Count);
            foreach (KeyValuePair<Pawn, PawnSpellbook> pair in books)
            {
                if (pair.Key != null && !pair.Key.Discarded && pair.Value != null)
                {
                    kept[pair.Key] = pair.Value;
                }
            }

            books = kept;
        }

        private static bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < SlotCount;
        }
    }

    /// <summary>One pawn's spells: its slots, the selected slot and when it last cast.</summary>
    public class PawnSpellbook : IExposable
    {
        public List<SpellRecipe> slots = NewSlots();
        public int activeSlot = -1;
        public int lastCastTick = -1;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref slots, "slots", LookMode.Deep);
            Scribe_Values.Look(ref activeSlot, "activeSlot", -1);
            Scribe_Values.Look(ref lastCastTick, "lastCastTick", -1);
        }

        private static List<SpellRecipe> NewSlots()
        {
            var list = new List<SpellRecipe>(GameComponent_Spellbook.SlotCount);
            for (int i = 0; i < GameComponent_Spellbook.SlotCount; i++)
            {
                list.Add(null);
            }

            return list;
        }
    }
}
