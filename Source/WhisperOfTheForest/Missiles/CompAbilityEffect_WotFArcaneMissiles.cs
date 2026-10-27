using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// XML side of the arcane missile psycast. Everything gameplay- or look-related sits in
    /// <see cref="settings"/> so the Def stays declarative and the lab can reuse the same struct
    /// with different numbers.
    /// </summary>
    public class CompProperties_AbilityWotFArcaneMissiles : CompProperties_AbilityEffect
    {
        public ArcaneMissileSettings settings = new ArcaneMissileSettings();

        public CompProperties_AbilityWotFArcaneMissiles()
        {
            compClass = typeof(CompAbilityEffect_WotFArcaneMissiles);
        }
    }

    /// <summary>
    /// Real psycast effect: vanilla Psycast handles gizmo, warmup, line of sight, psyfocus and
    /// entropy; this comp only turns the resolved target into a missile volley.
    /// </summary>
    public class CompAbilityEffect_WotFArcaneMissiles : CompAbilityEffect
    {
        public new CompProperties_AbilityWotFArcaneMissiles Props =>
            (CompProperties_AbilityWotFArcaneMissiles)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null)
            {
                return;
            }

            ArcaneMissileLauncher.Fire(caster, target, Props.settings);
        }
    }
}
