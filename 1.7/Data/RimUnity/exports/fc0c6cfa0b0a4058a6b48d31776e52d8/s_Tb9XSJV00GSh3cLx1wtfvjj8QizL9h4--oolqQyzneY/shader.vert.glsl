#version 450
layout(binding = 1, set = 2)
uniform sampler2D _MainTex; // @default = white
layout(binding = 0, set = 2, std140) uniform block_RU_MaterialData_0
{
    float _KeepHigh; // @default = 1.2
    float _KeepLow; // @default = 0.55
    float _Strength; // @default = 0.62
}RU_Material_0;


#line 15 0
struct RU_CameraData_0
{
    mat4 ViewProj_0;
};


#line 16
layout(binding = 0, std140) uniform block_RU_CameraData_0
{
    mat4 ViewProj_0;
}RU_Camera_0;

#line 17
struct RU_ObjectData_0
{
    mat4 model_0;
};


#line 18
layout(push_constant, std430) uniform block_RU_ObjectData_0
{
    mat4 model_0;
}RU_Object_0;
vec4 UnityObjectToClipPos_0(vec4 p_0)
{

#line 23
    return ((((((p_0) * (transpose(RU_Object_0.model_0))))) * (transpose(RU_Camera_0.ViewProj_0))));
}


#line 78
layout(location = 1)
out vec2 entryPointParam_vert_img_uv_0;


#line 78
layout(location = 0) in vec4 v_vertex_0; // @vertex Position required


#line 78
layout(location = 1) in vec2 v_texcoord_0; // @vertex TexCoord0 optional


#line 78
struct v2f_img_0
{
    vec4 pos_0;
    vec2 uv_0;
};


#line 78
void main()
{

#line 78
    v2f_img_0 o_0;

#line 78
    o_0.pos_0 = UnityObjectToClipPos_0(v_vertex_0);

#line 78
    o_0.uv_0 = v_texcoord_0;

#line 78
    v2f_img_0 _S1 = o_0;

#line 78
    gl_Position = o_0.pos_0;

#line 78
    entryPointParam_vert_img_uv_0 = _S1.uv_0;

#line 78
    return;
}


// @rimunity-host-matrices 1
