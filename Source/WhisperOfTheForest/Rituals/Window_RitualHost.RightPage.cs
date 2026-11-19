using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest.Rituals
{
    public sealed partial class Window_RitualHost
    {
        private void DrawRightPage(Rect pageRect)
        {
            if (selected == null)
            {
                RitualPanelStyle.DrawLabel(pageRect, "WotF_RitualPanel_NoResearched".Translate(),
                    GameFont.Small, RitualPanelStyle.TextMuted, TextAnchor.MiddleCenter, ellipsis: false);
                return;
            }

            Rect inner = pageRect.ContractedBy(14f, 12f);

            const float heroHeight = 136f;
            const float footerHeight = 72f;
            const float metricsHeight = 68f;

            Rect heroRect = new Rect(inner.x, inner.y, inner.width, heroHeight);
            Rect footerRect = new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight);
            Rect metricsRect = new Rect(inner.x, footerRect.y - metricsHeight - 8f, inner.width, metricsHeight);
            Rect middleRect = new Rect(inner.x, heroRect.yMax + 6f, inner.width,
                metricsRect.y - (heroRect.yMax + 6f) - 8f);

            DrawFormationHero(heroRect);
            DrawRightScrollBody(middleRect);
            DrawMetricsBar(metricsRect);
            DrawFooter(footerRect);
        }

        private void DrawFormationHero(Rect rect)
        {
            const float boardSize = 122f;
            Rect boardRect = new Rect(
                Mathf.Round(rect.center.x - boardSize * 0.5f),
                rect.y + 6f,
                boardSize,
                boardSize);

            if (RitualPanelStyle.HeroGlow != null)
            {
                Rect glowRect = boardRect.ExpandedBy(42f, 18f);
                RitualPanelStyle.DrawIcon(glowRect, RitualPanelStyle.HeroGlow, RitualPanelStyle.HeroGlowTint);
            }

            int ringSize = assessment != null && assessment.RingSize > 0 ? assessment.RingSize : selected.ringSize;
            ChalkColor chalk = assessment != null && assessment.RingSize > 0 ? assessment.ChalkColor : selected.chalkColor;
            DrawChalkBoard(boardRect, ringSize, chalk);

            bool tierLocked = !altar.SupportsRitual(selected);
            string tierText = string.Format("WotF_RitualPanel_TierCallout".Translate(), selected.tier);
            Color tierColor = tierLocked ? RitualPanelStyle.WarnRed : RitualPanelStyle.CalloutText;

            // Top-left polyline: Tx (+ altar hint if tier locked)
            Vector2 tlAnchor = new Vector2(boardRect.x, boardRect.y + 22f);
            Vector2 tlElbow = new Vector2(boardRect.x - 18f, boardRect.y + 10f);
            float tlLabelEnd = Mathf.Max(rect.x + 12f, tlElbow.x - 78f);
            DrawCalloutPolyline(tlAnchor, tlElbow, tlLabelEnd);
            Rect tlRect = new Rect(rect.x, tlElbow.y - 20f, tlElbow.x - rect.x - 4f, 20f);
            RitualPanelStyle.DrawLabel(tlRect, tierText, GameFont.Small, tierColor, TextAnchor.LowerRight);
            if (tierLocked)
            {
                string reqAltar = AltarDefForTier(selected.tier)?.LabelCap ?? ("T" + selected.tier);
                TooltipHandler.TipRegion(tlRect,
                    string.Format("WotF_RitualPanel_TierLockedTip".Translate(), reqAltar, selected.tier));
            }

            // Bottom-left polyline: minRitualStrength requirement (only when > 0)
            if (selected.minRitualStrength > 0f)
            {
                bool strengthShort = assessment != null
                    && assessment.RitualStrength + 0.0001f < selected.minRitualStrength;
                string minText = string.Format("WotF_RitualPanel_MinStrengthCallout".Translate(),
                    selected.minRitualStrength.ToString("0.#"));
                Vector2 blAnchor = new Vector2(boardRect.x, boardRect.yMax - 22f);
                Vector2 blElbow = new Vector2(boardRect.x - 18f, boardRect.yMax - 10f);
                float blLabelEnd = Mathf.Max(rect.x + 12f, blElbow.x - 84f);
                DrawCalloutPolyline(blAnchor, blElbow, blLabelEnd);
                Rect blRect = new Rect(rect.x, blElbow.y + 2f, blElbow.x - rect.x - 4f, 20f);
                RitualPanelStyle.DrawLabel(blRect, minText, GameFont.Small,
                    strengthShort ? RitualPanelStyle.WarnRed : RitualPanelStyle.CalloutText,
                    TextAnchor.UpperRight);
            }

            // Right shallower polyline: ritual family icon + label
            Vector2 rAnchor = new Vector2(boardRect.xMax, boardRect.y + 26f);
            Vector2 rElbow = new Vector2(boardRect.xMax + 20f, boardRect.y + 18f);
            float rLabelEnd = Mathf.Min(rect.xMax - 12f, rElbow.x + 86f);
            DrawCalloutPolyline(rAnchor, rElbow, rLabelEnd);

            Color famColor = RitualPanelStyle.FamilyColor(selected.family);
            string famLabel = RitualPanelStyle.FamilyLabel(selected.family);
            Rect rIconRect = new Rect(rElbow.x + 6f, rElbow.y - 20f, 16f, 16f);
            RitualPanelStyle.DrawIcon(rIconRect, RitualPanelStyle.FamilyIcon(selected.family), famColor);
            Rect rTextRect = new Rect(rIconRect.xMax + 5f, rElbow.y - 22f, rect.xMax - (rIconRect.xMax + 5f), 20f);
            RitualPanelStyle.DrawLabel(rTextRect, famLabel, GameFont.Small, famColor, TextAnchor.MiddleLeft);
        }

        private void DrawChalkBoard(Rect boardRect, int ringSize, ChalkColor chalk)
        {
            RitualPanelStyle.DrawBox(boardRect, RitualPanelStyle.BoardBg, RitualPanelStyle.BoardBorder, 1);

            int clampedSize = ringSize >= 3 ? ringSize : 3;
            int radius = (clampedSize - 1) / 2;
            const float pad = 6f;
            const float gap = 2f;
            float usable = boardRect.width - pad * 2f - gap * (clampedSize - 1);
            float cellSize = Mathf.Floor(usable / clampedSize);
            float totalSpan = cellSize * clampedSize + gap * (clampedSize - 1);
            float startX = Mathf.Round(boardRect.center.x - totalSpan * 0.5f);
            float startY = Mathf.Round(boardRect.center.y - totalSpan * 0.5f);

            GetChalkColors(chalk, out Color chalkBg, out Color chalkBorder);

            for (int row = 0; row < clampedSize; row++)
            {
                int dz = row - radius;
                for (int col = 0; col < clampedSize; col++)
                {
                    int dx = col - radius;
                    Rect cellRect = new Rect(
                        startX + col * (cellSize + gap),
                        startY + row * (cellSize + gap),
                        cellSize,
                        cellSize);

                    bool isAltar = dx == 0 && dz == 0;
                    bool isRing = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) == radius;
                    if (isAltar)
                    {
                        RitualPanelStyle.DrawBox(cellRect, RitualPanelStyle.AltarCellBg,
                            RitualPanelStyle.AltarCellBorder, 1);
                        if (cellSize >= 13f)
                        {
                            RitualPanelStyle.DrawLabel(cellRect, "WotF_RitualPanel_AltarCellMark".Translate(),
                                GameFont.Tiny, RitualPanelStyle.AltarCellText, TextAnchor.MiddleCenter,
                                ellipsis: false);
                        }
                    }
                    else if (isRing)
                    {
                        RitualPanelStyle.DrawBox(cellRect, chalkBg, chalkBorder, 1);
                    }
                    else
                    {
                        RitualPanelStyle.DrawBox(cellRect, RitualPanelStyle.BoardCellEmpty,
                            RitualPanelStyle.BoardCellBorder, 1);
                    }
                }
            }

            string chalkName = ChalkLabel(chalk);
            string ringTip = string.Format("WotF_RitualPanel_RingTip".Translate(),
                clampedSize, clampedSize, chalkName);
            if (assessment != null && assessment.MissingCells.Count > 0)
            {
                ringTip += "\n" + "WotF_Ritual_RingIncomplete".Translate(assessment.MissingCells.Count);
            }
            else if (assessment != null)
            {
                ringTip += "\n" + "WotF_Ritual_RingComplete".Translate();
            }

            TooltipHandler.TipRegion(boardRect, ringTip);
        }

        private static void DrawCalloutPolyline(Vector2 anchor, Vector2 elbow, float horizontalEndX)
        {
            Widgets.DrawLine(anchor, elbow, RitualPanelStyle.CalloutLine, 1f);
            Widgets.DrawLine(elbow, new Vector2(horizontalEndX, elbow.y), RitualPanelStyle.CalloutLine, 1f);
            if (RitualPanelStyle.Dot != null)
            {
                Rect dotRect = new Rect(anchor.x - 2.5f, anchor.y - 2.5f, 5f, 5f);
                RitualPanelStyle.DrawIcon(dotRect, RitualPanelStyle.Dot, RitualPanelStyle.CalloutLine);
            }
        }

        private static void GetChalkColors(ChalkColor chalk, out Color bg, out Color border)
        {
            switch (chalk)
            {
                case ChalkColor.Green:
                    bg = RitualPanelStyle.ChalkGreenBg;
                    border = RitualPanelStyle.ChalkGreenBorder;
                    break;
                case ChalkColor.Star:
                    bg = RitualPanelStyle.ChalkStarBg;
                    border = RitualPanelStyle.ChalkStarBorder;
                    break;
                default:
                    bg = RitualPanelStyle.ChalkWhiteBg;
                    border = RitualPanelStyle.ChalkWhiteBorder;
                    break;
            }
        }

        private static string ChalkLabel(ChalkColor chalk)
        {
            switch (chalk)
            {
                case ChalkColor.Green:
                    return "WotF_RitualPanel_ChalkGreen".Translate();
                case ChalkColor.Star:
                    return "WotF_RitualPanel_ChalkStar".Translate();
                default:
                    return "WotF_RitualPanel_ChalkWhite".Translate();
            }
        }

        private void DrawRightScrollBody(Rect outerRect)
        {
            bool needScrollbar = rightViewHeight > outerRect.height;
            float viewWidth = outerRect.width - (needScrollbar ? 18f : 0f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(outerRect.height, rightViewHeight));

            Widgets.BeginScrollView(outerRect, ref rightScroll, viewRect);
            float y = 0f;

            // Single-line / wrapped description
            string desc = selected.description;
            if (!desc.NullOrEmpty())
            {
                float descH = Mathf.Max(20f, RitualPanelStyle.CalcTextHeight(desc, GameFont.Small, viewWidth));
                RitualPanelStyle.DrawLabel(new Rect(0f, y, viewWidth, descH), desc, GameFont.Small,
                    RitualPanelStyle.TextMain, TextAnchor.UpperLeft, ellipsis: false);
                y += descH + 8f;
            }

            // Cost <-> Preparation progress block
            y = DrawCostBlock(viewWidth, y);

            // Dynamic parameter section (rune / gate / ruin / equipment & spellbook)
            if (NeedsParamSection(selected))
            {
                y += 8f;
                y = DrawParamSection(viewWidth, y);
            }

            // Host & helper portrait section
            y += 10f;
            y = DrawRosterSection(viewWidth, y);

            rightViewHeight = y + 4f;
            Widgets.EndScrollView();
        }

        private float DrawCostBlock(float width, float startY)
        {
            List<OfferingLine> lines = assessment?.OfferingLines ?? new List<OfferingLine>();
            float manaCost = assessment != null ? assessment.ManaCost : selected.manaCost;
            float manaPerStrike = assessment != null ? assessment.ManaPerStrike : selected.manaPerStrike;

            bool hasOfferings = lines.Count > 0;
            bool showPrep = hasOfferings && altar.IsPreparedFor(selected, selectedRune);

            if (lastCostRitual != selected)
            {
                lastCostRitual = selected;
                lastCostShowProgress = showPrep;
                costAnimStart = -1000f;
            }
            else if (lastCostShowProgress.HasValue && lastCostShowProgress.Value != showPrep)
            {
                lastCostShowProgress = showPrep;
                costAnimStart = Time.realtimeSinceStartup;
            }

            float animAge = Time.realtimeSinceStartup - costAnimStart;
            float t = costAnimStart > 0f && animAge < CostAnimDuration
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animAge / CostAnimDuration))
                : 1f;

            Color fromBg = showPrep ? RitualPanelStyle.CostBoxBg : RitualPanelStyle.PrepBoxBg;
            Color toBg = showPrep ? RitualPanelStyle.PrepBoxBg : RitualPanelStyle.CostBoxBg;
            Color fromBorder = showPrep ? RitualPanelStyle.CostBoxBorder : RitualPanelStyle.PrepBoxBorder;
            Color toBorder = showPrep ? RitualPanelStyle.PrepBoxBorder : RitualPanelStyle.CostBoxBorder;

            Color boxBg = Color.Lerp(fromBg, toBg, t);
            Color boxBorder = Color.Lerp(fromBorder, toBorder, t);

            // Pre-compute chip widths so box height adapts to wrapping cleanly
            const float padX = 10f;
            const float padY = 8f;
            const float headerH = 20f;
            const float chipH = 30f;
            const float chipGapX = 6f;
            const float chipGapY = 6f;
            float innerWidth = width - padX * 2f;

            var chipWidths = new List<float>();
            bool hasMana = manaCost > 0f;
            bool hasStrikeMana = selected.sustained && manaPerStrike > 0f;

            string manaCountText = manaCost.ToString("0.#");
            if (hasMana)
            {
                chipWidths.Add(MeasureChipWidth("WotF_RitualPanel_ManaLabel".Translate(), manaCountText, innerWidth));
            }

            string strikeCountText = hasStrikeMana
                ? string.Format("WotF_RitualPanel_PerStrike".Translate(), manaPerStrike.ToString("0.#"))
                : string.Empty;
            if (hasStrikeMana)
            {
                chipWidths.Add(MeasureChipWidth("WotF_RitualPanel_ManaPerStrikeLabel".Translate(),
                    strikeCountText, innerWidth));
            }

            for (int i = 0; i < lines.Count; i++)
            {
                OfferingLine line = lines[i];
                string countStr = showPrep
                    ? line.Delivered + "/" + line.Cost.count
                    : "×" + line.Cost.count;
                chipWidths.Add(MeasureChipWidth(line.Cost.Label, countStr, innerWidth));
            }

            int rowCount = CountWrappedRows(chipWidths, innerWidth, chipGapX);
            float chipsTotalH = rowCount == 0 ? 0f : rowCount * chipH + (rowCount - 1) * chipGapY;
            float totalH = padY * 2f + headerH + (chipsTotalH > 0f ? 4f + chipsTotalH : 0f);

            Rect boxRect = new Rect(0f, startY, width, totalH);
            RitualPanelStyle.DrawBox(boxRect, boxBg, boxBorder, 1);

            string headerText = (showPrep
                ? "WotF_RitualPanel_PrepHeader"
                : "WotF_RitualPanel_CostHeader").Translate();
            float slideOffset = (1f - t) * 14f;
            Color headerColor = showPrep
                ? Color.Lerp(RitualPanelStyle.TextSubtle, RitualPanelStyle.PrepGoldText, t)
                : Color.Lerp(RitualPanelStyle.PrepGoldText, RitualPanelStyle.TextSubtle, t);
            RitualPanelStyle.DrawLabel(
                new Rect(padX + slideOffset, startY + padY, innerWidth - slideOffset, headerH),
                headerText,
                GameFont.Small,
                headerColor,
                TextAnchor.MiddleLeft);

            if (rowCount == 0)
            {
                return boxRect.yMax;
            }

            float cx = padX;
            float cy = startY + padY + headerH + 4f;
            int chipIdx = 0;

            if (hasMana)
            {
                float w = chipWidths[chipIdx++];
                Rect r = PlaceWrappedChip(padX, innerWidth, chipH, chipGapX, chipGapY, ref cx, ref cy, w);
                bool manaShort = assessment != null && !assessment.ManaEnough;
                Color bg = manaShort ? new Color(0.28f, 0.12f, 0.11f, 0.88f) : RitualPanelStyle.ChipBg;
                Color br = manaShort ? RitualPanelStyle.WarnBorder : RitualPanelStyle.ManaBorder;
                Color countC = manaShort ? RitualPanelStyle.WarnRed : RitualPanelStyle.ManaBlue;
                string storedStr = assessment != null ? assessment.ManaStored.ToString("0.#") : "0";
                string manaTip = string.Format("WotF_RitualPanel_ManaTip".Translate(), storedStr, manaCountText);
                RitualPanelStyle.DrawCostChip(r, RitualPanelStyle.ManaCrystal, null,
                    "WotF_RitualPanel_ManaLabel".Translate(), manaCountText,
                    bg, br, RitualPanelStyle.TextMain, countC, manaTip);
            }

            if (hasStrikeMana)
            {
                float w = chipWidths[chipIdx++];
                Rect r = PlaceWrappedChip(padX, innerWidth, chipH, chipGapX, chipGapY, ref cx, ref cy, w);
                RitualPanelStyle.DrawCostChip(r, RitualPanelStyle.ManaCrystal, null,
                    "WotF_RitualPanel_ManaPerStrikeLabel".Translate(), strikeCountText,
                    RitualPanelStyle.ChipBg, RitualPanelStyle.ManaBorder,
                    RitualPanelStyle.TextMain, RitualPanelStyle.ManaBlue);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                OfferingLine line = lines[i];
                float w = chipWidths[chipIdx++];
                Rect r = PlaceWrappedChip(padX, innerWidth, chipH, chipGapX, chipGapY, ref cx, ref cy, w);

                if (showPrep)
                {
                    bool missing = line.Missing > 0;
                    string countStr = line.Delivered + "/" + line.Cost.count;
                    Color bg = missing
                        ? new Color(0.27f, 0.12f, 0.11f, 0.88f)
                        : new Color(0.11f, 0.21f, 0.13f, 0.88f);
                    Color br = missing ? RitualPanelStyle.WarnBorder : RitualPanelStyle.OkBorder;
                    Color cc = missing ? RitualPanelStyle.WarnRed : RitualPanelStyle.OkText;
                    RitualPanelStyle.DrawOfferingChip(r, line.Cost, countStr,
                        bg, br, RitualPanelStyle.TextMain, cc);
                }
                else
                {
                    int available = CountCostAvailableOnMapAndAltar(line.Cost);
                    bool missingOnMap = available < line.Cost.count;
                    Color bg = missingOnMap
                        ? new Color(0.25f, 0.12f, 0.11f, 0.82f)
                        : RitualPanelStyle.ChipBg;
                    Color br = missingOnMap ? RitualPanelStyle.WarnBorder : RitualPanelStyle.ChipBorder;
                    Color cc = missingOnMap ? RitualPanelStyle.WarnRed : RitualPanelStyle.TitleCream;
                    string tip = missingOnMap
                        ? string.Format("WotF_RitualPanel_CostMissingTip".Translate(), available, line.Cost.count)
                        : null;
                    RitualPanelStyle.DrawOfferingChip(r, line.Cost, "×" + line.Cost.count,
                        bg, br, RitualPanelStyle.TextMain, cc, tip);
                }
            }

            return boxRect.yMax;
        }

        private int CountCostAvailableOnMapAndAltar(SpellGlyphCost cost)
        {
            Map map = altar.parent.Map;
            if (map == null || cost == null)
            {
                return 0;
            }

            ThingOwner held = altar.GetDirectlyHeldThings();
            if (cost.thingDef != null)
            {
                return CountAvailableOnMapAndAltar(map, held, cost.thingDef);
            }

            int total = 0;
            if (held != null)
            {
                for (int i = 0; i < held.Count; i++)
                {
                    if (cost.Matches(held[i]))
                    {
                        total += held[i].stackCount;
                    }
                }
            }

            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && cost.Matches(t))
                {
                    total += t.stackCount;
                }
            }

            return total;
        }

        private static float MeasureChipWidth(string label, string countText, float maxWidth)
        {
            float textW = RitualPanelStyle.CalcTextWidth(label, GameFont.Small);
            float countW = RitualPanelStyle.CalcTextWidth(countText, GameFont.Small);
            float w = 5f + 24f + 6f + textW + 5f + countW + 10f;
            return Mathf.Clamp(w, 86f, maxWidth);
        }

        private static int CountWrappedRows(List<float> widths, float innerWidth, float gapX)
        {
            if (widths.Count == 0)
            {
                return 0;
            }

            int rows = 1;
            float x = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                float w = widths[i];
                if (x > 0f && x + w > innerWidth + 0.5f)
                {
                    rows++;
                    x = w + gapX;
                }
                else
                {
                    x += w + gapX;
                }
            }

            return rows;
        }

        private static Rect PlaceWrappedChip(float padX, float innerWidth, float chipH, float gapX, float gapY,
            ref float x, ref float y, float width)
        {
            if (x > padX + 0.5f && (x - padX) + width > innerWidth + 0.5f)
            {
                x = padX;
                y += chipH + gapY;
            }

            Rect rect = new Rect(x, y, width, chipH);
            x += width + gapX;
            return rect;
        }

        private static bool NeedsParamSection(RitualDef def)
        {
            return def != null && (def.chooseRune || def.chooseTeleportTarget || def.chooseRuinType || def.chooseEquipment);
        }

        private float DrawParamSection(float width, float startY)
        {
            const float rowH = 30f;
            const float pad = 10f;
            bool locked = altar.IsActive && altar.ActiveDef == selected;

            int controlRows = (selected.chooseRune ? 1 : 0)
                + (selected.chooseTeleportTarget ? 1 : 0)
                + (selected.chooseRuinType ? 1 : 0)
                + (selected.chooseEquipment ? 2 : 0);

            float headerH = 20f;
            float boxH = pad * 2f + headerH + 4f + controlRows * rowH + Mathf.Max(0, controlRows - 1) * 6f;
            Rect boxRect = new Rect(0f, startY, width, boxH);
            RitualPanelStyle.DrawBox(boxRect, RitualPanelStyle.PanelSurfaceSoft, RitualPanelStyle.PageInnerBorder, 1);

            RitualPanelStyle.DrawLabel(new Rect(pad, startY + pad, width - pad * 2f, headerH),
                "WotF_RitualPanel_ParamHeader".Translate(),
                GameFont.Small, RitualPanelStyle.TextSubtle, TextAnchor.MiddleLeft);

            float y = startY + pad + headerH + 4f;
            float btnWidth = width - pad * 2f;

            if (selected.chooseRune)
            {
                DrawRuneSelector(new Rect(pad, y, btnWidth, rowH), locked);
                y += rowH + 6f;
            }

            if (selected.chooseTeleportTarget)
            {
                DrawGateSelector(new Rect(pad, y, btnWidth, rowH), locked);
                y += rowH + 6f;
            }

            if (selected.chooseRuinType)
            {
                DrawRuinSelector(new Rect(pad, y, btnWidth, rowH), locked);
                y += rowH + 6f;
            }

            if (selected.chooseEquipment)
            {
                DrawEquipmentSelectors(pad, y, btnWidth, rowH, locked);
            }

            return boxRect.yMax;
        }

        private void DrawRuneSelector(Rect rect, bool locked)
        {
            List<SpellGlyphDef> unlockable = CollectUnlockableRunes();
            string label = selectedRune != null
                ? selectedRune.LabelCap + " (T" + selectedRune.tier + ")"
                : unlockable.Count == 0
                    ? "WotF_Ritual_AllRunesUnlocked".Translate()
                    : "WotF_RitualPanel_PickRune".Translate();
            bool enabled = !locked && unlockable.Count > 0;

            if (RitualPanelStyle.DrawActionButton(rect, label, RitualPanelStyle.BtnBorder, enabled))
            {
                ClosePopover();
                var options = new List<FloatMenuOption>(unlockable.Count);
                for (int i = 0; i < unlockable.Count; i++)
                {
                    SpellGlyphDef glyph = unlockable[i];
                    options.Add(new FloatMenuOption(
                        glyph.LabelCap + " (T" + glyph.tier + ")",
                        () =>
                        {
                            selectedRune = glyph;
                            SyncSelection();
                            dirty = true;
                        }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private List<SpellGlyphDef> CollectUnlockableRunes()
        {
            var result = new List<SpellGlyphDef>();
            List<SpellGlyphDef> all = DefDatabase<SpellGlyphDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                SpellGlyphDef glyph = all[i];
                if (glyph != null && glyph.kind != SpellGlyphKind.Super && glyph.requiresUnlock
                    && glyph.tier <= altar.Props.tier && selected.RuneTierFor(glyph.tier) != null
                    && !GameComponent_Spellbook.IsUnlockedInData(glyph))
                {
                    result.Add(glyph);
                }
            }

            result.Sort((a, b) => a.tier != b.tier ? a.tier.CompareTo(b.tier) : a.index.CompareTo(b.index));
            return result;
        }

        private void DrawGateSelector(Rect rect, bool locked)
        {
            List<Building> gates = CollectTeleportGates();
            string label = selectedNode != null
                ? selectedNode.LabelCap
                : gates.Count == 0
                    ? "WotF_Ritual_Teleport_NoGate".Translate()
                    : "WotF_RitualPanel_PickGate".Translate();
            bool enabled = !locked && gates.Count > 0;

            if (RitualPanelStyle.DrawActionButton(rect, label, RitualPanelStyle.BtnBorder, enabled))
            {
                ClosePopover();
                var options = new List<FloatMenuOption>(gates.Count);
                for (int i = 0; i < gates.Count; i++)
                {
                    Building gateBuilding = gates[i];
                    CompTeleportGate gate = gateBuilding.TryGetComp<CompTeleportGate>();
                    string state = gate.Running
                        ? "WotF_RitualPanel_GateReady".Translate()
                        : gate.StateText();
                    options.Add(new FloatMenuOption(
                        gateBuilding.LabelCap + " · " + state,
                        () =>
                        {
                            selectedNode = gateBuilding;
                            SyncSelection();
                            dirty = true;
                        }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private List<Building> CollectTeleportGates()
        {
            var gates = new List<Building>();
            Map map = altar.parent.Map;
            if (map == null)
            {
                return gates;
            }

            List<Building> all = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < all.Count; i++)
            {
                Building b = all[i];
                CompTeleportGate gate = b.TryGetComp<CompTeleportGate>();
                if (gate != null && !gate.Ruined)
                {
                    gates.Add(b);
                }
            }

            return gates;
        }

        private void DrawRuinSelector(Rect rect, bool locked)
        {
            List<RuinSeekOption> ruins = selected.ruinOptions;
            string label = selectedRuin != null
                ? selectedRuin.sitePart.LabelCap
                : "WotF_RitualPanel_PickRuin".Translate();
            bool enabled = !locked && ruins != null && ruins.Count > 0;

            if (RitualPanelStyle.DrawActionButton(rect, label, RitualPanelStyle.BtnBorder, enabled))
            {
                ClosePopover();
                var options = new List<FloatMenuOption>(ruins.Count);
                for (int i = 0; i < ruins.Count; i++)
                {
                    RuinSeekOption option = ruins[i];
                    options.Add(new FloatMenuOption(
                        option.sitePart.LabelCap,
                        () =>
                        {
                            selectedRuin = option;
                            SyncSelection();
                            dirty = true;
                        }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void DrawEquipmentSelectors(float x, float y, float width, float rowH, bool locked)
        {
            List<Thing> nearby = altar.EnchantableNearby(selected);
            string equipLabel = selectedEquipment != null
                ? selectedEquipment.LabelShortCap
                : nearby.Count == 0
                    ? "WotF_Ritual_NoEnchantable".Translate()
                    : "WotF_Ritual_ChooseEquipment".Translate();
            Rect equipRect = new Rect(x, y, width, rowH);
            if (RitualPanelStyle.DrawActionButton(equipRect, equipLabel, RitualPanelStyle.BtnBorder,
                    !locked && nearby.Count > 0))
            {
                ClosePopover();
                var options = new List<FloatMenuOption>(nearby.Count);
                for (int i = 0; i < nearby.Count; i++)
                {
                    Thing item = nearby[i];
                    Equipment.CompEnchantedEquipment comp = item.TryGetComp<Equipment.CompEnchantedEquipment>();
                    string inspect = comp?.CompInspectStringExtra();
                    string text = inspect.NullOrEmpty()
                        ? item.LabelCap
                        : item.LabelCap + " (" + inspect + ")";
                    options.Add(new FloatMenuOption(
                        text,
                        () =>
                        {
                            selectedEquipment = item;
                            SyncSelection();
                            dirty = true;
                        }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            int slots = selectedHost == null ? 0 : SpellService.OpenSlotCount(selectedHost);
            string bookLabel;
            bool bookEnabled = !locked && selectedHost != null && slots > 0;
            if (selectedHost == null)
            {
                bookLabel = "WotF_Ritual_HostFirst".Translate();
            }
            else if (slots == 0)
            {
                bookLabel = "WotF_Ritual_HostNoBook".Translate();
            }
            else if (selectedBookSlot >= 0 && selectedBookSlot < slots)
            {
                SpellRecipe picked = GameComponent_Spellbook.Current.GetSlot(selectedHost, selectedBookSlot);
                string spellName = picked == null || picked.name.NullOrEmpty()
                    ? string.Format("WotF_RitualPanel_BookSlotPrefix".Translate(), selectedBookSlot + 1)
                    : picked.name;
                bookLabel = picked != null
                    ? string.Format("WotF_RitualPanel_BookSlotSelected".Translate(), selectedBookSlot + 1, spellName)
                    : "WotF_Ritual_ChooseBookSlot".Translate();
            }
            else
            {
                bookLabel = "WotF_Ritual_ChooseBookSlot".Translate();
            }

            Rect bookRect = new Rect(x, y + rowH + 6f, width, rowH);
            if (RitualPanelStyle.DrawActionButton(bookRect, bookLabel, RitualPanelStyle.BtnBorder, bookEnabled))
            {
                ClosePopover();
                var options = new List<FloatMenuOption>(slots);
                for (int s = 0; s < slots; s++)
                {
                    int slotIndex = s;
                    SpellRecipe recipe = GameComponent_Spellbook.Current.GetSlot(selectedHost, slotIndex);
                    string slotTitle = string.Format("WotF_RitualPanel_BookSlotPrefix".Translate(), slotIndex + 1);
                    if (recipe == null)
                    {
                        options.Add(new FloatMenuOption(slotTitle + " · " + "WotF_RitualPanel_BookSlotEmpty".Translate(), null));
                        continue;
                    }

                    string recipeLabel = recipe.name.NullOrEmpty() ? slotTitle : recipe.name;
                    bool ok = Equipment.CompEnchantedEquipment.AcceptsRecipe(recipe, out string reason);
                    string rowText = ok
                        ? slotTitle + " · " + recipeLabel
                        : slotTitle + " · " + recipeLabel + " (" + reason + ")";
                    options.Add(new FloatMenuOption(
                        rowText,
                        ok
                            ? (System.Action)(() =>
                            {
                                selectedBookSlot = slotIndex;
                                SyncSelection();
                                dirty = true;
                            })
                            : null));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private float DrawRosterSection(float width, float startY)
        {
            bool viewingActive = altar.IsActive && altar.ActiveDef == selected;
            Pawn displayHost = viewingActive ? altar.ActiveHost : selectedHost;

            var displayHelpers = new List<Pawn>();
            if (viewingActive)
            {
                IReadOnlyList<Pawn> activeList = altar.ActiveHelpers;
                for (int i = 0; i < activeList.Count; i++)
                {
                    if (activeList[i] != null)
                    {
                        displayHelpers.Add(activeList[i]);
                    }
                }
            }
            else
            {
                foreach (Pawn h in helpers)
                {
                    if (h != null)
                    {
                        displayHelpers.Add(h);
                    }
                }

                displayHelpers.Sort((a, b) => string.Compare(a.LabelShort, b.LabelShort, System.StringComparison.Ordinal));
            }

            const float pad = 10f;
            const float labelH = 18f;
            const float slotW = 62f;
            const float slotH = 74f;
            const float slotGap = 6f;

            float hostColW = slotW + 14f;
            float helpersStartX = pad + hostColW + 10f;
            float helpersAvailW = Mathf.Max(slotW, width - helpersStartX - pad);
            int helperSlotsCount = viewingActive ? displayHelpers.Count : displayHelpers.Count + 1;
            int helpersPerRow = Mathf.Max(1, Mathf.FloorToInt((helpersAvailW + slotGap) / (slotW + slotGap)));
            int helperRows = Mathf.Max(1, Mathf.CeilToInt(helperSlotsCount / (float)helpersPerRow));

            float contentH = labelH + 6f + helperRows * slotH + (helperRows - 1) * slotGap;
            float boxH = pad * 2f + contentH;
            Rect boxRect = new Rect(0f, startY, width, boxH);
            RitualPanelStyle.DrawBox(boxRect, RitualPanelStyle.PanelSurfaceSoft, RitualPanelStyle.PageInnerBorder, 1);

            float topY = startY + pad;
            RitualPanelStyle.DrawLabel(new Rect(pad, topY, hostColW, labelH),
                "WotF_RitualPanel_HostLabel".Translate(), GameFont.Small,
                RitualPanelStyle.TextSubtle, TextAnchor.MiddleLeft);

            string helperHeader = selected.minParticipants > 0
                ? string.Format("WotF_RitualPanel_HelperHeaderMin".Translate(), selected.minParticipants)
                : "WotF_RitualPanel_HelperLabel".Translate();
            Color helperHeaderColor = !viewingActive && displayHelpers.Count < selected.minParticipants
                ? RitualPanelStyle.WarnRed
                : RitualPanelStyle.TextSubtle;
            RitualPanelStyle.DrawLabel(new Rect(helpersStartX, topY, helpersAvailW, labelH),
                helperHeader, GameFont.Small, helperHeaderColor, TextAnchor.MiddleLeft);

            // Vertical divider between Host and Helpers
            Widgets.DrawBoxSolid(
                new Rect(helpersStartX - 6f, topY + 2f, 1f, contentH - 4f),
                RitualPanelStyle.SepColor);

            float portraitsTop = topY + labelH + 6f;
            Rect hostSlotRect = new Rect(pad, portraitsTop, slotW, slotH);
            DrawPortraitSlot(hostSlotRect, displayHost, forHost: true, interactive: !viewingActive);

            for (int i = 0; i < helperSlotsCount; i++)
            {
                int row = i / helpersPerRow;
                int col = i % helpersPerRow;
                Rect slotRect = new Rect(
                    helpersStartX + col * (slotW + slotGap),
                    portraitsTop + row * (slotH + slotGap),
                    slotW,
                    slotH);
                Pawn helper = i < displayHelpers.Count ? displayHelpers[i] : null;
                DrawPortraitSlot(slotRect, helper, forHost: false, interactive: !viewingActive);
            }

            return boxRect.yMax;
        }

        private void DrawPortraitSlot(Rect rect, Pawn pawn, bool forHost, bool interactive)
        {
            bool over = interactive && Mouse.IsOver(rect);
            if (pawn != null)
            {
                Color border = over ? RitualPanelStyle.PortraitHoverBorder : RitualPanelStyle.PortraitBorder;
                RitualPanelStyle.DrawBox(rect, RitualPanelStyle.PortraitBg, border, 1);

                Rect imgRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 20f);
                RitualPanelStyle.DrawPawnPortrait(imgRect, pawn, new Vector2(imgRect.width, imgRect.height));

                float level = Gene_ForestBloodline.Get(pawn)?.Level ?? 0f;
                string lvText = "Lv." + level.ToString("0.#");
                float lvW = Mathf.Max(26f, RitualPanelStyle.CalcTextWidth(lvText, GameFont.Tiny) + 6f);
                Rect lvRect = new Rect(rect.xMax - lvW - 3f, rect.y + 3f, lvW, 15f);
                RitualPanelStyle.DrawBox(lvRect, RitualPanelStyle.LevelTagBg, RitualPanelStyle.LevelTagBorder, 1);
                RitualPanelStyle.DrawLabel(lvRect, lvText, GameFont.Tiny, RitualPanelStyle.LevelTagText,
                    TextAnchor.MiddleCenter, ellipsis: false);

                Rect nameRect = new Rect(rect.x + 1f, rect.yMax - 19f, rect.width - 2f, 18f);
                Widgets.DrawBoxSolid(nameRect, RitualPanelStyle.NameTagBg);
                RitualPanelStyle.DrawLabel(nameRect.ContractedBy(3f, 0f), pawn.LabelShortCap, GameFont.Tiny,
                    RitualPanelStyle.TitleCream, TextAnchor.MiddleCenter);
            }
            else
            {
                RitualPanelStyle.DrawBox(rect, RitualPanelStyle.PortraitEmptyBg, Color.clear, 0);
                RitualPanelStyle.DrawDashedRect(rect,
                    over ? RitualPanelStyle.PortraitHoverBorder : RitualPanelStyle.PortraitEmptyBorder);

                RitualPanelStyle.DrawIcon(
                    new Rect(rect.center.x - 10f, rect.y + 18f, 20f, 20f),
                    RitualPanelStyle.Plus,
                    over ? RitualPanelStyle.TitleCream : RitualPanelStyle.TextSubtle);

                string emptyLabel = (forHost
                    ? "WotF_RitualPanel_ChooseShort"
                    : "WotF_RitualPanel_AddHelperShort").Translate();
                RitualPanelStyle.DrawLabel(
                    new Rect(rect.x + 3f, rect.yMax - 24f, rect.width - 6f, 20f),
                    emptyLabel, GameFont.Tiny,
                    over ? RitualPanelStyle.TitleCream : RitualPanelStyle.TextSubtle,
                    TextAnchor.MiddleCenter);
            }

            if (!interactive)
            {
                TooltipHandler.TipRegion(rect, "WotF_RitualPanel_LockedWhileRunning".Translate());
                return;
            }

            if (pawn != null && Event.current.type == EventType.MouseDown && Event.current.button == 1
                && Mouse.IsOver(rect))
            {
                Event.current.Use();
                RitualPanelStyle.PlayClick();
                if (forHost)
                {
                    selectedHost = null;
                    selectedBookSlot = -1;
                }
                else
                {
                    helpers.Remove(pawn);
                }

                dirty = true;
                return;
            }

            if (Widgets.ButtonInvisible(rect, doMouseoverSound: true))
            {
                Vector2 scrollTopLeft = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
                Rect screenAnchor = new Rect(scrollTopLeft.x, scrollTopLeft.y, rect.width, rect.height);
                OpenPawnPopover(forHost, screenAnchor);
            }
        }

        private void DrawMetricsBar(Rect rect)
        {
            RitualPanelStyle.DrawBox(rect, RitualPanelStyle.PanelSurface, RitualPanelStyle.PageInnerBorder, 1);

            bool viewingActive = altar.IsActive && altar.ActiveDef == selected;
            float strength = viewingActive ? altar.ActiveRitualStrength : (assessment?.RitualStrength ?? 0f);
            Pawn hostForCalc = viewingActive ? altar.ActiveHost : selectedHost;

            ResolveMetricDisplay(selected, assessment, strength, hostForCalc, viewingActive,
                out string metricLabel, out string metricValue, out bool metricBlank);

            float topHeight = viewingActive ? rect.height - 20f : rect.height;
            Rect topRow = new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, topHeight - 6f);
            float halfW = Mathf.Floor(topRow.width * 0.5f);

            if (!metricBlank)
            {
                Rect leftRect = new Rect(topRow.x, topRow.y, halfW, topRow.height);
                RitualPanelStyle.DrawLabel(new Rect(leftRect.x, leftRect.y, leftRect.width, 18f),
                    metricLabel, GameFont.Small, RitualPanelStyle.TextSubtle, TextAnchor.MiddleLeft);
                RitualPanelStyle.DrawLabel(new Rect(leftRect.x, leftRect.y + 18f, leftRect.width, leftRect.height - 18f),
                    metricValue, GameFont.Medium, RitualPanelStyle.TitleCream, TextAnchor.MiddleLeft);
            }

            Rect rightRect = new Rect(topRow.x + halfW, topRow.y, topRow.width - halfW, topRow.height);
            RitualPanelStyle.DrawLabel(new Rect(rightRect.x, rightRect.y, rightRect.width, 18f),
                "WotF_RitualPanel_StrengthLabel".Translate(),
                GameFont.Small, RitualPanelStyle.TextSubtle, TextAnchor.MiddleRight);

            bool belowMin = selected.minRitualStrength > 0f && strength + 0.0001f < selected.minRitualStrength;
            Color rColor = belowMin ? RitualPanelStyle.WarnRed
                : strength > 0f ? RitualPanelStyle.ManaBlue
                : RitualPanelStyle.TextMuted;
            RitualPanelStyle.DrawLabel(new Rect(rightRect.x, rightRect.y + 18f, rightRect.width, rightRect.height - 18f),
                strength.ToString("0.##"), GameFont.Medium, rColor, TextAnchor.MiddleRight);

            if (viewingActive)
            {
                float frac = Mathf.Clamp01(altar.ActiveProgressFraction);
                Rect progRow = new Rect(rect.x + 12f, rect.yMax - 18f, rect.width - 24f, 13f);
                string phaseLabel = PhaseLabel(altar.Phase);
                float phaseW = Mathf.Max(60f, RitualPanelStyle.CalcTextWidth(phaseLabel, GameFont.Tiny) + 6f);
                RitualPanelStyle.DrawLabel(new Rect(progRow.x, progRow.y - 1f, phaseW, 15f),
                    phaseLabel, GameFont.Tiny, RitualPanelStyle.OkText, TextAnchor.MiddleLeft);

                string pctText = altar.Phase == RitualPhase.Sustaining
                    ? "WotF_RitualPanel_SustainingActive".Translate()
                    : Mathf.RoundToInt(frac * 100f) + "%";
                float pctW = Mathf.Max(38f, RitualPanelStyle.CalcTextWidth(pctText, GameFont.Tiny) + 4f);
                RitualPanelStyle.DrawLabel(new Rect(progRow.xMax - pctW, progRow.y - 1f, pctW, 15f),
                    pctText, GameFont.Tiny, RitualPanelStyle.OkText, TextAnchor.MiddleRight);

                Rect barRect = new Rect(progRow.x + phaseW + 4f, progRow.y + 2f,
                    progRow.width - phaseW - pctW - 8f, 8f);
                RitualPanelStyle.DrawBox(barRect, RitualPanelStyle.TrackBg, RitualPanelStyle.TrackActiveBorder, 1);
                if (frac > 0.001f)
                {
                    Rect fillRect = new Rect(barRect.x + 1f, barRect.y + 1f,
                        Mathf.Max(2f, (barRect.width - 2f) * frac), barRect.height - 2f);
                    Widgets.DrawBoxSolid(fillRect, RitualPanelStyle.TrackFill);
                }
            }
        }

        private void ResolveMetricDisplay(RitualDef def, RitualAssessment assess, float strength, Pawn host,
            bool viewingActive, out string label, out string value, out bool isBlank)
        {
            isBlank = false;
            label = string.Empty;
            value = string.Empty;

            if (def.effectWorkerClass == typeof(RitualEffect_Weather))
            {
                isBlank = true;
                return;
            }

            if (def.UsesWork)
            {
                label = "WotF_RitualPanel_MetricWork".Translate();
                if (viewingActive)
                {
                    int done = Mathf.RoundToInt(altar.ActiveProgress);
                    int total = Mathf.Max(1, Mathf.RoundToInt(altar.ActiveWorkAmount));
                    value = done + " / " + total;
                }
                else
                {
                    float work = assess != null && assess.WorkAmount > 0f ? assess.WorkAmount : def.workAmount;
                    value = work > 0f ? work.ToString("0") : "--";
                }

                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_Healing))
            {
                label = "WotF_RitualPanel_MetricHealingPool".Translate();
                value = def.HealingPoolFor(strength).ToString("0.#");
                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_ManaSpring))
            {
                label = "WotF_RitualPanel_MetricManaRestore".Translate();
                value = RitualEffect_ManaSpring.ManaRestoreFor(def, strength).ToString("0.#");
                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_FertileSoil))
            {
                label = "WotF_RitualPanel_MetricRadius".Translate();
                value = string.Format("WotF_RitualPanel_CellsUnit".Translate(),
                    def.RadiusFor(strength).ToString("0.#"));
                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_LeylineMining))
            {
                label = "WotF_RitualPanel_MetricYieldFactor".Translate();
                value = "×" + RitualEffect_LeylineMining.YieldFactorFor(def, strength).ToString("0.##");
                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_BeastCall))
            {
                label = "WotF_RitualPanel_MetricTameChance".Translate();
                float wildness = def.summonedAnimal != null && def.summonedAnimal.race != null
                    ? def.summonedAnimal.race.GetStatValueAbstract(StatDefOf.Wildness)
                    : 0f;
                float chance = RitualEffect_BeastCall.TameChanceFor(def, host, strength, wildness);
                value = Mathf.RoundToInt(chance * 100f) + "%";
                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_SkyWrath))
            {
                label = "WotF_RitualPanel_MetricStrikeRate".Translate();
                value = string.Format("WotF_RitualPanel_PerMinuteUnit".Translate(),
                    def.StrikesPerMinuteFor(strength).ToString("0.##"));
                return;
            }

            if (def.effectWorkerClass == typeof(RitualEffect_ShieldDome))
            {
                label = "WotF_RitualPanel_MetricShieldCharges".Translate();
                value = def.ShieldChargesFor(strength).ToString();
                return;
            }

            isBlank = true;
        }

        private static string PhaseLabel(RitualPhase phase)
        {
            switch (phase)
            {
                case RitualPhase.Gathering:
                    return "WotF_RitualPanel_PhaseGathering".Translate();
                case RitualPhase.Chanting:
                    return "WotF_RitualPanel_PhaseChanting".Translate();
                default:
                    return "WotF_RitualPanel_PhaseSustaining".Translate();
            }
        }

        private void DrawFooter(Rect rect)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), RitualPanelStyle.SepColor);

            float topY = rect.y + 8f;
            const float btnH = 34f;
            const float abortW = 96f;
            float primaryW = rect.width - abortW - 10f;

            Rect primaryRect = new Rect(rect.x, topY, primaryW, btnH);
            Rect abortRect = new Rect(primaryRect.xMax + 10f, topY, abortW, btnH);

            DrawPrimaryActionButton(primaryRect);
            DrawAbortButton(abortRect);

            if (selected.autoRepeatable)
            {
                Rect autoRow = new Rect(primaryRect.x, primaryRect.yMax + 6f, primaryRect.width, 22f);
                DrawAutoRepeatCheckbox(autoRow);
            }
        }

        private void DrawPrimaryActionButton(Rect rect)
        {
            if (altar.IsActive)
            {
                RitualPanelStyle.DrawActionButton(rect, "WotF_RitualPanel_RunningBtn".Translate(),
                    RitualPanelStyle.BtnPrimaryBorder, enabled: false);
                TooltipHandler.TipRegion(rect,
                    string.Format("WotF_Ritual_AlreadyRunning".Translate(), altar.ActiveLabel));
                return;
            }

            bool hasOfferingsToPrepare = selected.chooseRune
                ? (selectedRune == null || CompRitualAltar.RitualCosts(selected, selectedRune).Count > 0)
                : CompRitualAltar.RitualCosts(selected, selectedRune).Count > 0;
            bool inPrepareMode = hasOfferingsToPrepare && !altar.IsPreparedFor(selected, selectedRune);

            if (inPrepareMode)
            {
                bool canPrepare = altar.CanPrepare(selected, selectedRune);
                if (!canPrepare)
                {
                    string reason = !altar.SupportsRitual(selected)
                        ? string.Format("WotF_Ritual_TierRequired".Translate(), selected.tier)
                        : (selected.chooseRune && selectedRune == null
                            ? "WotF_Ritual_ChooseRune".Translate()
                            : null);
                    if (!reason.NullOrEmpty())
                    {
                        TooltipHandler.TipRegion(rect, reason);
                    }
                }

                if (RitualPanelStyle.DrawActionButton(rect, "WotF_RitualPanel_PrepareBtn".Translate(),
                        RitualPanelStyle.BtnPrimaryBorder, canPrepare))
                {
                    ClosePopover();
                    altar.Prepare(selected, selectedRune);
                    SyncSelection();
                    dirty = true;
                }

                return;
            }

            bool canStart = assessment != null && assessment.CanStart;
            if (!canStart && assessment != null && assessment.Problems.Count > 0)
            {
                TooltipHandler.TipRegion(rect, string.Join("\n", assessment.Problems));
            }

            if (RitualPanelStyle.DrawActionButton(rect, "WotF_RitualPanel_StartBtn".Translate(),
                    RitualPanelStyle.BtnPrimaryBorder, canStart))
            {
                ClosePopover();
                if (altar.TryStart(BuildRequest(), silent: false))
                {
                    Close();
                }
                else
                {
                    dirty = true;
                }
            }
        }

        private void DrawAbortButton(Rect rect)
        {
            bool canAbort = altar.IsActive || altar.CollectDef != null;
            string tip = altar.IsActive
                ? string.Format("WotF_RitualPanel_AbortActiveTip".Translate(), altar.ActiveLabel)
                : altar.CollectDef != null
                    ? string.Format("WotF_RitualPanel_AbortPrepTip".Translate(), altar.CollectLabel)
                    : "WotF_RitualPanel_AbortIdleTip".Translate();
            TooltipHandler.TipRegion(rect, tip);

            if (RitualPanelStyle.DrawActionButton(rect, "WotF_RitualPanel_AbortBtn".Translate(),
                    RitualPanelStyle.BtnDangerBorder, canAbort))
            {
                ClosePopover();
                altar.CancelByPlayer();
                dirty = true;
            }
        }

        private void DrawAutoRepeatCheckbox(Rect rowRect)
        {
            bool isAuto = altar.AutoRitual == selected;
            bool enabled = altar.SupportsRitual(selected);
            string label = "WotF_RitualPanel_AutoExecute".Translate();
            float textW = RitualPanelStyle.CalcTextWidth(label, GameFont.Small);
            const float iconSize = 18f;
            float totalW = iconSize + 6f + textW + 8f;
            Rect hitRect = new Rect(
                Mathf.Round(rowRect.center.x - totalW * 0.5f),
                rowRect.y,
                totalW,
                rowRect.height);

            bool over = enabled && Mouse.IsOver(hitRect);
            float pulseAge = Time.realtimeSinceStartup - autoPulseStart;
            float pulse = autoPulseStart > 0f && pulseAge < AutoPulseDuration
                ? Mathf.Sin(Mathf.Clamp01(pulseAge / AutoPulseDuration) * Mathf.PI)
                : 0f;

            if (over || pulse > 0.01f)
            {
                Color hl = new Color(RitualPanelStyle.BtnPrimaryBorder.r,
                    RitualPanelStyle.BtnPrimaryBorder.g,
                    RitualPanelStyle.BtnPrimaryBorder.b,
                    0.12f + 0.25f * pulse);
                Widgets.DrawBoxSolid(hitRect, hl);
            }

            float extra = pulse * 3f;
            Rect iconRect = new Rect(
                hitRect.x + 4f - extra * 0.5f,
                hitRect.y + (hitRect.height - iconSize) * 0.5f - extra * 0.5f,
                iconSize + extra,
                iconSize + extra);
            Texture2D checkTex = isAuto ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
            Color tint = !enabled ? RitualPanelStyle.TextMuted
                : isAuto ? RitualPanelStyle.OkGreen
                : over ? RitualPanelStyle.TitleCream
                : RitualPanelStyle.TextSubtle;
            RitualPanelStyle.DrawIcon(iconRect, checkTex, tint);

            Rect textRect = new Rect(hitRect.x + 4f + iconSize + 6f, hitRect.y, textW + 4f, hitRect.height);
            RitualPanelStyle.DrawLabel(textRect, label, GameFont.Small,
                !enabled ? RitualPanelStyle.TextMuted
                    : isAuto ? RitualPanelStyle.TitleCream
                    : RitualPanelStyle.TextSubtle,
                TextAnchor.MiddleLeft, ellipsis: false);

            if (!enabled)
            {
                string reqAltar = AltarDefForTier(selected.tier)?.LabelCap ?? ("T" + selected.tier);
                TooltipHandler.TipRegion(hitRect,
                    string.Format("WotF_RitualPanel_TierLockedTip".Translate(), reqAltar, selected.tier));
                return;
            }

            if (Widgets.ButtonInvisible(hitRect, doMouseoverSound: true))
            {
                autoPulseStart = Time.realtimeSinceStartup;
                altar.SetAutoRitual(isAuto ? null : selected);
                dirty = true;
            }
        }
    }
}
