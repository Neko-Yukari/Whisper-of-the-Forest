using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// A list holding three different SpellEffect subclasses, one carrying a nested payload, must
    /// survive save/load and resolve after the load exactly as it did before the save.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Spell_PayloadReload : LabTest
    {
        private const string FreezeDef = "WotF_Hediff_Freeze";
        private const string HexDef = "WotF_Hediff_Hex";

        private int fieldId;
        private int victimId;
        private string referenceState;
        private int payloadCount;

        public override string Name => "spell.payload-reload";

        private static bool Has(Pawn pawn, string defName)
        {
            return pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed(defName));
        }

        private static string State(Pawn pawn)
        {
            return "freeze=" + Has(pawn, FreezeDef) + " hex=" + Has(pawn, HexDef);
        }

        private static List<SpellEffect> BuildPayload()
        {
            var nested = new SpellPayload
            {
                steps = new List<SpellEffect> { new HexEffect(900, false, 0f) }
            };
            return new List<SpellEffect>
            {
                new HarmEffect(9f, 0.5f, false, 0f),
                new FreezeEffect(600, false, 0f),
                new DelayEffect(nested, 5)
            };
        }

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(4, 5);
            Pawn reference = t.SpawnTarget(8, 5);
            Pawn victim = t.SpawnTarget(12, 5);
            victimId = victim.thingIDNumber;
            List<SpellEffect> payload = BuildPayload();
            // Reference resolution before the save: frozen state, then the nested payload later.
            SpellExecutor.ExecuteStepList(payload, caster, reference, reference.Position, t.Map);
            t.Expect(Has(reference, FreezeDef), "reference run did not apply the freeze state");
            t.Expect(!Has(reference, HexDef), "reference run resolved the nested payload at once");
            // Only a handful of ticks: the shared reload save keeps a dart mid-flight, and this
            // scene must not advance the clock far enough for it to land before that save.
            t.Step(10);
            t.Expect(Has(reference, HexDef), "reference run never resolved the nested payload");
            referenceState = State(reference);
            t.Measure("referenceState", referenceState);

            ThingDef fieldDef = DefDatabase<ThingDef>.GetNamed(SpellEffectDefs.ThingSpellField);
            var field = (Thing_SpellField)ThingMaker.MakeThing(fieldDef);
            field.Configure(SpellFieldMode.Linger, caster, payload, 5000, 1.9f, 300);
            GenSpawn.Spawn(field, victim.Position, t.Map);
            t.Arena.Track(field);
            fieldId = field.thingIDNumber;
            payloadCount = field.Payload.Count;
            t.Note("fieldPayloadCount", payloadCount);
        }

        protected override void AfterReload(TestContext t)
        {
            var field = t.Arena.Find(fieldId) as Thing_SpellField;
            t.Expect(field != null, "the saved spell field did not come back");
            if (field == null) return;
            t.Expect(field.Payload.Count == payloadCount, "payload length changed across the save: " + field.Payload.Count);
            t.Expect(field.Payload[0] is HarmEffect && field.Payload[1] is FreezeEffect
                && field.Payload[2] is DelayEffect, "loaded list lost its concrete effect types");
            var delay = field.Payload[2] as DelayEffect;
            t.Expect(delay != null && delay.Payload.steps.Count == 1
                && delay.Payload.steps[0] is HexEffect, "nested payload did not come back as a HexEffect");
            t.Measure("loadedTypes", string.Join(",", field.Payload.Select(e => e.GetType().Name)));

            Pawn victim = (Pawn)t.Arena.Find(victimId);
            t.Arena.Track(victim);
            t.Expect(!Has(victim, FreezeDef) && !Has(victim, HexDef), "payload resolved before the field pulsed");
            bool pulsed = t.StepUntil(() => Has(victim, FreezeDef), 600);
            t.Expect(pulsed, "the restored field never resolved its payload");
            t.Expect(!Has(victim, HexDef), "nested payload resolved together with the outer list");
            bool nested = t.StepUntil(() => Has(victim, HexDef), 300);
            t.Expect(nested, "the nested Delay payload never resolved after the reload");
            string victimState = State(victim);
            t.Measure("victimState", victimState);
            t.Expect(victimState == referenceState,
                "post-load result differs from the pre-save run: " + victimState + " vs " + referenceState);
        }



    }
}

