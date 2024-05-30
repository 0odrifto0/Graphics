using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    [Flags]
    enum VFXExpressionContextOption
    {
        None = 0,
        Reduction = 1 << 0,
        CPUEvaluation = 1 << 1,
        ConstantFolding = 1 << 2,
        GPUDataTransformation = 1 << 3,
        PatchReadToEventAttribute = 1 << 4
    }

    abstract partial class VFXExpression
    {
        public class Context
        {
            private bool Has(VFXExpressionContextOption options)
            {
                return (m_ReductionOptions & options) == options;
            }

            private bool HasAny(VFXExpressionContextOption options)
            {
                return (m_ReductionOptions & options) != 0;
            }

            public Context(VFXExpressionContextOption reductionOption, List<VFXLayoutElementDesc> globalEventAttributes = null)
            {
                m_ReductionOptions = reductionOption;
                m_GlobalEventAttribute = globalEventAttributes;

                if (Has(VFXExpressionContextOption.CPUEvaluation) && Has(VFXExpressionContextOption.GPUDataTransformation))
                    throw new ArgumentException("Invalid reduction options");
            }

            public void RegisterExpression(VFXExpression expression, VFXContext sourceContext = null)
            {
                if (!m_EndExpressions.TryGetValue(expression, out var contexts))
                {
                    contexts = new();
                    m_EndExpressions.Add(expression, contexts);
                }

                if (sourceContext != null)
                {
                    if (!contexts.Add(sourceContext))
                        throw new InvalidOperationException("Trying to add twice the same context for the same expression.");
                }
            }

            public void UnregisterExpression(VFXExpression expression)
            {
                Invalidate(expression);
                m_EndExpressions.Remove(expression);
            }

            static readonly ProfilerMarker s_CompileExpressionContext = new ProfilerMarker("VFXEditor.CompileExpressionContext");

            public void Compile()
            {
                using (s_CompileExpressionContext.Auto())
                {
                    bool needToPatch = HasAny(VFXExpressionContextOption.GPUDataTransformation | VFXExpressionContextOption.PatchReadToEventAttribute);
                    var gpuTransformation = needToPatch && Has(VFXExpressionContextOption.GPUDataTransformation);
                    var spawnEventPath = needToPatch && Has(VFXExpressionContextOption.PatchReadToEventAttribute);

                    var collectedData = new CompileCollectedData()
                    {
                        bufferTypeUsages = new(),
                        hlslCodeHolders = new()
                    };

                    foreach (var exp in m_EndExpressions)
                    {
                        Compile(exp.Key, collectedData);
                        if (needToPatch)
                            m_ReducedCache[exp.Key] = PatchVFXExpression(GetReduced(exp.Key), null /* no source in end expression */, gpuTransformation, spawnEventPath, m_GlobalEventAttribute, collectedData);

                        if (collectedData.bufferTypeUsages.Count > 0)
                        {
                            foreach (var context in exp.Value)
                            {
                                if (!m_GraphicsBufferTypeUsagePerContext.TryGetValue(context, out var usages))
                                {
                                    usages = new Dictionary<VFXExpression, BufferUsage>();
                                    m_GraphicsBufferTypeUsagePerContext.Add(context, usages);
                                }

                                foreach (var expressionTypeUsage in collectedData.bufferTypeUsages)
                                {
                                    if (!usages.TryAdd(expressionTypeUsage.Key, expressionTypeUsage.Value) && usages[expressionTypeUsage.Key] != expressionTypeUsage.Value)
                                    {
                                        throw new InvalidOperationException($"Diverging type usage for GraphicsBuffer : {usages[expressionTypeUsage.Key]}, {expressionTypeUsage.Value}");
                                    }
                                }
                            }
                        }
                        collectedData.bufferTypeUsages.Clear();

                        if (collectedData.hlslCodeHolders.Count > 0)
                        {
                            foreach (var context in exp.Value)
                            {
                                if (!m_HLSLCollectionPerContext.TryGetValue(context, out var codeHolders))
                                {
                                    codeHolders = new List<IHLSLCodeHolder>();
                                    m_HLSLCollectionPerContext.Add(context, codeHolders);
                                }
                                codeHolders.AddRange(collectedData.hlslCodeHolders);
                            }
                        }
                        collectedData.hlslCodeHolders.Clear();
                    }
                }
            }

            public void Recompile()
            {
                Invalidate();
                Compile();
            }

            private bool ShouldEvaluate(VFXExpression exp, VFXExpression[] reducedParents)
            {
                if (!HasAny(VFXExpressionContextOption.Reduction | VFXExpressionContextOption.CPUEvaluation | VFXExpressionContextOption.ConstantFolding))
                    return false;

                if (exp.IsAny(Flags.NotCompilableOnCPU))
                    return false;

                if (!Has(VFXExpressionContextOption.CPUEvaluation) && exp.IsAny(Flags.InvalidConstant))
                    return false;

                if (!exp.Is(Flags.Value) && reducedParents.Length == 0) // not a value
                    return false;

                Flags flag = Flags.Value;
                if (!Has(VFXExpressionContextOption.CPUEvaluation))
                    flag |= Has(VFXExpressionContextOption.ConstantFolding) ? Flags.Foldable : Flags.Constant;

                if (exp.Is(Flags.Value) && ((exp.m_Flags & (flag | Flags.InvalidOnCPU)) != flag))
                    return false;

                foreach (var parent in reducedParents)
                {
                    if ((parent.m_Flags & (flag | Flags.InvalidOnCPU)) != flag)
                        return false;
                }

                return true;
            }

            private VFXExpression PatchVFXExpression(VFXExpression input, VFXExpression targetExpression, bool insertGPUTransformation, bool patchReadAttributeForSpawn, IEnumerable<VFXLayoutElementDesc> globalEventAttribute, CompileCollectedData collectedData)
            {
                if (insertGPUTransformation)
                {
                    switch (input.valueType)
                    {
                        case VFXValueType.ColorGradient:
                            input = new VFXExpressionBakeGradient(input);
                            break;
                        case VFXValueType.Curve:
                            input = new VFXExpressionBakeCurve(input);
                            break;

                        case VFXValueType.Mesh:
                        case VFXValueType.SkinnedMeshRenderer:
                            if (targetExpression != null)
                            {
                                if (input.valueType == VFXValueType.Mesh)
                                {
                                    switch (targetExpression.operation)
                                    {
                                        case VFXExpressionOperation.SampleMeshVertexFloat:
                                        case VFXExpressionOperation.SampleMeshVertexFloat2:
                                        case VFXExpressionOperation.SampleMeshVertexFloat3:
                                        case VFXExpressionOperation.SampleMeshVertexFloat4:
                                        case VFXExpressionOperation.SampleMeshVertexColor:
                                            var channelFormatAndDimensionAndStream = targetExpression.parents[2];
                                            channelFormatAndDimensionAndStream = Compile(channelFormatAndDimensionAndStream, collectedData);
                                            if (!(channelFormatAndDimensionAndStream is VFXExpressionMeshChannelInfos))
                                                throw new InvalidOperationException("Unexpected type of expression in mesh sampling : " + channelFormatAndDimensionAndStream);
                                            input = new VFXExpressionVertexBufferFromMesh(input, channelFormatAndDimensionAndStream);
                                            break;
                                        case VFXExpressionOperation.SampleMeshIndex:
                                            input = new VFXExpressionIndexBufferFromMesh(input);
                                            break;
                                        default:
                                            throw new InvalidOperationException("Unexpected source operation for InsertGPUTransformation : " + targetExpression.operation);
                                    }
                                }
                                else //VFXValueType.SkinnedMeshRenderer
                                {
                                    if (targetExpression is IVFXExpressionSampleSkinnedMesh skinnedMeshExpression)
                                    {
                                        var channelFormatAndDimensionAndStream = targetExpression.parents[2];
                                        channelFormatAndDimensionAndStream = Compile(channelFormatAndDimensionAndStream, collectedData);
                                        if (!(channelFormatAndDimensionAndStream is VFXExpressionMeshChannelInfos))
                                            throw new InvalidOperationException("Unexpected type of expression in skinned mesh sampling : " + channelFormatAndDimensionAndStream);
                                        input = new VFXExpressionVertexBufferFromSkinnedMeshRenderer(input, channelFormatAndDimensionAndStream, skinnedMeshExpression.frame);
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Unexpected source operation for InsertGPUTransformation : " + targetExpression);
                                    }
                                }
                            } //else sourceExpression is null, we can't determine usage but it's possible if value is declared but not used.
                            break;

                        default:
                            //Nothing to patch on this type
                            break;
                    }
                }

                if (input.valueType == VFXValueType.Buffer && input is VFXExpressionBufferWithType bufferWithType)
                {
                    input = input.parents[0]; //Explicitly skip NoOp expression
                    if (collectedData.bufferTypeUsages != null)
                    {
                        var usageType = bufferWithType.usage;
                        if (!collectedData.bufferTypeUsages.TryGetValue(input, out var registeredType))
                        {
                            collectedData.bufferTypeUsages.Add(input, usageType);
                        }
                        else if (registeredType != usageType)
                        {
                            throw new InvalidOperationException($"Diverging type usage for GraphicsBuffer : {registeredType}, {usageType}");
                        }
                    }
                }

                if (patchReadAttributeForSpawn && input is VFXAttributeExpression attribute)
                {
                    if (attribute.attributeLocation == VFXAttributeLocation.Current)
                    {
                        if (globalEventAttribute == null)
                            throw new InvalidOperationException("m_GlobalEventAttribute is null");

                        foreach (var layoutDesc in globalEventAttribute)
                        {
                            if (layoutDesc.name == attribute.attributeName)
                            {
                                input = new VFXReadEventAttributeExpression(attribute.attribute, layoutDesc.offset.element);
                                break;
                            }
                        }

                        if (input is not VFXReadEventAttributeExpression)
                            throw new InvalidOperationException("Unable to find " + attribute.attributeName + " in globalEventAttribute");
                    }
                }

                return input;
            }

            public struct CompileCollectedData
            {
                public Dictionary<VFXExpression, BufferUsage> bufferTypeUsages;
                public List<IHLSLCodeHolder> hlslCodeHolders;
            }

            public VFXExpression Compile(VFXExpression expression, CompileCollectedData collectedData = default(CompileCollectedData))
            {
                var gpuTransformation = Has(VFXExpressionContextOption.GPUDataTransformation);
                var patchReadAttributeForSpawn = Has(VFXExpressionContextOption.PatchReadToEventAttribute);

                VFXExpression reduced;
                if (!m_ReducedCache.TryGetValue(expression, out reduced))
                {
                    var parents = new VFXExpression[expression.parents.Length];
                    for (var i = 0; i < expression.parents.Length; i++)
                    {
                        var parent = Compile(expression.parents[i], collectedData);
                        bool currentGPUTransformation = gpuTransformation
                            && expression.IsAny(VFXExpression.Flags.NotCompilableOnCPU)
                            && !parent.IsAny(VFXExpression.Flags.NotCompilableOnCPU);
                        parent = PatchVFXExpression(parent, expression, currentGPUTransformation, patchReadAttributeForSpawn, m_GlobalEventAttribute, collectedData);
                        parents[i] = parent;
                    }

                    if (ShouldEvaluate(expression, parents))
                    {
                        reduced = expression.Evaluate(parents);
                    }
                    else if (HasAny(VFXExpressionContextOption.Reduction | VFXExpressionContextOption.CPUEvaluation | VFXExpressionContextOption.ConstantFolding) || !StructuralComparisons.StructuralEqualityComparer.Equals(parents, expression.parents))
                    {
                        reduced = expression.Reduce(parents);
                    }
                    else
                    {
                        reduced = expression;
                    }

                    if (expression is IHLSLCodeHolder hlslCodeHolder && collectedData.hlslCodeHolders != null)
                    {
                        if (!collectedData.hlslCodeHolders.Contains(hlslCodeHolder))
                            collectedData.hlslCodeHolders.Add(hlslCodeHolder);
                    }
                    m_ReducedCache[expression] = reduced;
                }
                return reduced;
            }

            public void Invalidate()
            {
                m_HLSLCollectionPerContext.Clear();
                m_ReducedCache.Clear();
                m_GraphicsBufferTypeUsagePerContext.Clear();
            }

            public void Invalidate(VFXExpression expression)
            {
                m_ReducedCache.Remove(expression);
            }

            public VFXExpression GetReduced(VFXExpression expression)
            {
                VFXExpression reduced;
                m_ReducedCache.TryGetValue(expression, out reduced);
                return reduced != null ? reduced : expression;
            }

            private void AddReducedGraph(HashSet<VFXExpression> dst, VFXExpression exp)
            {
                if (!dst.Contains(exp))
                {
                    dst.Add(exp);
                    foreach (var parent in exp.parents)
                        AddReducedGraph(dst, parent);
                }
            }

            public HashSet<VFXExpression> BuildAllReduced()
            {
                var reduced = new HashSet<VFXExpression>();
                foreach (var exp in m_EndExpressions)
                    if (m_ReducedCache.ContainsKey(exp.Key))
                        AddReducedGraph(reduced, m_ReducedCache[exp.Key]);
                return reduced;
            }

            public IEnumerable<VFXExpression> RegisteredExpressions => m_EndExpressions.Keys;

            public Dictionary<VFXContext, Dictionary<VFXExpression, BufferUsage>> GraphicsBufferTypeUsagePerContext => m_GraphicsBufferTypeUsagePerContext;

            public Dictionary<VFXContext, List<IHLSLCodeHolder>> hlslCodeHoldersPerContext => m_HLSLCollectionPerContext;

            private Dictionary<VFXExpression, VFXExpression> m_ReducedCache = new ();
            private Dictionary<VFXExpression, HashSet<VFXContext>> m_EndExpressions = new ();
            private Dictionary<VFXContext, Dictionary<VFXExpression, BufferUsage>> m_GraphicsBufferTypeUsagePerContext = new ();

            private IEnumerable<VFXLayoutElementDesc> m_GlobalEventAttribute;
            private VFXExpressionContextOption m_ReductionOptions;
            private readonly Dictionary<VFXContext, List<IHLSLCodeHolder>> m_HLSLCollectionPerContext = new ();
        }
    }
}
