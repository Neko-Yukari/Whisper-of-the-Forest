using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Public actions the lab commands call. Every action is limited to the explicitly passed pawn;
    /// nothing here changes global behaviour or other pawns.
    /// </summary>
    public static class LabActions
    {
        public const string TerrainStrideDefName = "WotF_TerrainStride";
        public const string BlinkStrideDefName = "WotF_BlinkStride";

        /// <summary>Last human-readable failure, for the lab JSON result.</summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// Raises the selected pawn to psylink level 1 and fills its psyfocus, which is the only thing
        /// the spellbook needs from Royalty: <see cref="Spells.SpellService"/> then grants the carrier
        /// ability on its own. Only the passed pawn changes.
        /// </summary>
        public static bool TryGrantPsylink(Pawn pawn)
        {
            return Run(pawn, "grant-psylink", delegate(Pawn target)
            {
                if (HediffDefOf.PsychicAmplifier == null || target.psychicEntropy == null || target.abilities == null)
                {
                    return FailMessage("Royalty psycast system is not loaded");
                }

                Hediff_Psylink psylink = target.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier, false) as Hediff_Psylink;
                if (psylink != null && psylink.Part == null)
                {
                    // A hediff created by an earlier buggy grant can have no part; repair it instead
                    // of leaving a broken implant that throws again on PostAdd.
                    target.health.RemoveHediff(psylink);
                    psylink = null;
                }

                if (psylink == null)
                {
                    // The vanilla implant is a brain hediff: Hediff_Psylink.PostAdd dereferences Part,
                    // so MakeHediff and AddHediff must both receive the brain part.
                    BodyPartRecord brain = target.health.hediffSet.GetBrain();
                    if (brain == null)
                    {
                        return FailMessage("no brain body part on " + target.LabelShort);
                    }

                    Hediff created = HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, target, brain);
                    psylink = created as Hediff_Psylink;
                    if (psylink == null)
                    {
                        return FailMessage("could not create psylink hediff on " + target.LabelShort);
                    }

                    target.health.AddHediff(psylink, brain, null, null);
                }

                if (psylink.level < 1)
                {
                    // ChangeLevel keeps vanilla level bookkeeping and psyfocus capacity in sync.
                    psylink.ChangeLevel(1 - psylink.level, false);
                }

                target.psychicEntropy.OffsetPsyfocusDirectly(1f);
                target.psychicEntropy.SetPsyfocusTarget(1f);
                return "psylink=" + psylink.level + " psyfocus=" + target.psychicEntropy.CurrentPsyfocus.ToString("0.##");
            });
        }

        public static void EquipTerrainStride(Pawn pawn)
        {
            ThrowIfFailed(TryEquipTerrainStride(pawn));
        }

        public static bool TryEquipTerrainStride(Pawn pawn)
        {
            return Equip(pawn, TerrainStrideDefName);
        }

        public static void EquipBlinkStride(Pawn pawn)
        {
            ThrowIfFailed(TryEquipBlinkStride(pawn));
        }

        public static bool TryEquipBlinkStride(Pawn pawn)
        {
            return Equip(pawn, BlinkStrideDefName);
        }

        /// <summary>Drops only the two experiment apparel defs; all other gear is untouched.</summary>
        public static void RemoveExperimentApparel(Pawn pawn)
        {
            ThrowIfFailed(TryRemoveExperimentApparel(pawn));
        }

        public static bool TryRemoveExperimentApparel(Pawn pawn)
        {
            return Run(pawn, "remove-apparel", delegate(Pawn target)
            {
                List<Apparel> worn = target.apparel.WornApparel;
                int removed = 0;
                for (int i = worn.Count - 1; i >= 0; i--)
                {
                    Apparel apparel = worn[i];
                    if (apparel == null || apparel.def == null || !IsExperimentApparel(apparel.def.defName))
                    {
                        continue;
                    }

                    if (!target.apparel.TryDrop(apparel))
                    {
                        target.apparel.Remove(apparel);
                        apparel.Destroy(DestroyMode.Vanish);
                    }

                    removed++;
                }

                return removed == 0 ? "no experiment apparel worn" : "removed=" + removed;
            });
        }

        private static bool Equip(Pawn pawn, string defName)
        {
            return Run(pawn, "equip:" + defName, delegate(Pawn target)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    return FailMessage("apparel def " + defName + " is not loaded");
                }

                foreach (Apparel worn in target.apparel.WornApparel)
                {
                    if (worn != null && worn.def == def)
                    {
                        return "already wearing " + defName;
                    }
                }

                if (!(ThingMaker.MakeThing(def, null) is Apparel apparel))
                {
                    return FailMessage(defName + " did not create Apparel");
                }

                target.apparel.Wear(apparel, false, false);
                if (!target.apparel.WornApparel.Contains(apparel))
                {
                    apparel.Destroy(DestroyMode.Vanish);
                    return FailMessage("Wear rejected " + defName);
                }

                return "wearing " + defName;
            });
        }

        private static bool IsExperimentApparel(string defName)
        {
            return defName == TerrainStrideDefName || defName == BlinkStrideDefName;
        }

        private static bool Run(Pawn pawn, string action, Func<Pawn, string> body)
        {
            LastError = null;
            if (pawn == null)
            {
                return Fail("no pawn selected");
            }

            if (!pawn.Spawned || pawn.Map == null)
            {
                return Fail("pawn is not spawned on a map");
            }

            if (pawn.Dead)
            {
                return Fail("pawn is dead");
            }

            string detail;
            try
            {
                detail = body(pawn);
            }
            catch (Exception exception)
            {
                Log.Error("[WotF-Lab] " + action + " failed: " + exception);
                return Fail(action + " threw: " + exception.Message);
            }

            if (detail == null || detail.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                return Fail(detail != null ? detail.Substring("ERROR:".Length).Trim() : action + " failed");
            }

            ExperimentLog.Record("lab", pawn, action + " ok: " + detail);
            return true;
        }

        private static bool Fail(string message)
        {
            LastError = message;
            ExperimentLog.Record("lab", null, "action failed: " + message);
            return false;
        }

        private static void ThrowIfFailed(bool ok)
        {
            if (!ok)
            {
                throw new InvalidOperationException(LastError ?? "experiment action failed");
            }
        }

        private static string FailMessage(string message)
        {
            return "ERROR:" + message;
        }
    }
}
