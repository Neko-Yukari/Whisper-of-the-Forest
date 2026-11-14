using System;
using RimWorld;
using Verse;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Turret_TargetFilters : LabTest
    {
        public override string Name => "turret.target-filters";

        protected override void Run(TestContext t)
        {
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 5, 5, ThingDefOf.Steel);
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            Pawn writer = t.SpawnElf(3, 5);
            Turret_TestFixture.Inscribe(t, writer, turret,
                TestContext.Recipe("target-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm));
            writer.drafter.Drafted = true;
            t.Expect(!turret.TryFindNewTarget().IsValid, "default filter selected a friendly");
            comp.TargetFriendly = true;
            LocalTargetInfo friendly = turret.TryFindNewTarget();
            t.Note("friendlyOwnColonistAllowed", comp.AllowsTarget(writer));
            t.Expect(comp.AllowsTarget(writer), "friendly filter rejected own colonist");
            t.Expect(!friendly.IsValid || (!friendly.Thing.HostileTo(turret.Faction) && comp.AllowsTarget(friendly.Thing)), "friendly filter selected a hostile or disallowed target");
            comp.TargetHumanlikes = false;
            t.Expect(!comp.AllowsTarget(writer), "relationship bypassed category filter");
            var node = Turret_TestFixture.Spawn<Building>(t, "WotF_ManaNode", 6, 5, ThingDefOf.BlocksGranite);
            comp.TargetNeutral = true;
            t.Expect(turret.TryFindNewTarget().Thing != node, "friendly/neutral filters selected own mana node");
            t.Expect(!comp.AllowsTarget(node) && !comp.AllowsTarget(turret), "building/self permitted by shot filter");
            t.Note("ownNodeAllowed", comp.AllowsTarget(node));
            node.Destroy(DestroyMode.Vanish);
            comp.TargetNeutral = false;
            comp.TargetHumanlikes = true;
            comp.TargetFriendly = false;
            Check(t, turret, DefDatabase<PawnKindDef>.GetNamed("Muffalo"), null, value => comp.TargetNeutral = value, "neutral");
            Check(t, turret, PawnKindDefOf.Colonist, Faction.OfAncientsHostile, value => comp.TargetHumanlikes = value, "humanlike");
            Check(t, turret, DefDatabase<PawnKindDef>.GetNamed("Muffalo"), Faction.OfMechanoids, value => comp.TargetAnimals = value, "animal");
            Check(t, turret, DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther"), Faction.OfMechanoids, value => comp.TargetMechanoids = value, "mechanoid");
            Check(t, turret, DefDatabase<PawnKindDef>.GetNamed("Fingerspike"), Faction.OfEntities, value => comp.TargetEntities = value, "entity");
        }

        private static void Check(TestContext t, Building_SpellTurret turret, PawnKindDef kind, Faction faction, Action<bool> toggle, string category)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, canGeneratePawnRelations: false));
            GenSpawn.Spawn(pawn, t.Arena.Cell(10, 5), t.Map, WipeMode.Vanish);
            t.Arena.Track(pawn);
            t.Measure(category + "Setup", "faction=" + pawn.Faction?.def.defName
                + ", hostile=" + turret.HostileTo(pawn) + ", threatDisabled=" + pawn.ThreatDisabled(turret));
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            if (category == "neutral")
                t.Expect(!comp.AllowsTarget(pawn), "default filter allowed neutral animal");
            toggle(true);
            LocalTargetInfo enabled = turret.TryFindNewTarget();
            bool allowed = comp.AllowsTarget(pawn);
            t.Expect(allowed, category + " enabled filter rejected target");
            t.Expect(!enabled.IsValid || comp.AllowsTarget(enabled.Thing), category + " selected a disallowed target");
            if (category == "humanlike")
            {
                comp.TargetHostile = false;
                bool blocked = !comp.AllowsTarget(pawn);
                t.Note("hostileDisabled", blocked);
                t.Expect(blocked, "hostile disabled still selected enemy");
                comp.TargetHostile = true;
            }
            toggle(false);
            LocalTargetInfo disabled = turret.TryFindNewTarget();
            t.Expect(!comp.AllowsTarget(pawn), category + " disabled filter still allows target");
            t.Expect(disabled.Thing != pawn, category + " disabled target still selected");
            t.Measure(category, "enabled=" + allowed + ", disabled=" + comp.AllowsTarget(pawn));
            pawn.Destroy(DestroyMode.Vanish);
        }
    }
}
