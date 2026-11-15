using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaBattery : CompProperties
    {
        public float capacity;
        public float linkRadius;
        public Vector3 barOffset;
        public Vector2 barSize;
        public float barMargin;
        public Color barFilledColor;
        public Color barUnfilledColor;

        public CompProperties_ManaBattery()
        {
            compClass = typeof(CompManaBattery);
        }

        // Ensures: storage and visible bar dimensions are valid before drawing; values live only in the Def.
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (capacity <= 0f) yield return "capacity must be positive";
            if (linkRadius <= 0f) yield return "linkRadius must be positive";
            if (barOffset.y <= 0f) yield return "barOffset.y must be positive to draw above the building";
            if (barSize.x <= 0f || barSize.y <= 0f) yield return "barSize dimensions must be positive";
            if (barMargin < 0f) yield return "barMargin must not be negative";
            if (barFilledColor.a <= 0f) yield return "barFilledColor must be visible";
            if (barUnfilledColor.a <= 0f) yield return "barUnfilledColor must be visible";
        }
    }

    /// <summary>
    /// The only place mana is stored. A battery joins the network of the nearest node within its own
    /// link radius; it never links batteries or devices to each other, that is the node's job.
    /// </summary>
    public class CompManaBattery : ThingComp
    {
        private float stored;
        private ManaNetwork network;

        public CompProperties_ManaBattery Props => (CompProperties_ManaBattery)props;

        public float Stored => stored;
        public float Capacity => Props.capacity;
        public float LinkRadius => Props.linkRadius;

        /// <summary>Null while no node is in reach; the charge stays in the battery meanwhile.</summary>
        public ManaNetwork Network
        {
            get => network;
            internal set => network = value;
        }

        // Ensures: the map's network graph is marked dirty so this battery is attached on the next rebuild.
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            MapComponent_ManaNetwork.For(parent.Map).MarkDirty();
        }

        // Ensures: the previous map's graph is marked dirty; the stored charge is lost with the building.
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            stored = 0f;
            network = null;
            MapComponent_ManaNetwork.For(previousMap)?.MarkDirty();
        }

        // Ensures: stored is saved under "manaStored".
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref stored, "manaStored", 0f);
        }

        // Ensures: stored is clamped to [0, Capacity].
        internal void SetStored(float value)
        {
            stored = value < 0f ? 0f : value > Capacity ? Capacity : value;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Rot4 rotation = parent.Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            Vector3 center = parent.DrawPos + Props.barOffset;
            center.y += 0.1f;
            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
            {
                center = center,
                size = Props.barSize,
                fillPercent = Stored / Capacity,
                filledMat = SolidColorMaterials.SimpleSolidColorMaterial(Props.barFilledColor),
                unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(Props.barUnfilledColor),
                margin = Props.barMargin,
                rotation = rotation
            });
            
            // 调整渲染层级：使用 Graphic.Draw 或通过设置材质渲染队列
            // 在这里只需设置材质的渲染队列，使其高于基础渲染层，或者在 DrawPos 上方增加 offset
        }

        // Ensures: shows stored/capacity and whether the battery is linked to a network.
        public override string CompInspectStringExtra()
        {
            ManaNetwork linked = parent.Spawned
                ? MapComponent_ManaNetwork.For(parent.Map).NetworkNear(parent.Position, LinkRadius) : null;
            return string.Format("WotF_ManaBattery_Inspect".Translate(), Stored.ToString("0"), Capacity.ToString("0"))
                + "\n" + (linked == null ? "WotF_ManaProducer_Unlinked".Translate().ToString()
                    : string.Format("WotF_ManaProducer_Linked".Translate(), linked.nodes.Count));
        }
    }
}
