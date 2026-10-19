Shader "Hidden/Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 0.6, 0, 1)
        _Thickness ("Thickness", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        // Pass 0: 마스크 (단색 흰색)
        Pass
        {
            Name "Mask"
            ZWrite On ZTest LEqual Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct V2F { float4 positionCS : SV_POSITION; };

            V2F vert(Attributes i)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }
            half4 frag(V2F i) : SV_Target { return 1; }
            ENDHLSL
        }

        // Pass 1: 컴포짓
        Pass
        {
            Name "Composite"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _OutlineColor;
            float _Thickness;

            half4 frag(Varyings i) : SV_Target
            {
                float2 texel = (_ScreenParams.zw - 1.0) * _Thickness;
                float center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.texcoord).r;
                float m = center;

                [unroll]
                for (int k = 0; k < 16; k++)
                {
                    float a = k * (PI / 8.0);
                    float2 o = float2(cos(a), sin(a)) * texel;
                    m = max(m, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, i.texcoord + o).r);
                }

                float edge = saturate(m - center);
                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}