#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec4 vUv;

layout(location = 0) out vec4 outColor;

void main()
{
    outColor = vec4(vColor.rgb * vUv.z, vColor.a);
}
