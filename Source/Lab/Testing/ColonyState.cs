using System.Collections.Generic;
using System.Reflection;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Testing
{
    /// <summary>
    /// Colony-wide state that outlives one test. Rune unlocks live in a GameComponent that only ever
    /// grows: the product's public surface has <see cref="GameComponent_Spellbook.Unlock"/> and no way
    /// back, because for a colony an unlock is permanent. A test that unlocks a rune would therefore
    /// change what the next pass in the same game sees, so the runner snapshots the set when a run
    /// starts and puts it back before every test; each test then begins from exactly the state the
    /// fixture was loaded with.
    ///
    /// The snapshot reads and writes the component's own serialized list and touches nothing else;
    /// no field of the product's API is added or changed for the test host.
    /// </summary>
    internal static class ColonyState
    {
        private static readonly FieldInfo UnlockedRunesField = typeof(GameComponent_Spellbook).GetField(
            "unlockedGlyphs", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A copy of the runes the colony has unlocked right now.</summary>
        internal static List<string> CaptureRunes()
        {
            var component = GameComponent_Spellbook.Current;
            var list = component == null ? null : UnlockedRunesField?.GetValue(component) as List<string>;
            return list == null ? new List<string>() : new List<string>(list);
        }

        /// <summary>Puts the colony's unlock set back to a snapshot taken by <see cref="CaptureRunes"/>.</summary>
        internal static void RestoreRunes(List<string> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var component = GameComponent_Spellbook.Current;
            if (component == null || !(UnlockedRunesField?.GetValue(component) is List<string> list))
            {
                return;
            }

            list.Clear();
            list.AddRange(snapshot);
        }
    }
}
