#version 450

layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 2) in vec4 inColor; // @vertex Color optional @default = vec4(1.0, 1.0, 1.0, 1.0)
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0, 0.0, 0.0, 0.0)

layout(push_constant) uniform Push { mat4 modelMatrix; } push;

layout(location = 0) out vec2 vNdc;
layout(location = 1) out vec2 vNdcOffset;
layout(location = 2) out vec2 vWaveParam;

layout(set = 0, binding = 0) uniform VertexUniforms
{
    mat4 ViewProj;
} uVertex;

void main()
{
    vec4 worldPos = push.modelMatrix * vec4(inPosition, 1.0);
    vec4 clip0 = uVertex.ViewProj * worldPos;
    vec4 clip1 = uVertex.ViewProj * (worldPos + vec4(inUv0.z, 0.0, inUv0.w, 0.0));
    gl_Position = clip0;

    float invW0 = 1.0 / max(1e-5, abs(clip0.w));
    float invW1 = 1.0 / max(1e-5, abs(clip1.w));
    vec2 ndc0 = clip0.xy * invW0;
    vec2 ndc1 = clip1.xy * invW1;

    vNdc = ndc0;
    vNdcOffset = ndc1 - ndc0;
    vWaveParam = vec2(inUv0.x, inUv0.y * inColor.a);
}
