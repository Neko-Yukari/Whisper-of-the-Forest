using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The numbers a composite form owns. They are copied into the compiled plan, so a cast keeps the
    /// numbers it started with even if the Def is edited later (dev mode) and so a save carries them.
    ///
    /// These are spell parameters only: how wide the corridor is, how far it reaches and how much
    /// damage one hit does. How long the shot takes belongs to the rune's <see cref="SpellTiming"/>
    /// (its channel), and everything about how the effect looks belongs to the Vfx Def named by
    /// <see cref="vfxDef"/>, which is why no colour, beam or screen value appears here.
    ///
    /// The class lives in its own file so the glyph-config harness can compile the real
    /// SpellGlyphDef (which owns one of these) without pulling in the whole plan/serialization file.
    /// </summary>
    public class SpellRailgunSpec : IExposable
    {
        /// <summary>Placeholder damage per hit; not a balance conclusion.</summary>
        public float damage = 120f;

        /// <summary>Armour penetration, following the existing spell damage convention.</summary>
        public float armorPenetration = 0.5f;

        /// <summary>Full corridor width in cells; a target within half of it is hit.</summary>
        public float hitWidth = 2.5f;

        /// <summary>Maximum corridor length in cells; 0 or less means the whole map.</summary>
        public float range = 0f;

        /// <summary>Semantic visual request name. The spell never names colours, beams or screens.</summary>
        public string vfxDef = SpellDefs.VfxRailgun;

        public void ExposeData()
        {
            Scribe_Values.Look(ref damage, "damage", 120f);
            Scribe_Values.Look(ref armorPenetration, "armorPenetration", 0.5f);
            Scribe_Values.Look(ref hitWidth, "hitWidth", 2.5f);
            Scribe_Values.Look(ref range, "range", 0f);
            Scribe_Values.Look(ref vfxDef, "vfxDef");
        }

        public SpellRailgunSpec Copy()
        {
            return new SpellRailgunSpec
            {
                damage = damage,
                armorPenetration = armorPenetration,
                hitWidth = hitWidth,
                range = range,
                vfxDef = vfxDef
            };
        }
    }
}
