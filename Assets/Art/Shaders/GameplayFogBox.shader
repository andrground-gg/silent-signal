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
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "VolumetricGameplayFog"

            // Render back-faces so the fog is visible from inside the box.
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

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
            CBUFFER_END

            float sampleDensity(float3 wpos)
            {
                float3 posOS = mul(UNITY_MATRIX_I_M, float4(wpos, 1.0)).xyz;
                float radius = min(_EdgeRadius, 0.49);
                float3 halfSize = float3(0.5, 0.5, 0.5) - radius;
                float3 q = abs(posOS) - halfSize;
                float dist = length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - radius;
                float edgeMask = smoothstep(0.0, max(0.001, _EdgeSoftness), -dist);

                return _Density * edgeMask;
            }

            bool RayBoxIntersect(float3 rayOriginOS, float3 rayDirOS, out float tEnter, out float tExit)
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);

                float3 invDir = 1.0 / rayDirOS;
                float3 t0 = (boxMin - rayOriginOS) * invDir;
                float3 t1 = (boxMax - rayOriginOS) * invDir;

                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);

                tEnter = max(max(tMin.x, tMin.y), tMin.z);
                tExit  = min(min(tMax.x, tMax.y), tMax.z);

                return tExit >= max(tEnter, 0.0);
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
                float3 camPos = _WorldSpaceCameraPos;
                float3 rayDir = normalize(IN.positionWS - camPos);

                float3 camPosOS = mul(UNITY_MATRIX_I_M, float4(camPos, 1.0)).xyz;
                float3 rayDirOS = mul((float3x3)UNITY_MATRIX_I_M, rayDir);

                float entryDist;
                float exitDist;
                if (!RayBoxIntersect(camPosOS, rayDirOS, entryDist, exitDist))
                {
                    return half4(0, 0, 0, 0);
                }

                entryDist = max(entryDist, 0.0);

                float2 screenUV   = IN.screenPos.xy / IN.screenPos.w;
                float  rawDepth   = SampleSceneDepth(screenUV);
                float  sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float3 viewForward = -UNITY_MATRIX_V._m20_m21_m22;
                float  rayCosine   = max(0.0001, dot(rayDir, viewForward));
                float  sceneDist   = sceneDepth / rayCosine;

                if (sceneDist <= entryDist)
                {
                    return half4(0, 0, 0, 0);
                }

                float marchDist = min(exitDist, sceneDist);
                float segmentLength = max(0.0, marchDist - entryDist);
                if (segmentLength <= 0.0)
                {
                    return half4(0, 0, 0, 0);
                }

                float jitter   = frac(sin(dot(screenUV, float2(12.9898, 78.233))) * 43758.5453);
                int   steps    = (int)_StepCount;
                float stepSize = segmentLength / max(1, steps);
                float t        = entryDist + stepSize * jitter;

                float opticalDepth = 0.0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    if (t >= marchDist) break;

                    float3 samplePos = camPos + rayDir * t;
                    opticalDepth += sampleDensity(samplePos) * stepSize;

                    t += stepSize;
                }

                float extinction = exp(-opticalDepth / max(0.001, _Visibility));
                float fogAmount  = 1.0 - extinction;

                // Enforce a minimum opacity based on full fog thickness to avoid silhouettes.
                float minFogAmount = (1.0 - exp(-segmentLength * _Density / max(0.001, _Visibility))) * _MinFogAmount;
                fogAmount = max(fogAmount, minFogAmount);

                return half4(_FogColor.rgb, fogAmount * _FogColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
