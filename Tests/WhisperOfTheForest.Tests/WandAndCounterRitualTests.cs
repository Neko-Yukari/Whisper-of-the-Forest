using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Spells;
using static WhisperOfTheForest.Tests.Glyphs;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam tests for unified spell staff / offhand wand modifiers, compiler range/windup scaling,
    /// and the Tier-2 counter-psychic ritual duration curve and ConfigErrors.
    /// </summary>
    internal static class WandAndCounterRitualTests
    {
        private static bool statsInitialized;

        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("staff-modifiers: wand vs long staff diverge on damage, mana, duration, range and windup", StaffModifiersDivergeBetweenWandAndLongStaff),
            ("staff-modifiers: CompProperties_SpellStaff ConfigErrors catches invalid bounds", StaffPropsConfigErrors),
            ("staff-compiler: rangeFactor and windupFactor scale compiled plan and keep touch reachable", CompilerAppliesRangeAndWindup),
            ("counter-ritual: CounterStunTicksFor scales with ritual strength and enforces floor", CounterStunScalesWithStrength),
            ("counter-ritual: RitualDef ConfigErrors validates counterStunTicks and curve", CounterRitualConfigErrors),
        };

        private static void EnsureStatDefs()
        {
            if (statsInitialized)
            {
                return;
            }

            StatDefOf.StuffPower_Armor_Heat ??= MakeStat("T_StuffPower_Armor_Heat", 0f);
            StatDefOf.StuffPower_Armor_Blunt ??= MakeStat("T_StuffPower_Armor_Blunt", 0f);
            StatDefOf.SharpDamageMultiplier ??= MakeStat("T_SharpDamageMultiplier", 1f);
            StatDefOf.BluntDamageMultiplier ??= MakeStat("T_BluntDamageMultiplier", 1f);
            statsInitialized = true;
        }

        private static StatDef MakeStat(string defName, float defaultBase)
        {
            var stat = new StatDef
            {
                defName = defName,
                workerClass = typeof(StatWorker),
                defaultBaseValue = defaultBase,
                minValue = 0f,
                maxValue = 100f,
                toStringStyle = ToStringStyle.PercentZero
            };
            DefDatabase<StatDef>.Add(stat);
            return stat;
        }

        private static CompProperties_SpellStaff MakeWandProps() => new CompProperties_SpellStaff
        {
            isOneHanded = true,
            allowsSuperGlyphs = false,
            allowsShieldBelt = true,
            rangeFactor = 0.64f,
            windupFactor = 0.70f,
            spellPowerFactor = 0.80f,
            bluntArmorCostReduction = 0.20f,
            minManaFactor = 0.85f,
            heatArmorDurationBonus = 0.10f,
            maxDurationFactor = 1.20f,
            maxDamageFactor = 1.15f
        };

        private static CompProperties_SpellStaff MakeLongStaffProps() => new CompProperties_SpellStaff
        {
            isOneHanded = false,
            allowsSuperGlyphs = true,
            allowsShieldBelt = false,
            rangeFactor = 1.00f,
            windupFactor = 1.00f,
            spellPowerFactor = 1.05f,
            bluntArmorCostReduction = 0.40f,
            minManaFactor = 0.60f,
            heatArmorDurationBonus = 0.20f,
            maxDurationFactor = 1.75f,
            maxDamageFactor = 1.50f
        };

        private static void StaffModifiersDivergeBetweenWandAndLongStaff()
        {
            EnsureStatDefs();

            // Neutral null-props guard
            SpellStaffModifiers neutral = SpellStaffModifiers.For(null, null);
            Check.Equal(1f, neutral.damageFactor, "null props damageFactor");
            Check.Equal(1f, neutral.manaFactor, "null props manaFactor");
            Check.Equal(1f, neutral.durationFactor, "null props durationFactor");
            Check.Equal(1f, neutral.rangeFactor, "null props rangeFactor");
            Check.Equal(1f, neutral.windupFactor, "null props windupFactor");

            CompProperties_SpellStaff wand = MakeWandProps();
            CompProperties_SpellStaff staff = MakeLongStaffProps();

            // Null stuff uses each focus's own baseline power, range and windup
            SpellStaffModifiers wandBase = SpellStaffModifiers.For(wand, null);
            SpellStaffModifiers staffBase = SpellStaffModifiers.For(staff, null);
            Check.True(wandBase.damageFactor < staffBase.damageFactor, "wand baseline power < long staff");
            Check.True(wandBase.rangeFactor < staffBase.rangeFactor, "wand rangeFactor < long staff");
            Check.True(wandBase.windupFactor < staffBase.windupFactor, "wand windupFactor < long staff");
            Check.Equal(1f, wandBase.manaFactor, "null stuff leaves manaFactor neutral");
            Check.Equal(1f, wandBase.durationFactor, "null stuff leaves durationFactor neutral");

            // High-stat material differentiates damage, mana discount and duration caps
            var highStuff = new ThingDef
            {
                defName = "T_StaffTest_HighStuff",
                statBases = new List<StatModifier>
                {
                    new StatModifier { stat = StatDefOf.SharpDamageMultiplier, value = 1.6f },
                    new StatModifier { stat = StatDefOf.BluntDamageMultiplier, value = 1.6f },
                    new StatModifier { stat = StatDefOf.StuffPower_Armor_Blunt, value = 1.5f },
                    new StatModifier { stat = StatDefOf.StuffPower_Armor_Heat, value = 3.0f },
                }
            };

            SpellStaffModifiers wandMod = SpellStaffModifiers.For(wand, highStuff);
            SpellStaffModifiers staffMod = SpellStaffModifiers.For(staff, highStuff);

            Check.True(wandMod.damageFactor < staffMod.damageFactor,
                $"wand damage cap ({wandMod.damageFactor}) must stay below long staff ({staffMod.damageFactor})");
            Check.True(Math.Abs(wandMod.damageFactor - wand.maxDamageFactor) < 0.0001f,
                "wand damage clamps at wand maxDamageFactor");
            Check.True(Math.Abs(staffMod.damageFactor - staff.maxDamageFactor) < 0.0001f,
                "long staff damage clamps at long staff maxDamageFactor");

            Check.True(wandMod.manaFactor > staffMod.manaFactor,
                $"wand mana floor ({wandMod.manaFactor}) must stay higher than long staff ({staffMod.manaFactor})");
            Check.True(Math.Abs(wandMod.manaFactor - wand.minManaFactor) < 0.0001f,
                "wand manaFactor clamps at wand minManaFactor");
            Check.True(Math.Abs(staffMod.manaFactor - staff.minManaFactor) < 0.0001f,
                "long staff manaFactor clamps at long staff minManaFactor");

            Check.True(wandMod.durationFactor < staffMod.durationFactor,
                $"wand duration cap ({wandMod.durationFactor}) must stay below long staff ({staffMod.durationFactor})");
            Check.True(Math.Abs(wandMod.durationFactor - wand.maxDurationFactor) < 0.0001f,
                "wand duration clamps at wand maxDurationFactor");
            Check.True(Math.Abs(staffMod.durationFactor - 1.60f) < 0.0001f,
                "long staff duration scales linearly below its cap");

            Check.True(wandMod.rangeFactor < staffMod.rangeFactor, "wand rangeFactor < long staff");
            Check.True(wandMod.windupFactor < staffMod.windupFactor, "wand windupFactor < long staff");
        }

        private static void StaffPropsConfigErrors()
        {
            var parent = new ThingDef { defName = "T_Staff_Parent" };

            Check.Equal(0, MakeWandProps().ConfigErrors(parent).Count(), "valid wand props pass ConfigErrors");
            Check.Equal(0, MakeLongStaffProps().ConfigErrors(parent).Count(), "valid long staff props pass ConfigErrors");

            var badRange = MakeWandProps();
            badRange.rangeFactor = 0f;
            Check.True(badRange.ConfigErrors(parent).Any(e => e.Contains("rangeFactor")), "rejects rangeFactor <= 0");

            var badWindup = MakeWandProps();
            badWindup.windupFactor = -0.1f;
            Check.True(badWindup.ConfigErrors(parent).Any(e => e.Contains("windupFactor")), "rejects windupFactor <= 0");

            var badMana = MakeWandProps();
            badMana.minManaFactor = 1.2f;
            Check.True(badMana.ConfigErrors(parent).Any(e => e.Contains("minManaFactor")), "rejects minManaFactor > 1");

            var badCap = MakeLongStaffProps();
            badCap.maxDamageFactor = 0.5f;
            Check.True(badCap.ConfigErrors(parent).Any(e => e.Contains("maxDamageFactor")),
                "rejects maxDamageFactor < spellPowerFactor");
        }

        private static void CompilerAppliesRangeAndWindup()
        {
            SpellGlyphDef proj = Get(Glyphs.Projectile);
            SpellGlyphDef touch = Get(Glyphs.Touch);
            SpellGlyphDef self = Get(Glyphs.Self);
            float origProjRange = proj.range;
            int origProjWindup = proj.windupTicks;
            float origTouchRange = touch.range;
            int origSelfWindup = self.windupTicks;
            try
            {
                proj.range = 25f;
                proj.windupTicks = 100;
                touch.range = 1.45f;
                self.windupTicks = 60;

                var wandMod = new SpellStaffModifiers { rangeFactor = 0.64f, windupFactor = 0.70f, durationFactor = 1.50f };

                SpellCastPlan projPlan = SpellCompiler.Compile(Recipe(Glyphs.Projectile, Harm), out string projErr, wandMod);
                Check.True(projPlan != null, "projectile compiles: " + projErr);
                Check.True(Math.Abs(projPlan.range - 16f) < 0.01f, $"projectile range scaled to 16, got {projPlan.range}");
                Check.Equal(70, projPlan.timing.windupTicks, "projectile windup scaled by 0.70");

                SpellCastPlan touchPlan = SpellCompiler.Compile(Recipe(Glyphs.Touch, Harm), out string touchErr, wandMod);
                Check.True(touchPlan != null, "touch compiles: " + touchErr);
                Check.True(touchPlan.range >= 1f, $"touch range stays >= 1 cell, got {touchPlan.range}");

                SpellCastPlan selfPlan = SpellCompiler.Compile(Recipe(Glyphs.Self, Harm), out string selfErr, wandMod);
                Check.True(selfPlan != null, "self compiles: " + selfErr);
                Check.Equal(0f, selfPlan.range, "self form keeps zero range");
                Check.Equal(42, selfPlan.timing.windupTicks, "self windup still scaled by windupFactor");

                // Snare, Freeze and Hex effects all honor staff.durationFactor
                var noAugments = new List<SpellGlyphDef>();
                SpellEffect snareBase = new SnareGlyphDef { rootDurationTicks = 300 }.Compile(noAugments, null);
                SpellEffect snareStaff = new SnareGlyphDef { rootDurationTicks = 300 }.Compile(noAugments, wandMod);
                Check.Equal(300, ReadIntField(snareBase, "rootDurationTicks"), "Snare base duration");
                Check.Equal(450, ReadIntField(snareStaff, "rootDurationTicks"), "Snare scales duration with staff.durationFactor");

                SpellEffect freezeBase = new FreezeGlyphDef { freezeDurationTicks = 300 }.Compile(noAugments, null);
                SpellEffect freezeStaff = new FreezeGlyphDef { freezeDurationTicks = 300 }.Compile(noAugments, wandMod);
                Check.Equal(300, ReadIntField(freezeBase, "freezeDurationTicks"), "Freeze base duration");
                Check.Equal(450, ReadIntField(freezeStaff, "freezeDurationTicks"), "Freeze scales duration with staff.durationFactor");

                SpellEffect hexBase = new HexGlyphDef { hexDurationTicks = 600 }.Compile(noAugments, null);
                SpellEffect hexStaff = new HexGlyphDef { hexDurationTicks = 600 }.Compile(noAugments, wandMod);
                Check.Equal(600, ReadIntField(hexBase, "hexDurationTicks"), "Hex base duration");
                Check.Equal(900, ReadIntField(hexStaff, "hexDurationTicks"), "Hex scales duration with staff.durationFactor");
            }
            finally
            {
                proj.range = origProjRange;
                proj.windupTicks = origProjWindup;
                touch.range = origTouchRange;
                self.windupTicks = origSelfWindup;
            }
        }

        private static int ReadIntField(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check.True(field != null, "missing field " + fieldName);
            return (int)field.GetValue(target);
        }

        private static RitualDef BuildCounterRitual(int stunTicks, SimpleCurve curve, ResearchProjectDef research)
        {
            return new RitualDef
            {
                defName = "T_Ritual_Counter",
                tier = 2,
                family = RitualFamily.Domain,
                ringSize = 5,
                chalkColor = ChalkColor.Green,
                manaCost = 40f,
                durationTicks = 900,
                effectRadius = 12.9f,
                counterStunTicks = stunTicks,
                strengthEffectFactor = curve,
                effectWorkerClass = typeof(RitualEffect_CounterPsychic),
                researchPrerequisite = research
            };
        }

        private static void CounterStunScalesWithStrength()
        {
            var research = new ResearchProjectDef { defName = "T_Counter_Res", baseCost = 100f, techLevel = TechLevel.Neolithic };
            var curve = new SimpleCurve
            {
                new CurvePoint(0f, 1f),
                new CurvePoint(15f, 2f),
                new CurvePoint(35f, 3.5f),
                new CurvePoint(60f, 5f),
                new CurvePoint(100f, 7f)
            };
            RitualDef def = BuildCounterRitual(600, curve, research);

            Check.True(def.UsesEffectStrength, "counter ritual reports UsesEffectStrength");
            int low = def.CounterStunTicksFor(0f);
            int mid = def.CounterStunTicksFor(15f);
            int high = def.CounterStunTicksFor(100f);
            Check.Equal(600, low, "R=0 yields base stun ticks");
            Check.Equal(1200, mid, "R=15 scales to 2x");
            Check.Equal(4200, high, "R=100 scales to 7x");
            Check.True(low < mid && mid < high, "stun duration grows monotonically with ritual strength");

            RitualDef tiny = BuildCounterRitual(10, new SimpleCurve { new CurvePoint(0f, 1f) }, research);
            Check.Equal(60, tiny.CounterStunTicksFor(0f), "CounterStunTicksFor enforces 60-tick minimum floor");
        }

        private static void CounterRitualConfigErrors()
        {
            var research = new ResearchProjectDef { defName = "T_Counter_Res2", baseCost = 100f, techLevel = TechLevel.Neolithic };
            var curve = new SimpleCurve { new CurvePoint(0f, 1f), new CurvePoint(100f, 7f) };

            RitualDef valid = BuildCounterRitual(600, curve, research);
            Check.Equal(0, valid.ConfigErrors().Count(), "valid counter-psychic RitualDef passes ConfigErrors");

            RitualDef zeroStun = BuildCounterRitual(0, curve, research);
            Check.True(zeroStun.ConfigErrors().Any(e => e.Contains("counterStunTicks")),
                "rejects counterStunTicks <= 0 on RitualEffect_CounterPsychic");

            RitualDef noCurve = BuildCounterRitual(600, null, research);
            Check.True(noCurve.ConfigErrors().Any(e => e.Contains("strengthEffectFactor")),
                "rejects missing strengthEffectFactor on RitualEffect_CounterPsychic");

            var strayField = new RitualDef
            {
                defName = "T_Ritual_StrayCounter",
                tier = 1,
                family = RitualFamily.Crafting,
                durationTicks = 60,
                counterStunTicks = 300,
                effectWorkerClass = typeof(RitualEffect_ManaSpring),
                researchPrerequisite = research
            };
            Check.True(strayField.ConfigErrors().Any(e => e.Contains("counterStunTicks")),
                "rejects counterStunTicks set on a non-counter ritual");
        }
    }
}
