#version 450

// Procedural domain-warped FBM + ridged turbulence fire tornado shader routed to Layer 15 (HDR).
// Vertex attributes:
//   vColor.r = radial/limb edge distance [0..1] (1 = deepest core, 0 = outer boundary)
//   vColor.g = linearBlendSpace flag (1.0 when HDR layer blends in Linear)
//   vColor.b = collapse dissolve [0..1]
//   vColor.a = master opacity [0..1]
//   vUv.x    = azimuthal coordinate u [0..1] (periodic across circumference)
//   vUv.y    = normalized height / radius v [0..1]
//   vUv.z    = elapsedSeconds (game-tick synchronized)
//   vUv.w    = hdrIntensity multiplier

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec4 vUv;

layout(location = 0) out vec4 outColor;

float hash21(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float pnoise(vec2 p, float periodX)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);

    float i0 = mod(i.x, periodX);
    float i1 = mod(i.x + 1.0, periodX);

    float a = hash21(vec2(i0, i.y));
    float b = hash21(vec2(i1, i.y));
    float c = hash21(vec2(i0, i.y + 1.0));
    float d = hash21(vec2(i1, i.y + 1.0));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbmP(vec2 p, float basePeriodX)
{
    float sum = 0.0;
    float amp = 0.5;
    float period = basePeriodX;
    for (int i = 0; i < 4; i++)
    {
        sum += amp * pnoise(p, period);
        p = vec2(p.x * 2.0, p.y * 2.03);
        period *= 2.0;
        amp *= 0.5;
    }
    return sum;
}

float ridgedP(vec2 p, float basePeriodX)
{
    float sum = 0.0;
    float amp = 0.52;
    float period = basePeriodX;
    for (int i = 0; i < 3; i++)
    {
        float n = 1.0 - abs(pnoise(p, period) * 2.0 - 1.0);
        sum += amp * n * n;
        p = vec2(p.x * 2.0, p.y * 2.07);
        period *= 2.0;
        amp *= 0.5;
    }
    return sum;
}

vec3 decodeStored(vec3 c)
{
    return mix(pow((c + 0.055) / 1.055, vec3(2.4)), c / 12.92, lessThanEqual(c, vec3(0.04045)));
}

void main()
{
    float t = vUv.z;
    float isBackWall = vUv.w < 0.0 ? 1.0 : 0.0;
    float hdr = abs(vUv.w);
    float edge = clamp(vColor.r, 0.0, 1.0);
    float dissolve = clamp(vColor.b, 0.0, 1.0);

    // Clockwise-from-above horizontal rotation synchronized with RimLumen +Y Vortex (dtheta/dt < 0):
    // u increases clockwise (+t across south/front -X motion, -X across north/back +X motion),
    // with low azimuthal frequency & high vertical frequency so stacked horizontal spinning bands dominate.
    float uFast = fract(vUv.x + t * 1.72 - vUv.y * 0.46);
    float uShear = fract(vUv.x + t * 2.35 - vUv.y * 0.68);

    vec2 q = vec2(
        fbmP(vec2(uFast * 2.0, vUv.y * 9.0 - t * 0.34), 2.0),
        fbmP(vec2(uShear * 3.0 + 1.3, vUv.y * 12.0 - t * 0.46 + 4.7), 3.0)
    );

    float bandFbm = fbmP(vec2(uFast * 2.0 + q.x * 0.65, vUv.y * 15.0 + q.y * 0.50 - t * 0.38), 2.0);
    float coilRidge = ridgedP(vec2(uShear * 4.0 - q.y * 0.45, vUv.y * 24.0 + q.x * 0.38 - t * 0.52), 4.0);

    // Explicit horizontal/slightly-tilted spinning vortex coils (integer azimuthal multiplier preserves u=0..1 seam)
    float spiralPhase = (vUv.x + t * 1.85) * 2.0 + vUv.y * 11.5 + (bandFbm - 0.5) * 1.55;
    float horizCoil = smoothstep(0.16, 0.88, 0.5 + 0.5 * cos(spiralPhase * 6.2831853));

    // Front shell fades slightly lower near the top rim than the back wall, opening a hollow 60-deg top mouth
    float topCutoff = mix(
        smoothstep(0.76, 0.96, vUv.y + (1.0 - horizCoil) * 0.14),
        smoothstep(0.84, 1.00, vUv.y + (1.0 - bandFbm) * 0.12),
        isBackWall
    );
    float vEnv = smoothstep(0.0, 0.06, vUv.y) * (1.0 - topCutoff);

    float density = (edge * 1.08 + bandFbm * 0.42 + horizCoil * 0.38 + coilRidge * 0.24 - 0.44 - dissolve * 0.92) * vEnv;
    float alpha = clamp(smoothstep(0.02, 0.30, density) * vColor.a, 0.0, isBackWall > 0.5 ? 0.94 : 0.90);

    if (alpha <= 0.001)
    {
        outColor = vec4(0.0);
        return;
    }

    float rawTemp = clamp(edge * 0.38 + horizCoil * 0.36 + bandFbm * 0.26 + coilRidge * 0.24 - vUv.y * 0.10, 0.0, 1.0);
    float temp = mix(rawTemp, rawTemp * 0.62, isBackWall);

    vec3 smokeDark = vec3(0.09, 0.015, 0.025);
    vec3 bloodRed  = vec3(0.58, 0.045, 0.075);
    vec3 cordium   = vec3(0.96, 0.22, 0.08);
    vec3 coreHeat  = vec3(1.00, 0.76, 0.46);

    vec3 col = mix(smokeDark, bloodRed, smoothstep(0.05, 0.34, temp));
    col = mix(col, cordium, smoothstep(0.32, 0.68, temp));
    col = mix(col, coreHeat, smoothstep(0.70, 0.98, temp));

    if (vColor.g > 0.5)
    {
        col = decodeStored(col);
    }

    float glow = mix(0.24, hdr * (isBackWall > 0.5 ? 1.05 : 1.78), smoothstep(0.22, 0.96, temp));
    outColor = vec4(col * (glow * alpha), alpha);
}
