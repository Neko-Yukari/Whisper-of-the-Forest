using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Missiles;
using WhisperOfTheForest.Movement;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Testing
{
    /// <summary>
    /// Everything a test may do: build a scene inside one arena, act through product APIs, advance
    /// game time synchronously and record observable measurements. Failures are recorded, not thrown,
    /// so a test keeps reporting its numbers after the first failed assertion; the runner still
    /// catches real exceptions and fails only that test.
    /// </summary>
    public sealed class TestContext
    {
        private const int DefaultWaitTicks = 600;
        private const string BloodlineGeneDefName = "WotF_ForestBloodline";
        public const string SpellbookDefName = "WotF_Spellbook_Basic";

        private readonly List<string> errors = new List<string>();
        private readonly List<Measurement> measurements = new List<Measurement>();
        private int failed;

        internal TestContext(TestArena arena, string phase, bool afterReload)
        {
            Arena = arena;
            Phase = phase;
            AfterReload = afterReload;
        }

        /// <summary>The rectangle this test owns; already cleared and paved.</summary>
        public TestArena Arena { get; }

        internal string Phase { get; }

        internal bool AfterReload { get; }

        public Map Map => Arena.Map;

        internal bool Failed => failed > 0;

        internal IReadOnlyList<string> Errors => errors;

        internal IReadOnlyList<Measurement> Measurements => measurements;

        public int Tick => Find.TickManager.TicksGame;

        // ------------------------------------------------------------------ scene helpers

        /// <summary>
        /// A player-controlled colonist standing still on the caster cell: carries the forest
        /// bloodline gene, wears only the basic spellbook and holds no weapon, drafted so the job
        /// system never walks it off the test spot.
        /// </summary>
        public Pawn SpawnCaster(int indexX, int indexZ)
        {
            Pawn pawn = SpawnColonist(indexX, indexZ, "Lab Caster");
            pawn.genes.AddGene(DefDatabase<GeneDef>.GetNamed(BloodlineGeneDefName), false);

            Strip(pawn);
            WearSpellbook(pawn);
            pawn.drafter.Drafted = true;
            pawn.Rotation = Rot4.East;
            return pawn;
        }

        /// <summary>A drafted, unarmed, unarmoured player colonist; a stable damage/heal target.</summary>
        public Pawn SpawnTarget(int indexX, int indexZ)
        {
            Pawn pawn = SpawnColonist(indexX, indexZ, "Lab Target");
            Strip(pawn);
            pawn.drafter.Drafted = true;
            return pawn;
        }

        /// <summary>
        /// A player elf of the mod's own race. SpawnCaster is a human carrying the gene, so anything
        /// that depends on the race itself (food type, body, appearance) needs this one.
        /// </summary>
        public Pawn SpawnElf(int indexX, int indexZ)
        {
            Pawn elf = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                DefDatabase<PawnKindDef>.GetNamed("WotF_Elf_Colonist"),
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                canGeneratePawnRelations: false));
            GenSpawn.Spawn(elf, Arena.Cell(indexX, indexZ), Map, WipeMode.Vanish);
            return Arena.Track(elf);
        }

        private Pawn SpawnColonist(int indexX, int indexZ, string name)
        {
            IntVec3 cell = Arena.Cell(indexX, indexZ);
            // Relations are the one part of vanilla generation that reaches into other pawns and can
            // throw (HAR's parent-relation patch assumed a NameTriple); a scene dummy needs none.
            var request = new PawnGenerationRequest(
                PawnKindDefOf.Colonist,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                canGeneratePawnRelations: false,
                forceNoGear: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);

            // Vanilla generation sometimes hands out pre-existing injuries (a "gunshot" from a
            // backstory); a damage dummy must start from a known state. Only injuries are removed:
            // Reset() rebuilds the health tracker and changed damage outcomes in testing.
            var injuries = new List<Hediff>();
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury)
                {
                    injuries.Add(hediff);
                }
            }

            for (int i = injuries.Count - 1; i >= 0; i--)
            {
                pawn.health.RemoveHediff(injuries[i]);
            }

            GenSpawn.Spawn(pawn, cell, Map, WipeMode.Vanish);
            pawn.Name = new NameSingle(name);
            Arena.Track(pawn);
            return pawn;
        }

        /// <summary>Puts a fresh basic spellbook on the pawn.</summary>
        public static Apparel WearSpellbook(Pawn pawn)
        {
            var book = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(SpellbookDefName));
            pawn.apparel.Wear(book, false, true);
            return book;
        }

        private static void Strip(Pawn pawn)
        {
            pawn.apparel?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment(DestroyMode.Vanish);
        }

        /// <summary>A wood wall, returned so the test can read its hit points.</summary>
        public Thing SpawnWall(int indexX, int indexZ, ThingDef material = null)
        {
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, material ?? ThingDefOf.WoodLog);
            GenSpawn.Spawn(wall, Arena.Cell(indexX, indexZ), Map, WipeMode.Vanish);
            return Arena.Track(wall);
        }

        /// <summary>A wild plant at the cell, grown to <paramref name="growth"/> first.</summary>
        public Plant SpawnPlant(int indexX, int indexZ, float growth = 0.1f, ThingDef plantDef = null)
        {
            ThingDef def = plantDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Grass");
            if (def == null)
            {
                Fail("plant def Plant_Grass is not loaded");
                return null;
            }

            IntVec3 cell = Arena.Cell(indexX, indexZ);
            Plant plant = (Plant)ThingMaker.MakeThing(def);
            plant.Growth = growth;
            GenSpawn.Spawn(plant, cell, Map, WipeMode.Vanish);
            return Arena.Track(plant);
        }

        public void SetTerrain(int indexX, int indexZ, TerrainDef terrain)
        {
            Arena.SetTerrain(Arena.Cell(indexX, indexZ), terrain);
        }

        /// <summary>
        /// Removes plants, filth and loose items from the band the beam will cross, from the given
        /// arena cell to the map edge. The railgun always runs to the edge, so a test that only wants
        /// its own targets in the corridor has to clean the part outside the arena too.
        /// </summary>
        public void ClearBandBeyondArena(int indexX, int indexZ, int halfWidth)
        {
            IntVec3 start = Arena.Cell(indexX, indexZ);
            for (int x = start.x; x < Map.Size.x; x++)
            {
                for (int dz = -halfWidth; dz <= halfWidth; dz++)
                {
                    var cell = new IntVec3(x, 0, start.z + dz);
                    if (!cell.InBounds(Map))
                    {
                        continue;
                    }

                    var here = new List<Thing>(Map.thingGrid.ThingsListAt(cell));
                    for (int i = 0; i < here.Count; i++)
                    {
                        Thing thing = here[i];
                        if (thing is Plant || thing is Filth || (thing.def != null && thing.def.category == ThingCategory.Item))
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }

            Map.pathing?.RecalculateAllPerceivedPathCosts();
            Map.reachability?.ClearCache();
        }

        /// <summary>Teleports a pawn (no pathing) so two laps of the same scene start identically.</summary>
        public void Place(Pawn pawn, int indexX, int indexZ)
        {
            IntVec3 cell = Arena.Cell(indexX, indexZ);
            pawn.Position = cell;
            pawn.Notify_Teleported(true, true);
        }

        /// <summary>The mod's own carrier ability, granted through the product path when missing.</summary>
        public Ability SpellAbility(Pawn caster)
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(SpellDefs.CastAbilityDefName);
            if (def == null)
            {
                Fail("carrier ability def " + SpellDefs.CastAbilityDefName + " is not loaded");
                return null;
            }

            Ability ability = caster.abilities.GetAbility(def, true);
            if (ability == null)
            {
                SpellService.GrantCastAbilityToEligiblePawns();
                ability = caster.abilities.GetAbility(def, true);
            }

            if (ability == null)
            {
                caster.abilities.GainAbility(def);
                ability = caster.abilities.GetAbility(def, true);
            }

            if (ability == null)
            {
                Fail("carrier ability could not be granted to " + caster.LabelShort);
            }

            return ability;
        }

        /// <summary>Copies the Def's own numbers into the test instead of a second hard-coded table.</summary>
        public static SpellGlyphDef Glyph(string defName)
        {
            SpellGlyphDef def = DefDatabase<SpellGlyphDef>.GetNamedSilentFail(defName);
            return def;
        }

        public static SpellTuningDef Tuning => SpellDefs.Tuning;

        // ------------------------------------------------------------------ player entry points

        /// <summary>The menu the player gets right-clicking the thing with only this pawn selected.</summary>
        public List<FloatMenuOption> RightClick(Pawn pawn, Thing target)
        {
            return FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, target.DrawPos, out _);
        }

        /// <summary>The check the build designator runs every frame before it places a blueprint.</summary>
        public AcceptanceReport CanPlace(ThingDef def, int indexX, int indexZ, ThingDef stuff = null)
        {
            return GenConstruct.CanPlaceBlueprintAt(def, Arena.Cell(indexX, indexZ), Rot4.North, Map, stuffDef: stuff);
        }

        // ------------------------------------------------------------------ casting

        /// <summary>Builds a named recipe from glyph defNames; the first one is the Form.</summary>
        public static SpellRecipe Recipe(string name, params string[] glyphs)
        {
            return new SpellRecipe
            {
                name = name,
                glyphs = new List<string>(glyphs)
            };
        }

        /// <summary>Casts through the real service and records the failure reason when it refuses.</summary>
        public bool Cast(Pawn caster, SpellRecipe recipe, LocalTargetInfo target)
        {
            string reason;
            bool ok = SpellService.TryCast(caster, recipe, target, out reason);
            if (!ok)
            {
                Fail("cast '" + recipe.name + "' refused: " + reason);
            }

            return ok;
        }

        public bool CastAtCell(Pawn caster, SpellRecipe recipe, IntVec3 cell)
        {
            return Cast(caster, recipe, new LocalTargetInfo(cell));
        }

        public bool CastAt(Pawn caster, SpellRecipe recipe, Thing target)
        {
            return Cast(caster, recipe, new LocalTargetInfo(target));
        }

        public bool CastAtSelf(Pawn caster, SpellRecipe recipe)
        {
            return Cast(caster, recipe, new LocalTargetInfo(caster));
        }

        /// <summary>
        /// Makes a second cast of the same pawn possible without waiting two real seconds: advances
        /// exactly the cooldown the book stores, in game ticks.
        /// </summary>
        public void ClearCooldown(Pawn caster)
        {
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            int remaining = component == null ? 0 : component.GetCooldownTicksRemaining(caster);
            if (remaining > 0)
            {
                Step(remaining);
            }
        }

        // ------------------------------------------------------------------ time

        /// <summary>Advances the game synchronously; no real time passes.</summary>
        public void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Find.TickManager.DoSingleTick();
            }
        }

        /// <summary>Steps until the condition holds; returns false after <paramref name="limit"/> ticks.</summary>
        public bool StepUntil(Func<bool> condition, int limit = DefaultWaitTicks)
        {
            for (int i = 0; i < limit; i++)
            {
                if (condition())
                {
                    return true;
                }

                Find.TickManager.DoSingleTick();
            }

            return condition();
        }

        /// <summary>
        /// Runs a Goto job and steps until the pawn stands on the destination (or the limit passes).
        /// Movement is driven through the normal job/pather path so the movement patches are under
        /// test, not bypassed.
        /// </summary>
        public int WalkTo(Pawn pawn, IntVec3 destination, int limit = DefaultWaitTicks)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(destination));
            pawn.jobs.TryTakeOrderedJob(job, null, false);
            int start = Tick;
            for (int i = 0; i < limit; i++)
            {
                if (pawn.Position == destination)
                {
                    return Tick - start;
                }

                Find.TickManager.DoSingleTick();
            }

            return pawn.Position == destination ? Tick - start : -1;
        }

        // ------------------------------------------------------------------ observations

        public float InjurySeverity(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return 0f;
            }

            float total = 0f;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury && !HediffUtility.IsPermanent(injury))
                {
                    total += injury.Severity;
                }
            }

            return total;
        }

        public int InjuryCount(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return 0;
            }

            int count = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury && !HediffUtility.IsPermanent((Hediff_Injury)hediffs[i]))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Adds a real, non-permanent wound so Heal has something to treat.</summary>
        public Hediff Wound(Pawn pawn, float severity)
        {
            BodyPartRecord part = null;
            foreach (BodyPartRecord candidate in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (candidate.coverageAbs > 0f && candidate.def.defName == "LeftArm")
                {
                    part = candidate;
                    break;
                }
            }

            if (part == null)
            {
                foreach (BodyPartRecord candidate in pawn.health.hediffSet.GetNotMissingParts())
                {
                    if (candidate.coverageAbs > 0f)
                    {
                        part = candidate;
                        break;
                    }
                }
            }

            Hediff injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
            injury.Severity = severity;
            pawn.health.AddHediff(injury, part, null, null);
            return injury;
        }

        /// <summary>Live darts of the arcane projectile def currently spawned on this map.</summary>
        public int LiveMissiles()
        {
            ThingDef def = ArcaneMissileDefs.ProjectileThingDef;
            return def == null ? 0 : Map.listerThings.ThingsOfDef(def).Count;
        }

        /// <summary>Launch speed of the first live dart, read from the lab frame's copy of its settings.</summary>
        public float LiveMissileSpeed()
        {
            MapComponent_ArcaneMissiles component = Map.GetComponent<MapComponent_ArcaneMissiles>();
            IReadOnlyList<ArcaneMissileLabFrame> frames = component?.GetLabFrames();
            return frames != null && frames.Count > 0 ? frames[0].Settings.speedCellsPerSecond : -1f;
        }

        /// <summary>Number of impact resolutions counted by the map adapter since this map loaded.</summary>
        public int MissileImpacts()
        {
            return Map.GetComponent<MapComponent_ArcaneMissiles>()?.TotalImpacts ?? 0;
        }

        /// <summary>Impacts whose cell lies inside one arena, for tests sharing a save with other scenes.</summary>
        public int MissileImpactsIn(CellRect area)
        {
            return Map.GetComponent<MapComponent_MissileImpactWatch>()?.CountIn(area) ?? 0;
        }

        /// <summary>Where impacts landed since this map loaded, as "cell=count" pairs.</summary>
        public string MissileImpactLocations()
        {
            return Map.GetComponent<MapComponent_MissileImpactWatch>()?.Locations ?? "";
        }

        public static bool Wears(Pawn pawn, string apparelDefName)
        {
            return StrideApparel.IsWearing(pawn, apparelDefName);
        }

        /// <summary>Every hediff on the pawn as "def:severity", for diagnosing a failed damage/heal.</summary>
        public static string HediffDump(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return "none";
            }

            var parts = new List<string>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff?.def == null)
                {
                    continue;
                }

                parts.Add(hediff.def.defName + ":" + hediff.Severity.ToString("0.##")
                          + (hediff is Hediff_Injury injury && HediffUtility.IsPermanent(injury) ? "(permanent)" : string.Empty));
            }

            return parts.Count == 0 ? "none" : string.Join(",", parts.ToArray());
        }

        /// <summary>Vanilla reachability with the same parameters the lab move command uses.</summary>
        public bool CanReach(Pawn pawn, IntVec3 cell)
        {
            try
            {
                TraverseParms parms = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors, false, false, false, false);
                return Map.reachability.CanReach(pawn.Position, new LocalTargetInfo(cell), PathEndMode.OnCell, parms);
            }
            catch
            {
                return false;
            }
        }

        public bool Walkable(IntVec3 cell, Pawn pawn)
        {
            return GenGrid.WalkableBy(cell, Map, pawn);
        }

        // ------------------------------------------------------------------ assertions

        public void Expect(bool condition, string message)
        {
            if (condition)
            {
                return;
            }

            failed++;
            errors.Add(message);
        }

        public void ExpectGreater(float actual, float minimum, string name)
        {
            Expect(actual > minimum, name + ": expected > " + minimum.ToString("0.###") + ", got " + actual.ToString("0.###"));
        }

        public void ExpectAtLeast(float actual, float minimum, string name)
        {
            Expect(actual >= minimum, name + ": expected >= " + minimum.ToString("0.###") + ", got " + actual.ToString("0.###"));
        }

        public void Fail(string message)
        {
            failed++;
            errors.Add(message);
        }

        public void Note(string name, float value)
        {
            measurements.Add(Measurement.Of(name, value));
        }

        public void Note(string name, int value)
        {
            measurements.Add(Measurement.Of(name, value));
        }

        public void Note(string name, bool value)
        {
            measurements.Add(Measurement.Of(name, value));
        }

        /// <summary>Records a free-form number (positions, fractions) the same way as <see cref="Note(string,float)"/>.</summary>
        public void Measure(string name, string value)
        {
            measurements.Add(new Measurement(name, value));
        }

        public static string DescribeThing(Thing thing)
        {
            return thing == null ? "null" : thing.def.defName + "@" + thing.Position;
        }
    }
}
