#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec4 vUv;

layout(location = 0) out vec4 outColor;

void main()
{
    float edge = pow(clamp(1.0 - abs(vUv.x * 2.0 - 1.0), 0.0, 1.0), max(0.25, vUv.w));
    float alpha = vColor.a * edge;
    outColor = vec4(vColor.rgb * (vUv.z * alpha), 0.0);
}
