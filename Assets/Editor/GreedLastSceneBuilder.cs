using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GreedLast_Boot.unity";
        private const string ResetProgressMenuPath = "Greed Last/Reset Prototype Progress (Keep Settings)";
        private const string VerifiedBuildMenuPath = "Greed Last/Build Verified Windows Prototype";
        private const string WindowsBuildDirectory = "Builds/GreedLastPrototype";
        private const string WindowsBuildPath = WindowsBuildDirectory + "/GreedLastPrototype.exe";

        [Serializable]
        private sealed class VerifiedBuildInfo
        {
            public string verifiedAtUtc;
            public string unityVersion;
            public string target;
            public bool smokePassed;
            public bool developmentBuild;
            public string executablePath;
            public string executableSha256;
            public string runtimeAssemblyPath;
            public string runtimeAssemblySha256;
            public string bundledFontLicensePath;
            public string bundledFontLicenseSha256;
            public string bundledSymbolFontLicensePath;
            public string bundledSymbolFontLicenseSha256;
            public string bundledFontNoticePath;
            public string bundledFontNoticeSha256;
        }

        [MenuItem("Greed Last/Rebuild Boot Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GreedLast_Boot";

            var marker = new GameObject("GreedLastSceneMarker");
            marker.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Greed Last boot scene rebuilt: " + ScenePath);
        }

        [MenuItem(ResetProgressMenuPath)]
        public static void ResetPrototypeProgress()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Greed Last 진행도 초기화",
                "일반 런, 저장 조합, 무한 기록, 성장과 첫 연습 완료 상태를 지웁니다.\n\n소리, 진동, 기울임 설정은 유지됩니다.",
                "진행도 초기화",
                "취소");
            if (!confirmed)
            {
                return;
            }

            GreedLastApp.ResetPrototypeProgressForEditor();
            Debug.Log("Greed Last prototype progress reset. Sensory and input settings were preserved.");
            EditorUtility.DisplayDialog(
                "초기화 완료",
                "다음 재생은 GREED LAST 타이틀과 기초 연습부터 시작합니다.",
                "확인");
        }

        [MenuItem(ResetProgressMenuPath, true)]
        private static bool ValidateResetPrototypeProgress()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(VerifiedBuildMenuPath)]
        public static void BuildVerifiedWindowsPrototype()
        {
            GreedLastPrototypeSmokeCheck.Run();
            EnsureBootSceneInBuildSettings();
            Directory.CreateDirectory(WindowsBuildDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "Greed Last verified Windows build failed: " + report.summary.result);
            }

            WriteVerifiedBuildInfo();
            Debug.Log(
                "Greed Last verified Windows prototype built: "
                + WindowsBuildPath
                + " ("
                + report.summary.totalSize
                + " bytes)");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "검증 빌드 완료",
                    "스모크 검사를 통과한 Windows 프로토타입을 만들었습니다.\n\n" + WindowsBuildPath,
                    "확인");
            }
        }

        [MenuItem(VerifiedBuildMenuPath, true)]
        private static bool ValidateBuildVerifiedWindowsPrototype()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void WriteVerifiedBuildInfo()
        {
            const string assemblyPath = "GreedLastPrototype_Data/Managed/Assembly-CSharp.dll";
            const string fontLicenseName = "NotoSansKR-OFL.txt";
            string fontLicensePath = Path.Combine(WindowsBuildDirectory, fontLicenseName);
            File.Copy("Assets/ThirdParty/NotoSansKR/OFL.txt", fontLicensePath, true);
            const string symbolLicenseName = "NotoSansSymbols2-OFL.txt";
            const string fontNoticeName = "NotoSansKR-NOTICE.txt";
            File.Copy("Assets/ThirdParty/NotoSansSymbols2/OFL.txt", Path.Combine(WindowsBuildDirectory, symbolLicenseName), true);
            File.Copy("Assets/ThirdParty/NotoSansKR/NOTICE.txt", Path.Combine(WindowsBuildDirectory, fontNoticeName), true);
            var info = new VerifiedBuildInfo
            {
                verifiedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                target = BuildTarget.StandaloneWindows64.ToString(),
                smokePassed = true,
                developmentBuild = false,
                executablePath = Path.GetFileName(WindowsBuildPath),
                executableSha256 = HashFile(WindowsBuildPath),
                runtimeAssemblyPath = assemblyPath,
                runtimeAssemblySha256 = HashFile(Path.Combine(WindowsBuildDirectory, assemblyPath)),
                bundledFontLicensePath = fontLicenseName,
                bundledFontLicenseSha256 = HashFile(fontLicensePath),
                bundledSymbolFontLicensePath = symbolLicenseName,
                bundledSymbolFontLicenseSha256 = HashFile(Path.Combine(WindowsBuildDirectory, symbolLicenseName)),
                bundledFontNoticePath = fontNoticeName,
                bundledFontNoticeSha256 = HashFile(Path.Combine(WindowsBuildDirectory, fontNoticeName)),
            };
            // Hash game code as well as Unity's reusable launcher; its timestamp alone is not a build ID.
            File.WriteAllText(Path.Combine(WindowsBuildDirectory, "VerifiedBuildInfo.json"), JsonUtility.ToJson(info, true));
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void EnsureBootSceneInBuildSettings()
        {
            if (!File.Exists(ScenePath))
            {
                Build();
                return;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }
    }
}
