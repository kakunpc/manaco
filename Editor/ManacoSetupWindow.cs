using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    public static class ManacoSetup
    {
        [MenuItem("GameObject/ちゃとらとりー/Manaco(まなこ)", false, 0)]
        private static void CreateManacoObject(MenuCommand command)
        {
            var parent = command.context as GameObject;
            if (parent == null)
                parent = Selection.activeGameObject;
            
            // 既にManacoのゲームオブジェクトがセットアップされているものがないか探す
            var manacoComponent =  parent.GetComponentInChildren<Manaco>();
            var eyeObj = manacoComponent != null ? manacoComponent.gameObject : null;

            if (eyeObj == null)
            {
                eyeObj = new GameObject("Manaco");
                if (parent != null)
                    eyeObj.transform.SetParent(parent.transform, false);

                eyeObj.AddComponent<Manaco>();

                Undo.RegisterCreatedObjectUndo(eyeObj, "Create Manaco");
            }
            
            Selection.activeGameObject = eyeObj;
        }
    }
}
