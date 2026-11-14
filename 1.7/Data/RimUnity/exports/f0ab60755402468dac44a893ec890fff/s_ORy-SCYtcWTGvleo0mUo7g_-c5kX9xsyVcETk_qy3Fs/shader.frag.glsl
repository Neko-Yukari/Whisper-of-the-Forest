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


#line 56 0
struct RU_MaterialData_0
{
    vec4 _Color; // @default = vec4(0.55, 0.4, 0.36, 0.8) @color
};


#line 59
layout(binding = 0, set = 2, std140) uniform block_RU_MaterialData_0
{
    vec4 _Color; // @default = vec4(0.55, 0.4, 0.36, 0.8) @color
}RU_Material_0;

#line 63
layout(location = 0)
out vec4 entryPointParam_frag_0;


#line 63
layout(location = 1) in vec2 i_uv_0; // @vertex TexCoord0 optional


#line 63
layout(location = 2) in vec4 i_color_0; // @vertex Color optional


#line 63
void main()
{

#line 63
    entryPointParam_frag_0 = vec4(RU_Material_0._Color.xyz * i_color_0.xyz, RU_Material_0._Color.w * i_color_0.w * (RU_CompatSmoothstep(0.0, 0.18000000715255737, i_uv_0.y) * RU_CompatSmoothstep(0.0, 0.18000000715255737, 1.0 - i_uv_0.y)));

#line 63
    return;
}


// @rimunity-host-matrices 1
