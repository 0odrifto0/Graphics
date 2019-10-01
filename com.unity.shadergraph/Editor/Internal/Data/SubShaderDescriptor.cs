﻿namespace UnityEditor.ShaderGraph.Internal
{
    public struct SubShaderDescriptor
    {
        public string pipelineTag;
        public string renderQueueOverride;
        public string renderTypeOverride;
        public ShaderPass[] passes;
    }
}
