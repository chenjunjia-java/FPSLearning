Shader "FPS/URP/DebrisDissolve"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Dissolve)]
        _DissolveNoise("Dissolve Noise", 2D) = "gray" {}
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0
        _DissolveSoftness("Dissolve Softness", Range(0.001,0.5)) = 0.06
        [HDR] _DissolveEdgeColor("Dissolve Edge Color", Color) = (1,0.45,0.1,1)
        _DissolveEdgeIntensity("Dissolve Edge Intensity", Range(0,8)) = 1.5

        [Header(Clip Compatibility)]
        _Cutoff("Cutoff", Range(0,1)) = 0
        _AlphaClipThreshold("Alpha Clip Threshold", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            TEXTURE2D(_DissolveNoise);
            SAMPLER(sampler_DissolveNoise);
            float4 _DissolveNoise_ST;

            float4 _BaseColor;
            float _DissolveAmount;
            float _DissolveSoftness;
            float4 _DissolveEdgeColor;
            float _DissolveEdgeIntensity;
            float _Cutoff;
            float _AlphaClipThreshold;

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(v.normalOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = NormalizeNormalPerVertex(nrmInputs.normalWS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uvNoise = TRANSFORM_TEX(i.uv, _DissolveNoise);
                float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise, uvNoise).r;

                float baseThreshold = max(_Cutoff, _AlphaClipThreshold);
                float dissolveThreshold = saturate(baseThreshold + _DissolveAmount * (1.0 - baseThreshold));
                float distToFront = noise - dissolveThreshold;

                clip(distToFront);

                float edgeSoft = max(0.001, _DissolveSoftness);
                float edgeMask = 1.0 - smoothstep(0.0, edgeSoft, distToFront);
                edgeMask *= step(0.0001, _DissolveAmount);

                float3 normalWS = normalize(i.normalWS);
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = SampleSH(normalWS);
                float3 lit = _BaseColor.rgb * (ambient + ndl * mainLight.color);

                float3 edgeEmission = _DissolveEdgeColor.rgb * (_DissolveEdgeIntensity * edgeMask);
                float3 finalColor = lit + edgeEmission;
                finalColor = MixFog(finalColor, i.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Shader Graph/FallbackError"
}
