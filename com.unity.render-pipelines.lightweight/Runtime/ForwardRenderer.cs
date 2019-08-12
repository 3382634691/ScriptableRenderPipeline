using System.Text;
using Unity.Collections;

namespace UnityEngine.Rendering.LWRP
{
    internal class ForwardRenderer : ScriptableRenderer
    {
        const int k_DepthStencilBufferBits = 32;
        const string k_CreateCameraTextures = "Create Camera Texture";

        DepthOnlyPass m_DepthPrepass; //+
        MainLightShadowCasterPass m_MainLightShadowCasterPass; //+
        AdditionalLightsShadowCasterPass m_AdditionalLightsShadowCasterPass; //+
        ScreenSpaceShadowResolvePass m_ScreenSpaceShadowResolvePass; //+
        DrawObjectsPass m_RenderOpaqueForwardPass; //+
        PostProcessPass m_OpaquePostProcessPass;
        DrawSkyboxPass m_DrawSkyboxPass; //+
        CopyDepthPass m_CopyDepthPass; //? this
        CopyColorPass m_CopyColorPass; //? and this together, cause of blit - so kinda works, but i don't see a point in these passes as depth/color can simply be sent as input to custom renderpass
        DrawObjectsPass m_RenderTransparentForwardPass; //+
        PostProcessPass m_PostProcessPass; //+
        FinalBlitPass m_FinalBlitPass; //+
        CapturePass m_CapturePass;

#if UNITY_EDITOR
        SceneViewDepthCopyPass m_SceneViewDepthCopyPass;
#endif

        RenderTargetHandle m_ActiveCameraColorAttachment;
        RenderTargetHandle m_ActiveCameraDepthAttachment;
        RenderTargetHandle m_CameraColorAttachment;
        RenderTargetHandle m_CameraDepthAttachment;
        RenderTargetHandle m_DepthTexture;
        RenderTargetHandle m_OpaqueColor;

        private AttachmentDescriptor m_ActiveCameraColorAttachmentDescriptor;
        private AttachmentDescriptor m_ActiveCameraDepthAttachmentDescriptor;

        ForwardLights m_ForwardLights;
        StencilState m_DefaultStencilState;

        public ForwardRenderer(ForwardRendererData data) : base(data)
        {
            Material blitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            Material copyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
            Material samplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);
            Material screenspaceShadowsMaterial = CoreUtils.CreateEngineMaterial(data.shaders.screenSpaceShadowPS);

            StencilStateData stencilData = data.defaultStencilState;
            m_DefaultStencilState = StencilState.defaultValue;
            m_DefaultStencilState.enabled = stencilData.overrideStencilState;
            m_DefaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            m_DefaultStencilState.SetPassOperation(stencilData.passOperation);
            m_DefaultStencilState.SetFailOperation(stencilData.failOperation);
            m_DefaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            // Note: Since all custom render passes inject first and we have stable sort,
            // we inject the builtin passes in the before events.
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrepasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            m_ScreenSpaceShadowResolvePass = new ScreenSpaceShadowResolvePass(RenderPassEvent.BeforeRenderingPrepasses, screenspaceShadowsMaterial);
            m_RenderOpaqueForwardPass = new DrawObjectsPass("Render Opaques", true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            m_CopyDepthPass = new CopyDepthPass(RenderPassEvent.BeforeRenderingOpaques, copyDepthMaterial);
            m_OpaquePostProcessPass = new PostProcessPass(RenderPassEvent.BeforeRenderingOpaques, true);
            m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            m_CopyColorPass = new CopyColorPass(RenderPassEvent.BeforeRenderingTransparents, samplingMaterial);
            m_RenderTransparentForwardPass = new DrawObjectsPass("Render Transparents", false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            m_PostProcessPass = new PostProcessPass(RenderPassEvent.BeforeRenderingPostProcessing);
            m_CapturePass = new CapturePass(RenderPassEvent.AfterRendering);
            m_FinalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering, blitMaterial);

#if UNITY_EDITOR
            m_SceneViewDepthCopyPass = new SceneViewDepthCopyPass(RenderPassEvent.AfterRendering + 9, copyDepthMaterial);
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            m_CameraColorAttachment.Init("_CameraColorTexture");
            m_CameraDepthAttachment.Init("_CameraDepthAttachment");
            m_DepthTexture.Init("_CameraDepthTexture");
            m_OpaqueColor.Init("_CameraOpaqueTexture");
            m_ForwardLights = new ForwardLights();
        }

        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
//            Camera camera = renderingData.cameraData.camera;
//            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

//            // Special path for depth only offscreen cameras. Only write opaques + transparents.
//            bool isOffscreenDepthTexture = camera.targetTexture != null && camera.targetTexture.format == RenderTextureFormat.Depth;
//            if (isOffscreenDepthTexture)
//            {
//                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

//                for (int i = 0; i < rendererFeatures.Count; ++i)
//                    rendererFeatures[i].AddRenderPasses(this, ref renderingData);

//                EnqueuePass(m_RenderOpaqueForwardPass);
//                EnqueuePass(m_DrawSkyboxPass);
//                EnqueuePass(m_RenderTransparentForwardPass);
//                return;
//            }

//            bool mainLightShadows = m_MainLightShadowCasterPass.Setup(ref renderingData);
//            bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);
//            bool resolveShadowsInScreenSpace = mainLightShadows && renderingData.shadowData.requiresScreenSpaceShadowResolve;

//            // Depth prepass is generated in the following cases:
//            // - We resolve shadows in screen space
//            // - Scene view camera always requires a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
//            // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
//            bool requiresDepthPrepass = renderingData.cameraData.isSceneViewCamera ||
//                (renderingData.cameraData.requiresDepthTexture && (!CanCopyDepth(ref renderingData.cameraData)));
//            requiresDepthPrepass |= resolveShadowsInScreenSpace;

//            // TODO: There's an issue in multiview and depth copy pass. Atm forcing a depth prepass on XR until
//            // we have a proper fix.
//            if (renderingData.cameraData.isStereoEnabled && renderingData.cameraData.requiresDepthTexture)
//                requiresDepthPrepass = true;

//            bool createColorTexture = RequiresIntermediateColorTexture(ref renderingData, cameraTargetDescriptor)
//                                      || rendererFeatures.Count != 0;

//            // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read
//            // later by effect requiring it.
//            bool createDepthTexture = renderingData.cameraData.requiresDepthTexture && !requiresDepthPrepass;
//            bool postProcessEnabled = renderingData.cameraData.postProcessEnabled;
//            bool hasOpaquePostProcess = postProcessEnabled &&
//                renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(RenderingUtils.postProcessRenderContext);

//            m_ActiveCameraColorAttachment = (createColorTexture) ? m_CameraColorAttachment : RenderTargetHandle.CameraTarget;
//            m_ActiveCameraDepthAttachment = (createDepthTexture) ? m_CameraDepthAttachment : RenderTargetHandle.CameraTarget;
//            if (createColorTexture || createDepthTexture)
//                CreateCameraRenderTarget(context, ref renderingData.cameraData);
//            ConfigureCameraTarget(m_ActiveCameraColorAttachment.Identifier(), m_ActiveCameraDepthAttachment.Identifier());

//            for (int i = 0; i < rendererFeatures.Count; ++i)
//            {
//                rendererFeatures[i].AddRenderPasses(this, ref renderingData);
//            }

//            int count = activeRenderPassQueue.Count;
//            for (int i = count - 1; i >= 0; i--)
//            {
//                if(activeRenderPassQueue[i] == null)
//                    activeRenderPassQueue.RemoveAt(i);
//            }
//            bool hasAfterRendering = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRendering) != null;

//            if (mainLightShadows)
//                EnqueuePass(m_MainLightShadowCasterPass);

//            if (additionalLightShadows)
//                EnqueuePass(m_AdditionalLightsShadowCasterPass);

//            if (requiresDepthPrepass)
//            {
//                m_DepthPrepass.Setup(cameraTargetDescriptor, m_DepthTexture);
//                EnqueuePass(m_DepthPrepass);
//            }

//            if (resolveShadowsInScreenSpace)
//            {
//                m_ScreenSpaceShadowResolvePass.Setup(cameraTargetDescriptor);
//                EnqueuePass(m_ScreenSpaceShadowResolvePass);
//            }

//            EnqueuePass(m_RenderOpaqueForwardPass);

//            if (hasOpaquePostProcess)
//                m_OpaquePostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);

//            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
//                EnqueuePass(m_DrawSkyboxPass);

//            // If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer
//            if (createDepthTexture)
//            {
//                m_CopyDepthPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);
//                EnqueuePass(m_CopyDepthPass);
//            }

//            if (renderingData.cameraData.requiresOpaqueTexture)
//            {
//                // TODO: Downsampling method should be store in the renderer isntead of in the asset.
//                // We need to migrate this data to renderer. For now, we query the method in the active asset.
//                Downsampling downsamplingMethod = LightweightRenderPipeline.asset.opaqueDownsampling;
//                m_CopyColorPass.Setup(m_ActiveCameraColorAttachment.Identifier(), m_OpaqueColor, downsamplingMethod);
//                EnqueuePass(m_CopyColorPass);
//            }

//            EnqueuePass(m_RenderTransparentForwardPass);

//            bool afterRenderExists = renderingData.cameraData.captureActions != null ||
//                                     hasAfterRendering;

//            // if we have additional filters
//            // we need to stay in a RT
//            if (afterRenderExists)
//            {
//                // perform post with src / dest the same
//                if (postProcessEnabled)
//                {
//                    m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);
//                    EnqueuePass(m_PostProcessPass);
//                }

//                //now blit into the final target
//                if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
//                {
//                    if (renderingData.cameraData.captureActions != null)
//                    {
//                        m_CapturePass.Setup(m_ActiveCameraColorAttachment);
//                        EnqueuePass(m_CapturePass);
//                    }

//                    m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
//                    EnqueuePass(m_FinalBlitPass);
//                }
//            }
//            else
//            {
//                if (postProcessEnabled)
//                {
//                    m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, RenderTargetHandle.CameraTarget);
//                    EnqueuePass(m_PostProcessPass);
//                }
//                else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
//                {
//                    m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
//                    EnqueuePass(m_FinalBlitPass);
//                }
//            }

//#if UNITY_EDITOR
//            if (renderingData.cameraData.isSceneViewCamera)
//            {
//                m_SceneViewDepthCopyPass.Setup(m_DepthTexture);
//                EnqueuePass(m_SceneViewDepthCopyPass);
//            }
//#endif
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            bool mainLightShadows = m_MainLightShadowCasterPass.Setup(ref renderingData);
            bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);
            bool resolveShadowsInScreenSpace = mainLightShadows && renderingData.shadowData.requiresScreenSpaceShadowResolve;

            // Depth prepass is generated in the following cases:
            // - We resolve shadows in screen space
            // - Scene view camera always requires a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
            // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
            bool requiresDepthPrepass = renderingData.cameraData.isSceneViewCamera ||
                (renderingData.cameraData.requiresDepthTexture && (!CanCopyDepth(ref renderingData.cameraData)));
            requiresDepthPrepass |= resolveShadowsInScreenSpace;

            // TODO: There's an issue in multiview and depth copy pass. Atm forcing a depth prepass on XR until
            // we have a proper fix.
            if (renderingData.cameraData.isStereoEnabled && renderingData.cameraData.requiresDepthTexture)
                requiresDepthPrepass = true;

            bool createColorTexture = RequiresIntermediateColorTexture(ref renderingData, cameraTargetDescriptor)
                                      || rendererFeatures.Count != 0;

            // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read
            // later by effect requiring it.
            bool createDepthTexture = renderingData.cameraData.requiresDepthTexture && !requiresDepthPrepass;
            bool postProcessEnabled = renderingData.cameraData.postProcessEnabled;
            bool hasOpaquePostProcess = postProcessEnabled &&
                renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(RenderingUtils.postProcessRenderContext);

            var cmd = CommandBufferPool.Get("");

            m_ActiveCameraColorAttachment = (createColorTexture) ? m_CameraColorAttachment : RenderTargetHandle.CameraTarget;
            m_ActiveCameraDepthAttachment = (createDepthTexture) ? m_CameraDepthAttachment : RenderTargetHandle.CameraTarget;

            m_ActiveCameraColorAttachmentDescriptor = new AttachmentDescriptor(cameraTargetDescriptor.colorFormat);
            m_ActiveCameraDepthAttachmentDescriptor = new AttachmentDescriptor(RenderTextureFormat.Depth);

            if (createColorTexture || createDepthTexture)
            {
                var descriptor = cameraData.cameraTargetDescriptor;
                int msaaSamples = descriptor.msaaSamples;
                if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                {
                    bool useDepthRenderBuffer = m_ActiveCameraDepthAttachment == RenderTargetHandle.CameraTarget;
                    var colorDescriptor = descriptor;
                    colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0;
                    cmd.GetTemporaryRT(m_ActiveCameraColorAttachment.id, colorDescriptor, FilterMode.Bilinear);
                }

                if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
                {
                    var depthDescriptor = descriptor;
                    depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                    depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                    depthDescriptor.bindMS = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
                    cmd.GetTemporaryRT(m_ActiveCameraDepthAttachment.id, depthDescriptor, FilterMode.Point);
                }
            }

            bool clearWithSkybox = (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null);

            if (mainLightShadows)
            {
                m_MainLightShadowCasterPass.Configure(cmd, cameraTargetDescriptor);

                AttachmentDescriptor shadowCasterDepthAttachmentDescriptor =
                    new AttachmentDescriptor(RenderTextureFormat.Shadowmap);
                shadowCasterDepthAttachmentDescriptor.ConfigureTarget(m_MainLightShadowCasterPass.colorAttachment,
                    false, true);
                shadowCasterDepthAttachmentDescriptor.ConfigureClear(Color.white, 1.0f, 0);
                NativeArray<AttachmentDescriptor> shadowCasterDescriptors =
                    new NativeArray<AttachmentDescriptor>(new[] {shadowCasterDepthAttachmentDescriptor},
                        Allocator.Temp);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var shadowMapHeight = (renderingData.shadowData.mainLightShadowCascadesCount == 2)
                    ? renderingData.shadowData.mainLightShadowmapHeight >> 1
                    : renderingData.shadowData.mainLightShadowmapHeight;

                using (context.BeginScopedRenderPass(renderingData.shadowData.mainLightShadowmapWidth, shadowMapHeight,
                    1, shadowCasterDescriptors, 0))
                {
                    shadowCasterDescriptors.Dispose();
                    NativeArray<int> indices = new NativeArray<int>(0, Allocator.Temp);
                    using (context.BeginScopedSubPass(indices))
                    {
                        indices.Dispose();
                        m_MainLightShadowCasterPass.Execute(context, ref renderingData);
                    }
                }
            }

            if (additionalLightShadows)
            {
                var shadowMapHeight = (renderingData.shadowData.mainLightShadowCascadesCount == 2)
                    ? renderingData.shadowData.mainLightShadowmapHeight >> 1
                    : renderingData.shadowData.mainLightShadowmapHeight;

                AttachmentDescriptor additionalLightShadowCasterAttachmentDescriptor =
                    new AttachmentDescriptor(RenderTextureFormat.Shadowmap);
                m_AdditionalLightsShadowCasterPass.Configure(cmd, cameraTargetDescriptor);
                additionalLightShadowCasterAttachmentDescriptor.ConfigureTarget(
                    m_AdditionalLightsShadowCasterPass.colorAttachment, false, true);
                NativeArray<AttachmentDescriptor> shadowCasterDescriptors =
                    new NativeArray<AttachmentDescriptor>(new[] {additionalLightShadowCasterAttachmentDescriptor},
                        Allocator.Temp);

                using (context.BeginScopedRenderPass(renderingData.shadowData.mainLightShadowmapWidth, shadowMapHeight,
                    1, shadowCasterDescriptors, 0))
                {
                    shadowCasterDescriptors.Dispose();
                    NativeArray<int> indices = new NativeArray<int>(1, Allocator.Temp);
                    indices[0] = 0;
                    using (context.BeginScopedSubPass(indices))
                    {
                        indices.Dispose();
                        m_AdditionalLightsShadowCasterPass.Execute(context, ref renderingData);
                    }
                }
            }

            bool stereoEnabled = false;
            context.SetupCameraProperties(camera, stereoEnabled);

            if (requiresDepthPrepass)
            {
                AttachmentDescriptor depthPrepassAttachmentDescriptor =
                    new AttachmentDescriptor(RenderTextureFormat.Depth);
                m_DepthPrepass.Setup(cameraTargetDescriptor, m_DepthTexture);
                m_DepthPrepass.Configure(cmd, cameraTargetDescriptor);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                depthPrepassAttachmentDescriptor.ConfigureTarget(m_DepthPrepass.colorAttachment, false, true);
                NativeArray<AttachmentDescriptor> depthPrepassDescriptors =
                    new NativeArray<AttachmentDescriptor>(new[] {depthPrepassAttachmentDescriptor}, Allocator.Temp);

                using (context.BeginScopedRenderPass(camera.pixelWidth, camera.pixelHeight, 1, depthPrepassDescriptors,
                    0))
                {
                    depthPrepassDescriptors.Dispose();
                    NativeArray<int> indices = new NativeArray<int>(0, Allocator.Temp);
                    using (context.BeginScopedSubPass(indices))
                    {
                        indices.Dispose();
                        m_DepthPrepass.Execute(context, ref renderingData);
                    }

                }
            }


            // Main Rendering
            SetupLights(context, ref renderingData);

            ClearFlag clearFlag = GetCameraClearFlag(camera.clearFlags);

            m_ActiveCameraColorAttachmentDescriptor.ConfigureTarget(m_ActiveCameraColorAttachment.Identifier(), true,
                true);
            m_ActiveCameraColorAttachmentDescriptor.ConfigureClear(Color.yellow, 1.0f, 0);
            m_ActiveCameraDepthAttachmentDescriptor.ConfigureClear(Color.cyan, 1.0f, 0);

            if (renderingData.cameraData.requiresOpaqueTexture)
            {
                Downsampling downsamplingMethod = LightweightRenderPipeline.asset.opaqueDownsampling;
                m_CopyColorPass.Setup(m_ActiveCameraColorAttachment.Identifier(), m_OpaqueColor,
                    downsamplingMethod);
                m_CopyColorPass.Configure(cmd, cameraTargetDescriptor);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            if (createDepthTexture)
            {
                m_CopyDepthPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);
                m_CopyDepthPass.Configure(cmd, cameraTargetDescriptor);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            NativeArray<AttachmentDescriptor> descriptors;

            m_ScreenSpaceShadowResolvePass.Setup(cameraTargetDescriptor);
            m_ScreenSpaceShadowResolvePass.Configure(cmd, cameraTargetDescriptor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            var shadowResolveDescriptor = new AttachmentDescriptor(RenderTextureFormat.R8);

            shadowResolveDescriptor.ConfigureClear(Color.black, 1.0f, 0);
            if (resolveShadowsInScreenSpace)
            {
                descriptors = new NativeArray<AttachmentDescriptor>(
                        new[]
                        {
                            m_ActiveCameraColorAttachmentDescriptor, m_ActiveCameraDepthAttachmentDescriptor,
                            shadowResolveDescriptor
                        },
                        Allocator.Temp);
            }
            else
            {
                descriptors = new NativeArray<AttachmentDescriptor>(
                        new[] {m_ActiveCameraColorAttachmentDescriptor, m_ActiveCameraDepthAttachmentDescriptor},
                        Allocator.Temp);
            }



        if (postProcessEnabled)
                m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, RenderTargetHandle.CameraTarget);
            else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);

        if (resolveShadowsInScreenSpace)
        {
            using (context.BeginScopedRenderPass(camera.pixelWidth, camera.pixelHeight,
                cameraData.cameraTargetDescriptor.msaaSamples, descriptors, 1))
            {
                descriptors.Dispose();
                {
                    NativeArray<int> shadowResolveIndices = new NativeArray<int>(new[] {2}, Allocator.Temp);
                    using (context.BeginScopedSubPass(shadowResolveIndices))
                    {
                        m_ScreenSpaceShadowResolvePass.Execute(context, ref renderingData);
                    }

                    NativeArray<int> indices = new NativeArray<int>(new[] {0}, Allocator.Temp);
                    using (context.BeginScopedSubPass(indices, shadowResolveIndices))
                    {
                        m_RenderOpaqueForwardPass.Execute(context, ref renderingData);
                        if (clearWithSkybox)
                            m_DrawSkyboxPass.Execute(context, ref renderingData);
                    }
                }
            }
            if (createDepthTexture)
                m_CopyDepthPass.Execute(context, ref renderingData);
            if (renderingData.cameraData.requiresOpaqueTexture)
                m_CopyColorPass.Execute(context, ref renderingData);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            m_ActiveCameraColorAttachmentDescriptor.loadAction = RenderBufferLoadAction.Load;
            m_ActiveCameraDepthAttachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;

                descriptors = new NativeArray<AttachmentDescriptor>(
                    new[]
                    {
                        m_ActiveCameraColorAttachmentDescriptor, m_ActiveCameraDepthAttachmentDescriptor,
                        shadowResolveDescriptor
                    }, Allocator.Temp);
                using (context.BeginScopedRenderPass(camera.pixelWidth, camera.pixelHeight,
                    cameraData.cameraTargetDescriptor.msaaSamples, descriptors, 1))
                {

                    NativeArray<int> shadowResolveIndices = new NativeArray<int>(new[] {2}, Allocator.Temp);

                    NativeArray<int> inputs = new NativeArray<int>(new[] {0}, Allocator.Temp);
                    using (context.BeginScopedSubPass(inputs, shadowResolveIndices))
                    {
                        m_RenderTransparentForwardPass.Execute(context, ref renderingData);
                        DrawGizmos(context, camera, GizmoSubset.PreImageEffects);
                        if (postProcessEnabled)
                        {
                            m_PostProcessPass.Execute(context, ref renderingData);

                        }
                        else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                            m_FinalBlitPass.Execute(context, ref renderingData);
                        DrawGizmos(context, camera, GizmoSubset.PostImageEffects);
                    }
                }
            }
        else
                {
                    using (context.BeginScopedRenderPass(camera.pixelWidth, camera.pixelHeight,
                        cameraData.cameraTargetDescriptor.msaaSamples, descriptors, 1))
                    {
                        NativeArray<int> indices = new NativeArray<int>(new[] {0}, Allocator.Temp);
                        using (context.BeginScopedSubPass(indices))
                        {
                            indices.Dispose();
                            m_RenderOpaqueForwardPass.Execute(context, ref renderingData);
                            if (clearWithSkybox)
                                m_DrawSkyboxPass.Execute(context, ref renderingData);

                            m_RenderTransparentForwardPass.Execute(context, ref renderingData);
                            DrawGizmos(context, camera, GizmoSubset.PreImageEffects);
                            if (postProcessEnabled)
                                m_PostProcessPass.Execute(context, ref renderingData);
                            else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                                m_FinalBlitPass.Execute(context, ref renderingData);
                            DrawGizmos(context, camera, GizmoSubset.PostImageEffects);
                        }
                    }
                }


#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                m_SceneViewDepthCopyPass.Setup(m_DepthTexture);
                m_SceneViewDepthCopyPass.Execute(context, ref renderingData);
            }
#endif

            // Release Resources
            m_MainLightShadowCasterPass.FrameCleanup(cmd);
            m_AdditionalLightsShadowCasterPass.FrameCleanup(cmd);
            m_DepthPrepass.FrameCleanup(cmd);
            m_ScreenSpaceShadowResolvePass.FrameCleanup(cmd);
            m_RenderOpaqueForwardPass.FrameCleanup(cmd);
            m_DrawSkyboxPass.FrameCleanup(cmd);
            m_RenderTransparentForwardPass.FrameCleanup(cmd);
            m_PostProcessPass.FrameCleanup(cmd);
            m_FinalBlitPass.FrameCleanup(cmd);
            #if UNITY_EDITOR
            m_SceneViewDepthCopyPass.FrameCleanup(cmd);
            #endif
            FinishRendering(cmd);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_ForwardLights.Setup(context, ref renderingData);
        }

        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {
            Camera camera = cameraData.camera;
            // TODO: PerObjectCulling also affect reflection probes. Enabling it for now.
            // if (asset.additionalLightsRenderingMode == LightRenderingMode.Disabled ||
            //     asset.maxAdditionalLightsCount == 0)
            // {
            //     cullingParameters.cullingOptions |= CullingOptions.DisablePerObjectCulling;
            // }

            cullingParameters.shadowDistance = Mathf.Min(cameraData.maxShadowDistance, camera.farClipPlane);
        }

        public override void FinishRendering(CommandBuffer cmd)
        {
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                cmd.ReleaseTemporaryRT(m_ActiveCameraColorAttachment.id);

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
                cmd.ReleaseTemporaryRT(m_ActiveCameraDepthAttachment.id);
        }

        void CreateCameraRenderTarget(ScriptableRenderContext context, ref CameraData cameraData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_CreateCameraTextures);
            var descriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = descriptor.msaaSamples;
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                bool useDepthRenderBuffer = m_ActiveCameraDepthAttachment == RenderTargetHandle.CameraTarget;
                var colorDescriptor = descriptor;
                colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0;
                cmd.GetTemporaryRT(m_ActiveCameraColorAttachment.id, colorDescriptor, FilterMode.Bilinear);
            }

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                var depthDescriptor = descriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                depthDescriptor.bindMS = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
                cmd.GetTemporaryRT(m_ActiveCameraDepthAttachment.id, depthDescriptor, FilterMode.Point);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        bool RequiresIntermediateColorTexture(ref RenderingData renderingData, RenderTextureDescriptor baseDescriptor)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            int msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;
            bool isStereoEnabled = renderingData.cameraData.isStereoEnabled;
            bool isScaledRender = !Mathf.Approximately(cameraData.renderScale, 1.0f);
            bool isCompatibleBackbufferTextureDimension = baseDescriptor.dimension == TextureDimension.Tex2D;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve;
            bool isOffscreenRender = cameraData.camera.targetTexture != null && !cameraData.isSceneViewCamera;
            bool isCapturing = cameraData.captureActions != null;

            if (isStereoEnabled)
                isCompatibleBackbufferTextureDimension = UnityEngine.XR.XRSettings.deviceEyeTextureDimension == baseDescriptor.dimension;

            bool requiresBlitForOffscreenCamera = cameraData.postProcessEnabled || cameraData.requiresOpaqueTexture || requiresExplicitMsaaResolve;
            if (isOffscreenRender)
                return requiresBlitForOffscreenCamera;
            
            return requiresBlitForOffscreenCamera || cameraData.isSceneViewCamera || isScaledRender || cameraData.isHdrEnabled ||
                   !isCompatibleBackbufferTextureDimension || !cameraData.isDefaultViewport || isCapturing || Display.main.requiresBlitToBackbuffer
                   || (renderingData.killAlphaInFinalBlit && !isStereoEnabled);
        }

        bool CanCopyDepth(ref CameraData cameraData)
        {
            bool msaaEnabledForCamera = cameraData.cameraTargetDescriptor.msaaSamples > 1;
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            bool supportsDepthTarget = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            // TODO:  We don't have support to highp Texture2DMS currently and this breaks depth precision.
            // currently disabling it until shader changes kick in.
            //bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;
            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }
    }
}
