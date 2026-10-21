using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastEngineLoopBootstrap
    {
        private const string Company = "GreedLastPrototypeChecks";

        [Serializable]
        private sealed class Prepared
        {
            public string id, company, product, unityVersion;
        }

        private static string RequireOwnedExport(out string id)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-greed-engine-id");
            id = index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
            string project = Path.GetDirectoryName(Application.dataPath);
            DirectoryInfo root = Directory.GetParent(project);
            if (Application.isPlaying || !Guid.TryParseExact(id, "N", out _)
                || Path.GetFileName(project) != "Project" || root == null || root.Name != id
                || root.Parent?.Name != "EngineLoopChecks" || root.Parent.Parent?.Name != "Logs"
                || !File.Exists(Path.Combine(root.FullName, "prepared.json")))
                throw new InvalidOperationException("Refuse to configure or run outside an isolated engine export.");
            return root.FullName;
        }

        public static void Prepare()
        {
            string root = RequireOwnedExport(out string id);
            string output = Path.Combine(root, "prepared-engine-project.json");
            if (File.Exists(output) || PlayerSettings.companyName != "DefaultCompany" || PlayerSettings.productName != "greed last")
                throw new InvalidOperationException("Unexpected source product or earlier preparation; preserve it for review.");
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = "EngineLoop-" + id;
            AssetDatabase.SaveAssets();
            var prepared = new Prepared { id = id, company = Company, product = PlayerSettings.productName, unityVersion = Application.unityVersion };
            using (var file = new FileStream(output, FileMode.CreateNew))
            using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(prepared, true));
        }

        public static void Run()
        {
            string root = RequireOwnedExport(out string id);
            GreedLastPrototypePlayModeCheck.RequireAsyncBatchLifetime();
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-greed-engine-kind");
            string kind = index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
            if (kind != "full" && kind != "cue") throw new InvalidOperationException("Choose a fixed full or cue engine check.");
            if (!File.Exists(Path.Combine(root, "prepared-engine-project.json"))
                || !File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings/Search.index"))
                || PlayerSettings.companyName != Company || Application.companyName != Company
                || PlayerSettings.productName != "EngineLoop-" + id || Application.productName != PlayerSettings.productName)
                throw new InvalidOperationException("Start a fresh editor with its prepared isolated identity and asset index.");
            if (kind == "full") GreedLastPrototypePlayModeCheck.RunFullLoop();
            else GreedLastPrototypePlayModeCheck.RunTrapCueBoundary();
        }
    }
}
