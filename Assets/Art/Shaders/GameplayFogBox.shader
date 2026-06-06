Shader "Custom/GameplayFogBox"
{
    Properties
    {
        [Header(Visibility)]
        _Visibility     ("Visibility (meters)",  Range(1, 1000))   = 100
        _Density        ("Density Multiplier",   Range(0, 5))      = 1
        _StepCount      ("Ray March Steps",      Range(8, 128))    = 32
        _MinFogAmount   ("Minimum Fog Amount",   Range(0, 1))      = 0

        [Header(Color)]
        _FogColor       ("Fog Color",            Color)            = (0.7, 0.75, 0.8, 1)

        [Header(Shape)]
        _EdgeRadius     ("Edge Radius",          Range(0, 0.45))   = 0.15
        _EdgeSoftness   ("Edge Softness",        Range(0.001, 0.5))= 0.1

        [Toggle] _IgnoreSceneDepth ("Ignore Scene Depth", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "FogVolume"
            Cull Off          // render both faces — fragment decides what to do
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float  _Visibility;
                float  _Density;
                float  _StepCount;
                float  _MinFogAmount;
                float4 _FogColor;
                float  _EdgeRadius;
                float  _EdgeSoftness;
                float  _IgnoreSceneDepth;
            CBUFFER_END

            float sampleDensity(float3 wpos)
            {
                float3 posOS  = mul(UNITY_MATRIX_I_M, float4(wpos, 1.0)).xyz;
                float  radius = min(_EdgeRadius, 0.49);
                float3 half_  = float3(0.5,0.5,0.5) - radius;
                float3 q      = abs(posOS) - half_;
                float  dist   = length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0) - radius;
                return _Density * smoothstep(0.0, max(0.001,_EdgeSoftness), -dist);
            }

            bool RayBoxIntersect(float3 ro, float3 rd, out float tEnter, out float tExit)
            {
                float3 invDir = 1.0 / rd;
                float3 t0 = (float3(-0.5,-0.5,-0.5) - ro) * invDir;
                float3 t1 = (float3( 0.5, 0.5, 0.5) - ro) * invDir;
                float3 tMin = min(t0,t1); float3 tMax = max(t0,t1);
                tEnter = max(max(tMin.x,tMin.y),tMin.z);
                tExit  = min(min(tMax.x,tMax.y),tMax.z);
                return tExit >= max(tEnter,0.0);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 camPos   = _WorldSpaceCameraPos;
                float3 rayDir   = normalize(IN.positionWS - camPos);
                float3 camPosOS = mul(UNITY_MATRIX_I_M, float4(camPos, 1.0)).xyz;
                float3 rayDirOS = mul((float3x3)UNITY_MATRIX_I_M, rayDir);

                float entryDist, exitDist;
                if (!RayBoxIntersect(camPosOS, rayDirOS, entryDist, exitDist))
                    return half4(0,0,0,0);

                // Camera inside box: entryDist <= 0, so march starts at camera.
                // Camera outside box: entryDist > 0, march starts at box entry.
                bool insideBox = entryDist <= 0.001;
                entryDist = max(entryDist, 0.0);

                // ── Analytical depth occlusion ──────────────────────────
                float2 screenUV  = IN.screenPos.xy / IN.screenPos.w;
                float3 viewFwd   = -UNITY_MATRIX_V._m20_m21_m22;
                float  cosine    = max(0.0001, dot(rayDir, viewFwd));
                float  rawDepth  = SampleSceneDepth(screenUV);
                float  sceneDist = LinearEyeDepth(rawDepth, _ZBufferParams) / cosine;

                float marchDist;
                if (_IgnoreSceneDepth > 0.5)
                {
                    marchDist = exitDist;
                }
                else
                {
                    if (!insideBox && sceneDist <= entryDist)
                    {
                        // Opaque geometry is fully in front of the fog — discard.
                        return half4(0,0,0,0);
                    }
                    // Clip march at scene geometry if it lands inside the box.
                    marchDist = min(exitDist, sceneDist);
                }

                float segmentLength = max(0.0, marchDist - entryDist);
                if (segmentLength <= 0.0) return half4(0,0,0,0);

                // ── Ray march ───────────────────────────────────────────
                float jitter   = frac(sin(dot(screenUV, float2(12.9898,78.233))) * 43758.5453);
                int   steps    = (int)_StepCount;
                float stepSize = segmentLength / max(1, steps);
                float t        = entryDist + stepSize * jitter;

                float opticalDepth = 0.0;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    if (t >= marchDist) break;
                    opticalDepth += sampleDensity(camPos + rayDir * t) * stepSize;
                    t += stepSize;
                }

                // ── Opacity ─────────────────────────────────────────────
                float fullThickness = exitDist - entryDist;
                float fogAmount     = 1.0 - exp(-opticalDepth / max(0.001, _Visibility));
                float minFog        = (1.0 - exp(-fullThickness * _Density / max(0.001, _Visibility)))
                                      * _MinFogAmount;
                fogAmount = max(fogAmount, minFog);

                return half4(_FogColor.rgb, saturate(fogAmount) * _FogColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}