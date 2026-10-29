using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPlayerPersistenceBuilder
    {
        private const string CheckCompany = "GreedLastPrototypeChecks";

        [Serializable]
        private sealed class BuildInfo
        {
            public string id, builtUtc, unityVersion, company, product, executableSha256, runtimeSha256;
            public string editorCompany, editorProduct;
            public bool privateEditorIdentityRetained;
            public string scope = "Isolated persistence-probe build, not the verified user release.";
        }

        public static void VerifyFixtureIsolation()
        {
            const string asset = "Assets/Editor/GreedLastPlayerPersistenceProbe.cs.txt";
            if (AssetDatabase.GetMainAssetTypeAtPath(asset) != typeof(TextAsset)
                || AssetDatabase.LoadAssetAtPath<TextAsset>(asset) == null
                || typeof(GreedLastApp).Assembly.GetType("GreedLast.PlayerChecks.GreedLastPlayerPersistenceProbe", false) != null)
                throw new InvalidOperationException("The persistence probe must remain an Editor text asset, not a game runtime type.");
            Debug.Log("Greed player persistence fixture isolation passed: Editor text asset only, no game runtime probe type.");
        }

        private static DirectoryInfo RequireOwnedProject(out string id)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-greed-probe-id");
            if (Application.isPlaying || index < 0 || index + 1 >= args.Length
                || !Guid.TryParseExact(args[index + 1], "N", out _))
                throw new InvalidOperationException("An idle isolated project and unique probe id are required.");
            id = args[index + 1];
            string project = Path.GetDirectoryName(Application.dataPath);
            var parent = Directory.GetParent(project);
            if (Path.GetFileName(project) != "Project" || parent.Name != id
                || parent.Parent.Name != "PlayerPersistenceChecks" || parent.Parent.Parent.Name != "Logs"
                || !File.Exists(Path.Combine(parent.FullName, "prepared.json")))
                throw new InvalidOperationException("Refuse to change settings or build outside an isolated probe export.");
            return parent;
        }

        public static void Build()
        {
            DirectoryInfo parent = RequireOwnedProject(out string id);
            string company = PlayerSettings.companyName, product = PlayerSettings.productName;
            if (company != "DefaultCompany" || product != "greed last")
                throw new InvalidOperationException("Unexpected source product; preserve it for review.");
            const string executable = "GreedLastPersistenceProbe.exe";
            string output = Path.Combine(parent.FullName, "Player");
            Directory.CreateDirectory(output);
            BuildInfo info = null;
            try
            {
                PlayerSettings.companyName = CheckCompany;
                PlayerSettings.productName = "Persistence-" + id;
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/GreedLast_Boot.unity" },
                    locationPathName = Path.Combine(output, executable),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Persistence-probe build failed: " + report.summary.result);
                info = new BuildInfo { id = id, builtUtc = DateTime.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion, company = PlayerSettings.companyName,
                    product = PlayerSettings.productName, executableSha256 = Hash(Path.Combine(output, executable)),
                    runtimeSha256 = Hash(Path.Combine(output, "GreedLastPersistenceProbe_Data/Managed/Assembly-CSharp.dll")) };
            }
            finally
            {
                // A copied project must stay isolated even when it is opened in an Editor later.
                PlayerSettings.companyName = CheckCompany;
                PlayerSettings.productName = "Persistence-" + id;
                AssetDatabase.SaveAssets();
            }
            info.editorCompany = PlayerSettings.companyName;
            info.editorProduct = PlayerSettings.productName;
            info.privateEditorIdentityRetained = info.editorCompany == CheckCompany && info.editorProduct == "Persistence-" + id;
            if (!info.privateEditorIdentityRetained) throw new InvalidOperationException("The copied Editor project must retain its private product identity.");
            File.WriteAllText(Path.Combine(parent.FullName, "build.json"), JsonUtility.ToJson(info, true));
            Debug.Log("Greed isolated persistence player built: " + parent.FullName);
        }

        [Serializable]
        private sealed class EditorIdentity
        {
            public string id, checkedUtc, company, product, settingsCompany, settingsProduct;
            public bool privateIdentity;
        }

        public static void VerifyEditorIdentity()
        {
            DirectoryInfo parent = RequireOwnedProject(out string id);
            var identity = new EditorIdentity { id = id, checkedUtc = DateTime.UtcNow.ToString("O"),
                company = Application.companyName, product = Application.productName,
                settingsCompany = PlayerSettings.companyName, settingsProduct = PlayerSettings.productName };
            identity.privateIdentity = identity.company == CheckCompany && identity.settingsCompany == CheckCompany
                && identity.product == "Persistence-" + id && identity.settingsProduct == "Persistence-" + id;
            if (!identity.privateIdentity) throw new InvalidOperationException("A fresh copied Editor must load only the private product identity.");
            string path = Path.Combine(parent.FullName, "editor-identity.json");
            if (File.Exists(path)) throw new InvalidOperationException("Preserve the previous private Editor identity result.");
            File.WriteAllText(path, JsonUtility.ToJson(identity, true));
            Debug.Log("Greed private Editor identity verified after reopening: " + parent.FullName);
        }

        private static string Hash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty);
        }
    }
}
