using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;

namespace WhisperOfTheForest.Teleport
{
    /// <summary>
    /// One gate's data. The same comp serves both ends: the colony gate draws upkeep from the mana
    /// network and is the only end that may be chosen as a destination, while the ruin's broken gate
    /// stands off the network, pays nothing and dies after the one connection home (设定/05,
    /// 技术/04). Which end a building is comes from this Def, not from code.
    /// </summary>
    public class CompProperties_TeleportGate : CompProperties
    {
        /// <summary>The ruin's broken gate: no network, no upkeep, one use.</summary>
        public bool ruined;

        /// <summary>Distance to the mana node that feeds it, the same rule every other consumer uses.</summary>
        public float linkRadius;

        /// <summary>Mana per game second while the gate stands open; the player closes it by flicking.</summary>
        public float upkeepPerSecond;

        /// <summary>Fixed fee for one remote connection, charged before anything moves.</summary>
        public float remoteStartCost;

        /// <summary>Fee per pawn carried.</summary>
        public float remoteCostPerPawn;

        /// <summary>Fee per kilogram of cargo; pawns are charged per head instead of by mass.</summary>
        public float remoteCostPerKg;

        /// <summary>
        /// Cells around this gate within which everything that crosses to it lands, one thing per cell.
        /// Read from the destination gate, so a gate decides how wide its own arrivals spread.
        /// </summary>
        public float arrivalRadius;

        /// <summary>
        /// Cells within which colonists carried here by the local teleport rite land. Vanilla's
        /// CellFinder measures it from a standable cell next to the footprint, not from the gate's centre
        /// (CellFinder.cs:219-233), so the reach from the centre is this plus the footprint.
        /// </summary>
        public int riteArrivalRadius;

        public CompProperties_TeleportGate()
        {
            compClass = typeof(CompTeleportGate);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (arrivalRadius <= 0f)
            {
                yield return "a gate needs a positive arrivalRadius: it is where connections land";
            }

            if (ruined)
            {
                yield break;
            }

            if (riteArrivalRadius <= 0)
            {
                yield return "a working gate needs a positive riteArrivalRadius for the local teleport rite";
            }

            if (linkRadius <= 0f)
            {
                yield return "a working gate needs a positive linkRadius";
            }

            if (upkeepPerSecond <= 0f)
            {
                yield return "a working gate needs a positive upkeepPerSecond: an open gate is never free";
            }

            if (remoteStartCost < 0f || remoteCostPerPawn < 0f || remoteCostPerKg < 0f)
            {
                yield return "gate fees cannot be negative";
            }
        }
    }

    /// <summary>
    /// The gate's open/closed state and its two menu actions. Contents live in the building's own
    /// <see cref="CompTransporter"/>, loaded exactly as any other transporter is loaded.
    ///
    /// "Running" is decided per tick, not cached for a while: the gate is open when the player has
    /// flicked it on, a mana node is within <c>linkRadius</c>, and this tick's upkeep came out of the
    /// network. A gate whose network went dry or was cut therefore stops being a destination on the
    /// next tick, which is what "断魔或停机时不能被远程连接" asks for.
    /// </summary>
    public class CompTeleportGate : ThingComp
    {
        private bool upkeepPaid;

        public CompProperties_TeleportGate Props => (CompProperties_TeleportGate)props;

        public CompTransporter Transporter => parent.GetComp<CompTransporter>();

        public bool Ruined => Props.ruined;

        public bool SwitchedOn => !Ruined && ManaSwitchUtility.IsSwitchedOn(parent);

        public ManaNetwork Network => Ruined || !parent.Spawned
            ? null
            : MapComponent_ManaNetwork.For(parent.Map)?.NetworkNear(parent.Position, Props.linkRadius);

        public bool Running => SwitchedOn && upkeepPaid;

        /// <summary>Only an open colony gate can be the far end of a connection.</summary>
        public bool CanAcceptRemote => !Ruined && parent.Spawned && Running;

        public override void CompTick()
        {
            if (Ruined || !parent.Spawned)
            {
                return;
            }

            if (!SwitchedOn)
            {
                upkeepPaid = false;
                return;
            }

            ManaNetwork network = Network;
            upkeepPaid = network != null && network.TryDraw(Props.upkeepPerSecond / 60f);
        }

        /// <summary>Why the gate is or is not usable, in the words the player sees.</summary>
        public string StateText()
        {
            if (Ruined)
            {
                return (string)"WotF_TeleportGate_Inspect_Ruined".Translate();
            }

            if (!SwitchedOn)
            {
                return (string)"WotF_TeleportGate_Inspect_Closed".Translate();
            }

            if (Network == null)
            {
                return (string)"WotF_TeleportGate_Inspect_Unlinked".Translate(Props.linkRadius.ToString("0.#"));
            }

            if (!upkeepPaid)
            {
                return (string)"WotF_TeleportGate_Inspect_Starved".Translate(Props.upkeepPerSecond.ToString("0.#"));
            }

            return (string)"WotF_TeleportGate_Inspect_Running".Translate(Props.upkeepPerSecond.ToString("0.#"));
        }

        public override string CompInspectStringExtra()
        {
            string text = StateText();
            CompTransporter transporter = Transporter;
            if (transporter == null)
            {
                return text;
            }

            int pawns = TeleportGateTransfer.PawnCount(transporter);
            float mass = TeleportGateTransfer.CargoMass(transporter);
            if (pawns == 0 && mass <= 0.0001f)
            {
                return text;
            }

            return text + "\n" + (string)"WotF_TeleportGate_Inspect_Contents".Translate(pawns, mass.ToString("0.#"));
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = (Ruined
                    ? (string)"WotF_TeleportGate_Gizmo_Connect".Translate()
                    : (string)"WotF_TeleportGate_Gizmo_Send".Translate()),
                defaultDesc = (Ruined
                    ? (string)"WotF_TeleportGate_Gizmo_ConnectDesc".Translate()
                    : (string)"WotF_TeleportGate_Gizmo_SendDesc".Translate()),
                icon = CompLaunchable.LaunchCommandTex,
                action = delegate { Find.WindowStack.Add(new FloatMenu(TeleportGateTransfer.Options(this))); }
            };
        }

        /// <summary>The same menu on right-click, pointed at the selected colonist.</summary>
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            return TeleportGateTransfer.Options(this);
        }
    }
}
