using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Race
{
    public sealed class ElfNameRules : DefModExtension
    {
        public List<string> reservedNames = new List<string>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string name in reservedNames)
            {
                string[] parts = name.Split(' ');
                if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                    yield return "Reserved elf name must contain a first and clan name: " + name;
            }
        }
    }
}
