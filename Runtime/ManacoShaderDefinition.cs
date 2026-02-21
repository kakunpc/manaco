using UnityEngine;

namespace com.kakunvr.manaco
{
    [CreateAssetMenu(fileName = "NewManacoShaderDef", menuName = "ちゃとらとりー/Manaco Shader Definition")]
    public class ManacoShaderDefinition : ScriptableObject
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
