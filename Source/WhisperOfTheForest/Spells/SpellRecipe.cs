using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// One saved spell: a display name plus the ordered glyph defNames. The class only holds data
    /// and its own copy/save behaviour; grammar and execution live in the shared services, so the
    /// book and the runtime can never disagree about what a recipe means.
    /// </summary>
    public class SpellRecipe : IExposable
    {
        public string name;

        /// <summary>Glyph defNames in the order they were arranged, first one is the Form.</summary>
        public List<string> glyphs = new List<string>();

        /// <summary>
        /// The phase lengths this spell was created with. Filled by
        /// <see cref="GameComponent_Spellbook.SaveSlot"/> from the chain, then carried by every copy
        /// and every cast, so casting never recomputes it.
        /// </summary>
        public SpellTiming timing;

        /// <summary>The cost this spell was created with; frozen and carried like <see cref="timing"/>.</summary>
        public SpellCost cost;

        /// <summary>
        /// The union of every effect's target categories, frozen when the spell was created. Casting,
        /// the player targeter, AI and turrets read only this list.
        /// </summary>
        public List<SpellTargetClass> targets;

        /// <summary>An independent recipe; editing the copy never touches the source slot.</summary>
        public SpellRecipe Copy()
        {
            return new SpellRecipe
            {
                name = name,
                glyphs = glyphs == null ? new List<string>() : new List<string>(glyphs),
                timing = timing?.Copy(),
                cost = cost?.Copy(),
                targets = targets == null ? null : new List<SpellTargetClass>(targets)
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref glyphs, "glyphs", LookMode.Value);
            Scribe_Deep.Look(ref timing, "timing");
            Scribe_Deep.Look(ref cost, "cost");
            Scribe_Collections.Look(ref targets, "targets", LookMode.Value);

            // Scribing only clears this while loading; a null list would break every later check.
            if (glyphs == null)
            {
                glyphs = new List<string>();
            }
        }

        public override string ToString()
        {
            return (name.NullOrEmpty() ? "<unnamed>" : name) + " [" + string.Join(", ", glyphs.ToArray()) + "]";
        }
    }

    /// <summary>Spell-layer logging: the shared leveled entry with the spell module tag.</summary>
    public static class SpellDiagnostics
    {
        public static void Error(string message)
        {
            WotfLog.Error(WotfLog.ModuleSpell, message);
        }

        /// <summary>For a broken config that would otherwise spam every tick/cast.</summary>
        public static void ErrorOnce(string message)
        {
            WotfLog.ErrorOnce(WotfLog.ModuleSpell, message);
        }

        public static void Message(string message)
        {
            WotfLog.Info(WotfLog.ModuleSpell, message);
        }

        public static void Warning(string message)
        {
            WotfLog.Warning(WotfLog.ModuleSpell, message);
        }

        public static void Debug(string message)
        {
            WotfLog.Debug(WotfLog.ModuleSpell, message);
        }
    }

    /// <summary>Keyed translation lookups used by reasons, gizmos and cast messages.</summary>
    public static class SpellText
    {
        public static string T(string key)
        {
            return Translator.Translate(key);
        }

        public static string TF(string key, params object[] args)
        {
            // Same pattern as the mod's other UI text helpers: string.Format over the translated
            // template, because Translator.Translate(string, object[]) is obsolete in 1.7.
            return string.Format(Translator.Translate(key), args);
        }
    }
}
