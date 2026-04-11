using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;

namespace com.kakunvr.manaco.Editor
{
    [InitializeOnLoad]
    internal static class ManacoVersionChecker
    {
        private const string LatestUrl = "https://raw.githubusercontent.com/kakunpc/manaco/versions/latest.json";
        private const string CheckedKey = "com.kakunvr.manaco.version.checked";
        private const string LatestVersionKey = "com.kakunvr.manaco.version.latest";
        private const string ReleaseUrlKey = "com.kakunvr.manaco.version.releaseUrl";

        [Serializable]
        private sealed class LatestVersionInfo
        {
            public string version;
            public string url;
        }

        static ManacoVersionChecker()
        {
            EditorApplication.delayCall += BeginVersionCheck;
        }

        public static void CheckNow()
        {
            SessionState.SetBool(CheckedKey, true);
            _ = FetchLatestVersionAsync();
        }

        public static string CurrentVersion
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ManacoVersionChecker).Assembly);
                return string.IsNullOrEmpty(info?.version) ? "0.0.0" : info.version;
            }
        }

        public static string LatestVersion => SessionState.GetString(LatestVersionKey, string.Empty);
        public static string ReleaseUrl => SessionState.GetString(ReleaseUrlKey, string.Empty);

        public static bool HasUpdate(out string latestVersion)
        {
            latestVersion = LatestVersion;
            if (string.IsNullOrEmpty(latestVersion))
                return false;
            return TryParseVersion(CurrentVersion, out var current) &&
                   TryParseVersion(latestVersion, out var latest) &&
                   latest > current;
        }

        private static void BeginVersionCheck()
        {
            if (SessionState.GetBool(CheckedKey, false))
                return;

            SessionState.SetBool(CheckedKey, true);
            _ = FetchLatestVersionAsync();
        }

        private static async Task FetchLatestVersionAsync()
        {
            using (var request = UnityWebRequest.Get(LatestUrl))
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                    await Task.Delay(100);

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                    return;

                var info = JsonUtility.FromJson<LatestVersionInfo>(request.downloadHandler.text);
                if (info == null || string.IsNullOrEmpty(info.version))
                    return;

                SessionState.SetString(LatestVersionKey, info.version);
                SessionState.SetString(ReleaseUrlKey, info.url ?? string.Empty);
                InternalEditorUtility.RepaintAllViews();
            }
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            if (Version.TryParse(value, out version))
                return true;

            version = null;
            return false;
        }
    }
}
