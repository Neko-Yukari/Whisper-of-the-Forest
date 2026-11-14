#version 450
layout(binding = 0, set = 2, std140) uniform block_RU_MaterialData_0
{
    vec4 _Color; // @default = vec4(0.55, 0.4, 0.36, 0.8) @color
}RU_Material_0;


#line 17 0
struct RU_ObjectData_0
{
    mat4 model_0;
};


#line 18
layout(push_constant, std430) uniform block_RU_ObjectData_0
{
    mat4 model_0;
}RU_Object_0;

#line 15
struct RU_CameraData_0
{
    mat4 ViewProj_0;
};


#line 16
layout(binding = 0, std140) uniform block_RU_CameraData_0
{
    mat4 ViewProj_0;
}RU_Camera_0;

#line 63
layout(location = 1)
out vec2 entryPointParam_vert_uv_0;


#line 63
layout(location = 2)
out vec4 entryPointParam_vert_color_0;


#line 63
layout(location = 0) in vec4 v_vertex_0; // @vertex Position required


#line 63
layout(location = 1) in vec2 v_uv_0; // @vertex TexCoord0 optional


#line 63
layout(location = 2) in vec4 v_color_0; // @vertex Color optional


#line 63
struct vary_0
{
    vec4 pos_0;
    vec2 uv_0;
    vec4 color_0;
};


#line 63
void main()
{

#line 63
    vec3 origin_0 = (((vec4(0.0, 0.0, 0.0, 1.0)) * (transpose(RU_Object_0.model_0)))).xyz;

#line 63
    vec3 p_0 = (((v_vertex_0) * (transpose(RU_Object_0.model_0)))).xyz - origin_0;

#line 63
    p_0[0] = p_0[0] + sin(v_uv_0.x * 3.0) * 0.11249999701976776 * length(vec3(transpose(RU_Object_0.model_0)[0][0], transpose(RU_Object_0.model_0)[1][0], transpose(RU_Object_0.model_0)[2][0]));

#line 63
    p_0[2] = p_0.z * 0.5 + p_0.y * 0.70710700750350952;

#line 63
    vary_0 o_0;

#line 63
    o_0.pos_0 = (((vec4(origin_0 + p_0, 1.0)) * (transpose(RU_Camera_0.ViewProj_0))));

#line 63
    o_0.uv_0 = v_uv_0;

#line 63
    o_0.color_0 = v_color_0;

#line 63
    vary_0 _S1 = o_0;

#line 63
    gl_Position = o_0.pos_0;

#line 63
    entryPointParam_vert_uv_0 = _S1.uv_0;

#line 63
    entryPointParam_vert_color_0 = _S1.color_0;

#line 63
    return;
}


// @rimunity-host-matrices 1
