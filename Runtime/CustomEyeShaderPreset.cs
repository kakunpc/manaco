using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chatoratori.CustomEyeShaderCore
{
    [CreateAssetMenu(fileName = "NewCustomEyeShaderPreset", menuName = "ちゃとらとりー/CustomEyeShader Preset")]
    public class CustomEyeShaderPreset : ScriptableObject
    {
        [Serializable]
        public class PresetRegion
        {
            [Tooltip("目の対象")]
            public CustomEyeShaderCore.EyeType eyeType = CustomEyeShaderCore.EyeType.Both;

            [Tooltip("対象のMeshRendererのオブジェクト名")]
            public string targetRendererName;

            [Tooltip("目のテクスチャが含まれるマテリアルスロット")]
            public int materialIndex;

            [Tooltip("目のポリゴンを特定するためのUV島")]
            public CustomEyeShaderCore.UVPolygonRegion[] eyePolygonRegions = Array.Empty<CustomEyeShaderCore.UVPolygonRegion>();
        }

        [Tooltip("アバター名")]
        public string avatarName = "New Avatar";

        [Tooltip("目の領域のプリセットデータ")]
        public List<PresetRegion> regions = new List<PresetRegion>();
    }
}
