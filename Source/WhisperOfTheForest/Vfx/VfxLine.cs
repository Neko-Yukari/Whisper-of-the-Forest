namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// One RimUnity <see cref="RimUnity.LineRenderer"/> owned by a railgun worker: a world-space ribbon
    /// whose colour comes from its gradient, so every layer of the effect can share one material.
    ///
    /// The renderer is camera-facing by design (RimUnity's <c>LineAlignment.View</c>), which is what makes
    /// a two-point line read as a beam from every angle.
    /// </summary>
    internal sealed class VfxLine
    {
        private readonly RimUnity.GameObject owner;
        private readonly RimUnity.LineRenderer line;
        private bool visible = true;

        public VfxLine(string name, int layer, RimUnity.Material material, int pointCount)
        {
            owner = new RimUnity.GameObject(name);
            owner.layer = layer;
            line = owner.AddComponent<RimUnity.LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = pointCount;
            line.alignment = RimUnity.LineAlignment.View;
            owner.SetActive(true);
        }

        public void SetPoint(int index, RimUnity.Vector3 point)
        {
            line.positions[index] = point;
        }

        public void SetWidth(float cells)
        {
            line.widthMultiplier = cells;
        }

        /// <summary>Uniform colour; the renderer interpolates it across the whole ribbon.</summary>
        public void SetColor(RimUnity.Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        public void SetCapVertices(int vertices)
        {
            line.numCapVertices = vertices;
        }

        public void SetActive(bool value)
        {
            if (visible == value)
            {
                return;
            }

            visible = value;
            owner.SetActive(value);
        }

        public void Destroy()
        {
            RimUnity.Object.Destroy(owner);
        }
    }
}
