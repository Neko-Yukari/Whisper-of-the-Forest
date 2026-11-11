using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    public class CompProperties_RitualAltar : CompProperties
    {
        public float linkRadius = 12.9f;
        public List<string> ritualDefNames = new List<string>();

        public CompProperties_RitualAltar()
        {
            compClass = typeof(CompRitualAltar);
        }
    }

    /// <summary>
    /// The altar owns the active ritual: which Def is running, which pawn performs it, and the mana
    /// already paid. The job driver only performs the time; the effect is applied once here.
    /// </summary>
    public class CompRitualAltar : ThingComp
    {
        private Pawn activePawn;
        private RitualDef activeDef;
        private SpellGlyphDef activeRune;
        private Building activeTeleportTarget;
        private RitualDef autoRitual;
        private int activeParticipants;

        public CompProperties_RitualAltar Props => (CompProperties_RitualAltar)props;

        public bool IsActive => activeDef != null;
        public RitualDef ActiveDef => activeDef;
        public Pawn ActivePawn => activePawn;
        public RitualDef AutoRitual => autoRitual;
        public int ActiveParticipants => activeParticipants;

        public void SetAutoRitual(RitualDef def)
        {
            autoRitual = def != null && def.autoRepeatable ? def : null;
        }

        public IEnumerable<RitualDef> Rituals
        {
            get
            {
                for (int i = 0; i < Props.ritualDefNames.Count; i++)
                {
                    RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail(Props.ritualDefNames[i]);
                    if (def != null)
                    {
                        yield return def;
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref activePawn, "ritualPawn", true);
            Scribe_Defs.Look(ref activeDef, "ritualDef");
            Scribe_Defs.Look(ref activeRune, "ritualRune");
            Scribe_References.Look(ref activeTeleportTarget, "ritualTeleportTarget", true);
            Scribe_Defs.Look(ref autoRitual, "ritualAuto");
            Scribe_Values.Look(ref activeParticipants, "ritualParticipants", 0);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            if (activeDef == null)
            {
                activePawn = null;
                activeRune = null;
                activeTeleportTarget = null;
                activeParticipants = 0;
            }
            else if (activePawn == null || activePawn.Dead || (activeDef.chooseRune && activeRune == null)
                || (activeDef.chooseTeleportTarget && activeTeleportTarget == null))
            {
                activePawn = null;
                activeDef = null;
                activeRune = null;
                activeTeleportTarget = null;
                activeParticipants = 0;
            }
            else if (!activeDef.chooseRune)
            {
                activeRune = null;
            }

            if (activeDef != null && !activeDef.chooseTeleportTarget)
            {
                activeTeleportTarget = null;
            }

            if (autoRitual != null && !autoRitual.autoRepeatable)
            {
                autoRitual = null;
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (activeDef == null)
            {
                if (autoRitual != null)
                {
                    TryStart(autoRitual, null, BestCaster(), null, true);
                }

                return;
            }

            if (activePawn == null || activePawn.Dead || !activePawn.Spawned
                || activePawn.CurJobDef != RitualJobDef)
            {
                CancelRitual(activePawn);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned)
            {
                yield break;
            }

            if (IsActive)
            {
                yield return new Command_Action
                {
                    defaultLabel = "WotF_Ritual_InProgress".Translate(ActiveLabel),
                    Disabled = true
                };
                yield break;
            }

            RitualDef runeRitual = null;
            RitualDef teleportRitual = null;
            yield return AutoRepeatGizmo();
            foreach (RitualDef def in Rituals)
            {
                if (def.chooseRune)
                {
                    runeRitual = def;
                    continue;
                }

                if (def.chooseTeleportTarget)
                {
                    teleportRitual = def;
                    continue;
                }

                RitualDef captured = def;
                yield return new Command_Action
                {
                    defaultLabel = "WotF_Ritual_Start".Translate(captured.LabelCap),
                    defaultDesc = RitualDescription(captured),
                    action = delegate { TryStart(captured); }
                };
            }

            if (runeRitual != null)
            {
                yield return RuneAwakeningGizmo(runeRitual);
            }

            if (teleportRitual != null)
            {
                yield return TeleportGizmo(teleportRitual);
            }
        }

        public override string CompInspectStringExtra()
        {
            string text = IsActive
                ? (string)"WotF_Ritual_InspectActive".Translate(ActiveLabel)
                : (string)"WotF_Ritual_InspectIdle".Translate();
            if (IsActive)
            {
                text += "\n" + "WotF_Ritual_Participants".Translate(activeParticipants);
            }

            return autoRitual == null
                ? text
                : text + "\n" + "WotF_Ritual_AutoRepeat_Current".Translate(autoRitual.LabelCap);
        }

        private Gizmo AutoRepeatGizmo()
        {
            var options = new List<FloatMenuOption>();
            foreach (RitualDef def in Rituals)
            {
                if (!def.autoRepeatable)
                {
                    continue;
                }

                RitualDef captured = def;
                options.Add(new FloatMenuOption(captured.LabelCap, delegate { SetAutoRitual(captured); }));
            }

            if (autoRitual != null)
            {
                options.Add(new FloatMenuOption(
                    "WotF_Ritual_AutoRepeat_Off".Translate(),
                    delegate { SetAutoRitual(null); }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("WotF_Ritual_AutoRepeat_None".Translate(), null));
            }

            return new Command_Action
            {
                defaultLabel = autoRitual == null
                    ? "WotF_Ritual_AutoRepeat".Translate()
                    : "WotF_Ritual_AutoRepeat_Current".Translate(autoRitual.LabelCap),
                defaultDesc = "WotF_Ritual_AutoRepeat_Desc".Translate(),
                action = delegate { Find.WindowStack.Add(new FloatMenu(options)); }
            };
        }

        private static void Reject(bool silent, string text)
        {
            if (!silent)
            {
                Messages.Message(text, MessageTypeDefOf.RejectInput, false);
            }
        }

        public bool TryStart(RitualDef def)
        {
            return TryStart(def, null, BestCaster());
        }

        public bool TryStart(RitualDef def, Pawn caster)
        {
            return TryStart(def, null, caster);
        }

        public bool TryStart(RitualDef def, SpellGlyphDef rune)
        {
            return TryStart(def, rune, BestCaster());
        }

        public bool TryStart(RitualDef def, Building teleportTarget)
        {
            return TryStart(def, null, BestCaster(), teleportTarget);
        }

        public bool TryStart(RitualDef def, SpellGlyphDef rune, Pawn caster, Building teleportTarget = null, bool silent = false)
        {
            if (def == null)
            {
                return false;
            }

            if (def.chooseRune && rune == null)
            {
                Reject(silent, "WotF_Ritual_ChooseRune".Translate());
                return false;
            }

            if (!def.chooseRune && rune != null)
            {
                return false;
            }

            if (def.chooseTeleportTarget && (teleportTarget == null

                || teleportTarget.Map != parent.Map
                || !teleportTarget.Spawned
                || teleportTarget.TryGetComp<CompTeleportNode>() == null))
            {
                Reject(silent, "WotF_Ritual_Teleport_NoNode".Translate());
                return false;
            }

            if (!def.chooseTeleportTarget && teleportTarget != null)
            {
                return false;
            }

            if (rune != null && GameComponent_Spellbook.IsUnlockedInData(rune))
            {
                Reject(silent, "WotF_Ritual_RuneAlreadyUnlocked".Translate(rune.LabelCap));
                return false;
            }

            if (IsActive)
            {
                Reject(silent, "WotF_Ritual_AlreadyRunning".Translate(ActiveLabel));
                return false;
            }

            if (!parent.Spawned)
            {
                return false;
            }

            if (caster == null)
            {
                Reject(silent, "WotF_Ritual_NoCaster".Translate());
                return false;
            }

            if (caster.Drafted)
            {
                Reject(silent, "WotF_Ritual_Drafted".Translate());
                return false;
            }

            if (!SpellService.IsEligibleCaster(caster, out string casterReason))
            {
                Reject(silent, casterReason);
                return false;
            }

            if (!RitualRing.IsComplete(parent.Map, parent.Position, def.ringSize, def.chalkColor, out int missing))
            {
                Reject(silent, "WotF_Ritual_RingIncomplete".Translate(missing));
                return false;
            }

            int participants = CountParticipants(parent.Map, parent.Position, def.ringSize, caster);
            if (participants < def.minParticipants)
            {
                Reject(silent, "WotF_Ritual_NeedsParticipants".Translate(def.minParticipants, participants));
                return false;
            }

            float effectRadius = def.RadiusFor(participants);
            if (def.requiresDeepResources
                && !RitualEffect_LeylineMining.HasDeepResources(parent.Map, parent.Position, effectRadius))
            {
                Reject(silent, "WotF_Ritual_LeylineMining_NoResource".Translate());
                return false;
            }

            ManaNetwork network = MapComponent_ManaNetwork.For(parent.Map)
                ?.NetworkNear(parent.Position, Props.linkRadius);
            if (network == null || network.Stored + 0.0001f < def.manaCost)
            {
                Reject(silent, "WotF_Ritual_NoMana".Translate());
                return false;
            }

            string missingMaterials = MissingMaterials(
                parent.Map, parent.Position, effectRadius, RitualCosts(def, rune));
            if (missingMaterials != null)
            {
                Reject(silent, "WotF_Ritual_NoMaterials".Translate(missingMaterials));
                return false;
            }

            activePawn = caster;
            activeDef = def;
            activeRune = rune;
            activeTeleportTarget = teleportTarget;
            activeParticipants = participants;

            Job job = JobMaker.MakeJob(RitualJobDef, parent);
            if (!caster.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                activePawn = null;
                activeDef = null;
                activeRune = null;
                activeTeleportTarget = null;
                activeParticipants = 0;
                return false;
            }

            network.TryDraw(def.manaCost);
            ConsumeMaterials(parent.Map, parent.Position, effectRadius, RitualCosts(def, rune));

            WotfLog.Info(
                WotfLog.ModuleRitual,
                "started " + def.defName + (rune == null ? "" : " for " + rune.defName)
                + " by " + caster.LabelShort + " at " + parent.Position);
            return true;
        }

        /// <summary>The driver calls this only after the full performance has finished.</summary>
        public void CompleteRitual(Pawn caster)
        {
            if (activeDef == null || activePawn != caster)
            {
                return;
            }

            if (!parent.Spawned)
            {
                CancelRitual(caster);
                return;
            }

            RitualDef def = activeDef;
            SpellGlyphDef rune = activeRune;
            Building teleportTarget = activeTeleportTarget;
            int participants = activeParticipants;
            activePawn = null;
            activeDef = null;
            activeRune = null;
            activeTeleportTarget = null;
            activeParticipants = 0;
            def.EffectWorker.Apply(new RitualContext(parent.Map, (Building)parent, caster, def, rune, teleportTarget, participants));
            WotfLog.Info(
                WotfLog.ModuleRitual,
                "completed " + def.defName + (rune == null ? "" : " for " + rune.defName)
                + " by " + caster.LabelShort);
        }

        public void CancelRitual(Pawn caster)
        {
            if (activeDef == null || (caster != null && activePawn != caster))
            {
                return;
            }

            RitualDef def = activeDef;
            activePawn = null;
            activeDef = null;
            activeRune = null;
            activeTeleportTarget = null;
            activeParticipants = 0;
            WotfLog.Info(WotfLog.ModuleRitual, "cancelled " + def.defName);
            Messages.Message("WotF_Ritual_Cancelled".Translate(def.LabelCap), parent, MessageTypeDefOf.NegativeEvent, false);
        }

        private string ActiveLabel => activeDef.chooseRune && activeRune != null
            ? activeDef.LabelCap + ": " + activeRune.LabelCap
            : activeDef.LabelCap;

        private Gizmo RuneAwakeningGizmo(RitualDef def)
        {
            var options = new List<FloatMenuOption>();
            foreach (SpellGlyphDef glyph in DefDatabase<SpellGlyphDef>.AllDefsListForReading)
            {
                if (!glyph.requiresUnlock || GameComponent_Spellbook.IsUnlockedInData(glyph))
                {
                    continue;
                }

                SpellGlyphDef captured = glyph;
                string label = captured.LabelCap;
                string cost = MaterialLabel(captured.unlockCost);
                if (!string.IsNullOrEmpty(cost))
                {
                    label += " (" + cost + ")";
                }

                options.Add(new FloatMenuOption(label, delegate { TryStart(def, captured); }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("WotF_Ritual_AllRunesUnlocked".Translate(), null));
            }

            return new Command_Action
            {
                defaultLabel = "WotF_Ritual_RuneAwakening".Translate(),
                defaultDesc = "WotF_Ritual_RuneAwakening_Desc".Translate(),
                action = delegate { Find.WindowStack.Add(new FloatMenu(options)); }
            };
        }

        private Gizmo TeleportGizmo(RitualDef def)
        {
            var options = new List<FloatMenuOption>();
            foreach (Building building in parent.Map.listerBuildings.allBuildingsColonist)
            {
                if (building.TryGetComp<CompTeleportNode>() == null)
                {
                    continue;
                }

                Building captured = building;
                options.Add(new FloatMenuOption(captured.LabelCap, delegate { TryStart(def, captured); }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("WotF_Ritual_Teleport_NoNode".Translate(), null));
            }

            return new Command_Action
            {
                defaultLabel = "WotF_Ritual_Start".Translate(def.LabelCap),
                defaultDesc = RitualDescription(def),
                action = delegate { Find.WindowStack.Add(new FloatMenu(options)); }
            };
        }

        private static List<ThingDefCountClass> RitualCosts(RitualDef def, SpellGlyphDef rune)
        {
            var costs = new List<ThingDefCountClass>();
            if (def.inputCosts != null)
            {
                costs.AddRange(def.inputCosts);
            }

            if (rune?.unlockCost != null)
            {
                costs.AddRange(rune.unlockCost);
            }

            return costs;
        }

        private static string MaterialLabel(List<ThingDefCountClass> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return null;
            }

            var parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                parts.Add(costs[i].thingDef.label + " x" + costs[i].count);
            }

            return string.Join(", ", parts);
        }

        private static string MissingMaterials(Map map, IntVec3 center, float radius, List<ThingDefCountClass> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return null;
            }

            var missing = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                int available = CountThings(map, center, radius, costs[i].thingDef);
                if (available < costs[i].count)
                {
                    missing.Add(costs[i].thingDef.label + " x" + (costs[i].count - available));
                }
            }

            return missing.Count == 0 ? null : string.Join(", ", missing);
        }

        private static int CountThings(Map map, IntVec3 center, float radius, ThingDef def)
        {
            int total = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, map, radius, true))
            {
                if (thing.def == def && thing.Spawned && !thing.Destroyed)
                {
                    total += thing.stackCount;
                }
            }

            return total;
        }

        private static void ConsumeMaterials(Map map, IntVec3 center, float radius, List<ThingDefCountClass> costs)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass cost = costs[i];
                int remaining = cost.count;
                var things = new List<Thing>(GenRadial.RadialDistinctThingsAround(center, map, radius, true));
                for (int j = 0; j < things.Count && remaining > 0; j++)
                {
                    Thing thing = things[j];
                    if (thing.def != cost.thingDef || thing.Destroyed)
                    {
                        continue;
                    }

                    int take = Math.Min(remaining, thing.stackCount);
                    if (take <= 0)
                    {
                        continue;
                    }

                    if (take >= thing.stackCount)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                    else
                    {
                        thing.SplitOff(take).Destroy(DestroyMode.Vanish);
                    }

                    remaining -= take;
                }
            }
        }

        private static string RitualDescription(RitualDef def)
        {
            string text = def.description + "\n\n" + "WotF_Ritual_ParticipantsHint".Translate();
            if (def.minParticipants > 0)
            {
                text += "\n" + "WotF_Ritual_MinParticipants".Translate(def.minParticipants);
            }

            return text;
        }

        private static int CountParticipants(Map map, IntVec3 center, int ringSize, Pawn caster)
        {
            int count = 0;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == caster || pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState)
                {
                    continue;
                }

                if (Gene_ForestBloodline.Get(pawn) == null)
                {
                    continue;
                }

                if (RitualRing.IsOnRing(center, ringSize, pawn.Position))
                {
                    count++;
                }
            }

            return count;
        }

        private Pawn BestCaster()
        {
            Pawn best = null;
            float bestDistance = float.MaxValue;
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState)
                {
                    continue;
                }

                if (!SpellService.IsEligibleCaster(pawn, out _))
                {
                    continue;
                }

                float distance = (pawn.Position - parent.Position).LengthHorizontalSquared;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pawn;
                }
            }

            return best;
        }

        private static JobDef RitualJobDef => DefDatabase<JobDef>.GetNamed("WotF_PerformRitual");
    }
}
