Shader "Wreckabulary/Skyline"
{
    Properties
    {
        [MainColor] _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Lit window glow", Color) = (0.28, 0.22, 0.13, 1)
        _Smoothness ("Glass smoothness", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkylineForwardVertex
            #pragma fragment SkylineForwardFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "BackdropCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define SKYLINE_WALL_SMOOTHNESS 0.06
            #define SKYLINE_JOINT_SHADE 0.8
            #define SKYLINE_JOINT_HEIGHT 2.5
            #define SKYLINE_BLINDS 0.07
            #define SKYLINE_DARK 0.88

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 facade : TEXCOORD0;
                float4 grid : TEXCOORD1;
                float4 glass : TEXCOORD2;
                float2 extra : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 tintFog : TEXCOORD2;
                float4 facade : TEXCOORD3;
                float4 grid : TEXCOORD4;
                float4 glass : TEXCOORD5;
                float2 extra : TEXCOORD6;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD7;
            #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SkylineForwardVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = BackdropWorldPosition(input.positionOS.xyz, input.color.a);
                VertexPositionInputs vertex = BackdropVertexInputs(positionWS);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = vertex.positionCS;
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.tintFog = float4(BackdropTint(input.color), ComputeFogFactor(vertex.positionCS.z));
                output.facade = input.facade;
                output.grid = input.grid;
                output.glass = input.glass;
                output.extra = input.extra;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertex);
            #endif
                OUTPUT_SH(normalWS, output.vertexSH);
                return output;
            }

            float SkylineHash(float2 id, float seed)
            {
                return frac(sin(dot(float3(id, seed * 97.13), float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            void SkylineSurface(float4 facade, float4 grid, float4 glassLit, float2 extra, half3 wall, out half3 albedo, out half3 emission, out half glassMask)
            {
                float2 cell = facade.xy / max(grid.xy, float2(0.01, 0.01));
                float2 fw = max(fwidth(cell), float2(0.0001, 0.0001));
                float joint = lerp(SKYLINE_JOINT_SHADE, 1.0, saturate(facade.y / SKYLINE_JOINT_HEIGHT));
                albedo = wall;
                emission = half3(0, 0, 0);
                glassMask = 0;
                if (grid.x > 0.01)
                {
                    float2 id = floor(cell);
                    float2 local = abs(frac(cell) - 0.5);
                    float2 halfSize = grid.zw * 0.5;
                    float2 edge = 1.0 - smoothstep(halfSize - fw, halfSize + fw, local);
                    float inside = step(0.0, id.x) * step(id.x + 0.5, facade.z) * step(0.0, id.y) * step(id.y + 0.5, facade.w);
                    float blur = saturate(max(fw.x, fw.y) * 2.5 - 0.75);
                    float mask = lerp(edge.x * edge.y, grid.z * grid.w, blur) * inside;
                    float r = SkylineHash(id, extra.x);
                    float haze = extra.y;
                    half3 clear = SRGBToLinear(glassLit.rgb);
                    float rise = saturate((id.y + 0.5) / max(facade.w, 1.0));
                    half3 glass = clear * lerp(0.78, 1.12, rise);
                    float lit = step(r, glassLit.a) * (1.0 - blur);
                    float blinds = step(glassLit.a, r) * step(r, glassLit.a + SKYLINE_BLINDS) * step(0.5, frac(cell.y)) * (1.0 - blur);
                    float dark = step(SKYLINE_DARK, r) * (1.0 - blur);
                    glass = lerp(glass, glass * 0.6, dark);
                    glass = lerp(glass, half3(0.45, 0.43, 0.38), blinds * 0.85);
                    glass = lerp(glass, half3(0.84, 0.66, 0.42), lit);
                    glass = lerp(glass, clear, haze);
                    albedo = lerp(wall, glass, mask);
                    emission = _EmissionColor.rgb * (lit * mask * (1.0 - haze));
                    glassMask = mask;
                }
                albedo *= joint;
            }

            void SkylineForwardFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                BackdropClip(input.positionWS);
                half3 albedo;
                half3 emission;
                half glassMask;
                SkylineSurface(input.facade, input.grid, input.glass, input.extra, (half3)input.tintFog.rgb, albedo, emission, glassMask);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.tintFog.a);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = fogCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SAMPLE_GI(0, input.vertexSH, normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb * albedo;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = lerp(SKYLINE_WALL_SMOOTHNESS, _Smoothness, glassMask);
                surface.normalTS = half3(0, 0, 1);
                surface.emission = emission;
                surface.occlusion = 1;
                surface.alpha = 1;
                half3 lit = UniversalFragmentPBR(inputData, surface).rgb;
                Light mainLight = GetMainLight();
                half3 capNormal = -normalWS;
                half3 cap = BackdropCap(capNormal, mainLight.direction, mainLight.color, SampleSH(capNormal));
                half3 color = IS_FRONT_VFACE(face, lit, cap);
                outColor = half4(MixFog(color, fogCoord), 1);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SkylineShadowVertex
            #pragma fragment SkylineShadowFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "BackdropCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings SkylineShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = BackdropWorldPosition(input.positionOS.xyz, input.color.a);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return output;
            }

            half4 SkylineShadowFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SkylineDepthVertex
            #pragma fragment SkylineDepthFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "BackdropCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SkylineDepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = BackdropWorldPosition(input.positionOS.xyz, input.color.a);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half SkylineDepthFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                BackdropClip(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SkylineDepthNormalsVertex
            #pragma fragment SkylineDepthNormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "BackdropCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SkylineDepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = BackdropWorldPosition(input.positionOS.xyz, input.color.a);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void SkylineDepthNormalsFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                BackdropClip(input.positionWS);
                float3 normalWS = normalize(float3(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0));
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(remappedOctNormalWS), 0.0);
            #else
                outNormalWS = half4(normalWS, 0.0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
