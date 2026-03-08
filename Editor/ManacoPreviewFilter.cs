using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    public class ManacoPreviewFilter : IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            context.Observe(ManacoProjectSettings.DependencyAsset);

            var renderers = new HashSet<Renderer>();
            var comps = context.GetAvatarRoots()
                .SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true));

            foreach (var comp in comps)
            {
                context.Observe(comp);
                if (!comp.useNdmfPreview) continue;

                foreach (var region in comp.eyeRegions)
                {
                    if (region.targetRenderer == null) continue;
                    if (region.eyePolygonRegions == null || region.eyePolygonRegions.Length == 0) continue;

                    bool regionReady;
                    if (comp.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                    {
                        regionReady = region.sourceRenderer != null
                            && region.sourceEyePolygonRegions != null
                            && region.sourceEyePolygonRegions.Length > 0;
                        if (region.sourceRenderer != null)
                            context.Observe(region.sourceRenderer);
                    }
                    else
                    {
                        regionReady = region.customMaterial != null;
                        if (region.customMaterial != null)
                            context.Observe(region.customMaterial);
                    }

                    if (!regionReady) continue;

                    renderers.Add(region.targetRenderer);
                    context.Observe(region.targetRenderer);
                }
            }

            if (renderers.Count == 0) return ImmutableList<RenderGroup>.Empty;
            return ImmutableList.Create(RenderGroup.For(renderers));
        }

        public bool IsEnabled(ComputeContext context)
        {
            context.Observe(ManacoProjectSettings.DependencyAsset);

            var comps = context.GetAvatarRoots()
                .SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true));
            foreach (var comp in comps)
            {
                context.Observe(comp);
                if (comp.useNdmfPreview) return true;
            }
            return false;
        }

        public Task<IRenderFilterNode> Instantiate(
            RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            context.Observe(ManacoProjectSettings.DependencyAsset);

            var comps = context.GetAvatarRoots()
                .SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true))
                .Where(c => c.useNdmfPreview)
                .ToList();
            bool useFastPreview = ManacoProjectSettings.UseFastPreview;

            foreach (var comp in comps)
            {
                context.Observe(comp);
                foreach (var region in comp.eyeRegions)
                {
                    if (comp.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                    {
                        if (region.sourceRenderer != null)
                            context.Observe(region.sourceRenderer);
                    }
                    else
                    {
                        if (region.customMaterial != null)
                            context.Observe(region.customMaterial);
                    }
                }
            }

            var pass = new ManacoPass();
            var node = new ManacoPreviewNode(comps, proxyPairs, pass, useFastPreview);
            return Task.FromResult<IRenderFilterNode>(node);
        }
    }

    public class ManacoPreviewNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        private sealed class RendererModification
        {
            public Mesh OriginalMesh;
            public Material[] OriginalMaterials;
            public Material[] CurrentPreviewMaterials;
            public Manaco.EyeRegion Region;
            public Material EyeMaterial;
            public bool UseFastPreview;
            public Mesh CurrentPreviewMesh;
            public float[] LastBlendShapeWeights;
        }

        private readonly List<Mesh> _createdMeshes = new List<Mesh>();
        private readonly Dictionary<Renderer, RendererModification> _rendererModifications
            = new Dictionary<Renderer, RendererModification>();
        private readonly ManacoPass _pass;

        public ManacoPreviewNode(
            List<Manaco> comps,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ManacoPass pass,
            bool useFastPreview)
        {
            _pass = pass;
            var proxyMap = proxyPairs.ToDictionary(p => p.Item1, p => p.Item2);

            foreach (var comp in comps)
            {
                foreach (var region in comp.eyeRegions)
                {
                    if (region.targetRenderer == null) continue;
                    if (region.eyePolygonRegions == null || region.eyePolygonRegions.Length == 0) continue;

                    // モードに応じて使用するマテリアルを決定（実コンポーネントには書き込まない）
                    Material eyeMat;
                    if (comp.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                    {
                        if (region.sourceRenderer == null) continue;
                        if (region.sourceEyePolygonRegions == null || region.sourceEyePolygonRegions.Length == 0) continue;
                        eyeMat = ManacoEyeCopyProcessor.PrepareEyeCopyMaterial(region);
                        if (eyeMat == null) continue;
                    }
                    else // EyeMaterialAssignment
                    {
                        if (region.customMaterial == null) continue;
                        eyeMat = region.customMaterial;
                    }

                    if (region.targetRenderer is not SkinnedMeshRenderer originalSmr) continue;
                    if (!proxyMap.TryGetValue(region.targetRenderer, out var proxyRenderer)) continue;
                    if (proxyRenderer is not SkinnedMeshRenderer proxySmr) continue;

                    var modification = new RendererModification
                    {
                        OriginalMesh = proxySmr.sharedMesh,
                        OriginalMaterials = proxySmr.sharedMaterials,
                        Region = region,
                        EyeMaterial = eyeMat,
                        UseFastPreview = useFastPreview,
                    };

                    _rendererModifications[region.targetRenderer] = modification;

                    if (useFastPreview)
                        UpdateFastPreviewMesh(originalSmr, proxySmr, modification, force: true);
                    else
                        ApplyStandardPreviewMesh(originalSmr, proxySmr, modification);
                }
            }
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (!_rendererModifications.TryGetValue(original, out var mods)) return;
            if (original is not SkinnedMeshRenderer originalSmr) return;
            if (proxy is SkinnedMeshRenderer proxySmr)
            {
                if (mods.UseFastPreview)
                    UpdateFastPreviewMesh(originalSmr, proxySmr, mods, force: true);
                else
                {
                    proxySmr.sharedMesh = mods.CurrentPreviewMesh ?? mods.OriginalMesh;
                    proxySmr.sharedMaterials = mods.CurrentPreviewMaterials ?? mods.OriginalMaterials;
                    SyncBlendShapeWeights(originalSmr, proxySmr);
                }
            }
        }

        private void ApplyStandardPreviewMesh(
            SkinnedMeshRenderer originalSmr,
            SkinnedMeshRenderer proxySmr,
            RendererModification modification)
        {
            proxySmr.sharedMesh = modification.OriginalMesh;
            proxySmr.sharedMaterials = modification.OriginalMaterials;

            var newMesh = _pass.ApplyEyeSubMesh(
                modification.Region,
                proxySmr,
                modification.EyeMaterial,
                preserveBlendShapes: true);

            if (newMesh != null)
            {
                _createdMeshes.Add(newMesh);
                modification.CurrentPreviewMesh = newMesh;
                modification.CurrentPreviewMaterials = proxySmr.sharedMaterials;
                SyncBlendShapeWeights(originalSmr, proxySmr);
            }
        }

        private void UpdateFastPreviewMesh(
            SkinnedMeshRenderer originalSmr,
            SkinnedMeshRenderer proxySmr,
            RendererModification modification,
            bool force)
        {
            if (!force && !HaveBlendShapeWeightsChanged(originalSmr, modification))
            {
                proxySmr.sharedMesh = modification.CurrentPreviewMesh ?? modification.OriginalMesh;
                proxySmr.sharedMaterials = modification.CurrentPreviewMaterials ?? modification.OriginalMaterials;
                return;
            }

            ReleasePreviewMesh(modification.CurrentPreviewMesh);
            modification.CurrentPreviewMesh = null;
            modification.CurrentPreviewMaterials = null;

            proxySmr.sharedMesh = modification.OriginalMesh;
            proxySmr.sharedMaterials = modification.OriginalMaterials;

            Mesh bakedShapeMesh = null;
            ManacoPass.PreviewMeshSnapshot previewMeshSnapshot = null;
            bool canUseBakedShapeMesh =
                proxySmr.bones != null &&
                proxySmr.bones.Length == 0;

            if (canUseBakedShapeMesh)
            {
                bakedShapeMesh = new Mesh();
                proxySmr.BakeMesh(bakedShapeMesh);
            }
            else
            {
                previewMeshSnapshot = ManacoPass.CaptureBlendShapePreviewSnapshot(proxySmr, originalSmr);
            }

            var newMesh = _pass.ApplyEyeSubMesh(
                modification.Region,
                proxySmr,
                modification.EyeMaterial,
                preserveBlendShapes: false,
                bakedShapeMesh: bakedShapeMesh,
                previewMeshSnapshot: previewMeshSnapshot);

            if (newMesh != null)
            {
                _createdMeshes.Add(newMesh);
                modification.CurrentPreviewMesh = newMesh;
                modification.CurrentPreviewMaterials = proxySmr.sharedMaterials;
                CaptureBlendShapeWeights(originalSmr, modification);
            }

            if (bakedShapeMesh != null)
                Object.DestroyImmediate(bakedShapeMesh);
        }

        private static bool HaveBlendShapeWeightsChanged(
            SkinnedMeshRenderer originalSmr,
            RendererModification modification)
        {
            var mesh = originalSmr.sharedMesh;
            if (mesh == null) return false;

            int count = mesh.blendShapeCount;
            if (count == 0) return modification.LastBlendShapeWeights == null;
            if (modification.LastBlendShapeWeights == null || modification.LastBlendShapeWeights.Length != count)
                return true;

            for (int i = 0; i < count; i++)
            {
                if (!Mathf.Approximately(modification.LastBlendShapeWeights[i], originalSmr.GetBlendShapeWeight(i)))
                    return true;
            }

            return false;
        }

        private static void CaptureBlendShapeWeights(
            SkinnedMeshRenderer originalSmr,
            RendererModification modification)
        {
            var mesh = originalSmr.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0)
            {
                modification.LastBlendShapeWeights = null;
                return;
            }

            int count = mesh.blendShapeCount;
            if (modification.LastBlendShapeWeights == null || modification.LastBlendShapeWeights.Length != count)
                modification.LastBlendShapeWeights = new float[count];

            for (int i = 0; i < count; i++)
                modification.LastBlendShapeWeights[i] = originalSmr.GetBlendShapeWeight(i);
        }

        private void ReleasePreviewMesh(Mesh mesh)
        {
            if (mesh == null) return;
            _createdMeshes.Remove(mesh);
            Object.DestroyImmediate(mesh);
        }

        private static void SyncBlendShapeWeights(SkinnedMeshRenderer originalSmr, SkinnedMeshRenderer proxySmr)
        {
            var originalMesh = originalSmr.sharedMesh;
            var proxyMesh = proxySmr.sharedMesh;
            if (originalMesh == null || proxyMesh == null) return;

            int count = Mathf.Min(originalMesh.blendShapeCount, proxyMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
                proxySmr.SetBlendShapeWeight(i, originalSmr.GetBlendShapeWeight(i));
        }

        public void Dispose()
        {
            foreach (var modification in _rendererModifications.Values)
            {
                modification.CurrentPreviewMesh = null;
                modification.CurrentPreviewMaterials = null;
                modification.LastBlendShapeWeights = null;
            }
            foreach (var mesh in _createdMeshes)
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
            _createdMeshes.Clear();
            _rendererModifications.Clear();
        }
    }
}
