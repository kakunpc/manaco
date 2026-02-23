using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// Manacoエディタ拡張のローカライズ管理クラス。
    /// 選択言語は EditorPrefs に永続化される。
    /// </summary>
    [InitializeOnLoad]
    public static class ManacoLocale
    {
        private const string PrefKey = "com.kakunvr.manaco.language";

        // ---- 日本語フォールバック（locale assetが存在しない場合に使用） ----
        private static readonly Dictionary<string, string> _jaFallback = new Dictionary<string, string>
        {
            ["Label.AvatarPreset"]        = "アバタープリセット",
            ["Label.CustomMaterial"]      = "カスタムマテリアル",
            ["Label.AdvancedSettings"]    = "上級設定",
            ["Label.EyeType"]             = "目のタイプ",
            ["Label.Renderer"]            = "レンダラー",
            ["Label.MaterialSlot"]        = "マテリアルスロット",
            ["Label.Material"]            = "マテリアル",
            ["Label.Resolution"]          = "解像度",
            ["Label.Mode"]                = "モード",
            ["Label.DestinationPreset"]   = "アバタープリセット（コピー先）",
            ["Label.SourceAvatar"]        = "コピー元のアバター",
            ["Label.SourcePreset"]        = "コピー元のアバタープリセット",
            ["Label.ExtractResolution"]   = "抽出解像度",
            ["ManacoMode.EyeMaterialAssignment"] = "マテリアル割り当て",
            ["ManacoMode.CopyEyeFromAvatar"]     = "別アバターの目をコピー",
            ["Section.CopyDestination"]   = "▼ コピー先（自分のアバター）",
            ["Section.CopySource"]        = "▼ コピー元（コピーするアバター）",
            ["Button.OpenUVEditorSource"] = "UV エディタを開く（コピー元）",
            ["Message.SourceUVIslandCount"] = "UV Island 数: {0}",
            ["Prompt.SelectPreset"]       = "--- プリセットを選択して適用 ---",
            ["Prompt.SelectMaterial"]     = "--- マテリアルを選択して適用 ---",
            ["Prompt.SelectAvatarPreset"] = "--- アバタープリセットを選択 ---",
            ["Prompt.SelectMatPreset"]    = "--- マテリアルプリセットを選択 ---",
            ["Popup.ApplyPreset"]         = "Apply Preset",
            ["Popup.ApplyMaterial"]       = "Apply Material",
            ["Popup.AvatarPreset"]        = "Avatar Preset",
            ["Popup.ShaderPreset"]        = "Shader Preset",
            ["Button.Refresh"]            = "更新",
            ["Button.Delete"]             = "削除",
            ["Button.Add"]                = "+ 追加",
            ["Button.Apply"]              = "Apply",
            ["Button.OpenUVEditor"]       = "UV エディタを開く",
            ["Toggle.NdmfPreview"]        = "NDMF Preview を有効にする",
            ["Toggle.FallbackTexture"]    = "フォールバックテクスチャを自動生成",
            ["Tooltip.FallbackTexture"]   = "ビルド時にシェーダーをレンダリングして _MainTex に設定します。VRChatセーフティー設定対策。",
            ["EyeType.Both"]              = "両目",
            ["EyeType.Left"]              = "左目",
            ["EyeType.Right"]             = "右目",
            ["EyeType.BothPupil"]         = "両目瞳孔",
            ["EyeType.LeftPupil"]         = "左目瞳孔",
            ["EyeType.RightPupil"]        = "右目瞳孔",
            ["Message.MaterialNotSet"]    = "カスタムマテリアルが未設定です。",
            ["Message.OpenFromInspector"] = "Inspectorの「UV エディタを開く」ボタンから開いてください。",
            ["Message.RegionDeleted"]     = "リージョンが削除されました。",
            ["Message.ClickHint"]         = "右パネルをクリックして追加\n右クリックで削除",
            ["Message.NotSet"]            = "（未設定）",
            ["Message.NoTexture"]         = "テクスチャなし",
            ["Message.ClickHintBottom"]   = "左クリック: UVIslandを追加　右クリック: 削除",
            ["Message.UVIslandCount"]     = "選択済みUV Island: {0} 個",
            ["Message.SelUVIslands"]      = "選択済み UV Island: {0} 個",
            ["Message.UVPoints"]          = "UV頂点: {0} 個",
            ["Window.UVEditor"]           = "Eye UV Editor",
            ["Window.Setup"]              = "Manaco(まなこ)",
            ["Setup.Title"]               = "Material Assign Non-destructive Assistant for Customization Operations（まなこ）",
            ["Setup.TargetAvatar"]        = "Target Avatar",
            ["Preset.SaveTitle"]          = "Save Avatar Preset",
            ["Prefs.Language"]            = "言語",
        };

        private static readonly Dictionary<string, ManacoLocaleData> _locales =
            new Dictionary<string, ManacoLocaleData>();
        private static ManacoLocaleData _current;
        private static string _currentCode;

        static ManacoLocale()
        {
            _currentCode = EditorPrefs.GetString(PrefKey, "ja");
            // AssetDatabase は InitializeOnLoad 時点で利用可能
            Reload();
        }

        public static string CurrentLanguageCode => _currentCode ?? "ja";

        /// <summary>
        /// 利用可能なロケールアセットを再読み込みする。
        /// </summary>
        public static void Reload()
        {
            _locales.Clear();
            var guids = AssetDatabase.FindAssets("t:ManacoLocaleData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<ManacoLocaleData>(path);
                if (data != null && !string.IsNullOrEmpty(data.languageCode))
                    _locales[data.languageCode] = data;
            }

            _currentCode = EditorPrefs.GetString(PrefKey, "ja");
            _locales.TryGetValue(_currentCode, out _current);
        }

        /// <summary>
        /// 使用言語を変更し EditorPrefs に永続化する。
        /// </summary>
        public static void SetLanguage(string code)
        {
            _currentCode = code;
            EditorPrefs.SetString(PrefKey, code);
            _locales.TryGetValue(code, out _current);
        }

        /// <summary>
        /// キーに対応するローカライズ文字列を返す。
        /// ロケールアセットに該当がない場合は日本語フォールバックを使用。
        /// </summary>
        public static string T(string key)
        {
            if (_current != null)
            {
                var val = _current.Get(key);
                if (val != null) return val;
            }
            if (_jaFallback.TryGetValue(key, out var fallback))
                return fallback;
            return key;
        }

        /// <summary>
        /// string.Format を適用してローカライズ文字列を返す。
        /// </summary>
        public static string T(string key, params object[] args)
            => string.Format(T(key), args);

        /// <summary>
        /// 利用可能な言語の一覧を (codes, names) のペアで返す。
        /// </summary>
        public static (string[] codes, string[] names) GetAvailableLanguages()
        {
            if (_locales.Count == 0)
                return (new[] { "ja" }, new[] { "日本語" });

            var codes = new List<string>();
            var names = new List<string>();
            foreach (var kv in _locales)
            {
                codes.Add(kv.Key);
                names.Add(kv.Value.languageName);
            }
            return (codes.ToArray(), names.ToArray());
        }
    }
}
