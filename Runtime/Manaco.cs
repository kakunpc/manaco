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
            Right,
            BothPupil,
            LeftPupil,
            RightPupil
        }

        public enum ManacoMode
        {
            EyeMaterialAssignment,  // 既存: カスタムマテリアル割り当て
            CopyEyeFromAvatar       // 新規: 別アバターの目をコピー
        }

        [Serializable]
        public class UVPolygonRegion
        {
            public Vector2[] uvPoints = Array.Empty<Vector2>();
        }

        [Serializable]
        public class EyeRegion
        {
            [Tooltip("対象の目")]
            public EyeType eyeType = EyeType.Both;

            [Tooltip("目のポリゴンが含まれるSkinnedMeshRenderer")]
            public SkinnedMeshRenderer targetRenderer;

            [Tooltip("目のテクスチャが含まれるマテリアルスロット")]
            public int materialIndex;

            [Tooltip("目のポリゴンを特定するためのUV Island")]
            public UVPolygonRegion[] eyePolygonRegions = Array.Empty<UVPolygonRegion>();

            [Tooltip("割り当てるカスタムマテリアル")]
            public Material customMaterial;

            [Tooltip("VRChatセーフティー設定用のフォールバックテクスチャをビルド時に自動生成し _MainTex に設定する")]
            public bool bakeFallbackTexture = true;

            [Tooltip("フォールバックテクスチャの解像度（64〜2048）")]
            public int fallbackTextureResolution = 128;

            // CopyEyeFromAvatar モード用フィールド
            [Tooltip("コピー元のSkinnedMeshRenderer")]
            public SkinnedMeshRenderer sourceRenderer;

            [Tooltip("コピー元のマテリアルスロット")]
            public int sourceMaterialIndex;

            [Tooltip("コピー元の目のポリゴンを特定するためのUV Island")]
            public UVPolygonRegion[] sourceEyePolygonRegions = Array.Empty<UVPolygonRegion>();

            [Tooltip("抽出テクスチャの解像度（64〜2048）")]
            public int extractTextureResolution = 512;

            [HideInInspector] public ManacoPreset sourcePreset;
            [HideInInspector] public int sourcePresetRegionIndex;
        }

        [Tooltip("動作モード")]
        public ManacoMode mode = ManacoMode.EyeMaterialAssignment;

        [Tooltip("設定する目の領域リスト。SMRごとに追加してください。")]
        public List<EyeRegion> eyeRegions = new List<EyeRegion>();

        [Tooltip("NDMF Previewの有効無効を切り替え")]
        public bool useNdmfPreview = false;

        [HideInInspector]
        public ManacoPreset appliedAvatarPreset;

        [HideInInspector]
        public ManacoMaterialDefinition appliedShaderDef;

        // CopyEyeFromAvatar モード - コンポーネントレベル設定
        [Tooltip("コピー元のアバターのルートGameObject（Prefab可）")]
        public GameObject sourceAvatarPrefab;

        [HideInInspector]
        public ManacoPreset appliedSourceAvatarPreset;
    }
}
