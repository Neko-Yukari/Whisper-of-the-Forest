using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// 【主持仪式…】 on one page: which rite, which rune or teleport node, the circle (with the cells
    /// still missing, also outlined on the map), the offerings and network mana, who hosts, who helps
    /// and the resonance that comes out of it. The bottom button is enabled only when the same
    /// validation the altar runs would pass, so the panel can never offer a start the altar refuses.
    ///
    /// Nothing is spent here. [准备] has haulers carry the offerings into the altar; the rite can begin
    /// once they are all inside, and the mana and the stacks are taken when the chant completes
    /// (技术/04-仪式实现.md:116).
    /// </summary>
    public sealed class Window_RitualHost : Window
    {
        private const float ContentWidth = 580f;

        private readonly CompRitualAltar altar;
        private readonly HashSet<Pawn> helpers = new HashSet<Pawn>();
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
        private float contentBottom;
        private float viewHeight = 900f;
        private Vector2 scroll;

        public Window_RitualHost(CompRitualAltar altar)
        {
            this.altar = altar;
            selected = altar.SelectedDef;
            selectedRune = altar.SelectedRune;
            selectedNode = altar.SelectedTeleportTarget;
            selectedRuin = altar.SelectedRuinOption;
            selectedEquipment = altar.SelectedEquipment;
            selectedBookSlot = altar.SelectedBookSlot;

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

        public override Vector2 InitialSize
        {
            get
            {
                float height = Mathf.Clamp(UI.screenHeight - 80f, 560f, 780f);
                return new Vector2(660f, height);
            }
        }

        private void SyncSelection()
        {
            altar.SetSelectedRitual(selected, selectedRune, selectedNode, selectedEquipment,
                selectedBookSlot, selectedRuin?.quest);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (altar == null || altar.parent == null || !altar.parent.Spawned)
            {
                Close();
                return;
            }

            Rect footer = new Rect(inRect.x, inRect.yMax - 34f, inRect.width, 34f);
            Rect body = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 42f);

            if (altar.IsActive)
            {
                DrawRunning(body);
                DrawFooter(footer);
                return;
            }

            RitualAssessment found = Assessment();
            if (found.MissingCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(found.MissingCells);
            }

            Widgets.BeginScrollView(body, ref scroll, new Rect(0f, 0f, body.width - 16f, viewHeight));
            float y = 0f;
            y = DrawPicker(y);
            if (selected != null)
            {
                y = DrawFormation(y, found);
                y = DrawCosts(y, found);
                y = DrawRoster(y, found);
                y = DrawProblems(y, found);
            }
            contentBottom = y;
            Widgets.EndScrollView();
            viewHeight = Mathf.Max(400f, contentBottom + 24f);
            DrawFooter(footer);
        }

        private void DrawRunning(Rect rect)
        {
            float y = rect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), "WotF_Ritual_InProgress".Translate(altar.ActiveLabel));
            Text.Font = GameFont.Small;
            y += 34f;

            y = Line(rect.x, y, "WotF_Ritual_Participants".Translate(altar.ActiveParticipants));
            y = Line(rect.x, y, altar.Phase == RitualPhase.Gathering
                ? "WotF_Ritual_InspectGathering".Translate()
                : "WotF_Ritual_InspectChanting".Translate());
            y = Line(rect.x, y, string.Format("WotF_Ritual_Power".Translate(), altar.ActivePower.ToString("0.#")));
            Line(rect.x, y, "WotF_Ritual_RunningFeeNote".Translate());
        }

        private static float Line(float x, float y, string text)
        {
            Widgets.Label(new Rect(x, y, ContentWidth, 22f), text);
            return y + 24f;
        }

        private static float Header(float y, string text)
        {
            GUI.color = new Color(0.72f, 0.78f, 0.68f);
            Widgets.Label(new Rect(0f, y, ContentWidth, 24f), text);
            GUI.color = Color.white;
            return y + 26f;
        }

        private float DrawPicker(float y)
        {
            y = Header(y, "WotF_Ritual_ChooseRitual".Translate());
            bool anyRitual = false;
            foreach (RitualDef def in altar.Rituals)
            {
                anyRitual = true;
                if (Widgets.RadioButton(new Vector2(0f, y + 2f), def == selected) && selected != def)
                {
                    selected = def;
                    selectedRune = null;
                    selectedNode = null;
                    selectedRuin = null;
                    selectedEquipment = null;
                    selectedBookSlot = -1;
                    SyncSelection();
                    dirty = true;
                }

                Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f), RitualRowLabel(def));
                y += 26f;
            }

            if (!anyRitual)
            {
                return Line(0f, y, "WotF_Ritual_NoneAvailable".Translate());
            }

            if (selected == null)
            {
                return Line(0f, y, "WotF_Ritual_NotSelected".Translate());
            }

            if (selected.chooseRune)
            {
                y += 6f;
                y = Header(y, "WotF_Ritual_ChooseRune".Translate());
                bool anyRune = false;
                foreach (SpellGlyphDef glyph in DefDatabase<SpellGlyphDef>.AllDefsListForReading)
                {
                    if (glyph.kind == SpellGlyphKind.Super || !glyph.requiresUnlock
                        || glyph.tier > altar.Props.tier || GameComponent_Spellbook.IsUnlockedInData(glyph))
                    {
                        continue;
                    }

                    anyRune = true;
                    if (Widgets.RadioButton(new Vector2(0f, y + 2f), glyph == selectedRune) && selectedRune != glyph)
                    {
                        selectedRune = glyph;
                        SyncSelection();
                        dirty = true;
                    }

                    Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f), RuneLabel(glyph));
                    y += 26f;
                }

                if (!anyRune)
                {
                    y = Line(0f, y, "WotF_Ritual_AllRunesUnlocked".Translate());
                }
            }

            if (selected.chooseTeleportTarget)
            {
                y += 6f;
                y = Header(y, "WotF_Ritual_ChooseGate".Translate());
                bool anyGate = false;
                foreach (Building building in altar.parent.Map.listerBuildings.allBuildingsColonist)
                {
                    CompTeleportGate gate = building.TryGetComp<CompTeleportGate>();
                    if (gate == null || gate.Ruined)
                    {
                        continue;
                    }

                    anyGate = true;
                    bool usable = gate.Running;
                    if (Widgets.RadioButton(new Vector2(0f, y + 2f), building == selectedNode, !usable)
                        && usable && selectedNode != building)
                    {
                        selectedNode = building;
                        SyncSelection();
                        dirty = true;
                    }

                    GUI.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                    Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f),
                        building.LabelCap + (usable ? string.Empty : " — " + gate.StateText()));
                    GUI.color = Color.white;
                    y += 26f;
                }

                if (!anyGate)
                {
                    y = Line(0f, y, "WotF_Ritual_Teleport_NoGate".Translate());
                }
            }

            if (selected.chooseRuinType)
            {
                y += 6f;
                y = Header(y, "WotF_Ritual_RuinSeek_Choose".Translate());
                for (int i = 0; i < selected.ruinOptions.Count; i++)
                {
                    RuinSeekOption option = selected.ruinOptions[i];
                    if (Widgets.RadioButton(new Vector2(0f, y + 2f), option == selectedRuin) && selectedRuin != option)
                    {
                        selectedRuin = option;
                        SyncSelection();
                        dirty = true;
                    }

                    var row = new Rect(28f, y, ContentWidth - 28f, 24f);
                    Widgets.Label(row, option.sitePart.LabelCap);
                    TooltipHandler.TipRegion(row, option.sitePart.description);
                    y += 26f;
                }
            }

            if (selected.chooseEquipment)
            {
                y = DrawEquipmentPicker(y);
            }
            return y;
        }

        /// <summary>The item beside the altar, then which page of the host's book goes into it.</summary>
        private float DrawEquipmentPicker(float y)
        {
            y += 6f;
            y = Header(y, "WotF_Ritual_ChooseEquipment".Translate());
            List<Thing> items = altar.EnchantableNearby(selected);
            for (int i = 0; i < items.Count; i++)
            {
                Thing item = items[i];
                if (Widgets.RadioButton(new Vector2(0f, y + 2f), item == selectedEquipment) && selectedEquipment != item)
                {
                    selectedEquipment = item;
                    SyncSelection();
                    dirty = true;
                }

                string held = item.TryGetComp<Equipment.CompEnchantedEquipment>().CompInspectStringExtra();
                Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f), item.LabelCap + "  ·  " + held);
                y += 26f;
            }

            if (items.Count == 0)
            {
                y = Line(0f, y, "WotF_Ritual_NoEnchantable".Translate());
            }

            y += 6f;
            y = Header(y, "WotF_Ritual_ChooseBookSlot".Translate());
            if (selectedHost == null)
            {
                return Line(0f, y, "WotF_Ritual_HostFirst".Translate());
            }

            if (SpellService.OpenSlotCount(selectedHost) == 0)
            {
                return Line(0f, y, "WotF_Ritual_HostNoBook".Translate());
            }

            bool anyPage = false;
            for (int slot = 0; slot < GameComponent_Spellbook.SlotCount; slot++)
            {
                SpellRecipe recipe = GameComponent_Spellbook.Current.GetSlot(selectedHost, slot);
                if (recipe == null)
                {
                    continue;
                }

                anyPage = true;
                bool disabled = !Equipment.CompEnchantedEquipment.AcceptsRecipe(recipe, out string reason);
                if (Widgets.RadioButton(new Vector2(0f, y + 2f), slot == selectedBookSlot, disabled)
                    && !disabled && selectedBookSlot != slot)
                {
                    selectedBookSlot = slot;
                    SyncSelection();
                    dirty = true;
                }

                GUI.color = disabled ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f),
                    (slot + 1) + ". " + recipe.name + (disabled ? " — " + reason : ""));
                GUI.color = Color.white;
                y += 26f;
            }

            if (!anyPage)
            {
                y = Line(0f, y, "WotF_Equipment.Gizmo.NoSavedSpell".Translate());
            }

            return y;
        }

        private float DrawFormation(float y, RitualAssessment found)
        {
            y += 10f;
            y = Header(y, "WotF_Ritual_SectionFormation".Translate());
            y = Line(0f, y, "WotF_Ritual_CodexRing".Translate(found.RingSize,
                ("WotF_Ritual_Chalk_" + found.ChalkColor).Translate()));
            y = Line(0f, y, found.RingComplete
                ? "WotF_Ritual_RingComplete".Translate()
                : "WotF_Ritual_RingIncomplete".Translate(found.MissingCells.Count));

            for (int i = 0; i < found.MissingCells.Count && i < 8; i++)
            {
                IntVec3 delta = found.MissingCells[i] - altar.parent.Position;
                y = Line(0f, y, "WotF_Ritual_MissingCell".Translate(delta.x, delta.z));
            }

            if (found.MissingCells.Count > 8)
            {
                y = Line(0f, y, "WotF_Ritual_MoreCells".Translate(found.MissingCells.Count - 8));
            }

            return y;
        }

        private float DrawCosts(float y, RitualAssessment found)
        {
            y += 10f;
            y = Header(y, "WotF_Ritual_SectionCost".Translate());
            y = Line(0f, y, found.ManaEnough
                ? "WotF_Ritual_ManaHave".Translate(found.ManaCost.ToString("0.#"), found.ManaStored.ToString("0.#"))
                : "WotF_Ritual_ManaShort".Translate(found.ManaCost.ToString("0.#"), found.ManaStored.ToString("0.#")));

            if (altar.CollectDef != null)
            {
                y = Line(0f, y, "WotF_Ritual_Collecting".Translate(altar.CollectLabel));
            }

            if (found.OfferingLines.Count == 0)
            {
                y = Line(0f, y, "WotF_Ritual_NoOfferings".Translate());
            }

            for (int i = 0; i < found.OfferingLines.Count; i++)
            {
                OfferingLine line = found.OfferingLines[i];
                GUI.color = line.Missing > 0 ? new Color(1f, 0.6f, 0.55f) : Color.white;
                y = Line(0f, y, "WotF_Ritual_Offering".Translate(line.Cost.Label, line.Delivered, line.Cost.count));
                GUI.color = Color.white;
            }

            return Line(0f, y, "WotF_Ritual_FeeAtEnd".Translate());
        }

        private float DrawRoster(float y, RitualAssessment found)
        {
            y += 10f;
            y = Header(y, "WotF_Ritual_SectionHost".Translate());
            List<Pawn> bloodline = BloodlinePawns();
            for (int i = 0; i < bloodline.Count; i++)
            {
                Pawn pawn = bloodline[i];
                string reason = Verdict(pawn);
                bool disabled = reason != null;
                if (Widgets.RadioButton(new Vector2(0f, y + 2f), pawn == selectedHost, disabled)
                    && !disabled && selectedHost != pawn)
                {
                    selectedHost = pawn;
                    selectedBookSlot = -1;
                    helpers.Remove(pawn);
                    SyncSelection();
                    dirty = true;
                }

                GUI.color = disabled ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f),
                    PawnLabel(pawn) + (disabled ? " — " + reason : ""));
                GUI.color = Color.white;
                y += 26f;
            }

            if (bloodline.Count == 0)
            {
                y = Line(0f, y, "WotF_Ritual_NoCaster".Translate());
            }

            y += 8f;
            y = Header(y, "WotF_Ritual_SectionHelpers".Translate());
            for (int i = 0; i < bloodline.Count; i++)
            {
                Pawn pawn = bloodline[i];
                if (pawn == selectedHost)
                {
                    continue;
                }

                string reason = Verdict(pawn);
                bool disabled = reason != null;
                bool on = helpers.Contains(pawn);
                Widgets.Checkbox(new Vector2(0f, y + 2f), ref on, 24f, disabled);
                if (!disabled && on != helpers.Contains(pawn))
                {
                    if (on)
                    {
                        helpers.Add(pawn);
                    }
                    else
                    {
                        helpers.Remove(pawn);
                    }

                    dirty = true;
                }

                GUI.color = disabled ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                Widgets.Label(new Rect(28f, y, ContentWidth - 28f, 24f),
                    PawnLabel(pawn) + (disabled ? " — " + reason : ""));
                GUI.color = Color.white;
                y += 26f;
            }

            y += 6f;
            y = Line(0f, y, "WotF_Ritual_Participants".Translate(found.Participants));
            if (selected != null && selected.minParticipants > 0)
            {
                y = Line(0f, y, "WotF_Ritual_MinParticipants".Translate(selected.minParticipants));
            }

            y = Line(0f, y, "WotF_Ritual_Resonance".Translate(
                found.BloodlinePower.ToString("0.#"), found.FacilityPower.ToString("0.#"),
                found.Power.ToString("0.#")));
            y = Line(0f, y, selected != null && selected.UsesPower
                ? "WotF_Ritual_PowerScaled".Translate()
                : "WotF_Ritual_PowerFixed".Translate());
            return Line(0f, y, "WotF_Ritual_ParticipantsHint".Translate());
        }

        private static float DrawProblems(float y, RitualAssessment found)
        {
            if (found.CanStart)
            {
                return y;
            }

            y += 10f;
            y = Header(y, "WotF_Ritual_SectionBlocked".Translate());
            for (int i = 0; i < found.Problems.Count; i++)
            {
                GUI.color = new Color(1f, 0.6f, 0.55f);
                y = Line(0f, y, found.Problems[i]);
                GUI.color = Color.white;
            }

            return y;
        }

        private void DrawFooter(Rect rect)
        {
            if (altar.IsActive)
            {
                if (Widgets.ButtonText(new Rect(rect.x, rect.y, 180f, 32f), "WotF_Ritual_CancelButton".Translate()))
                {
                    altar.CancelByPlayer();
                }
            }
            else
            {
                RitualAssessment found = Assessment();
                bool oldEnabled = GUI.enabled;
                GUI.enabled = altar.CanPrepare(selected, selectedRune);
                if (Widgets.ButtonText(new Rect(rect.x, rect.y, 140f, 32f), "WotF_Ritual_Prepare".Translate()))
                {
                    altar.Prepare(selected, selectedRune);
                    dirty = true;
                }

                var begin = new Rect(rect.x + 148f, rect.y, 140f, 32f);
                GUI.enabled = found.CanStart;
                if (Widgets.ButtonText(begin, "WotF_Ritual_Begin".Translate()))
                {
                    altar.TryStart(BuildRequest());
                }

                GUI.enabled = altar.CollectDef != null;
                if (Widgets.ButtonText(new Rect(rect.x + 296f, rect.y, 140f, 32f), "WotF_Ritual_CancelPrepare".Translate()))
                {
                    altar.CancelByPlayer();
                    dirty = true;
                }

                GUI.enabled = oldEnabled;
                if (!found.CanStart && found.FirstProblem != null)
                {
                    TooltipHandler.TipRegion(begin, found.FirstProblem);
                }
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - 120f, rect.y, 120f, 32f), "CloseButton".Translate()))
            {
                Close();
            }
        }

        private RitualAssessment Assessment()
        {
            int now = Find.TickManager.TicksGame;
            if (assessment == null || dirty || now - assessedTick > 15)
            {
                assessment = altar.Assess(BuildRequest());
                assessedTick = now;
                dirty = false;
            }

            return assessment;
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

            list.Sort(delegate(Pawn a, Pawn b)
            {
                return string.Compare(a.LabelShort, b.LabelShort, StringComparison.Ordinal);
            });
            request.Helpers.AddRange(list);
            return request;
        }

        private List<Pawn> BloodlinePawns()
        {
            var pawns = new List<Pawn>();
            foreach (Pawn pawn in altar.parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (Gene_ForestBloodline.Get(pawn) != null)
                {
                    pawns.Add(pawn);
                }
            }

            pawns.Sort(delegate(Pawn a, Pawn b)
            {
                return string.Compare(a.LabelShort, b.LabelShort, StringComparison.Ordinal);
            });
            return pawns;
        }

        private static string PawnLabel(Pawn pawn)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(pawn);
            return pawn.LabelShortCap + " (" + gene.Level + ")";
        }

        private static string Verdict(Pawn pawn)
        {
            return CompRitualAltar.IsEligibleRitualCaster(pawn, out string reason) ? null : reason;
        }

        private static string RitualRowLabel(RitualDef def)
        {
            return def.LabelCap + "  ·  " + "WotF_Ritual_TierShort".Translate(def.tier) + "  ·  "
                + "WotF_Ritual_RingShort".Translate(def.ringSize, ("WotF_Ritual_Chalk_" + def.chalkColor).Translate());
        }

        private static string RuneLabel(SpellGlyphDef glyph)
        {
            RitualDef awakening = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_RuneAwakening");
            RuneAwakeningTier tier = awakening?.RuneTierFor(glyph.tier);
            if (tier == null)
            {
                return glyph.LabelCap;
            }

            return glyph.LabelCap + " — " + "WotF_Ritual_RuneRequirements".Translate(tier.tier, tier.ringSize,
                tier.manaCost.ToString("0.#"), tier.durationTicks);
        }
    }
}
