using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// CopyEyeFromAvatar モード用のテクスチャ抽出処理。
    /// ソース目領域を切り出し、軽量化合成で扱いやすい _MainTex マテリアルを返す。
    /// </summary>
    public static class ManacoEyeCopyProcessor
    {
        /// <summary>
        /// CopyEyeFromAvatar モード用のマテリアルを生成して返す。
        /// 返却されるマテリアルは Unlit ベースで、抽出済みの _MainTex を持つ。
        /// </summary>
        public static Material PrepareEyeCopyMaterial(Manaco.EyeRegion region)
        {
            if (region.sourceRenderer == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer is missing. Skipping.");
                return null;
            }

            var sourceMesh = region.sourceRenderer.sharedMesh;
            if (sourceMesh == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer.sharedMesh is null. Skipping.");
                return null;
            }

            var sourceMaterials = region.sourceRenderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer has no materials. Skipping.");
                return null;
            }

            int materialIndex = Mathf.Clamp(region.sourceMaterialIndex, 0, sourceMaterials.Length - 1);
            var sourceMaterial = sourceMaterials[materialIndex];
            if (sourceMaterial == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: source material is null. Skipping.");
                return null;
            }

            if (region.sourceEyePolygonRegions == null || region.sourceEyePolygonRegions.Length == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceEyePolygonRegions is empty. Skipping.");
                return null;
            }

            var selectedUVPoints = new HashSet<long>();
            foreach (var polygon in region.sourceEyePolygonRegions)
            {
                if (polygon.uvPoints == null)
                    continue;

                foreach (var point in polygon.uvPoints)
                    selectedUVPoints.Add(ManacoPass.QuantizeUV(point));
            }

            var uvs = sourceMesh.uv;
            if (uvs == null || uvs.Length == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: source mesh has no UVs. Skipping.");
                return null;
            }

            int sourceSubMeshIndex = Mathf.Clamp(region.sourceMaterialIndex, 0, sourceMesh.subMeshCount - 1);
            var sourceTriangles = sourceMesh.GetTriangles(sourceSubMeshIndex);
            var sourceEyeTriangles = new List<int>();
            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                int i0 = sourceTriangles[i];
                int i1 = sourceTriangles[i + 1];
                int i2 = sourceTriangles[i + 2];

                if (!selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i0])) ||
                    !selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i1])) ||
                    !selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i2])))
                    continue;

                sourceEyeTriangles.Add(i0);
                sourceEyeTriangles.Add(i1);
                sourceEyeTriangles.Add(i2);
            }

            if (sourceEyeTriangles.Count == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: no eye vertices matched the selected UV island.");
                return null;
            }

            int resolution = Mathf.Clamp(region.extractTextureResolution, 64, 2048);
            var extractedTextures = new Dictionary<string, Texture2D>();
            var texturePropertyNames = sourceMaterial.GetTexturePropertyNames();

            foreach (var propertyName in texturePropertyNames)
            {
                var sourceTexture = sourceMaterial.GetTexture(propertyName);
                if (sourceTexture == null)
                    continue;

                Vector2 scale = Vector2.one;
                Vector2 offset = Vector2.zero;
                if (sourceMaterial.HasProperty(propertyName + "_ST"))
                {
                    var st = sourceMaterial.GetVector(propertyName + "_ST");
                    scale = new Vector2(st.x, st.y);
                    offset = new Vector2(st.z, st.w);
                }
                else if (propertyName == "_MainTex")
                {
                    scale = sourceMaterial.mainTextureScale;
                    offset = sourceMaterial.mainTextureOffset;
                }

                var extracted = ExtractCircularTextureRegion(
                    sourceTexture,
                    uvs,
                    sourceEyeTriangles,
                    scale,
                    offset,
                    resolution);
                if (extracted != null)
                    extractedTextures[propertyName] = extracted;
            }

            Texture mainTexture = null;
            if (extractedTextures.TryGetValue("_MainTex", out var explicitMainTexture))
                mainTexture = explicitMainTexture;
            else if (sourceMaterial.mainTexture != null)
            {
                mainTexture = ExtractCircularTextureRegion(
                    sourceMaterial.mainTexture,
                    uvs,
                    sourceEyeTriangles,
                    sourceMaterial.mainTextureScale,
                    sourceMaterial.mainTextureOffset,
                    resolution);
            }

            mainTexture ??= extractedTextures.Values.FirstOrDefault();
            if (mainTexture == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: no extractable texture was found on the source material.");
                return null;
            }

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: failed to find an unlit shader.");
                return null;
            }

            var result = new Material(shader)
            {
                name = sourceMaterial.name + "_ManacoCopy"
            };
            result.SetTexture("_MainTex", mainTexture);

            if (sourceMaterial.HasProperty("_Color"))
                result.color = sourceMaterial.color;

            return result;
        }

        private static Texture2D ExtractCircularTextureRegion(
            Texture source,
            Vector2[] sourceUvs,
            IReadOnlyList<int> sourceEyeTriangles,
            Vector2 scale,
            Vector2 offset,
            int resolution)
        {
            if (source == null || sourceUvs == null || sourceEyeTriangles == null || sourceEyeTriangles.Count == 0)
                return null;

            var transformedUvs = new Vector2[sourceUvs.Length];
            for (int i = 0; i < sourceUvs.Length; i++)
                transformedUvs[i] = new Vector2(sourceUvs[i].x * scale.x + offset.x, sourceUvs[i].y * scale.y + offset.y);

            var mapping = ManacoPass.BuildCircularUvMapping(transformedUvs, sourceEyeTriangles);
            if (mapping.Triangles.Count == 0)
                return null;

            var readableSource = ReadTexture(source, source.name + "_Readable");
            if (readableSource == null)
                return null;

            var sourcePixels = readableSource.GetPixels();
            var outputPixels = Enumerable.Repeat(new Color(0f, 0f, 0f, 0f), resolution * resolution).ToArray();
            foreach (var triangle in mapping.Triangles)
                RasterizeExtractedTriangle(outputPixels, resolution, sourcePixels, readableSource.width, readableSource.height, triangle, source.wrapMode);

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = source.name + "_EyeExtract"
            };
            texture.SetPixels(outputPixels);
            texture.Apply(true);

            Object.DestroyImmediate(readableSource);
            return texture;
        }

        private static Texture2D ReadTexture(Texture source, string name)
        {
            if (source == null)
                return null;

            var renderTexture = RenderTexture.GetTemporary(
                Mathf.Max(1, source.width),
                Mathf.Max(1, source.height),
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            renderTexture.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, renderTexture);

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false)
            {
                name = name
            };
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply(false, false);

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);

            return texture;
        }

        private static void RasterizeExtractedTriangle(
            Color[] outputPixels,
            int outputResolution,
            Color[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            ManacoPass.CircularUvTriangle triangle,
            TextureWrapMode wrapMode)
        {
            var p0 = new Vector2(triangle.RemappedUv0.x * (outputResolution - 1), triangle.RemappedUv0.y * (outputResolution - 1));
            var p1 = new Vector2(triangle.RemappedUv1.x * (outputResolution - 1), triangle.RemappedUv1.y * (outputResolution - 1));
            var p2 = new Vector2(triangle.RemappedUv2.x * (outputResolution - 1), triangle.RemappedUv2.y * (outputResolution - 1));

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, outputResolution - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, outputResolution - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, outputResolution - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, outputResolution - 1);

            float area = Edge(p0, p1, p2);
            if (Mathf.Approximately(area, 0f))
                return;
            float inverseArea = 1f / area;

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

                    var sourceUv = triangle.InterpolateOriginal(w0, w1, w2, inverseArea);
                    outputPixels[y * outputResolution + x] = SampleBilinear(sourcePixels, sourceWidth, sourceHeight, sourceUv, wrapMode);
                }
            }
        }

        private static Color SampleBilinear(Color[] pixels, int width, int height, Vector2 uv, TextureWrapMode wrapMode)
        {
            float x = WrapUv(uv.x, wrapMode) * (width - 1);
            float y = WrapUv(uv.y, wrapMode) * (height - 1);

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

        private static float WrapUv(float value, TextureWrapMode wrapMode)
        {
            return wrapMode switch
            {
                TextureWrapMode.Repeat => Mathf.Repeat(value, 1f),
                TextureWrapMode.Mirror => Mirror(value),
                TextureWrapMode.MirrorOnce => Mathf.Clamp01(Mirror(value)),
                _ => Mathf.Clamp01(value),
            };
        }

        private static float Mirror(float value)
        {
            float repeated = Mathf.Repeat(value, 2f);
            return repeated <= 1f ? repeated : 2f - repeated;
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 c)
        {
            return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
        }
    }
}
