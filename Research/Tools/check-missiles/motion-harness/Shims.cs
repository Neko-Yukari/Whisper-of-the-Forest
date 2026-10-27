using System;
using System.Collections.Generic;

// Minimal stand-ins for the game API surface used by the real missile files. Only what those
// files actually touch is provided, so the production source compiles unchanged.

namespace UnityEngine
{
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}

namespace Verse
{
    public interface IExposable
    {
        void ExposeData();
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UnsavedAttribute : Attribute
    {
    }

    public static class Log
    {
        public static readonly List<string> Errors = new List<string>();

        public static void Error(string message)
        {
            Errors.Add(message);
        }
    }

    public class Def
    {
        public string defName;
        public string label;

        public virtual IEnumerable<string> ConfigErrors()
        {
            yield break;
        }
    }

    public class DamageDef : Def
    {
    }

    public static class DamageDefOf
    {
        public static readonly DamageDef Blunt = new DamageDef { defName = "Blunt" };
    }

    public static class DefDatabase<T> where T : Def
    {
        public static readonly Dictionary<string, T> Named = new Dictionary<string, T>();

        public static T GetNamedSilentFail(string defName)
        {
            Named.TryGetValue(defName, out T def);
            return def;
        }
    }

    public enum FillCategory
    {
        None,
        Partial,
        Full
    }

    public class ThingDef : Def
    {
        public FillCategory Fillage;
        public Type thingClass;
        public float Altitude;
    }

    public struct IntVec3
    {
        public int x;
        public int y;
        public int z;

        public IntVec3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly IntVec3 Invalid = new IntVec3(-1, -1, -1);

        public bool IsValid => x >= 0 && z >= 0;

        public bool InBounds(Map map)
        {
            return GenGrid.InBounds(this, map);
        }

        public UnityEngine.Vector3 ToVector3Shifted()
        {
            return new UnityEngine.Vector3(x + 0.5f, 0f, z + 0.5f);
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ")";
        }

        public static bool operator ==(IntVec3 a, IntVec3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(IntVec3 a, IntVec3 b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return obj is IntVec3 other && this == other;
        }

        public override int GetHashCode()
        {
            return (x * 397) ^ (z * 31) ^ y;
        }
    }

    public class ThingGrid
    {
        private readonly Dictionary<long, List<Thing>> cells = new Dictionary<long, List<Thing>>();
        private static readonly List<Thing> Empty = new List<Thing>();

        public void Register(Thing thing)
        {
            long key = Key(thing.Position);
            if (!cells.TryGetValue(key, out List<Thing> list))
            {
                list = new List<Thing>();
                cells[key] = list;
            }

            list.Add(thing);
        }

        public List<Thing> ThingsListAt(IntVec3 cell)
        {
            return cells.TryGetValue(Key(cell), out List<Thing> list) ? list : Empty;
        }

        public void Move(Thing thing, IntVec3 to)
        {
            if (cells.TryGetValue(Key(thing.Position), out List<Thing> list))
            {
                list.Remove(thing);
            }

            thing.Position = to;
            Register(thing);
        }

        public void Remove(Thing thing)
        {
            if (cells.TryGetValue(Key(thing.Position), out List<Thing> list))
            {
                list.Remove(thing);
            }
        }

        private static long Key(IntVec3 cell)
        {
            return ((long)cell.x << 32) ^ (uint)cell.z;
        }
    }

    public class Map
    {
        public IntVec3 Size;
        public ThingGrid thingGrid;
        public ListerThings listerThings = new ListerThings();
        public readonly List<MapComponent> components = new List<MapComponent>();

        public T GetComponent<T>() where T : class
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T component)
                {
                    return component;
                }
            }

            return null;
        }
    }

    public static class GenGrid
    {
        public static bool InBounds(IntVec3 cell, Map map)
        {
            return cell.x >= 0 && cell.z >= 0 && cell.x < map.Size.x && cell.z < map.Size.z;
        }
    }

    public struct LocalTargetInfo
    {
        private readonly Thing thing;
        private readonly IntVec3 cell;

        public LocalTargetInfo(Thing thing)
        {
            this.thing = thing;
            cell = thing != null ? thing.Position : IntVec3.Invalid;
        }

        public LocalTargetInfo(IntVec3 cell)
        {
            thing = null;
            this.cell = cell;
        }

        public bool IsValid => thing != null || cell.IsValid;
        public bool HasThing => thing != null;
        public Thing Thing => thing;
        public bool ThingDestroyed => thing != null && thing.Destroyed;
        public IntVec3 Cell => thing != null ? thing.Position : cell;
    }

    public static class Gen
    {
        public static int HashCombineInt(int seed, int value)
        {
            return (seed * 397) ^ value;
        }
    }

    public class MapComponent
    {
        public Map map;

        public MapComponent(Map map)
        {
            this.map = map;
        }

        public virtual void MapComponentTick()
        {
        }

        public virtual void MapComponentUpdate()
        {
        }

        public virtual void FinalizeInit()
        {
        }

        public virtual void MapRemoved()
        {
        }

        public virtual void ExposeData()
        {
        }
    }

    public static class Find
    {
        public static Map CurrentMap;
        public static TickManager TickManager = new TickManager();
    }

    public class TickManager
    {
        public int TicksGame;
    }

    public enum LoadSaveMode
    {
        Inactive,
        Saving,
        Loading,
        PostLoadInit
    }

    public enum LookMode
    {
        Undefined,
        Value,
        Deep,
        Reference,
        Def,
        LocalTargetInfo,
        TargetInfo,
        GlobalTargetInfo,
        BodyPart
    }

    public static class Scribe
    {
        public static LoadSaveMode mode;
    }

    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> list, string label, LookMode lookMode)
        {
        }
    }

    public class Thing
    {
        public int thingIDNumber;
        public ThingDef def;
        public bool Destroyed;
        public bool Spawned;
        public Map Map;
        public IntVec3 Position;

        /// <summary>Set to simulate sub-cell (fractional) positions such as a moving pawn.</summary>
        public bool HasDrawPosOverride;
        public UnityEngine.Vector3 DrawPosOverride;

        /// <summary>Damage applied through TakeDamage, observed by the harness.</summary>
        public float DamageTaken;

        public virtual UnityEngine.Vector3 DrawPos =>
            HasDrawPosOverride ? DrawPosOverride : Position.ToVector3Shifted();

        public virtual string LabelShort => def != null ? def.defName : "thing";

        public virtual void TakeDamage(DamageInfo dinfo)
        {
            DamageTaken += dinfo.Amount;
        }

        /// <summary>Spawn registration the game does in Thing.SpawnSetup; here only registry state.</summary>
        public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            Map = map;
            Spawned = true;
        }

        public virtual void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!Spawned)
            {
                return;
            }

            Spawned = false;
            Map?.listerThings?.Remove(this);
            Map?.thingGrid?.Remove(this);
        }

        public virtual void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Destroyed)
            {
                return;
            }

            Destroyed = true;
            DeSpawn(mode);
        }

        public virtual void ExposeData()
        {
        }
    }

    public class ThingWithComps : Thing
    {
    }

    /// <summary>
    /// Only the members the missile carrier touches: it proves the subclass plugs into the vanilla
    /// projectile contract, without modelling flight, saving or the thing ticker.
    /// </summary>
    public class Projectile : ThingWithComps
    {
        protected UnityEngine.Vector3 origin;
        protected UnityEngine.Vector3 destination;
        protected Thing launcher;
        protected int ticksToImpact;
        protected int lifetime;

        public LocalTargetInfo usedTarget;
        public LocalTargetInfo intendedTarget;

        public virtual UnityEngine.Vector3 ExactPosition => Position.ToVector3Shifted();

        public override UnityEngine.Vector3 DrawPos => ExactPosition;

        public virtual int UpdateRateTicks => 1;

        protected virtual void Tick()
        {
        }

        protected virtual void TickInterval(int delta)
        {
        }
    }

    public class ListerThings
    {
        private static readonly List<Thing> Empty = new List<Thing>();

        private readonly List<Thing> allThings = new List<Thing>();
        private readonly Dictionary<ThingDef, List<Thing>> byDef = new Dictionary<ThingDef, List<Thing>>();

        public void Add(Thing thing)
        {
            allThings.Add(thing);
            if (thing.def == null)
            {
                return;
            }

            if (!byDef.TryGetValue(thing.def, out List<Thing> list))
            {
                list = new List<Thing>();
                byDef[thing.def] = list;
            }

            list.Add(thing);
        }

        public void Remove(Thing thing)
        {
            allThings.Remove(thing);
            if (thing.def != null && byDef.TryGetValue(thing.def, out List<Thing> list))
            {
                list.Remove(thing);
            }
        }

        public List<Thing> ThingsOfDef(ThingDef def)
        {
            return def != null && byDef.TryGetValue(def, out List<Thing> list) ? list : Empty;
        }
    }

    public enum WipeMode
    {
        Vanish,
        Deconstruct
    }

    public enum DestroyMode
    {
        Vanish,
        WillReplace,
        Deconstruct,
        Destroy,
        KillFinalize
    }

    public static class GenSpawn
    {
        public static Thing Spawn(Thing newThing, IntVec3 loc, Map map, WipeMode wipeMode = WipeMode.Vanish)
        {
            newThing.Position = loc;
            newThing.SpawnSetup(map, false);
            map.listerThings.Add(newThing);
            map.thingGrid.Register(newThing);
            return newThing;
        }
    }

    public static class ThingMaker
    {
        private static int nextId = 5000;

        public static Thing MakeThing(ThingDef def, ThingDef stuff = null)
        {
            var thing = (Thing)Activator.CreateInstance(def.thingClass);
            thing.def = def;
            thing.thingIDNumber = ++nextId;
            return thing;
        }
    }

    public class Pawn : Thing
    {
        public bool Dead;
        public Pawn_HealthTracker health = new Pawn_HealthTracker();
    }

    public class Pawn_HealthTracker
    {
        public HediffSet hediffSet = new HediffSet();
    }

    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
    }

    public class Hediff
    {
        public float Severity;
    }

    public class Hediff_Injury : Hediff
    {
        public void Heal(float amount)
        {
            Severity = Math.Max(0f, Severity - amount);
        }
    }

    public static class HediffUtility
    {
        public static bool IsPermanent(Hediff hediff)
        {
            return false;
        }
    }

    public struct DamageInfo
    {
        public DamageDef Def;
        public float Amount;

        public DamageInfo(
            DamageDef def,
            float amount,
            float armorPenetration = 0f,
            float angle = -1f,
            Thing instigator = null,
            Thing intendedTarget = null,
            bool instigatorGuilty = true,
            bool spawnFilth = true)
        {
            Def = def;
            Amount = amount;
        }
    }

    public static class GenExplosion
    {
        public static int Calls;

        public static void DoExplosion(
            IntVec3 center,
            Map map,
            float radius,
            DamageDef damType,
            Thing instigator,
            int damAmount = -1,
            float armorPenetration = -1f,
            bool doVisualEffects = true,
            bool damageFalloff = false)
        {
            Calls++;
        }
    }

    public static class GenTicks
    {
        public static readonly int TicksPerRealSecond = 60;
    }

    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string label)
        {
        }

        public static void Look<T>(ref T value, string label, T defaultValue)
        {
        }
    }

    public static class Scribe_References
    {
        public static void Look<T>(ref T value, string label) where T : Thing
        {
        }
    }

    public static class Scribe_Defs
    {
        public static void Look<T>(ref T value, string label) where T : Def
        {
        }
    }

    public static class Scribe_Deep
    {
        public static void Look<T>(ref T value, string label) where T : IExposable
        {
        }
    }
}

namespace RimWorld
{
    // The production files import this namespace for sibling game types; the shim only needs the
    // namespace to exist so `using RimWorld;` compiles.
    internal static class HarnessNamespaceMarker
    {
    }
}

namespace WhisperOfTheForest.Experimental
{
    public static class ExperimentLog
    {
        public static readonly List<string> Entries = new List<string>();

        public static void Record(string category, Verse.Pawn actor, string detail)
        {
            Entries.Add(category + "|" + detail);
        }
    }
}

