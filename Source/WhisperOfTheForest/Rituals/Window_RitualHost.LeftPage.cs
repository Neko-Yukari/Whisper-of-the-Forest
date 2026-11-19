using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    public sealed partial class Window_RitualHost
    {
        private void DrawLeftPage(Rect pageRect)
        {
            Rect inner = pageRect.ContractedBy(14f, 12f);

            const float footerHeight = 46f;
            Rect footerRect = new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight);
            Rect scrollOuter = new Rect(inner.x, inner.y, inner.width, footerRect.y - inner.y - 8f);

            DrawLeftCatalog(scrollOuter);
            DrawLeftFooter(footerRect);
        }

        private void DrawLeftCatalog(Rect outerRect)
        {
            if (researchedRituals.Count == 0)
            {
                RitualPanelStyle.DrawLabel(
                    outerRect,
                    "WotF_RitualPanel_NoResearched".Translate(),
                    GameFont.Small,
                    RitualPanelStyle.TextMuted,
                    TextAnchor.MiddleCenter,
                    ellipsis: false);
                return;
            }

            bool needScrollbar = leftViewHeight > outerRect.height;
            float viewWidth = outerRect.width - (needScrollbar ? 18f : 0f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(outerRect.height, leftViewHeight));

            Widgets.BeginScrollView(outerRect, ref leftScroll, viewRect);
            float y = 0f;
            for (int tier = 1; tier <= 3; tier++)
            {
                if (!HasTierRituals(tier))
                {
                    continue;
                }

                if (y > 0f)
                {
                    y += 10f;
                }

                y = DrawTierGroup(viewWidth, y, tier);
            }

            leftViewHeight = y + 4f;
            Widgets.EndScrollView();
        }

        private bool HasTierRituals(int tier)
        {
            for (int i = 0; i < researchedRituals.Count; i++)
            {
                if (researchedRituals[i].tier == tier)
                {
                    return true;
                }
            }

            return false;
        }

        private float DrawTierGroup(float width, float startY, int tier)
        {
            const float headerH = 20f;
            const int columns = 3;
            const float gap = 8f;
            const float cardH = 66f;

            string title = string.Format("WotF_RitualPanel_TierHeader".Translate(), tier);
            bool tierTooHigh = tier > altar.Props.tier;
            string reqText = tierTooHigh
                ? " · " + (AltarDefForTier(tier)?.LabelCap ?? ("T" + tier))
                : string.Empty;

            float titleW = RitualPanelStyle.CalcTextWidth(title, GameFont.Small);
            RitualPanelStyle.DrawLabel(new Rect(0f, startY, titleW + 4f, headerH), title,
                GameFont.Small, RitualPanelStyle.HeaderGreen, TextAnchor.MiddleLeft, ellipsis: false);

            float ruleStart = titleW + 8f;
            if (!reqText.NullOrEmpty())
            {
                float reqW = RitualPanelStyle.CalcTextWidth(reqText, GameFont.Tiny);
                RitualPanelStyle.DrawLabel(new Rect(titleW + 4f, startY, reqW + 4f, headerH), reqText,
                    GameFont.Tiny, RitualPanelStyle.TextMuted, TextAnchor.MiddleLeft, ellipsis: false);
                ruleStart = titleW + reqW + 10f;
            }

            if (ruleStart < width - 8f)
            {
                Widgets.DrawBoxSolid(
                    new Rect(ruleStart, startY + headerH * 0.5f, width - ruleStart, 1f),
                    RitualPanelStyle.SepColor);
            }

            float y = startY + headerH + 6f;
            float cardW = Mathf.Floor((width - gap * (columns - 1)) / columns);
            int col = 0;

            for (int i = 0; i < researchedRituals.Count; i++)
            {
                RitualDef def = researchedRituals[i];
                if (def.tier != tier)
                {
                    continue;
                }

                float x = col * (cardW + gap);
                Rect cardRect = new Rect(x, y, cardW, cardH);
                DrawRitualCard(cardRect, def);

                col++;
                if (col >= columns)
                {
                    col = 0;
                    y += cardH + gap;
                }
            }

            if (col > 0)
            {
                y += cardH;
            }
            else if (y > startY + headerH + 6f)
            {
                y -= gap;
            }

            return y;
        }

        private void DrawRitualCard(Rect cardRect, RitualDef def)
        {
            bool tierLocked = !altar.SupportsRitual(def);
            bool isRunning = altar.IsActive && altar.ActiveDef == def;
            bool isSelected = selected == def;
            bool isAuto = altar.AutoRitual == def;
            bool isCollecting = !tierLocked && !isRunning && altar.CollectDef == def;
            bool isMissingMat = !tierLocked && !isRunning
                && !(isCollecting && altar.HasOfferingsFor(def, altar.CollectRune))
                && missingMapMaterials.Contains(def);

            bool hover = Mouse.IsOver(cardRect);

            Color fill;
            Color border;
            int borderThickness = isSelected ? 2 : 1;
            if (isRunning)
            {
                fill = isSelected ? RitualPanelStyle.CardRunSelBg : RitualPanelStyle.CardRunBg;
                border = isSelected ? RitualPanelStyle.CardRunSelBorder : RitualPanelStyle.CardRunBorder;
            }
            else if (tierLocked)
            {
                fill = isSelected ? RitualPanelStyle.CardLockedSelBg : RitualPanelStyle.CardLockedBg;
                border = isSelected ? RitualPanelStyle.CardLockedSelBorder : RitualPanelStyle.CardLockedBorder;
            }
            else if (isSelected)
            {
                fill = RitualPanelStyle.CardSelBg;
                border = RitualPanelStyle.CardSelBorder;
            }
            else
            {
                fill = hover ? RitualPanelStyle.CardHoverBg : RitualPanelStyle.CardNormalBg;
                border = hover ? RitualPanelStyle.CardHoverBorder : RitualPanelStyle.CardBorder;
            }

            RitualPanelStyle.DrawBox(cardRect, fill, border, borderThickness);

            Rect inner = cardRect.ContractedBy(7f, 6f);
            Color iconTint = RitualPanelStyle.FamilyColor(def.family);
            if (tierLocked)
            {
                iconTint = new Color(iconTint.r, iconTint.g, iconTint.b, 0.45f);
            }

            RitualPanelStyle.DrawIcon(new Rect(inner.x, inner.y + 1f, 14f, 14f),
                RitualPanelStyle.FamilyIcon(def.family), iconTint);

            // Right-to-left card badges: running, collecting/missing materials, auto-repeat.
            float badgeRight = inner.xMax;
            if (isRunning)
            {
                badgeRight -= DrawCardBadge(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeRunning".Translate(),
                    RitualPanelStyle.BadgeRunningBg, RitualPanelStyle.BadgeRunningText) + 3f;
            }
            else if (isMissingMat)
            {
                badgeRight -= DrawCardBadge(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeMissingMat".Translate(),
                    RitualPanelStyle.BadgeMissingBg, RitualPanelStyle.BadgeMissingText) + 3f;
            }
            else if (isCollecting)
            {
                badgeRight -= DrawCardBadge(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeCollecting".Translate(),
                    RitualPanelStyle.BadgeCollectingBg, RitualPanelStyle.BadgeCollectingText) + 3f;
            }

            if (isAuto && badgeRight - (inner.x + 18f) >= 28f)
            {
                DrawCardBadge(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeAuto".Translate(),
                    RitualPanelStyle.BadgeAutoBg, RitualPanelStyle.BadgeAutoText);
            }

            Rect titleRect = new Rect(inner.x, inner.y + 18f, inner.width, inner.height - 18f);
            Color titleColor = isSelected ? RitualPanelStyle.TitleCream
                : tierLocked ? RitualPanelStyle.TextMuted
                : RitualPanelStyle.TextMain;
            RitualPanelStyle.DrawLabel(titleRect, def.LabelCap, GameFont.Small, titleColor,
                TextAnchor.UpperLeft, ellipsis: false);

            if (tierLocked)
            {
                string reqAltar = AltarDefForTier(def.tier)?.LabelCap ?? ("T" + def.tier);
                TooltipHandler.TipRegion(cardRect,
                    string.Format("WotF_RitualPanel_TierLockedTip".Translate(), reqAltar, def.tier));
            }
            else if (isMissingMat)
            {
                TooltipHandler.TipRegion(cardRect, "WotF_RitualPanel_MissingMatTip".Translate());
            }

            if (Widgets.ButtonInvisible(cardRect, doMouseoverSound: true))
            {
                ClosePopover();
                SelectRitual(def);
            }
        }

        private static float DrawCardBadge(float rightX, float y, string text, Color bg, Color textColor)
        {
            float width = Mathf.Max(26f, RitualPanelStyle.CalcTextWidth(text, GameFont.Tiny) + 8f);
            Rect rect = new Rect(rightX - width, y, width, 16f);
            Widgets.DrawBoxSolid(rect, bg);
            RitualPanelStyle.DrawLabel(rect, text, GameFont.Tiny, textColor,
                TextAnchor.MiddleCenter, ellipsis: false);
            return width;
        }

        private void DrawLeftFooter(Rect rect)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), RitualPanelStyle.SepColor);

            float rowH = 20f;
            Rect row1 = new Rect(rect.x, rect.y + 4f, rect.width, rowH);
            Rect row2 = new Rect(rect.x, row1.yMax + 2f, rect.width, rowH);

            RitualPanelStyle.DrawLabel(row1, "WotF_RitualPanel_AltarTierLabel".Translate(),
                GameFont.Small, RitualPanelStyle.TextSubtle, TextAnchor.MiddleLeft);
            string altarValue = string.Format("WotF_RitualPanel_AltarTierValue".Translate(),
                altar.parent.def.LabelCap, altar.Props.tier);
            RitualPanelStyle.DrawLabel(row1, altarValue, GameFont.Small, RitualPanelStyle.TextMain,
                TextAnchor.MiddleRight);

            RitualPanelStyle.DrawLabel(row2, "WotF_RitualPanel_ResonanceLabel".Translate(),
                GameFont.Small, RitualPanelStyle.TextSubtle, TextAnchor.MiddleLeft);
            string resValue = string.Format("WotF_RitualPanel_ResonanceValue".Translate(),
                altar.ResonanceStrength.ToString("0.#"), altar.ResonanceMultiplier.ToString("0.00"));
            Color resColor = altar.ResonanceStrength > 0f ? RitualPanelStyle.ManaBlue : RitualPanelStyle.TextSubtle;
            RitualPanelStyle.DrawLabel(row2, resValue, GameFont.Small, resColor, TextAnchor.MiddleRight);
        }
    }
}
