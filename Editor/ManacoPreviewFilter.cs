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
            var comps = context.GetAvatarRoots().SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true));
            foreach (var comp in comps)
            {
                context.Observe(comp);
                if (comp.useNdmfPreview)
                {
                    foreach (var region in comp.eyeRegions)
                    {
                        if (region.targetRenderer != null)
                        {
                            renderers.Add(region.targetRenderer);
                            context.Observe(region.targetRenderer);
                        }
                    }
                }
            }

            if (renderers.Count == 0) return ImmutableList<RenderGroup>.Empty;
            return ImmutableList.Create(RenderGroup.For(renderers));
        }

        public bool IsEnabled(ComputeContext context)
        {
            var comps = context.GetAvatarRoots().SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true));
            foreach (var comp in comps)
            {
                context.Observe(comp);
                if (comp.useNdmfPreview)
                {
                    return true;
                }
            }
            return false;
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var comps = context.GetAvatarRoots()
                .SelectMany(root => context.GetComponentsInChildren<Manaco>(root, true))
                .Where(c => c.useNdmfPreview).ToList();

            var pass = new ManacoPass();
            var node = new ManacoPreviewNode(comps, proxyPairs, pass);
            return Task.FromResult<IRenderFilterNode>(node);
        }
    }

    public class ManacoPreviewNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        private List<Mesh> _createdMeshes = new List<Mesh>();
        private Dictionary<Renderer, (Mesh, Material[])> _rendererModifications = new Dictionary<Renderer, (Mesh, Material[])>();

        public ManacoPreviewNode(List<Manaco> comps, IEnumerable<(Renderer, Renderer)> proxyPairs, ManacoPass pass)
        {
            var proxyMap = proxyPairs.ToDictionary(p => p.Item1, p => p.Item2);

            foreach (var comp in comps)
            {
                foreach (var region in comp.eyeRegions)
                {
                    if (region.targetRenderer != null && proxyMap.TryGetValue(region.targetRenderer, out var proxyRenderer))
                    {
                        if (proxyRenderer is SkinnedMeshRenderer proxySmr)
                        {
                            var newMesh = pass.ApplyEyeSubMesh(region, proxySmr);
                            if (newMesh != null)
                            {
                                _createdMeshes.Add(newMesh);
                                _rendererModifications[region.targetRenderer] = (proxySmr.sharedMesh, proxySmr.sharedMaterials);
                            }
                        }
                    }
                }
            }
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (_rendererModifications.TryGetValue(original, out var mods))
            {
                if (proxy is SkinnedMeshRenderer proxySmr)
                {
                    proxySmr.sharedMesh = mods.Item1;
                    proxySmr.sharedMaterials = mods.Item2;
                }
            }
        }

        public void Dispose()
        {
            foreach (var mesh in _createdMeshes)
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            _createdMeshes.Clear();
            _rendererModifications.Clear();
        }
    }
}
