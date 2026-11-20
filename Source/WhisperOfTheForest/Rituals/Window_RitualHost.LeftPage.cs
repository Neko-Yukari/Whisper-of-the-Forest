using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    public sealed partial class Window_RitualHost
    {
        private void DrawLeftPage(Rect pageRect)
        {
            Rect inner = pageRect.ContractedBy(22f, 34f);

            const float footerHeight = 52f;
            Rect footerRect = new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight);
            Rect scrollOuter = new Rect(inner.x, inner.y, inner.width, footerRect.y - inner.y - 8f);

            DrawLeftCatalog(scrollOuter);
            DrawLeftFooter(footerRect);
        }

        private void DrawLeftCatalog(Rect outerRect)
        {
            if (researchedRituals.Count == 0)
            {
                RitualPanelStyle.DrawWrappedLabel(
                    outerRect,
                    "WotF_RitualPanel_NoResearched".Translate(),
                    GameFont.Small,
                    RitualPanelStyle.InkMuted,
                    TextAnchor.MiddleCenter);
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
                    y += 12f;
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
            const float gap = 8f;
            const float cardH = 70f;
            int columns = width >= 350f ? 2 : 1;

            string title = string.Format("WotF_RitualPanel_TierHeader".Translate(), tier);
            bool tierTooHigh = tier > altar.Props.tier;
            string reqText = tierTooHigh
                ? " · " + (AltarDefForTier(tier)?.LabelCap ?? ("T" + tier))
                : string.Empty;

            float titleW = RitualPanelStyle.CalcTextWidth(title, GameFont.Small);
            RitualPanelStyle.DrawLabel(
                new Rect(0f, startY, titleW + 4f, headerH),
                title,
                GameFont.Small,
                tierTooHigh ? RitualPanelStyle.InkMuted : RitualPanelStyle.BotanicalJade,
                TextAnchor.MiddleLeft,
                ellipsis: false);

            float ruleStart = titleW + 8f;
            if (!reqText.NullOrEmpty())
            {
                float reqW = RitualPanelStyle.CalcTextWidth(reqText, GameFont.Tiny);
                RitualPanelStyle.DrawLabel(
                    new Rect(titleW + 4f, startY, reqW + 4f, headerH),
                    reqText,
                    GameFont.Tiny,
                    RitualPanelStyle.InkMuted,
                    TextAnchor.MiddleLeft,
                    ellipsis: false);
                ruleStart = titleW + reqW + 10f;
            }

            if (ruleStart < width - 8f)
            {
                RitualPanelStyle.DrawHairline(ruleStart, startY + headerH * 0.5f, width - ruleStart);
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

            Color vellumTint;
            if (isRunning)
            {
                vellumTint = isSelected ? RitualPanelStyle.CardVellumRunSel : RitualPanelStyle.CardVellumRun;
            }
            else if (tierLocked)
            {
                vellumTint = isSelected ? RitualPanelStyle.CardVellumLockedSel : RitualPanelStyle.CardVellumLocked;
            }
            else if (isSelected)
            {
                vellumTint = RitualPanelStyle.CardVellumSel;
            }
            else
            {
                vellumTint = hover ? RitualPanelStyle.CardVellumHover : RitualPanelStyle.CardVellumNormal;
            }

            RitualPanelStyle.DrawPanel(cardRect, vellumTint);

            if (isSelected)
            {
                RitualPanelStyle.DrawBox(cardRect, Color.clear, RitualPanelStyle.GoldAccent, 1);
            }

            // Semantic left rubrication bar
            Color accentColor = isRunning ? RitualPanelStyle.BotanicalJade
                : isSelected ? RitualPanelStyle.GoldAccent
                : isCollecting ? RitualPanelStyle.AmberAccent
                : isMissingMat ? RitualPanelStyle.RubricRed
                : tierLocked ? new Color(RitualPanelStyle.InkMuted.r, RitualPanelStyle.InkMuted.g, RitualPanelStyle.InkMuted.b, 0.45f)
                : new Color(RitualPanelStyle.InkSecondary.r, RitualPanelStyle.InkSecondary.g, RitualPanelStyle.InkSecondary.b, 0.35f);
            Widgets.DrawBoxSolid(new Rect(cardRect.x + 2f, cardRect.y + 4f, 3f, cardRect.height - 8f), accentColor);

            Rect inner = new Rect(cardRect.x + 10f, cardRect.y + 5f, cardRect.width - 16f, cardRect.height - 10f);

            // Row 1: Family crest on left, non-overlapping status seals on right
            Color iconTint = RitualPanelStyle.FamilyColor(def.family);
            if (tierLocked)
            {
                iconTint = new Color(iconTint.r, iconTint.g, iconTint.b, 0.45f);
            }

            RitualPanelStyle.DrawIcon(new Rect(inner.x, inner.y + 1f, 15f, 15f),
                RitualPanelStyle.FamilyIcon(def.family), iconTint);

            float badgeRight = inner.xMax;
            if (isRunning)
            {
                badgeRight -= DrawCardSeal(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeRunning".Translate(),
                    RitualPanelStyle.SealRunningBg) + 3f;
            }
            else if (isMissingMat)
            {
                badgeRight -= DrawCardSeal(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeMissingMat".Translate(),
                    RitualPanelStyle.SealMissingBg) + 3f;
            }
            else if (isCollecting)
            {
                badgeRight -= DrawCardSeal(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeCollecting".Translate(),
                    RitualPanelStyle.SealCollectingBg) + 3f;
            }

            if (isAuto && badgeRight - (inner.x + 18f) >= 28f)
            {
                DrawCardSeal(badgeRight, inner.y,
                    "WotF_RitualPanel_BadgeAuto".Translate(),
                    RitualPanelStyle.SealAutoBg);
            }

            // Row 2: Ritual title (42f height fits 2 wrapped GameFont.Small lines cleanly)
            Rect titleRect = new Rect(inner.x, inner.y + 18f, inner.width, inner.height - 18f);
            Color titleColor = tierLocked ? RitualPanelStyle.InkMuted : RitualPanelStyle.InkPrimary;
            RitualPanelStyle.DrawWrappedLabel(titleRect, def.LabelCap, GameFont.Small, titleColor, TextAnchor.UpperLeft);

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

        private static float DrawCardSeal(float rightX, float y, string text, Color bg)
        {
            float width = Mathf.Max(26f, Mathf.Ceil(RitualPanelStyle.CalcTextWidth(text, GameFont.Tiny)) + 10f);
            Rect rect = new Rect(rightX - width, y, width, 16f);
            RitualPanelStyle.DrawBox(rect, bg, RitualPanelStyle.Darken(bg, 0.25f), 1);
            RitualPanelStyle.DrawLabel(rect, text, GameFont.Tiny, RitualPanelStyle.SealText,
                TextAnchor.MiddleCenter, ellipsis: false);
            return width;
        }

        private void DrawLeftFooter(Rect rect)
        {
            RitualPanelStyle.DrawPanel(rect, RitualPanelStyle.MetricsVellumBg);
            Rect inner = rect.ContractedBy(10f, 5f);

            float rowH = 20f;
            Rect row1 = new Rect(inner.x, inner.y, inner.width, rowH);
            Rect row2 = new Rect(inner.x, row1.yMax + 2f, inner.width, rowH);

            RitualPanelStyle.DrawLabel(row1, "WotF_RitualPanel_AltarTierLabel".Translate(),
                GameFont.Small, RitualPanelStyle.InkSecondary, TextAnchor.MiddleLeft, ellipsis: false);
            string altarValue = string.Format("WotF_RitualPanel_AltarTierValue".Translate(),
                altar.parent.def.LabelCap, altar.Props.tier);
            RitualPanelStyle.DrawLabel(row1, altarValue, GameFont.Small, RitualPanelStyle.InkPrimary,
                TextAnchor.MiddleRight, ellipsis: true);

            Color resColor = altar.ResonanceStrength > 0f ? RitualPanelStyle.CrystalBlue : RitualPanelStyle.InkMuted;
            RitualPanelStyle.DrawIcon(new Rect(row2.x, row2.y + 3f, 14f, 14f),
                RitualPanelStyle.ManaCrystal, resColor);
            RitualPanelStyle.DrawLabel(new Rect(row2.x + 18f, row2.y, row2.width * 0.5f, row2.height),
                "WotF_RitualPanel_ResonanceLabel".Translate(),
                GameFont.Small, RitualPanelStyle.InkSecondary, TextAnchor.MiddleLeft, ellipsis: false);
            string resValue = string.Format("WotF_RitualPanel_ResonanceValue".Translate(),
                altar.ResonanceStrength.ToString("0.#"), altar.ResonanceMultiplier.ToString("0.00"));
            RitualPanelStyle.DrawLabel(row2, resValue, GameFont.Small, resColor,
                TextAnchor.MiddleRight, ellipsis: false);
        }
    }
}
