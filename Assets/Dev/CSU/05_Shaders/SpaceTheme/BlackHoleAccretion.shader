Shader "CSU/Space Theme/Black Hole Accretion"
{
    Properties
    {
        [PerRendererData] [NoScaleOffset]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _AccretionColor ("Accretion Color", Color) =
            (1.25, 0.72, 0.18, 1)
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.4
        _RotationSpeed ("Rotation Speed", Range(-1, 1)) = 0.08
        _SwirlStrength ("Swirl Strength", Range(0, 2)) = 0.45
        _SwirlRadius ("Swirl Radius", Range(0.1, 1)) = 0.62
        _PulseSpeed ("Pulse Speed", Range(0, 4)) = 0.65
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.08
        _ChromaticShift ("Chromatic Shift", Range(0, 0.01)) = 0.0014
        _NoiseStrength ("Flow Variation", Range(0, 0.05)) = 0.009
        _NoiseScale ("Flow Detail", Range(1, 20)) = 7
        _HorizonRadius ("Event Horizon Radius", Range(0, 0.5)) = 0.23
        _HorizonSoftness ("Event Horizon Softness", Range(0.001, 0.2)) =
            0.035
        _OverallOpacity ("Overall Opacity", Range(0, 1)) = 0.93
        [HideInInspector] _SpriteRect ("Sprite UV Rect", Vector) =
            (0.0567268, 0, 0.8809986, 1)
        [HideInInspector] _SpriteAspect ("Sprite Aspect", Float) =
            1.8465116
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _AccretionColor;
                float4 _SpriteRect;
                float _EmissionStrength;
                float _RotationSpeed;
                float _SwirlStrength;
                float _SwirlRadius;
                float _PulseSpeed;
                float _PulseAmount;
                float _ChromaticShift;
                float _NoiseStrength;
                float _NoiseScale;
                float _HorizonRadius;
                float _HorizonSoftness;
                float _OverallOpacity;
                float _SpriteAspect;
            CBUFFER_END

            float2 Rotate(float2 value, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float2(
                    value.x * cosine - value.y * sine,
                    value.x * sine + value.y * cosine);
            }

            float2 ToAtlasUv(float2 localUv)
            {
                return _SpriteRect.xy
                    + saturate(localUv) * _SpriteRect.zw;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 safeRectSize =
                    max(_SpriteRect.zw, float2(0.0001, 0.0001));
                float2 localUv =
                    (input.uv - _SpriteRect.xy) / safeRectSize;

                float safeAspect = max(_SpriteAspect, 0.001);
                float2 centered = localUv - 0.5;
                centered.x *= safeAspect;

                float radius = length(centered);
                float safeRadius = max(_SwirlRadius, 0.001);
                float flowMask = 1.0 - smoothstep(
                    safeRadius * 0.35,
                    safeRadius,
                    radius);
                float radialFalloff =
                    1.0 - saturate(radius / safeRadius);
                float angle = atan2(centered.y, centered.x);
                float rotation =
                    (_Time.y * _RotationSpeed
                        + _SwirlStrength * radialFalloff)
                    * flowMask;

                float flowWave =
                    sin(
                        angle * _NoiseScale
                        - _Time.y * 0.9
                        + radius * 28.0)
                    + 0.5 * sin(
                        angle * (_NoiseScale * 1.7)
                        + _Time.y * 0.55
                        - radius * 41.0);

                float2 warped = Rotate(centered, rotation);
                float2 tangent = normalize(
                    float2(-warped.y, warped.x)
                    + float2(0.0001, 0.0001));
                warped += tangent
                    * flowWave
                    * (_NoiseStrength / 1.5)
                    * flowMask;

                float2 warpedLocalUv =
                    float2(warped.x / safeAspect, warped.y)
                    + 0.5;
                float2 sampleUv = ToAtlasUv(warpedLocalUv);
                float2 chromaticDirection =
                    float2(
                        tangent.x / safeAspect,
                        tangent.y)
                    * _ChromaticShift
                    * flowMask
                    * _SpriteRect.zw;

                half4 centerSample = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    sampleUv);
                half redSample = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    sampleUv + chromaticDirection).r;
                half blueSample = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    sampleUv - chromaticDirection).b;

                half3 textureRgb = half3(
                    redSample,
                    centerSample.g,
                    blueSample);
                half luminance = dot(
                    textureRgb,
                    half3(0.2126, 0.7152, 0.0722));

                float horizonSoftness =
                    max(_HorizonSoftness, 0.001);
                float horizonMask = smoothstep(
                    _HorizonRadius,
                    _HorizonRadius + horizonSoftness,
                    radius);
                float outerRingMask = 1.0 - smoothstep(
                    safeRadius * 0.72,
                    safeRadius,
                    radius);
                float accretionMask =
                    horizonMask
                    * outerRingMask
                    * flowMask;

                float pulse =
                    1.0
                    + sin(_Time.y * _PulseSpeed)
                    * _PulseAmount;
                half3 warmTint = lerp(
                    half3(1.0, 1.0, 1.0),
                    _AccretionColor.rgb,
                    0.16 * accretionMask);
                half3 emission =
                    _AccretionColor.rgb
                    * luminance
                    * accretionMask
                    * _EmissionStrength
                    * pulse;

                half3 finalRgb =
                    textureRgb
                    * warmTint
                    * input.color.rgb
                    * horizonMask;
                finalRgb += emission * input.color.rgb;
                finalRgb = min(
                    finalRgb,
                    half3(4.0, 4.0, 4.0));

                half finalAlpha =
                    centerSample.a
                    * input.color.a
                    * saturate(_OverallOpacity);
                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
