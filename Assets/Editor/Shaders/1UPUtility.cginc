
#ifndef ONEUPUTILITY_CG_INCLUDE
    #define ONEUPUTILITY_CG_INCLUDE

    #include "UnityCG.cginc"
    #include "AutoLight.cginc"
    #include "Lighting.cginc"


    #define BASE_DATA_INPUT \
    fixed3 normal:NORMAL; \
    fixed4 tangent:TANGENT;\
    float4 wpos:TEXCOORD1;

    #define BASE_DATA_VERTEX \
    o.wpos = mul(unity_ObjectToWorld,v.vertex); \
    o.normal = v.normal; \
    o.tangent = v.tangent;

    #define BASE_DATA_FRAG \
    half3 worldNormal = normalize(UnityObjectToWorldNormal(i.normal)); \
    half3 worldTangent = normalize(mul(unity_ObjectToWorld,i.tangent)); \
    half3 worldLightDir = normalize(UnityWorldSpaceLightDir(i.wpos)); \
    half3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.wpos)); \

    #define MATRIX_T2W \
    half3 worldBiNormal = normalize(cross(worldNormal,worldTangent)) * i.wpos.w; \
    float3x3 matrix_t2w = float3x3( \
    worldTangent.x,worldBiNormal.x,worldNormal.x, \
    worldTangent.y,worldBiNormal.y,worldNormal.y, \
    worldTangent.z,worldBiNormal.z,worldNormal.z \
    );

    //vertex:局部坐标顶点 _JitterSpeedRadio:抖动速度因子 _JitterRangeY：允许抖动的区域的y值范围 _JitterOffset：抖动时的顶点偏移
    float3 VertexJitterOffset(float3 vertex, half _JitterSpeedRadio,half _JitterRangeY,half _JitterOffset)
    {
        //float4 _Time; // (t/20, t, t*2, t*3) 
        half _OptTime = sin(_Time.w * _JitterSpeedRadio);
        half timeToJitter = step(0.99, _OptTime);
        //float4 _SinTime; // sin(t/8), sin(t/4), sin(t/2), sin(t)
        //每次需要抖动的顶点，y是不一样的，这样就使抖动更随机
        half jitterPosY = vertex.y + _SinTime.y;
        //抖动区域   0<y<_JitterRangeY
        half jitterPosYRange = step(0, jitterPosY) * step(jitterPosY, _JitterRangeY);
        half offset = jitterPosYRange * _JitterOffset * timeToJitter * _SinTime.y;
        return half3(offset,0,offset);
    }

    //计算matcap采样坐标，输入归一化的模型空间下的法线和顶点
    inline half2 CaculateMatCapUV(half3 normal,float3 vertex)
    {
        // https://gameinstitute.qq.com/community/detail/128771
        //乘以逆转置矩阵将normal变换到视空间
        float3 viewnormal = mul(UNITY_MATRIX_IT_MV, normal);
        viewnormal = normalize(viewnormal);
        float3 viewPos = UnityObjectToViewPos(vertex);
        float3 r = reflect(-viewPos, viewnormal);
        float m = 2.0 * sqrt(r.x * r.x + r.y * r.y + (r.z + 1) * (r.z + 1));
        return r.xy / m + 0.5;
    }

    //计算matcap采样坐标，输入归一化的世界空间下的法线和顶点
    inline half2 CaculateMatCapUV1(half3 worldNormal,float4 wpos)
    {
        float3 viewDir = -normalize(UnityWorldSpaceViewDir(wpos));
        float3 wr = reflect(viewDir, worldNormal);
        float3 vr = mul(UNITY_MATRIX_V,wr);
        float m = 2.82842712474619 * sqrt(vr.z + 1.0);
        return vr.xy / m + 0.5;
    }

    // ref https://www.gamedev.net/topic/678043-how-to-blend-world-space-normals/#entry5287707
    // assume compositing in world space
    // Note: Using vtxNormal = real3(0, 0, 1) give the BlendNormalRNM formulation.
    // TODO: Untested
    // half3 BlendNormalWorldspaceRNM(half3 n1, half3 n2, half3 vtxNormal)
    // {
        //     // Build the shortest-arc quaternion
        //     half3 q = half3(cross(vtxNormal, n2), dot(vtxNormal, n2) + 1.0) / sqrt(2.0 * (dot(vtxNormal, n2) + 1));

        //     // Rotate the normal
        //     return n1 * (q.w * q.w - dot(q.xyz, q.xyz)) + 2 * q.xyz * dot(q.xyz, n1) + 2 * q.w * cross(q.xyz, n1);
    // }

    // ref http://blog.selfshadow.com/publications/blending-in-detail/
    // ref https://gist.github.com/selfshadow/8048308
    // Reoriented Normal Mapping
    // Blending when n1 and n2 are already 'unpacked' and normalised
    // assume compositing in tangent space
    half3 BlendNormalRNM(half3 n1, half3 n2)
    {
        half3 t = n1.xyz + half3(0.0, 0.0, 1.0);
        half3 u = n2.xyz * half3(-1.0, -1.0, 1.0);
        half3 r = (t / t.z) * dot(t, u) - u;
        return r;
    }

    half3 BlendNormal(half3 n1, half3 n2)
    {
        return normalize(half3(n1.xy * n2.z + n2.xy * n1.z, n1.z * n2.z));
    }

    //specular
    half3 Highlights(half3 positionWS, half roughness, half3 normalWS, half3 viewDirectionWS,half3 lightDir)
    {

        half roughness2 = roughness * roughness;
        half3 halfDir = normalize(lightDir + viewDirectionWS);
        half NoH = saturate(dot(normalize(normalWS), halfDir));
        half LoH = saturate(dot(lightDir, halfDir));
        // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
        // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
        // https://community.arm.com/events/1155
        half d = NoH * NoH * (roughness2 - 1.h) + 1.0001h;
        half LoH2 = LoH * LoH;
        half specularTerm = roughness2 / ((d * d) * max(0.1h, LoH2) * (roughness + 0.5h) * 4);
        // on mobiles (where half actually means something) denominator have risk of overflow
        // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
        // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
        #if defined (SHADER_API_MOBILE)
            // specularTerm = specularTerm - HALF_MIN;
            specularTerm = clamp(specularTerm, 0.0, 5.0); // Prevent FP16 overflow on mobiles
        #endif
        return specularTerm * _LightColor0;
    }

    //Debug
    #define DR(x) return fixed4(x,0,0,1);
    #define DRG(xy) return fixed4(xy,0,1);
    #define DRGB(xyz) return fixed4(xyz,1);


#endif