using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>One "run these steps on this cell later" registration, saved with the map.</summary>
    public class SpellDelayedPayload : IExposable
    {
        public Pawn caster;
        public IntVec3 cell;
        public int dueTick;
        public List<SpellPlanStep> steps = new List<SpellPlanStep>();

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref dueTick, "dueTick", 0);
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);

            if (steps == null)
            {
                steps = new List<SpellPlanStep>();
            }
        }
    }

    /// <summary>
    /// The Delay rune's clock. A delayed payload has no body on the map, so the map component is the
    /// owner of the pending list; it survives a save/load and resolves on the first tick that is due.
    /// </summary>
    public class MapComponent_SpellDelays : MapComponent
    {
        private List<SpellDelayedPayload> pending = new List<SpellDelayedPayload>();

        public MapComponent_SpellDelays(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                SpellDelayedPayload payload = pending[i];
                if (payload == null)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (now < payload.dueTick)
                {
                    continue;
                }

                pending.RemoveAt(i);
                string report = SpellExecutor.ExecuteStepList(
                    payload.steps, payload.caster, null, payload.cell, map);
                SpellDiagnostics.Message("delay resolved at " + payload.cell + " " + report);
            }
        }

        public void Schedule(Pawn caster, IntVec3 cell, int dueTick, List<SpellPlanStep> steps)
        {
            if (pending == null)
            {
                pending = new List<SpellDelayedPayload>();
            }

            pending.Add(new SpellDelayedPayload
            {
                caster = caster,
                cell = cell,
                dueTick = dueTick,
                steps = SpellExecutor.CopySteps(steps)
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "spellDelays", LookMode.Deep);

            if (pending == null)
            {
                pending = new List<SpellDelayedPayload>();
            }
        }
    }
}
