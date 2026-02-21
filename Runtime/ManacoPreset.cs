using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.kakunvr.manaco
{
    public class ManacoPreset : ScriptableObject
    {
        [Serializable]
        public class PresetRegion
        {
            [Tooltip("目の対象")]
            public Manaco.EyeType eyeType = Manaco.EyeType.Both;

            [Tooltip("対象のMeshRendererのオブジェクト名")]
            public string targetRendererName;

            [Tooltip("目のテクスチャが含まれるマテリアルスロット")]
            public int materialIndex;

            [Tooltip("目のポリゴンを特定するためのUV Island")]
            public Manaco.UVPolygonRegion[] eyePolygonRegions = Array.Empty<Manaco.UVPolygonRegion>();
        }

        [Tooltip("アバター名")]
        public string avatarName = "New Avatar";

        [Tooltip("目の領域のプリセットデータ")]
        public List<PresetRegion> regions = new List<PresetRegion>();
    }
}
