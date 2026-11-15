using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Turrets
{
    public class CompProperties_SpellTurret : CompProperties
    {
        public float manaCostFactor;
        public int inscriptionTicks;
        public float linkRadius;
        public JobDef inscriptionJob;

        public CompProperties_SpellTurret()
        {
            compClass = typeof(CompSpellTurret);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (manaCostFactor <= 0f)
                yield return "manaCostFactor must be positive";
            if (inscriptionTicks <= 0)
                yield return "inscriptionTicks must be positive";
            if (linkRadius <= 0f)
                yield return "linkRadius must be positive";
            if (inscriptionJob == null)
                yield return "inscriptionJob is required";
            if (!typeof(Building_SpellTurret).IsAssignableFrom(parentDef.thingClass))
                yield return "spell turret requires Building_SpellTurret";
        }
    }

    public class CompSpellTurret : ThingComp
    {
        private SpellRecipe currentSpell;
        private SpellRecipe pendingSpell;
        private SpellCastPlan plan;
        private int inscriptionRevision;
        private bool targetHumanlikes = true;
        private bool targetAnimals = true;
        private bool targetMechanoids = true;
        private bool targetEntities = true;
        private bool targetHostile = true;
        private bool targetNeutral;
        private bool targetFriendly;

        public CompProperties_SpellTurret Props => (CompProperties_SpellTurret)props;
        // Callers receive detached recipes, never the turret's saved mutable objects.
        public SpellRecipe CurrentSpell => currentSpell?.Copy();
        public bool HasSpell => currentSpell != null;
        public SpellRecipe PendingSpell => pendingSpell?.Copy();
        public bool HasPendingSpell => pendingSpell != null;
        public float ManaPerShot => currentSpell == null ? 0f : currentSpell.cost.mana * Props.manaCostFactor;
        internal int InscriptionRevision => inscriptionRevision;

        public bool TargetHumanlikes { get => targetHumanlikes; set => targetHumanlikes = value; }
        public bool TargetAnimals { get => targetAnimals; set => targetAnimals = value; }
        public bool TargetMechanoids { get => targetMechanoids; set => targetMechanoids = value; }
        public bool TargetEntities { get => targetEntities; set => targetEntities = value; }
        public bool TargetHostile { get => targetHostile; set => targetHostile = value; }
        public bool TargetNeutral { get => targetNeutral; set => targetNeutral = value; }
        public bool TargetFriendly { get => targetFriendly; set => targetFriendly = value; }

        public bool CanAfford => plan != null && Network is ManaNetwork network && network.Stored >= ManaPerShot;

        private ManaNetwork Network =>
            MapComponent_ManaNetwork.For(parent.Map)?.NetworkNear(parent.Position, Props.linkRadius);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref currentSpell, "currentSpell");
            Scribe_Deep.Look(ref pendingSpell, "pendingSpell");
            Scribe_Values.Look(ref inscriptionRevision, "inscriptionRevision");
            Scribe_Values.Look(ref targetHumanlikes, "targetHumanlikes", true);
            Scribe_Values.Look(ref targetAnimals, "targetAnimals", true);
            Scribe_Values.Look(ref targetMechanoids, "targetMechanoids", true);
            Scribe_Values.Look(ref targetEntities, "targetEntities", true);
            Scribe_Values.Look(ref targetHostile, "targetHostile", true);
            Scribe_Values.Look(ref targetNeutral, "targetNeutral");
            Scribe_Values.Look(ref targetFriendly, "targetFriendly");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (currentSpell != null)
            {
                plan = SpellCompiler.Compile(currentSpell, out string reason);
                if (plan == null)
                    SpellDiagnostics.Error("spell turret could not compile its saved spell: " + reason);
            }
        }

        public bool TryRequestInscription(SpellRecipe recipe, out string reason)
        {
            if (!CanCompileProjectile(recipe, out reason))
                return false;

            pendingSpell = recipe.Copy();
            pendingSpell.cost = SpellCost.Derive(pendingSpell.glyphs);
            if (pendingSpell.targets == null)
                pendingSpell.targets = SpellCompiler.DeriveTargets(pendingSpell);
            inscriptionRevision++;
            return true;
        }

        /// <summary>Nonempty open book slots of this map's colonists; null reason means writable.</summary>
        public IEnumerable<TurretSpellCandidate> GetInscriptionCandidates()
        {
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                for (int slot = 0; slot < GameComponent_Spellbook.SlotCount; slot++)
                {
                    SpellRecipe recipe = GameComponent_Spellbook.Current.GetSlot(pawn, slot);
                    if (recipe == null)
                        continue;
                    CanCompileProjectile(recipe, out string reason);
                    yield return new TurretSpellCandidate(pawn, slot, recipe, reason);
                }
            }
        }

        /// <summary>A turret only fires projectile spells; Super runes never go into one.</summary>
        public static bool CanCompileProjectile(SpellRecipe recipe, out string reason)
        {
            SpellCastPlan compiled = SpellCompiler.Compile(recipe, out reason);
            if (compiled == null)
                return false;
            if (SpellDefs.Glyph(recipe.glyphs[0]).kind == SpellGlyphKind.Super)
            {
                reason = "WotF_Spells.Reason.SuperNoTurret".Translate();
                return false;
            }
            if (compiled.form != SpellFormKind.Projectile)
            {
                reason = "WotF_Turret_ProjectileOnly".Translate();
                return false;
            }
            return true;
        }

        internal bool CanInscribe(Pawn pawn) => HasPendingSpell
            && parent.Spawned && parent.Faction == pawn.Faction
            && SpellService.IsEligibleCaster(pawn, out _);

        internal void CompleteInscription(int revision)
        {
            // Replacing a pending order invalidates an already-running writing job.
            if (!HasPendingSpell || inscriptionRevision != revision)
                return;
            currentSpell = pendingSpell;
            pendingSpell = null;
            plan = SpellCompiler.Compile(currentSpell, out string reason);
            if (plan == null)
                SpellDiagnostics.Error("spell turret could not compile its inscription: " + reason);
        }

        public bool AllowsTarget(Thing target)
        {
            if (target == parent)
                return false;
            // The player's category switches can only narrow the spell's own whitelist.
            if (plan == null || !SpellTargeting.AcceptsThing(plan, target))
                return false;
            bool hostile = target.HostileTo(parent.Faction);
            // Non-hostile buildings never enter the friendly/neutral pawn filters.
            if (!(target is Pawn) && !hostile)
                return false;
            bool relationAllowed = hostile ? targetHostile
                : target.Faction != null && (target.Faction == parent.Faction
                    || target.Faction.RelationKindWith(parent.Faction) == FactionRelationKind.Ally)
                    ? targetFriendly : targetNeutral;
            if (!relationAllowed)
                return false;
            switch (SpellTargeting.ClassOf(target))
            {
                case SpellTargetClass.Entity: return targetEntities;
                case SpellTargetClass.Mechanoid: return targetMechanoids;
                case SpellTargetClass.Humanlike: return targetHumanlikes;
                case SpellTargetClass.Animal: return targetAnimals;
                default: return true;
            }
        }

        public bool TryCast(LocalTargetInfo target, out string reason)
        {
            if (plan == null)
            {
                reason = "its spell plan is not ready";
                return false;
            }
            if (target.HasThing && !AllowsTarget(target.Thing))
            {
                reason = "target category or faction relation is disabled";
                return false;
            }

            ManaNetwork network = Network;
            if (network == null)
            {
                reason = "no mana network within " + Props.linkRadius.ToString("0.#") + " cells";
                return false;
            }
            float cost = ManaPerShot;
            if (!network.TryDraw(cost))
            {
                reason = "the network holds " + network.Stored.ToString("0.#") + " mana";
                return false;
            }
            if (SpellCastingService.LaunchProjectile(parent, target, plan) > 0)
            {
                reason = null;
                return true;
            }
            network.Add(cost);
            reason = "the dart could not be launched";
            return false;
        }

        public override string CompInspectStringExtra()
        {
            SpellRecipe current = CurrentSpell;
            SpellRecipe pending = PendingSpell;
            string text = current == null ? SpellText.T("WotF_Turret_NoSpell")
                : SpellText.TF("WotF_Turret_CurrentSpell", current.name);
            if (pending != null)
                text += "\n" + SpellText.TF("WotF_Turret_PendingSpell", pending.name);
            if (current != null)
            {
                ManaNetwork network = Network;
                string stored = network == null ? "-" : network.Stored.ToString("0");
                text += "\n" + SpellText.TF("WotF_SpellTurret_Inspect", stored, ManaPerShot.ToString("0.#"));
            }
            return text;
        }
    }
}
