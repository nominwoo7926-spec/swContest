Shader "FactoryTask/WorkerHeat"
{
    Properties
    {
        _BaseMap("Worker texture",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _HeatOpacity("Heat opacity",Range(0,1))=.72
        _LoadsA("Shoulders and arms",Vector)=(0,0,0,0)
        _LoadsB("Wrists and torso",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;half4 _BaseColor;float4 _LoadsA,_LoadsB;half _HeatOpacity;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 maskA:TEXCOORD2;float4 maskB:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half3 normal:TEXCOORD1;float3 world:TEXCOORD2;half2 heat:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);
                float mask=dot(i.maskA,1)+dot(i.maskB.xyz,1);
                o.heat=half2((dot(i.maskA,_LoadsA)+dot(i.maskB,_LoadsB))/max(mask,.0001),saturate(mask));return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                half score=saturate(i.heat.x);
                half3 green=half3(.045,.78,.23),yellow=half3(1,.72,.025),red=half3(.95,.025,.018);
                half3 heat=score<.5?lerp(green,yellow,score*2):lerp(yellow,red,score*2-1);
                // Keep garment detail visible beneath the surface color; no detached indicator geometry.
                half detail=dot(albedo,half3(.21,.72,.07));
                albedo=lerp(albedo,heat*(.68+detail*.5),i.heat.y*_HeatOpacity);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 n=normalize(i.normal);
                half3 lit=albedo*(max(SampleSH(n),half3(.28,.28,.28))+light.color*(.22+.78*saturate(dot(n,light.direction)))*lerp(.65,1,light.shadowAttenuation));
                lit+=heat*i.heat.y*score*score*.14;
                return half4(lit,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
