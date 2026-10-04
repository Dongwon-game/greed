using System;
using System.Reflection;
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
            VerifyCalibratedInputDeadlines();
            VerifyFrameInterruption();
            float[][] frames = { new[] { 1f / 30f }, new[] { 1f / 60f }, new[] { 1f / 120f }, new[] { 0.008f, 0.04f, 0.017f, 0.025f } };
            foreach (float[] frameSteps in frames) VerifyFrames(frameSteps);
            Debug.Log("Greed frame clock passed: 30/60/120 Hz and uneven synthetic ticks, one publication on trap creation, coherent elapsed/beat/core delta, judgement gaps/stagger, ignored countdown inputs and long-pause lead-in. No preferences or saved slots changed.");
        }

        [MenuItem("Greed Last/Run Frame Clock Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Greed Last/Run Calibrated Input Deadline Check")]
        public static void VerifyCalibratedInputDeadlines()
        {
            Require(CanRun(), "Calibrated deadline checks require Edit mode.");
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var deck = (GreedLastPatternModel[])typeof(GreedLastRunCore)
                .GetField("patternDeck", fields).GetValue(new GreedLastRunCore());
            int cases = 0;
            foreach (GreedLastPatternModel source in deck)
            foreach (int mode in new[] { 0, 1, 2, 3, 4, 5 })
            foreach (int growth in new[] { 0, 3 })
            foreach (float offset in new[] { -0.2f, -0.02f, 0f, 0.02f, 0.2f })
            {
                GreedLastRunSnapshot run = default;
                GreedLastRunCore Fresh()
                {
                    var core = new GreedLastRunCore();
                    core.ConfigureGrowth(growth, 0, growth);
                    core.SnapshotChanged += value => run = value;
                    core.RestartPractice(20f);
                    core.Tick(22f);
                    void Set(string name, object value) => typeof(GreedLastRunCore).GetField(name, fields).SetValue(core, value);
                    // Only isolated fixture state changes; never rewrite the personal calibration preference.
                    Set("inputTimingOffsetSeconds", offset);
                    Set("infiniteMode", mode >= 4);
                    Set("infiniteThreatTestOffset", mode == 5 ? 4 : 0);
                    Set("chapterIndex", mode == 2 ? 4 : 1);
                    Set("phase", mode == 3 ? GreedLastRunPhase.EscapeRunning : GreedLastRunPhase.RunPlaying);
                    Set("lastNode", mode == 1 ? "안정 루트" : mode == 2 ? "압박 루트" : string.Empty);
                    Set("devNextPatternOverride", source);
                    core.RequestNextPattern(23f);
                    Require(run.ActivePattern && run.Pattern.Channel == source.Channel,
                        "The deadline fixture must start the requested authored trap through the core.");
                    return core;
                }

                foreach (float deltaScale in new[] { 0f, 0.95f })
                {
                    GreedLastRunCore core = Fresh();
                    float delta = run.Pattern.GoodWindowSeconds * deltaScale;
                    float tapAt = 23f + run.Pattern.CoreSeconds - offset + delta;
                    float clock = 23f;
                    float step = cases % 3 == 0 ? 1f / 30f : cases % 3 == 1 ? 1f / 60f : 1f / 120f;
                    while (clock + step < tapAt) core.Tick(clock += step);
                    core.Tick(tapAt);
                    Require(run.ActivePattern && run.MissCount == 0,
                        $"A valid calibrated tap must survive preceding frame ticks: {source.PatternId}, mode={mode}, growth={growth}, offset={offset}, delta={delta}, elapsed={run.ElapsedSeconds}, reason={run.MissReason}.");
                    int beforeScore = run.Score;
                    core.HandleInput(run.Pattern.Channel, tapAt);
                    Require(run.LastResult == (deltaScale == 0f ? GreedLastJudgementResult.Success : GreedLastJudgementResult.Good)
                        && run.LastJudgementScoreGain == run.Score - beforeScore
                        && Mathf.Abs(run.LastInputTimingDeltaSeconds - delta) < 0.00001f,
                        "Preserving a calibrated input window must not change its result, actual reward or signed timing.");
                    core.HandleInput(source.Channel, tapAt);
                    Require(run.SuccessCount + run.GoodCount == 1 && run.MissCount == 0,
                        "An extended deadline must still resolve exactly one tap.");
                    cases++;
                }

                GreedLastRunCore timeoutCore = Fresh();
                float deadline = Mathf.Max(run.Pattern.HitSeconds,
                    run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - offset);
                int health = run.Health;
                timeoutCore.Tick(23f + deadline - 0.001f);
                Require(run.ActivePattern && run.Health == health && run.MissCount == 0,
                    "No-input resolution must wait for both authored impact and the calibrated late Good edge.");
                timeoutCore.Tick(23f + deadline + 0.001f);
                Require(!run.ActivePattern && run.MissCount == 1 && run.MissReason == "no_input"
                    && !run.HasJudgedInputTiming && run.Health == health - 1,
                    "Passing the complete calibrated window without a tap must charge one real no-input miss.");
                cases++;
            }
            Debug.Log($"Greed calibrated deadlines passed: {cases} exact/late-Good/no-input cases across the authored deck, six tempo/route modes, timing growth and five offsets; 30/60/120 Hz preceding ticks. No personal preferences changed.");
        }

        [MenuItem("Greed Last/Run Frame Interruption Check")]
        public static void VerifyFrameInterruption()
        {
            Require(CanRun(), "Frame interruption checks require Edit mode.");
            foreach (int mode in new[] { 0, 1, 2, 3 })
            foreach (float gap in new[] { 0.501f, 0.75f, 30f, 3600f })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = 16f;
                core.RestartPractice(clock);
                if (mode != 0)
                {
                    core.Tick(clock += 2f);
                    core.RequestNextPattern(clock += 1f);
                    core.FillFocusForTest();
                    core.Tick(clock += 0.125f);
                    if (mode == 2) core.HandleInput(run.Pattern.Channel,
                        clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - core.InputTimingOffsetSeconds);
                }
                if (mode == 3)
                {
                    core.TogglePause(clock);
                    core.TogglePause(clock);
                    core.Tick(clock += 0.125f);
                }
                GreedLastRunSnapshot frozen = run;
                float lastElapsed = run.ElapsedSeconds;
                float lastOpening = run.StartCountdownRemainingSeconds;
                Require(core.PauseForInterruptedFrame(clock += gap) && run.Paused && !run.ResumeCountdownActive,
                    "A large interactive frame gap must pause an opening, live trap, inter-trap gap or resume.");
                Require(run.ActivePattern == frozen.ActivePattern && run.Pattern.PatternId == frozen.Pattern.PatternId
                    && run.ElapsedSeconds == lastElapsed && run.StartCountdownRemainingSeconds == lastOpening
                    && run.Health == frozen.Health && run.Score == frozen.Score && run.MissCount == frozen.MissCount
                    && run.Distance == frozen.Distance && run.FocusStateRemainingSeconds == frozen.FocusStateRemainingSeconds
                    && run.FocusStateActiveSeconds == frozen.FocusStateActiveSeconds,
                    "Interrupting an unseen frame must preserve the complete last presented gameplay state.");
                Require(!core.PauseForInterruptedFrame(clock += gap),
                    "An idle pause must not repeatedly publish interruption messages.");
                core.TogglePause(clock);
                Require(run.ResumeCountdownActive, "A frame interruption must use the normal explicit resume action.");
                core.Tick(clock += 1.5f);
                Require(!run.Paused && run.Health == frozen.Health && run.Score == frozen.Score
                    && run.Distance == frozen.Distance && run.MissCount == frozen.MissCount,
                    "Resuming after a frame gap must not charge suspended movement, focus or damage.");
                if (frozen.ActivePattern) Near(run.ElapsedSeconds, lastElapsed - 0.65f, "Interrupted trap lead-in");
            }

            var ordinary = new GreedLastRunCore();
            GreedLastRunSnapshot value = default;
            ordinary.SnapshotChanged += snapshot => value = snapshot;
            ordinary.RestartPractice(16f);
            ordinary.Tick(18f);
            ordinary.RequestNextPattern(19f);
            ordinary.Tick(19f);
            Require(!ordinary.PauseForInterruptedFrame(19.5f), "The exact half-second threshold must not pause a frame.");
            ordinary.Tick(19.5f);
            Require(!value.Paused && value.ElapsedSeconds == 0.5f, "An accepted frame must retain ordinary progression.");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 18f })
                Require(!ordinary.PauseForInterruptedFrame(invalid), "Invalid or reversed timestamps must not fabricate a gap.");
            ordinary.Enter();
            Require(value.ChoiceActive && !ordinary.PauseForInterruptedFrame(10000f),
                "A choice menu must remain a menu after a long idle interval.");
            Debug.Log("Greed frame interruption passed: 16 opening/live/gap/resume cases, 0.501-second to one-hour synthetic interruptions, frozen gameplay and ordinary lead-in, exact threshold, invalid time rejection and choice isolation. Tick remains independently controllable by fixtures.");
        }

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
                float elapsed = outcome == 3 ? Mathf.Max(pattern.HitSeconds,
                    pattern.CoreSeconds + pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds) + 0.05f
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
