Shader "Custom/FogBox"
{
    Properties
    {
        [Header(Visibility)]
        _Visibility     ("Visibility (meters)",  Range(5, 1000))   = 100
        _Density        ("Density Multiplier",   Range(0, 5))      = 1
        _StepCount      ("Ray March Steps",      Range(8, 128))    = 32

        [Header(Color)]
        _FogColor       ("Fog Color",            Color)            = (0.7, 0.75, 0.8, 1)

        [Header(Height Fog)]
        _HeightStart    ("Height Start (Y)",     Float)            = 0
        _HeightEnd      ("Height End (Y)",       Float)            = 50
        _HeightFalloff  ("Height Falloff",       Range(0.1, 10))   = 2

        [Header(Noise)]
        _NoiseScale     ("Noise Scale",          Range(0.001, 1))  = 0.05
        _NoiseStrength  ("Noise Strength",       Range(0, 1))      = 0.5
        _NoiseSpeed     ("Wind Speed (XYZ)",     Vector)           = (0.5, 0.1, 0.3, 0)
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
            Name "VolumetricFog"

            // Renderимо back-faces — щоб коли камера всередині куба, ми все одно бачили туман
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
                float4 _FogColor;
                float  _HeightStart;
                float  _HeightEnd;
                float  _HeightFalloff;
                float  _NoiseScale;
                float  _NoiseStrength;
                float4 _NoiseSpeed;
            CBUFFER_END

            // ---------- 3D value noise + FBM ----------
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3D(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            // 3 октави — добрий компроміс ціна/вигляд
            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * noise3D(p);
                    p *= 2.03; // не ціле число — менше grid artifacts
                    a *= 0.5;
                }
                return v;
            }

            // Густина в одній точці world-space
            float sampleDensity(float3 wpos)
            {
                // Height falloff: щільніше біля _HeightStart, нічого вище _HeightEnd
                float h = saturate((wpos.y - _HeightStart) / max(0.001, _HeightEnd - _HeightStart));
                float heightFactor = pow(1.0 - h, _HeightFalloff);

                // Animated FBM
                float3 noisePos = wpos * _NoiseScale + _NoiseSpeed.xyz * _Time.y;
                float n = fbm(noisePos);
                float noiseMod = lerp(1.0 - _NoiseStrength, 1.0 + _NoiseStrength, n);

                return _Density * heightFactor * noiseMod;
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

                // Дистанція до back-face куба (ця точка — exit ray-а)
                float exitDist = distance(camPos, IN.positionWS);

                // Дистанція до scene geometry — щоб не малювати туман поверх об'єктів
                float2 screenUV   = IN.screenPos.xy / IN.screenPos.w;
                float  rawDepth   = SampleSceneDepth(screenUV);
                float  sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                // sceneDepth — це Z-distance вздовж view-axis. Перетворюємо у відстань вздовж rayDir:
                float3 viewForward = -UNITY_MATRIX_V._m20_m21_m22;
                float  rayCosine   = max(0.0001, dot(rayDir, viewForward));
                float  sceneDist   = sceneDepth / rayCosine;

                float marchDist = min(exitDist, sceneDist);

                // Blue-noise-style jitter, щоб уникнути banding при низькому stepCount
                float jitter   = frac(sin(dot(screenUV, float2(12.9898, 78.233))) * 43758.5453);
                int   steps    = (int)_StepCount;
                float stepSize = marchDist / max(1, steps);
                float t        = stepSize * jitter;

                // Beer-Lambert accumulation
                float opticalDepth = 0.0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    if (t >= marchDist) break;

                    float3 samplePos = camPos + rayDir * t;
                    opticalDepth += sampleDensity(samplePos) * stepSize;

                    t += stepSize;
                }

                // Visibility у метрах: на цій дистанції туман вкриває ~63% (1 - 1/e)
                float extinction = exp(-opticalDepth / max(0.001, _Visibility));
                float fogAmount  = 1.0 - extinction;

                // return half4(opticalDepth.xxx, 1);
                // return half4(abs(IN.positionWS - camPos) / 10, 1);
                // return half4(saturate(exitDist / 100), saturate(sceneDist / 100), 0, 1);
                // return half4((float)steps / 128, jitter, fogAmount, 1);
                // return half4(saturate(marchDist / 100).xxx, 1); // має бути градієнт сірого
                return half4(_FogColor.rgb, fogAmount * _FogColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}