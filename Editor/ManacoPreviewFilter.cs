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
            var renderers = new HashSet<Renderer>();
            var comps = context.GetAvatarRoots()
                .SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true));

            foreach (var comp in comps)
            {
                context.Observe(comp);
                if (!comp.useNdmfPreview) continue;

                foreach (var region in comp.eyeRegions)
                {
                    if (region.targetRenderer != null)
                    {
                        renderers.Add(region.targetRenderer);
                        context.Observe(region.targetRenderer);
                    }

                    if (comp.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                    {
                        // ソース SMR の変化を監視
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

            if (renderers.Count == 0) return ImmutableList<RenderGroup>.Empty;
            return ImmutableList.Create(RenderGroup.For(renderers));
        }

        public bool IsEnabled(ComputeContext context)
        {
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
            var comps = context.GetAvatarRoots()
                .SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true))
                .Where(c => c.useNdmfPreview)
                .ToList();

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
            var node = new ManacoPreviewNode(comps, proxyPairs, pass);
            return Task.FromResult<IRenderFilterNode>(node);
        }
    }

    public class ManacoPreviewNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        private readonly List<Mesh> _createdMeshes = new List<Mesh>();
        private readonly Dictionary<Renderer, (Mesh mesh, Material[] materials)> _rendererModifications
            = new Dictionary<Renderer, (Mesh, Material[])>();

        public ManacoPreviewNode(
            List<Manaco> comps,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ManacoPass pass)
        {
            var proxyMap = proxyPairs.ToDictionary(p => p.Item1, p => p.Item2);

            foreach (var comp in comps)
            {
                foreach (var region in comp.eyeRegions)
                {
                    // CopyEyeFromAvatar モード: ApplyEyeSubMesh の前にマテリアルを生成
                    if (comp.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                        ManacoEyeCopyProcessor.PrepareEyeCopyMaterial(region);

                    if (region.targetRenderer == null) continue;
                    if (!proxyMap.TryGetValue(region.targetRenderer, out var proxyRenderer)) continue;
                    if (proxyRenderer is not SkinnedMeshRenderer proxySmr) continue;

                    var newMesh = pass.ApplyEyeSubMesh(region, proxySmr);
                    if (newMesh != null)
                    {
                        _createdMeshes.Add(newMesh);
                        _rendererModifications[region.targetRenderer] =
                            (proxySmr.sharedMesh, proxySmr.sharedMaterials);
                    }
                }
            }
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (!_rendererModifications.TryGetValue(original, out var mods)) return;
            if (proxy is SkinnedMeshRenderer proxySmr)
            {
                proxySmr.sharedMesh      = mods.mesh;
                proxySmr.sharedMaterials = mods.materials;
            }
        }

        public void Dispose()
        {
            foreach (var mesh in _createdMeshes)
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
            _createdMeshes.Clear();
            _rendererModifications.Clear();
        }
    }
}
