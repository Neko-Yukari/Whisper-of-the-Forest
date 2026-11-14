#version 450
#extension GL_EXT_control_flow_attributes : require
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
    vec4 _Time;
};


#line 59
layout(binding = 0, set = 2, std140) uniform block_RU_MaterialData_0
{
    vec4 _Time;
}RU_Material_0;

#line 62
layout(binding = 1, set = 2)
uniform sampler2D _NoiseTex; // @default = gray


#line 64



#line 74
bool IntersectSlab_0(float origin_0, float direction_0, float lower_0, float upper_0, inout float nearT_0, inout float farT_0)
{

#line 74
    if((abs(direction_0)) < 9.99999997475242708e-07)
    {

#line 74
        bool _S1;

#line 74
        if(origin_0 >= lower_0)
        {

#line 74
            _S1 = origin_0 <= upper_0;

#line 74
        }
        else
        {

#line 74
            _S1 = false;

#line 74
        }

#line 74
        return _S1;
    }

#line 74
    float t0_0 = (lower_0 - origin_0) / direction_0;

#line 74
    float t1_0 = (upper_0 - origin_0) / direction_0;

#line 74
    nearT_0 = max(nearT_0, min(t0_0, t1_0));

#line 74
    float _S2 = min(farT_0, max(t0_0, t1_0));

#line 74
    farT_0 = _S2;

#line 74
    return _S2 > nearT_0;
}


#line 74
vec2 Axis_0(float y_0)
{

#line 74
    return vec2(sin(y_0 * 0.4699999988079071 + RU_Material_0._Time.y * 0.18000000715255737) * 0.25999999046325684, sin(y_0 * 0.37000000476837158 - RU_Material_0._Time.y * 0.15000000596046448) * 0.18999999761581421) * (y_0 / 10.0);
}


#line 74
float Radius_0(float y_1)
{

#line 74
    return 0.47999998927116394 + 0.10499999672174454 * y_1 + 0.00999999977648258 * y_1 * y_1 + 0.43000000715255737 * exp(- y_1 * 3.0);
}


#line 69
vec4 RU_tex2Dlod_NoiseTex_0(vec4 uv_0)
{

#line 69
    return (textureLod(_NoiseTex, (vec2(uv_0.x, 1.0 - uv_0.y)), (uv_0.w)));
}



float Noise3_0(vec3 p_0)
{

#line 74
    vec3 cell_0 = floor(p_0);

#line 74
    vec3 f_0 = fract(p_0);

#line 74
    vec3 f_1 = f_0 * f_0 * (3.0 - 2.0 * f_0);

#line 74
    vec2 n_0 = RU_tex2Dlod_NoiseTex_0(vec4((cell_0.xy + vec2(37.0, 17.0) * cell_0.z + f_1.xy + 0.5) / 256.0, 0.0, 0.0)).xy;

#line 74
    return mix(n_0.x, n_0.y, f_1.z);
}


#line 74
float Fbm_0(vec3 p_1)
{

#line 74
    return Noise3_0(p_1) * 0.56999999284744263 + Noise3_0(p_1 * 2.02999997138977051 + 9.69999980926513672) * 0.2800000011920929 + Noise3_0(p_1 * 4.1100001335144043 + 17.29999923706054688) * 0.15000000596046448;
}


#line 74
vec3 Density_0(vec3 p_2)
{

#line 74
    float h_0 = p_2.y;

#line 74
    vec2 q_0 = p_2.xz - Axis_0(h_0);

#line 74
    float r_0 = length(q_0);

#line 74
    float rad_0 = Radius_0(h_0);

#line 74
    float a_0 = h_0 * 0.92000001668930054 + RU_Material_0._Time.y * 1.85000002384185791 / (1.0 + h_0 * 0.05499999970197678);

#line 74
    float s_0 = sin(a_0);

#line 74
    float c_0 = cos(a_0);

#line 74
    float _S3 = q_0.x;

#line 74
    float _S4 = q_0.y;

#line 74
    vec2 w_0 = vec2(c_0 * _S3 + s_0 * _S4, - s_0 * _S3 + c_0 * _S4);

#line 74
    float n_1 = Fbm_0(vec3(w_0 * 1.85000002384185791, h_0 * 0.81999999284744263 - RU_Material_0._Time.y * 0.64999997615814209));

#line 74
    float billow_0 = Fbm_0(vec3(w_0 * 0.80000001192092896 + 4.0, h_0 * 0.43999999761581421 - RU_Material_0._Time.y * 0.23999999463558197));

#line 74
    float edge_0 = rad_0 * (0.87000000476837158 + 0.37999999523162842 * billow_0) + 0.18999999761581421 * (n_1 - 0.5);

#line 74
    float skinOffset_0 = (r_0 / rad_0 - 0.87999999523162842) * 3.09999990463256836;

#line 74
    return vec3((1.0 - RU_CompatSmoothstep(edge_0 * 0.62999999523162842, edge_0 * 1.14999997615814209, r_0)) * (RU_CompatSmoothstep(-0.07999999821186066, 0.2800000011920929, h_0) * (1.0 - RU_CompatSmoothstep(8.80000019073486328, 11.30000019073486328, h_0))) * (0.40000000596046448 + 0.81999999284744263 * n_1), RU_CompatSmoothstep(0.47999998927116394, 0.73000001907348633, n_1) * exp(- skinOffset_0 * skinOffset_0), billow_0);
}


#line 74
layout(location = 0)
out vec4 entryPointParam_Frag_0;


#line 74
layout(location = 1) in vec2 input_viewPlane_0; // @vertex TexCoord0 optional


#line 74
void main()
{

#line 74
    const vec3 direction_1 = vec3(0.0, -0.70710676908493042, 0.70710676908493042);

#line 74
    vec3 origin_1 = vec3(input_viewPlane_0.x, 0.0, 0.0) + vec3(0.0, 0.70710676908493042, 0.70710676908493042) * input_viewPlane_0.y - direction_1 * 20.0;

#line 74
    float nearT_1 = 0.0;

#line 74
    float farT_1 = 1.00000002004087734e+20;

#line 74
    bool _S5 = IntersectSlab_0(origin_1.x, 0.0, -4.0, 4.0, nearT_1, farT_1);

#line 74
    bool _S6;

#line 74
    if(!_S5)
    {

#line 74
        _S6 = true;

#line 74
    }
    else
    {

#line 74
        bool _S7 = IntersectSlab_0(origin_1.y, -0.70710676908493042, 0.0, 11.39999961853027344, nearT_1, farT_1);

#line 74
        _S6 = !_S7;

#line 74
    }

#line 74
    if(_S6)
    {

#line 74
        _S6 = true;

#line 74
    }
    else
    {

#line 74
        bool _S8 = IntersectSlab_0(origin_1.z, 0.70710676908493042, -4.0, 4.0, nearT_1, farT_1);

#line 74
        _S6 = !_S8;

#line 74
    }

#line 74
    if(_S6)
    {

#line 74
        discard;

#line 74
    }

#line 74
    float stepSize_0 = (farT_1 - nearT_1) / 64.0;

#line 74
    vec3 entry_0 = origin_1 + direction_1 * nearT_1;

#line 74
    float _S9 = nearT_1 + stepSize_0 * fract(sin(dot(entry_0.xz + entry_0.y, vec2(12.98980045318603516, 78.233001708984375))) * 43758.546875);

#line 74
    const vec3 _S10 = vec3(0.0);

#line 74
    int i_0 = 0;

#line 74
    float t_0 = _S9;

#line 74
    float trans_0 = 1.0;

#line 74
    vec3 color_0 = _S10;

#line 74
    [[dont_unroll]]
    for(;;)
    {

#line 74
        if(i_0 < 64)
        {
        }
        else
        {

#line 74
            break;
        }

#line 74
        vec3 pos_0 = origin_1 + direction_1 * t_0;

#line 74
        vec3 d_0 = Density_0(pos_0);

#line 74
        float alpha_0 = 1.0 - exp(- d_0.x * stepSize_0 * 2.70000004768371582);

#line 74
        vec3 color_1 = color_0 + trans_0 * alpha_0 * (mix(vec3(0.07000000029802322, 0.0430000014603138, 0.01999999955296516), vec3(0.30000001192092896, 0.20000000298023224, 0.10499999672174454), vec3(d_0.z)) + vec3(0.41999998688697815, 0.27000001072883606, 0.125) * d_0.y * (0.64999997615814209 + 0.34999999403953552 * sin(pos_0.y * 0.80000001192092896 + 1.0)));

#line 74
        float trans_1 = trans_0 * (1.0 - alpha_0);

#line 74
        if(trans_1 < 0.01200000010430813)
        {

#line 74
            color_0 = color_1;

#line 74
            trans_0 = trans_1;

#line 74
            break;
        }

#line 74
        float t_1 = t_0 + stepSize_0;

#line 74
        i_0 = i_0 + 1;

#line 74
        t_0 = t_1;

#line 74
        trans_0 = trans_1;

#line 74
        color_0 = color_1;

#line 74
    }

#line 74
    entryPointParam_Frag_0 = vec4(color_0, 1.0 - trans_0);

#line 74
    return;
}


// @rimunity-host-matrices 1
