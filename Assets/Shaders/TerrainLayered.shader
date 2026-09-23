// Lit URP shader for the streamed terrain's layered strata. Each mesh vertex carries a color
// (ChunkMeshGenerator.TerrainBandColor) that encodes the dig depth below the pristine surface:
// grass -> dirt -> stone. The ForwardLit pass multiplies that vertex color into the albedo and
// shades it with an explicit Lambert + sky-ambient model (no shadow maps / no UniversalFragmentPBR
// — deliberately small, verifiable against URP 17.5). ShadowCaster/DepthOnly passes keep the
// terrain in the shadow/depth pipelines. If the shader is ever missing, GameBootstrap falls back
// to URP Lit.
Shader "NewWorld/TerrainLayered"
{
    Properties
    {
        [MainColor] _Color ("Vertex Color Multiplier", Color) = (1,1,1,1)
        [Toggle] _UseVertexColor ("Use Vertex Colors", Float) = 1
        // Cull mode as a material property (1ei): the far shell's decimated cells render with a
        // Cull-Off variant so they show from above regardless of mesh winding/culling artifacts
        // (real chunks keep the default Cull [_Cull]). 2 = Back, 0 = Off, 1 = Front.
        _Cull ("Cull (0=Off,1=Front,2=Back)", Float) = 2
        // Horizon tonal lift (1ef): subtle distance tint over the outermost band only — aerial
        // perspective for the ~2 km far shell WITHOUT fog (mid-view stays crisp). Push
        // _HorizonStart past the far plane to disable.
        _HorizonColor ("Horizon Tint", Color) = (0.78, 0.83, 0.90, 1)
        _HorizonStart ("Horizon Tint Start (m)", Float) = 1600
        _HorizonEnd ("Horizon Tint End (m)", Float) = 2050
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            // Fog only. Direct sun + sky ambient are computed explicitly in frag with NO shadow
            // maps and NO UniversalFragmentPBR/InputData plumbing — a deliberately small, verified
            // lighting surface so the terrain can never silently render black. (The terrain still
            // casts shadows via the ShadowCaster pass below; it just doesn't sample them.)
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _Color;
            float _UseVertexColor;
            half4 _HorizonColor;
            float _HorizonStart;
            float _HorizonEnd;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 vertexColor : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 vertexColor : COLOR;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.vertexColor = lerp(half4(1, 1, 1, 1), input.vertexColor, _UseVertexColor);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                half3 albedo = _Color.rgb * input.vertexColor.rgb;

                // Explicit Lambert + sky ambient, verified against URP 17.5 sources:
                //   GetMainLight()        RealtimeLights.hlsl:89  (no shadow sampling, atten = 1)
                //   LightingLambert(...)  Lighting.hlsl:32
                //   SampleSHVertex(...)   GlobalIllumination.hlsl:45
                // All reachable via the Lighting.hlsl include chain. Direct light is always
                // positive while the scene has a main directional sun, so the terrain never goes
                // fully black.
                Light sun = GetMainLight();
                half3 direct = albedo * LightingLambert(sun.color, sun.direction, normalWS);
                half3 ambient = albedo * SampleSHVertex(normalWS);

                half3 color = direct + ambient;

                // Horizon tonal lift (1ef): lerp toward a soft horizon tint only across the outer
                // distance band — atmospheric perspective for the far shell with NO fog, so the
                // near/mid terrain the player actually plays on stays fully crisp.
                float horizonDist = distance(_WorldSpaceCameraPos, input.positionWS);
                half horizonBlend = saturate((horizonDist - _HorizonStart) / max(_HorizonEnd - _HorizonStart, 1.0));
                color = lerp(color, _HorizonColor.rgb, horizonBlend * 0.6);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Casts the terrain's shadow.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Directional main-light direction (and punctual-light position) for shadow bias. URP
            // declares these inside its own ShadowCasterPass.hlsl, which we do not include, so we
            // declare them here for the caster to compile.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // Keeps the terrain in the depth prepass (opaque depth texture / SSAO usability).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
