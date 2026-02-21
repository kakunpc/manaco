using UnityEngine;

namespace com.kakunvr.manaco
{
    [CreateAssetMenu(fileName = "NewCustomEyeShaderDef", menuName = "ちゃとらとりー/Custom Eye Shader Definition")]
    public class CustomEyeShaderDefinition : ScriptableObject
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
