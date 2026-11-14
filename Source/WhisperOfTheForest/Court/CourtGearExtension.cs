using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Court
{
    /// <summary>
    /// What a court pawn kind cannot say with Def fields alone: the material every piece of its
    /// generated gear is made from, and whether its members cast.
    ///
    /// Generation cannot pick these materials by itself. The four forest stuffs are
    /// allowedInStuffGeneration=false with commonality 0 (1.7/Defs/Materials/Stuff.xml), so
    /// PawnApparelGenerator.CanUseStuff (RimWorld/PawnApparelGenerator.cs:943-965) and the weapon pair
    /// filter (RimWorld/PawnWeaponGenerator.cs:66) drop every pair made of them; weaponStuffOverride
    /// and SpecificApparelRequirement.stuff sit behind the same check. The Def therefore names the
    /// target material per gear Def and <see cref="Patch_CourtGearMaterial"/> swaps it in once the
    /// garment or weapon exists.
    /// </summary>
    public class CourtGearExtension : DefModExtension
    {
        /// <summary>Gear Def to material. A piece not listed keeps whatever vanilla generated.</summary>
        public List<CourtGearMaterial> gear = new List<CourtGearMaterial>();

        /// <summary>
        /// True when the kind's members cast. This is the only thing that widens
        /// <see cref="SpellService.IsEligibleCaster"/> past player-controlled pawns, and it must be
        /// set explicitly: the sword guard, the sentinel and the archon carry the same extension and
        /// still cannot cast. Bloodline, worn spellbook and being spawned on a map stay required for everyone.
        /// </summary>
        public bool castsSpells;

        /// <summary>
        /// The spell written into a new caster's book, as SpellGlyphDef defNames in chain order. Held
        /// as data for the same reason as the materials: the glyph chain is a design value.
        /// </summary>
        public List<string> spellGlyphs = new List<string>();

        /// <summary>The name the written spell is saved and shown under.</summary>
        public string spellName;

        public static CourtGearExtension For(Pawn pawn)
        {
            return pawn?.kindDef?.GetModExtension<CourtGearExtension>();
        }

        /// <summary>True only for kinds whose Def opts in; never inferred from having the extension.</summary>
        public static bool DeclaresCasting(Pawn pawn)
        {
            return For(pawn)?.castsSpells == true;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (gear == null)
            {
                yield return "gear is null";
            }
            else
            {
                for (int i = 0; i < gear.Count; i++)
                {
                    CourtGearMaterial entry = gear[i];
                    if (entry == null || entry.thingDef == null)
                    {
                        yield return "gear[" + i + "] has no thingDef";
                        continue;
                    }

                    if (!entry.thingDef.MadeFromStuff)
                    {
                        yield return entry.thingDef.defName + " is not made from stuff, so a material cannot apply";
                        continue;
                    }

                    if (entry.stuffs.NullOrEmpty())
                    {
                        yield return entry.thingDef.defName + " names no material";
                        continue;
                    }

                    for (int j = 0; j < entry.stuffs.Count; j++)
                    {
                        ThingDef stuff = entry.stuffs[j];
                        if (stuff == null)
                        {
                            yield return entry.thingDef.defName + " has a null material at index " + j;
                        }
                        else if (!stuff.IsStuff)
                        {
                            yield return entry.thingDef.defName + ": " + stuff.defName + " is not a stuff";
                        }
                        else if (!stuff.stuffProps.CanMake(entry.thingDef))
                        {
                            yield return entry.thingDef.defName + " cannot be made from " + stuff.defName;
                        }
                    }
                }
            }

            if (!castsSpells)
            {
                yield break;
            }

            if (spellName.NullOrEmpty())
            {
                yield return "castsSpells is set but spellName is empty";
            }

            if (spellGlyphs.NullOrEmpty())
            {
                yield return "castsSpells is set but spellGlyphs is empty";
                yield break;
            }

            for (int i = 0; i < spellGlyphs.Count; i++)
            {
                if (DefDatabase<SpellGlyphDef>.GetNamedSilentFail(spellGlyphs[i]) == null)
                {
                    yield return "spellGlyphs[" + i + "] '" + spellGlyphs[i] + "' is not a SpellGlyphDef";
                }
            }
        }
    }

    /// <summary>One gear Def and the material(s) the court makes it from; more than one is rolled per item.</summary>
    public class CourtGearMaterial
    {
        public ThingDef thingDef;

        public List<ThingDef> stuffs = new List<ThingDef>();

        public ThingDef PickStuff()
        {
            if (stuffs.NullOrEmpty())
            {
                return null;
            }

            return stuffs.Count == 1 ? stuffs[0] : stuffs.RandomElement();
        }
    }
}
