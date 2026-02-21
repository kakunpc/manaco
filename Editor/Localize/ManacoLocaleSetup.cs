using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// 初回起動時にデフォルトのロケールアセット（ja / en）を自動生成する。
    /// </summary>
    [InitializeOnLoad]
    public static class ManacoLocaleSetup
    {
        static ManacoLocaleSetup()
        {
            EditorApplication.delayCall += CreateDefaultLocaleAssetsIfNeeded;
        }

        public static void RecreateLocaleAssets()
        {
            CreateDefaultLocaleAssets(force: true);
            ManacoLocale.Reload();
            Debug.Log("[Manaco] Locale assets recreated.");
        }

        private static void CreateDefaultLocaleAssetsIfNeeded()
        {
            CreateDefaultLocaleAssets(force: false);
        }

        private static void CreateDefaultLocaleAssets(bool force)
        {
            string packageRoot = GetPackageRoot();
            if (string.IsNullOrEmpty(packageRoot)) return;

            string localizePath = packageRoot + "/Localize";
            if (!AssetDatabase.IsValidFolder(localizePath))
                AssetDatabase.CreateFolder(packageRoot, "Localize");

            CreateOrUpdate(localizePath + "/ja.asset", BuildJaLocale, force);
            CreateOrUpdate(localizePath + "/en.asset", BuildEnLocale, force);

            AssetDatabase.SaveAssets();
            ManacoLocale.Reload();
        }

        private static void CreateOrUpdate(string path, System.Action<ManacoLocaleData> builder, bool force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ManacoLocaleData>(path);
            if (existing != null && !force) return;

            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ManacoLocaleData>();
                builder(existing);
                AssetDatabase.CreateAsset(existing, path);
            }
            else
            {
                builder(existing);
                EditorUtility.SetDirty(existing);
            }
        }

        // ---- 日本語ロケール ----
        private static void BuildJaLocale(ManacoLocaleData d)
        {
            d.languageName = "日本語";
            d.languageCode = "ja";
            d.entries = new List<ManacoLocaleData.LocaleEntry>
            {
                E("Label.AvatarPreset",        "アバタープリセット"),
                E("Label.CustomMaterial",      "カスタムマテリアル"),
                E("Label.AdvancedSettings",    "上級設定"),
                E("Label.EyeType",             "目のタイプ"),
                E("Label.Renderer",            "レンダラー"),
                E("Label.MaterialSlot",        "マテリアルスロット"),
                E("Label.Material",            "マテリアル"),
                E("Label.Resolution",          "解像度"),
                E("Prompt.SelectPreset",       "--- プリセットを選択して適用 ---"),
                E("Prompt.SelectMaterial",     "--- マテリアルを選択して適用 ---"),
                E("Prompt.SelectAvatarPreset", "--- アバタープリセットを選択 ---"),
                E("Prompt.SelectMatPreset",    "--- マテリアルプリセットを選択 ---"),
                E("Popup.ApplyPreset",         "Apply Preset"),
                E("Popup.ApplyMaterial",       "Apply Material"),
                E("Popup.AvatarPreset",        "Avatar Preset"),
                E("Popup.ShaderPreset",        "Shader Preset"),
                E("Button.Refresh",            "更新"),
                E("Button.Delete",             "削除"),
                E("Button.Add",                "+ 追加"),
                E("Button.Apply",              "Apply"),
                E("Button.OpenUVEditor",       "UV エディタを開く"),
                E("Toggle.NdmfPreview",        "NDMF Preview を有効にする"),
                E("Toggle.FallbackTexture",    "フォールバックテクスチャを自動生成"),
                E("Tooltip.FallbackTexture",   "ビルド時にシェーダーをレンダリングして _MainTex に設定します。VRChatセーフティー設定対策。"),
                E("EyeType.Both",              "両目"),
                E("EyeType.Left",              "左目"),
                E("EyeType.Right",             "右目"),
                E("Message.MaterialNotSet",    "カスタムマテリアルが未設定です。"),
                E("Message.OpenFromInspector", "Inspectorの「UV エディタを開く」ボタンから開いてください。"),
                E("Message.RegionDeleted",     "リージョンが削除されました。"),
                E("Message.ClickHint",         "右パネルをクリックして追加\n右クリックで削除"),
                E("Message.NotSet",            "（未設定）"),
                E("Message.NoTexture",         "テクスチャなし"),
                E("Message.ClickHintBottom",   "左クリック: UVIslandを追加　右クリック: 削除"),
                E("Message.UVIslandCount",     "選択済みUV Island: {0} 個"),
                E("Message.SelUVIslands",      "選択済み UV Island: {0} 個"),
                E("Message.UVPoints",          "UV頂点: {0} 個"),
                E("Window.UVEditor",           "Eye UV Editor"),
                E("Window.Setup",              "Manaco(まなこ)"),
                E("Setup.Title",               "Material Assign Non-destructive Assistant for Customization Operations（まなこ）"),
                E("Setup.TargetAvatar",        "Target Avatar"),
                E("Preset.SaveTitle",          "Save Avatar Preset"),
                E("Prefs.Language",            "言語"),
            };
        }

        // ---- 英語ロケール ----
        private static void BuildEnLocale(ManacoLocaleData d)
        {
            d.languageName = "English";
            d.languageCode = "en";
            d.entries = new List<ManacoLocaleData.LocaleEntry>
            {
                E("Label.AvatarPreset",        "Avatar Preset"),
                E("Label.CustomMaterial",      "Custom Material"),
                E("Label.AdvancedSettings",    "Advanced Settings"),
                E("Label.EyeType",             "Eye Type"),
                E("Label.Renderer",            "Renderer"),
                E("Label.MaterialSlot",        "Material Slot"),
                E("Label.Material",            "Material"),
                E("Label.Resolution",          "Resolution"),
                E("Prompt.SelectPreset",       "--- Select and Apply Preset ---"),
                E("Prompt.SelectMaterial",     "--- Select and Apply Material ---"),
                E("Prompt.SelectAvatarPreset", "--- Select Avatar Preset ---"),
                E("Prompt.SelectMatPreset",    "--- Select Material Preset ---"),
                E("Popup.ApplyPreset",         "Apply Preset"),
                E("Popup.ApplyMaterial",       "Apply Material"),
                E("Popup.AvatarPreset",        "Avatar Preset"),
                E("Popup.ShaderPreset",        "Shader Preset"),
                E("Button.Refresh",            "Refresh"),
                E("Button.Delete",             "Delete"),
                E("Button.Add",                "+ Add"),
                E("Button.Apply",              "Apply"),
                E("Button.OpenUVEditor",       "Open UV Editor"),
                E("Toggle.NdmfPreview",        "Enable NDMF Preview"),
                E("Toggle.FallbackTexture",    "Auto-generate Fallback Texture"),
                E("Tooltip.FallbackTexture",   "Renders shader at build time and sets _MainTex. Protects against VRChat safety settings."),
                E("EyeType.Both",              "Both"),
                E("EyeType.Left",              "Left"),
                E("EyeType.Right",             "Right"),
                E("Message.MaterialNotSet",    "Custom material is not set."),
                E("Message.OpenFromInspector", "Please open from Inspector's 'Open UV Editor' button."),
                E("Message.RegionDeleted",     "The region has been deleted."),
                E("Message.ClickHint",         "Click right panel to add\nRight-click to delete"),
                E("Message.NotSet",            "(Not set)"),
                E("Message.NoTexture",         "No texture"),
                E("Message.ClickHintBottom",   "Left-click: Add UV Island  Right-click: Delete"),
                E("Message.UVIslandCount",     "Selected UV Islands: {0}"),
                E("Message.SelUVIslands",      "Selected UV Islands: {0}"),
                E("Message.UVPoints",          "UV Points: {0}"),
                E("Window.UVEditor",           "Eye UV Editor"),
                E("Window.Setup",              "Manaco"),
                E("Setup.Title",               "Material Assign Non-destructive Assistant for Customization Operations"),
                E("Setup.TargetAvatar",        "Target Avatar"),
                E("Preset.SaveTitle",          "Save Avatar Preset"),
                E("Prefs.Language",            "Language"),
            };
        }

        private static ManacoLocaleData.LocaleEntry E(string key, string value)
            => new ManacoLocaleData.LocaleEntry { key = key, value = value };

        private static string GetPackageRoot()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(ManacoLocaleSetup).Assembly);
            return packageInfo?.assetPath;
        }
    }
}
