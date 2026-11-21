using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Symmetric two-page spellbook built on <see cref="BookUi.DrawBookSpread"/>: rune codex and
    /// fixed bottom inspector on the left page; 2×5 ten-slot grid, single-row 10-socket rune chain,
    /// live compiled tactical plan ledger and action controls on the right page.
    ///
    /// All ten bookmarks stay open while wearing any spellbook, while book tier caps single-spell
    /// rune chain length (4 / 7 / 10). The window owns no spell rules: it delegates validation,
    /// compilation, saving, casting and carrier checks to the shared services.
    /// </summary>
    public partial class Window_Spellbook : Window
    {
        protected delegate bool SubmitRecipe(SpellRecipe recipe, out string reason);

        private const float ToastDurationSeconds = 3.5f;

        private readonly SubmitRecipe submitRecipe;
        private readonly Func<SpellRecipe, float> previewMana;
        private readonly string composeTitle;
        private readonly string submitLabel;
        protected bool IsStandalone => submitRecipe != null;

        private readonly Pawn caster;
        private readonly SpellbookChain chain = new SpellbookChain();
        private readonly SpellbookAnim anim = new SpellbookAnim();
        private readonly SpellRecipe[] slotRecipes = new SpellRecipe[GameComponent_Spellbook.SlotCount];
        private readonly Texture2D[] slotIcons = new Texture2D[GameComponent_Spellbook.SlotCount];
        private readonly List<(ThingDef Def, int MaxGlyphs)> bookTiers = new List<(ThingDef, int)>();

        private int slot;
        private int activeSlot = -1;
        private SpellGlyphKind? category;
        private string search = "";
        private int hoverGlyph = -1;
        private SpellGlyphDef lastInspectedGlyph;
        private Vector2 catalogScroll;
        private Vector2 ledgerScroll;
        private float ledgerViewHeight = 140f;
        private string statusText;
        private bool statusIsError;
        private float statusSetAt = -1000f;
        private bool refuseAsDuplicate;
        private int lastSlotRefreshTick = -9999;

        // Per-frame book/draft/cast snapshot
        private Apparel wornBook;
        private int bookMaxGlyphs;
        private bool bookAllowsSuper;
        private CompSpellStaff heldStaff;
        private SpellStaffModifiers heldStaffMods;

        private bool draftOk;
        private string draftReason;
        private bool castable;
        private string castReason;
        private SpellTiming barTiming;
        private SpellCost barCost;

        // Cached live compiled tactical plan (rebuilt only when draft chain or held staff changes)
        private string cachedPlanKey;
        private SpellCastPlan previewPlan;
        private readonly List<string> previewSummaryLines = new List<string>();
        private bool previewStaffOk;
        private bool previewTurretOk;
        private string previewTurretReason;
        private bool previewEnchantOk;
        private string previewEnchantReason;

        public Window_Spellbook(Pawn caster) : this()
        {
            this.caster = caster;
        }

        protected Window_Spellbook(SubmitRecipe submit, string title, string buttonLabel,
            Func<SpellRecipe, float> manaPreview) : this()
        {
            submitRecipe = submit;
            composeTitle = title;
            submitLabel = buttonLabel;
            previewMana = manaPreview;
        }

        private Window_Spellbook()
        {
            BuildBookTierIndex();
            // Keep onlyOneOfTypeAllowed=false so PreOpen drops duplicate opens without replacing the live book.
            BookUi.ConfigureBookWindow(this, onlyOneOfType: false);
        }

        protected override float Margin => 0f;

        public override Vector2 InitialSize
        {
            get
            {
                float width = Mathf.Clamp(UI.screenWidth - 40f, 820f, 1088f);
                float height = Mathf.Clamp(UI.screenHeight - 60f, 560f, 728f);
                return new Vector2(width, height);
            }
        }

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = BookUi.CenteredWindowRect(InitialSize);
        }

        /// <summary>Optional cover-header badge text for standalone subclasses (e.g. turret inscription).</summary>
        protected virtual string HeaderBadgeText => null;

        /// <summary>
        /// Single catalogue gate for whether <paramref name="glyph"/> can be appended right now.
        /// Subclasses (such as <see cref="Window_TurretSpell"/>) override this to grey out
        /// unsupported forms or super runes directly in the catalogue.
        /// </summary>
        protected virtual bool ValidateCatalogGlyph(SpellGlyphDef glyph, out string reason)
        {
            int? limit = IsStandalone ? (int?)null : bookMaxGlyphs;
            if (!SpellCompiler.CanAppend(chain.glyphs, glyph, out reason, limit))
            {
                return false;
            }

            if (!IsStandalone && !SpellService.CanUseGlyph(caster, glyph, out reason))
            {
                return false;
            }

            return true;
        }

        /// <summary>Full-chain validation for the active draft; subclasses may add carrier rules.</summary>
        protected virtual bool ValidateChain(SpellRecipe recipe, out string reason)
        {
            if (!SpellService.Validate(recipe, out reason))
            {
                return false;
            }

            if (IsStandalone)
            {
                return true;
            }

            if (!SpellService.CanFitBook(caster, recipe.glyphs.Count, out reason))
            {
                return false;
            }

            for (int i = 0; i < recipe.glyphs.Count; i++)
            {
                SpellGlyphDef glyph = SpellDefs.Glyph(recipe.glyphs[i]);
                if (!SpellService.CanUseGlyph(caster, glyph, out reason))
                {
                    return false;
                }
            }

            return true;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            int open = 0;
            WindowStack stack = Find.WindowStack;
            if (stack != null)
            {
                for (int i = 0; i < stack.Windows.Count; i++)
                {
                    if (stack.Windows[i].GetType() == GetType())
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
            if (refuseAsDuplicate || IsStandalone)
            {
                return;
            }

            RefreshSlots();

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

        public override bool OnCloseRequest() => true;

        /// <summary>Switches to bookmark <paramref name="index"/> (all 10 slots stay selectable).</summary>
        public void SelectBookmark(int index)
        {
            if (IsStandalone || index < 0 || index >= GameComponent_Spellbook.SlotCount)
            {
                return;
            }

            anim.OnSlotSwitch(slot, index);

            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            component?.SetActiveSlot(caster, index);

            activeSlot = index;
            slot = index;
            ClearStatus();
            ledgerScroll = Vector2.zero;
            LoadChainFromSlot();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (refuseAsDuplicate)
            {
                Close(true);
                return;
            }

            using (TechLabDrawScope.Begin())
            {
                RefreshFrameState();

                BookUi.DrawBookSpread(inRect, SpellbookLayout.HeaderHeight,
                    out Rect headerRect, out Rect leftPageRect, out Rect rightPageRect);

                DrawHeader(headerRect);
                DrawCatalogPage(leftPageRect, out CatalogLayout catalogLayout);
                DrawEditorPage(rightPageRect, out EditorLayout editorLayout);
                // Draw inspector after editor so hovering right-page chain beads updates on the same frame.
                DrawInspectorCard(catalogLayout.Inspector);
                DrawAnimations(rightPageRect, editorLayout);
                DrawBlockedTooltip(new Rect(0f, 0f, inRect.width, inRect.height));
            }
        }

        // ---------------------------------------------------------------- frame state & plan cache

        private void RefreshFrameState()
        {
            if (!IsStandalone)
            {
                wornBook = SpellService.WornSpellbook(caster);
                SpellbookExtension ext = wornBook?.def.GetModExtension<SpellbookExtension>();
                bookMaxGlyphs = ext?.maxGlyphs ?? 0;
                bookAllowsSuper = ext?.allowsSuperGlyphs == true;
                heldStaff = CompSpellStaff.FindEquipped(caster);
                heldStaffMods = heldStaff?.Modifiers;

                int nowTick = Find.TickManager?.TicksGame ?? 0;
                if (nowTick - lastSlotRefreshTick >= 30 || nowTick < lastSlotRefreshTick)
                {
                    RefreshSlots();
                }
            }
            else
            {
                wornBook = null;
                bookMaxGlyphs = SpellbookStyle.MaxGlyphs;
                bookAllowsSuper = false;
                heldStaff = null;
                heldStaffMods = null;
            }

            SpellRecipe draft = chain.ToRecipe();
            draftOk = ValidateChain(draft, out draftReason);
            barTiming = SpellTiming.Derive(chain.glyphs);
            barCost = SpellCost.Derive(chain.glyphs);

            RefreshPreviewPlan(draft);

            if (IsStandalone)
            {
                return;
            }

            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            activeSlot = component == null ? -1 : component.GetActiveSlot(caster);
            castable = false;
            SpellRecipe saved = SavedRecipe(slot);
            if (saved == null)
            {
                castReason = TF("WotF_Spellbook.CastNoSaved", slot + 1);
                return;
            }

            castable = SpellService.CanCast(caster, saved, out string canReason);
            castReason = castable ? null : canReason;
        }

        private void RefreshPreviewPlan(SpellRecipe draft)
        {
            string staffKey = heldStaff != null
                ? heldStaff.parent.def.defName + ":" + (heldStaff.parent.Stuff?.defName ?? "")
                : "";
            string key = string.Join("|", draft.glyphs) + "#" + staffKey;
            if (key == cachedPlanKey)
            {
                return;
            }

            cachedPlanKey = key;
            previewSummaryLines.Clear();
            previewPlan = SpellCompiler.Compile(draft, out _, heldStaffMods);
            if (previewPlan != null)
            {
                previewPlan.AppendSummary(previewSummaryLines);
                previewStaffOk = Verb_CastSpellStaff.AutoAttackPlan(draft) != null;
                previewTurretOk = CompSpellTurret.CanCompileProjectile(draft, out previewTurretReason);
                previewEnchantOk = CompEnchantedEquipment.AcceptsRecipe(draft, out previewEnchantReason);
            }
            else
            {
                previewStaffOk = false;
                previewTurretOk = false;
                previewTurretReason = null;
                previewEnchantOk = false;
                previewEnchantReason = null;
            }
        }

        // ---------------------------------------------------------------- cover header

        private void DrawHeader(Rect rect)
        {
            Rect closeRect = new Rect(rect.xMax - 24f, rect.y + (rect.height - 24f) * 0.5f, 24f, 24f);
            float availWidth = closeRect.x - rect.x - 10f;

            string title = IsStandalone ? composeTitle : T("WotF_Spellbook.WindowTitle");
            float titleW = Mathf.Min(availWidth * 0.45f, Mathf.Ceil(BookUi.CalcTextWidth(title, GameFont.Medium)) + 6f);
            Rect titleRect = new Rect(rect.x + 2f, rect.y, titleW, rect.height);
            BookUi.DrawLabel(titleRect, title, GameFont.Medium, BookUi.CoverGold, TextAnchor.MiddleLeft, ellipsis: true);

            float badgeStartX = titleRect.xMax + 10f;
            float badgeAvailW = closeRect.x - badgeStartX - 8f;
            if (badgeAvailW > 60f)
            {
                DrawHeaderBadge(new Rect(badgeStartX, rect.y + 3f, badgeAvailW, rect.height - 6f));
            }

            BookUi.DrawCloseButton(closeRect, () => Close());
        }

        private void DrawHeaderBadge(Rect area)
        {
            string badgeText;
            string tip = null;
            Color bg = new Color(0.11f, 0.21f, 0.16f, 0.88f);
            Color border = BookUi.CoverBorder;
            Color textColor = BookUi.CoverText;

            if (IsStandalone)
            {
                badgeText = HeaderBadgeText ?? T("WotF_Turret_ComposeHint");
            }
            else
            {
                string casterLabel = caster != null ? caster.LabelShort : T("WotF_Spellbook.NoCaster");
                Gene_ForestBloodline bloodline = Gene_ForestBloodline.Get(caster);
                string manaText = bloodline != null
                    ? bloodline.CurrentMana.ToString("0") + "/" + bloodline.MaxMana.ToString("0")
                    : "-";

                if (wornBook != null)
                {
                    badgeText = TF("WotF_Spellbook.HeaderBadge",
                        casterLabel, wornBook.def.LabelCap, bookMaxGlyphs, manaText);
                    tip = TF("WotF_Spellbook.HeaderBadgeTip",
                        wornBook.def.LabelCap,
                        bookMaxGlyphs,
                        bookAllowsSuper ? "Yes".Translate() : "No".Translate());
                }
                else
                {
                    badgeText = TF("WotF_Spellbook.HeaderNoBook", casterLabel, manaText);
                    bg = new Color(0.32f, 0.12f, 0.10f, 0.90f);
                    border = BookUi.CoverWarning;
                    textColor = BookUi.CoverWarning;
                }
            }

            float desiredW = Mathf.Ceil(BookUi.CalcTextWidth(badgeText, GameFont.Tiny)) + 16f;
            Rect pill = new Rect(area.x, area.y, Mathf.Min(area.width, desiredW), area.height);
            BookUi.DrawPillBadge(pill, badgeText, bg, border, textColor, GameFont.Tiny, tip);
        }

        // ---------------------------------------------------------------- slot actions

        private void LoadChainFromSlot()
        {
            chain.LoadFrom(SavedRecipe(slot));
            hoverGlyph = -1;
            cachedPlanKey = null;
        }

        private void RequestCreate()
        {
            if (IsStandalone)
            {
                if (!submitRecipe(chain.ToRecipe(), out string refusal))
                {
                    anim.Nudge();
                    SetStatus(refusal, true);
                    return;
                }

                Close();
                return;
            }

            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null)
            {
                anim.Nudge();
                SetStatus(T("WotF_Spellbook.NoGameComponent"), true);
                return;
            }

            if (!chain.HasGlyphs)
            {
                anim.Nudge();
                SetStatus(T("WotF_Spellbook.BarEmpty"), true);
                return;
            }

            if (!draftOk)
            {
                anim.Nudge();
                SetStatus(TF("WotF_Spellbook.CreateRefused", ReasonOrUnknown(draftReason)), true);
                return;
            }

            if (!component.SaveSlot(caster, slot, chain.ToRecipe(), out string reason))
            {
                anim.Nudge();
                SetStatus(TF("WotF_Spellbook.CreateRefused", ReasonOrUnknown(reason)), true);
                return;
            }

            component.SetActiveSlot(caster, slot);
            activeSlot = slot;
            RefreshSlots();
            LoadChainFromSlot();
            anim.OnCreate(chain.glyphs.Count);
            SetStatus(TF("WotF_Spellbook.Created", slot + 1), false);
        }

        private void RequestClearBar()
        {
            if (!chain.HasGlyphs)
            {
                anim.Nudge();
                SetStatus(T("WotF_Spellbook.BarEmpty"), true);
                return;
            }

            chain.ClearBar();
            hoverGlyph = -1;
            cachedPlanKey = null;
            SetStatus(T("WotF_Spellbook.BarCleared"), false);
        }

        private void RequestDeleteSlot()
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null)
            {
                anim.Nudge();
                SetStatus(T("WotF_Spellbook.NoGameComponent"), true);
                return;
            }

            if (slotRecipes[slot] == null)
            {
                anim.Nudge();
                SetStatus(TF("WotF_Spellbook.SlotAlreadyEmpty", slot + 1), false);
                return;
            }

            component.ClearSlot(caster, slot);
            RefreshSlots();
            activeSlot = component.GetActiveSlot(caster);
            LoadChainFromSlot();
            SetStatus(TF("WotF_Spellbook.SlotDeleted", slot + 1), false);
        }

        private void RequestCast()
        {
            SpellRecipe saved = SavedRecipe(slot);
            if (saved == null)
            {
                anim.Nudge();
                SetStatus(TF("WotF_Spellbook.CastNoSaved", slot + 1), true);
                return;
            }

            if (!castable)
            {
                anim.Nudge();
                SetStatus(TF("WotF_Spellbook.CastBlocked", ReasonOrUnknown(castReason)), true);
                return;
            }

            Close(true);
            SpellService.BeginTargeting(caster, saved);
        }

        // ---------------------------------------------------------------- helpers

        private SpellRecipe SavedRecipe(int index)
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null || index < 0 || index >= GameComponent_Spellbook.SlotCount)
            {
                return null;
            }

            return component.GetSlot(caster, index);
        }

        private void RefreshSlots()
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            lastSlotRefreshTick = Find.TickManager?.TicksGame ?? 0;
            for (int i = 0; i < slotRecipes.Length; i++)
            {
                SpellRecipe recipe = component?.GetSlot(caster, i);
                slotRecipes[i] = recipe;
                slotIcons[i] = recipe != null && recipe.glyphs != null && recipe.glyphs.Count > 0
                    ? SpellbookStyle.GlyphIcon(SpellDefs.Glyph(recipe.glyphs[0]))
                    : null;
            }
        }

        private bool SlotExceedsBook(SpellRecipe recipe, out string reason)
        {
            reason = null;
            if (recipe == null || IsStandalone)
            {
                return false;
            }

            if (!SpellService.CanFitBook(caster, recipe.glyphs.Count, out reason))
            {
                return true;
            }

            for (int i = 0; i < recipe.glyphs.Count; i++)
            {
                SpellGlyphDef glyph = SpellDefs.Glyph(recipe.glyphs[i]);
                if (glyph != null && !SpellService.CanUseGlyph(caster, glyph, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildBookTierIndex()
        {
            bookTiers.Clear();
            List<ThingDef> all = DefDatabase<ThingDef>.AllDefsListForReading;
            if (all == null)
            {
                return;
            }

            for (int i = 0; i < all.Count; i++)
            {
                SpellbookExtension ext = all[i]?.GetModExtension<SpellbookExtension>();
                if (ext != null && ext.maxGlyphs > 0)
                {
                    bookTiers.Add((all[i], ext.maxGlyphs));
                }
            }

            bookTiers.Sort((a, b) => a.MaxGlyphs.CompareTo(b.MaxGlyphs));
        }

        private string SealedSocketTooltip(int socketIndex)
        {
            int needed = socketIndex + 1;
            for (int i = 0; i < bookTiers.Count; i++)
            {
                if (bookTiers[i].MaxGlyphs >= needed)
                {
                    return TF("WotF_Spellbook.SocketSealedTip", needed, bookTiers[i].Def.LabelCap, bookTiers[i].MaxGlyphs);
                }
            }

            return TF("WotF_Spellbook.SocketSealedFallback", needed);
        }

        private int FirstUsedSlot()
        {
            for (int i = 0; i < slotRecipes.Length; i++)
            {
                if (slotRecipes[i] != null)
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
            statusSetAt = Time.realtimeSinceStartup;
        }

        private void ClearStatus()
        {
            statusText = null;
            statusIsError = false;
            statusSetAt = -1000f;
        }

        private bool HasActiveToast => !statusText.NullOrEmpty()
            && Time.realtimeSinceStartup - statusSetAt <= ToastDurationSeconds;

        private string ReasonOrUnknown(string reason)
        {
            return reason.NullOrEmpty() ? T("WotF_Spellbook.ValidationUnknown") : reason;
        }

        private static string T(string key) => SpellbookStyle.T(key);
        private static string TF(string key, params object[] args) => SpellbookStyle.TF(key, args);
    }
}
