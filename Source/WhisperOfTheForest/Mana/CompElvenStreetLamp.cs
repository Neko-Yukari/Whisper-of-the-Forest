using System.Collections.Generic;
using RimLumen.Core.Lighting;
using RimWorld;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Mana
{
    public sealed class CompProperties_ElvenStreetLamp : CompProperties
    {
        public float linkRadius;
        public float manaPerSecond;
        public VfxDef vfxDef;

        public CompProperties_ElvenStreetLamp()
        {
            compClass = typeof(CompElvenStreetLamp);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (linkRadius <= 0f)
            {
                yield return "linkRadius must be positive";
            }

            if (manaPerSecond <= 0f)
            {
                yield return "manaPerSecond must be positive";
            }

            if (!(vfxDef is StreetLampLumenVfxDef))
            {
                yield return "vfxDef must resolve to a StreetLampLumenVfxDef";
            }

            if (parentDef.GetCompProperties<CompProperties_RimLumenLogicalLight>() == null)
            {
                yield return "elven streetlamp requires CompProperties_RimLumenLogicalLight";
            }
        }
    }

    public sealed class CompElvenStreetLamp : ThingComp, IThingGlower
    {
        private bool lit;
        private VfxHandle vfxHandle;

        public CompProperties_ElvenStreetLamp Props => (CompProperties_ElvenStreetLamp)props;

        public bool SwitchedOn => ManaSwitchUtility.IsSwitchedOn(parent);

        public bool IsLit => lit;

        public ManaNetwork Network => !parent.Spawned
            ? null
            : MapComponent_ManaNetwork.For(parent.Map)?.NetworkNear(parent.Position, Props.linkRadius);

        public bool ShouldBeLitNow() => lit;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lit, "lit", true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ManaNetwork network = Network;
                lit = ManaSwitchUtility.ShouldOperate(SwitchedOn, network != null && network.Stored >= Props.manaPerSecond / 60f);
            }
            else if (!SwitchedOn)
            {
                lit = false;
            }

            parent.BroadcastCompSignal(lit ? CompPowerTrader.PowerTurnedOnSignal : CompPowerTrader.PowerTurnedOffSignal);
            StartVfx();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }

            if (!SwitchedOn)
            {
                SetLit(false);
                return;
            }

            ManaNetwork network = Network;
            bool paid = network != null && network.TryDraw(Props.manaPerSecond / 60f);
            SetLit(paid);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == CompFlickable.FlickedOffSignal)
            {
                SetLit(false);
            }
            else if (signal == CompFlickable.FlickedOnSignal && parent.Spawned)
            {
                ManaNetwork network = Network;
                SetLit(network != null && network.Stored >= Props.manaPerSecond / 60f);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            StopVfx();
            lit = false;
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            StopVfx();
            lit = false;
            base.PostDestroy(mode, previousMap);
        }

        public override string CompInspectStringExtra()
        {
            if (!SwitchedOn)
            {
                return "WotF_ManaSwitch_Off".Translate();
            }

            if (Network == null)
            {
                return "WotF_ManaProducer_Unlinked".Translate();
            }

            if (!lit)
            {
                return string.Format("WotF_ElvenStreetLamp_NoMana".Translate(), Props.manaPerSecond.ToString("0.###"));
            }

            return string.Format("WotF_ElvenStreetLamp_Lit".Translate(), Props.manaPerSecond.ToString("0.###"));
        }

        private void SetLit(bool value)
        {
            if (lit == value)
            {
                return;
            }

            lit = value;
            if (parent.Spawned)
            {
                parent.BroadcastCompSignal(lit ? CompPowerTrader.PowerTurnedOnSignal : CompPowerTrader.PowerTurnedOffSignal);
            }
        }

        private void StartVfx()
        {
            if (vfxHandle != null || !parent.Spawned || parent.Map == null || Props.vfxDef == null)
            {
                return;
            }

            vfxHandle = VfxService.Play(new VfxRequest
            {
                vfxDefName = Props.vfxDef.defName,
                map = parent.Map,
                origin = parent.DrawPos,
                follow = parent
            });
        }

        private void StopVfx()
        {
            if (vfxHandle == null)
            {
                return;
            }

            vfxHandle.End();
            vfxHandle = null;
        }
    }
}
