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
            Require(failures.Count == 0, "Chapter review goals must ignore other segments: " + string.Join(" | ", failures));
            Debug.Log("Greed chapter review scope passed: " + cases + " actual full normal clears, live HUD/feedback/failure state"
                + " and final profile/result agree with the named chapter or escape; gameplay counts unchanged. No preferences, desktop input or device proof.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
