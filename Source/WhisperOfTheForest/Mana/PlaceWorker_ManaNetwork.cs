using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Teleport;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.Mana
{
    [StaticConstructorOnStartup]
    public class PlaceWorker_ManaNetwork : PlaceWorker
    {
        // Mana blue, same as the battery charge bar.
        internal static readonly Color LineColor = new Color(0.31f, 0.71f, 1f);
        internal static readonly Material LineMat =
            MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, LineColor);

        internal static void DrawLine(Vector3 a, Vector3 b) => GenDraw.DrawLineBetween(a, b, LineMat);

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            DrawConnections(def, center, Find.CurrentMap, thing);
        }

        internal static float LinkRadius(ThingDef def)
        {
            foreach (CompProperties properties in def.comps)
            {
                if (properties is CompProperties_ManaNode node) return node.linkRadius;
                if (properties is CompProperties_ManaBattery battery) return battery.linkRadius;
                if (properties is CompProperties_ManaProducer furnace) return furnace.linkRadius;
                if (properties is CompProperties_ManaPlantProducer grove) return grove.radius;
                if (properties is CompProperties_ManaPowerProducer converter) return converter.linkRadius;
                if (properties is CompProperties_ManaWorkbench bench) return bench.linkRadius;
                if (properties is CompProperties_RitualAltar altar) return altar.linkRadius;
                if (properties is CompProperties_SpellTurret turret) return turret.linkRadius;
                if (properties is CompProperties_TeleportGate teleportGate) return teleportGate.linkRadius;
            }
            return 0f;
        }

        internal static void DrawConnections(ThingDef def, IntVec3 center, Map map, Thing thing = null,
            bool drawRadius = true)
        {
            float radius = LinkRadius(def);
            if (radius <= 0f) return;
            if (drawRadius) GenDraw.DrawRadiusRing(center, radius, LineColor);
            var graph = MapComponent_ManaNetwork.For(map);
            if (def.GetCompProperties<CompProperties_ManaNode>() == null)
            {
                CompManaNode nearest = graph.NodeNear(center, radius);
                if (nearest != null) DrawLine(center.ToVector3Shifted(), nearest.parent.TrueCenter());
                return;
            }

            foreach (ManaNetwork network in graph.Networks)
            foreach (CompManaNode node in network.nodes)
            {
                if (node.parent == thing) continue;
                float reach = Mathf.Min(radius, node.LinkRadius);
                if ((center - node.parent.Position).LengthHorizontalSquared <= reach * reach)
                    DrawLine(center.ToVector3Shifted(), node.parent.TrueCenter());
            }

            foreach (Thing building in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (building == thing || building.def.GetCompProperties<CompProperties_ManaNode>() != null) continue;
                float deviceRadius = LinkRadius(building.def);
                if (deviceRadius <= 0f || (building.Position - center).LengthHorizontalSquared > deviceRadius * deviceRadius)
                    continue;
                CompManaNode nearest = graph.NodeNear(building.Position, deviceRadius);
                bool connected = thing != null && thing.Spawned
                    ? nearest?.parent == thing
                    : nearest == null || (building.Position - center).LengthHorizontalSquared
                        < (nearest.parent.Position - building.Position).LengthHorizontalSquared;
                if (connected) DrawLine(center.ToVector3Shifted(), building.TrueCenter());
            }
        }
    }
}
