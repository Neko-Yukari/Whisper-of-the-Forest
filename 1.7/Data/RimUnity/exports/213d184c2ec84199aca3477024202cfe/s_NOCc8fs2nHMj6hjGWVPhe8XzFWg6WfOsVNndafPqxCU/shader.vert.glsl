#version 450
layout(binding = 1, set = 2)
uniform sampler2D _NoiseTex; // @default = gray
layout(binding = 0, set = 2, std140) uniform block_RU_MaterialData_0
{
    vec4 _Time;
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


#line 74
layout(location = 1)
out vec2 entryPointParam_Vert_viewPlane_0;


#line 74
layout(location = 0) in vec4 input_vertex_0; // @vertex Position required


#line 74
layout(location = 1) in vec2 input_viewPlane_0; // @vertex TexCoord0 optional


#line 74
struct VertexOutput_0
{
    vec4 position_0;
    vec2 viewPlane_0;
};


#line 74
void main()
{

#line 74
    VertexOutput_0 output_0;

#line 74
    output_0.position_0 = UnityObjectToClipPos_0(input_vertex_0);

#line 74
    output_0.viewPlane_0 = input_viewPlane_0;

#line 74
    VertexOutput_0 _S1 = output_0;

#line 74
    gl_Position = output_0.position_0;

#line 74
    entryPointParam_Vert_viewPlane_0 = _S1.viewPlane_0;

#line 74
    return;
}


// @rimunity-host-matrices 1
