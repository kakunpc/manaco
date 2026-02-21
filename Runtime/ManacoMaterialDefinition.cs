using UnityEngine;

namespace com.kakunvr.manaco
{
    [CreateAssetMenu(fileName = "NewManacoMaterialDef", menuName = "ちゃとらとりー/Manaco Material Definition")]
    public class ManacoMaterialDefinition : ScriptableObject
    {
        [Tooltip("シェーダーの表示名")]
        public string shaderName = "New Shader";

        [Tooltip("左目用のマテリアル")]
        public Material leftEyeMaterial;

        [Tooltip("右目用のマテリアル")]
        public Material rightEyeMaterial;

        [Tooltip("両目用のマテリアル")]
        public Material bothEyeMaterial;
    }
}
