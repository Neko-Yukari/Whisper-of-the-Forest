using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Mana_BatteryHandoff : LabTest
    {
        public override string Name => "mana.battery-handoff";

        protected override void Run(TestContext t)
        {
            var node = Turret_TestFixture.Spawn<Building>(t, "WotF_ManaNode", 5, 5);
            var graph = MapComponent_ManaNetwork.For(t.Map);
            ManaNetwork network = graph.NetworkNear(node.Position, 1f);
            t.Note("nodeOnlyCapacity", network.Capacity);
            t.Expect(network.Capacity == 0f && network.Add(1f) == 0f && !network.TryDraw(1f),
                "a node without a battery stored or supplied mana");
            var def = DefDatabase<ThingDef>.GetNamed("WotF_ManaBattery");
            t.Expect(t.CanPlace(def, 7, 7).Accepted, "battery placement refused");
            CompManaBattery battery = Mana_TestFixture.AddBattery(t, node);
            var furnace = Turret_TestFixture.Spawn<Building>(t, "WotF_ManaFurnace", 4, 5);
            var fuel = furnace.GetComp<CompRefuelable>();
            fuel.Refuel(fuel.Props.fuelCapacity);
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 6, 5, ThingDefOf.Steel);
            Pawn writer = t.SpawnElf(2, 5);
            Turret_TestFixture.Inscribe(t, writer, turret,
                TestContext.Recipe("battery-shot", SpellDefs.FormProjectile, SpellDefs.EffectHarm));
            var comp = turret.GetComp<CompSpellTurret>();
            var producer = furnace.GetComp<CompManaProducer>();
            int chargeTicks = (int)System.Math.Ceiling(comp.ManaPerShot / producer.Props.manaPerSecond) * 60 + 60;
            t.Expect(t.StepUntil(() => battery.Stored >= comp.ManaPerShot, chargeTicks), "producer did not charge battery");
            furnace.Destroy(DestroyMode.Vanish);
            float charged = battery.Stored;
            t.Note("producerCharge", charged);
            Pawn target = t.SpawnTarget(12, 5);
            comp.TargetFriendly = true;
            t.Expect(turret.AttackVerb.TryStartCastOn(target), "charged battery could not supply the turret");
            comp.TargetFriendly = false;
            t.Note("afterConsumer", battery.Stored);
            t.Expect(battery.Stored < charged, "consumer did not draw from battery");
            float retained = battery.Stored;
            node.Destroy(DestroyMode.Vanish);
            graph.NetworkNear(battery.parent.Position, battery.LinkRadius);
            t.Expect(battery.Network == null && battery.Stored == retained, "disconnect lost battery charge");
            node = Turret_TestFixture.Spawn<Building>(t, "WotF_ManaNode", 5, 5);
            network = graph.NetworkNear(node.Position, 1f);
            t.Expect(battery.Network == network && network.Stored == retained, "reconnecting lost the battery charge");
            battery.parent.Destroy(DestroyMode.Deconstruct);
            network = graph.NetworkNear(node.Position, 1f);
            t.Note("capacityAfterBatteryDestroyed", network.Capacity);
            t.Expect(network.Capacity == 0f && network.Stored == 0f, "destroyed battery left charge in the network");
        }
    }
}
