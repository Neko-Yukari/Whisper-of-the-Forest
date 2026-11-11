using Verse;

namespace WhisperOfTheForest.Spells
{
    public interface ISpellSpecialSpec
    {
        float Range { get; }
        void BeginChannel(SpellCast cast);
        string Release(SpellCast cast);
    }
}
