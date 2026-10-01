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
            VerifyLongInfiniteRun();
            Debug.Log("Greed continuous timeline passed: 12 complete synthetic timelines, practice/normal clear/normal-record infinite, natural failure, recurring pause/background interruption and restart. No private clock edits, skipped traps, settings writes or player slot consumption. Device performance is not measured.");
        }

        [MenuItem("Greed Last/Run Continuous Timeline Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static (GreedLastRunSnapshot before, GreedLastRunSnapshot after) VerifyLongInfiniteRun(
            Action<GreedLastRunSnapshot> observe = null)
        {
            const int targetInputs = 1200;
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            GreedLastRunSnapshot beforeFinalInput = default;
            GreedLastRunSnapshot afterFinalInput = default;
            int publications = 0;
            int frames = 0;
            float clock = Time.realtimeSinceStartup;
            var freshProfile = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), core,
                typeof(GreedLastRunCore).GetMethod("BuildTimingProfileText", BindingFlags.Instance | BindingFlags.NonPublic));
            FieldInfo recentCount = typeof(GreedLastRunCore).GetField("recentPatternCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            core.SnapshotChanged += value =>
            {
                if (++publications % 128 == 0)
                    Require(value.TimingProfile == freshProfile(), "Long-run timing profiles must stay current.");
                run = value;
                observe?.Invoke(value);
            };
            int Judgements() => run.SuccessCount + run.GoodCount + run.MissCount;
            void Step(float seconds)
            {
                GreedLastRunSnapshot before = run;
                int previousPublications = publications;
                clock += seconds;
                core.Tick(clock);
                Require(++frames < 150000 && publications - previousPublications <= 1,
                    "The long run must make bounded progress with at most one publication per tick.");
                Require(!float.IsNaN(run.Distance) && !float.IsInfinity(run.Distance)
                    && run.Distance >= before.Distance && run.Score >= before.Score,
                    "Long-run distance and score must remain finite, nonnegative and monotonic.");
                Require(run.InfiniteThreatLevel >= 1 && run.InfiniteThreatLevel <= 5
                    && run.MaxInfiniteThreatLevel <= 5 && (int)recentCount.GetValue(core) <= 4,
                    "Long-run threat and recent-pattern memory must remain bounded.");
                if (Judgements() > before.SuccessCount + before.GoodCount + before.MissCount)
                    Require(run.LastJudgementScoreGain == run.Score - before.Score
                        && run.LastJudgementHealthChange == run.Health - before.Health
                        && !run.HasJudgedInputTiming && run.LastJudgedInputChannel == GreedLastRunChannel.None,
                        "Long-run unanswered traps must not fabricate an input or reward.");
            }
            void Advance(float seconds)
            {
                float remaining = Mathf.Max(0f, seconds);
                while (remaining > 0.0001f)
                {
                    float step = Mathf.Min(remaining, 0.04f);
                    Step(step);
                    remaining -= step;
                }
            }
            void AwaitPattern()
            {
                int waiting = 0;
                while (!run.ActivePattern && !run.Stopped)
                {
                    Step(0.04f);
                    Require(++waiting < 150, "Long-run automatic traps must arrive without a shortcut command.");
                }
            }

            core.EnterInfiniteRun();
            Advance(run.StartCountdownRemainingSeconds + 0.05f);
            for (int input = 0; input < targetInputs; input++)
            {
                AwaitPattern();
                Require(run.ActivePattern && !run.Stopped && !run.ChoiceActive,
                    "Long-run correct play must remain live without development invincibility.");
                float untilInput = run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                Require(untilInput >= -0.01f, "Every long-run trap must retain a playable lead-in.");
                Advance(untilInput);
                GreedLastRunSnapshot before = run;
                float inputDelta = run.CoreDeltaSeconds + run.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Success && run.SuccessCount == input + 1
                    && run.GoodCount == 0 && run.MissCount == 0 && run.Combo >= before.Combo + 1
                    && run.LastJudgementScoreGain == run.Score - before.Score
                    && run.LastJudgementHealthChange == run.Health - before.Health
                    && run.HasJudgedInputTiming && run.LastJudgedInputChannel == before.Pattern.Channel
                    && Mathf.Abs(run.LastInputTimingDeltaSeconds - inputDelta) < 0.002f,
                    "Long-run exact inputs must resolve once and preserve actual rewards, lane, timing and combo."
                        + $" input={input + 1}, result={run.LastResult}, exact={run.SuccessCount}, good={run.GoodCount}, miss={run.MissCount},"
                        + $" combo={before.Combo}/{run.Combo}, score={run.LastJudgementScoreGain}/{run.Score - before.Score},"
                        + $" health={run.LastJudgementHealthChange}/{run.Health - before.Health}, delta={run.LastInputTimingDeltaSeconds}/{inputDelta}.");
                if (input == targetInputs - 1)
                {
                    beforeFinalInput = before;
                    afterFinalInput = run;
                }
            }
            Require(run.InfiniteSectionsCleared == targetInputs / 3 && run.MaxInfiniteThreatLevel == 5
                && run.Score > 100000 && run.Health > 0 && !run.SaveEligible,
                "1,200 real inputs must complete 400 infinite sections without creating a normal-run save candidate.");
            int unanswered = 0;
            while (!run.Stopped)
            {
                AwaitPattern();
                if (run.Stopped) break;
                int count = Judgements();
                Advance(run.Pattern.HitSeconds - run.ElapsedSeconds + 0.05f);
                Require(Judgements() == count + 1 && run.MissReason == "no_input" && ++unanswered <= 32,
                    "Long-run natural failure must resolve each unanswered trap once, including earned protection.");
            }
            GreedLastInfiniteRunRecord record = core.CreateInfiniteRunRecord();
            Require(run.Health == 0 && record.IsValid && record.Score == run.Score
                && record.SuccessCount == targetInputs && record.MaxCombo == afterFinalInput.Combo
                && record.SectionsCleared == run.InfiniteSectionsCleared,
                "A long run must finish with an intact record of its real cumulative counters.");
            VerifyLongRecordPersistence(record);
            int finalCount = Judgements();
            int finalScore = run.Score;
            float finalDistance = run.Distance;
            Step(3600f);
            Require(run.Stopped && Judgements() == finalCount && run.Score == finalScore && run.Distance == finalDistance,
                "An hour of synthetic result-screen waiting must not duplicate a long-run reward or record.");
            core.RestartPractice(clock);
            Require(run.StartCountdownActive && !run.InfiniteMode && run.Score == 0 && run.Combo == 0
                && Judgements() == 0 && run.Distance == 0f && !run.HasJudgedInputTiming
                && run.LastJudgedInputChannel == GreedLastRunChannel.None && (int)recentCount.GetValue(core) == 0,
                "Practice after a long run must clear cumulative judgement, input and recent-pattern state.");
            Debug.Log($"Greed long infinite timeline passed: {targetInputs} exact inputs, 400 completed sections,"
                + $" {unanswered} natural unanswered traps, {frames} synthetic ticks, score={record.Score},"
                + $" distance={record.Distance:F1}m. No private clock edits, trap shortcuts, invincibility or personal slot writes; not device endurance or performance measurement.");
            return (beforeFinalInput, afterFinalInput);
        }

        private static void VerifyLongRecordPersistence(GreedLastInfiniteRunRecord record)
        {
            string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "long-record-" + Guid.NewGuid().ToString("N");
            try
            {
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(backend.RegisterInfiniteRun(record), "A real long-run record must register in the isolated backend.");
                string saved = PlayerPrefs.GetString(key);
                string board = backend.BuildRecordBoardText(2);
                var reloaded = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                foreach (string field in new[] { "lastInfiniteRunRecord", "bestInfiniteRunRecord" })
                {
                    var restored = (GreedLastInfiniteRunRecord)typeof(GreedLastMockBackend).GetField(field,
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reloaded);
                    Require(restored.IsValid && restored.Score == record.Score
                        && Mathf.Abs(restored.Distance - record.Distance) < 0.01f
                        && restored.SectionsCleared == record.SectionsCleared
                        && restored.MaxCombo == record.MaxCombo && restored.MaxThreatLevel == record.MaxThreatLevel
                        && restored.SuccessCount == record.SuccessCount && restored.GoodCount == record.GoodCount
                        && restored.MissCount == record.MissCount && restored.MissReason == record.MissReason
                        && restored.TimingProfile == record.TimingProfile,
                        "Long-run JSON reload must preserve every real cumulative counter and timing profile: " + field);
                }
                var rankings = (GreedLastInfiniteRunRecord[])typeof(GreedLastMockBackend).GetField("infiniteRunRankings",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reloaded);
                var lobby = reloaded.LoadLobby();
                Require(rankings[0].Score == record.Score && rankings[0].MaxCombo == record.MaxCombo
                    && reloaded.BuildRecordBoardText(2) == board && PlayerPrefs.GetString(key) == saved
                    && !reloaded.HasSaveLoadoutCandidate && !lobby.SaveSlotOccupied[0]
                    && !lobby.SaveSlotOccupied[1] && !lobby.SaveSlotOccupied[2],
                    "A long infinite record must reload unchanged without fabricating a normal-save opportunity or slot.");
                Debug.Log("Greed long record persistence passed: recent/best/ranking JSON reload, full score/distance/counters/profile, stable record-board copy and no normal-save candidate. Dedicated temporary editor-test key only.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

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
                        && !run.HasJudgedInputTiming && run.LastJudgedInputChannel == GreedLastRunChannel.None,
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
                GreedLastRunChannel actualInput = run.Pattern.Channel;
                float expectedInputDelta = run.CoreDeltaSeconds + run.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Success && Judgements() == beforeCount + 1,
                    "A scheduled exact input must resolve once without a hidden wall-clock dependency.");
                Require(run.LastJudgementScoreGain == run.Score - beforeScore
                    && run.LastJudgementHealthChange == run.Health - beforeHealth
                    && run.HasJudgedInputTiming
                    && run.LastJudgedInputChannel == actualInput
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
