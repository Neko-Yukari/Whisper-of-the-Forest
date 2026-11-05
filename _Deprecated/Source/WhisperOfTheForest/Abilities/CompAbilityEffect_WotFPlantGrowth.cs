using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Abilities
{
    /// <summary>
    /// Thin psycast adapter. Vanilla <see cref="Psycast"/> owns the gizmo, warmup, line of sight,
    /// psyfocus and cooldown; this comp only rejects targets that are not an immature spawned plant
    /// and hands the plant to <see cref="PlantGrowthEffect"/>.
    /// </summary>
    public class CompAbilityEffect_WotFPlantGrowth : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            string failureReasonKey = PlantGrowthEffect.FailureReasonKey(target.Thing as Plant);
            if (failureReasonKey == null)
            {
                return true;
            }

            // Vanilla calls Valid untargeted every GUI frame for the cursor icon, so only the
            // real click attempt (throwMessages) is allowed to talk to the player.
            if (throwMessages)
            {
                Messages.Message(failureReasonKey.Translate(), MessageTypeDefOf.RejectInput, false);
            }

            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return PlantGrowthEffect.FailureReasonKey(target.Thing as Plant) == null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            string failureReasonKey;
            if (PlantGrowthEffect.TryMature(target.Thing as Plant, out failureReasonKey))
            {
                return;
            }

            // Normal play never reaches this: Valid already refused the target. A programmatic or
            // hot-reloaded cast still gets a readable reason instead of a silent no-op.
            Messages.Message(failureReasonKey.Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
