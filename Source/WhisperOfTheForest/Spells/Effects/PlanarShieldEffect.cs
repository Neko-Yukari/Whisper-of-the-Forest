using System.Collections.Generic;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Shields;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    public sealed class PlanarShieldEffect : SpellEffect
    {
        private float spanLength;
        private int charges;
        private int lifetimeTicks;
        private float selfForwardOffset;
        private string vfxDefName;
        private SpellThemeDef theme;

        public PlanarShieldEffect()
        {
        }

        public PlanarShieldEffect(
            float spanLength,
            int charges,
            int lifetimeTicks,
            float selfForwardOffset,
            string vfxDefName,
            SpellThemeDef theme)
        {
            this.spanLength = spanLength;
            this.charges = charges;
            this.lifetimeTicks = lifetimeTicks;
            this.selfForwardOffset = selfForwardOffset;
            this.vfxDefName = vfxDefName;
            this.theme = theme;
        }

        public float SpanLength => spanLength;
        public int Charges => charges;
        public int LifetimeTicks => lifetimeTicks;

        public override string DiagnosticName => "planar-shield";

        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
            {
                return 0;
            }

            var effectPoint = new Vector2(cell.x + 0.5f, cell.z + 0.5f);
            Vector2? casterPoint = caster != null && caster.Spawned && caster.Map == map
                ? new Vector2(caster.DrawPos.x, caster.DrawPos.z)
                : (Vector2?)null;
            Vector2 fallbackFacing = caster != null
                ? new Vector2(caster.Rotation.FacingCell.x, caster.Rotation.FacingCell.z)
                : new Vector2(0f, 1f);
            bool isSelfCast = primary != null && primary == caster;

            PlanarShieldGeometry geometry = ShieldGeometry.ResolvePlanar(
                effectPoint,
                casterPoint,
                fallbackFacing,
                isSelfCast,
                selfForwardOffset,
                spanLength);

            var spawnCell = new IntVec3(
                Mathf.Clamp(Mathf.FloorToInt(geometry.Center.x), 0, map.Size.x - 1),
                0,
                Mathf.Clamp(Mathf.FloorToInt(geometry.Center.y), 0, map.Size.z - 1));

            ShieldInterception.SpawnPlanar(
                map,
                spawnCell,
                caster?.Faction,
                geometry,
                spanLength,
                charges,
                lifetimeTicks,
                vfxDefName,
                theme);
            return 1;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref spanLength, "spanLength");
            Scribe_Values.Look(ref charges, "charges");
            Scribe_Values.Look(ref lifetimeTicks, "lifetimeTicks");
            Scribe_Values.Look(ref selfForwardOffset, "selfForwardOffset");
            Scribe_Values.Look(ref vfxDefName, "vfxDefName");
            Scribe_Defs.Look(ref theme, "theme");
        }
    }

    public sealed class PlanarShieldGlyphDef : EffectGlyphDef
    {
        public int baseCharges;
        public int maxCharges;
        public int lifetimeTicks;
        public float maxLength;
        public float selfForwardOffset;
        public string vfxDef;

        public override SpellEffect Compile(
            IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff,
            SpellPayload payload = null)
        {
            float magnitude = 1f;
            float extraHalfLength = 0f;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                {
                    magnitude *= augment.factor;
                }
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    extraHalfLength += augment.radius;
                }
            }

            int finalCharges = ShieldGeometry.ComputeCharges(baseCharges, magnitude, maxCharges);
            float finalLength = ShieldGeometry.ComputeSpanLength(radius, extraHalfLength, maxLength);
            return new PlanarShieldEffect(finalLength, finalCharges, lifetimeTicks, selfForwardOffset, vfxDef, theme);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (baseCharges <= 0)
            {
                yield return "PlanarShield needs a positive baseCharges";
            }

            if (maxCharges < baseCharges)
            {
                yield return "PlanarShield maxCharges must be at least baseCharges";
            }

            if (lifetimeTicks <= 0)
            {
                yield return "PlanarShield needs a positive lifetimeTicks";
            }

            if (radius <= 0f)
            {
                yield return "PlanarShield needs a positive radius (half-length)";
            }

            if (maxLength < radius * 2f)
            {
                yield return "PlanarShield maxLength must be at least radius * 2";
            }

            if (selfForwardOffset < 0f)
            {
                yield return "PlanarShield selfForwardOffset must not be negative";
            }

            if (vfxDef.NullOrEmpty())
            {
                yield return "PlanarShield must declare its vfxDef";
            }

            foreach (string error in AreaErrors(radius))
            {
                yield return error;
            }
        }
    }
}
