using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Two-page book ritual host window replacing the legacy single-column host dialog.
    /// Left page shows researched rituals grouped by T1/T2/T3 (with tier-locked cards greyed out)
    /// and the altar tier + facility resonance footer. Right page shows the centered chalk formation
    /// diagram with tier/resonance/family callouts, single-sentence description + flavour placeholder,
    /// dynamic parameters, cost <-> preparation progress transition, host/helper portraits, live
    /// ritual metrics computed via the effect workers' shared formulas, and fixed action footer.
    /// </summary>
    public sealed partial class Window_RitualHost : Window
    {
        private const float CostAnimDuration = 0.40f;
        private const float AutoPulseDuration = 0.24f;

        private readonly CompRitualAltar altar;
        private readonly HashSet<Pawn> helpers = new HashSet<Pawn>();
        private readonly List<RitualDef> researchedRituals = new List<RitualDef>();
        private readonly HashSet<RitualDef> missingMapMaterials = new HashSet<RitualDef>();
        private readonly Dictionary<int, ThingDef> altarDefsByTier = new Dictionary<int, ThingDef>();

        private RitualDef selected;
        private SpellGlyphDef selectedRune;
        private Building selectedNode;
        private RuinSeekOption selectedRuin;
        private Thing selectedEquipment;
        private int selectedBookSlot = -1;
        private Pawn selectedHost;

        private RitualAssessment assessment;
        private bool dirty = true;
        private int assessedTick = -9999;

        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private float leftViewHeight = 400f;
        private float rightViewHeight = 320f;

        // Cost <-> preparation progress transition state
        private RitualDef lastCostRitual;
        private bool? lastCostShowProgress;
        private float costAnimStart = -1000f;

        // Auto-repeat checkbox click pulse timestamp
        private float autoPulseStart = -1000f;

        private PopoverWindow activePopover;

        public Window_RitualHost(CompRitualAltar altar)
        {
            this.altar = altar;
            BuildAltarTierIndex();
            InitializeSelection();

            doWindowBackground = false;
            doCloseX = false;
            doCloseButton = false;
            draggable = true;
            forcePause = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            onlyOneOfTypeAllowed = true;
            closeOnAccept = false;
            closeOnCancel = true;
            drawShadow = true;
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
            Vector2 size = InitialSize;
            float x = Mathf.Clamp((UI.screenWidth - size.x) * 0.5f, 0f, Mathf.Max(0f, UI.screenWidth - size.x));
            float y = Mathf.Clamp((UI.screenHeight - size.y) * 0.5f, 0f, Mathf.Max(0f, UI.screenHeight - size.y));
            windowRect = GenUI.Rounded(new Rect(x, y, size.x, size.y));
        }

        public override void PostClose()
        {
            base.PostClose();
            ClosePopover();
        }

        private void BuildAltarTierIndex()
        {
            List<ThingDef> allThings = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < allThings.Count; i++)
            {
                ThingDef def = allThings[i];
                CompProperties_RitualAltar props = def?.GetCompProperties<CompProperties_RitualAltar>();
                if (props != null && !altarDefsByTier.ContainsKey(props.tier))
                {
                    altarDefsByTier[props.tier] = def;
                }
            }
        }

        private ThingDef AltarDefForTier(int tier)
        {
            return altarDefsByTier.TryGetValue(tier, out ThingDef def) ? def : null;
        }

        private void InitializeSelection()
        {
            RefreshResearchedRituals();
            if (altar.IsActive && altar.ActiveDef != null && altar.ActiveDef.IsResearchFinished)
            {
                selected = altar.ActiveDef;
            }
            else if (altar.CollectDef != null && altar.CollectDef.IsResearchFinished)
            {
                selected = altar.CollectDef;
                selectedRune = altar.CollectRune;
            }
            else if (altar.SelectedDef != null && altar.SelectedDef.IsResearchFinished)
            {
                selected = altar.SelectedDef;
                selectedRune = altar.SelectedRune;
                selectedNode = altar.SelectedTeleportTarget;
                selectedRuin = altar.SelectedRuinOption;
                selectedEquipment = altar.SelectedEquipment;
                selectedBookSlot = altar.SelectedBookSlot;
            }
            else
            {
                selected = FindDefaultRitual();
            }
        }

        private RitualDef FindDefaultRitual()
        {
            for (int i = 0; i < researchedRituals.Count; i++)
            {
                if (researchedRituals[i].tier <= altar.Props.tier)
                {
                    return researchedRituals[i];
                }
            }

            return researchedRituals.Count > 0 ? researchedRituals[0] : null;
        }

        private void SelectRitual(RitualDef def)
        {
            if (selected == def)
            {
                return;
            }

            selected = def;
            selectedRune = def != null && def == altar.CollectDef ? altar.CollectRune : null;
            selectedNode = null;
            selectedRuin = null;
            selectedEquipment = null;
            selectedBookSlot = -1;
            lastCostRitual = null;
            lastCostShowProgress = null;
            costAnimStart = -1000f;
            rightScroll = Vector2.zero;
            SyncSelection();
            dirty = true;
        }

        private void SyncSelection()
        {
            if (selected == null || altar.SupportsRitual(selected))
            {
                altar.SetSelectedRitual(selected, selectedRune, selectedNode, selectedEquipment,
                    selectedBookSlot, selectedRuin?.quest);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (altar == null || altar.parent == null || !altar.parent.Spawned)
            {
                Close();
                return;
            }

            using (TechLabDrawScope.Begin())
            {
                RefreshFrameState();

                if (!altar.IsActive && selected != null && assessment != null && assessment.MissingCells.Count > 0)
                {
                    GenDraw.DrawFieldEdges(assessment.MissingCells);
                }

                RitualPanelStyle.DrawBookSpread(inRect, 26f, out Rect headerRect, out Rect leftPageRect,
                    out Rect rightPageRect);

                DrawHeader(headerRect);
                DrawLeftPage(leftPageRect);
                DrawRightPage(rightPageRect);
            }
        }

        private void DrawHeader(Rect rect)
        {
            RitualPanelStyle.DrawLabel(
                new Rect(rect.x + 4f, rect.y, rect.width - 34f, rect.height),
                "WotF_RitualPanel_Title".Translate(),
                GameFont.Medium,
                RitualPanelStyle.TitleCream,
                TextAnchor.MiddleLeft);

            Rect closeRect = new Rect(rect.xMax - 24f, rect.y + (rect.height - 24f) * 0.5f, 24f, 24f);
            bool overClose = Mouse.IsOver(closeRect);
            if (overClose)
            {
                RitualPanelStyle.DrawBox(closeRect, new Color(1f, 1f, 1f, 0.08f), RitualPanelStyle.CoverBorder);
            }

            RitualPanelStyle.DrawIcon(
                closeRect.ContractedBy(4f),
                TexButton.CloseXSmall,
                overClose ? Color.white : RitualPanelStyle.TextSubtle);
            TooltipHandler.TipRegion(closeRect, "CloseButton".Translate());
            if (Widgets.ButtonInvisible(closeRect, doMouseoverSound: true))
            {
                Close();
            }
        }

        private void RefreshFrameState()
        {
            PruneSelectedRoster();
            int now = Find.TickManager.TicksGame;
            if (!dirty && assessment != null && now - assessedTick <= 15)
            {
                return;
            }

            RefreshResearchedRituals();
            if (selected != null && !researchedRituals.Contains(selected))
            {
                selected = FindDefaultRitual();
            }

            RefreshMissingMapMaterials();
            assessment = altar.Assess(BuildRequest());
            assessedTick = now;
            dirty = false;
        }

        private void RefreshResearchedRituals()
        {
            researchedRituals.Clear();
            List<RitualDef> all = DefDatabase<RitualDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                RitualDef def = all[i];
                if (def != null && def.IsResearchFinished)
                {
                    researchedRituals.Add(def);
                }
            }

            researchedRituals.Sort(CompareRituals);
        }

        private static int CompareRituals(RitualDef a, RitualDef b)
        {
            int byTier = a.tier.CompareTo(b.tier);
            if (byTier != 0)
            {
                return byTier;
            }

            int byFamily = ((int)a.family).CompareTo((int)b.family);
            if (byFamily != 0)
            {
                return byFamily;
            }

            return a.index.CompareTo(b.index);
        }

        private void RefreshMissingMapMaterials()
        {
            missingMapMaterials.Clear();
            Map map = altar.parent.Map;
            if (map == null)
            {
                return;
            }

            ThingOwner held = altar.GetDirectlyHeldThings();
            var availableCache = new Dictionary<ThingDef, int>();

            for (int i = 0; i < researchedRituals.Count; i++)
            {
                RitualDef def = researchedRituals[i];
                if (def.tier > altar.Props.tier || def.inputCosts == null || def.inputCosts.Count == 0)
                {
                    continue;
                }

                for (int c = 0; c < def.inputCosts.Count; c++)
                {
                    ThingDefCountClass cost = def.inputCosts[c];
                    if (cost?.thingDef == null)
                    {
                        continue;
                    }

                    if (!availableCache.TryGetValue(cost.thingDef, out int available))
                    {
                        available = CountAvailableOnMapAndAltar(map, held, cost.thingDef);
                        availableCache[cost.thingDef] = available;
                    }

                    if (available < cost.count)
                    {
                        missingMapMaterials.Add(def);
                        break;
                    }
                }
            }
        }

        private static int CountAvailableOnMapAndAltar(Map map, ThingOwner held, ThingDef def)
        {
            int total = 0;
            if (held != null)
            {
                for (int i = 0; i < held.Count; i++)
                {
                    if (held[i].def == def)
                    {
                        total += held[i].stackCount;
                    }
                }
            }

            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer))
                {
                    total += t.stackCount;
                }
            }

            return total;
        }

        private void PruneSelectedRoster()
        {
            Map map = altar.parent.Map;
            if (selectedHost != null && !IsPawnOnAltarMap(selectedHost, map))
            {
                selectedHost = null;
                selectedBookSlot = -1;
                dirty = true;
            }

            if (helpers.Count > 0)
            {
                helpers.RemoveWhere(p => p == null || p == selectedHost || !IsPawnOnAltarMap(p, map));
            }
        }

        private static bool IsPawnOnAltarMap(Pawn pawn, Map map)
        {
            return !pawn.Destroyed && pawn.Spawned && !pawn.Dead && pawn.Map == map
                && Gene_ForestBloodline.Get(pawn) != null;
        }

        private RitualRequest BuildRequest()
        {
            var request = new RitualRequest
            {
                Def = selected,
                Rune = selectedRune,
                TeleportTarget = selectedNode,
                RuinOption = selected != null && selected.chooseRuinType ? selectedRuin : null,
                Equipment = selected != null && selected.chooseEquipment ? selectedEquipment : null,
                BookSlot = selected != null && selected.chooseEquipment ? selectedBookSlot : -1,
                Host = selectedHost
            };

            var list = new List<Pawn>();
            foreach (Pawn helper in helpers)
            {
                if (helper != null && helper != selectedHost)
                {
                    list.Add(helper);
                }
            }

            list.Sort((a, b) => string.Compare(a.LabelShort, b.LabelShort, StringComparison.Ordinal));
            request.Helpers.AddRange(list);
            return request;
        }

        private List<Pawn> BloodlinePawns()
        {
            var pawns = new List<Pawn>();
            Map map = altar.parent.Map;
            if (map == null)
            {
                return pawns;
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (Gene_ForestBloodline.Get(pawn) != null)
                {
                    pawns.Add(pawn);
                }
            }

            pawns.Sort((a, b) => string.Compare(a.LabelShort, b.LabelShort, StringComparison.Ordinal));
            return pawns;
        }
    }
}
