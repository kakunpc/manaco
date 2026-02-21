using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// Manacoエディタ拡張の言語データを保持するScriptableObject。
    /// </summary>
    [CreateAssetMenu(fileName = "NewLocale", menuName = "ちゃとらとりー/Manaco Locale")]
    public class ManacoLocaleData : ScriptableObject
    {
        [Tooltip("言語の表示名（例: 日本語, English）")]
        public string languageName = "English";

        [Tooltip("言語コード（例: ja, en）")]
        public string languageCode = "en";

        [Serializable]
        public class LocaleEntry
        {
            public string key;
            [TextArea(1, 3)]
            public string value;
        }

        public List<LocaleEntry> entries = new List<LocaleEntry>();

        /// <summary>
        /// キーに対応する文字列を返す。見つからない場合は null を返す。
        /// </summary>
        public string Get(string key)
        {
            foreach (var e in entries)
                if (e.key == key) return e.value;
            return null;
        }
    }
}
