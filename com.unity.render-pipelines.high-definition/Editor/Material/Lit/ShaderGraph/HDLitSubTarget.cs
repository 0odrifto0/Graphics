﻿using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.Graphing;
using static UnityEngine.Rendering.HighDefinition.HDMaterialProperties;

namespace UnityEditor.Rendering.HighDefinition.ShaderGraph
{
    sealed class HDLitSubTarget : SubTarget<HDTarget>, IHasMetadata,
        IRequiresData<HDSystemData>, IRequiresData<HDBuiltinData>, IRequiresData<HDLightingData>, IRequiresData<HDLitData>
    {
        const string kAssetGuid = "caab952c840878340810cca27417971c";
        static string passTemplatePath => $"{HDUtils.GetHDRenderPipelinePath()}Editor/Material/Lit/ShaderGraph/LitPass.template";

        public HDLitSubTarget()
        {
            displayName = "Lit";
        }

        // Render State
        string renderType => HDRenderTypeTags.HDLitShader.ToString();
        string renderQueue
        {
            get
            {
                if(systemData.renderingPass == HDRenderQueue.RenderQueueType.Unknown)
                {
                    switch(systemData.surfaceType)
                    {
                        case SurfaceType.Opaque:
                            systemData.renderingPass = HDRenderQueue.RenderQueueType.Opaque;
                            break;
                        case SurfaceType.Transparent:
                        #pragma warning disable CS0618 // Type or member is obsolete
                            if (litData.drawBeforeRefraction)
                            {
                                litData.drawBeforeRefraction = false;
                        #pragma warning restore CS0618 // Type or member is obsolete
                                systemData.renderingPass = HDRenderQueue.RenderQueueType.PreRefraction;
                            }
                            else
                            {
                                systemData.renderingPass = HDRenderQueue.RenderQueueType.Transparent;
                            }
                            break;
                    }
                }
                int queue = HDRenderQueue.ChangeType(systemData.renderingPass, systemData.sortPriority, systemData.alphaTest);
                return HDRenderQueue.GetShaderTagValue(queue);
            }
        }

        // Material Data
        HDSystemData m_SystemData;
        HDBuiltinData m_BuiltinData;
        HDLightingData m_LightingData;
        HDLitData m_LitData;

        // Interface Properties
        HDSystemData IRequiresData<HDSystemData>.data
        {
            get => m_SystemData;
            set => m_SystemData = value;
        }
        HDBuiltinData IRequiresData<HDBuiltinData>.data
        {
            get => m_BuiltinData;
            set => m_BuiltinData = value;
        }
        HDLightingData IRequiresData<HDLightingData>.data
        {
            get => m_LightingData;
            set => m_LightingData = value;
        }
        HDLitData IRequiresData<HDLitData>.data
        {
            get => m_LitData;
            set => m_LitData = value;
        }

        // Public properties
        public HDSystemData systemData
        {
            get => m_SystemData;
            set => m_SystemData = value;
        }
        public HDBuiltinData builtinData
        {
            get => m_BuiltinData;
            set => m_BuiltinData = value;
        }
        public HDLightingData lightingData
        {
            get => m_LightingData;
            set => m_LightingData = value;
        }
        public HDLitData litData
        {
            get => m_LitData;
            set => m_LitData = value;
        }

        public override bool IsActive() => true;

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependencyPath(AssetDatabase.GUIDToAssetPath(kAssetGuid));
            context.SetDefaultShaderGUI("Rendering.HighDefinition.HDLitGUI");

            // Process SubShaders
            SubShaderDescriptor[] subShaders = { SubShaders.Lit, SubShaders.LitRaytracing };
            for(int i = 0; i < subShaders.Length; i++)
            {
                // Update Render State
                subShaders[i].renderType = renderType;
                subShaders[i].renderQueue = renderQueue;

                // Add
                context.AddSubShader(subShaders[i]);
            }
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            bool hasRefraction = (systemData.surfaceType == SurfaceType.Transparent && systemData.renderingPass != HDRenderQueue.RenderQueueType.PreRefraction && litData.refractionModel != ScreenSpaceRefraction.RefractionModel.None);

            // Features
            context.AddField(Fields.LodCrossFade,                           systemData.supportLodCrossFade);

            // Surface Type
            context.AddField(Fields.SurfaceOpaque,                          systemData.surfaceType == SurfaceType.Opaque);
            context.AddField(Fields.SurfaceTransparent,                     systemData.surfaceType != SurfaceType.Opaque);

            // Structs
            context.AddField(HDStructFields.FragInputs.IsFrontFace,         systemData.doubleSidedMode != DoubleSidedMode.Disabled && !context.pass.Equals(HDLitSubTarget.LitPasses.MotionVectors));

            // Dots
            context.AddField(HDFields.DotsInstancing,                       systemData.dotsInstancing);
            context.AddField(HDFields.DotsProperties,                       context.hasDotsProperties);

            // Material
            context.AddField(HDFields.Anisotropy,                           litData.materialType == HDLitData.MaterialType.Anisotropy);
            context.AddField(HDFields.Iridescence,                          litData.materialType == HDLitData.MaterialType.Iridescence);
            context.AddField(HDFields.SpecularColor,                        litData.materialType == HDLitData.MaterialType.SpecularColor);
            context.AddField(HDFields.Standard,                             litData.materialType == HDLitData.MaterialType.Standard);
            context.AddField(HDFields.SubsurfaceScattering,                 litData.materialType == HDLitData.MaterialType.SubsurfaceScattering && systemData.surfaceType != SurfaceType.Transparent);
            context.AddField(HDFields.Transmission,                         (litData.materialType == HDLitData.MaterialType.SubsurfaceScattering && litData.sssTransmission) ||
                                                                                (litData.materialType == HDLitData.MaterialType.Translucent));
            context.AddField(HDFields.Translucent,                          litData.materialType == HDLitData.MaterialType.Translucent);

            // Blend Mode
            context.AddField(Fields.BlendAdd,                               systemData.surfaceType != SurfaceType.Opaque && systemData.blendMode == BlendMode.Additive);
            context.AddField(Fields.BlendAlpha,                             systemData.surfaceType != SurfaceType.Opaque && systemData.blendMode == BlendMode.Alpha);
            context.AddField(Fields.BlendPremultiply,                       systemData.surfaceType != SurfaceType.Opaque && systemData.blendMode == BlendMode.Premultiply);

            // Double Sided
            context.AddField(HDFields.DoubleSided,                          systemData.doubleSidedMode != DoubleSidedMode.Disabled);
            context.AddField(HDFields.DoubleSidedFlip,                      systemData.doubleSidedMode == DoubleSidedMode.FlippedNormals && !context.pass.Equals(HDLitSubTarget.LitPasses.MotionVectors));
            context.AddField(HDFields.DoubleSidedMirror,                    systemData.doubleSidedMode == DoubleSidedMode.MirroredNormals && !context.pass.Equals(HDLitSubTarget.LitPasses.MotionVectors));

            // Specular Occlusion
            context.AddField(HDFields.SpecularOcclusionFromAO,              lightingData.specularOcclusionMode == SpecularOcclusionMode.FromAO);
            context.AddField(HDFields.SpecularOcclusionFromAOBentNormal,    lightingData.specularOcclusionMode == SpecularOcclusionMode.FromAOAndBentNormal);
            context.AddField(HDFields.SpecularOcclusionCustom,              lightingData.specularOcclusionMode == SpecularOcclusionMode.Custom);

            // Distortion
            context.AddField(HDFields.DistortionDepthTest,                  builtinData.distortionDepthTest);
            context.AddField(HDFields.DistortionAdd,                        builtinData.distortionMode == DistortionMode.Add);
            context.AddField(HDFields.DistortionMultiply,                   builtinData.distortionMode == DistortionMode.Multiply);
            context.AddField(HDFields.DistortionReplace,                    builtinData.distortionMode == DistortionMode.Replace);
            context.AddField(HDFields.TransparentDistortion,                systemData.surfaceType != SurfaceType.Opaque && builtinData.distortion);

            // Refraction
            context.AddField(HDFields.Refraction,                           hasRefraction);
            context.AddField(HDFields.RefractionBox,                        hasRefraction && litData.refractionModel == ScreenSpaceRefraction.RefractionModel.Box);
            context.AddField(HDFields.RefractionSphere,                     hasRefraction && litData.refractionModel == ScreenSpaceRefraction.RefractionModel.Sphere);

            // Normal Drop Off Space
            context.AddField(Fields.NormalDropOffOS,                        lightingData.normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddField(Fields.NormalDropOffTS,                        lightingData.normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddField(Fields.NormalDropOffWS,                        lightingData.normalDropOffSpace == NormalDropOffSpace.World);


            // AlphaTest
            // We always generate the keyword ALPHATEST_ON
            context.AddField(Fields.AlphaTest,                              systemData.alphaTest && (context.pass.pixelBlocks.Contains(BlockFields.SurfaceDescription.AlphaClipThreshold) || context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow) ||
                                                                                context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPrepass) || context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPostpass)));
            // All the DoAlphaXXX field drive the generation of which code to use for alpha test in the template
            // Do alpha test only if we aren't using the TestShadow one
            context.AddField(HDFields.DoAlphaTest,                          systemData.alphaTest && (context.pass.pixelBlocks.Contains(BlockFields.SurfaceDescription.AlphaClipThreshold) &&
                                                                                !(lightingData.alphaTestShadow && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow))));
            context.AddField(HDFields.DoAlphaTestShadow,                    systemData.alphaTest && lightingData.alphaTestShadow && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow));
            context.AddField(HDFields.DoAlphaTestPrepass,                   systemData.alphaTest && systemData.alphaTestDepthPrepass && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPrepass));
            context.AddField(HDFields.DoAlphaTestPostpass,                  systemData.alphaTest && systemData.alphaTestDepthPostpass && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPostpass));

            // Misc
            context.AddField(Fields.AlphaToMask,                            systemData.alphaTest && context.pass.pixelBlocks.Contains(BlockFields.SurfaceDescription.AlphaClipThreshold) && builtinData.alphaToMask);
            context.AddField(HDFields.AlphaFog,                             systemData.surfaceType != SurfaceType.Opaque && builtinData.transparencyFog);
            context.AddField(HDFields.BlendPreserveSpecular,                systemData.surfaceType != SurfaceType.Opaque && lightingData.blendPreserveSpecular);
            context.AddField(HDFields.TransparentWritesMotionVec,           systemData.surfaceType != SurfaceType.Opaque && builtinData.transparentWritesMotionVec);
            context.AddField(HDFields.DisableDecals,                        !lightingData.receiveDecals);
            context.AddField(HDFields.DisableSSR,                           !lightingData.receiveSSR);
            context.AddField(HDFields.DisableSSRTransparent,                !litData.receiveSSRTransparent);
            context.AddField(Fields.VelocityPrecomputed,                    builtinData.addPrecomputedVelocity);
            context.AddField(HDFields.SpecularAA,                           lightingData.specularAA &&
                                                                                context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.SpecularAAThreshold) &&
                                                                                context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance));
            context.AddField(HDFields.EnergyConservingSpecular,             lightingData.energyConservingSpecular);
            context.AddField(HDFields.BentNormal,                           context.blocks.Contains(HDBlockFields.SurfaceDescription.BentNormal) && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.BentNormal));
            context.AddField(HDFields.AmbientOcclusion,                     context.blocks.Contains(BlockFields.SurfaceDescription.Occlusion) && context.pass.pixelBlocks.Contains(BlockFields.SurfaceDescription.Occlusion));
            context.AddField(HDFields.CoatMask,                             context.blocks.Contains(HDBlockFields.SurfaceDescription.CoatMask) && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.CoatMask));
            context.AddField(HDFields.Tangent,                              context.blocks.Contains(HDBlockFields.SurfaceDescription.Tangent) && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.Tangent));
            context.AddField(HDFields.LightingGI,                           context.blocks.Contains(HDBlockFields.SurfaceDescription.BakedGI) && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.BakedGI));
            context.AddField(HDFields.BackLightingGI,                       context.blocks.Contains(HDBlockFields.SurfaceDescription.BakedBackGI) && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.BakedBackGI));
            context.AddField(HDFields.DepthOffset,                          builtinData.depthOffset && context.pass.pixelBlocks.Contains(HDBlockFields.SurfaceDescription.DepthOffset));
            context.AddField(HDFields.TransparentBackFace,                  systemData.surfaceType != SurfaceType.Opaque && lightingData.backThenFrontRendering);
            context.AddField(HDFields.TransparentDepthPrePass,              systemData.surfaceType != SurfaceType.Opaque && systemData.alphaTestDepthPrepass);
            context.AddField(HDFields.TransparentDepthPostPass,             systemData.surfaceType != SurfaceType.Opaque && systemData.alphaTestDepthPrepass);
            context.AddField(HDFields.RayTracing,                           litData.rayTracing);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            bool hasRefraction = (systemData.surfaceType == SurfaceType.Transparent && systemData.renderingPass != HDRenderQueue.RenderQueueType.PreRefraction && litData.refractionModel != ScreenSpaceRefraction.RefractionModel.None);
            bool hasDistortion = (systemData.surfaceType == SurfaceType.Transparent && builtinData.distortion);

            // Vertex
            context.AddBlock(BlockFields.VertexDescription.Position);
            context.AddBlock(BlockFields.VertexDescription.Normal);
            context.AddBlock(BlockFields.VertexDescription.Tangent);

            // Common
            context.AddBlock(BlockFields.SurfaceDescription.BaseColor);
            context.AddBlock(HDBlockFields.SurfaceDescription.BentNormal);
            context.AddBlock(HDBlockFields.SurfaceDescription.CoatMask);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);
            context.AddBlock(BlockFields.SurfaceDescription.Smoothness);
            context.AddBlock(BlockFields.SurfaceDescription.Occlusion);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha);

            // Alpha Test
            context.AddBlock(BlockFields.SurfaceDescription.Alpha,                  systemData.alphaTest);
            context.AddBlock(HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPrepass, systemData.surfaceType == SurfaceType.Transparent && systemData.alphaTest && systemData.alphaTestDepthPrepass);
            context.AddBlock(HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPostpass, systemData.surfaceType == SurfaceType.Transparent && systemData.alphaTest && systemData.alphaTestDepthPostpass);
            context.AddBlock(HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow, systemData.alphaTest && lightingData.alphaTestShadow);

            // Specular AA
            context.AddBlock(HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance, lightingData.specularAA);
            context.AddBlock(HDBlockFields.SurfaceDescription.SpecularAAThreshold,  lightingData.specularAA);

            // Refraction
            context.AddBlock(HDBlockFields.SurfaceDescription.RefractionIndex,      hasRefraction);
            context.AddBlock(HDBlockFields.SurfaceDescription.RefractionColor,      hasRefraction);
            context.AddBlock(HDBlockFields.SurfaceDescription.RefractionDistance,   hasRefraction);

            // Distortion
            context.AddBlock(HDBlockFields.SurfaceDescription.Distortion,           hasDistortion);
            context.AddBlock(HDBlockFields.SurfaceDescription.DistortionBlur,       hasDistortion);

            // Baked GI
            context.AddBlock(HDBlockFields.SurfaceDescription.BakedGI,              lightingData.overrideBakedGI);
            context.AddBlock(HDBlockFields.SurfaceDescription.BakedBackGI,          lightingData.overrideBakedGI);

            // Normal
            context.AddBlock(BlockFields.SurfaceDescription.NormalOS,               lightingData.normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddBlock(BlockFields.SurfaceDescription.NormalTS,               lightingData.normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddBlock(BlockFields.SurfaceDescription.NormalWS,               lightingData.normalDropOffSpace == NormalDropOffSpace.World);

            // Material
            context.AddBlock(HDBlockFields.SurfaceDescription.Tangent,              litData.materialType == HDLitData.MaterialType.Anisotropy);
            context.AddBlock(HDBlockFields.SurfaceDescription.Anisotropy,           litData.materialType == HDLitData.MaterialType.Anisotropy);
            context.AddBlock(HDBlockFields.SurfaceDescription.SubsurfaceMask,       litData.materialType == HDLitData.MaterialType.SubsurfaceScattering);
            context.AddBlock(HDBlockFields.SurfaceDescription.Thickness,            ((litData.materialType == HDLitData.MaterialType.SubsurfaceScattering || litData.materialType == HDLitData.MaterialType.Translucent) &&
                                                                                        (litData.sssTransmission || litData.materialType == HDLitData.MaterialType.Translucent)) || hasRefraction);
            context.AddBlock(HDBlockFields.SurfaceDescription.DiffusionProfileHash, litData.materialType == HDLitData.MaterialType.SubsurfaceScattering || litData.materialType == HDLitData.MaterialType.Translucent);
            context.AddBlock(HDBlockFields.SurfaceDescription.IridescenceMask,      litData.materialType == HDLitData.MaterialType.Iridescence);
            context.AddBlock(HDBlockFields.SurfaceDescription.IridescenceThickness, litData.materialType == HDLitData.MaterialType.Iridescence);
            context.AddBlock(BlockFields.SurfaceDescription.Specular,               litData.materialType == HDLitData.MaterialType.SpecularColor);
            context.AddBlock(BlockFields.SurfaceDescription.Metallic,               litData.materialType == HDLitData.MaterialType.Standard || 
                                                                                        litData.materialType == HDLitData.MaterialType.Anisotropy ||
                                                                                        litData.materialType == HDLitData.MaterialType.Iridescence);

            // Misc
            context.AddBlock(HDBlockFields.SurfaceDescription.SpecularOcclusion,    lightingData.specularOcclusionMode == SpecularOcclusionMode.Custom);
            context.AddBlock(HDBlockFields.SurfaceDescription.DepthOffset,          builtinData.depthOffset);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange)
        {
            var settingsView = new HDLitSettingsView(this);
            settingsView.GetPropertiesGUI(ref context, onChange);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // Trunk currently relies on checking material property "_EmissionColor" to allow emissive GI. If it doesn't find that property, or it is black, GI is forced off.
            // ShaderGraph doesn't use this property, so currently it inserts a dummy color (white). This dummy color may be removed entirely once the following PR has been merged in trunk: Pull request #74105
            // The user will then need to explicitly disable emissive GI if it is not needed.
            // To be able to automatically disable emission based on the ShaderGraph config when emission is black,
            // we will need a more general way to communicate this to the engine (not directly tied to a material property).
            collector.AddShaderProperty(new ColorShaderProperty()
            {
                overrideReferenceName = "_EmissionColor",
                hidden = true,
                value = new Color(1.0f, 1.0f, 1.0f, 1.0f)
            });
            // ShaderGraph only property used to send the RenderQueueType to the material
            collector.AddShaderProperty(new Vector1ShaderProperty
            {
                overrideReferenceName = "_RenderQueueType",
                hidden = true,
                value = (int)systemData.renderingPass,
            });

            //See SG-ADDITIONALVELOCITY-NOTE
            if (builtinData.addPrecomputedVelocity)
            {
                collector.AddShaderProperty(new BooleanShaderProperty
                {
                    value = true,
                    hidden = true,
                    overrideReferenceName = kAddPrecomputedVelocity,
                });
            }

            // Add all shader properties required by the inspector
            HDSubShaderUtilities.AddStencilShaderProperties(collector, litData.materialType == HDLitData.MaterialType.SubsurfaceScattering, lightingData.receiveSSR, litData.receiveSSRTransparent);
            HDSubShaderUtilities.AddBlendingStatesShaderProperties(
                collector,
                systemData.surfaceType,
                systemData.blendMode,
                systemData.sortPriority,
                builtinData.alphaToMask,
                systemData.zWrite,
                systemData.transparentCullMode,
                systemData.zTest,
                lightingData.backThenFrontRendering,
                builtinData.transparencyFog
            );
            HDSubShaderUtilities.AddAlphaCutoffShaderProperties(collector, systemData.alphaTest, lightingData.alphaTestShadow);
            HDSubShaderUtilities.AddDoubleSidedProperty(collector, systemData.doubleSidedMode);
            HDSubShaderUtilities.AddRayTracingProperty(collector, litData.rayTracing);
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            // Fixup the material settings:
            material.SetFloat(kSurfaceType, (int)systemData.surfaceType);
            material.SetFloat(kDoubleSidedNormalMode, (int)systemData.doubleSidedMode);
            material.SetFloat(kAlphaCutoffEnabled, systemData.alphaTest ? 1 : 0);
            material.SetFloat(kBlendMode, (int)systemData.blendMode);
            material.SetFloat(kEnableFogOnTransparent, builtinData.transparencyFog ? 1.0f : 0.0f);
            material.SetFloat(kZTestTransparent, (int)systemData.zTest);
            material.SetFloat(kTransparentCullMode, (int)systemData.transparentCullMode);
            material.SetFloat(kZWrite, systemData.zWrite ? 1.0f : 0.0f);

            // No sorting priority for shader graph preview
            material.renderQueue = (int)HDRenderQueue.ChangeType(systemData.renderingPass, offset: 0, alphaTest: systemData.alphaTest);

            HDLitGUI.SetupMaterialKeywordsAndPass(material);
        }

        int ComputeMaterialNeedsUpdateHash()
        {
            int hash = 0;
            hash |= (systemData.alphaTest ? 0 : 1) << 0;
            hash |= (lightingData.alphaTestShadow ? 0 : 1) << 1;
            hash |= (lightingData.receiveSSR ? 0 : 1) << 2;
            hash |= (litData.receiveSSRTransparent ? 0 : 1) << 3;
            hash |= (litData.materialType == HDLitData.MaterialType.SubsurfaceScattering ? 0 : 1) << 4;
            return hash;
        }

        public override object saveContext
        {
            get
            {
                int hash = ComputeMaterialNeedsUpdateHash();
                bool needsUpdate = hash != systemData.materialNeedsUpdateHash;
                if (needsUpdate)
                    systemData.materialNeedsUpdateHash = hash;

                return new HDSaveContext{ updateMaterials = needsUpdate };
            }
        }

        // IHasMetaData
        public string identifier => "HDLitSubTarget";

        public ScriptableObject GetMetadataObject()
        {
            var hdMetadata = ScriptableObject.CreateInstance<HDMetadata>();
            hdMetadata.shaderID = HDShaderUtils.ShaderID.SG_Lit;
            return hdMetadata;
        }

#region SubShaders
        static class SubShaders
        {
            public static SubShaderDescriptor Lit = new SubShaderDescriptor()
            {
                pipelineTag = HDRenderPipeline.k_ShaderTagName,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { LitPasses.ShadowCaster },
                    { LitPasses.META },
                    { LitPasses.SceneSelection },
                    { LitPasses.DepthOnly },
                    { LitPasses.GBuffer },
                    { LitPasses.MotionVectors },
                    { LitPasses.DistortionVectors, new FieldCondition(HDFields.TransparentDistortion, true) },
                    { LitPasses.TransparentBackface, new FieldCondition(HDFields.TransparentBackFace, true) },
                    { LitPasses.TransparentDepthPrepass, new FieldCondition(HDFields.TransparentDepthPrePass, true) },
                    { LitPasses.Forward },
                    { LitPasses.TransparentDepthPostpass, new FieldCondition(HDFields.TransparentDepthPostPass, true) },
                    { LitPasses.RayTracingPrepass, new FieldCondition(HDFields.RayTracing, true) },
                },
            };

            public static SubShaderDescriptor LitRaytracing = new SubShaderDescriptor()
            {
                pipelineTag = HDRenderPipeline.k_ShaderTagName,
                generatesPreview = false,
                passes = new PassCollection
                {
                    { LitPasses.RaytracingIndirect, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingVisibility, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingForward, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingGBuffer, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingSubSurface, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingPathTracing, new FieldCondition(Fields.IsPreview, false) },
                },
            };
        }
#endregion

#region Passes
        public static class LitPasses
        {
            public static PassDescriptor GBuffer = new PassDescriptor()
            {
                // Definition
                displayName = "GBuffer",
                referenceName = "SHADERPASS_GBUFFER",
                lightMode = "GBuffer",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitMinimal,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.GBuffer,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = LitKeywords.GBuffer,
                includes = LitIncludes.GBuffer,
            };

            public static PassDescriptor META = new PassDescriptor()
            {
                // Definition
                displayName = "META",
                referenceName = "SHADERPASS_LIGHT_TRANSPORT",
                lightMode = "META",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                pixelBlocks = LitBlockMasks.FragmentMeta,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.Meta,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.Meta,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.Meta,
            };

            public static PassDescriptor ShadowCaster = new PassDescriptor()
            {
                // Definition
                displayName = "ShadowCaster",
                referenceName = "SHADERPASS_SHADOWS",
                lightMode = "ShadowCaster",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentShadowCaster,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.BlendShadowCaster,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor SceneSelection = new PassDescriptor()
            {
                // Definition
                displayName = "SceneSelectionPass",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "SceneSelectionPass",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentSceneSelection,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.SceneSelection,
                pragmas = CorePragmas.DotsInstancedInV1AndV2EditorSync,
                defines = CoreDefines.SceneSelection,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor DepthOnly = new PassDescriptor()
            {
                // Definition
                displayName = "DepthOnly",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "DepthOnly",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDepthMotionVectors,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitFull,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.DepthOnly,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = LitKeywords.DepthMotionVectors,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor MotionVectors = new PassDescriptor()
            {
                // Definition
                displayName = "MotionVectors",
                referenceName = "SHADERPASS_MOTION_VECTORS",
                lightMode = "MotionVectors",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDepthMotionVectors,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitFull,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.MotionVectors,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = LitKeywords.DepthMotionVectors,
                includes = LitIncludes.MotionVectors,
            };

            public static PassDescriptor DistortionVectors = new PassDescriptor()
            {
                // Definition
                displayName = "DistortionVectors",
                referenceName = "SHADERPASS_DISTORTION",
                lightMode = "DistortionVectors",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDistortion,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.Distortion,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.Distortion,
            };

            public static PassDescriptor TransparentDepthPrepass = new PassDescriptor()
            {
                // Definition
                displayName = "TransparentDepthPrepass",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "TransparentDepthPrepass",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentTransparentDepthPrepass,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitFull,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.TransparentDepthPrePostPass,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.TransparentDepthPrepass,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor TransparentBackface = new PassDescriptor()
            {
                // Definition
                displayName = "TransparentBackface",
                referenceName = "SHADERPASS_FORWARD",
                lightMode = "TransparentBackface",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentTransparentBackface,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.TransparentBackface,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.Forward,
                keywords = CoreKeywords.Forward,
                includes = LitIncludes.Forward,
            };

            public static PassDescriptor Forward = new PassDescriptor()
            {
                // Definition
                displayName = "Forward",
                referenceName = "SHADERPASS_FORWARD",
                lightMode = "Forward",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitMinimal,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.ForwardColorMask,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.Forward,
                keywords = CoreKeywords.Forward,
                includes = LitIncludes.Forward,
            };

            public static PassDescriptor TransparentDepthPostpass = new PassDescriptor()
            {
                // Definition
                displayName = "TransparentDepthPostpass",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "TransparentDepthPostpass",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentTransparentDepthPostpass,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.TransparentDepthPrePostPass,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor RayTracingPrepass = new PassDescriptor()
            {
                // Definition
                displayName = "RayTracingPrepass",
                referenceName = "SHADERPASS_CONSTANT",
                lightMode = "RayTracingPrepass",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentRayTracingPrepass,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.RayTracingPrepass,
                pragmas = LitPragmas.RaytracingBasic,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.RayTracingPrepass,
            };

            public static PassDescriptor RaytracingIndirect = new PassDescriptor()
            {
                // Definition
                displayName = "IndirectDXR",
                referenceName = "SHADERPASS_RAYTRACING_INDIRECT",
                lightMode = "IndirectDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingForwardIndirect,
                keywords = CoreKeywords.RaytracingIndirect,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingIndirect },
            };

            public static PassDescriptor RaytracingVisibility = new PassDescriptor()
            {
                // Definition
                displayName = "VisibilityDXR",
                referenceName = "SHADERPASS_RAYTRACING_VISIBILITY",
                lightMode = "VisibilityDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingVisibility,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingVisibility },
            };

            public static PassDescriptor RaytracingForward = new PassDescriptor()
            {
                // Definition
                displayName = "ForwardDXR",
                referenceName = "SHADERPASS_RAYTRACING_FORWARD",
                lightMode = "ForwardDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingForwardIndirect,
                keywords = CoreKeywords.RaytracingGBufferForward,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingForward },
            };

            public static PassDescriptor RaytracingGBuffer = new PassDescriptor()
            {
                // Definition
                displayName = "GBufferDXR",
                referenceName = "SHADERPASS_RAYTRACING_GBUFFER",
                lightMode = "GBufferDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingGBuffer,
                keywords = CoreKeywords.RaytracingGBufferForward,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RayTracingGBuffer },
            };

            public static PassDescriptor RaytracingPathTracing = new PassDescriptor()
            {
                //Definition
                displayName = "PathTracingDXR",
                referenceName = "SHADERPASS_PATH_TRACING",
                lightMode = "PathTracingDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                //Port mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                //Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingPathTracing,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingPathTracing },
            };

            public static PassDescriptor RaytracingSubSurface = new PassDescriptor()
            {
                //Definition
                displayName = "SubSurfaceDXR",
                referenceName = "SHADERPASS_RAYTRACING_SUB_SURFACE",
                lightMode = "SubSurfaceDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                //Port mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentDefault,

                //Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingGBuffer,
                keywords = CoreKeywords.RaytracingGBufferForward,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingSubSurface },
            };
        }
#endregion

#region BlockMasks
        static class LitBlockMasks
        {
            public static BlockFieldDescriptor[] FragmentDefault = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                HDBlockFields.SurfaceDescription.BentNormal,
                HDBlockFields.SurfaceDescription.Tangent,
                HDBlockFields.SurfaceDescription.SubsurfaceMask,
                HDBlockFields.SurfaceDescription.Thickness,
                HDBlockFields.SurfaceDescription.DiffusionProfileHash,
                HDBlockFields.SurfaceDescription.IridescenceMask,
                HDBlockFields.SurfaceDescription.IridescenceThickness,
                BlockFields.SurfaceDescription.Specular,
                HDBlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                HDBlockFields.SurfaceDescription.SpecularOcclusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Anisotropy,
                HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance,
                HDBlockFields.SurfaceDescription.SpecularAAThreshold,
                HDBlockFields.SurfaceDescription.RefractionIndex,
                HDBlockFields.SurfaceDescription.RefractionColor,
                HDBlockFields.SurfaceDescription.RefractionDistance,
                HDBlockFields.SurfaceDescription.BakedGI,
                HDBlockFields.SurfaceDescription.BakedBackGI,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentMeta = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                HDBlockFields.SurfaceDescription.BentNormal,
                HDBlockFields.SurfaceDescription.Tangent,
                HDBlockFields.SurfaceDescription.SubsurfaceMask,
                HDBlockFields.SurfaceDescription.Thickness,
                HDBlockFields.SurfaceDescription.DiffusionProfileHash,
                HDBlockFields.SurfaceDescription.IridescenceMask,
                HDBlockFields.SurfaceDescription.IridescenceThickness,
                BlockFields.SurfaceDescription.Specular,
                HDBlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                HDBlockFields.SurfaceDescription.SpecularOcclusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Anisotropy,
                HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance,
                HDBlockFields.SurfaceDescription.SpecularAAThreshold,
                HDBlockFields.SurfaceDescription.RefractionIndex,
                HDBlockFields.SurfaceDescription.RefractionColor,
                HDBlockFields.SurfaceDescription.RefractionDistance,
            };

            public static BlockFieldDescriptor[] FragmentShadowCaster = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentSceneSelection = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentDepthMotionVectors = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentDistortion = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Distortion,
                HDBlockFields.SurfaceDescription.DistortionBlur,
            };

            public static BlockFieldDescriptor[] FragmentTransparentDepthPrepass = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPrepass,
                HDBlockFields.SurfaceDescription.DepthOffset,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.Smoothness,
            };

            public static BlockFieldDescriptor[] FragmentTransparentBackface = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                HDBlockFields.SurfaceDescription.BentNormal,
                HDBlockFields.SurfaceDescription.Tangent,
                HDBlockFields.SurfaceDescription.SubsurfaceMask,
                HDBlockFields.SurfaceDescription.Thickness,
                HDBlockFields.SurfaceDescription.DiffusionProfileHash,
                HDBlockFields.SurfaceDescription.IridescenceMask,
                HDBlockFields.SurfaceDescription.IridescenceThickness,
                BlockFields.SurfaceDescription.Specular,
                HDBlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                HDBlockFields.SurfaceDescription.SpecularOcclusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Anisotropy,
                HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance,
                HDBlockFields.SurfaceDescription.SpecularAAThreshold,
                HDBlockFields.SurfaceDescription.RefractionIndex,
                HDBlockFields.SurfaceDescription.RefractionColor,
                HDBlockFields.SurfaceDescription.RefractionDistance,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentTransparentDepthPostpass = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPostpass,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentRayTracingPrepass = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };
        }
#endregion

#region RenderStates
        static class LitRenderStates
        {
            public static RenderStateCollection GBuffer = new RenderStateCollection
            {
                { RenderState.Cull(CoreRenderStates.Uniforms.cullMode) },
                { RenderState.ZTest(CoreRenderStates.Uniforms.zTestGBuffer) },
                { RenderState.Stencil(new StencilDescriptor()
                {
                    WriteMask = CoreRenderStates.Uniforms.stencilWriteMaskGBuffer,
                    Ref = CoreRenderStates.Uniforms.stencilRefGBuffer,
                    Comp = "Always",
                    Pass = "Replace",
                }) },
            };

            public static RenderStateCollection Distortion = new RenderStateCollection
            {
                { RenderState.Blend(Blend.One, Blend.One, Blend.One, Blend.One), new FieldCondition(HDFields.DistortionAdd, true) },
                { RenderState.Blend(Blend.DstColor, Blend.Zero, Blend.DstAlpha, Blend.Zero), new FieldCondition(HDFields.DistortionMultiply, true) },
                { RenderState.Blend(Blend.One, Blend.Zero, Blend.One, Blend.Zero), new FieldCondition(HDFields.DistortionReplace, true) },
                { RenderState.BlendOp(BlendOp.Add, BlendOp.Add) },
                { RenderState.ZWrite(ZWrite.Off) },
                { RenderState.ZTest(ZTest.Always), new FieldCondition(HDFields.DistortionDepthTest, false) },
                { RenderState.ZTest(ZTest.LEqual), new FieldCondition(HDFields.DistortionDepthTest, true) },
                { RenderState.Stencil(new StencilDescriptor()
                {
                    WriteMask = CoreRenderStates.Uniforms.stencilWriteMaskDistortionVec,
                    Ref = CoreRenderStates.Uniforms.stencilRefDistortionVec,
                    Comp = "Always",
                    Pass = "Replace",
                }) },
            };

            public static RenderStateCollection TransparentDepthPrePostPass = new RenderStateCollection
            {
                { RenderState.Blend(Blend.One, Blend.Zero) },
                { RenderState.Cull(CoreRenderStates.Uniforms.cullMode) },
                { RenderState.ZWrite(ZWrite.On) },
                { RenderState.Stencil(new StencilDescriptor()
                {
                    WriteMask = CoreRenderStates.Uniforms.stencilWriteMaskDepth,
                    Ref = CoreRenderStates.Uniforms.stencilRefDepth,
                    Comp = "Always",
                    Pass = "Replace",
                }) },
            };

            public static RenderStateCollection RayTracingPrepass = new RenderStateCollection
            {
                { RenderState.Blend(Blend.One, Blend.Zero) },
                { RenderState.Cull(CoreRenderStates.Uniforms.cullMode) },
                { RenderState.ZWrite(ZWrite.On) },
                // Note: we use default ZTest LEqual so if the object have already been render in depth prepass, it will re-render to tag stencil
            };
        }
#endregion

#region Pragmas
        static class LitPragmas
        {
            public static PragmaCollection RaytracingBasic = new PragmaCollection
            {
                { Pragma.Target(ShaderModel.Target45) },
                { Pragma.Vertex("Vert") },
                { Pragma.Fragment("Frag") },
                { Pragma.OnlyRenderers(new Platform[] {Platform.D3D11}) },
            };
        }
#endregion

#region Defines
        static class LitDefines
        {
            public static DefineCollection RaytracingForwardIndirect = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 1 },
                { CoreKeywordDescriptors.HasLightloop, 1 },
            };

            public static DefineCollection RaytracingGBuffer = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 1 },
            };

            public static DefineCollection RaytracingVisibility = new DefineCollection
            {
                { RayTracingNode.GetRayTracingKeyword(), 1 },
            };

            public static DefineCollection RaytracingPathTracing = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 0 },
            };
        }
#endregion

#region Keywords
        static class LitKeywords
        {
            public static KeywordCollection GBuffer = new KeywordCollection
            {
                { CoreKeywords.HDBase },
                { CoreKeywordDescriptors.DebugDisplay },
                { CoreKeywords.Lightmaps },
                { CoreKeywordDescriptors.ShadowsShadowmask },
                { CoreKeywordDescriptors.LightLayers },
                { CoreKeywordDescriptors.Decals },
            };

            public static KeywordCollection DepthMotionVectors = new KeywordCollection
            {
                { CoreKeywords.HDBase },
                { CoreKeywordDescriptors.WriteMsaaDepth },
                { CoreKeywordDescriptors.WriteNormalBuffer },
                { CoreKeywordDescriptors.AlphaToMask, new FieldCondition(Fields.AlphaToMask, true) },
            };
        }
#endregion

#region Includes
        static class LitIncludes
        {
            const string kLitDecalData = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitDecalData.hlsl";
            const string kPassGBuffer = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassGBuffer.hlsl";
            const string kPassConstant = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassConstant.hlsl";
            
            public static IncludeCollection Common = new IncludeCollection
            {
                { CoreIncludes.CorePregraph },
                { CoreIncludes.kNormalSurfaceGradient, IncludeLocation.Pregraph },
                { CoreIncludes.kLit, IncludeLocation.Pregraph },
                { CoreIncludes.CoreUtility },
                { CoreIncludes.kDecalUtilities, IncludeLocation.Pregraph },
                { kLitDecalData, IncludeLocation.Pregraph },
                { CoreIncludes.kShaderGraphFunctions, IncludeLocation.Pregraph },
            };

            public static IncludeCollection GBuffer = new IncludeCollection
            {
                { Common },
                { kPassGBuffer, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Meta = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kPassLightTransport, IncludeLocation.Postgraph },
            };

            public static IncludeCollection DepthOnly = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kPassDepthOnly, IncludeLocation.Postgraph },
            };

            public static IncludeCollection RayTracingPrepass = new IncludeCollection
            {
                { Common },
                { kPassConstant, IncludeLocation.Postgraph },
            };

            public static IncludeCollection MotionVectors = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kPassMotionVectors, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Forward = new IncludeCollection
            {
                { CoreIncludes.CorePregraph },
                { CoreIncludes.kNormalSurfaceGradient, IncludeLocation.Pregraph },
                { CoreIncludes.kLighting, IncludeLocation.Pregraph },
                { CoreIncludes.kLightLoopDef, IncludeLocation.Pregraph },
                { CoreIncludes.kLit, IncludeLocation.Pregraph },
                { CoreIncludes.kLightLoop, IncludeLocation.Pregraph },
                { CoreIncludes.CoreUtility },
                { CoreIncludes.kDecalUtilities, IncludeLocation.Pregraph },
                { kLitDecalData, IncludeLocation.Pregraph },
                { CoreIncludes.kShaderGraphFunctions, IncludeLocation.Pregraph },
                { CoreIncludes.kPassForward, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Distortion = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kDisortionVectors, IncludeLocation.Postgraph },
            };
        }
#endregion
    }
}
