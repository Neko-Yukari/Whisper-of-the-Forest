using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Abilities
{
    /// <summary>
    /// Growth-only plant mutation shared by every trigger that wants to finish a plant on the spot.
    /// It needs no Ability, projectile or lab: anything holding a spawned <see cref="Plant"/> on a
    /// map can call <see cref="TryMature"/> directly.
    /// </summary>
    public static class PlantGrowthEffect
    {
        /// <summary>
        /// Returns null when the plant can be matured, otherwise the key of a localised, player-facing
        /// failure message. A null plant means the clicked target was not a plant at all.
        /// </summary>
        public static string FailureReasonKey(Plant plant)
        {
            if (plant == null || plant.def?.plant == null)
            {
                return "WotF_MaturePlant_Failed_NotPlant";
            }

            if (plant.Destroyed || !plant.Spawned || plant.Map == null)
            {
                return "WotF_MaturePlant_Failed_Unavailable";
            }

            // Vanilla: growthInt > 0.999 is PlantLifeStage.Mature.
            if (plant.LifeStage == PlantLifeStage.Mature)
            {
                return "WotF_MaturePlant_Failed_AlreadyMature";
            }

            return null;
        }

        /// <summary>
        /// Sets the vanilla growth field to full. Only growth changes: hit points, diseases, age,
        /// stack count and the def's harvest rules stay untouched, so a plant the def does not allow
        /// to be harvested still cannot be harvested. Returns false with a failure key otherwise.
        /// </summary>
        public static bool TryMature(Plant plant, out string failureReasonKey)
        {
            failureReasonKey = FailureReasonKey(plant);
            if (failureReasonKey != null)
            {
                return false;
            }

            // Plant.set_Growth clamps to 0..1 and clears the cached mouseover label.
            plant.Growth = 1f;

            // Vanilla repaints only cultivated plants while they grow; do it unconditionally so a
            // wild tree or an ignored crop switches to the mature graphic immediately.
            plant.Map.mapDrawer.MapMeshDirty(plant.Position, MapMeshFlagDefOf.Things);

            return true;
        }
    }
}
