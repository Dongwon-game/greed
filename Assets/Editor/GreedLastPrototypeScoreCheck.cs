using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeScoreCheck
    {
        [MenuItem("Greed Last/Run Score Boundary Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Score checks require Edit mode.");
            foreach (int initial in new[] { 0, int.MaxValue - 5, int.MaxValue })
            {
                foreach (GreedLastJudgementResult result in new[] { GreedLastJudgementResult.Success,
                    GreedLastJudgementResult.Good, GreedLastJudgementResult.Miss })
                    VerifyJudgement(initial, result);
            }
            VerifyBundleAwards();
            RunGrades();
            RunTargets();
            VerifyRecordReload();
            Debug.Log("Greed score boundaries passed: actual exact/Good/Miss rewards, section/route rewards, grade and review targets, capped recent/best/ranking JSON reload. Numeric boundary values are injected in isolated editor cores; this is not a multi-hour play or device-performance test. Personal settings are preserved and slots are not accessed.");
        }

        [MenuItem("Greed Last/Run Score Grade Boundary Check")]
        public static void RunGrades()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Grade checks require Edit mode.");
            foreach (var example in new[] { (1940, "S"), (1240, "A"), (640, "B"), (0, "C"), (int.MaxValue, "S") })
                Require(Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteGrade", Record(example.Item1)) == example.Item2,
                    "Infinite grades must retain existing thresholds without overflow at score=" + example.Item1);
            Require(Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteGrade", Record(0, combo: int.MaxValue)) == "S"
                && Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteGrade", Record(int.MaxValue, misses: int.MaxValue)) == "C",
                "Large combo bonuses and Miss penalties must be evaluated before narrowing, not wrap across grades.");
            var normal = new GreedLastRunRecord(true, int.MaxValue, 3, 0, 0, 6, 15, 0, 0,
                90f, "score-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            Require(Invoke<string>(typeof(GreedLastMockBackend), "BuildRunGrade", normal) == "S",
                "A capped normal record must not receive C from grade addition overflow.");
            Require(Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", "2147483647점 갱신", true) == "정박 3연속"
                && Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", "2147483447점 갱신", true) == "2147483647점 갱신"
                && Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", "800점 탈출", false) == "1000점 탈출",
                "An advanced score goal must not wrap to 800 or repeat an unreachable goal at the numeric ceiling.");
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.EnterInfiniteRun();
            Field(core, "score").SetValue(core, int.MaxValue);
            core.Tick(Time.realtimeSinceStartup);
            Require(Invoke<string>(typeof(GreedLastRuntimeUi), "BuildInfiniteSnapshotGrade", run) == "S",
                "The live infinite grade must agree with a capped score rather than wrap to C.");
            Debug.Log("Greed score grade boundaries passed: existing S/A/B/C thresholds, wide intermediate sums/products, live grade and advancing score goal ceiling.");
        }

        [MenuItem("Greed Last/Run Score Target Boundary Check")]
        public static void RunTargets()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Target checks require Edit mode.");
            int failures = 0;
            void Check(string actual, string expected, string source)
            {
                Debug.Log($"Greed score target observation: source={source}, actual={actual}, expected={expected}.");
                if (actual != expected) failures++;
            }
            foreach (var example in new[] { (800, "1000점 갱신"), (int.MaxValue - 200, "2147483647점 갱신"),
                (int.MaxValue - 199, "정박 3연속"), (int.MaxValue, "정박 3연속") })
            {
                string summary = example.Item1 + "점 갱신 / 달성 " + example.Item1 + "점";
                Check(Invoke<string>(typeof(GreedLastMockBackend), "BuildAdvancedReviewTargetText", summary, true),
                    example.Item2, "result/" + example.Item1);
                Check(Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", summary, true),
                    example.Item2, "entry/" + example.Item1);
            }
            foreach (var example in new[] { (1650, "무한 2000점 갱신 / 현재 1650점"),
                (int.MaxValue - 500, "무한 2147483500점 갱신 / 현재 2147483147점"),
                (int.MaxValue - 100, "무한 2147483647점 갱신 / 현재 2147483547점"),
                (int.MaxValue, "다음 구간 무실수") })
            {
                var record = new GreedLastInfiniteRunRecord(true, example.Item1, 240f, 5, 5,
                    "score-boundary", string.Empty, 15, 0, 0, 12, "manual_stop",
                    "평균 정확 / 정박 최고 5 / 집중 발동 1회 / 최근 구간 실수 0회 / 최근 구간 보정 0회");
                Check(Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteRunNextTargetText", record),
                    example.Item2, "milestone/" + example.Item1);
            }
            Require(failures == 0, "Result, entry and infinite milestone goals must agree without wrapping or exceeding the score ceiling. Failures=" + failures);
            Debug.Log("Greed score target boundaries passed: 12 result/entry/milestone cases; existing targets remain unchanged below the numeric ceiling.");
        }

        internal static (GreedLastRunSnapshot before, GreedLastRunSnapshot after) VerifyJudgement(
            int initial, GreedLastJudgementResult expected)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.EnterInfiniteRun();
            void Advance(float seconds)
            {
                while (seconds > 0.0001f)
                {
                    float step = Mathf.Min(seconds, 0.04f);
                    core.Tick(clock += step);
                    seconds -= step;
                }
            }
            for (int wait = 0; !run.ActivePattern && wait < 200; wait++) Advance(0.04f);
            Require(run.ActivePattern, "Score boundary checks need a real automatically arriving trap.");
            // Only the cumulative score is seeded; clocks, trap timing and resolution remain real core APIs.
            Field(core, "score").SetValue(core, initial);
            core.Tick(clock);
            float delta = expected == GreedLastJudgementResult.Good
                ? -(run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
            Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds);
            GreedLastRunSnapshot before = run;
            if (initial > 0)
            {
                int available = int.MaxValue - initial;
                Require(Invoke<int>(typeof(GreedLastRuntimeUi), "BuildActiveExactScorePreview", before) == available
                    && Invoke<int>(typeof(GreedLastRuntimeUi), "BuildActiveGoodScorePreview", before) == available,
                    "Before-input exact and Good previews must show only the points still available at the score ceiling.");
            }
            GreedLastRunChannel channel = expected == GreedLastJudgementResult.Miss
                ? (run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left)
                : run.Pattern.Channel;
            core.HandleInput(channel, clock);
            Debug.Log($"Greed score boundary observation: initial={initial}, expected={expected}, actual={run.LastResult}, score={run.Score}, gain={run.LastJudgementScoreGain}.");
            Require(run.LastResult == expected && run.Score >= initial
                && run.LastJudgementScoreGain == (long)run.Score - initial,
                "A real judgement must not wrap a cumulative score or lose its actual reward at the integer boundary.");
            Require(expected == GreedLastJudgementResult.Miss ? run.Score == initial
                : initial == 0 ? run.Score > 0 : run.Score == int.MaxValue,
                "Score rewards must remain unchanged normally and saturate only at the existing storage ceiling.");
            GreedLastRunSnapshot afterJudgement = run;
            int judged = run.SuccessCount + run.GoodCount + run.MissCount;
            core.HandleInput(channel, clock);
            Require(run.SuccessCount + run.GoodCount + run.MissCount == judged && run.Score == before.Score + run.LastJudgementScoreGain,
                "A duplicate input must not re-award a boundary judgement.");
            Require(core.CreateInfiniteRunRecord().Score == run.Score && !run.SaveEligible,
                "Boundary records must preserve the current infinite score without creating a normal-save candidate.");
            return (before, afterJudgement);
        }

        private static void VerifyBundleAwards()
        {
            foreach (bool pressure in new[] { false, true })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                core.EnterInfiniteRun();
                Field(core, "score").SetValue(core, int.MaxValue - 5);
                Field(core, "chapterProgress").SetValue(core, 2);
                Field(core, "activeInfiniteLoadoutDisplayProfile").SetValue(core, pressure ? "압박 루트" : string.Empty);
                typeof(GreedLastRunCore).GetMethod("AdvanceRunAfterPattern", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(core, new object[] { Time.realtimeSinceStartup });
                core.Tick(Time.realtimeSinceStartup);
                Require(run.Score == int.MaxValue && run.InfiniteSectionsCleared == 1,
                    "Section and pressure-route awards must not wrap the cumulative score.");
            }
        }

        private static GreedLastInfiniteRunRecord Record(int score, float distance = 100f, int combo = 0, int misses = 0)
            => new GreedLastInfiniteRunRecord(true, score, distance, 0, 1, "score-boundary", string.Empty,
                3, 0, misses, combo, "no_input", string.Empty);

        private static void VerifyRecordReload()
        {
            string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "score-boundary-" + Guid.NewGuid().ToString("N");
            try
            {
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(backend.RegisterInfiniteRun(Record(int.MaxValue, 100f))
                    && backend.RegisterInfiniteRun(Record(int.MaxValue, 120f)),
                    "Capped scores must still use distance to resolve tied records.");
                string saved = PlayerPrefs.GetString(key);
                string board = backend.BuildRecordBoardText(2);
                var reloaded = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                foreach (string name in new[] { "lastInfiniteRunRecord", "bestInfiniteRunRecord" })
                {
                    var record = (GreedLastInfiniteRunRecord)Field(reloaded, name).GetValue(reloaded);
                    Require(record.Score == int.MaxValue && record.Distance == 120f,
                        "JSON must retain capped recent and best scores and the normal tie-breaker.");
                }
                var ranks = (GreedLastInfiniteRunRecord[])Field(reloaded, "infiniteRunRankings").GetValue(reloaded);
                var lobby = reloaded.LoadLobby();
                Require(ranks[0].Score == int.MaxValue && ranks[0].Distance == 120f && ranks[1].Distance == 100f
                    && reloaded.BuildRecordBoardText(2) == board && board.Contains("[S]", StringComparison.Ordinal)
                    && PlayerPrefs.GetString(key) == saved && !reloaded.HasSaveLoadoutCandidate
                    && !lobby.SaveSlotOccupied[0] && !lobby.SaveSlotOccupied[1] && !lobby.SaveSlotOccupied[2],
                    "Capped record reload must preserve ranking, grade and text without changing slots or persistence.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static FieldInfo Field(object instance, string name)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static T Invoke<T>(Type type, string name, params object[] args)
            => (T)type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
