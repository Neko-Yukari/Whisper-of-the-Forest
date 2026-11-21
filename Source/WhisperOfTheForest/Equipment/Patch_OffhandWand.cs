using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// Enforces at most one one-handed offhand wand (<c>equipmentType = None</c> +
    /// <see cref="CompProperties_SpellStaff.isOneHanded"/>) per pawn: equipping a second wand drops
    /// the previously held wand unforbidden beside the pawn, mirroring vanilla's Primary handling
    /// (<c>Verse/Pawn_EquipmentTracker.cs:125-143</c>).
    /// </summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.MakeRoomFor),
        new[] { typeof(ThingWithComps), typeof(ThingWithComps) },
        new[] { ArgumentType.Normal, ArgumentType.Out })]
    internal static class Patch_EquipmentTracker_MakeRoomFor
    {
        private static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref ThingWithComps dropped)
        {
            CompSpellStaff incoming = eq?.GetComp<CompSpellStaff>();
            if (incoming == null || !incoming.Props.isOneHanded)
            {
                return;
            }

            List<ThingWithComps> all = __instance.AllEquipmentListForReading;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                ThingWithComps existing = all[i];
                if (existing == eq)
                {
                    continue;
                }

                CompSpellStaff staff = existing.GetComp<CompSpellStaff>();
                if (staff == null || !staff.Props.isOneHanded)
                {
                    continue;
                }

                if (__instance.TryDropEquipment(existing, out ThingWithComps droppedWand, __instance.pawn.Position, forbid: false))
                {
                    if (droppedWand != null)
                    {
                        droppedWand.SetForbidden(false, warnOnFail: false);
                        dropped = droppedWand;
                    }
                }
                else
                {
                    WotfLog.Warning(StaffDefs.LogModule,
                        __instance.pawn + " couldn't make room for offhand wand " + eq);
                }
            }
        }
    }

    /// <summary>
    /// Right-clicking a selected pawn carrying an offhand wand offers "Drop &lt;wand&gt;" alongside
    /// vanilla's <c>FloatMenuOptionProvider_DropEquipment</c> (which only inspects <c>Primary</c>).
    /// Discovered automatically by <c>FloatMenuMakerMap.Init</c>.
    /// </summary>
    public class FloatMenuOptionProvider_DropOffhandWand : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool CanSelfTarget => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            return CompSpellStaff.FindOffhand(context.FirstSelectedPawn) != null;
        }

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn != context.FirstSelectedPawn)
            {
                return null;
            }

            ThingWithComps wand = CompSpellStaff.FindOffhand(clickedPawn)?.parent;
            if (wand == null)
            {
                return null;
            }

            if (clickedPawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanUnequip(wand, clickedPawn))
            {
                return new FloatMenuOption(
                    "CannotDrop".Translate(wand.Label, wand) + ": " + "QuestRelated".Translate().CapitalizeFirst(),
                    null);
            }

            Action action = delegate
            {
                clickedPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.DropEquipment, wand), JobTag.Misc);
            };
            return new FloatMenuOption(
                "Drop".Translate(wand.Label, wand),
                action,
                wand,
                Color.white,
                MenuOptionPriority.Default,
                null,
                clickedPawn);
        }
    }

    /// <summary>
    /// Surfaces slot-selection gizmos for offhand one-handed wands (vanilla <c>GetGizmos</c> only
    /// calls <c>CompGetEquippedGizmosExtra</c> on <c>Primary</c>, <c>Pawn_EquipmentTracker.cs:233-239</c>),
    /// and disables two-handed staff attack commands when blocked by a worn shield belt.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos))]
    internal static class Patch_EquipmentTracker_GetGizmos
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_EquipmentTracker __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                if (gizmo is Command_VerbTarget cmd && cmd.verb is Verb_CastSpellStaff staffVerb)
                {
                    CompSpellStaff staff = staffVerb.EquipmentSource?.GetComp<CompSpellStaff>();
                    if (staff != null && staff.IsBlockedByShieldBelt(__instance.pawn))
                    {
                        cmd.Disable("WotF_Equipment.Reason.TwoHandedShieldBlock".Translate());
                    }
                }

                yield return gizmo;
            }

            List<ThingWithComps> list = __instance.AllEquipmentListForReading;
            ThingWithComps primary = __instance.Primary;
            for (int i = 0; i < list.Count; i++)
            {
                ThingWithComps eq = list[i];
                if (eq == primary)
                {
                    continue;
                }

                CompSpellStaff staff = eq.GetComp<CompSpellStaff>();
                if (staff == null || !staff.Props.isOneHanded)
                {
                    continue;
                }

                foreach (Gizmo extra in staff.CompGetEquippedGizmosExtra())
                {
                    yield return extra;
                }
            }
        }
    }

    /// <summary>
    /// Shows the vanilla "Fire at will" toggle for drafted colonists wielding an offhand wand solo
    /// (with no primary weapon), matching <c>Pawn_DraftController.cs:197-212</c>.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DraftController), "GetGizmos")]
    internal static class Patch_DraftController_GetGizmos
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_DraftController __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }

            if (__instance.Drafted
                && __instance.pawn.equipment?.Primary == null
                && CompSpellStaff.FindOffhand(__instance.pawn) != null)
            {
                yield return new Command_Toggle
                {
                    hotKey = KeyBindingDefOf.Misc6,
                    isActive = () => __instance.FireAtWill,
                    toggleAction = delegate { __instance.FireAtWill = !__instance.FireAtWill; },
                    icon = TexCommand.FireAtWill,
                    defaultLabel = "CommandFireAtWillLabel".Translate(),
                    defaultDesc = "CommandFireAtWillDesc".Translate(),
                    tutorTag = "FireAtWillToggle"
                };
            }
        }
    }

    /// <summary>
    /// When a pawn holds no Primary weapon and carries an available offhand wand, promote the wand's
    /// cast verb as the attack verb so solo-wand pawns auto-attack and right-click cast identically
    /// to a primary staff. When a Primary weapon is equipped (dual-wield), Primary keeps full control
    /// of auto-attacks while the offhand wand fires manually via its verb gizmo.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryGetAttackVerb))]
    internal static class Patch_Pawn_TryGetAttackVerb
    {
        private static void Postfix(Pawn __instance, ref Verb __result, bool allowManualCastWeapons)
        {
            if (__instance.equipment?.Primary != null)
            {
                return;
            }

            if (__result != null && !__result.verbProps.IsMeleeAttack)
            {
                return;
            }

            CompSpellStaff wand = CompSpellStaff.FindOffhand(__instance);
            Verb verb = wand?.PrimaryVerb;
            if (verb == null || !verb.Available())
            {
                return;
            }

            if (verb.verbProps.onlyManualCast && !allowManualCastWeapons
                && (__instance.CurJob == null || __instance.CurJob.def == JobDefOf.Wait_Combat))
            {
                return;
            }

            __result = verb;
        }
    }

    /// <summary>
    /// Lets drafted solo-wand pawns (<c>Primary == null</c>) qualify for right-click ranged attack
    /// orders in <c>FloatMenuOptionProvider_DraftedAttack</c> (<c>RimWorld/FloatMenuUtility.cs:150-157</c>).
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.UseRangedAttack))]
    internal static class Patch_FloatMenuUtility_UseRangedAttack
    {
        private static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result || pawn?.equipment?.Primary != null)
            {
                return;
            }

            Verb wandVerb = CompSpellStaff.FindOffhand(pawn)?.PrimaryVerb;
            if (wandVerb != null && !wandVerb.verbProps.IsMeleeAttack && wandVerb.Available())
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// Supplies the drafted right-click ranged attack action for solo-wand pawns (<c>Primary == null</c>),
    /// mirroring <c>RimWorld/FloatMenuUtility.cs:20-93</c> against the offhand wand's verb.
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetRangedAttackAction))]
    internal static class Patch_FloatMenuUtility_GetRangedAttackAction
    {
        private static void Postfix(Pawn pawn, LocalTargetInfo target, ref string failStr, ref Action __result)
        {
            if (__result != null || pawn?.equipment?.Primary != null)
            {
                return;
            }

            Verb wandVerb = CompSpellStaff.FindOffhand(pawn)?.PrimaryVerb;
            if (wandVerb == null || wandVerb.verbProps.IsMeleeAttack || !wandVerb.Available())
            {
                return;
            }

            failStr = "";
            if (!pawn.Drafted)
            {
                failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
            }
            else if (!pawn.IsColonistPlayerControlled && !pawn.IsColonyMech && !pawn.IsColonySubhumanPlayerControlled)
            {
                failStr = "CannotOrderNonControlledLower".Translate();
            }
            else if (pawn.IsColonyMechPlayerControlled && target.IsValid && !MechanitorUtility.InMechanitorCommandRange(pawn, target))
            {
                failStr = "OutOfCommandRange".Translate();
            }
            else if (target.IsValid && !wandVerb.CanHitTarget(target))
            {
                if (!pawn.Position.InHorDistOf(target.Cell, wandVerb.EffectiveRange))
                {
                    failStr = "OutOfRange".Translate();
                }
                else
                {
                    float minRange = wandVerb.verbProps.EffectiveMinRange(target, pawn);
                    failStr = (float)pawn.Position.DistanceToSquared(target.Cell) < minRange * minRange
                        ? (string)"TooClose".Translate()
                        : (string)"CannotHitTarget".Translate();
                }
            }
            else if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                failStr = "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn);
            }
            else if (pawn == target.Thing)
            {
                failStr = "CannotAttackSelf".Translate();
            }
            else if (target.Thing is Pawn sameFactionTarget
                && (pawn.InSameExtraFaction(sameFactionTarget, ExtraFactionType.HomeFaction)
                    || pawn.InSameExtraFaction(sameFactionTarget, ExtraFactionType.MiniFaction)))
            {
                failStr = "CannotAttackSameFactionMember".Translate();
            }
            else if (target.Thing is Pawn innocent
                && HistoryEventUtility.IsKillingInnocentAnimal(pawn, innocent)
                && !new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                failStr = "IdeoligionForbids".Translate();
            }
            else if (target.Thing is Pawn venerated && pawn.Ideo != null && pawn.Ideo.IsVeneratedAnimal(venerated)
                && !new HistoryEvent(HistoryEventDefOf.HuntedVeneratedAnimal, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                failStr = "IdeoligionForbids".Translate();
            }
            else
            {
                __result = delegate
                {
                    Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                };
                return;
            }

            failStr = failStr.CapitalizeFirst();
        }
    }

    /// <summary>
    /// Draws an equipped offhand wand alongside or in place of a primary weapon
    /// (<c>Verse/PawnRenderUtility.cs:212-251</c>). When solo-wielded (<c>Primary == null</c>),
    /// mirrors vanilla weapon aiming and carried placement; when dual-wielded, places the wand on
    /// the opposite hand/side with a subtle altitude offset to prevent overlap and z-fighting.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    internal static class Patch_PawnRenderUtility_DrawOffhandWand
    {
        private static readonly Vector3 OffhandNorth = new Vector3(-0.20f, -0.002f, -0.11f);
        private static readonly Vector3 OffhandEast = new Vector3(0.10f, -0.002f, -0.26f);
        private static readonly Vector3 OffhandSouth = new Vector3(-0.22f, 0.002f, -0.22f);
        private static readonly Vector3 OffhandWest = new Vector3(-0.10f, -0.002f, -0.26f);

        private static void Postfix(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
        {
            ThingWithComps wand = CompSpellStaff.FindOffhand(pawn)?.parent;
            if (wand == null)
            {
                return;
            }

            Job curJob = pawn.CurJob;
            if (curJob == null || curJob.def?.neverShowWeapon != false)
            {
                return;
            }

            bool hasPrimary = pawn.equipment?.Primary != null;
            Stance_Busy stanceBusy = pawn.stances?.curStance as Stance_Busy;
            float distFactor = pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
            bool aimingWithWand = !flags.HasFlag(PawnRenderFlags.NeverAimWeapon)
                && stanceBusy != null
                && !stanceBusy.neverAimWeapon
                && stanceBusy.focusTarg.IsValid
                && (!hasPrimary || stanceBusy.verb?.EquipmentSource == wand);

            if (aimingWithWand)
            {
                float aimAngle = 0f;
                Vector3 targetPos = stanceBusy.focusTarg.HasThing
                    ? stanceBusy.focusTarg.Thing.DrawPos
                    : stanceBusy.focusTarg.Cell.ToVector3Shifted();
                if ((targetPos - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
                {
                    aimAngle = (targetPos - pawn.DrawPos).AngleFlat();
                }

                Verb verb = stanceBusy.verb ?? pawn.CurrentEffectiveVerb;
                if (verb != null && verb.AimAngleOverride.HasValue)
                {
                    aimAngle = verb.AimAngleOverride.Value;
                }

                Vector3 aimPos = drawPos
                    + new Vector3(0f, 0f, 0.4f + wand.def.equippedDistanceOffset).RotatedBy(aimAngle) * distFactor;
                if (hasPrimary)
                {
                    aimPos.y += 0.002f;
                }

                PawnRenderUtility.DrawEquipmentAiming(wand, aimPos, aimAngle);
                return;
            }

            if (!PawnRenderUtility.CarryWeaponOpenly(pawn))
            {
                return;
            }

            if (!hasPrimary)
            {
                PawnRenderUtility.DrawCarriedWeapon(wand, drawPos, facing, distFactor);
                return;
            }

            DrawDualWieldOffhand(wand, drawPos, facing, distFactor);
        }

        private static void DrawDualWieldOffhand(ThingWithComps wand, Vector3 drawPos, Rot4 facing, float distFactor)
        {
            float angle;
            Vector3 offset;
            switch (facing.AsInt)
            {
                case 0:
                    offset = OffhandNorth;
                    angle = 217f;
                    break;
                case 1:
                    offset = OffhandEast;
                    angle = 130f;
                    break;
                case 2:
                    offset = OffhandSouth;
                    angle = 217f;
                    break;
                default:
                    offset = OffhandWest;
                    angle = 230f;
                    break;
            }

            Vector3 offhandPos = drawPos + new Vector3(offset.x * distFactor, offset.y, offset.z * distFactor);
            PawnRenderUtility.DrawEquipmentAiming(wand, offhandPos, angle);
        }
    }
}
