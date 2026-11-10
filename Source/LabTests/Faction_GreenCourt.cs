using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The Verdant Court faction handoff: its pawn kinds are the HAR elf, the race's bloodline
    /// gene reaches generated members, and a generated faction picks the archon kind as leader.
    /// </summary>
    [LabTest]
    public sealed class Faction_GreenCourt : LabTest
    {
        public override string Name => "faction.green-court";

        protected override void Run(TestContext t)
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("WotF_GreenCourt");
            ThingDef elfDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_Elf");
            PawnKindDef villager = DefDatabase<PawnKindDef>.GetNamedSilentFail("WotF_Elf_Villager");
            PawnKindDef leaderKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WotF_Elf_Leader");
            if (def == null || elfDef == null || villager == null || leaderKind == null)
            {
                t.Fail("Verdant Court faction or pawn kinds are not loaded");
                return;
            }

            t.Expect(def.humanlikeFaction, "the Verdant Court is not a humanlike faction");
            t.Expect(def.basicMemberKind == villager, "the faction's basic member kind is not the forest villager");
            t.Expect(villager.race == elfDef && leaderKind.race == elfDef, "a faction pawn kind does not use the elf race");
            t.Expect(def.fixedLeaderKinds != null && def.fixedLeaderKinds.Contains(leaderKind), "the archon kind is not a fixed leader kind");

            string[] groups = def.pawnGroupMakers.Select(g => g.kindDef.defName).ToArray();
            t.Expect(groups.Contains("Combat"), "the faction has no combat pawn group");
            t.Expect(groups.Contains("Peaceful"), "the faction has no peaceful pawn group");
            t.Expect(groups.Contains("Trader"), "the faction has no trader pawn group");

            Pawn villagerPawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                villager, Faction.OfPlayer, canGeneratePawnRelations: false));
            t.Expect(villagerPawn.def == elfDef, "a generated villager is not an elf");
            t.Expect(Gene_ForestBloodline.Get(villagerPawn) != null, "a generated villager has no forest bloodline");
            Find.WorldPawns.PassToWorld(villagerPawn, PawnDiscardDecideMode.Discard);

            Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
            Find.FactionManager.Add(faction);
            t.Expect(faction.def == def, "the generated faction has the wrong def");
            t.Expect(faction.leader != null, "the generated faction has no leader");
            if (faction.leader != null)
            {
                t.Expect(faction.leader.def == elfDef, "the faction leader is not an elf");
                t.Expect(faction.leader.kindDef == leaderKind, "the faction leader is not the archon kind");
                t.Expect(SpellService.WornSpellbook(faction.leader) != null, "the archon has no spellbook");
                t.Measure("leaderKind", faction.leader.kindDef.defName);
            }

            t.Measure("groups", string.Join(",", groups));
            t.Measure("leaderTitle", def.leaderTitle);
        }
    }
}
