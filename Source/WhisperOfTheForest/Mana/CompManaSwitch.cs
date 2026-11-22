using RimWorld;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public sealed class CompProperties_ManaSwitch : CompProperties_Flickable
    {
        public CompProperties_ManaSwitch()
        {
            compClass = typeof(CompManaSwitch);
            commandLabelKey = "WotF_ManaSwitch_Label";
            commandDescKey = "WotF_ManaSwitch_Desc";
        }
    }

    /// <summary>
    /// Unified on/off switch for mana-network machines and lighting. Builds directly on RimWorld's
    /// flickable workflow while drawing the vanilla power-off badge for machines without
    /// <see cref="CompPowerTrader"/>.
    /// </summary>
    public sealed class CompManaSwitch : CompFlickable
    {
        private OverlayHandle? overlayOff;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RefreshOverlay();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            overlayOff = null;
            base.PostDeSpawn(map, mode);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == FlickedOnSignal || signal == FlickedOffSignal)
            {
                RefreshOverlay();
            }
        }

        private void RefreshOverlay()
        {
            if (!parent.Spawned || parent.Map == null)
            {
                return;
            }

            parent.Map.overlayDrawer.Disable(parent, ref overlayOff);
            if (parent.GetComp<CompPowerTrader>() == null && !SwitchIsOn)
            {
                overlayOff = parent.Map.overlayDrawer.Enable(parent, OverlayTypes.PowerOff);
            }
        }
    }

    public static class ManaSwitchUtility
    {
        public static bool IsSwitchedOn(Thing thing)
        {
            return thing?.TryGetComp<CompFlickable>()?.SwitchIsOn ?? true;
        }

        public static bool ShouldOperate(bool switchedOn, bool hasSupply)
        {
            return switchedOn && hasSupply;
        }
    }
}
