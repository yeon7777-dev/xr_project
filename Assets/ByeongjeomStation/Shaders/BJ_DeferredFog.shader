// Built-in 렌더 파이프라인 + 디퍼드 렌더링에서는 RenderSettings 안개가 불투명 물체에 적용되지 않는다.
// 깊이 텍스처로 안개(Exponential Squared)를 직접 입히는 이미지 이펙트. (URP 에서는 사용하지 않음)
// VR(Single Pass Instanced / Multi Pass) 에서도 동작하도록 스테레오 매크로 사용.
Shader "Hidden/BJ_DeferredFog"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _BJFogColor;
            float _BJFogDensity;

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv);
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float d = LinearEyeDepth(rawDepth);
                float x = _BJFogDensity * d;
                float f = saturate(exp2(-x * x * 1.442695));
                col.rgb = lerp(_BJFogColor.rgb, col.rgb, f);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
