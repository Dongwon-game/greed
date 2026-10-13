using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeAndroidReadinessCheck
    {
        // Bundled Windows tool paths from the installed 6000.4.3f1 module manifest.
        private static readonly string[] RequiredFiles =
        {
            "OpenJDK/bin/java.exe",
            "OpenJDK/bin/javac.exe",
            "Tools/gradle/bin/gradle.bat",
            "SDK/platform-tools/adb.exe",
            "SDK/platforms/android-36/android.jar",
            "SDK/build-tools/36.0.0/aapt2.exe",
            "SDK/build-tools/36.0.0/apksigner.bat",
            "NDK/source.properties",
            "NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang++.exe"
        };

        [Serializable]
        private sealed class Report
        {
            public string checkedUtc, unityVersion, editorDataPath;
            public string scope = "Read-only bundled Windows-host Android prerequisite checks. File presence is not an APK build, accepted SDK license, tool execution/version check, signing check or device test. Custom external tool settings are not changed or evaluated.";
            public bool moduleSupported, bundledPrerequisitesPresent;
            public List<FileCheck> files = new List<FileCheck>();
            public List<string> missingBundledFiles = new List<string>();
        }

        [Serializable]
        private sealed class FileCheck
        {
            public string relativePath;
            public bool present;
        }

        [MenuItem("Greed Last/Check Android Prerequisites")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Android prerequisite checks require an idle Edit-mode editor.");
            Require(Application.platform == RuntimePlatform.WindowsEditor && Application.unityVersion == "6000.4.3f1",
                "This bundled tool list is specific to Windows-host Unity 6000.4.3f1; review it for another editor.");
            VerifyPolicy();
            Report report = Inspect(EditorApplication.applicationContentsPath,
                BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android), File.Exists);
            report.checkedUtc = DateTime.UtcNow.ToString("O");
            report.unityVersion = Application.unityVersion;
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "AndroidChecks");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "prerequisites-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("Greed Android prerequisites: module=" + report.moduleSupported
                + ", bundled prerequisites=" + report.bundledPrerequisitesPresent
                + ", missing=" + report.missingBundledFiles.Count + ". Report: " + path
                + ". No module install, license acceptance, target/settings change or APK build was performed.");
        }

        [MenuItem("Greed Last/Check Android Prerequisites", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static Report Inspect(string editorDataPath, bool supported, Func<string, bool> fileExists)
        {
            string dataPath = Path.GetFullPath(editorDataPath);
            string androidPath = Path.Combine(dataPath, "PlaybackEngines", "AndroidPlayer");
            var report = new Report { editorDataPath = dataPath, moduleSupported = supported };
            foreach (string relative in RequiredFiles)
            {
                bool present = fileExists(Path.Combine(androidPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                report.files.Add(new FileCheck { relativePath = relative, present = present });
                if (!present) report.missingBundledFiles.Add(relative);
            }
            report.bundledPrerequisitesPresent = supported && report.missingBundledFiles.Count == 0;
            return report;
        }

        internal static void VerifyPolicy()
        {
            string dataPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GreedAndroidProbeFixture", "Data"));
            string androidPath = Path.Combine(dataPath, "PlaybackEngines", "AndroidPlayer");
            var completeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string relative in RequiredFiles)
                Require(completeFiles.Add(Path.Combine(androidPath, relative.Replace('/', Path.DirectorySeparatorChar))),
                    "Android prerequisite paths must be unique.");
            int probes = 0;
            Report empty = Inspect(dataPath, false, path => { probes++; return false; });
            Require(probes == RequiredFiles.Length && !empty.bundledPrerequisitesPresent
                && empty.missingBundledFiles.Count == RequiredFiles.Length,
                "An absent module must still list every missing file without declaring prerequisites present.");
            Report unsupported = Inspect(dataPath, false, completeFiles.Contains);
            Require(!unsupported.bundledPrerequisitesPresent && unsupported.missingBundledFiles.Count == 0,
                "Files alone must not masquerade as loaded Android build support.");
            Report supported = Inspect(dataPath, true, completeFiles.Contains);
            Require(supported.bundledPrerequisitesPresent && supported.missingBundledFiles.Count == 0,
                "The supported-module and complete-file positive control must pass.");
            foreach (string missing in completeFiles)
            {
                Report incomplete = Inspect(dataPath, true, path => path != missing && completeFiles.Contains(path));
                Require(!incomplete.bundledPrerequisitesPresent && incomplete.missingBundledFiles.Count == 1,
                    "Every individual missing Android tool must be reported, not silently accepted.");
            }
            Debug.Log("Greed Android prerequisite policy passed: absent module, unsupported complete tree, supported complete tree and "
                + RequiredFiles.Length + " single missing-file cases. In-memory file probes only; no APK/tool execution or settings changes.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
