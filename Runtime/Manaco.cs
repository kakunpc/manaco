using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace com.kakunvr.manaco
{
    /// <summary>
    /// アバターの目のポリゴンに対してカスタムシェーダーを適用するModularAvatarコンポーネント。
    /// 対象のSkinnedMeshRendererと、左目・右目に対応するUV範囲を指定します。
    /// </summary>
    [AddComponentMenu("ちゃとらとりー/Manaco")]
    [DisallowMultipleComponent]
    public class Manaco : MonoBehaviour, IEditorOnly
    {
        public enum EyeType
        {
            Both,
            Left,
            Right
        }

        [Serializable]
        public class UVPolygonRegion
        {
            [Tooltip("この島のUV頂点座標")]
            public Vector2[] uvPoints = Array.Empty<Vector2>();
        }

        [Serializable]
        public class EyeRegion
        {
            [Tooltip("目の対象")]
            public EyeType eyeType = EyeType.Both;

            [Tooltip("目のポリゴンが含まれるSkinnedMeshRenderer")]
            public SkinnedMeshRenderer targetRenderer;

            [Tooltip("目のテクスチャが含まれるマテリアルスロット")]
            public int materialIndex;

            [Tooltip("目のポリゴンを特定するためのUV島")]
            public UVPolygonRegion[] eyePolygonRegions = Array.Empty<UVPolygonRegion>();

            [Tooltip("割り当てるカスタムマテリアル")]
            public Material customMaterial;

            [Tooltip("VRChatセーフティー設定用のフォールバックテクスチャをビルド時に自動生成し _MainTex に設定する")]
            public bool bakeFallbackTexture = true;

            [Tooltip("フォールバックテクスチャの解像度（64〜2048）")]
            public int fallbackTextureResolution = 128;
        }

        [Tooltip("設定する目の領域リスト。SMRごとに追加してください。")]
        public List<EyeRegion> eyeRegions = new List<EyeRegion>();

        [Tooltip("NDMF Previewの有効無効を切り替え")]
        public bool useNdmfPreview = false;

        [HideInInspector]
        public ManacoPreset appliedAvatarPreset;

        [HideInInspector]
        public ManacoShaderDefinition appliedShaderDef;
    }
}
