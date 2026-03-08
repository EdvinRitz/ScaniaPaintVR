Shader "Custom/SprayBrush"
{
    Properties { _Color("Color", Color) = (1,0,0,1) _UV("UV", Vector) = (0,0,0,0) _Size("Size", Float) = 0.02 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags{ "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha   // <-- add this line
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings  { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            float4 _Color, _UV; float _Size;

            Varyings vert(Attributes v){ Varyings o; o.positionHCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float2 d = i.uv - _UV.xy;
                float dist = length(d);
                float a = saturate(1.0 - smoothstep(_Size*0.4, _Size, dist));
                return half4(_Color.rgb * a, a); // premultiplied RGB
            }
            ENDHLSL
        }

    }
}
