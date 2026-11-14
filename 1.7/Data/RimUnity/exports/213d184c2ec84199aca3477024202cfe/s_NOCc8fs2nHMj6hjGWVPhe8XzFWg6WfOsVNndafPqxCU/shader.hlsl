// @rimunity-vertex 0 Position required
// @rimunity-vertex 1 TexCoord0 optional
// @rimunity-vertex 2 Color optional
// @rimunity-vertex 3 Normal optional
// @rimunity-vertex 4 Tangent optional
// @rimunity-vertex 5 TexCoord1 optional
// @rimunity-vertex 6 TexCoord2 optional
// @rimunity-vertex 7 TexCoord3 optional
// @rimunity-vertex 8 TexCoord4 optional
// @rimunity-vertex 9 TexCoord5 optional
// @rimunity-vertex 10 TexCoord6 optional
// @rimunity-vertex 11 TexCoord7 optional
// @rimunity-vertex 12 BlendWeight optional
// @rimunity-vertex 13 BlendIndices optional
struct RU_CameraData { float4x4 ViewProj; };
[[vk::binding(0, 0)]] ConstantBuffer<RU_CameraData> RU_Camera;
struct RU_ObjectData { float4x4 model; };
[[vk::push_constant]] ConstantBuffer<RU_ObjectData> RU_Object;
#define unity_ObjectToWorld RU_Object.model
#define UNITY_MATRIX_M RU_Object.model
#define UNITY_MATRIX_VP RU_Camera.ViewProj
#define UNITY_MATRIX_MVP mul(RU_Camera.ViewProj, RU_Object.model)
float4 UnityObjectToClipPos(float4 p) { return mul(RU_Camera.ViewProj, mul(RU_Object.model, p)); }
float4 UnityObjectToClipPos(float3 p) { return UnityObjectToClipPos(float4(p, 1)); }
float3 TransformObjectToWorld(float3 p) { return mul(RU_Object.model, float4(p, 1)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(RU_Camera.ViewProj, float4(p, 1)); }
float4 UnityWorldToClipPos(float3 p) { return TransformWorldToHClip(p); }
float3 UnityObjectToWorldNormal(float3 normal) {
    float3x3 m = (float3x3)RU_Object.model;
    float3x3 cofactors = float3x3(cross(m[1],m[2]), cross(m[2],m[0]), cross(m[0],m[1]));
    return normalize(mul(cofactors, normal) * sign(dot(m[0],cofactors[0])));
}
float3 UnityObjectToWorldDir(float3 direction) { return normalize(mul((float3x3)RU_Object.model, direction)); }
float4 TransformObjectToHClip(float3 p) { return UnityObjectToClipPos(p); }
float4 ComputeScreenPos(float4 p) { return float4(p.xy * 0.5 + p.w * 0.5, p.zw); }
#define TRANSFORM_TEX(uv,name) ((uv).xy * name##_ST.xy + name##_ST.zw)
#define UNITY_INITIALIZE_OUTPUT(type,name) name = (type)0
//非雾、非实例化、单眼变体在 Unity 中也不产生这些字段和调用。
#define UNITY_FOG_COORDS(index)
#define UNITY_TRANSFER_FOG(output,position)
#define UNITY_APPLY_FOG(coord,color)
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_SETUP_INSTANCE_ID(input)
#define UNITY_TRANSFER_INSTANCE_ID(input,output)
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output)
#define SAMPLE_TEXTURE2D(tex,samp,uv) tex.Sample(samp,float2((uv).x,1-(uv).y))
#define SAMPLE_TEXTURE2D_LOD(tex,samp,uv,lod) tex.SampleLevel(samp,float2((uv).x,1-(uv).y),lod)
#define SAMPLE_TEXTURE2D_BIAS(tex,samp,uv,bias) tex.SampleBias(samp,float2((uv).x,1-(uv).y),bias)
float4 ComputeGrabScreenPos(float4 p) { float4 r = p * 0.5; r.xy = float2(r.x, -r.y) + r.w; r.zw = p.zw; return r; }
float3 UnpackNormal(float4 n) { n.x *= n.w; float2 xy = n.xy * 2 - 1; return float3(xy, sqrt(1 - saturate(dot(xy, xy)))); }
float UnityGet2DClipping(float2 p, float4 r) { float2 v = step(r.xy,p)*step(p,r.zw); return v.x*v.y; }
struct appdata_base { [[vk::location(0)]] float4 vertex : POSITION; [[vk::location(3)]] float3 normal : NORMAL; [[vk::location(1)]] float4 texcoord : TEXCOORD0; };
struct appdata_tan { [[vk::location(0)]] float4 vertex : POSITION; [[vk::location(4)]] float4 tangent : TANGENT; [[vk::location(3)]] float3 normal : NORMAL; [[vk::location(1)]] float4 texcoord : TEXCOORD0; };
struct appdata_full { [[vk::location(0)]] float4 vertex : POSITION; [[vk::location(4)]] float4 tangent : TANGENT; [[vk::location(3)]] float3 normal : NORMAL; [[vk::location(1)]] float4 texcoord : TEXCOORD0; [[vk::location(5)]] float4 texcoord1 : TEXCOORD1; [[vk::location(6)]] float4 texcoord2 : TEXCOORD2; [[vk::location(7)]] float4 texcoord3 : TEXCOORD3; [[vk::location(2)]] float4 color : COLOR; };
struct RU_MaterialData {
float4 _Time;
};
[[vk::binding(0, 2)]] ConstantBuffer<RU_MaterialData> RU_Material;
// @rimunity-material _Time
#define _Time (RU_Material._Time)
[[vk::binding(1, 2)]] Texture2D _NoiseTex;
// @rimunity-texture 1 _NoiseTex
[[vk::binding(2, 2)]] SamplerState RU_Sampler_NoiseTex;
// @rimunity-combined 1 2
float4 RU_tex2D_NoiseTex(float2 uv) { return _NoiseTex.Sample(RU_Sampler_NoiseTex, float2(uv.x,1-uv.y)); }
float4 RU_tex2D_NoiseTex(float3 uv) { return RU_tex2D_NoiseTex(uv.xy); }
float4 RU_tex2D_NoiseTex(float4 uv) { return RU_tex2D_NoiseTex(uv.xy); }
float4 RU_tex2Dlod_NoiseTex(float4 uv) { return _NoiseTex.SampleLevel(RU_Sampler_NoiseTex, float2(uv.x,1-uv.y),uv.w); }
float4 RU_tex2Dbias_NoiseTex(float4 uv) { return _NoiseTex.SampleBias(RU_Sampler_NoiseTex, float2(uv.x,1-uv.y),uv.w); }
float4 RU_tex2Dproj_NoiseTex(float4 uv) { return _NoiseTex.Sample(RU_Sampler_NoiseTex, float2(uv.x/uv.w,1-uv.y/uv.w)); }
float4 RU_tex2Dgrad_NoiseTex(float2 uv,float2 dx,float2 dy) { return _NoiseTex.SampleGrad(RU_Sampler_NoiseTex,float2(uv.x,1-uv.y),float2(dx.x,-dx.y),float2(dy.x,-dy.y)); }

 struct VertexInput { [[vk::location(0)]] float4 vertex : POSITION ; [[vk::location(1)]] float2 viewPlane : TEXCOORD0 ; } ; struct VertexOutput { float4 position : SV_POSITION ; [[vk::location(1)]] float2 viewPlane : TEXCOORD0 ; } ; VertexOutput Vert ( VertexInput input ) { VertexOutput output ; output . position = UnityObjectToClipPos ( input . vertex ) ; output . viewPlane = input . viewPlane ; return output ; } float Noise3 ( float3 p ) { float3 cell = floor ( p ) ; float3 f = frac ( p ) ; f = f * f * ( 3.0 - 2.0 * f ) ; float2 q = cell . xy + float2 ( 37.0 , 17.0 ) * cell . z + f . xy ; float2 n = RU_tex2Dlod_NoiseTex( float4 ( ( q + 0.5 ) / 256.0 , 0.0 , 0.0 ) ) . rg ; return lerp ( n . r , n . g , f . z ) ; } float Fbm ( float3 p ) { return Noise3 ( p ) * 0.57 + Noise3 ( p * 2.03 + 9.7 ) * 0.28 + Noise3 ( p * 4.11 + 17.3 ) * 0.15 ; } float2 Axis ( float y ) { return float2 ( sin ( y * 0.47 + _Time . y * 0.18 ) * 0.26 , sin ( y * 0.37 - _Time . y * 0.15 ) * 0.19 ) * ( y / 10.0 ) ; } float Radius ( float y ) { return 0.48 + 0.105 * y + 0.010 * y * y + 0.43 * exp ( - y * 3.0 ) ; } float3 Density ( float3 p ) { float h = p . y ; float2 q = p . xz - Axis ( h ) ; float r = length ( q ) ; float rad = Radius ( h ) ; float a = h * 0.92 + _Time . y * 1.85 / ( 1.0 + h * 0.055 ) ; float s , c ; sincos ( a , s , c ) ; float2 w = float2 ( c * q . x + s * q . y , - s * q . x + c * q . y ) ; float n = Fbm ( float3 ( w * 1.85 , h * 0.82 - _Time . y * 0.65 ) ) ; float billow = Fbm ( float3 ( w * 0.8 + 4.0 , h * 0.44 - _Time . y * 0.24 ) ) ; float edge = rad * ( 0.87 + 0.38 * billow ) + 0.19 * ( n - 0.5 ) ; float d = 1.0 - smoothstep ( edge * 0.63 , edge * 1.15 , r ) ; d *= smoothstep ( - 0.08 , 0.28 , h ) * ( 1.0 - smoothstep ( 8.8 , 11.3 , h ) ) ; d *= 0.40 + 0.82 * n ; float skinOffset = ( r / rad - 0.88 ) * 3.1 ; float skin = exp ( - skinOffset * skinOffset ) ; float filaments = smoothstep ( 0.48 , 0.73 , n ) * skin ; return float3 ( d , filaments , billow ) ; } bool IntersectSlab ( float origin , float direction , float lower , float upper , inout float nearT , inout float farT ) { if ( abs ( direction ) < 0.000001 ) return origin >= lower && origin <= upper ; float t0 = ( lower - origin ) / direction ; float t1 = ( upper - origin ) / direction ; nearT = max ( nearT , min ( t0 , t1 ) ) ; farT = min ( farT , max ( t0 , t1 ) ) ; return farT > nearT ; } float4 Frag ( VertexOutput input ) : SV_Target { const float diagonal = 0.70710678118 ; float3 viewUp = float3 ( 0.0 , diagonal , diagonal ) ; float3 direction = float3 ( 0.0 , - diagonal , diagonal ) ; float3 origin = float3 ( input . viewPlane . x , 0.0 , 0.0 ) + viewUp * input . viewPlane . y - direction * 20.0 ; float nearT = 0.0 ; float farT = 1.0e20 ; if ( ! IntersectSlab ( origin . x , direction . x , - 4.0 , 4.0 , nearT , farT ) || ! IntersectSlab ( origin . y , direction . y , 0.0 , 11.4 , nearT , farT ) || ! IntersectSlab ( origin . z , direction . z , - 4.0 , 4.0 , nearT , farT ) ) discard ; float stepSize = ( farT - nearT ) / 64.0 ; float3 entry = origin + direction * nearT ; float jitter = frac ( sin ( dot ( entry . xz + entry . y , float2 ( 12.9898 , 78.233 ) ) ) * 43758.5453 ) ; float t = nearT + stepSize * jitter ; float trans = 1.0 ; float3 color = 0.0 ; {[ loop ] for ( int i = 0 ; i < 64 ; i ++ ) { float3 pos = origin + direction * t ; float3 d = Density ( pos ) ; float alpha = 1.0 - exp ( - d . x * stepSize * 2.7 ) ; float3 sand = lerp ( float3 ( 0.07 , 0.043 , 0.020 ) , float3 ( 0.30 , 0.20 , 0.105 ) , d . z ) ; float3 emission = sand + float3 ( 0.42 , 0.27 , 0.125 ) * d . y * ( 0.65 + 0.35 * sin ( pos . y * 0.8 + 1.0 ) ) ; color += trans * alpha * emission ; trans *= 1.0 - alpha ; if ( trans < 0.012 ) break ; t += stepSize ; }} return float4 ( color , 1.0 - trans ) ; } 
