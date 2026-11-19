// Sprite sin luz que lleva los colores claros hacia el hielo y deja el contorno oscuro igual.
// Lo usan los enemigos ralentizados. El color del SpriteRenderer se multiplica después (golpe, desvanecido).
Shader "Cardstorm/Sprite Congelado"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Ice ("Hielo", Color) = (0.604, 0.843, 1, 1)
        _Amount ("Mezcla", Range(0, 1)) = 0.65
        _Dark ("Luminancia del contorno", Range(0, 1)) = 0.2
        _Soft ("Transición", Range(0.01, 1)) = 0.15
        _Base ("Hielo en lo oscuro", Range(0, 2)) = 0.45
        _Gain ("Hielo en lo claro", Range(0, 2)) = 0.65

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #pragma vertex FrozenVertex
            #pragma fragment FrozenFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Ice;
                half _Amount;
                half _Dark;
                half _Soft;
                half _Base;
                half _Gain;
            CBUFFER_END

            Varyings FrozenVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 FrozenFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Se calcula en sRGB, igual que la paleta del sprite.
                half3 c = tex.rgb;
                half3 ice = _Ice.rgb;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                c = LinearToSRGB(c);
                ice = LinearToSRGB(ice);
            #endif
                half l = dot(c, half3(0.3, 0.59, 0.11));
                half k = _Amount * saturate((l - _Dark) / _Soft);
                c = lerp(c, ice * (_Base + _Gain * l), k);
            #if !defined(UNITY_COLORSPACE_GAMMA)
                c = SRGBToLinear(c);
            #endif

                return half4(c, tex.a) * input.color;
            }
            ENDHLSL
        }
    }
}
