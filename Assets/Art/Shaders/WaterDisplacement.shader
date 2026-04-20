Shader "Custom/WaterDisplacement"
{
    Properties
    {
        _ShallowColor   ("Shallow Color",   Color) = (0.13, 0.60, 0.78, 0.85)
        _DeepColor      ("Deep Color",      Color) = (0.04, 0.20, 0.45, 1.0)
        _FoamColor      ("Foam Color",      Color) = (0.85, 0.95, 1.0,  1.0)
        _FoamThreshold  ("Foam Threshold",  Range(0, 1))   = 0.08
        _FoamSoftness   ("Foam Softness",   Range(0.001, 0.1)) = 0.02
        _WaveSpeed      ("Wave Speed",      Float) = 0.8
        _WaveHeight     ("Wave Height",     Float) = 0.12
        _WaveScale      ("Wave Scale (world units)", Float) = 8.0
        _DisplacementStrength ("Displacement Strength", Float) = 1.5
        _DisplacementRadius   ("Displacement Radius",   Float) = 1.0
        _Smoothness     ("Smoothness",      Range(0, 1)) = 0.85
        _Metallic       ("Metallic",        Range(0, 1)) = 0.0

        // Up to 8 displacing objects (position + radius packed into float4)
        _Displacer0 ("Displacer 0 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer1 ("Displacer 1 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer2 ("Displacer 2 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer3 ("Displacer 3 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer4 ("Displacer 4 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer5 ("Displacer 5 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer6 ("Displacer 6 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _Displacer7 ("Displacer 7 (xyz=pos, w=radius)", Vector) = (0,0,0,0)
        _DisplacerCount ("Active Displacer Count", Int) = 0

        [HideInInspector] _CameraDepthTexture ("", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"  = "Transparent"
            "Queue"       = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "WaterForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   WaterVert
            #pragma fragment WaterFrag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Properties ──────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor;
                half4  _DeepColor;
                half4  _FoamColor;
                float  _FoamThreshold;
                float  _FoamSoftness;
                float  _WaveSpeed;
                float  _WaveHeight;
                float  _WaveScale;
                float  _DisplacementStrength;
                float  _DisplacementRadius;
                float  _Smoothness;
                float  _Metallic;

                float4 _Displacer0, _Displacer1, _Displacer2, _Displacer3;
                float4 _Displacer4, _Displacer5, _Displacer6, _Displacer7;
                int    _DisplacerCount;
            CBUFFER_END

            TEXTURE2D(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);

            // ── Gerstner wave ────────────────────────────────────────────────
            float3 GerstnerWave(float2 dir, float2 xz, float amplitude,
                                float wavelength, float speed, float steepness)
            {
                float k  = 2.0 * PI / wavelength;
                float c  = sqrt(9.8 / k);
                float2 d = normalize(dir);
                float  f = k * (dot(d, xz) - c * speed * _Time.y);
                float  a = steepness / k;
                return float3(d.x * a * cos(f),
                              amplitude * sin(f),
                              d.y * a * cos(f));
            }

            // ── Displacement ─────────────────────────────────────────────────
            float DisplacerOffset(float4 displacer, float3 worldPos)
            {
                if (displacer.w <= 0.0) return 0.0;

                float2 delta = worldPos.xz - displacer.xz;
                float  dist  = length(delta);
                float  r     = displacer.w * _DisplacementRadius;
                if (dist >= r) return 0.0;

                // Submersion factor: 0 = fully above water, 1 = fully submerged
                float waterY   = worldPos.y;
                float objBot   = displacer.y - displacer.w;
                float submerge = saturate((waterY - objBot) / (displacer.w * 2.0));
                if (submerge <= 0.0) return 0.0;

                // Radial falloff
                float falloff = 1.0 - smoothstep(0.0, r, dist);
                falloff = falloff * falloff;

                return -falloff * _DisplacementStrength * displacer.w * submerge;
            }

            float TotalDisplacement(float3 worldPos)
            {
                float d = 0.0;
                if (_DisplacerCount > 0) d += DisplacerOffset(_Displacer0, worldPos);
                if (_DisplacerCount > 1) d += DisplacerOffset(_Displacer1, worldPos);
                if (_DisplacerCount > 2) d += DisplacerOffset(_Displacer2, worldPos);
                if (_DisplacerCount > 3) d += DisplacerOffset(_Displacer3, worldPos);
                if (_DisplacerCount > 4) d += DisplacerOffset(_Displacer4, worldPos);
                if (_DisplacerCount > 5) d += DisplacerOffset(_Displacer5, worldPos);
                if (_DisplacerCount > 6) d += DisplacerOffset(_Displacer6, worldPos);
                if (_DisplacerCount > 7) d += DisplacerOffset(_Displacer7, worldPos);
                return d;
            }

            // ── Normal ───────────────────────────────────────────────────────
            #define WAVE_Y(pos) ( \
                GerstnerWave(float2( 1.0,  0.4),  (pos).xz, _WaveHeight,        _WaveScale,        _WaveSpeed,       0.35).y + \
                GerstnerWave(float2(-0.6,  0.8),  (pos).xz, _WaveHeight * 0.55, _WaveScale * 0.61, _WaveSpeed * 1.2, 0.28).y + \
                GerstnerWave(float2( 0.3, -0.95), (pos).xz, _WaveHeight * 0.30, _WaveScale * 0.37, _WaveSpeed * 1.5, 0.20).y + \
                GerstnerWave(float2(-0.9, -0.45), (pos).xz, _WaveHeight * 0.15, _WaveScale * 0.22, _WaveSpeed * 1.9, 0.12).y + \
                TotalDisplacement(pos) )

            float3 ComputeNormal(float3 worldPos, float eps)
            {
                float hL = WAVE_Y(worldPos - float3(eps, 0, 0));
                float hR = WAVE_Y(worldPos + float3(eps, 0, 0));
                float hD = WAVE_Y(worldPos - float3(0, 0, eps));
                float hU = WAVE_Y(worldPos + float3(0, 0, eps));
                return normalize(float3(hL - hR, 2.0 * eps, hD - hU));
            }
            #undef WAVE_Y

            // ── Vertex ───────────────────────────────────────────────────────
            struct Attributes { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 posCS     : SV_POSITION;
                float3 worldPos  : TEXCOORD0;
                float2 uv        : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float  fogFactor : TEXCOORD3;
            };

            Varyings WaterVert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.posOS.xyz);

                float3 w1 = GerstnerWave(float2( 1.0,  0.4), worldPos.xz,
                                _WaveHeight,        _WaveScale,        _WaveSpeed,       0.35);
                float3 w2 = GerstnerWave(float2(-0.6,  0.8), worldPos.xz,
                                _WaveHeight * 0.55, _WaveScale * 0.61, _WaveSpeed * 1.2, 0.28);
                float3 w3 = GerstnerWave(float2( 0.3, -0.95), worldPos.xz,
                                _WaveHeight * 0.30, _WaveScale * 0.37, _WaveSpeed * 1.5, 0.20);
                float3 w4 = GerstnerWave(float2(-0.9, -0.45), worldPos.xz,
                                _WaveHeight * 0.15, _WaveScale * 0.22, _WaveSpeed * 1.9, 0.12);

                worldPos.y  += w1.y + w2.y + w3.y + w4.y;
                worldPos.xz += w1.xz + w2.xz + w3.xz + w4.xz;
                worldPos.y  += TotalDisplacement(worldPos);

                OUT.worldPos  = worldPos;
                OUT.posCS     = TransformWorldToHClip(worldPos);
                OUT.uv        = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.posCS);
                OUT.fogFactor = ComputeFogFactor(OUT.posCS.z);
                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────────────
            half4 WaterFrag(Varyings IN) : SV_Target
            {
                float3 N = ComputeNormal(IN.worldPos, 0.1);
                float3 V = normalize(GetCameraPositionWS() - IN.worldPos);

                float2 screenUV  = IN.screenPos.xy / IN.screenPos.w;
                float  rawDepth  = SAMPLE_TEXTURE2D(_CameraDepthTexture,
                                    sampler_CameraDepthTexture, screenUV).r;
                float  sceneZ    = LinearEyeDepth(rawDepth, _ZBufferParams);
                float  depthDiff = saturate((sceneZ - IN.screenPos.w) * 0.3);

                half4 waterCol = lerp(_ShallowColor, _DeepColor, depthDiff);

                float foamMask = 1.0 - smoothstep(_FoamThreshold,
                                                   _FoamThreshold + _FoamSoftness, depthDiff);

                float dX = TotalDisplacement(IN.worldPos + float3(0.12,0,0))
                         - TotalDisplacement(IN.worldPos - float3(0.12,0,0));
                float dZ = TotalDisplacement(IN.worldPos + float3(0,0,0.12))
                         - TotalDisplacement(IN.worldPos - float3(0,0,0.12));
                foamMask = saturate(foamMask + saturate(length(float2(dX, dZ)) * 1.5));

                float3 L     = normalize(_MainLightPosition.xyz);
                float3 H     = normalize(V + L);
                float  spec  = pow(max(0, dot(N, H)), exp2(_Smoothness * 8.0 + 1.0)) * _Smoothness;
                float  fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);

                half4 col = lerp(waterCol, _FoamColor, foamMask);
                col.rgb  += spec * _MainLightColor.rgb;
                col.rgb   = lerp(col.rgb, col.rgb + 0.3, fresnel * 0.4);
                col.a     = lerp(waterCol.a, 1.0, foamMask + fresnel * 0.5);
                col.rgb   = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
