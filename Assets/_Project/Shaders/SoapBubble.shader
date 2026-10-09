Shader "Wreckabulary/Soap Bubble"
{
    Properties
    {
        _Tint ("Pearl tint", Color) = (0.68, 0.87, 1, 1)
        _Rim ("Lavender rim", Color) = (0.85, 0.65, 1, 1)
        _Opacity ("Film opacity", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Bubble Film"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Rim;
                half _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS;
                output.positionWS = vertex.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(vertex.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(dot(normal, view));
                half rim = pow(1 - facing, 2.8);
                half film = .5 + .5 * sin(normal.y * 6 + facing * 9 + _Time.y * .35);
                half3 color = lerp(_Tint.rgb, _Rim.rgb, film);
                half3 reflection = reflect(-view, normal);
                half highlight = pow(saturate(dot(reflection, normalize(half3(-.45, .75, -.4)))), 34);
                half shoulder = pow(saturate(dot(reflection, normalize(half3(.6, .35, .4)))), 70);
                color = lerp(color, half3(1, .98, .9), saturate(highlight * 1.8 + shoulder));
                color += rim * .18;
                half alpha = saturate((.035 + rim * .65 + highlight * .9 + shoulder * .5) * _Opacity);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
