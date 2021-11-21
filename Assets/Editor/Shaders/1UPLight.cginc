#ifndef ONEUP_LIGHT_CG_INCLUDE
    #define ONEUP_LIGHT_CG_INCLUDE


    #include "UnityCG.cginc"
    #include "AutoLight.cginc"
    #include "Lighting.cginc"

    //Lambert
    inline fixed3 Lambert(half3 lightDir,half3 normal)
    {
        return saturate(dot(lightDir,normal)) * _LightColor0 + UNITY_LIGHTMODEL_AMBIENT;
    }

    //HalfLambert
    inline fixed3 HalfLambert(half3 lightDir,half3 normal)
    {
        return (saturate(dot(lightDir,normal)) * 0.5 + 0.5) * _LightColor0 + UNITY_LIGHTMODEL_AMBIENT;
    }

    //BllinPhong
    inline fixed3 Specular(half3 lightDir,half3 normal,half3 viewDir,float power,float gloss)
    {
        half3 halfDir = normalize(lightDir+viewDir);
        float spec = pow(dot(halfDir,normal),power) * gloss;
        return _LightColor0 * spec;
    }

    //ToonLight
    inline fixed3 ToonRamp(half3 lightDir,half3 normal,float step)
    {
        fixed3 diffuse = Lambert(lightDir,normal);
        return (floor(diffuse.r * step) / step) * diffuse + UNITY_LIGHTMODEL_AMBIENT;
    }

    //ToonLight Texture
    inline fixed3 ToonRampTexture(half3 lightDir,half3 normal,sampler2D rampTex)
    {
        half d = dot(normal, lightDir)*0.5 + 0.5;
        fixed3 ramp = tex2D(rampTex, half2(d,d)).rgb;
        return ramp;
    }

    /*
    Schlick菲涅尔近似等式
    //resnelBias 菲尼尔偏移系数  
    //fresnelScale 菲尼尔缩放系数  
    //fresnelPower 菲尼尔指数  
    reflectFact = fresnelBias + fresnelScale*pow(1-dot(viewDir,N)),fresnelPower);  //系数：多少光发生折射多少光发生反射
    */
    inline fixed4 FresnelSchlick(half3 normal,half3 viewDir,samplerCUBE _CubeTex, half _RefractRatio,half _FresnelScale)
    {
        half3 reflectDir = reflect(normal,viewDir);
        half3 refractDir = refract(normal,viewDir,_RefractRatio);

        float4 fresnelReflectFactor = _FresnelScale + (1 - _FresnelScale)*pow(1-dot(viewDir,normal), 5);
        fixed4 colReflect = texCUBE(_CubeTex, normalize(reflectDir));
        fixed4 colRefract = texCUBE(_CubeTex, normalize(refractDir));
        fixed4 freselCol = fresnelReflectFactor * colReflect + (1-fresnelReflectFactor) * colRefract;
        
        return freselCol;
    }

    float Freshnel(float waterlevel,float dotNV)
    {
        // frensel
        float f0 = lerp(0.035, 0.02, waterlevel);
        float frensel = f0 + (1 - f0) * pow((1 - dotNV), 5);
        return frensel;
    }

    //采样ReflecPrbobe菲涅尔等式
    inline fixed4 FresnelSchlickReflectProbe(half3 normal,half3 viewDir,samplerCUBE _CubeTex, half _RefractRatio,half _FresnelScale)
    {
        half3 reflectDir = reflect(normal,viewDir);
        half3 refractDir = refract(normal,viewDir,_RefractRatio);

        float4 fresnelReflectFactor = _FresnelScale + (1 - _FresnelScale)*pow((1-dot(viewDir,normal)), 5);
        fixed4 colReflect = texCUBE(_CubeTex, normalize(reflectDir));
        fixed4 colRefract = texCUBE(_CubeTex, normalize(refractDir));
        fixed4 freselCol = fresnelReflectFactor * colReflect + (1-fresnelReflectFactor) * colRefract;
        
        return freselCol;
    }

    //计算天空球间接光映射
    //Sample IndirctReflection
    inline half3 SamplerReflectProbe(UNITY_ARGS_TEXCUBE(tex),half3 refDir,half roughness,half4 hdr)
    {
        roughness = roughness * (1.7 - 0.7 * roughness);
        half mip = roughness * 6;
        half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(tex,refDir,mip);
        return DecodeHDR(rgbm,hdr);
    }

    inline half3 BoxProjectedDirection(half3 worldRefDir,float3 worldPos,float4 cubemapCenter,float4 boxMin,float4 boxMax)
    {
        //使下面的if语句产生分支，定义在HLSLSupport.cginc中
        UNITY_BRANCH
        if(cubemapCenter.w > 0.0)//如果反射探头开启了BoxProjection选项，cubemapCenter.w > 0
        {
            half3 rbmax = (boxMax.xyz - worldPos) / worldRefDir;
            half3 rbmin = (boxMin.xyz - worldPos) / worldRefDir;

            half3 rbminmax = (worldRefDir > 0.0f) ? rbmax : rbmin;

            half fa = min(min(rbminmax.x,rbminmax.y),rbminmax.z);

            worldPos -= cubemapCenter.xyz;
            worldRefDir = worldPos + worldRefDir * fa;
        }
        return worldRefDir;
    }

    inline half3 ComputeIndirectSpecular(half3 refDir,float3 worldPos,half roughness)
    {
        half3 specular = 0;
        //重新映射第一个反射探头的采样方向
        half3 refDir1 = BoxProjectedDirection(refDir,worldPos,unity_SpecCube0_ProbePosition,unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax);
        //对第一个反射探头进行采样
        half3 ref1 = SamplerReflectProbe(UNITY_PASS_TEXCUBE(unity_SpecCube0),refDir1,roughness,unity_SpecCube0_HDR);
        //如果第一个反射探头的权重小于1的话，我们将会采样第二个反射探头，进行混合
        //使下面的if语句产生分支，定义在HLSLSupport.cginc中
        UNITY_BRANCH
        if(unity_SpecCube0_BoxMin.w < 0.99999)
        {
            //重新映射第二个反射探头的方向
            half3 refDir2 = BoxProjectedDirection(refDir,worldPos,unity_SpecCube1_ProbePosition,unity_SpecCube1_BoxMin,unity_SpecCube1_BoxMax);
            //对第二个反射探头进行采样
            half3 ref2 = SamplerReflectProbe(UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0),refDir2,roughness,unity_SpecCube1_HDR);

            //进行混合
            specular = lerp(ref2,ref1,unity_SpecCube0_BoxMin.w);
        }
        else
        {
            specular = ref1;
        }
        return specular;
    }


#endif