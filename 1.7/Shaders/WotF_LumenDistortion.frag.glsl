#version 450

// Shield-surface-confined screen-space refraction routed to Layer 14 (RimLumenRenderHost.DistortionPassId).
// Geometry is analytically clamped to the shield silhouette so zero pixels outside the shield are ever touched.
layout(location = 0) in vec2 vNdc;
layout(location = 1) in vec2 vNdcOffset;
layout(location = 2) in vec2 vWaveParam;

layout(location = 0) out vec4 outOffset;

void main()
{
    vec2 ndcPerPixel = vec2(dFdx(vNdc.x), dFdy(vNdc.y));

    float x = clamp(vWaveParam.x * 2.0 - 1.0, -1.0, 1.0);
    float bell = max(0.0, 1.0 - x * x);
    float profile = (bell * bell) * (0.58 - 0.82 * x) * vWaveParam.y;

    if (abs(ndcPerPixel.x) < 1e-8 || abs(ndcPerPixel.y) < 1e-8 || abs(profile) < 1e-5)
    {
        outOffset = vec4(0.0);
        return;
    }

    vec2 pixelOffset = (vNdcOffset * profile) / ndcPerPixel;
    outOffset = vec4(pixelOffset, 0.0, 0.0);
}
