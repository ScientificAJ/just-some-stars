Shader "JustSomeStars/CaptainLayeredSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PaletteMask ("Palette Mask", 2D) = "black" {}
        _FacePresetsModule ("Face Presets", 2D) = "black" {}
        _EyeShapesModule ("Eye Shapes", 2D) = "black" {}
        _IrisColorsModule ("Iris Colors", 2D) = "black" {}
        _HairShapesModule ("Hair Shapes", 2D) = "black" {}
        _SuitComponentsModule ("Suit Components", 2D) = "black" {}
        _PatchesModule ("Patches", 2D) = "black" {}
        _AccessoriesModule ("Accessories", 2D) = "black" {}
        _GlovesModule ("Gloves", 2D) = "black" {}
        _BootsModule ("Boots", 2D) = "black" {}
        _HelmetsModule ("Helmets", 2D) = "black" {}
        _BackpacksModule ("Backpacks", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _PaletteMask;
            sampler2D _FacePresetsModule;
            sampler2D _EyeShapesModule;
            sampler2D _IrisColorsModule;
            sampler2D _HairShapesModule;
            sampler2D _SuitComponentsModule;
            sampler2D _PatchesModule;
            sampler2D _AccessoriesModule;
            sampler2D _GlovesModule;
            sampler2D _BootsModule;
            sampler2D _HelmetsModule;
            sampler2D _BackpacksModule;
            float4 _FacePresetsUv;
            float4 _EyeShapesUv;
            float4 _IrisColorsUv;
            float4 _HairShapesUv;
            float4 _SuitComponentsUv;
            float4 _PatchesUv;
            float4 _AccessoriesUv;
            float4 _GlovesUv;
            float4 _BootsUv;
            float4 _HelmetsUv;
            float4 _BackpacksUv;
            fixed4 _SkinColor;
            fixed4 _HairColor;
            fixed4 _SuitColor;
            fixed4 _SignalColor;

            v2f vert(appdata value)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(value.vertex);
                output.color = value.color;
                output.uv = value.uv;
                return output;
            }

            fixed3 palette(fixed3 source, fixed weight, fixed3 selected)
            {
                fixed luminance = dot(source, fixed3(0.299, 0.587, 0.114));
                fixed3 recolored = selected * (0.42 + luminance * 0.95);
                return lerp(source, recolored, weight * 0.42);
            }

            fixed4 over(fixed4 baseColor, fixed4 overlay)
            {
                fixed alpha = overlay.a + baseColor.a * (1.0 - overlay.a);
                fixed3 rgb = overlay.rgb * overlay.a +
                    baseColor.rgb * baseColor.a * (1.0 - overlay.a);
                return fixed4(alpha > 0.0001 ? rgb / alpha : 0, alpha);
            }

            fixed4 moduleSample(sampler2D page, float2 uv, float4 transform)
            {
                return tex2D(page, uv * transform.xy + transform.zw);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 result = tex2D(_MainTex, input.uv) * input.color;
                fixed4 mask = tex2D(_PaletteMask, input.uv);
                result.rgb = palette(result.rgb, mask.r, _SkinColor.rgb);
                result.rgb = palette(result.rgb, mask.g, _HairColor.rgb);
                result.rgb = palette(result.rgb, mask.b, _SuitColor.rgb);
                result.rgb = palette(result.rgb, mask.a, _SignalColor.rgb);
                result.rgb += _SignalColor.rgb * mask.a * 0.30;
                result = over(result, moduleSample(
                    _FacePresetsModule, input.uv, _FacePresetsUv));
                result = over(result, moduleSample(
                    _EyeShapesModule, input.uv, _EyeShapesUv));
                result = over(result, moduleSample(
                    _IrisColorsModule, input.uv, _IrisColorsUv));
                result = over(result, moduleSample(
                    _HairShapesModule, input.uv, _HairShapesUv));
                result = over(result, moduleSample(
                    _SuitComponentsModule, input.uv, _SuitComponentsUv));
                result = over(result, moduleSample(
                    _PatchesModule, input.uv, _PatchesUv));
                result = over(result, moduleSample(
                    _AccessoriesModule, input.uv, _AccessoriesUv));
                result = over(result, moduleSample(
                    _GlovesModule, input.uv, _GlovesUv));
                result = over(result, moduleSample(
                    _BootsModule, input.uv, _BootsUv));
                result = over(result, moduleSample(
                    _HelmetsModule, input.uv, _HelmetsUv));
                result = over(result, moduleSample(
                    _BackpacksModule, input.uv, _BackpacksUv));
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
