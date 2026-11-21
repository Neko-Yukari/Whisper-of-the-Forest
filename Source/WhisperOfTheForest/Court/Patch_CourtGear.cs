using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Court
{
    /// <summary>
    /// Puts a court pawn's generated gear into the material its kind declares.
    ///
    /// Vanilla calls <see cref="PawnGenerator.PostProcessGeneratedGear"/> for every apparel piece
    /// (RimWorld/PawnApparelGenerator.cs:573) and for the weapon (RimWorld/PawnWeaponGenerator.cs:79)
    /// after the thing has been made and before quality and condition are rolled, so a prefix is the
    /// first and cheapest place to change the material: the quality roll, the condition roll and every
    /// later stat read then see the final material.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.PostProcessGeneratedGear))]
    internal static class Patch_CourtGearMaterial
    {
        private static void Prefix(Thing gear, Pawn pawn)
        {
            CourtGearExtension extension = CourtGearExtension.For(pawn);
            if (extension?.gear == null || gear?.def == null)
            {
                return;
            }

            for (int i = 0; i < extension.gear.Count; i++)
            {
                CourtGearMaterial entry = extension.gear[i];
                if (entry?.thingDef != gear.def)
                {
                    continue;
                }

                ThingDef stuff = entry.PickStuff();
                if (stuff != null && stuff != gear.Stuff)
                {
                    ApplyStuff(gear, stuff);
                }

                return;
            }
        }

        /// <summary>
        /// The same steps vanilla takes when it rewrites an existing thing's material
        /// (RimWorld/CompAbilityEffect_Transmute.cs:26-30): write the stuff, drop this thing's cached
        /// MaxHitPoints (an immutable stat, so it would otherwise keep the old material's value), then
        /// reset condition to the new maximum. Draw colour and worn tint are read from the stuff, so no
        /// extra call is needed for an unspawned thing being generated.
        /// </summary>
        private static void ApplyStuff(Thing gear, ThingDef stuff)
        {
            gear.SetStuffDirect(stuff);
            StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(gear);
            gear.HitPoints = gear.MaxHitPoints;
            gear.Notify_ColorChanged();
        }
    }

    /// <summary>
    /// Gives a court caster the one spell its staff fires, and points the staff at that bookmark.
    ///
    /// Spawn time rather than generation time: the spell lives in <see cref="GameComponent_Spellbook"/>,
    /// a GameComponent that only exists once a game is loaded, while PawnGenerator also runs during
    /// world generation. A pawn that spawns on a map always has both the game and its worn gear.
    ///
    /// The write goes through <see cref="GameComponent_Spellbook.SaveSlot"/> exactly like the book
    /// page, so the glyph unlock, slot and validation checks all apply; nothing here bypasses them.
    /// Repeated spawns are idempotent: the slot is only written while it is empty. Two separate steps
    /// for a pawn that wears its book: write the spell when slot 1 is empty (a failure there is a real
    /// error), then point the staff at slot 1 when it has none. A pawn without a book does neither.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_CourtCasterSpell
    {
        private static void Postfix(Pawn __instance)
        {
            CourtGearExtension extension = CourtGearExtension.For(__instance);
            if (extension == null || !extension.castsSpells)
            {
                return;
            }

            // A pawn whose SpawnSetup bailed out (dead, no kind) is not on the map and has nothing to write to.
            if (!__instance.Spawned || __instance.Dead)
            {
                return;
            }

            // Without a worn book every slot is unavailable and SaveSlot would refuse with NoBook.
            // That is not a fault: a stripped caster (captured, then loaded: Map.cs:775 respawns it)
            // simply cannot cast, and the cast gate says so itself. The staff keeps its saved slot.
            if (!GameComponent_Spellbook.IsSlotOpen(__instance, 0))
            {
                return;
            }

            CompSpellStaff staff = CompSpellStaff.FindEquipped(__instance);
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null)
            {
                WotfLog.Error(WotfLog.ModuleCourt,
                    "court caster " + __instance.LabelShortCap + " spawned without the spellbook component; no spell was written");
                return;
            }

            if (component.GetSlot(__instance, 0) == null)
            {
                var recipe = new SpellRecipe
                {
                    name = extension.spellName,
                    glyphs = new List<string>(extension.spellGlyphs)
                };
                if (!component.SaveSlot(__instance, 0, recipe, out string reason))
                {
                    WotfLog.Error(WotfLog.ModuleCourt,
                        "court caster " + __instance.LabelShortCap + " could not be given its spell: " + reason);
                    return;
                }

                WotfLog.Info(WotfLog.ModuleCourt,
                    "court caster " + __instance.LabelShortCap + " spawned with '" + extension.spellName + "' in slot 1");
            }

            if (staff != null && staff.Slot == CompSpellStaff.NoSlot)
            {
                staff.SetSlot(0);
            }
        }
    }
}
