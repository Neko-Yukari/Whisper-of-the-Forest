using System.Collections.Generic;
using System.Linq;
using RimWorld;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Spells;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Turrets
{
    public class Building_SpellTurret : Building_TurretGun
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                yield return gizmo;
            if (Faction != Faction.OfPlayer)
                yield break;
            yield return new Command_Action
            {
                defaultLabel = SpellText.T("WotF_Turret_SelectSpell"),
                defaultDesc = SpellText.T("WotF_Turret_SelectSpellDesc"),
                icon = SpellbookStyle.GlyphIcon(SpellDefs.Glyph(SpellDefs.FormProjectile)),
                action = () =>
                {
                    CompSpellTurret comp = GetComp<CompSpellTurret>();
                    if (!comp.GetInscriptionCandidates().Any())
                        Messages.Message(SpellText.T("WotF_Turret_NoCandidates"), MessageTypeDefOf.RejectInput, false);
                    else
                        Find.WindowStack.Add(new FloatMenu_TurretSpells(comp));
                }
            };
            yield return new Command_Action
            {
                defaultLabel = SpellText.T("WotF_Turret_NewSpell"),
                defaultDesc = SpellText.T("WotF_Turret_ComposeHint"),
                icon = SpellbookStyle.GlyphIcon(SpellDefs.Glyph(SpellDefs.FormProjectile)),
                action = () => Find.WindowStack.Add(new Window_TurretSpell(GetComp<CompSpellTurret>()))
            };
        }

        public override LocalTargetInfo TryFindNewTarget()
        {
            if (!ManaSwitchUtility.IsSwitchedOn(this))
                return LocalTargetInfo.Invalid;
            CompSpellTurret comp = GetComp<CompSpellTurret>();
            if (!comp.HasSpell)
                return LocalTargetInfo.Invalid;
            TargetScanFlags flags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (!AttackVerb.ProjectileFliesOverhead())
                flags |= TargetScanFlags.NeedLOSToAll | TargetScanFlags.LOSBlockableByGas;
            if (AttackVerb.IsIncendiary_Ranged())
                flags |= TargetScanFlags.NeedNonBurning;
            Thing target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, flags, IsAllowedTarget);
            if (!comp.TargetNeutral && !comp.TargetFriendly)
                return target;

            // Vanilla's hostility gate cannot be disabled. Keep its enemy selection, then consider
            // non-hostile pawns without NeedThreat/NeedAutoTargetable (never friendly buildings).
            float distance = target == null ? float.MaxValue : Position.DistanceToSquared(target.Position);
            foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.HostileTo(Faction) || pawn.Dead || pawn.Downed || pawn.Position.Fogged(Map)
                    || !IsAllowedTarget(pawn) || !AttackVerb.CanHitTarget(pawn))
                    continue;
                float candidateDistance = Position.DistanceToSquared(pawn.Position);
                if (candidateDistance < distance)
                {
                    target = pawn;
                    distance = candidateDistance;
                }
            }
            return target;
        }

        private bool IsAllowedTarget(Thing target)
        {
            if (!GetComp<CompSpellTurret>().AllowsTarget(target))
                return false;
            if (target is Pawn pawn)
            {
                if (Faction == Faction.OfPlayer && pawn.IsPrisoner)
                    return false;
                if (AttackVerb.ProjectileFliesOverhead() && Map.roofGrid.RoofAt(target.Position)?.isThickRoof == true)
                    return false;
                if (pawn.HostileTo(Faction) && GenAI.MachinesLike(Faction, pawn))
                    return false;
            }
            return true;
        }

        protected override void BeginBurst()
        {
            if (!ManaSwitchUtility.IsSwitchedOn(this) || (CurrentTarget.HasThing && !IsAllowedTarget(CurrentTarget.Thing)))
            {
                currentTargetInt = LocalTargetInfo.Invalid;
                return;
            }
            base.BeginBurst();
        }
    }
}
