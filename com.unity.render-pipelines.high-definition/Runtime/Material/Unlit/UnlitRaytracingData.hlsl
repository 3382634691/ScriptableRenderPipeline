#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/RaytracingBuiltinData.hlsl"

void GetSurfaceDataFromIntersection(FragInputs input, float3 V, PositionInputs posInput, IntersectionVertice intersectionVertice, RayCone rayCone, out SurfaceData surfaceData, out BuiltinData builtinData)
{
    float2 unlitColorMapUv = TRANSFORM_TEX(input.texCoord0.xy, _UnlitColorMap);
    surfaceData.color = SAMPLE_TEXTURE2D_LOD(_UnlitColorMap, sampler_UnlitColorMap, unlitColorMapUv, 0).rgb * _UnlitColor.rgb;
    float alpha = SAMPLE_TEXTURE2D_LOD(_UnlitColorMap, sampler_UnlitColorMap, unlitColorMapUv, 0).a * _UnlitColor.a;

#ifdef _ALPHATEST_ON
    DoAlphaTest(alpha, _AlphaCutoff);
#endif

    // Builtin Data
    ZERO_INITIALIZE(BuiltinData, builtinData); // No call to InitBuiltinData as we don't have any lighting
    builtinData.opacity = alpha;

#ifdef _EMISSIVE_COLOR_MAP
    builtinData.emissiveColor = SAMPLE_TEXTURE2D_LOD(_EmissiveColorMap, sampler_EmissiveColorMap, TRANSFORM_TEX(input.texCoord0.xy, _EmissiveColorMap), 0).rgb * _EmissiveColor;
#else
    builtinData.emissiveColor = _EmissiveColor;
#endif

#if (SHADERPASS == SHADERPASS_DISTORTION) || defined(DEBUG_DISPLAY)
    float3 distortion = SAMPLE_TEXTURE2D_LOD(_DistortionVectorMap, sampler_DistortionVectorMap, input.texCoord0.xy, 0).rgb;
    distortion.rg = distortion.rg * _DistortionVectorScale.xx + _DistortionVectorBias.xx;
    builtinData.distortion = distortion.rg * _DistortionScale;
    builtinData.distortionBlur = clamp(distortion.b * _DistortionBlurScale, 0.0, 1.0) * (_DistortionBlurRemapMax - _DistortionBlurRemapMin) + _DistortionBlurRemapMin;
#endif

#if defined(DEBUG_DISPLAY)
    if (_DebugMipMapMode != DEBUGMIPMAPMODE_NONE)
    {
        surfaceData.color = GetTextureDataDebug(_DebugMipMapMode, unlitColorMapUv, _UnlitColorMap, _UnlitColorMap_TexelSize, _UnlitColorMap_MipInfo, surfaceData.color);
    }
#endif
}