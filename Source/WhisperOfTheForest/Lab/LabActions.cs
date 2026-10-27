using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Missiles;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Public actions the experiment window (and lab commands) call. Every action is limited to
    /// the explicitly passed pawn; nothing here changes global behaviour or other pawns.
    /// </summary>
    public static class LabActions
    {
        public const string ArcaneMissilesDefName = "WotF_ArcaneMissiles";
        public const string ArcaneMissilesHeavyDefName = "WotF_ArcaneMissiles_Heavy";
        public const string ArcaneMissilesMenderDefName = "WotF_ArcaneMissiles_Mender";
        public const string TerrainStrideDefName = "WotF_TerrainStride";
        public const string BlinkStrideDefName = "WotF_BlinkStride";

        // Grant targets are discovered from loaded AbilityDefs that carry the missile comp, so a new
        // XML preset needs no C# list change. The constants above remain the default entry points.

        /// <summary>Last human-readable failure, for the window and the JSON lab result.</summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// Grants every loaded experiment psycast to the selected pawn: raises psylink to the highest
        /// required level, fills psyfocus, then learns each missing ability. Only the passed pawn
        /// changes. Throws InvalidOperationException on failure so the UI can surface the reason.
        /// </summary>
        public static void GrantPsycast(Pawn pawn)
        {
            ThrowIfFailed(TryGrantPsycast(pawn));
        }

        public static bool TryGrantPsycast(Pawn pawn)
        {
            return Run(pawn, "grant-psycast", delegate(Pawn target)
            {
                List<AbilityDef> abilityDefs = new List<AbilityDef>();
                foreach (AbilityDef def in DefDatabase<AbilityDef>.AllDefs)
                {
                    if (def == null || def.comps == null)
                    {
                        continue;
                    }

                    if (def.comps.OfType<CompProperties_AbilityWotFArcaneMissiles>().Any())
                    {
                        abilityDefs.Add(def);
                    }
                }

                if (abilityDefs.Count == 0)
                {
                    return FailMessage("no AbilityDef with CompProperties_AbilityWotFArcaneMissiles is loaded");
                }

                // Deterministic grant/report order regardless of DefDatabase order.
                abilityDefs.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));

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

                int requiredLevel = 1;
                foreach (AbilityDef def in abilityDefs)
                {
                    requiredLevel = Math.Max(requiredLevel, def.level);
                }

                if (psylink.level < requiredLevel)
                {
                    // ChangeLevel keeps vanilla level bookkeeping and psyfocus capacity in sync.
                    psylink.ChangeLevel(requiredLevel - psylink.level, false);
                }

                target.psychicEntropy.OffsetPsyfocusDirectly(1f);
                target.psychicEntropy.SetPsyfocusTarget(1f);

                for (int i = 0; i < abilityDefs.Count; i++)
                {
                    if (target.abilities.GetAbility(abilityDefs[i], true) == null)
                    {
                        target.abilities.GainAbility(abilityDefs[i]);
                    }
                }

                List<string> granted = new List<string>(abilityDefs.Count);
                for (int i = 0; i < abilityDefs.Count; i++)
                {
                    AbilityDef def = abilityDefs[i];
                    if (target.abilities.GetAbility(def, true) == null)
                    {
                        return FailMessage("ability " + def.defName + " still missing after GainAbility");
                    }

                    granted.Add(def.defName);
                }

                return "psylink=" + psylink.level + " psyfocus=" + target.psychicEntropy.CurrentPsyfocus.ToString("0.##") + " abilities=" + string.Join(",", granted.ToArray());
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
