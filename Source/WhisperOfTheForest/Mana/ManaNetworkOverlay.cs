using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public sealed class GameComponent_ManaView : GameComponent
    {
        public bool showNetwork;

        public GameComponent_ManaView(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showNetwork, "showManaNetwork");
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    internal static class Patch_ManaPlaySettings
    {
        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/ManaNetwork");

        private static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || Current.Game == null) return;
            using (TechLabDrawScope.Begin())
            {
                row.ToggleableIcon(ref Current.Game.GetComponent<GameComponent_ManaView>().showNetwork,
                    Icon, "WotF_ManaNetwork_Toggle".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            }
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.DrawExtraSelectionOverlays))]
    internal static class Patch_ManaSelection
    {
        private static void Postfix(Building __instance)
        {
            if (__instance.Spawned)
                PlaceWorker_ManaNetwork.DrawConnections(__instance.def, __instance.Position, __instance.Map, __instance);
        }
    }

    public sealed class MapComponent_ManaOverlay : MapComponent
    {
        public MapComponent_ManaOverlay(Map map) : base(map) { }

        private bool Visible => Find.CurrentMap == map
            && Current.Game.GetComponent<GameComponent_ManaView>().showNetwork;

        public override void MapComponentDraw()
        {
            if (!Visible) return;
            foreach (ManaNetwork network in MapComponent_ManaNetwork.For(map).Networks)
            {
                for (int i = 0; i < network.nodes.Count; i++)
                for (int j = i + 1; j < network.nodes.Count; j++)
                {
                    CompManaNode a = network.nodes[i];
                    CompManaNode b = network.nodes[j];
                    float radius = Mathf.Min(a.LinkRadius, b.LinkRadius);
                    if ((a.parent.Position - b.parent.Position).LengthHorizontalSquared <= radius * radius)
                        PlaceWorker_ManaNetwork.DrawLine(a.parent.TrueCenter(), b.parent.TrueCenter());
                }
            }
            foreach (Thing building in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (building.def.GetCompProperties<CompProperties_ManaNode>() == null)
                    PlaceWorker_ManaNetwork.DrawConnections(building.def, building.Position, map, building, false);
            }
        }

        public override void MapComponentOnGUI()
        {
            if (!Visible) return;
            using (TechLabDrawScope.Begin())
            {
                foreach (ManaNetwork network in MapComponent_ManaNetwork.For(map).Networks)
                    GenMapUI.DrawThingLabel(network.nodes[0].parent,
                        string.Format("WotF_ManaNetwork_Stored".Translate(),
                            network.Stored.ToString("0.#"), network.Capacity.ToString("0.#")));
            }
        }
    }
}
