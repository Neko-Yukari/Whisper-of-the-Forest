using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Missiles;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// A small rune set made for the grammar, registered once in the real DefDatabase. Only the
    /// structure matters here (kinds, which augments a rune lists); none of the mod's XML is read.
    /// Offline, Translate returns the key, so a refusal reason is its Keyed key.
    /// </summary>
    internal static class Glyphs
    {
        internal const int MaxGlyphs = 4;

        internal const string Projectile = "T_Form_Projectile";
        internal const string Touch = SpellDefs.FormTouch;
        internal const string Self = "T_Form_Self";
        internal const string Orbit = "T_Form_Orbit";
        internal const string Super = "T_Super";
        internal const string Harm = "T_Effect_Harm";
        internal const string Wait = "T_Effect_Delay";
        internal const string Locked = "T_Effect_Locked";
        internal const string FormAugment = "T_Augment_Form";
        internal const string Amplify = "T_Augment_Amplify";
        internal const string Dampen = "T_Augment_Dampen";

        internal static void Register()
        {
            DefDatabase<SpellTuningDef>.Add(new SpellTuningDef { defName = SpellDefs.TuningDefName, maxGlyphs = MaxGlyphs, maxMissiles = 3 });

            Add(new SpellGlyphDef { defName = Projectile, kind = SpellGlyphKind.Form, projectile = new ArcaneMissileSettings(), compatibleAugments = new List<string> { FormAugment } });
            Add(new SpellGlyphDef { defName = Touch, kind = SpellGlyphKind.Form });
            Add(new SpellGlyphDef { defName = Self, kind = SpellGlyphKind.Form });
            Add(new SpellGlyphDef { defName = Orbit, kind = SpellGlyphKind.Form, orbit = new SpellOrbitSpec() });
            Add(new SpellGlyphDef { defName = Super, kind = SpellGlyphKind.Super, railgun = new SpellRailgunSpec(), targets = new List<SpellTargetClass> { SpellTargetClass.Cell } });
            Add(new HarmGlyphDef { defName = Harm, kind = SpellGlyphKind.Effect, damage = 1f, targets = new List<SpellTargetClass> { SpellTargetClass.Humanlike }, compatibleAugments = new List<string> { Amplify, Dampen } });
            Add(new DelayGlyphDef { defName = Wait, kind = SpellGlyphKind.Effect, delayTicks = 1, targets = new List<SpellTargetClass>() });
            Add(new HarmGlyphDef { defName = Locked, kind = SpellGlyphKind.Effect, damage = 1f, requiresUnlock = true, targets = new List<SpellTargetClass> { SpellTargetClass.Humanlike } });
            Add(new SpellGlyphDef { defName = FormAugment, kind = SpellGlyphKind.Augment });
            Add(new SpellGlyphDef { defName = Amplify, kind = SpellGlyphKind.Augment, excludesAugments = new List<string> { Dampen } });
            Add(new SpellGlyphDef { defName = Dampen, kind = SpellGlyphKind.Augment });
        }

        internal static SpellGlyphDef Get(string defName) => SpellDefs.Glyph(defName);

        internal static SpellRecipe Recipe(params string[] glyphs) => new SpellRecipe { name = "test", glyphs = new List<string>(glyphs) };

        private static void Add(SpellGlyphDef glyph) => DefDatabase<SpellGlyphDef>.Add(glyph);
    }
}
