using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeReviewScopeCheck
    {
        [MenuItem("Greed Last/Run Chapter Review Scope Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Review scope checks require Edit mode.");
            var failures = new List<string>();
            int cases = 0;
            foreach (string segment in new[] { "1장", "2장", "3장", "4장", "탈출" })
            foreach (bool noGood in new[] { false, true })
            foreach (bool inside in new[] { false, true })
            {
                string target = segment + (noGood ? " 보정 0회" : " 무실수");
                string badSegment = inside ? segment : segment == "1장" ? "2장" : "1장";
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                bool injected = false;
                int judgements = 0, choices = 0;
                core.Enter("복기 재도전\n재도전 목표: " + target);
                for (int step = 0; !run.Stopped && step < 100; step++)
                {
                    if (run.ChoiceKind != GreedLastChoiceKind.None)
                    {
                        core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
                        choices++;
                        continue;
                    }
                    if (run.StartCountdownActive)
                    {
                        core.Tick(clock += 2f);
                        core.Tick(clock += 1f);
                    }
                    if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                    Require(run.ActivePattern, "The scope fixture must drive actual normal traps without shortcuts.");
                    string currentSegment = run.Phase == GreedLastRunPhase.EscapeRunning
                        ? "탈출" : run.ChapterIndex + "장";
                    bool bad = !injected && currentSegment == badSegment;
                    float delta = bad && noGood ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                    clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - core.InputTimingOffsetSeconds + delta;
                    GreedLastRunChannel channel = bad && !noGood
                        ? run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left
                        : run.Pattern.Channel;
                    core.HandleInput(channel, clock);
                    Require(run.LastResult == (bad ? noGood ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Miss
                        : GreedLastJudgementResult.Success), "The fixture must produce the requested actual judgement.");
                    judgements++;
                    if (bad)
                    {
                        injected = true;
                        if (!inside && segment != "1장")
                        {
                            string earlyProfile = core.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned).TimingProfile;
                            string earlyResult = (string)typeof(GreedLastRuntimeUi).GetMethod("BuildReviewTargetSettlementText",
                                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });
                            Require(earlyProfile.Contains("복기 결과 진행 구간 대기", StringComparison.Ordinal)
                                && earlyResult == "진행 구간 대기", "An unvisited named segment must not settle as achieved.");
                        }
                        string hud = GreedLastRuntimeUi.BuildReviewTargetHudLine(run);
                        string feedback = GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(run);
                        bool failure = (bool)typeof(GreedLastRuntimeUi).GetMethod("IsReviewTargetFailureSnapshot",
                            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });
                        if (failure != inside || (!run.Stopped && (hud.Contains("목표 실패", StringComparison.Ordinal) != inside
                            || feedback.Contains("목표 실패", StringComparison.Ordinal) != inside)))
                            failures.Add(target + "/" + badSegment + "/live");
                    }
                }
                Require(injected && run.Stopped && run.EscapeSucceeded && judgements == 15 && choices == 8
                    && run.SuccessCount == 14 && run.GoodCount == (noGood ? 1 : 0) && run.MissCount == (noGood ? 0 : 1),
                    "Every scope case must retain a real full clear, eight choices and all fifteen unchanged judgements.");
                string expected = inside ? noGood ? "실패 보정 1" : "실패 실수 1"
                    : noGood ? "달성 보정 0" : "달성 무실수";
                GreedLastRunRecord record = core.CreateRunRecord();
                string settled = GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(run);
                if (!record.TimingProfile.Contains("복기 결과 " + expected, StringComparison.Ordinal)
                    || !settled.Contains(" / " + expected, StringComparison.Ordinal))
                    failures.Add(target + "/" + badSegment + "/settled");
                core.Exit();
                cases++;
            }
            CheckIncompleteNormalScopes(failures);
            CheckInfiniteScopes(failures);
            CheckWholeRunAndPracticeControls();
            Require(failures.Count == 0, "Chapter review goals must ignore other segments: " + string.Join(" | ", failures));
            Debug.Log("Greed chapter review scope passed: " + cases + " actual full normal clears, live HUD/feedback/failure state"
                + " and final profile/result agree with the named chapter or escape; partial normal/infinite scopes stay pending,"
                + " four first-infinite-section goals survive later natural failure, result/preparation handoff and reset;"
                + " whole-run and practice controls remain global. No preferences, desktop input or device proof.");
        }

        private static void CheckIncompleteNormalScopes(List<string> failures)
        {
            foreach (bool noGood in new[] { false, true })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.Enter("복기 재도전\n재도전 목표: 1장 " + (noGood ? "보정 0회" : "무실수"));
                core.HandleInput(GreedLastRunChannel.Center, clock);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Tap(core, ref run, ref clock, false, noGood);
                Require(run.ChapterProgress == 1 && !run.Stopped, "The normal partial control must play exactly one trap.");
                string profile = core.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned).TimingProfile;
                if (!profile.Contains("복기 결과 진행 구간 미완료", StringComparison.Ordinal) || Settlement(run) != "진행 구간 미완료")
                    failures.Add("partial-normal/" + noGood);
                core.Exit();
            }
        }

        private static void CheckInfiniteScopes(List<string> failures)
        {
            var stored = new GreedLastRunRecord(true, 100, 3, 0, 0, 3, 3, 0, 0, 20f,
                "scope-check", "박자 감각", "집중 축", string.Empty, "집중 축", string.Empty);
            foreach (bool noGood in new[] { false, true })
            foreach (bool firstError in new[] { false, true })
            {
                string context = "무한 복기 재도전\n목표: 다음 구간 " + (noGood ? "보정 0회" : "무실수");
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.EnterInfiniteRun(stored, context);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                for (int i = 0; i < 3; i++)
                {
                    if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                    Tap(core, ref run, ref clock, firstError && i == 0, noGood);
                    if (i == 0 && !firstError)
                    {
                        string partial = core.CreateInfiniteRunRecord().TimingProfile;
                        if (!partial.Contains("복기 결과 진행 구간 미완료", StringComparison.Ordinal) || Settlement(run) != "진행 구간 미완료")
                            failures.Add("partial-infinite/" + noGood);
                    }
                }
                Require(run.InfiniteSectionsCleared == 1 && !run.Stopped,
                    "The infinite scope fixture must finish exactly its actual first section.");
                core.RequestNextPattern(clock += 2f);
                Tap(core, ref run, ref clock, true, false);
                bool failed = (bool)typeof(GreedLastRuntimeUi).GetMethod("IsReviewTargetFailureSnapshot",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });
                if (failed != firstError) failures.Add("infinite-later-error/" + noGood + "/" + firstError);
                for (int tick = 0; !run.Stopped && tick < 10000; tick++) core.Tick(clock += 0.05f);
                Require(run.Stopped && run.Health == 0 && run.MissCount > 0 && !run.DevInvincible,
                    "The infinite goal must settle after natural failure, not an artificial test stop.");
                string expected = firstError ? noGood ? "실패 보정 1" : "실패 실수 1"
                    : noGood ? "달성 보정 0" : "달성 무실수";
                GreedLastInfiniteRunRecord record = core.CreateInfiniteRunRecord();
                if (!record.TimingProfile.Contains("복기 결과 " + expected, StringComparison.Ordinal)
                    || !GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(run).Contains(expected, StringComparison.Ordinal))
                    failures.Add("infinite-natural-result/" + noGood + "/" + firstError);
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                Require(backend.RegisterInfiniteRun(record), "A naturally played scoped result must register once.");
                int points = backend.LoadLobby().GrowthPoints;
                string message = backend.BuildInfiniteRunResultMessage(record);
                string prep = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(message);
                Require(message.Contains(" / " + expected, StringComparison.Ordinal) && prep.Contains(" / " + expected, StringComparison.Ordinal),
                    "The actual backend result and next preparation must retain the same scoped outcome.");
                if (!firstError)
                    Require(prep.Contains("목표: " + (noGood ? "정박 3연속" : "다음 구간 보정 0회"), StringComparison.Ordinal),
                        "A completed first-section goal must advance through the actual preparation context.");
                string repeatedMessage = backend.BuildInfiniteRunResultMessage(record);
                string repeatedPrep = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(repeatedMessage);
                Require(repeatedMessage == message && repeatedPrep == prep && backend.LoadLobby().GrowthPoints == points,
                    "Repeated result reads must preserve the same handoff without awarding more growth.");
                core.EnterInfiniteRun(stored, context);
                if (Settlement(run) != "진행 구간 대기") failures.Add("infinite-restart/" + noGood + "/" + firstError);
                core.Exit();
            }
        }

        private static void CheckWholeRunAndPracticeControls()
        {
            foreach (bool practice in new[] { false, true })
            foreach (bool noGood in new[] { false, true })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                string goal = (noGood ? "보정 0회" : "무실수");
                if (practice) core.EnterPractice("복기 연습\n재도전 목표: 1장 " + goal);
                else core.EnterInfiniteRun(new GreedLastRunRecord(true, 100, 3, 0, 0, 3, 3, 0, 0, 20f,
                    "global-scope-control", "박자 감각", "집중 축", string.Empty, "집중 축", string.Empty), "무한 복기 재도전\n목표: " + goal);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Tap(core, ref run, ref clock, true, noGood);
                string expected = noGood ? "실패 보정 1" : "실패 실수 1";
                Require(!run.ReviewTargetScoped && run.ReviewTargetGoodCount == run.GoodCount
                    && run.ReviewTargetMissCount == run.MissCount && Settlement(run) == expected,
                    "Short practice and unscoped infinite goals must retain their actual whole-run counters.");
                core.Exit();
            }
        }

        private static void Tap(GreedLastRunCore core, ref GreedLastRunSnapshot run, ref float clock, bool bad, bool noGood)
        {
            Require(run.ActivePattern, "A scope input must target a genuinely active trap.");
            float delta = bad && noGood ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
            clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - core.InputTimingOffsetSeconds + delta;
            GreedLastRunChannel channel = bad && !noGood
                ? run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left : run.Pattern.Channel;
            core.HandleInput(channel, clock);
            Require(run.LastResult == (bad ? noGood ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Miss
                : GreedLastJudgementResult.Success), "The scope control must preserve the actual judgement.");
        }

        private static string Settlement(GreedLastRunSnapshot run) => (string)typeof(GreedLastRuntimeUi)
            .GetMethod("BuildReviewTargetSettlementText", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
