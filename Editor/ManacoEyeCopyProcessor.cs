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
            var eyeVertexSet = new HashSet<int>();
            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                int i0 = sourceTriangles[i];
                int i1 = sourceTriangles[i + 1];
                int i2 = sourceTriangles[i + 2];

                if (!selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i0])) ||
                    !selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i1])) ||
                    !selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i2])))
                    continue;

                eyeVertexSet.Add(i0);
                eyeVertexSet.Add(i1);
                eyeVertexSet.Add(i2);
            }

            if (eyeVertexSet.Count == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: no eye vertices matched the selected UV island.");
                return null;
            }

            float minU = float.MaxValue;
            float minV = float.MaxValue;
            float maxU = float.MinValue;
            float maxV = float.MinValue;
            foreach (int vertexIndex in eyeVertexSet)
            {
                var uv = uvs[vertexIndex];
                minU = Mathf.Min(minU, uv.x);
                minV = Mathf.Min(minV, uv.y);
                maxU = Mathf.Max(maxU, uv.x);
                maxV = Mathf.Max(maxV, uv.y);
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

                var extracted = ExtractTextureRegion(
                    sourceTexture,
                    minU * scale.x + offset.x,
                    minV * scale.y + offset.y,
                    maxU * scale.x + offset.x,
                    maxV * scale.y + offset.y,
                    resolution);
                if (extracted != null)
                    extractedTextures[propertyName] = extracted;
            }

            Texture mainTexture = null;
            if (extractedTextures.TryGetValue("_MainTex", out var explicitMainTexture))
                mainTexture = explicitMainTexture;
            else if (sourceMaterial.mainTexture != null)
            {
                mainTexture = ExtractTextureRegion(
                    sourceMaterial.mainTexture,
                    minU * sourceMaterial.mainTextureScale.x + sourceMaterial.mainTextureOffset.x,
                    minV * sourceMaterial.mainTextureScale.y + sourceMaterial.mainTextureOffset.y,
                    maxU * sourceMaterial.mainTextureScale.x + sourceMaterial.mainTextureOffset.x,
                    maxV * sourceMaterial.mainTextureScale.y + sourceMaterial.mainTextureOffset.y,
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

        private static Texture2D ExtractTextureRegion(
            Texture source,
            float minU,
            float minV,
            float maxU,
            float maxV,
            int resolution)
        {
            float scaleU = maxU - minU;
            float scaleV = maxV - minV;
            if (Mathf.Approximately(scaleU, 0f) || Mathf.Approximately(scaleV, 0f))
                return null;

            var renderTexture = RenderTexture.GetTemporary(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            renderTexture.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, renderTexture, new Vector2(scaleU, scaleV), new Vector2(minU, minV));

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = source.name + "_EyeExtract"
            };
            texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            texture.Apply(true);

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);

            return texture;
        }
    }
}
