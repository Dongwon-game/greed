using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeRecordCultureCheck
    {
        [MenuItem("Greed Last/Run Record Timing Culture Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Record culture checks require idle Edit mode.");
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            var failures = new List<string>();
            int cases = 0;
            try
            {
                foreach (string writer in new[] { "en-US", "de-DE" })
                foreach (string reader in new[] { "ko-KR", "en-US", "de-DE", "fr-FR" })
                foreach (float seconds in new[] { 1.5f, 12.3f })
                {
                    CheckReload(writer, reader, seconds, failures);
                    cases++;
                }
            }
            finally { CultureInfo.CurrentCulture = previousCulture; }
            Require(failures.Count == 0,
                "Stored focus duration must survive a decimal culture change: " + string.Join(" | ", failures));
            Debug.Log("Greed record timing culture passed: " + cases
                + " stored decimal/write-read culture pairs retain normal and infinite focus summaries,"
                + " original timing text and exact saved JSON. Dedicated test keys only; no personal data or device-language proof.");
        }

        private static void CheckReload(string writer, string reader, float seconds, List<string> failures)
        {
            string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "record-culture-" + Guid.NewGuid().ToString("N");
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(writer);
                string timing = "집중 발동 1회 / 집중 성공 2회 / 집중 유지 " + seconds.ToString("0.0") + "초";
                var normal = new GreedLastRunRecord(true, 900, 3, 0, 2, 6, 12, 1, 2, 90f,
                    "culture-record", "박자 감각", "집중 축", "황금 박동", "안정 루트 / 집중 축", timing);
                var infinite = new GreedLastInfiniteRunRecord(true, 900, 90f, 3, 2,
                    "culture-record", "집중형", 12, 1, 2, 6, "too_late", timing);
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                backend.RegisterClear(normal);
                Require(backend.RegisterInfiniteRun(infinite), "The private infinite record must be registered.");
                // Complete existing legacy normalization before testing a culture-only reload.
                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                string originalJson = PlayerPrefs.GetString(key);
                int points = Read<int>(backend, "growthPoints");

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(reader);
                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                var restoredNormal = Read<GreedLastRunRecord>(backend, "lastRunRecord");
                var restoredInfinite = Read<GreedLastInfiniteRunRecord>(backend, "lastInfiniteRunRecord");
                string expected = "집중: 발동 1회 / 집중 중 정확 2회 / 유지 " + seconds.ToString("0.0") + "초";
                string label = writer + "->" + reader + "/" + seconds.ToString("0.0", CultureInfo.InvariantCulture);
                if (!backend.BuildRunClearResultMessage(restoredNormal).Contains(expected, StringComparison.Ordinal))
                    failures.Add(label + "/normal");
                if (!backend.BuildInfiniteRunSavedPrepMessage(restoredInfinite).Contains(expected, StringComparison.Ordinal))
                    failures.Add(label + "/infinite");
                Require(restoredNormal.Equals(normal) && restoredInfinite.Equals(infinite)
                    && restoredNormal.TimingProfile == timing && restoredInfinite.TimingProfile == timing,
                    "A culture change must not rewrite either saved record or its original timing text.");
                Require(PlayerPrefs.GetString(key) == originalJson && Read<int>(backend, "growthPoints") == points
                    && Read<int>(backend, "normalClearCount") == 1 && Read<int>(backend, "infiniteRunCount") == 1,
                    "Reading record summaries must not change progress, rewards or persisted JSON.");
            }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }

        private static T Read<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
