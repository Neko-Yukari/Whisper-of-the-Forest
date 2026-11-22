using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    public class Graphic_TallProp : Graphic_Single
    {
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            Vector2 size;
            bool flipUv;
            if (ShouldDrawRotated)
            {
                size = drawSize;
                flipUv = false;
            }
            else
            {
                size = thing.Rotation.IsHorizontal ? drawSize.Rotated() : drawSize;
                flipUv = (thing.Rotation == Rot4.West && WestFlipped) || (thing.Rotation == Rot4.East && EastFlipped);
            }

            if (thing.MultipleItemsPerCellDrawn())
            {
                size *= 0.8f;
            }

            float rot = AngleFromRot(thing.Rotation) + extraRotation;
            if (flipUv && data != null)
            {
                rot += data.flipExtraRotation;
            }

            Vector3 center = thing.TrueCenter() + DrawOffset(thing.Rotation);
            Material material = MatAt(thing.Rotation, thing);
            TryGetTextureAtlasReplacementInfo(material, thing.def.category.ToAtlasGroup(), flipUv, vertexColors: true, out material, out Vector2[] uvs, out Color32 vertexColor);
            Printer_Plane.PrintPlane(
                layer,
                center,
                size,
                material,
                rot,
                flipUv,
                uvs,
                new Color32[] { vertexColor, vertexColor, vertexColor, vertexColor },
                Plant.TopVerticesAltitudeBias);
            ShadowGraphic?.Print(layer, thing, 0f);
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_TallProp>(path, newShader, drawSize, newColor, newColorTwo, data);
        }
    }
}
