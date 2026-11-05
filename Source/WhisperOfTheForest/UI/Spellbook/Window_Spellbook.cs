using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Experimental.Spells;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// The two-page magic book: rune catalogue on the left page, the spell bar on the right page,
    /// ten slot bookmarks along the spine.
    ///
    /// Like Ars Nouveau, the current slot <em>is</em> the editing slot: clicking a bookmark makes it
    /// the pawn's active slot immediately, and the bar shown on the right page is that slot's spell.
    /// Glyphs are appended and removed with single clicks, "create" writes the bar into the slot and
    /// overwrites without asking, and switching or closing simply drops whatever was not created.
    ///
    /// The window owns no spell logic. It calls the shared services (<see cref="SpellService"/>,
    /// <see cref="GameComponent_Spellbook"/>) to validate, save and start targeting, so the page and
    /// the native cast gizmo always read the same slot.
    /// </summary>
    public partial class Window_Spellbook : Window
    {
        private readonly Pawn caster;
        private readonly SpellbookChain chain = new SpellbookChain();
        private readonly SpellbookAnim anim = new SpellbookAnim();
        private readonly string[] slotNames = new string[GameComponent_Spellbook.SlotCount];

        /// <summary>Icon of each slot's first rune for its ribbon, refreshed with <see cref="slotNames"/>.</summary>
        private readonly Texture2D[] slotIcons = new Texture2D[GameComponent_Spellbook.SlotCount];

        /// <summary>Remembers the player's animation choice for the next time the book opens.</summary>
        private static bool animationsEnabled = true;

        private int slot;
        private int activeSlot = -1;
        private SpellGlyphKind? category;
        private string search = "";
        private int hoverGlyph = -1;
        private Vector2 catalogScroll;
        private string statusText;
        private bool statusIsError;
        private bool refuseAsDuplicate;

        private bool chainValid;
        private string chainReason;
        private bool castable;
        private string castReason;

        /// <summary>What "create" would freeze for the bar as it stands (the same derivation the slot uses).</summary>
        private SpellTiming barTiming;

        public Window_Spellbook(Pawn caster)
        {
            this.caster = caster;
            anim.enabled = animationsEnabled;

            // The cover is the window: no vanilla title bar, background or close X, all of which
            // would be painted over. The header draws the title and its own close button.
            doWindowBackground = false;
            doCloseX = false;
            doCloseButton = false;
            draggable = true;
            forcePause = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            // Keep a second Add from replacing this window: WindowStack.Add would drop the open book.
            onlyOneOfTypeAllowed = false;
            closeOnAccept = false;
            closeOnCancel = true;
            drawShadow = true;
        }

        /// <summary>Read-only probe for the lab: true while the bar holds edits no slot has.</summary>
        public bool HasUnsavedDraft
        {
            get { return chain.IsModified; }
        }

        /// <summary>Read-only probe for the lab: the pawn this book was opened for.</summary>
        public Pawn Caster
        {
            get { return caster; }
        }

        protected override float Margin
        {
            get { return 0f; }
        }

        public override Vector2 InitialSize
        {
            get
            {
                float width = Mathf.Clamp(UI.screenWidth - 40f, 820f, 1080f);
                float height = Mathf.Clamp(UI.screenHeight - 60f, 560f, 700f);
                return new Vector2(width, height);
            }
        }

        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            float x = Mathf.Clamp((UI.screenWidth - size.x) * 0.5f, 0f, Mathf.Max(0f, UI.screenWidth - size.x));
            float y = Mathf.Clamp((UI.screenHeight - size.y) * 0.5f, 0f, Mathf.Max(0f, UI.screenHeight - size.y));
            windowRect = GenUI.Rounded(new Rect(x, y, size.x, size.y));
        }

        public override void PreOpen()
        {
            base.PreOpen();

            // Runs before this window is inserted, so any match is a book that is already open.
            int open = 0;
            WindowStack stack = Find.WindowStack;
            if (stack != null)
            {
                for (int i = 0; i < stack.Windows.Count; i++)
                {
                    if (stack.Windows[i] is Window_Spellbook)
                    {
                        open++;
                        break;
                    }
                }
            }

            refuseAsDuplicate = open > 0;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            if (refuseAsDuplicate)
            {
                return;
            }

            RefreshSlotNames();

            // The page opens on the pawn's active slot, so the bookmark strip and the cast gizmo
            // describe the same spell from the first frame. A pawn without a slot gets the first
            // used one (or slot 1 when the book is empty).
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            activeSlot = component == null ? -1 : component.GetActiveSlot(caster);
            if (activeSlot < 0 || activeSlot >= GameComponent_Spellbook.SlotCount)
            {
                activeSlot = FirstUsedSlot();
                component?.SetActiveSlot(caster, activeSlot);
            }

            slot = activeSlot;
            LoadChainFromSlot();
        }

        /// <summary>
        /// Nothing is pending any more: the bar is the slot, so there is no unsaved work to confirm.
        /// </summary>
        public override bool OnCloseRequest()
        {
            return true;
        }

        /// <summary>
        /// Makes <paramref name="index"/> the pawn's current slot and reloads the page for it.
        /// Single click, empty slots included; the cast gizmo reads the new slot on the next frame.
        /// The bookmark strip and any lab probe both go through here.
        /// </summary>
        public void SelectBookmark(int index)
        {
            if (index < 0 || index >= GameComponent_Spellbook.SlotCount)
            {
                return;
            }

            anim.OnSlotSwitch(slot, index);

            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            component?.SetActiveSlot(caster, index);

            activeSlot = index;
            slot = index;
            statusText = null;
            statusIsError = false;
            LoadChainFromSlot();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (refuseAsDuplicate)
            {
                // One frame of closed background; the already open book stays as it is.
                Close(true);
                return;
            }

            using (TechLabDrawScope.Begin())
            {
                SpellbookStyle.DrawCover(inRect);
                RefreshFrameState();

                SpellbookLayout layout = SpellbookLayout.Compute(inRect);

                DrawHeader(layout.Header);
                DrawCatalogPage(layout.LeftPage);
                DrawEditorPage(layout.RightPage);
                DrawBookmarks(layout.Bookmarks);
                DrawFooter(layout.Footer);
                DrawAnimations(layout);
                DrawBlockedTooltip(new Rect(0f, 0f, inRect.width, inRect.height));
            }
        }

        // ---------------------------------------------------------------- frame state

        private void RefreshFrameState()
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            activeSlot = component == null ? -1 : component.GetActiveSlot(caster);

            chainValid = SpellService.Validate(chain.ToRecipe(), out chainReason);
            barTiming = SpellTiming.Derive(chain.glyphs);

            castable = false;
            SpellRecipe saved = SavedRecipe(slot);
            if (saved == null)
            {
                castReason = TF("WotF_Spellbook.CastNoSaved", slot + 1);
                return;
            }

            // The shared service decides; the window only surfaces its reason.
            castable = SpellService.CanCast(caster, saved, out string canReason);
            castReason = castable ? null : canReason;
        }

        // ---------------------------------------------------------------- header/footer

        /// <summary>
        /// Cover band: caster and current slot on the left, the book's title in the middle, the
        /// animation toggle and the close button on the right.
        /// </summary>
        private void DrawHeader(Rect rect)
        {
            Rect closeRect = new Rect(rect.xMax - 22f, rect.y + (rect.height - 20f) * 0.5f, 20f, 20f);
            Rect animRect = new Rect(closeRect.x - 10f - 96f, rect.y + 2f, 96f, rect.height - 4f);

            float sideWidth = Mathf.Max(0f, animRect.x - rect.x - 8f);
            float titleWidth = Mathf.Min(rect.width * 0.34f, 300f);
            Rect titleRect = new Rect(rect.center.x - titleWidth * 0.5f, rect.y, titleWidth, rect.height);
            SpellbookStyle.DrawLabel(titleRect, T("WotF_Spellbook.WindowTitle"), GameFont.Medium,
                SpellbookStyle.CoverGold, TextAnchor.MiddleCenter);

            float leftWidth = Mathf.Min(sideWidth, titleRect.x - rect.x - 8f);
            string casterLabel = caster != null ? caster.LabelShort : T("WotF_Spellbook.NoCaster");
            string currentName = slotNames[slot];
            string slotText = TF("WotF_Spellbook.CurrentSlot", slot + 1) + " · " +
                              (currentName.NullOrEmpty() ? T("WotF_Spellbook.SlotEmpty") : currentName);
            SpellbookStyle.DrawLabel(new Rect(rect.x, rect.y, leftWidth, rect.height * 0.5f + 2f),
                TF("WotF_Spellbook.Caster", casterLabel), GameFont.Tiny, SpellbookStyle.CoverText,
                TextAnchor.LowerLeft);
            SpellbookStyle.DrawLabel(new Rect(rect.x, rect.y + rect.height * 0.5f, leftWidth, rect.height * 0.5f),
                slotText, GameFont.Tiny, SpellbookStyle.CoverTextDim, TextAnchor.UpperLeft);

            string animLabel = anim.enabled ? T("WotF_Spellbook.AnimOn") : T("WotF_Spellbook.AnimOff");
            if (SpellbookStyle.Button(animRect, animLabel, SpellbookButtonRole.Secondary, true))
            {
                anim.enabled = !anim.enabled;
                animationsEnabled = anim.enabled;
            }

            TooltipHandler.TipRegion(animRect, T("WotF_Spellbook.AnimTip"));

            bool overClose = Mouse.IsOver(closeRect);
            SpellbookStyle.DrawIcon(closeRect, SpellbookStyle.IconRemove,
                overClose ? SpellbookStyle.CoverWarning : SpellbookStyle.CoverTextDim);
            TooltipHandler.TipRegion(closeRect, T("CloseButton"));
            if (Widgets.ButtonInvisible(closeRect, true))
            {
                Close();
            }
        }

        private void DrawFooter(Rect rect)
        {
            string text = statusText ?? T("WotF_Spellbook.StatusReady");
            Color color = statusIsError ? SpellbookStyle.CoverWarning : SpellbookStyle.CoverTextDim;
            const float iconSize = 14f;
            Texture2D icon = statusIsError ? SpellbookStyle.IconWarn : SpellbookStyle.IconOk;
            SpellbookStyle.DrawIcon(new Rect(rect.x, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize), icon,
                color);
            SpellbookStyle.DrawLabel(new Rect(rect.x + iconSize + 6f, rect.y, rect.width - iconSize - 6f, rect.height),
                text, GameFont.Tiny, color, TextAnchor.MiddleLeft);
        }

        // ---------------------------------------------------------------- bookmarks

        /// <summary>
        /// Ten cloth ribbons tucked into the right page. The current slot's ribbon is pulled out to
        /// full length and glows; a used slot shows its first rune, an empty one is plain undyed
        /// cloth. The click rectangle is the whole tab, whatever the ribbon's drawn length.
        /// </summary>
        private void DrawBookmarks(Rect rect)
        {
            const float tuck = 6f;
            for (int i = 0; i < GameComponent_Spellbook.SlotCount; i++)
            {
                Rect tab = SpellbookLayout.BookmarkTab(rect, i, GameComponent_Spellbook.SlotCount);
                bool selected = i == activeSlot;
                bool used = !slotNames[i].NullOrEmpty();
                bool over = Mouse.IsOver(tab);

                Rect ribbon = selected ? tab : new Rect(tab.x + tuck, tab.y, tab.width - tuck, tab.height);
                Color cloth = selected ? SpellbookStyle.RibbonActive
                    : used ? SpellbookStyle.RibbonUsed
                    : SpellbookStyle.RibbonEmpty;
                if (over && !selected)
                {
                    cloth = SpellbookStyle.Lighten(cloth, 0.15f);
                }

                if (selected)
                {
                    SpellbookStyle.DrawGlow(ribbon.center, ribbon.width * 1.6f, SpellbookStyle.JadeBright, 0.35f);
                }

                SpellbookStyle.DrawStretched(ribbon, SpellbookStyle.Ribbon, cloth);

                // Content sits on the part of the ribbon outside the page, short of the swallowtail.
                Rect face = new Rect(tab.x + SpellbookLayout.BookmarkOverlap, tab.y,
                    tab.width - SpellbookLayout.BookmarkOverlap - 8f, tab.height);
                bool showIcon = used && slotIcons[i] != null && face.height >= 40f;
                Rect numberRect = showIcon ? new Rect(face.x, face.y + 2f, face.width, 16f) : face;
                SpellbookStyle.DrawLabel(numberRect, (i + 1).ToString(), GameFont.Small,
                    used || selected ? SpellbookStyle.CoverText : SpellbookStyle.Fade(SpellbookStyle.CoverText, 0.6f),
                    TextAnchor.MiddleCenter);
                if (showIcon)
                {
                    float iconSize = Mathf.Min(22f, face.height - 20f);
                    SpellbookStyle.DrawIcon(new Rect(face.center.x - iconSize * 0.5f, numberRect.yMax, iconSize, iconSize),
                        slotIcons[i], Color.white);
                }

                string tip = TF("WotF_Spellbook.SlotTitle", i + 1) + " · " +
                             (used ? TF("WotF_Spellbook.SlotTooltipUsed", slotNames[i]) : T("WotF_Spellbook.SlotEmpty"))
                             + "\n" + T("WotF_Spellbook.SwitchBookmark");
                TooltipHandler.TipRegion(tab, tip);

                if (Widgets.ButtonInvisible(tab, true))
                {
                    SelectBookmark(i);
                }
            }
        }

        // ---------------------------------------------------------------- slot actions

        private void LoadChainFromSlot()
        {
            chain.LoadFrom(SavedRecipe(slot));
            hoverGlyph = -1;
        }

        private void RequestCreate()
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null)
            {
                anim.Shake();
                SetStatus(T("WotF_Spellbook.NoGameComponent"), true);
                return;
            }

            if (!chain.HasGlyphs)
            {
                anim.Shake();
                SetStatus(T("WotF_Spellbook.BarEmpty"), true);
                return;
            }

            if (!chainValid)
            {
                anim.Shake();
                SetStatus(TF("WotF_Spellbook.CreateRefused", ReasonOrUnknown(chainReason)), true);
                return;
            }

            // No overwrite prompt: the bar is written straight into the current slot.
            string reason;
            if (!component.SaveSlot(slot, chain.ToRecipe(), out reason))
            {
                anim.Shake();
                SetStatus(TF("WotF_Spellbook.CreateRefused", ReasonOrUnknown(reason)), true);
                return;
            }

            component.SetActiveSlot(caster, slot);
            activeSlot = slot;
            RefreshSlotNames();
            LoadChainFromSlot();
            anim.Sweep();
            SetStatus(TF("WotF_Spellbook.Created", slot + 1), false);
        }

        /// <summary>Empties the bar only; the saved spell in the slot is untouched.</summary>
        private void RequestClearBar()
        {
            if (!chain.HasGlyphs)
            {
                anim.Shake();
                SetStatus(T("WotF_Spellbook.BarEmpty"), true);
                return;
            }

            chain.ClearBar();
            hoverGlyph = -1;
            SetStatus(T("WotF_Spellbook.BarCleared"), false);
        }

        /// <summary>One click deletes the current slot; there is no second confirmation.</summary>
        private void RequestDeleteSlot()
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null)
            {
                anim.Shake();
                SetStatus(T("WotF_Spellbook.NoGameComponent"), true);
                return;
            }

            if (slotNames[slot].NullOrEmpty())
            {
                anim.Shake();
                SetStatus(TF("WotF_Spellbook.SlotAlreadyEmpty", slot + 1), false);
                return;
            }

            component.ClearSlot(slot);
            RefreshSlotNames();
            activeSlot = component.GetActiveSlot(caster);
            LoadChainFromSlot();
            SetStatus(TF("WotF_Spellbook.SlotDeleted", slot + 1), false);
        }

        private void RequestCast()
        {
            SpellRecipe saved = SavedRecipe(slot);
            if (saved == null)
            {
                anim.Shake();
                SetStatus(TF("WotF_Spellbook.CastNoSaved", slot + 1), true);
                return;
            }

            if (!castable)
            {
                anim.Shake();
                SetStatus(TF("WotF_Spellbook.CastBlocked", ReasonOrUnknown(castReason)), true);
                return;
            }

            // Same entry point as the native gizmo, so distance, line of sight and cooldown are
            // enforced by the shared service rather than by this window.
            Close(true);
            SpellService.BeginTargeting(caster, saved);
        }

        // ---------------------------------------------------------------- small helpers

        private SpellRecipe SavedRecipe(int index)
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null || index < 0 || index >= GameComponent_Spellbook.SlotCount)
            {
                return null;
            }

            return component.GetSlot(index);
        }

        private void RefreshSlotNames()
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            for (int i = 0; i < slotNames.Length; i++)
            {
                SpellRecipe recipe = component?.GetSlot(i);
                slotNames[i] = recipe?.name;
                slotIcons[i] = recipe != null && recipe.glyphs != null && recipe.glyphs.Count > 0
                    ? SpellbookStyle.GlyphIcon(SpellDefs.Glyph(recipe.glyphs[0]))
                    : null;
            }
        }

        private int FirstUsedSlot()
        {
            for (int i = 0; i < slotNames.Length; i++)
            {
                if (!slotNames[i].NullOrEmpty())
                {
                    return i;
                }
            }

            return 0;
        }

        private void SetStatus(string text, bool isError)
        {
            statusText = text;
            statusIsError = isError;
        }

        private string ReasonOrUnknown(string reason)
        {
            return reason.NullOrEmpty() ? T("WotF_Spellbook.ValidationUnknown") : reason;
        }

        private static string T(string key)
        {
            return SpellbookStyle.T(key);
        }

        private static string TF(string key, params object[] args)
        {
            return SpellbookStyle.TF(key, args);
        }
    }
}
