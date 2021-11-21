Shader "River/Water"
{
    Properties
    {
        _NormalTex ("Normal", 2D) = "white" {}
        _FoamNoiseTex("FoamNoiseTex",2D) = "white"{}
        _Fresnel("Fresnel",2D) = "white"{}
        _Foam_Softness ("Foam Softness", Range(0,1)) = 0.18
        _WaveSpeed("WaveSpeed",vector) = (0.5,0.5,-0.5,-0.5)
        _DistortAmount("DistortAmount",float) = 100
        _FoamFade("FoamFade",Range(0,2)) = 1
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "1UPLight.cginc"
    #include "1UPUtility.cginc"
    sampler2D _NormalTex,_FoamNoiseTex,_GrabTex,_Fresnel;
    float4 _NormalTex_ST,_FoamNoiseTex_ST;
    float4 _WaveSpeed;
    float _DistortAmount,_FoamFade,_Foam_Softness;
    float2 _GrabTex_TexelSize;
    sampler2D _CameraDepthTexture;

    ENDCG

    SubShader
    {

        GrabPass
        {
            "_GrabTex"
        }

        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos:TEXCOORD2;
                BASE_DATA_INPUT
            };

            v2f vert (appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = v.texcoord.xy * _NormalTex_ST.xy + _NormalTex_ST.zw;
                o.uv.zw = v.texcoord.xy * _FoamNoiseTex_ST.xy + _FoamNoiseTex_ST.zw;
                o.screenPos = ComputeGrabScreenPos(o.vertex);
                COMPUTE_EYEDEPTH(o.screenPos.z);
                BASE_DATA_VERTEX

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.screenPos.xy /= i.screenPos.w;

                BASE_DATA_FRAG
                float NdotUp = 1 - saturate(dot(worldNormal,float3(0,1,0)));
                float3 n1 = UnpackNormal(tex2D(_NormalTex,i.uv + _WaveSpeed.xy * _Time.x));
                float3 n2 = UnpackNormal(tex2D(_NormalTex,i.uv + _WaveSpeed.zw * _Time.x));
                float3 offset = normalize(n1 + n2);
                
                //反射
                half3 rdir = reflect(-worldViewDir,offset);
                fixed3 rcolor = ComputeIndirectSpecular(rdir,i.wpos,0);

                //伪折射
                fixed4 col = tex2D(_GrabTex,i.screenPos.xy + offset.xy * _GrabTex_TexelSize * _DistortAmount);

                //泡沫
                fixed3 noise_color = tex2D(_FoamNoiseTex,i.uv.zw - float2(0.05,0.1) * _Time.y * abs(_WaveSpeed.y));
                col.xyz += noise_color * (_Foam_Softness + NdotUp * 2);

                half fresnelFac = dot( worldViewDir, offset );
                half fresnel = UNITY_SAMPLE_1CHANNEL( _Fresnel, float2(fresnelFac,fresnelFac) );
                col.xyz = col * (1 - fresnel) + fresnel * rcolor;
                return col;
                
            }
            ENDCG
        }
    }
}
