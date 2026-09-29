using System;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeClockCheck
    {
        [MenuItem("Greed Last/Run Frame Clock Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Frame-clock checks require Edit mode.");
            VerifyCountdownOrigin();
            VerifyJudgementOrigin();
            float[][] frames = { new[] { 1f / 30f }, new[] { 1f / 60f }, new[] { 1f / 120f }, new[] { 0.008f, 0.04f, 0.017f, 0.025f } };
            foreach (float[] frameSteps in frames) VerifyFrames(frameSteps);
            Debug.Log("Greed frame clock passed: 30/60/120 Hz and uneven synthetic ticks, one publication on trap creation, coherent elapsed/beat/core delta, judgement gaps/stagger, ignored countdown inputs and long-pause lead-in. No preferences or saved slots changed.");
        }

        [MenuItem("Greed Last/Run Frame Clock Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void VerifyCountdownOrigin()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Frame-clock checks require Edit mode.");
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup + 3600f;
            core.RestartPractice(clock);
            Near(run.StartCountdownRemainingSeconds, 1.4f, "Opening countdown origin");
        }

        public static void VerifyJudgementOrigin()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Frame-clock checks require Edit mode.");
            for (int outcome = 0; outcome < 4; outcome++)
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                int publications = 0;
                core.SnapshotChanged += value => { run = value; publications++; };
                float clock = Time.realtimeSinceStartup + 3600f;
                core.RestartPractice(clock);
                core.HandleInput(GreedLastRunChannel.Center, clock += 0.2f);
                Near(run.StartCountdownRemainingSeconds, 1.2f, "Ignored opening input countdown");
                core.Tick(clock += 2f);
                core.Tick(clock += 0.3f);
                Require(run.ActivePattern, "Judgement clock fixture needs an active practice trap.");
                GreedLastPatternModel pattern = run.Pattern;
                float signedDelta = outcome == 1 ? -(pattern.SuccessWindowSeconds + pattern.GoodWindowSeconds) * 0.5f : 0f;
                float elapsed = outcome == 3 ? pattern.HitSeconds + 0.05f
                    : pattern.CoreSeconds + signedDelta - core.InputTimingOffsetSeconds;
                clock += elapsed;
                int health = run.Health;
                int before = publications;
                if (outcome == 3) core.Tick(clock);
                else core.HandleInput(outcome == 2 ? GreedLastRunChannel.None : pattern.Channel, clock);
                GreedLastJudgementResult expected = outcome == 0 ? GreedLastJudgementResult.Success
                    : outcome == 1 ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Miss;
                Require(!run.ActivePattern && run.LastResult == expected && publications == before + 1,
                    "Each input or timeout must resolve once and publish its final judgement once.");
                if (outcome >= 2)
                {
                    Require(run.Health == health - 1 && run.MissCount == 1, "A single miss must charge one health only.");
                    Near(run.StaggerRemainingSeconds, 0.55f, "Initial miss stagger");
                }
                core.Tick(clock += 0.1f);
                Require(!run.ActivePattern, "The next trap must respect its gap after the supplied judgement time.");
                if (outcome >= 2) Near(run.StaggerRemainingSeconds, 0.45f, "Miss stagger progress");
                core.TogglePause(clock);
                float stagger = run.StaggerRemainingSeconds;
                float distance = run.Distance;
                int score = run.Score;
                int resolvedHealth = run.Health;
                int resolvedCount = run.SuccessCount + run.GoodCount + run.MissCount;
                core.Tick(clock += 3600f);
                core.TogglePause(clock);
                core.HandleInput(pattern.Channel, clock += 0.2f);
                Near(run.ResumeCountdownRemainingSeconds, 1.2f, "Ignored resume input countdown");
                Require(run.Paused && !run.ActivePattern && run.Distance == distance && run.Score == score
                    && run.Health == resolvedHealth && run.SuccessCount + run.GoodCount + run.MissCount == resolvedCount,
                    "Ignored resume input must preserve the resolved trap and distance.");
                Near(run.StaggerRemainingSeconds, stagger, "Paused miss stagger");
                core.Tick(clock += 1.3f);
                Require(!run.Paused && !run.ActivePattern, "Resume should preserve the inter-trap gap.");
                Near(run.StaggerRemainingSeconds, stagger, "Restored miss stagger");
                core.Tick(clock += 0.1f);
                Require(!run.ActivePattern, "A resumed gap must include the pre-trap buffer.");
                core.Tick(clock += 2f);
                Require(run.ActivePattern && !run.Staggered, "The next trap should eventually start after the preserved gap and stagger.");
                Near(run.ElapsedSeconds, 0f, "Post-judgement trap origin");
            }
        }

        private static void VerifyFrames(float[] frameSteps)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            int publications = 0;
            core.SnapshotChanged += value => { run = value; publications++; };
            float clock = Time.realtimeSinceStartup + 3600f;
            core.RestartPractice(clock);
            core.Tick(clock += 2f);
            int beforeSpawn = publications;
            core.Tick(clock += 0.3f);
            Require(run.ActivePattern && publications == beforeSpawn + 1,
                "Creating the next auto-flow trap must publish once, not render the same frame twice.");
            float patternStart = clock;
            Require(Mathf.Abs(run.ElapsedSeconds) < 0.002f,
                "The first trap snapshot must use its supplied start time, not a fresh wall-clock read.");
            for (int step = 0; step < 9; step++)
            {
                clock += frameSteps[step % frameSteps.Length];
                int before = publications;
                core.Tick(clock);
                float elapsed = clock - patternStart;
                Require(run.ActivePattern && publications == before + 1, "An ordinary live tick must publish one current snapshot.");
                Near(run.ElapsedSeconds, elapsed, "Elapsed time");
                Near(run.BeatProgress, Mathf.Clamp01(elapsed / run.Pattern.HitSeconds), "Beat progress");
                Near(run.CoreDeltaSeconds, elapsed - run.Pattern.CoreSeconds, "Timing-guide delta");
            }
            core.TogglePause(clock);
            float pausedElapsed = run.ElapsedSeconds;
            float distance = run.Distance;
            int health = run.Health;
            int beforeWait = publications;
            core.Tick(clock += 3600f);
            Require(run.Paused && publications == beforeWait && run.Distance == distance && run.Health == health,
                "A long pause must preserve both gameplay and its last presentation.");
            core.TogglePause(clock);
            Near(run.ResumeCountdownRemainingSeconds, 1.4f, "Resume opening countdown");
            core.Tick(clock += 0.3f);
            Near(run.ResumeCountdownRemainingSeconds, 1.1f, "Resume countdown progress");
            core.Tick(clock += 1.2f);
            Require(!run.Paused && run.ActivePattern && run.Distance == distance,
                "Completing the resume countdown must not move or replace the paused trap.");
            Near(run.ElapsedSeconds, pausedElapsed - 0.65f, "Restored pre-trap lead-in");
            float resumedElapsed = run.ElapsedSeconds;
            float resumeAt = clock;
            core.Tick(clock += 0.05f);
            Near(run.ElapsedSeconds, resumedElapsed + clock - resumeAt, "First resumed frame");
            Require(run.Distance >= distance && run.Distance - distance < 1f && run.Health == health,
                "The resumed frame must charge only its own movement time and no idle damage.");

            core.RestartPractice(clock += 3f);
            Near(run.StartCountdownRemainingSeconds, 1.4f, "Practice opening countdown");
            core.Tick(clock += 0.3f);
            Near(run.StartCountdownRemainingSeconds, 1.1f, "Practice countdown progress");
            core.PauseForAppBackground(clock);
            core.Tick(clock += 3600f);
            Near(run.StartCountdownRemainingSeconds, 1.1f, "Suspended opening countdown");
            core.TogglePause(clock);
            core.Tick(clock += 1.5f);
            Require(!run.Paused && run.StartCountdownActive, "Resume must return to the unfinished opening countdown.");
            Near(run.StartCountdownRemainingSeconds, 1.1f, "Opening countdown after return");
            core.Tick(clock += 0.2f);
            Near(run.StartCountdownRemainingSeconds, 0.9f, "Opening countdown after first resumed tick");
            core.Enter();
            core.HandleInput(GreedLastRunChannel.Center, clock);
            Near(run.StartCountdownRemainingSeconds, 1.4f, "Normal start-choice countdown");
            core.Tick(clock += 0.2f);
            Near(run.StartCountdownRemainingSeconds, 1.2f, "Normal countdown progress");
        }

        private static void Near(float actual, float expected, string context)
            => Require(Mathf.Abs(actual - expected) < 0.003f, context + " must use the caller's frame time: " + actual + " vs " + expected);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Greed frame clock: " + message);
        }
    }
}
