using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeRankingIdentityCheck
    {
        [MenuItem("Greed Last/Run Ranking Identity Preservation Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Ranking checks require idle Edit mode.");
            var failures = new List<string>();
            int cases = 0;
            foreach (string difference in new[] { "start-gift", "primary-gift", "relic", "route", "timing", "combo", "name" })
            {
                CheckNormal(difference, failures);
                cases++;
            }
            foreach (string difference in new[] { "timing", "miss-reason", "profile", "name" })
            {
                CheckInfinite(difference, failures);
                cases++;
            }
            Require(failures.Count == 0, "Distinct equal-stat ranking records must survive insertion and reload: "
                + string.Join(" | ", failures));
            Debug.Log("Greed ranking identity preservation passed: " + cases
                + " equal-stat metadata pairs survive insertion/reload; exact duplicate rows still collapse. "
                + "Isolated test keys and direct backend records only, not native input or full-play validation.");
        }

        private static void CheckNormal(string difference, List<string> failures)
        {
            string key = NewKey();
            try
            {
                var first = Normal(string.Empty);
                var second = Normal(difference);
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                backend.RegisterClear(first);
                backend.RegisterClear(second);
                var ranks = Read<GreedLastRunRecord[]>(backend, "normalRunRankings");
                CheckPair(ranks, first, second, difference + "/normal-insert", failures);

                // Seed both valid rows independently so reload pruning is checked even on a broken insertion path.
                ranks[0] = first;
                ranks[1] = second;
                Save(backend);
                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                ranks = Read<GreedLastRunRecord[]>(backend, "normalRunRankings");
                CheckPair(ranks, first, second, difference + "/normal-reload", failures);
                backend.RegisterClear(second);
                CheckPair(Read<GreedLastRunRecord[]>(backend, "normalRunRankings"), first, second,
                    difference + "/normal-exact-duplicate", failures);
                Require(Read<int>(backend, "normalClearCount") == 3 && Read<int>(backend, "growthPoints") == 9,
                    "Ranking identity must not change clear counts or growth rewards.");
            }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }

        private static void CheckInfinite(string difference, List<string> failures)
        {
            string key = NewKey();
            try
            {
                var first = Infinite(string.Empty);
                var second = Infinite(difference);
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(backend.RegisterInfiniteRun(first) && backend.RegisterInfiniteRun(second),
                    "Both independent infinite results must be accepted.");
                var ranks = Read<GreedLastInfiniteRunRecord[]>(backend, "infiniteRunRankings");
                CheckPair(ranks, first, second, difference + "/infinite-insert", failures);
                ranks[0] = first;
                ranks[1] = second;
                Save(backend);
                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                CheckPair(Read<GreedLastInfiniteRunRecord[]>(backend, "infiniteRunRankings"), first, second,
                    difference + "/infinite-reload", failures);
                backend.RegisterInfiniteRun(second);
                CheckPair(Read<GreedLastInfiniteRunRecord[]>(backend, "infiniteRunRankings"), first, second,
                    difference + "/infinite-exact-duplicate", failures);
                Require(Read<int>(backend, "infiniteRunCount") == 3,
                    "Ranking identity must not change actual infinite attempt counts.");
            }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }

        private static GreedLastRunRecord Normal(string difference) => new GreedLastRunRecord(
            true, 900, 3, difference == "combo" ? 1 : 0, 2, 6, 12, 1, 2, 90f,
            difference == "name" ? "other-name" : "equal-stat-record",
            difference == "start-gift" ? "생존 감각" : "박자 감각",
            difference == "primary-gift" ? "연쇄 축" : "집중 축",
            difference == "relic" ? "사막의 잔상" : "황금 박동",
            difference == "route" ? "변주 루트 / 집중 축" : "안정 루트 / 집중 축",
            difference == "timing" ? "타이밍 평균 늦음 30ms" : "타이밍 평균 빠름 30ms");

        private static GreedLastInfiniteRunRecord Infinite(string difference) => new GreedLastInfiniteRunRecord(
            true, 900, 90f, 3, 2,
            difference == "name" ? "other-name" : "equal-stat-record",
            difference == "profile" ? "연쇄형" : "집중형", 12, 1, 2, 6,
            difference == "miss-reason" ? "too_late" : "wrong_channel",
            difference == "timing" ? "타이밍 평균 늦음 30ms" : "타이밍 평균 빠름 30ms");

        private static void CheckPair<T>(T[] ranks, T first, T second, string label, List<string> failures)
            where T : struct
        {
            int firstCount = 0, secondCount = 0;
            foreach (T row in ranks)
            {
                if (row.Equals(first)) firstCount++;
                if (row.Equals(second)) secondCount++;
            }
            if (firstCount != 1 || secondCount != 1) failures.Add(label + "=" + firstCount + "/" + secondCount);
        }

        private static string NewKey() => GreedLastMockBackend.PersistenceTestKeyPrefix
            + "ranking-identity-" + Guid.NewGuid().ToString("N");

        private static T Read<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void Save(GreedLastMockBackend backend) => typeof(GreedLastMockBackend)
            .GetMethod("SavePersistedData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(backend, null);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
