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
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half3 normal:TEXCOORD1;float3 world:TEXCOORD2;half4 maskA:TEXCOORD3;half4 maskB:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);
                o.maskA=i.maskA;o.maskB=i.maskB;return o;
            }
            void SelectRegion(half weight,half load,inout half strongest,inout half runnerUp,inout half score)
            {
                if(weight>strongest){runnerUp=strongest;strongest=weight;score=load;}
                else runnerUp=max(runnerUp,weight);
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                half strongest=0,runnerUp=0,score=0;
                SelectRegion(i.maskA.x,_LoadsA.x,strongest,runnerUp,score);
                SelectRegion(i.maskA.y,_LoadsA.y,strongest,runnerUp,score);
                SelectRegion(i.maskA.z,_LoadsA.z,strongest,runnerUp,score);
                SelectRegion(i.maskA.w,_LoadsA.w,strongest,runnerUp,score);
                SelectRegion(i.maskB.x,_LoadsB.x,strongest,runnerUp,score);
                SelectRegion(i.maskB.y,_LoadsB.y,strongest,runnerUp,score);
                SelectRegion(i.maskB.z,_LoadsB.z,strongest,runnerUp,score);
                score=saturate(score);
                half covered=step(.16h,strongest);
                // One region and one categorical color per pixel: no blended boundary heat.
                half3 heat=score<.36h?half3(.02,.78,.16):score<.68h?half3(1,.68,.015):half3(.98,.035,.025);
                albedo=lerp(albedo,heat,covered*_HeatOpacity);
                half dividingLine=covered*(1-step(.09h,strongest-runnerUp));
                half outerLine=step(.10h,strongest)*(1-step(.19h,strongest));
                albedo=lerp(albedo,half3(.015,.025,.035),saturate(dividingLine+outerLine));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 n=normalize(i.normal);
                half3 lit=albedo*(max(SampleSH(n),half3(.28,.28,.28))+light.color*(.22+.78*saturate(dot(n,light.direction)))*lerp(.65,1,light.shadowAttenuation));
                lit+=heat*covered*score*score*.08;
                return half4(lit,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
