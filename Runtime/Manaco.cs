using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace com.kakunvr.manaco
{
    [AddComponentMenu("ちゃんとみる/Manaco")]
    [DisallowMultipleComponent]
    public class Manaco : MonoBehaviour, IEditorOnly
    {
        public enum EyeType
        {
            Left,
            Right,
            LeftPupil,
            RightPupil
        }

        public enum ManacoMode
        {
            EyeMaterialAssignment,
            CopyEyeFromAvatar
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
            public EyeType eyeType = EyeType.Left;

            [Tooltip("目のポリゴンが含まれる SkinnedMeshRenderer")]
            public SkinnedMeshRenderer targetRenderer;

            [Tooltip("目のテクスチャが含まれるマテリアルスロット")]
            public int materialIndex;

            [Tooltip("目のポリゴンを特定するための UV Island")]
            public UVPolygonRegion[] eyePolygonRegions = Array.Empty<UVPolygonRegion>();

            [Tooltip("割り当てるカスタムマテリアル")]
            public Material customMaterial;

            [Tooltip("VRChat セーフティー設定用のフォールバックテクスチャをビルド時に生成して _MainTex に設定する")]
            public bool bakeFallbackTexture = true;

            [Tooltip("フォールバックテクスチャの解像度")]
            public int fallbackTextureResolution = 128;

            [Tooltip("コピー元の SkinnedMeshRenderer")]
            public SkinnedMeshRenderer sourceRenderer;

            [Tooltip("コピー元のマテリアルスロット")]
            public int sourceMaterialIndex;

            [Tooltip("コピー元の目のポリゴンを特定するための UV Island")]
            public UVPolygonRegion[] sourceEyePolygonRegions = Array.Empty<UVPolygonRegion>();

            [Tooltip("抽出テクスチャの解像度")]
            public int extractTextureResolution = 512;

            [HideInInspector] public ManacoPreset sourcePreset;
            [HideInInspector] public int sourcePresetRegionIndex;
        }

        [Tooltip("動作モード")]
        public ManacoMode mode = ManacoMode.EyeMaterialAssignment;

        [Tooltip("設定する目の領域リスト")]
        public List<EyeRegion> eyeRegions = new List<EyeRegion>();

        [Tooltip("NDMF Preview の有効無効を切り替え")]
        public bool useNdmfPreview = false;

        [Tooltip("Bake the eye appearance into the face texture without adding a new material slot.")]
        public bool useLightweightMode = false;

        [Tooltip("Resolution used to render the eye texture for lightweight mode.")]
        public int lightweightTextureResolution = 512;

        [HideInInspector]
        public ManacoPreset appliedAvatarPreset;

        [HideInInspector]
        public ManacoMaterialDefinition appliedShaderDef;

        [Tooltip("コピー元のアバターのルート GameObject")]
        public GameObject sourceAvatarPrefab;

        [HideInInspector]
        public ManacoPreset appliedSourceAvatarPreset;

        [HideInInspector]
        public int tutorialPage;

        [HideInInspector]
        public bool tutorialSkipped;

        [HideInInspector]
        public bool tutorialCompleted;
    }
}
