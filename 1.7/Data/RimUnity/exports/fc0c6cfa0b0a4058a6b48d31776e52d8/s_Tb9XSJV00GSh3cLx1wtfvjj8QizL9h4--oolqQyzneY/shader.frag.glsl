#version 450
float RU_CompatSmoothstep(float low, float high, float x) {
    if (low == high) return step(low, x);
    float t = clamp((x - low) / (high - low), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}
vec2 RU_CompatSmoothstep(vec2 low, vec2 high, vec2 x) {
    return vec2(RU_CompatSmoothstep(low.x, high.x, x.x), RU_CompatSmoothstep(low.y, high.y, x.y));
}
vec2 RU_CompatSmoothstep(float low, float high, vec2 x) { return RU_CompatSmoothstep(vec2(low), vec2(high), x); }
vec3 RU_CompatSmoothstep(vec3 low, vec3 high, vec3 x) {
    return vec3(RU_CompatSmoothstep(low.x, high.x, x.x), RU_CompatSmoothstep(low.y, high.y, x.y), RU_CompatSmoothstep(low.z, high.z, x.z));
}
vec3 RU_CompatSmoothstep(float low, float high, vec3 x) { return RU_CompatSmoothstep(vec3(low), vec3(high), x); }
vec4 RU_CompatSmoothstep(vec4 low, vec4 high, vec4 x) {
    return vec4(RU_CompatSmoothstep(low.x, high.x, x.x), RU_CompatSmoothstep(low.y, high.y, x.y), RU_CompatSmoothstep(low.z, high.z, x.z), RU_CompatSmoothstep(low.w, high.w, x.w));
}
vec4 RU_CompatSmoothstep(float low, float high, vec4 x) { return RU_CompatSmoothstep(vec4(low), vec4(high), x); }


#line 66 0
layout(binding = 1, set = 2)
uniform sampler2D _MainTex; // @default = white


#line 68



#line 56
struct RU_MaterialData_0
{
    float _KeepHigh; // @default = 1.2
    float _KeepLow; // @default = 0.55
    float _Strength; // @default = 0.62
};


#line 61
layout(binding = 0, set = 2, std140) uniform block_RU_MaterialData_0
{
    float _KeepHigh; // @default = 1.2
    float _KeepLow; // @default = 0.55
    float _Strength; // @default = 0.62
}RU_Material_0;


vec4 RU_tex2D_MainTex_0(vec2 uv_0)
{

#line 70
    return (texture(_MainTex, (vec2(uv_0.x, 1.0 - uv_0.y))));
}


#line 70
layout(location = 0)
out vec4 entryPointParam_frag_0;


#line 70
layout(location = 1) in vec2 input_uv_0; // @vertex TexCoord0 optional


#line 78
void main()
{

#line 78
    vec4 source_0 = RU_tex2D_MainTex_0(input_uv_0);

#line 78
    vec3 _S1 = source_0.xyz;

#line 78
    entryPointParam_frag_0 = vec4(_S1 * (1.0 - RU_Material_0._Strength * (1.0 - RU_CompatSmoothstep(RU_Material_0._KeepLow, RU_Material_0._KeepHigh, dot(_S1, vec3(0.2125999927520752, 0.71520000696182251, 0.07220000028610229))))), source_0.w);

#line 78
    return;
}


// @rimunity-host-matrices 1
