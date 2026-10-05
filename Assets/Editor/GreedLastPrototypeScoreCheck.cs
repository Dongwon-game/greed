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
            RunProgress();
            RunRecordStatistics();
            VerifyRecordReload();
            VerifyLoadoutReload();
            Debug.Log("Greed score boundaries passed: actual exact/Good/Miss rewards, section/route rewards, grade and review targets, capped recent/best/ranking JSON reload, and all three isolated normal-save slots with one candidate and two starts. Numeric boundary values are injected in isolated editor cores/records; this is not a multi-hour play or device-performance test. Personal settings are preserved and personal slots are not accessed.");
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

        [MenuItem("Greed Last/Run Progress Boundary Check")]
        public static void RunProgress()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Progress checks require Edit mode.");
            int failures = 0;
            void Check(bool passed, string observation)
            {
                Debug.Log("Greed progress boundary observation: " + observation + ", passed=" + passed);
                if (!passed) failures++;
            }
            foreach (int initial in new[] { 0, int.MaxValue - 2, int.MaxValue })
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                Field(backend, "growthPoints").SetValue(backend, initial);
                int expected = (int)Math.Min(int.MaxValue, (long)initial + 3);
                int actual = backend.AddGrowthPointsForTest(3).GrowthPoints;
                Check(actual == expected, "growth initial=" + initial + ", actual=" + actual + ", expected=" + expected);
                Check(backend.AddGrowthPointsForTest(-3).GrowthPoints == expected,
                    "negative development reward must not remove earned points at initial=" + initial);
            }
            foreach (int initial in new[] { 0, int.MaxValue - 1, int.MaxValue })
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                string[] counters = { "normalClearCount", "normalFailCount", "normalAbandonCount", "infiniteRunCount" };
                foreach (string counter in counters) Field(backend, counter).SetValue(backend, initial);
                var normal = new GreedLastRunRecord(true, 100, 3, 0, 0, 1, 1, 0, 0, 12f,
                    "progress-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                backend.RegisterClear(normal);
                foreach (GreedLastRunAttemptOutcome outcome in new[] { GreedLastRunAttemptOutcome.Failed, GreedLastRunAttemptOutcome.Abandoned })
                    backend.RegisterNormalAttempt(new GreedLastRunAttemptRecord(true, outcome, 100, 0, 0, 0,
                        1, 1, 0, 1, 12f, 1, 1, 3, false, "no_input", string.Empty, string.Empty));
                backend.RegisterInfiniteRun(Record(100));
                int expected = initial == int.MaxValue ? initial : initial + 1;
                foreach (string counter in counters)
                {
                    int actual = (int)Field(backend, counter).GetValue(backend);
                    Check(actual == expected, counter + " initial=" + initial + ", actual=" + actual + ", expected=" + expected);
                }
                string progress = InvokeInstance<string>(backend, "BuildRunProgressSummaryText");
                Check(progress.Contains("일반 " + ((long)expected * 3) + "회", StringComparison.Ordinal)
                    && progress.Contains("클리어율 50%", StringComparison.Ordinal),
                    "wide totals and clear rate initial=" + initial + ", actual=" + progress);
                string tier = InvokeInstance<string>(backend, "BuildProfileTierLabel");
                Check(tier == (initial == 0 ? "B" : "S"), "profile tier initial=" + initial + ", actual=" + tier);
            }
            var retries = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            Field(retries, "normalFailCount").SetValue(retries, int.MaxValue);
            Field(retries, "normalAbandonCount").SetValue(retries, int.MaxValue);
            Check(InvokeInstance<string>(retries, "BuildProfileBadgeName") == "다시 일어서는 자"
                && InvokeInstance<string>(retries, "BuildPrototypePlaytestChecklistText").Contains("일반 재도전 필요", StringComparison.Ordinal),
                "large retry counters must retain their existing retry badge and checklist");
            foreach (int initial in new[] { 100_000_000, int.MaxValue })
            {
                var sample = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                Field(sample, "normalClearCount").SetValue(sample, initial);
                Field(sample, "infiniteRunCount").SetValue(sample, initial);
                Field(sample, "sampleSaveLoadoutSequence").SetValue(sample, initial);
                sample.AddSampleSaveLoadoutForTest(out _);
                var saved = (GreedLastRunRecord[])Field(sample, "saveLoadoutSlots").GetValue(sample);
                Check(saved[0].IsValid && saved[0].Score == int.MaxValue
                    && saved[0].Distance > 0f && !float.IsInfinity(saved[0].Distance)
                    && (int)Field(sample, "sampleSaveLoadoutSequence").GetValue(sample) >= initial,
                    "development sample must remain bounded at progress seed=" + initial + ", score=" + saved[0].Score);
            }
            var candidate = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            Field(candidate, "sampleSaveLoadoutSequence").SetValue(candidate, int.MaxValue);
            candidate.AddSampleClearCandidateForTest(out _);
            var candidateRecord = (GreedLastRunRecord)Field(candidate, "lastRunRecord").GetValue(candidate);
            Check(candidate.HasSaveLoadoutCandidate && candidateRecord.Score == int.MaxValue
                && candidateRecord.LoadoutName == "테스트 후보 S1-2147483648",
                "development clear candidate must preserve a positive sequence name at the ceiling");
            Require(failures == 0, "Growth, persistent play counts and profile aggregates must not wrap. Failures=" + failures);
            VerifyProgressReload();
            Debug.Log("Greed progress boundaries passed: three growth seeds, three real registration seeds across four counters, wide totals/clear rate/profile tier, retry presence, two development samples, one development candidate, and three isolated JSON reload/spend/reward/two-use cases. Only non-persisting backends and assigned temporary test keys are used; no personal save key or settings are accessed.");
        }

        private static void VerifyProgressReload()
        {
            foreach (int initial in new[] { 0, int.MaxValue - 2, int.MaxValue })
            {
                string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "progress-boundary-" + Guid.NewGuid().ToString("N");
                try
                {
                    var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    string[] counters = { "normalClearCount", "normalFailCount", "normalAbandonCount", "infiniteRunCount" };
                    foreach (string counter in counters) Field(backend, counter).SetValue(backend, initial);
                    Field(backend, "growthPoints").SetValue(backend, initial);
                    backend.RegisterClear(new GreedLastRunRecord(true, 100, 3, 0, 0, 1, 1, 0, 0, 12f,
                        "progress-boundary", "박자 감각", "집중 축", string.Empty, "박자 감각 / 집중 축", string.Empty));
                    backend.SelectSaveLoadoutSlot(1);
                    backend.ConfirmSaveLoadout();
                    foreach (GreedLastRunAttemptOutcome outcome in new[] { GreedLastRunAttemptOutcome.Failed, GreedLastRunAttemptOutcome.Abandoned })
                        backend.RegisterNormalAttempt(new GreedLastRunAttemptRecord(true, outcome, 100, 0, 0, 0,
                            1, 1, 0, 1, 12f, 1, 1, 3, false, "no_input", string.Empty, string.Empty));
                    backend.RegisterInfiniteRun(Record(100));
                    int points = (int)Math.Min(int.MaxValue, (long)initial + 5);
                    int plays = (int)Math.Min(int.MaxValue, (long)initial + 1);
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.LoadLobby().GrowthPoints == points && !backend.HasSaveLoadoutCandidate,
                        "JSON reload must preserve capped growth and the already-consumed clear candidate.");
                    foreach (string counter in counters)
                        Require((int)Field(backend, counter).GetValue(backend) == plays,
                            "Each persistent play counter must retain its capped registration through JSON: " + counter);
                    int cost = Invoke<int>(typeof(GreedLastMockBackend), "BuildGrowthUpgradeCost", 0);
                    backend.UpgradeGrowth(GreedLastRunChannel.Center, out _);
                    points -= cost;
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.GrowthFocusLevel == 1 && backend.LoadLobby().GrowthPoints == points,
                        "An earned growth balance at the ceiling must still be spendable at its existing cost.");
                    backend.RegisterInfiniteRun(Record(200, 120f));
                    points = (int)Math.Min(int.MaxValue, (long)points + 1);
                    int infinitePlays = (int)Math.Min(int.MaxValue, (long)plays + 1);
                    for (int use = 0; use < 2; use++)
                    {
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(backend.TryConsumeSelectedSaveLoadout(out var used, out _, out var error)
                            && used.Score == 100 && string.IsNullOrEmpty(error),
                            "The saved S2 loadout must still permit exactly two real backend starts.");
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        var lobby = backend.LoadLobby();
                        Require(lobby.GrowthPoints == points && !lobby.SaveSlotOccupied[0] && !lobby.SaveSlotOccupied[2]
                            && lobby.SaveSlotOccupied[1] == (use == 0) && !backend.HasSaveLoadoutCandidate,
                            "Use consumption must preserve growth/other slots and auto-clear only S2 after its second start.");
                        foreach (string counter in counters)
                            Require((int)Field(backend, counter).GetValue(backend) == (counter == "infiniteRunCount" ? infinitePlays : plays),
                                "Starting a saved loadout must not register a finished run or alter other counters.");
                    }
                    Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                        "A third backend start must remain unavailable after reload.");
                }
                finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            }
        }

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

        [MenuItem("Greed Last/Run Record Statistics Boundary Check")]
        public static void RunRecordStatistics()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Record statistics checks require Edit mode.");
            int failures = 0;
            foreach (var example in new[] {
                (0, 0, 0, 0, 0, 0), (8, 1, 1, 80, 10, 10), (1, 1, 1, 33, 34, 33),
                (int.MaxValue, int.MaxValue, 0, 50, 50, 0), (int.MaxValue, 0, int.MaxValue, 50, 0, 50),
                (int.MaxValue, int.MaxValue, int.MaxValue, 33, 34, 33), (-1, 5, 0, 0, 100, 0) })
            {
                string expected = $"정확 {example.Item4}% · 보정 {example.Item5}% · 실수 {example.Item6}%";
                string actual = Invoke<string>(typeof(GreedLastMockBackend), "BuildJudgementRatioSummary",
                    example.Item1, example.Item2, example.Item3);
                int exact = Invoke<int>(typeof(GreedLastMockBackend), "CalculateExactRatePercent",
                    example.Item1, example.Item2, example.Item3);
                string flow = Invoke<string>(typeof(GreedLastMockBackend), "BuildJudgementFlowSummary",
                    example.Item1, example.Item2, example.Item3, string.Empty);
                bool hasData = (long)Math.Max(0, example.Item1) + Math.Max(0, example.Item2) + Math.Max(0, example.Item3) > 0;
                bool passed = actual == expected && exact == example.Item4
                    && (hasData ? flow.Contains(expected, StringComparison.Ordinal) : flow == "판정 기록 없음");
                Debug.Log($"Greed record statistics observation: exact={example.Item1}, Good={example.Item2}, Miss={example.Item3}, actual={actual}, rate={exact}, passed={passed}.");
                if (!passed) failures++;
            }
            var record = new GreedLastRunRecord(true, 100, 3, 0, 0, 1, int.MaxValue, int.MaxValue, 0,
                12f, "statistics-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            if (Invoke<string>(typeof(GreedLastMockBackend), "FormatRunRecordJudgementCounts", record)
                .Contains("기록 없음", StringComparison.Ordinal)) failures++;
            var notClean = new GreedLastRunRecord(true, 100, 3, 0, 0, 1, 1, int.MaxValue, 1,
                12f, "statistics-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            if (Invoke<bool>(typeof(GreedLastMockBackend), "HasCleanRunRecord", notClean)) failures++;
            foreach (var example in new[] { (279.25f, "C"), (279.75f, "B"), (549.25f, "A"), (549.75f, "S"),
                (5_000_000_000f, "S"), (float.MaxValue, "S") })
            {
                var distanceRecord = new GreedLastRunRecord(true, 0, 0, 0, 0, 0, 0, 0, 0, example.Item1,
                    "distance-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                string grade = Invoke<string>(typeof(GreedLastMockBackend), "BuildRunGrade", distanceRecord);
                Debug.Log($"Greed normal distance grade observation: distance={example.Item1}, grade={grade}, expected={example.Item2}.");
                if (grade != example.Item2) failures++;
            }
            foreach (var example in new[] { (8, 1, 1, string.Empty, 1, 0, 40),
                (1, 2, 0, string.Empty, 0, 0, 0), (3, 0, 2, string.Empty, 0, 0, 0),
                (0, 0, 0, "정박 최고 3", 1, 0, 36), (0, 0, 0, "정박 최고 4", 1, 0, 48),
                (0, 0, 0, "정박 최고 5", 1, 1, 100), (0, 0, 0, "정박 최고 7", 1, 1, 120),
                (int.MaxValue, int.MaxValue, 0, string.Empty, 1, 0, 40),
                (int.MaxValue, int.MaxValue, 1, string.Empty, 0, 0, 0),
                (0, 0, 0, "정박 최고 2147483647", 1, 1, 120) })
            {
                var bonusRecord = new GreedLastRunRecord(true, 0, 0, 0, 0, 0, example.Item1, example.Item2, example.Item3,
                    12f, "bonus-statistics", string.Empty, string.Empty, string.Empty, string.Empty, example.Item4);
                var bonus = Invoke<GreedLastInfiniteLoadoutBonus>(typeof(GreedLastRunCore), "CalculateInfiniteLoadoutBonus", bonusRecord);
                bool passed = bonus.FocusBonus == example.Item5 && bonus.ComboBonus == example.Item6 && bonus.ScoreBonus == example.Item7;
                Debug.Log($"Greed infinite statistics bonus observation: focus={bonus.FocusBonus}, combo={bonus.ComboBonus}, score={bonus.ScoreBonus}, passed={passed}.");
                if (!passed) failures++;
            }
            foreach (var example in new[] { (3, 3, 1, false), (1, 1, 3, true),
                (int.MaxValue, int.MaxValue, int.MaxValue, false) })
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                var infinite = new GreedLastInfiniteRunRecord(true, 0, 12f, 0, 1, string.Empty, string.Empty,
                    example.Item1, example.Item2, example.Item3, 0, string.Empty, string.Empty);
                Field(backend, "lastInfiniteRunRecord").SetValue(backend, infinite);
                string hint = InvokeInstance<string>(backend, "BuildInfiniteRunTrainingHint", infinite);
                object[] recommendation = { 0, string.Empty };
                bool hasRecommendation = InvokeInstance<bool>(backend, "TrySelectInfiniteGrowthRecommendation", recommendation);
                var attempt = new GreedLastRunAttemptRecord(true, GreedLastRunAttemptOutcome.Abandoned, 0, 0, 0, 0,
                    1, example.Item1, example.Item2, example.Item3, 12f, 1, 1, 3, false, string.Empty, string.Empty, string.Empty);
                string normalHint = Invoke<string>(typeof(GreedLastMockBackend), "BuildNormalAttemptTrainingHint", attempt);
                bool passed = hint.Contains("위험도 상승 전 보정권 입력 안정화", StringComparison.Ordinal) == example.Item4
                    && hasRecommendation == example.Item4
                    && (!hasRecommendation || (int)recommendation[0] == 2)
                    && normalHint.Contains("타이밍보다 생존 보정 우선", StringComparison.Ordinal) == example.Item4;
                Debug.Log($"Greed record guidance observation: counts={example.Item1}/{example.Item2}/{example.Item3}, needsCorrection={example.Item4}, passed={passed}.");
                if (!passed) failures++;
            }
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.EnterInfiniteRun();
            foreach (string counter in new[] { "successCount", "goodCount", "missCount" })
                Field(core, counter).SetValue(core, int.MaxValue);
            core.Tick(Time.realtimeSinceStartup);
            if (Invoke<string>(typeof(GreedLastRuntimeUi), "BuildInfiniteStoppedHighlight", snapshot) != "기록 확인") failures++;
            Require(failures == 0, "Large judgement totals must retain records, ratios and clean-run requirements. Failures=" + failures);
            Debug.Log("Greed record statistics boundaries passed: seven ratio cases, record presence, clean-run comparison, six normal-distance grades, ten infinite start bonuses, three backend guidance cases and stopped review guidance. Existing half-point thresholds retained. Constructed records only; no personal persistence, performance or multi-hour play measurement.");
        }

        private static void VerifyLoadoutReload()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                foreach (int score in new[] { int.MaxValue - 5, int.MaxValue })
                {
                    string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "normal-score-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        var record = new GreedLastRunRecord(true, score, 3, 0, 0, 6, 15, 0, 0, 90f,
                            "score-boundary", "박자 감각", "집중 축", "황금 박동",
                            "박자 감각 / 안정 루트 / 집중 축 / 황금 박동", string.Empty);
                        var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(backend.RegisterClear(record).SaveLoadoutCandidateAvailable,
                            "A capped normal clear must still offer its one save candidate.");
                        backend.SelectSaveLoadoutSlot(slot);
                        backend.ConfirmSaveLoadout();
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(!backend.HasSaveLoadoutCandidate, "Saving a capped normal record must consume its candidate across reload.");
                        var saved = (GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend);
                        Require(saved[slot].IsValid && saved[slot].Score == score
                            && saved[slot].PrimaryGift == record.PrimaryGift && string.IsNullOrEmpty(saved[slot].Relic)
                            && !saved[slot].ChoiceSummary.Contains(record.Relic, StringComparison.Ordinal),
                            "The chosen slot must retain capped score/gift while excluding the normal-run-only relic through JSON reload.");
                        backend.SelectSaveLoadoutSlot((slot + 1) % 3);
                        backend.ConfirmSaveLoadout();
                        Require(!backend.HasSaveLoadoutCandidate
                            && !((GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend))[(slot + 1) % 3].IsValid,
                            "A second confirmation must not copy the consumed candidate into another slot.");
                        backend.SelectSaveLoadoutSlot(slot);
                        for (int use = 0; use < 2; use++)
                        {
                            backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                            string beforePreparation = PlayerPrefs.GetString(key);
                            Require(!string.IsNullOrEmpty(backend.BuildActiveLoadoutText())
                                && !string.IsNullOrEmpty(backend.BuildInfiniteStartReadyText())
                                && PlayerPrefs.GetString(key) == beforePreparation
                                && ((int[])Field(backend, "saveLoadoutUsesRemaining").GetValue(backend))[slot] == 2 - use,
                                "Preparation previews must not consume or rewrite a capped loadout.");
                            Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord consumed, out _, out string error)
                                && consumed.Score == score && string.IsNullOrEmpty(error),
                                "Each actual start must retain the capped source record and consume exactly one use.");
                            var core = new GreedLastRunCore();
                            GreedLastRunSnapshot run = default;
                            core.SnapshotChanged += value => run = value;
                            var prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(consumed, "집중 축", "황금 박동");
                            core.EnterInfiniteRun(prepared);
                            var bonus = Invoke<GreedLastInfiniteLoadoutBonus>(typeof(GreedLastRunCore), "CalculateInfiniteLoadoutBonus", prepared);
                            Require(run.InfiniteMode && !run.Stopped && !run.SaveEligible
                                && run.Score == bonus.ScoreBonus && run.Score < consumed.Score
                                && string.IsNullOrEmpty(consumed.Relic) && prepared.Relic == "황금 박동",
                                "The live infinite run must receive only its existing start bonus, not inherit the normal cumulative score.");
                            backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                            saved = (GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend);
                            Require(saved[slot].IsValid == (use == 0)
                                && ((int[])Field(backend, "saveLoadoutUsesRemaining").GetValue(backend))[slot] == 1 - use,
                                "The first start retains one use; the second auto-clears only its source slot across reload.");
                            for (int other = 0; other < 3; other++)
                                if (other != slot) Require(!saved[other].IsValid,
                                    "A boundary start must not populate another slot.");
                        }
                        Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _)
                            && !backend.HasSaveLoadoutCandidate && backend.BuildRecordBoardText(0).Contains("[S]", StringComparison.Ordinal),
                            "A third start must fail while the capped normal record and its grade remain available.");
                    }
                    finally
                    {
                        PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.Save();
                    }
                }
            }
            Debug.Log("Greed capped loadout reload passed: six isolated score/slot cases, candidate once, preparation without consumption, two starts with existing bonus, auto-clear/reload and third-start rejection. Constructed boundary records, not a full normal-play or device-performance measurement; personal slots/settings unchanged.");
        }

        private static FieldInfo Field(object instance, string name)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static T Invoke<T>(Type type, string name, params object[] args)
            => (T)type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        private static T InvokeInstance<T>(object instance, string name, params object[] args)
            => (T)instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
