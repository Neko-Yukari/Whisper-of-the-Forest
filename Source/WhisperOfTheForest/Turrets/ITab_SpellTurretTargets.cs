using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Turrets
{
    public sealed class ITab_SpellTurretTargets : ITab
    {
        public ITab_SpellTurretTargets()
        {
            size = new Vector2(320f, 340f);
            labelKey = "WotF_Turret_Targets";
        }

        public override bool IsVisible => SelThing?.Faction == Faction.OfPlayer;

        protected override void FillTab()
        {
            using (TechLabDrawScope.Begin())
            {
                CompSpellTurret turret = SelThing.TryGetComp<CompSpellTurret>();
                var listing = new Listing_Standard();
                listing.Begin(new Rect(15f, 15f, size.x - 30f, size.y - 30f));
                listing.Label("WotF_Turret_CategoryGroup".Translate());
                bool humanlikes = turret.TargetHumanlikes;
                bool animals = turret.TargetAnimals;
                bool mechanoids = turret.TargetMechanoids;
                bool entities = turret.TargetEntities;
                listing.CheckboxLabeled("WotF_Turret_Humanlikes".Translate(), ref humanlikes);
                listing.CheckboxLabeled("WotF_Turret_Animals".Translate(), ref animals);
                listing.CheckboxLabeled("WotF_Turret_Mechanoids".Translate(), ref mechanoids);
                listing.CheckboxLabeled("WotF_Turret_Entities".Translate(), ref entities);
                listing.GapLine();
                listing.Label("WotF_Turret_RelationGroup".Translate());
                bool hostile = turret.TargetHostile;
                bool neutral = turret.TargetNeutral;
                bool friendly = turret.TargetFriendly;
                listing.CheckboxLabeled("WotF_Turret_Hostile".Translate(), ref hostile);
                listing.CheckboxLabeled("WotF_Turret_Neutral".Translate(), ref neutral);
                listing.CheckboxLabeled("WotF_Turret_Friendly".Translate(), ref friendly);
                turret.TargetHostile = hostile;
                turret.TargetNeutral = neutral;
                turret.TargetFriendly = friendly;
                listing.End();
                turret.TargetHumanlikes = humanlikes;
                turret.TargetAnimals = animals;
                turret.TargetMechanoids = mechanoids;
                turret.TargetEntities = entities;
            }
        }
    }
}
