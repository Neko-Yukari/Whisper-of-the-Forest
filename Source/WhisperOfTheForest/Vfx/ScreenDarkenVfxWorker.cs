using RimUnity;
using RimUnity.ScreenEfx;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>Runs one exported selective-darkening material for a fixed number of game ticks.</summary>
    public class ScreenDarkenVfxWorker : VfxWorker
    {
        private ScreenDarkenVfxDef def;
        private int startTick;
        private EfxHandle handle;

        protected override void OnStart()
        {
            def = (ScreenDarkenVfxDef)Def;
            startTick = Find.TickManager.TicksGame;
            Material template = RimUnity.Assets.AssetLoader.Load<Material>(def.modContentPack, def.materialPath);
            handle = EfxPlayer.Play(new SelectiveDarkenClip(template, def.strength, def.keepLow, def.keepHigh));
        }

        public override void Update(float deltaSeconds)
        {
            if (Find.TickManager.TicksGame - startTick >= def.durationTicks)
            {
                Finished = true;
            }
        }

        protected override void Release()
        {
            handle?.Stop();
            handle = null;
        }
    }

    /// <summary>
    /// One ScreenEfx clip. The clip owns its material copy until RimUnity calls <see cref="OnRelease"/>;
    /// stopping the handle must not destroy the material while queued frames still reference it.
    /// </summary>
    internal sealed class SelectiveDarkenClip : EfxClip
    {
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly float strength;
        private readonly float keepLow;
        private readonly float keepHigh;

        public SelectiveDarkenClip(Material template, float strength, float keepLow, float keepHigh)
        {
            material = new Material(template);
            this.strength = strength;
            this.keepLow = keepLow;
            this.keepHigh = keepHigh;
        }

        public override void BuildFrame(EfxFrameBuilder frame)
        {
            properties.SetFloat("_Strength", strength);
            properties.SetFloat("_KeepLow", keepLow);
            properties.SetFloat("_KeepHigh", keepHigh);
            frame.AddPass(material, 0, properties);
        }

        protected override void OnRelease()
        {
            RimUnity.Object.Destroy(material);
        }
    }
}
