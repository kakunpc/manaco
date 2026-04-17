using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    internal static class ManacoLightweightUtility
    {
        internal static bool ApplyLightweightMaterial(
            Manaco.EyeRegion region,
            SkinnedMeshRenderer smr,
            Material eyeMaterial,
            Dictionary<(SkinnedMeshRenderer renderer, int materialIndex), Material> materialCache,
            ICollection<UnityEngine.Object> createdObjects = null)
        {
            if (smr == null || eyeMaterial == null)
                return false;

            var materials = smr.sharedMaterials.ToArray();
            if (materials.Length == 0)
                return false;

            int materialIndex = Mathf.Clamp(region.materialIndex, 0, materials.Length - 1);
            var cacheKey = (smr, materialIndex);
            var baseMaterial = materialCache.TryGetValue(cacheKey, out var cachedMaterial)
                ? cachedMaterial
                : materials[materialIndex];
            if (baseMaterial == null)
                return false;

            var compositedTexture = CreateCompositedMainTexture(region, smr.sharedMesh, baseMaterial, eyeMaterial);
            if (compositedTexture == null)
                return false;

            if (cachedMaterial == null)
            {
                cachedMaterial = new Material(baseMaterial)
                {
                    name = baseMaterial.name + "_ManacoLightweight"
                };
                materialCache[cacheKey] = cachedMaterial;
                createdObjects?.Add(cachedMaterial);
            }

            cachedMaterial.SetTexture("_MainTex", compositedTexture);
            materials[materialIndex] = cachedMaterial;
            smr.sharedMaterials = materials;
            createdObjects?.Add(compositedTexture);
            return true;
        }

        internal static Texture2D CreateCompositedMainTexture(
            Manaco.EyeRegion region,
            Mesh mesh,
            Material targetMaterial,
            Material eyeMaterial)
        {
            if (mesh == null || targetMaterial == null || eyeMaterial == null)
                return null;
            if (!targetMaterial.HasProperty("_MainTex"))
                return null;

            var baseTexture = targetMaterial.GetTexture("_MainTex");
            return CreateCompositedMainTexture(region, mesh, targetMaterial, baseTexture, eyeMaterial);
        }

        internal static Texture2D CreateCompositedMainTexture(
            Manaco.EyeRegion region,
            Mesh mesh,
            Material targetMaterial,
            Texture baseTexture,
            Material eyeMaterial)
        {
            if (mesh == null || targetMaterial == null || baseTexture == null || eyeMaterial == null)
                return null;

            var eyeTexture = eyeMaterial.HasProperty("_MainTex")
                ? eyeMaterial.GetTexture("_MainTex")
                : eyeMaterial.mainTexture;
            if (baseTexture == null || eyeTexture == null)
                return null;

            var selectedTriangles = CollectSelectedTriangles(region, mesh, out var transformedBounds, targetMaterial);
            if (selectedTriangles.Count == 0)
                return null;

            var readableBase = ReadTexture(baseTexture, baseTexture.name + "_Readable");
            var readableEye = ReadTexture(eyeTexture, eyeTexture.name + "_Readable");
            if (readableBase == null || readableEye == null)
            {
                if (readableBase != null) Object.DestroyImmediate(readableBase);
                if (readableEye != null) Object.DestroyImmediate(readableEye);
                return null;
            }

            var output = new Texture2D(readableBase.width, readableBase.height, TextureFormat.RGBA32, true)
            {
                name = baseTexture.name + "_ManacoLightweight"
            };

            var basePixels = readableBase.GetPixels();
            var eyePixels = readableEye.GetPixels();
            var baseWidth = readableBase.width;
            var baseHeight = readableBase.height;
            var eyeWidth = readableEye.width;
            var eyeHeight = readableEye.height;

            foreach (var triangle in selectedTriangles)
            {
                RasterizeTriangle(
                    basePixels,
                    baseWidth,
                    baseHeight,
                    eyePixels,
                    eyeWidth,
                    eyeHeight,
                    triangle,
                    transformedBounds);
            }

            output.SetPixels(basePixels);
            output.Apply(true, false);

            Object.DestroyImmediate(readableBase);
            Object.DestroyImmediate(readableEye);
            return output;
        }

        internal static Texture2D ReadMainTextureCopy(Material material, string name)
        {
            if (material == null || !material.HasProperty("_MainTex"))
                return null;

            return ReadTexture(material.GetTexture("_MainTex"), name);
        }

        private static List<Vector2[]> CollectSelectedTriangles(
            Manaco.EyeRegion region,
            Mesh mesh,
            out Rect transformedBounds,
            Material targetMaterial)
        {
            transformedBounds = default;
            var triangles = new List<Vector2[]>();
            if (mesh == null)
                return triangles;

            var uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0 || mesh.subMeshCount == 0)
                return triangles;

            var selectedUVPoints = new HashSet<long>();
            if (region.eyePolygonRegions != null)
            {
                foreach (var polygon in region.eyePolygonRegions)
                {
                    if (polygon.uvPoints == null)
                        continue;

                    foreach (var point in polygon.uvPoints)
                        selectedUVPoints.Add(ManacoPass.QuantizeUV(point));
                }
            }

            if (selectedUVPoints.Count == 0)
                return triangles;

            int subMeshIndex = Mathf.Clamp(region.materialIndex, 0, mesh.subMeshCount - 1);
            var meshTriangles = mesh.GetTriangles(subMeshIndex);
            var textureScale = targetMaterial.GetTextureScale("_MainTex");
            var textureOffset = targetMaterial.GetTextureOffset("_MainTex");

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < meshTriangles.Length; i += 3)
            {
                int i0 = meshTriangles[i];
                int i1 = meshTriangles[i + 1];
                int i2 = meshTriangles[i + 2];
                if (!selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i0])) ||
                    !selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i1])) ||
                    !selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i2])))
                    continue;

                var uv0 = TransformUv(uvs[i0], textureScale, textureOffset);
                var uv1 = TransformUv(uvs[i1], textureScale, textureOffset);
                var uv2 = TransformUv(uvs[i2], textureScale, textureOffset);
                triangles.Add(new[] { uv0, uv1, uv2 });

                minX = Mathf.Min(minX, uv0.x, uv1.x, uv2.x);
                minY = Mathf.Min(minY, uv0.y, uv1.y, uv2.y);
                maxX = Mathf.Max(maxX, uv0.x, uv1.x, uv2.x);
                maxY = Mathf.Max(maxY, uv0.y, uv1.y, uv2.y);
            }

            if (triangles.Count == 0)
                return triangles;

            transformedBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return triangles;
        }

        private static Vector2 TransformUv(Vector2 uv, Vector2 scale, Vector2 offset)
        {
            return new Vector2(uv.x * scale.x + offset.x, uv.y * scale.y + offset.y);
        }

        private static Texture2D ReadTexture(Texture source, string name)
        {
            if (source == null)
                return null;

            var rt = RenderTexture.GetTemporary(
                Mathf.Max(1, source.width),
                Mathf.Max(1, source.height),
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, rt);

            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false)
            {
                name = name
            };
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply(false, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return texture;
        }

        private static void RasterizeTriangle(
            Color[] basePixels,
            int baseWidth,
            int baseHeight,
            Color[] eyePixels,
            int eyeWidth,
            int eyeHeight,
            IReadOnlyList<Vector2> triangle,
            Rect bounds)
        {
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;

            var p0 = new Vector2(triangle[0].x * (baseWidth - 1), triangle[0].y * (baseHeight - 1));
            var p1 = new Vector2(triangle[1].x * (baseWidth - 1), triangle[1].y * (baseHeight - 1));
            var p2 = new Vector2(triangle[2].x * (baseWidth - 1), triangle[2].y * (baseHeight - 1));

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, baseWidth - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, baseHeight - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, baseWidth - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, baseHeight - 1);

            float area = Edge(p0, p1, p2);
            if (Mathf.Approximately(area, 0f))
                return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(p1, p2, point);
                    float w1 = Edge(p2, p0, point);
                    float w2 = Edge(p0, p1, point);

                    bool inside = area > 0f
                        ? (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                        : (w0 <= 0f && w1 <= 0f && w2 <= 0f);
                    if (!inside)
                        continue;

                    var uv = new Vector2(
                        Mathf.Lerp(0f, 1f, x / (float)Mathf.Max(1, baseWidth - 1)),
                        Mathf.Lerp(0f, 1f, y / (float)Mathf.Max(1, baseHeight - 1)));
                    var eyeUv = new Vector2(
                        Mathf.InverseLerp(bounds.xMin, bounds.xMax, uv.x),
                        Mathf.InverseLerp(bounds.yMin, bounds.yMax, uv.y));
                    var eyeColor = SampleBilinear(eyePixels, eyeWidth, eyeHeight, eyeUv);

                    int pixelIndex = y * baseWidth + x;
                    var baseColor = basePixels[pixelIndex];
                    basePixels[pixelIndex] = Color.Lerp(baseColor, eyeColor, eyeColor.a);
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 c)
        {
            return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
        }

        private static Color SampleBilinear(Color[] pixels, int width, int height, Vector2 uv)
        {
            float x = Mathf.Clamp01(uv.x) * (width - 1);
            float y = Mathf.Clamp01(uv.y) * (height - 1);

            int xMin = Mathf.FloorToInt(x);
            int yMin = Mathf.FloorToInt(y);
            int xMax = Mathf.Min(xMin + 1, width - 1);
            int yMax = Mathf.Min(yMin + 1, height - 1);

            float tx = x - xMin;
            float ty = y - yMin;

            var c00 = pixels[yMin * width + xMin];
            var c10 = pixels[yMin * width + xMax];
            var c01 = pixels[yMax * width + xMin];
            var c11 = pixels[yMax * width + xMax];

            var bottom = Color.Lerp(c00, c10, tx);
            var top = Color.Lerp(c01, c11, tx);
            return Color.Lerp(bottom, top, ty);
        }
    }
}
