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
            ["Label.MaterialList"]        = "マテリアル一覧",
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
            ["Button.Select"]             = "選ぶ",
            ["Button.OpenUVEditor"]       = "UV エディタを開く",
            ["Toggle.NdmfPreview"]        = "NDMF Preview を有効にする",
            ["Toggle.FallbackTexture"]    = "フォールバックテクスチャを自動生成",
            ["Tooltip.FallbackTexture"]   = "ビルド時にシェーダーをレンダリングして _MainTex に設定します。VRChatセーフティー設定対策。",
            ["EyeType.Left"]              = "左目",
            ["EyeType.Right"]             = "右目",
            ["EyeType.LeftPupil"]         = "左目瞳孔",
            ["EyeType.RightPupil"]        = "右目瞳孔",
            ["Tutorial.SelectEyeType"]       = "{0}を選んでください",
            ["Tutorial.SelectSourceEyeType"] = "コピー元の{0}を選んでください",
            ["Message.MaterialNotSet"]    = "カスタムマテリアルが未設定です。",
            ["Message.MatSlotHint"]       = "メッシュが表示されない場合は ◀ ▶ でマテリアルスロットを切り替えてみてください",
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

        private static readonly Dictionary<string, string> _jaSupplemental = new Dictionary<string, string>
        {
            ["ManacoMode.EyeMaterialAssignment"] = "マテリアル割り当て",
            ["ManacoMode.CopyEyeFromAvatar"] = "別アバターの目をコピー",
            ["Label.Renderer"] = "レンダラー",
            ["Label.MaterialSlot"] = "マテリアルスロット",
            ["Label.Mode"] = "モード",
            ["Label.MaterialList"] = "マテリアル一覧",
            ["EyeType.LeftPupil"] = "左目瞳孔",
            ["EyeType.RightPupil"] = "右目瞳孔",
            ["Tutorial.Title"] = "Manaco Tutorial",
            ["Tutorial.Skip"] = "スキップ",
            ["Tutorial.Progress"] = "Step {0} / {1}",
            ["Tutorial.Restart"] = "チュートリアルを最初からやり直す",
            ["Tutorial.ModeDescription"] = "まず使いたいモードを選んでください。",
            ["Tutorial.DestinationPresetDescription"] = "対象アバターのプリセットを選んでください。",
            ["Tutorial.MaterialDescription"] = "使うカスタムマテリアルを選んでください。",
            ["Tutorial.SourceSetupDescription"] = "コピー元アバターとコピー元プリセットを選んでください。",
            ["Tutorial.SelectPresetFirst"] = "先にプリセットを選択してください。",
            ["Tutorial.IslandDescription"] = "{0} のアイランドを選択してください。",
            ["Tutorial.SourceIslandDescription"] = "コピー元の {0} のアイランドを選択してください。",
            ["Tutorial.SelectEyeButton"] = "{0} を選択",
            ["Tutorial.SelectSourceEyeButton"] = "コピー元の {0} を選択",
            ["Tutorial.Back"] = "戻る",
            ["Tutorial.Next"] = "次へ",
            ["Tutorial.Finish"] = "完了",
            ["Tutorial.RequiredTitle"] = "確認",
            ["Tutorial.RequiredDestinationPreset"] = "対象アバターのプリセットを選択してください。",
            ["Tutorial.RequiredMaterial"] = "カスタムマテリアルを選択してください。",
            ["Tutorial.RequiredSourceAvatar"] = "コピー元アバターを選択してください。",
            ["Tutorial.RequiredSourcePreset"] = "コピー元プリセットを選択してください。",
            ["Tutorial.UnselectedTitle"] = "未選択の確認",
            ["Tutorial.UnselectedBody"] = "まだアイランドが選択されていません。このまま進むと、目が正しく反映されない可能性があります。",
            ["Tutorial.SelectNow"] = "選択する",
            ["Tutorial.ProceedAnyway"] = "次へ進む",
            ["Tutorial.UvTitle"] = "{0} UV",
            ["Tutorial.SourceUvTitle"] = "{0} コピー元 UV",
            ["Button.OpenEmbeddedUv"] = "UV をインスペクター内で選択",
            ["Button.CloseEmbeddedUv"] = "UV 選択を閉じる",
        };

        private static readonly Dictionary<string, string> _enSupplemental = new Dictionary<string, string>
        {
            ["ManacoMode.EyeMaterialAssignment"] = "Material Assignment",
            ["ManacoMode.CopyEyeFromAvatar"] = "Copy Eyes From Another Avatar",
            ["Label.Renderer"] = "Renderer",
            ["Label.MaterialSlot"] = "Material Slot",
            ["Label.Mode"] = "Mode",
            ["Label.MaterialList"] = "Material List",
            ["EyeType.LeftPupil"] = "Left Pupil",
            ["EyeType.RightPupil"] = "Right Pupil",
            ["Tutorial.Title"] = "Manaco Tutorial",
            ["Tutorial.Skip"] = "Skip",
            ["Tutorial.Progress"] = "Step {0} / {1}",
            ["Tutorial.Restart"] = "Restart tutorial",
            ["Tutorial.ModeDescription"] = "Choose the mode you want to use first.",
            ["Tutorial.DestinationPresetDescription"] = "Select the target avatar preset.",
            ["Tutorial.MaterialDescription"] = "Select the custom material you want to use.",
            ["Tutorial.SourceSetupDescription"] = "Select the source avatar and source preset.",
            ["Tutorial.FinalPreviewDescription"] = "Review the preview settings, then finish the tutorial.",
            ["Tutorial.SelectPresetFirst"] = "Select a preset first.",
            ["Tutorial.IslandDescription"] = "Select the island for {0}.",
            ["Tutorial.SourceIslandDescription"] = "Select the source island for {0}.",
            ["Tutorial.SelectEyeButton"] = "Select {0}",
            ["Tutorial.SelectSourceEyeButton"] = "Select source {0}",
            ["Tutorial.Back"] = "Back",
            ["Tutorial.Next"] = "Next",
            ["Tutorial.Finish"] = "Finish",
            ["Tutorial.RequiredTitle"] = "Confirmation",
            ["Tutorial.RequiredDestinationPreset"] = "Select the target avatar preset.",
            ["Tutorial.RequiredMaterial"] = "Select a custom material.",
            ["Tutorial.RequiredSourceAvatar"] = "Select the source avatar.",
            ["Tutorial.RequiredSourcePreset"] = "Select the source preset.",
            ["Tutorial.UnselectedTitle"] = "Nothing selected",
            ["Tutorial.UnselectedBody"] = "No island is selected yet. If you continue, the eye may not be applied correctly.",
            ["Tutorial.SelectNow"] = "Select now",
            ["Tutorial.ProceedAnyway"] = "Continue",
            ["Tutorial.UvTitle"] = "{0} UV",
            ["Tutorial.SourceUvTitle"] = "{0} Source UV",
            ["Button.OpenEmbeddedUv"] = "Select UV in Inspector",
            ["Button.CloseEmbeddedUv"] = "Close UV selection",
        };

        private static readonly Dictionary<string, string> _jaAdditionalSupplemental = new Dictionary<string, string>
        {
            ["Label.DestinationPreset"] = "アバタープリセット（コピー先）",
            ["Label.SourceAvatar"] = "コピー元のアバター",
            ["Label.SourcePreset"] = "コピー元のアバタープリセット",
            ["Tutorial.Texture"] = "テクスチャ",
            ["Tutorial.SelectedUvIslands"] = "選択済みUV Island {0}個",
            ["Tutorial.FinalPreviewDescription"] = "NDMFプレビュー設定を確認して完了してください。",
        };

        private static readonly Dictionary<string, string> _enAdditionalSupplemental = new Dictionary<string, string>
        {
            ["Label.DestinationPreset"] = "Avatar Preset (Destination)",
            ["Label.SourceAvatar"] = "Source Avatar",
            ["Label.SourcePreset"] = "Source Avatar Preset",
            ["Tutorial.Texture"] = "Texture",
            ["Tutorial.SelectedUvIslands"] = "Selected UV Islands: {0}",
            ["Tutorial.FinalPreviewDescription"] = "Review the NDMF preview settings, then finish.",
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
            if (_currentCode == "en" && _enAdditionalSupplemental.TryGetValue(key, out var enExtra))
                return enExtra;
            if (_jaAdditionalSupplemental.TryGetValue(key, out var jaExtra))
                return jaExtra;
            if (_currentCode == "en" && _enSupplemental.TryGetValue(key, out var enVal))
                return enVal;
            if (_jaSupplemental.TryGetValue(key, out var jaVal))
                return jaVal;
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
        /// EyeType をローカライズした名称に変換する。
        /// </summary>
        public static string GetEyeTypeName(Manaco.EyeType eyeType) => eyeType switch
        {
            Manaco.EyeType.Left       => T("EyeType.Left"),
            Manaco.EyeType.Right      => T("EyeType.Right"),
            Manaco.EyeType.LeftPupil  => T("EyeType.LeftPupil"),
            Manaco.EyeType.RightPupil => T("EyeType.RightPupil"),
            _                         => eyeType.ToString(),
        };

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
