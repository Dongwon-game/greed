using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeTimelineCheck
    {
        [MenuItem("Greed Last/Run Continuous Timeline Check")]
        public static void Run()
        {
            Require(CanRun(), "Continuous timeline checks require Edit mode.");
            float[][] schedules = { new[] { 1f / 30f }, new[] { 1f / 60f }, new[] { 1f / 120f },
                new[] { 0.008f, 0.04f, 0.017f, 0.025f } };
            foreach (float[] schedule in schedules)
            {
                VerifyRun(0, schedule, GreedLastRunRecord.Empty);
                GreedLastRunRecord normal = VerifyRun(1, schedule, GreedLastRunRecord.Empty);
                VerifyRun(2, schedule, normal);
            }
            Debug.Log("Greed continuous timeline passed: 12 complete synthetic timelines, practice/normal clear/normal-record infinite, natural failure, recurring pause/background interruption and restart. No private clock edits, skipped traps, settings writes or player slot consumption. Device performance is not measured.");
        }

        [MenuItem("Greed Last/Run Continuous Timeline Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static GreedLastRunRecord VerifyRun(int mode, float[] schedule, GreedLastRunRecord loadout)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            int publications = 0;
            var freshProfile = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), core,
                typeof(GreedLastRunCore).GetMethod("BuildTimingProfileText", BindingFlags.Instance | BindingFlags.NonPublic));
            core.SnapshotChanged += value =>
            {
                Require(value.TimingProfile == freshProfile(),
                    "Every cached publication must match a freshly calculated timing profile.");
                run = value;
                publications++;
            };
            float clock = Time.realtimeSinceStartup;
            if (mode == 0) core.RestartPractice(clock);
            else if (mode == 1) core.Enter();
            else core.EnterInfiniteRun(loadout);
            int frames = 0;
            int choices = 0;
            int pauses = 0;
            int countdowns = 0;
            int correct = 0;
            bool sawCore = false;

            float NextFrame() => schedule[frames % schedule.Length];
            int Judgements() => run.SuccessCount + run.GoodCount + run.MissCount;

            void Step(float seconds)
            {
                GreedLastRunSnapshot before = run;
                int beforePublications = publications;
                clock += seconds;
                core.Tick(clock);
                frames++;
                Require(frames < 100000, "A timeline must make bounded progress.");
                Require(publications - beforePublications <= 1, "A timeline tick may publish at most once.");
                Require(!float.IsNaN(run.Distance) && !float.IsInfinity(run.Distance)
                    && run.Distance >= before.Distance, "Run distance must remain finite and monotonic.");
                if (Judgements() > before.SuccessCount + before.GoodCount + before.MissCount)
                    Require(run.LastJudgementScoreGain == run.Score - before.Score
                        && run.LastJudgementHealthChange == run.Health - before.Health
                        && !run.HasJudgedInputTiming,
                        "Natural timeout payload must match actual state changes without fabricated input timing.");
                if (before.Paused || before.StartCountdownActive || before.ChoiceActive || before.Stopped)
                    Require(run.Distance == before.Distance && run.Health == before.Health,
                        "Paused/countdown/choice/result time must not charge movement or damage.");
            }

            void Advance(float seconds)
            {
                float remaining = Mathf.Max(0f, seconds);
                while (remaining > 0.0001f)
                {
                    float step = Mathf.Min(remaining, NextFrame());
                    Step(step);
                    remaining -= step;
                }
            }

            void PauseCycle()
            {
                string patternId = run.Pattern.PatternId;
                bool active = run.ActivePattern;
                float elapsed = run.ElapsedSeconds;
                float startRemaining = run.StartCountdownRemainingSeconds;
                float distance = run.Distance;
                float focusRemaining = run.FocusStateRemainingSeconds;
                int health = run.Health;
                int score = run.Score;
                int count = Judgements();
                core.PauseForAppBackground(clock);
                Require(run.Paused, "Background interruption must pause a live run or opening countdown.");
                Step(pauses == 0 ? 3600f : 15f);
                core.TogglePause(clock);
                Advance(0.3f);
                Require(run.ResumeCountdownActive, "Resume must start with a countdown.");
                core.PauseForAppBackground(clock);
                Require(run.Paused && !run.ResumeCountdownActive, "A second interruption must cancel the resume countdown.");
                Step(30f);
                core.TogglePause(clock);
                core.TogglePause(clock);
                Advance(1.5f);
                Require(!run.Paused && !run.ResumeCountdownActive, "One completed resume countdown must restore play.");
                // Only the final overrun after resuming is allowed to move; no wait time is charged.
                Require(run.Distance >= distance && run.Distance - distance < 0.6f
                    && run.Health == health && run.Score == score && Judgements() == count,
                    "Repeated interruptions must not move a long distance, judge or damage the player.");
                Require(run.FocusStateRemainingSeconds <= focusRemaining + 0.002f
                    && focusRemaining - run.FocusStateRemainingSeconds < 0.2f,
                    "Focus duration must not expire during paused/countdown time.");
                Require(run.ActivePattern == active && (!active || run.Pattern.PatternId == patternId),
                    "Resume must preserve the pending trap.");
                if (active)
                    Require(run.ElapsedSeconds >= elapsed - 0.67f && run.ElapsedSeconds < elapsed - 0.5f,
                        "Every restored live trap must retain its pre-trap lead-in.");
                if (startRemaining > 0f)
                    Require(run.StartCountdownActive && run.StartCountdownRemainingSeconds <= startRemaining + 0.002f
                        && startRemaining - run.StartCountdownRemainingSeconds < 0.2f,
                        "Interrupted opening countdown must preserve its unfinished duration.");
                pauses++;
            }

            for (int cycle = 0; cycle < 256 && !run.Stopped; cycle++)
            {
                if (run.ChoiceActive)
                {
                    int count = Judgements();
                    Step(choices == 0 ? 3600f : 20f);
                    Require(run.ChoiceActive && Judgements() == count, "Waiting on a choice must preserve the run.");
                    GreedLastRunChannel channel = choices % 3 == 0 ? GreedLastRunChannel.Center
                        : choices % 3 == 1 ? GreedLastRunChannel.Left : GreedLastRunChannel.Right;
                    core.HandleInput(channel, clock);
                    Require(++choices < 24, "Normal choices must eventually return to play.");
                    continue;
                }
                if (run.StartCountdownActive)
                {
                    PauseCycle();
                    Advance(run.StartCountdownRemainingSeconds + 0.05f);
                    Require(!run.CountdownActive && !run.Paused, "Opening countdown must finish without skipping a trap.");
                    countdowns++;
                }
                int waitingFrames = 0;
                while (!run.ActivePattern && !run.Stopped && !run.ChoiceActive && !run.CountdownActive)
                {
                    Step(NextFrame());
                    Require(++waitingFrames < 1000, "The next scheduled trap must arrive within a bounded gap.");
                }
                if (run.Stopped || run.ChoiceActive || run.CountdownActive) continue;
                if (correct % 3 == 0) PauseCycle();
                if (mode == 2 && correct >= 30)
                {
                    Advance(Mathf.Max(0f, run.Pattern.HitSeconds - run.ElapsedSeconds) + 0.05f);
                    Require(run.MissReason == "no_input", "Unanswered infinite traps must produce one natural miss.");
                    continue;
                }
                float untilInput = run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                Require(untilInput >= -0.01f, "An automatic trap must arrive with enough time to respond.");
                Advance(untilInput);
                Require(run.ActivePattern, "The trap must remain live until its authored correct timing.");
                int beforeCount = Judgements();
                int beforeScore = run.Score;
                int beforeHealth = run.Health;
                float expectedInputDelta = run.CoreDeltaSeconds + run.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Success && Judgements() == beforeCount + 1,
                    "A scheduled exact input must resolve once without a hidden wall-clock dependency.");
                Require(run.LastJudgementScoreGain == run.Score - beforeScore
                    && run.LastJudgementHealthChange == run.Health - beforeHealth
                    && run.HasJudgedInputTiming
                    && Mathf.Abs(run.LastInputTimingDeltaSeconds - expectedInputDelta) < 0.002f,
                    "Every judgement must retain actual timing and include chain, section and final-bundle rewards exactly once."
                        + $" mode={mode}, count={beforeCount}, score={run.LastJudgementScoreGain}/{run.Score - beforeScore},"
                        + $" health={run.LastJudgementHealthChange}/{run.Health - beforeHealth}, timing={run.LastInputTimingDeltaSeconds}/{expectedInputDelta}.");
                correct++;
                sawCore |= run.CoreRetrieved;
            }
            Require(run.Stopped, "The complete timeline must reach its real result screen.");
            GreedLastRunRecord record = GreedLastRunRecord.Empty;
            if (mode == 0)
                Require(core.IsPracticeComplete && correct == 3 && !run.SaveEligible,
                    "Practice must complete its three authored traps without granting a saved combination.");
            else if (mode == 1)
            {
                record = core.CreateRunRecord();
                Require(run.EscapeSucceeded && run.SaveEligible && sawCore && correct == 15
                    && record.IsValid && record.SuccessCount == correct && record.Score == run.Score,
                    "Normal play must clear all chapters and escape, preserving its final save candidate.");
            }
            else
            {
                GreedLastInfiniteRunRecord infinite = core.CreateInfiniteRunRecord();
                Require(run.Health == 0 && run.InfiniteSectionsCleared >= 10 && run.MaxInfiniteThreatLevel == 5
                    && infinite.IsValid && infinite.Score == run.Score && !run.SaveEligible,
                    "Infinite play must reach maximum threat and naturally fail with a valid non-saveable record.");
            }
            int finalCount = Judgements();
            int finalScore = run.Score;
            Step(3600f);
            Require(run.Stopped && Judgements() == finalCount && run.Score == finalScore,
                "Long result-screen waits must not add another judgement or reward.");
            core.RestartPractice(clock);
            Require(run.StartCountdownActive && !run.Stopped && !run.InfiniteMode && run.Score == 0
                && Judgements() == 0 && run.Distance == 0f && run.Health > 0,
                "Fresh practice after a completed timeline must not inherit old result state.");
            Debug.Log($"Greed continuous timeline: mode={mode}, frameProfile={schedule[0]:F4}/{schedule.Length}, frames={frames}, exact={correct}, choices={choices}, countdowns={countdowns}, interruptions={pauses}.");
            return record;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Greed continuous timeline: " + message);
        }
    }
}
