using Verse;

namespace WhisperOfTheForest.Rituals
{
    public class CompProperties_TeleportNode : CompProperties
    {
        public CompProperties_TeleportNode()
        {
            compClass = typeof(CompTeleportNode);
        }
    }

    /// <summary>
    /// Marks a building as a local teleport destination. The node carries no state yet; the ritual
    /// only needs a stable way to recognise a valid destination.
    /// </summary>
    public class CompTeleportNode : ThingComp
    {
    }
}
