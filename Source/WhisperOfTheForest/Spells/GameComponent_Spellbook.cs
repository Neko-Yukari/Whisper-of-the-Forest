using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The saved book: ten recipe slots plus, per pawn, which slot is currently selected and when
    /// that pawn last cast. Everything here is scribed with the game, so a save restores the book,
    /// the per-pawn selection and the cooldown instead of starting blank.
    ///
    /// This component is the only owner of saved spells. The book page edits a private draft and
    /// calls <see cref="SaveSlot"/>; the runtime reads <see cref="GetSlot"/> copies. Neither side
    /// keeps a second copy of a recipe.
    /// </summary>
    public class GameComponent_Spellbook : GameComponent
    {
        /// <summary>Documented slot count; the book draws exactly this many bookmarks.</summary>
        public const int SlotCount = 10;

        /// <summary>How often the carrier ability is granted to newly eligible pawns, in ticks.</summary>
        private const int GrantIntervalTicks = 250;

        private static GameComponent_Spellbook current;

        private List<SpellRecipe> slots = new List<SpellRecipe>(SlotCount);
        private Dictionary<Pawn, int> activeSlots = new Dictionary<Pawn, int>();
        private Dictionary<Pawn, int> lastCastTicks = new Dictionary<Pawn, int>();

        // Scribe_Collections requires the same working lists across LoadingVars and
        // ResolvingCrossRefs when a dictionary has Reference keys; without them a save with
        // vanished pawn references logs "Cannot fill dictionary because there are no values".
        [Unsaved] private List<Pawn> activeSlotKeys = new List<Pawn>();
        [Unsaved] private List<int> activeSlotValues = new List<int>();
        [Unsaved] private List<Pawn> lastCastKeys = new List<Pawn>();
        [Unsaved] private List<int> lastCastValues = new List<int>();

        public GameComponent_Spellbook(Game game)
        {
            current = this;
            EnsureSlotList();
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

        /// <summary>An independent copy of the saved recipe, or null when the slot is empty.</summary>
        public SpellRecipe GetSlot(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return null;
            }

            EnsureSlotList();
            SpellRecipe saved = slots[slot];
            return saved?.Copy();
        }

        /// <summary>
        /// Validates first, then stores a copy. A refused save never touches the current value and
        /// always explains itself with player-facing text.
        /// </summary>
        public bool SaveSlot(int slot, SpellRecipe recipe, out string reason)
        {
            reason = null;
            if (!IsValidSlot(slot))
            {
                reason = SpellText.TF("WotF_Spells.Reason.SlotOutOfRange", SlotCount);
                return false;
            }

            if (!SpellService.Validate(recipe, out reason))
            {
                return false;
            }

            if (recipe.name.NullOrEmpty())
            {
                reason = SpellText.T("WotF_Spells.Reason.NeedName");
                return false;
            }

            EnsureSlotList();

            // Creation is what freezes the timing: the chain's numbers become this spell's own, so a
            // later Def edit cannot change a spell that is already in the book.
            SpellRecipe stored = recipe.Copy();
            stored.timing = SpellTiming.Derive(stored.glyphs);
            slots[slot] = stored;
            return true;
        }

        public void ClearSlot(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }

            EnsureSlotList();
            slots[slot] = null;
        }

        /// <summary>Selected slot for this pawn, or -1 when the pawn has not chosen one.</summary>
        public int GetActiveSlot(Pawn pawn)
        {
            if (pawn == null || activeSlots == null)
            {
                return -1;
            }

            return activeSlots.TryGetValue(pawn, out int slot) && IsValidSlot(slot) ? slot : -1;
        }

        public void SetActiveSlot(Pawn pawn, int slot)
        {
            if (pawn == null || !IsValidSlot(slot))
            {
                return;
            }

            if (activeSlots == null)
            {
                activeSlots = new Dictionary<Pawn, int>();
            }

            activeSlots[pawn] = slot;
        }

        /// <summary>The pawn's currently selected recipe, or null when there is none.</summary>
        public SpellRecipe GetActiveRecipe(Pawn pawn)
        {
            return GetSlot(GetActiveSlot(pawn));
        }

        /// <summary>Ticks still to wait before this pawn may cast again; 0 when ready.</summary>
        public int GetCooldownTicksRemaining(Pawn pawn)
        {
            if (pawn == null || lastCastTicks == null || !lastCastTicks.TryGetValue(pawn, out int lastTick))
            {
                return 0;
            }

            int elapsed = Find.TickManager.TicksGame - lastTick;
            int remaining = SpellDefs.Tuning.cooldownTicks - elapsed;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>Starts the casting cooldown for this pawn only; nobody else is affected.</summary>
        public void NotifyCast(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (lastCastTicks == null)
            {
                lastCastTicks = new Dictionary<Pawn, int>();
            }

            lastCastTicks[pawn] = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            EnsureSlotList();
            Scribe_Collections.Look(ref slots, "slots", LookMode.Deep);
            // Empty/partially-pruned pawn maps are legal: a fresh colony has no cast history, and a
            // loaded save may drop a reference whose pawn is gone. The explicit working lists and
            // logNullErrors=false keep that normal case out of the error log.
            Scribe_Collections.Look(ref activeSlots, "activeSlots", LookMode.Reference, LookMode.Value,
                ref activeSlotKeys, ref activeSlotValues, false);
            Scribe_Collections.Look(ref lastCastTicks, "lastCastTicks", LookMode.Reference, LookMode.Value,
                ref lastCastKeys, ref lastCastValues, false);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                EnsureSlotList();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            current = this;
            EnsureSlotList();
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
        /// Keeps the carrier ability on every pawn that becomes eligible later (psylink raised by a
        /// ritual, a new colony member). Only the passed pawns are inspected, one scan every few
        /// seconds; no per-tick work and no global state.
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

        private void PruneMissingPawns()
        {
            Prune(activeSlots);
            Prune(lastCastTicks);
        }

        /// <summary>
        /// Drops entries whose pawn died or was removed. Rebuilt rather than Remove(null): a save
        /// that lost a pawn reference yields a null key, and Dictionary.Remove(null) throws.
        /// </summary>
        private static void Prune(Dictionary<Pawn, int> map)
        {
            if (map == null || map.Count == 0)
            {
                return;
            }

            bool dirty = false;
            foreach (KeyValuePair<Pawn, int> pair in map)
            {
                if (pair.Key == null || pair.Key.Destroyed || pair.Key.Dead)
                {
                    dirty = true;
                    break;
                }
            }

            if (!dirty)
            {
                return;
            }

            var kept = new List<KeyValuePair<Pawn, int>>(map.Count);
            foreach (KeyValuePair<Pawn, int> pair in map)
            {
                if (pair.Key != null && !pair.Key.Destroyed && !pair.Key.Dead)
                {
                    kept.Add(pair);
                }
            }

            map.Clear();
            for (int i = 0; i < kept.Count; i++)
            {
                map[kept[i].Key] = kept[i].Value;
            }
        }

        private void EnsureSlotList()
        {
            if (slots == null)
            {
                slots = new List<SpellRecipe>(SlotCount);
            }

            while (slots.Count < SlotCount)
            {
                slots.Add(null);
            }

            // A save that once carried more slots must not shift the bookmarks around.
            while (slots.Count > SlotCount)
            {
                slots.RemoveAt(slots.Count - 1);
            }
        }

        private static bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < SlotCount;
        }
    }
}
