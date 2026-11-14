using RimWorld;
using RimWorld.Planet;
using Verse;
using WhisperOfTheForest.Rituals;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// Runs the ruin quest the host picked and points the player at the site it placed. The altar has
    /// already refused a second unclaimed ruin and proved the quest can find a tile, so the only
    /// failure left here is the engine refusing the world object, which is reported instead of hidden.
    /// </summary>
    public sealed class RitualEffect_SeekRuin : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            RuinSeekOption option = context.RuinOption;
            Site site = RuinSeeking.RunQuest(option, context.Map);
            if (site == null)
            {
                Messages.Message("WotF_RuinSeek_Failed".Translate(option.sitePart.LabelCap),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.LetterStack.ReceiveLetter("WotF_RuinSeek_LetterLabel".Translate(),
                "WotF_RuinSeek_LetterText".Translate(site.Label), LetterDefOf.PositiveEvent, site);
            WotfLog.Info(WotfLog.ModuleRitual, "sought " + option.sitePart.defName + " at tile " + site.Tile
                + " for " + context.Caster.LabelShort);
        }
    }
}
