using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Scans a map for active hostile psychic rituals (all Anomaly ritual types, not just
    /// skip-abduction), cancels them and stuns every assigned participant.
    /// </summary>
    public static class PsychicRitualDisruption
    {
        /// <summary>All live, uncanceled, unfinished hostile psychic rituals on <paramref name="map"/>.</summary>
        public static List<PsychicRitual> FindHostileRituals(Map map)
        {
            var results = new List<PsychicRitual>();
            List<Lord> lords = map?.lordManager?.lords;
            if (lords == null)
            {
                return results;
            }

            for (int i = 0; i < lords.Count; i++)
            {
                Lord lord = lords[i];
                if (lord?.faction == null || !lord.faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                if (lord.CurLordToil is LordToil_PsychicRitual toil)
                {
                    PsychicRitual ritual = toil.RitualData?.psychicRitual;
                    if (ritual != null && !ritual.canceled && !ritual.succeeded)
                    {
                        results.Add(ritual);
                    }
                }
            }

            return results;
        }

        /// <summary>True when at least one active hostile psychic ritual is running on <paramref name="map"/>.</summary>
        public static bool HasHostileRitual(Map map)
        {
            return FindHostileRituals(map).Count > 0;
        }

        /// <summary>
        /// Cancels every active hostile psychic ritual on <paramref name="map"/> and stuns each
        /// assigned participant once for <paramref name="stunTicks"/>.
        /// </summary>
        public static void DisruptHostileRituals(
            Map map,
            Pawn host,
            int stunTicks,
            out int canceledCount,
            out int stunnedCount)
        {
            // Snapshot lords/rituals first: CancelPsychicRitual removes the lord from map.lordManager.lords.
            List<PsychicRitual> rituals = FindHostileRituals(map);
            canceledCount = 0;
            var stunned = new HashSet<Pawn>();
            string hostName = host != null ? host.LabelShort : string.Empty;
            TaggedString cancelReason = string.Format("WotF_Ritual_CounterPsychic_CancelReason".Translate(), hostName);

            for (int i = 0; i < rituals.Count; i++)
            {
                PsychicRitual ritual = rituals[i];
                if (ritual.canceled || ritual.succeeded)
                {
                    continue;
                }

                if (ritual.assignments != null)
                {
                    // Snapshot participants before stunning/canceling so collection mutation cannot skip entries.
                    var participants = new List<Pawn>(ritual.assignments.AllAssignedPawns);
                    for (int j = 0; j < participants.Count; j++)
                    {
                        Pawn participant = participants[j];
                        if (participant == null || participant.Dead || !participant.Spawned
                            || participant.stances?.stunner == null)
                        {
                            continue;
                        }

                        if (stunned.Add(participant))
                        {
                            participant.stances.stunner.StunFor(stunTicks, host, addBattleLog: true);
                        }
                    }
                }

                ritual.CancelPsychicRitual(cancelReason);
                canceledCount++;
            }

            stunnedCount = stunned.Count;
        }
    }
}
