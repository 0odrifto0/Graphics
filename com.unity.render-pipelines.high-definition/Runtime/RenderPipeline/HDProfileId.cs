using UnityEngine.Rendering;

namespace UnityEngine.Rendering.HighDefinition
{
    internal enum HDProfileId
    {
        PushGlobalParameters,
        CopyDepthBuffer,
        CopyDepthInTargetTexture,
        CoarseStencilGeneration,
        HTileForSSS,
        RenderSSAO,
        ResolveStencilBuffer,
        HorizonSSAO,
        DenoiseSSAO,
        UpSampleSSAO,
        ScreenSpaceShadows,
        BuildLightList,
        ContactShadows,
        BlitToFinalRTDevBuildOnly,
        Distortion,
        ApplyDistortion,
        DepthPrepassForward,
        DepthPrepassDeferredForDecals,
        DepthPrepassDeferred,
        DepthPrepassDeferredIncomplete,
        TransparentDepthPrepass,
        GBuffer,
        GBufferDebug,
        DBufferRender,
        DBufferPrepareDrawData,
        DBufferNormal,
        DisplayDebugDecalsAtlas,
        DisplayDebugViewMaterial,
        DebugViewMaterialGBuffer,
        SubsurfaceScattering,
        SsrTracing,
        SsrReprojection,
        PrepareForTransparentSsr,
        SsgiPass,
        ForwardEmissive,
        ForwardOpaque,
        ForwardOpaqueDebug,
        ForwardTransparent,
        ForwardTransparentDebug,
        ForwardPreRefraction,
        ForwardPreRefractionDebug,
        ForwardTransparentDepthPrepass,
        RenderForwardError,
        TransparentDepthPostpass,
        ObjectsMotionVector,
        CameraMotionVectors,
        ColorPyramid,
        DepthPyramid,
        PostProcessing,
        AfterPostProcessing,
        RenderDebug,
        DisplayLightVolume,
        ClearBuffers,
        ClearDepthStencil,
        ClearStencil,
        ClearSssLightingBuffer,
        ClearSSSFilteringTarget,
        ClearAndCopyStencilTexture,
        ClearHDRTarget,
        ClearGBuffer,
        ClearSsrBuffers,
        HDRenderPipelineRenderCamera,
        HDRenderPipelineRenderAOV,
        HDRenderPipelineAllRenderRequest,
        CullResultsCull,
        CustomPassCullResultsCull,
        UpdateStencilCopyForSSRExclusion,
        GizmosPrePostprocess,
        Gizmos,
        DisplayCookieAtlas,
        RenderWireFrame,
        PushToColorPicker,
        ResolveMSAAColor,
        ResolveMSAAMotionVector,
        ResolveMSAADepth,
        ConvolveReflectionProbe,
        ConvolvePlanarReflectionProbe,
        PreIntegradeWardCookTorrance,
        FilterCubemapCharlie,
        FilterCubemapGGX,
        DisplayPointLightCookieArray,
        DisplayPlanarReflectionProbeAtlas,
        BlitTextureInPotAtlas,
        AreaLightCookieConvolution,

        UpdateSkyEnvironmentConvolution,
        RenderSkyToCubemap,
        UpdateSkyEnvironment,
        UpdateSkyAmbientProbe,
        PreRenderSky,
        RenderSky,
        OpaqueAtmosphericScattering,
        InScatteredRadiancePrecomputation,

        VolumeVoxelization,
        VolumetricLighting,
        VolumetricLightingFiltering,
        PrepareVisibleDensityVolumeList,

        RaytracingBuildCluster,
        RaytracingCullLights,
        RaytracingIntegrateReflection,
        RaytracingFilterReflection,
        RaytracingAmbientOcclusion,
        RaytracingFilterAmbientOcclusion,
        RaytracingDirectionalLightShadow,
        RaytracingLightShadow,
        RaytracingIntegrateIndirectDiffuse,
        RaytracingFilterIndirectDiffuse,
        RaytracingDebugOverlay,
        RayTracingRecursiveRendering,
        RayTracingPrepass,

        // Profile sampler for prepare light for GPU
        PrepareLightsForGPU,

        // Profile sampler for shadow
        RenderShadowMaps,
        RenderMomentShadowMaps,
        RenderPunctualShadowMaps,
        RenderDirectionalShadowMaps,
        RenderAreaShadowMaps,
        RenderEVSMShadowMaps,
        RenderEVSMShadowMapsBlur,
        RenderEVSMShadowMapsCopyToAtlas,

        // Profile sampler for tile pass
        LightLoopPushGlobalParameters,
        TileClusterLightingDebug,
        DisplayShadows,

        RenderDeferredLightingCompute,
        RenderDeferredLightingComputeAsPixel,
        RenderDeferredLightingSinglePass,
        RenderDeferredLightingSinglePassMRT,

        // Misc
        VolumeUpdate,
        CustomPassVolumeUpdate,

        // XR
        XROcclusionMesh,
        XRMirrorView,
        XRCustomMirrorView,
        XRDepthCopy,

        // Low res transparency
        DownsampleDepth,
        LowResTransparent,
        UpsampleLowResTransparent,

        // Post-processing
        AlphaCopy,
        StopNaNs,
        FixedExposure,
        DynamicExposure,
        TemporalAntialiasing,
        DepthOfField,
        DepthOfFieldKernel,
        DepthOfFieldCoC,
        DepthOfFieldPrefilter,
        DepthOfFieldPyramid,
        DepthOfFieldDilate,
        DepthOfFieldTileMax,
        DepthOfFieldGatherFar,
        DepthOfFieldGatherNear,
        DepthOfFieldPreCombine,
        DepthOfFieldCombine,
        MotionBlur,
        MotionBlurMotionVecPrep,
        MotionBlurTileMinMax,
        MotionBlurTileNeighbourhood,
        MotionBlurTileScattering,
        MotionBlurKernel,
        PaniniProjection,
        Bloom,
        ColorGradingLUTBuilder,
        UberPost,
        FXAA,
        SMAA,
        FinalPost,
        CustomPostProcessBeforePP,
        CustomPostProcessAfterPP,
        CustomPostProcessAfterOpaqueAndSky,
        ContrastAdaptiveSharpen,
        PrepareProbeVolumeList,
        ProbeVolumeDebug,
        BuildGPULightListProbeVolumes,
        PushProbeVolumeLightListGlobalParameters,

#if ENABLE_VIRTUALTEXTURES
        VTFeedbackClear,
#endif
    }
}
