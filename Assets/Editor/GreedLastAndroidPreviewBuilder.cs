using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastAndroidPreviewBuilder
    {
        private const string Company = "GreedLastPrototypeChecks";
        private const string PreviewCompany = "GreedLastPrototypePreview";
        private const string PreviewProduct = "Greed Last Mobile Preview";
        private const string PackageId = "com.greedlast.prototype.mobilepreview";
        private const string Scene = "Assets/Scenes/GreedLast_Boot.unity";

        [Serializable]
        private sealed class Export
        {
            public int schema;
            public string id, sourceHead, sourceProject;
        }

        [Serializable]
        private sealed class Prerequisites
        {
            public string unityVersion;
            public bool moduleSupported, bundledPrerequisitesPresent;
        }

        [Serializable]
        private sealed class Identity
        {
            public string id, company, product, packageId;
        }

        [Serializable]
        private sealed class BuildInfo
        {
            public string id, sourceHead, builtUtc, unityVersion, company, product, packageId;
            public string apkPath, apkSha256, editorCompany, editorProduct;
            public bool smokePassed, developmentBuild, privateEditorIdentityRetained;
            public string scope = "Isolated ARM64 IL2CPP preview APK with default debug signing. Not a store release, installation, device/input/audio/FPS test or proof of SDK license acceptance.";
        }

        private static string RequireExport(out Export export)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-greed-android-id");
            string id = index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
            string project = Path.GetDirectoryName(Application.dataPath);
            string root = Path.GetDirectoryName(project);
            string marker = Path.Combine(root, "prepared.json");
            Require(Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode
                && Application.platform == RuntimePlatform.WindowsEditor && Application.unityVersion == "6000.4.3f1",
                "Use an idle Windows-host Unity 6000.4.3f1 batch export, not a personal Editor.");
            Require(File.Exists(marker), "The Android export marker is missing.");
            export = JsonUtility.FromJson<Export>(File.ReadAllText(marker));
            Require(IsOwnedExport(project, id, export), "Refuse Android preparation/build outside its owned export.");
            for (DirectoryInfo directory = new DirectoryInfo(project); directory != null; directory = directory.Parent)
                Require((directory.Attributes & FileAttributes.ReparsePoint) == 0, "Android exports must not follow directory links.");
            return root;
        }

        private static bool IsOwnedExport(string project, string id, Export export)
        {
            if (export == null || export.schema != 1 || !Regex.IsMatch(id ?? "", "^[0-9a-f]{32}$")
                || export.id != id || !Regex.IsMatch(export.sourceHead ?? "", "^[0-9a-f]{40}$")
                || string.IsNullOrEmpty(export.sourceProject) || !Path.IsPathRooted(export.sourceProject)) return false;
            string expected = Path.Combine(export.sourceProject, "Logs", "AndroidPreviewBuilds", id, "Project");
            return string.Equals(Path.GetFullPath(project), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
        }

        public static void Inspect()
        {
            string root = RequireExport(out _);
            Require(!File.Exists(Path.Combine(root, "prerequisites.json")), "Preserve the previous Android prerequisite evidence.");
            VerifyPolicy();
            GreedLastPrototypeAndroidReadinessCheck.Run();
            string[] reports = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "AndroidChecks"), "prerequisites-*.json");
            Require(reports.Length == 1, "A new export must produce exactly one prerequisite report.");
            File.Copy(reports[0], Path.Combine(root, "prerequisites.json"), false);
            Debug.Log("Greed Android preview inspection complete. No settings, target, SDK license or APK changes.");
        }

        private static void RequirePrerequisites(string root)
        {
            string path = Path.Combine(root, "prerequisites.json");
            Require(File.Exists(path), "Inspect the owned Android export before preparation.");
            var readiness = JsonUtility.FromJson<Prerequisites>(File.ReadAllText(path));
            Require(readiness != null && readiness.unityVersion == Application.unityVersion,
                "The Android prerequisite report must match this Editor.");
            RequireSupported(readiness.moduleSupported && BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
                readiness.bundledPrerequisitesPresent);
        }

        private static void RequireSupported(bool module, bool bundledFiles)
        {
            Require(module && bundledFiles, "Android prerequisites are missing; no settings, target or APK changes were attempted.");
        }

        public static void Prepare()
        {
            string root = RequireExport(out Export export);
            RequirePrerequisites(root);
            string prepared = Path.Combine(root, "prepared-android-project.json");
            Require(!File.Exists(prepared) && PlayerSettings.companyName == "DefaultCompany" && PlayerSettings.productName == "greed last",
                "Android preparation requires an untouched source export.");
            string notices = Path.Combine(Application.dataPath, "StreamingAssets", "GreedLast", "ThirdParty");
            Require(!Directory.Exists(notices), "Do not replace exported third-party notices.");
            Directory.CreateDirectory(notices);
            File.Copy("Assets/ThirdParty/NotoSansKR/OFL.txt", Path.Combine(notices, "NotoSansKR-OFL.txt"), false);
            File.Copy("Assets/ThirdParty/NotoSansSymbols2/OFL.txt", Path.Combine(notices, "NotoSansSymbols2-OFL.txt"), false);
            File.Copy("Assets/ThirdParty/NotoSansKR/NOTICE.txt", Path.Combine(notices, "NotoSansKR-NOTICE.txt"), false);
            // Retain a unique Editor identity even if the prepared copy is reopened later.
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = "AndroidPreview-" + export.id;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            WriteNew(prepared, new Identity { id = export.id, company = Company,
                product = PlayerSettings.productName, packageId = PackageId });
        }

        public static void Build()
        {
            string root = RequireExport(out Export export);
            RequirePrerequisites(root);
            string identityPath = Path.Combine(root, "prepared-android-project.json");
            Require(File.Exists(identityPath), "The Android preview must be prepared before switching targets.");
            var identity = JsonUtility.FromJson<Identity>(File.ReadAllText(identityPath));
            string editorProduct = "AndroidPreview-" + export.id;
            Require(identity != null && identity.id == export.id && identity.company == Company && identity.product == editorProduct
                && identity.packageId == PackageId && Application.companyName == Company && Application.productName == editorProduct
                && PlayerSettings.companyName == Company && PlayerSettings.productName == editorProduct,
                "Reopen the Android export with its unique private Editor identity.");
            Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                && PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) == PackageId
                && PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP
                && PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64 && !PlayerSettings.Android.useCustomKeystore,
                "Require the prepared Android ARM64 IL2CPP preview with no custom signing identity.");
            string output = Path.Combine(root, "Player");
            Require(!Directory.Exists(output) && !File.Exists(Path.Combine(root, "build.json")), "Preserve previous APK output and build evidence.");
            GreedLastPrototypeSmokeCheck.Run();
            Directory.CreateDirectory(output);
            string apk = Path.Combine(output, "GreedLastMobilePreview.apk");
            BuildInfo info = null;
            try
            {
                PlayerSettings.companyName = PreviewCompany;
                PlayerSettings.productName = PreviewProduct;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.buildAppBundle = false;
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { Scene }, locationPathName = apk, target = BuildTarget.Android, options = BuildOptions.None });
                Require(result.summary.result == BuildResult.Succeeded && File.Exists(apk), "Android preview APK build did not succeed.");
                bool development = (result.summary.options & BuildOptions.Development) != 0;
                Require(!development, "The mobile preview must not package development controls.");
                info = new BuildInfo { id = export.id, sourceHead = export.sourceHead, builtUtc = DateTime.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion, company = PreviewCompany, product = PreviewProduct, packageId = PackageId,
                    apkPath = "Player/GreedLastMobilePreview.apk", apkSha256 = Hash(apk), smokePassed = true, developmentBuild = development };
            }
            finally
            {
                PlayerSettings.companyName = Company;
                PlayerSettings.productName = editorProduct;
                AssetDatabase.SaveAssets();
            }
            info.editorCompany = PlayerSettings.companyName;
            info.editorProduct = PlayerSettings.productName;
            info.privateEditorIdentityRetained = info.editorCompany == Company && info.editorProduct == editorProduct;
            Require(info.privateEditorIdentityRetained, "The Android export must retain its private Editor identity.");
            WriteNew(Path.Combine(root, "build.json"), info);
            Debug.Log("Greed Android preview APK built without installation: " + apk);
        }

        public static void VerifyPolicy()
        {
            const string id = "0123456789abcdef0123456789abcdef";
            var spec = new Export { schema = 1, id = id, sourceHead = new string('a', 40),
                sourceProject = Path.Combine(Path.GetTempPath(), "GreedAndroidOwnershipFixture") };
            string expected = Path.Combine(spec.sourceProject, "Logs", "AndroidPreviewBuilds", id, "Project");
            Require(IsOwnedExport(expected, id, spec), "The valid owned-export policy control must pass.");
            foreach (string path in new[] { spec.sourceProject, Path.GetDirectoryName(expected), expected + "Other",
                Path.Combine(spec.sourceProject, "Logs", "EngineLoopChecks", id, "Project") })
                Require(!IsOwnedExport(path, id, spec), "Android ownership must reject personal or wrong-kind paths.");
            Require(!IsOwnedExport(expected, "", spec) && !IsOwnedExport(expected, id + "x", spec)
                && !IsOwnedExport(expected, id, null), "Android ownership must reject missing/malformed identities.");
            spec.schema = 2;
            Require(!IsOwnedExport(expected, id, spec), "Unknown export schemas must not change settings.");
            spec.schema = 1;
            spec.sourceHead = "HEAD";
            Require(!IsOwnedExport(expected, id, spec), "An unresolved source ref must not count as committed input.");
            spec.sourceHead = new string('a', 40);
            spec.id = new string('b', 32);
            Require(!IsOwnedExport(expected, id, spec), "The marker and argument must name the same export.");
            int rejected = 0;
            foreach (bool module in new[] { false, true })
            foreach (bool files in new[] { false, true })
            {
                bool blocked = false;
                try { RequireSupported(module, files); }
                catch (InvalidOperationException) { blocked = true; rejected++; }
                Require(blocked == !(module && files), "Only complete module/file readiness may permit preparation.");
            }
            Require(rejected == 3, "All three incomplete prerequisite combinations must fail before preparation.");
            Debug.Log("Greed Android preview policy passed: owned export, ten invalid ownership cases and three missing-prerequisite combinations. In-memory policy only; no target, settings, tool or APK changes.");
        }

        private static void WriteNew(string path, object value)
        {
            using (var file = new FileStream(path, FileMode.CreateNew))
            using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(value, true));
        }

        private static string Hash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
