using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// 《精灵仪式图鉴》: the left column lists every rite by family, the right column describes the
    /// selected one and draws the family's 3x3 / 5x5 / 7x7 chalk circles, marking the cells a higher
    /// tier has to add.
    ///
    /// Every number comes from RitualDef and its runeTiers - tier, ring size, chalk colour, mana,
    /// work, offerings - so adding a rite to XML adds it to this window. The family cell patterns are
    /// still undecided, so the circles are drawn as the placeholder the brief asks for: concentric
    /// square frames in the chalk colour the Defs already declare.
    ///
    /// The window never pauses and never captures input outside itself, so the player can keep the
    /// map and the book side by side, like the 萌螈 help window it is modelled on.
    /// </summary>
    public sealed class Window_RitualCodex : Window
    {
        private static readonly RitualFamily[] FamilyOrder =
        {
            RitualFamily.Crafting,
            RitualFamily.Life,
            RitualFamily.Domain
        };

        private readonly CompRitualAltar altar;
        private readonly List<RitualDef> allRituals = new List<RitualDef>();
        private RitualDef selected;
        private Vector2 listScroll;
        private Vector2 detailScroll;

        public Window_RitualCodex(CompRitualAltar altar)
        {
            this.altar = altar;

            foreach (RitualDef def in DefDatabase<RitualDef>.AllDefsListForReading)
            {
                if (def.family != RitualFamily.Unspecified)
                {
                    allRituals.Add(def);
                }
            }

            allRituals.Sort(delegate(RitualDef a, RitualDef b)
            {
                int family = Array.IndexOf(FamilyOrder, a.family).CompareTo(Array.IndexOf(FamilyOrder, b.family));
                if (family != 0) return family;
                if (a.tier != b.tier) return a.tier.CompareTo(b.tier);
                return string.Compare(a.label, b.label, StringComparison.Ordinal);
            });

            if (allRituals.Count > 0)
            {
                selected = allRituals[0];
            }

            draggable = true;
            forcePause = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            onlyOneOfTypeAllowed = false;
            closeOnCancel = true;
            doCloseX = true;
            doCloseButton = false;
        }

        public override Vector2 InitialSize => new Vector2(860f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            if (altar == null || altar.parent == null || !altar.parent.Spawned)
            {
                Close();
                return;
            }

            const float listWidth = 250f;
            Rect listRect = new Rect(inRect.x, inRect.y, listWidth, inRect.height);
            Rect detailRect = new Rect(listRect.xMax + 14f, inRect.y, inRect.width - listWidth - 14f, inRect.height);

            Widgets.DrawMenuSection(listRect);
            DrawList(listRect.ContractedBy(8f));
            DrawDetail(detailRect);
        }

        private void DrawList(Rect rect)
        {
            int rows = 0;
            for (int i = 0; i < FamilyOrder.Length; i++)
            {
                rows += 1 + CountInFamily(FamilyOrder[i]);
            }

            Rect view = new Rect(0f, 0f, rect.width - 16f, rows * 26f + 8f);
            Widgets.BeginScrollView(rect, ref listScroll, view);
            float y = 0f;
            for (int f = 0; f < FamilyOrder.Length; f++)
            {
                RitualFamily family = FamilyOrder[f];
                GUI.color = new Color(0.72f, 0.78f, 0.68f);
                Widgets.Label(new Rect(0f, y, view.width, 24f), ("WotF_Ritual_Family_" + family).Translate());
                GUI.color = Color.white;
                y += 26f;

                for (int i = 0; i < allRituals.Count; i++)
                {
                    RitualDef def = allRituals[i];
                    if (def.family != family) continue;

                    Rect row = new Rect(0f, y, view.width, 24f);
                    bool available = altar != null && def.tier <= altar.Props.tier;
                    if (def == selected) Widgets.DrawHighlightSelected(row);
                    else Widgets.DrawHighlightIfMouseover(row);
                    GUI.color = available ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    if (Widgets.ButtonText(row, "  " + def.LabelCap, drawBackground: false))
                    {
                        selected = def;
                    }

                    GUI.color = Color.white;
                    y += 26f;
                }
            }

            Widgets.EndScrollView();
        }

        private int CountInFamily(RitualFamily family)
        {
            int count = 0;
            for (int i = 0; i < allRituals.Count; i++)
            {
                if (allRituals[i].family == family) count++;
            }

            return count;
        }

        private void DrawDetail(Rect rect)
        {
            if (selected == null)
            {
                Widgets.Label(rect, "WotF_Ritual_CodexEmpty".Translate());
                return;
            }

            RitualDef def = selected;
            float height = 250f;
            Rect view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref detailScroll, view);
            float y = 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 32f), def.LabelCap);
            Text.Font = GameFont.Small;
            y += 36f;

            y = Line(view, y, string.Format("WotF_Ritual_TierFamily".Translate(), def.tier,
                ("WotF_Ritual_Family_" + def.family).Translate()));
            y = Line(view, y, def.UsesEffectStrength
                ? "WotF_Ritual_StrengthScaled".Translate()
                : def.UsesWork ? "WotF_Ritual_StrengthSpeed".Translate() : "WotF_Ritual_StrengthFixed".Translate());
            if (def.UsesWork)
            {
                RuneAwakeningTier runeTier = def.chooseRune ? def.RuneTierFor(1) : null;
                float mana = runeTier != null ? runeTier.manaCost : def.manaCost;
                float work = runeTier != null ? runeTier.workAmount : def.workAmount;
                y = Line(view, y, string.Format("WotF_Ritual_WorkRequirements".Translate(), def.tier, def.ringSize,
                    mana.ToString("0.#"), work.ToString("0.#")));
            }
            else if (def.sustained)
            {
                y = Line(view, y, string.Format("WotF_Ritual_SustainedRequirements".Translate(), def.tier, def.ringSize,
                    def.manaCost.ToString("0.#"), def.manaPerStrike.ToString("0.#"), def.durationTicks));
            }
            else
            {
                y = Line(view, y, string.Format("WotF_Ritual_RuneRequirements".Translate(), def.tier, def.ringSize,
                    def.manaCost.ToString("0.#"), def.durationTicks));
            }
            if (def.minParticipants > 0)
            {
                y = Line(view, y, "WotF_Ritual_MinParticipants".Translate(def.minParticipants));
            }

            y += 4f;
            Widgets.Label(new Rect(0f, y, view.width, 60f), def.description);
            y += 64f;

            float boardSize = Mathf.Min(150f, (view.width - 40f) / 3f);
            for (int size = 3; size <= 7; size += 2)
            {
                Rect board = new Rect((size - 3) / 2 * (boardSize + 20f), y, boardSize, boardSize);
                DrawCircle(board, size, def);
            }

            y += boardSize + 26f;
            DrawLegend(new Rect(0f, y, view.width, 40f));

            Widgets.EndScrollView();
        }

        private static float Line(Rect view, float y, string text)
        {
            Widgets.Label(new Rect(0f, y, view.width, 22f), text);
            return y + 22f;
        }

        /// <summary>
        /// One circle of the selected family. Inner rings keep the colour their own rite declares, so
        /// the picture already shows the upgrade path; the ring of the selected rite is framed and a
        /// larger ring is labelled with the number of cells it still needs.
        /// </summary>
        private void DrawCircle(Rect area, int size, RitualDef selectedDef)
        {
            int radius = (size - 1) / 2;
            float cell = Mathf.Floor(area.width / size);
            float side = cell * size;
            Rect board = new Rect(area.x, area.y, side, side);
            Widgets.DrawBoxSolid(board, new Color(0.10f, 0.11f, 0.10f, 0.65f));

            for (int ring = 1; ring <= radius; ring++)
            {
                ChalkColor? color = ChalkForRing(selectedDef.family, ring * 2 + 1);
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;

                        Rect c = new Rect(board.x + (dx + radius) * cell, board.y + (dz + radius) * cell,
                            cell - 1f, cell - 1f);
                        if (color.HasValue)
                        {
                            Widgets.DrawBoxSolid(c, ChalkColorValue(color.Value));
                        }

                        Widgets.DrawBox(c, 1);
                    }
                }
            }

            int selectedRing = selectedDef.ringSize;
            string caption = "WotF_Ritual_CodexRing".Translate(size, ChalkLabel(selectedDef.family, size));
            if (size == selectedRing)
            {
                Widgets.DrawBox(board, 2);
                caption = caption + " — " + "WotF_Ritual_CodexThisRite".Translate();
            }
            else if (size > selectedRing)
            {
                caption = caption + " — " + "WotF_Ritual_CodexUpgrade".Translate(4 * (size - 1));
            }

            Widgets.Label(new Rect(area.x, board.yMax + 2f, area.width + 16f, 20f), caption);
        }

        private void DrawLegend(Rect rect)
        {
            Widgets.Label(rect, "WotF_Ritual_CodexLegend".Translate());
        }

        private ChalkColor? ChalkForRing(RitualFamily family, int size)
        {
            for (int i = 0; i < allRituals.Count; i++)
            {
                RitualDef def = allRituals[i];
                if (def.family != family) continue;
                if (def.chooseRune)
                {
                    if (def.runeTiers == null) continue;
                    for (int t = 0; t < def.runeTiers.Count; t++)
                    {
                        RuneAwakeningTier tier = def.runeTiers[t];
                        if (tier != null && tier.ringSize == size) return tier.chalkColor;
                    }

                    continue;
                }

                if (def.ringSize == size) return def.chalkColor;
            }

            return null;
        }

        private string ChalkLabel(RitualFamily family, int size)
        {
            ChalkColor? color = ChalkForRing(family, size);
            return color.HasValue
                ? ("WotF_Ritual_Chalk_" + color.Value).Translate()
                : (string)"WotF_Ritual_CodexUndecided".Translate();
        }

        private static Color ChalkColorValue(ChalkColor color)
        {
            ThingDef mark = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_" + color);
            Color value = mark?.graphicData?.color ?? Color.white;
            value.a = 0.85f;
            return value;
        }
    }
}
