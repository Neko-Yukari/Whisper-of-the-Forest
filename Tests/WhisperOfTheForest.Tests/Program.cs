using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WhisperOfTheForest.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string filter = args.Length == 2 && args[0] == "--filter" ? args[1] : "";
            Glyphs.Register();

            var tests = new List<(string Name, Action Run)>();
            tests.AddRange(SpellGrammarTests.All);
            tests.AddRange(SpellCarrierTests.All);
            tests.AddRange(ManaTopologyTests.All);
            tests.AddRange(RailgunGeometryTests.All);
            tests.AddRange(SpellCastVfxSeamTests.All);
            tests.AddRange(ShieldGeometryTests.All);
            tests.AddRange(HealingPoolTests.All);
            tests.AddRange(RitualResearchTests.All);

            var clock = Stopwatch.StartNew();
            int run = 0;
            int failed = 0;
            foreach ((string name, Action test) in tests)
            {
                if (!name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                run++;
                try
                {
                    test();
                    Console.WriteLine("[PASS] " + name);
                }
                catch (Exception e)
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + name + ": " + e.Message);
                }
            }

            Console.WriteLine($"SUMMARY passed={run - failed} failed={failed} ms={clock.ElapsedMilliseconds}");
            return failed == 0 && run > 0 ? 0 : 1;
        }
    }

    internal static class Check
    {
        internal static void True(bool actual, string message)
        {
            if (!actual)
            {
                throw new InvalidOperationException(message);
            }
        }

        internal static void Equal<T>(T expected, T actual, string message) =>
            True(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected={expected}; actual={actual}");
    }
}
