using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Turret_Interface : LabTest
    {
        public override string Name => "turret.interface";

        protected override void Run(TestContext t)
        {
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 5, 5, ThingDefOf.Steel);
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            Pawn elf = t.SpawnElf(3, 5);
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            SpellRecipe dart = TestContext.Recipe("menu-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            SpellRecipe touch = TestContext.Recipe("menu-touch", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            t.Expect(book.SaveSlot(elf, 0, dart, out string reason), reason);
            t.Expect(book.SaveSlot(elf, 1, touch, out reason), reason);
            t.Expect(book.SaveSlot(elf, 2, dart, out reason), reason);
            int activeBefore = book.GetActiveSlot(elf);

            Command_Action command = turret.GetGizmos().OfType<Command_Action>()
                .SingleOrDefault(c => c.defaultLabel == SpellText.T("WotF_Turret_SelectSpell"));
            t.Expect(command != null, "spell-selection gizmo missing");
            if (command == null) return;
            command.action();
            FloatMenu_TurretSpells menu = Find.WindowStack.WindowOfType<FloatMenu_TurretSpells>();
            t.Expect(menu != null, "gizmo did not open the spell menu");
            if (menu == null) return;
            try
            {
                t.Note("candidates", comp.GetInscriptionCandidates().Count());
                t.Note("menuOptions", menu.Options.Count);
                t.Expect(menu.Options.Count == comp.GetInscriptionCandidates().Count(),
                    "menu must contain candidates only");
                t.Expect(menu.Options.Count(o => o.Label.Contains("menu-dart")) == 2, "duplicate slots were merged");
                FloatMenuOption disabled = menu.Options.Single(o => o.Label.Contains("menu-touch"));
                t.Expect(disabled.Disabled && disabled.Label.Contains(SpellText.T("WotF_Turret_ProjectileOnly")),
                    "non-projectile is not disabled with its refusal reason");
                menu.Options.First(o => o.Label.Contains("menu-dart")).action();
                t.Expect(comp.PendingSpell?.name == "menu-dart" && !comp.HasSpell,
                    "menu click did not queue an inscription while keeping current spell empty");
                t.Expect(turret.GetInspectString().Contains("menu-dart"), "pending spell missing from inspect text");
                t.Expect(!menu.Options.Any(o => o.Label == SpellText.T("WotF_Turret_NewSpell")), "new-spell action remains in menu");
                Command_Action create = turret.GetGizmos().OfType<Command_Action>()
                    .Single(c => c.defaultLabel == SpellText.T("WotF_Turret_NewSpell"));
                create.action();
                Window_TurretSpell editor = Find.WindowStack.WindowOfType<Window_TurretSpell>();
                t.Expect(editor != null, "new spell did not open the independent editor");
                editor?.Close(false);
                t.Expect(book.GetActiveSlot(elf) == activeBefore, "independent editor changed the active slot");
                t.Expect(book.GetSlot(elf, 0).name == "menu-dart" && book.GetSlot(elf, 1).name == "menu-touch",
                    "independent editor changed saved slots");
            }
            finally
            {
                menu.Close(false);
            }
            book.ClearSlot(elf, 0);
            book.ClearSlot(elf, 1);
            book.ClearSlot(elf, 2);
            t.Expect(!comp.GetInscriptionCandidates().Any(), "empty-menu fixture has other candidates");
            command.action();
            t.Expect(Find.WindowStack.WindowOfType<FloatMenu_TurretSpells>() == null, "empty menu was opened");
            t.Expect(turret.GetInspectTabs().Any(tab => tab is ITab_SpellTurretTargets),
                "target-filter ITab is not registered on the spawned turret");
        }
    }
}
