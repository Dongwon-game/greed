using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeIdleCheck
    {
        private const int SampleFrames = 120;

        public static void Measure() => Execute(false);

        [MenuItem("Greed Last/Run Idle Presentation Check")]
        public static void Run() => Execute(true);

        [MenuItem("Greed Last/Run Idle Presentation Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Execute(bool enforce)
        {
            Require(CanRun(), "Idle checks require an isolated Edit-mode fixture.");
            GameObject previousCamera = GameObject.Find("GreedLastCamera");
            GameObject previousEvents = GameObject.Find("GreedLastEventSystem");
            GameObject host = new GameObject("GreedLastIdleCheck");
            GameObject canvas = null;
            try
            {
                GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui);
                canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                ui.ToggleDevCleanView();
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                    GreedLastScreenState.RunCoreTest, "Idle check", string.Empty, 0));
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                int publications = 0;
                core.SnapshotChanged += value =>
                {
                    publications++;
                    run = value;
                    ui.RenderRun(value);
                };
                float clock = Time.realtimeSinceStartup;
                void Idle(string name)
                {
                    GreedLastRunSnapshot before = run;
                    int startCount = publications;
                    long startTime = System.Diagnostics.Stopwatch.GetTimestamp();
                    for (int frame = 0; frame < SampleFrames; frame++) core.Tick(clock += 1f / 60f);
                    double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - startTime)
                        * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    Debug.Log($"Greed idle measurement: {name}, frames={SampleFrames}, publications={publications - startCount}, elapsedMs={milliseconds:F3}. Core tick + subscribed UI only; allocations and device frame timing not measured.");
                    if (!enforce) return;
                    Require(publications == startCount, name + " must not rebuild an unchanged run presentation every frame.");
                    Require(run.Equals(before), name + " must preserve the published run state while waiting.");
                }
                void StartPractice()
                {
                    clock = Time.realtimeSinceStartup;
                    core.RestartPractice(clock);
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                    Require(run.ActivePattern, "The idle fixture needs a live practice trap.");
                }
                void WaitAnHour(string name)
                {
                    GreedLastRunSnapshot before = run;
                    int startCount = publications;
                    core.Tick(clock += 3600f);
                    Require(run.Equals(before) && publications == startCount,
                        name + " must preserve its state through a simulated hour without redundant publication.");
                }
                void CheckFirstLiveStep(float distanceBefore, int healthBefore)
                {
                    core.Tick(clock += 1f / 60f);
                    Require(run.Health == healthBefore && run.Distance >= distanceBefore
                        && run.Distance - distanceBefore < 1f,
                        "The first live tick must not charge idle time as distance or damage.");
                }

                core.Enter();
                Require(run.ChoiceActive, "The initial choice must be published before any Tick.");
                Idle("starting-choice");
                if (enforce) WaitAnHour("starting-choice");
                int beforeChoice = publications;
                core.HandleInput(GreedLastRunChannel.Center, clock);
                Require(publications > beforeChoice && run.StartCountdownActive,
                    "Choosing after an idle menu must immediately publish the opening countdown.");
                float initialDistance = run.Distance;
                int initialHealth = run.Health;
                core.Tick(clock += 2f);
                CheckFirstLiveStep(initialDistance, initialHealth);
                StartPractice();
                core.TogglePause(clock += 0.1f);
                Require(run.Paused, "Pause must publish before an idle tick.");
                Idle("paused-practice");
                if (enforce) WaitAnHour("paused-practice");
                float pausedDistance = run.Distance;
                int pausedHealth = run.Health;
                float pausedElapsed = run.ElapsedSeconds;
                int beforeCommand = publications;
                core.ToggleDevInvincible();
                Require(publications > beforeCommand && run.DevInvincible && run.Paused,
                    "An explicit state-changing command must still publish while paused.");
                core.ToggleDevInvincible();
                core.TogglePause(clock);
                float firstCountdown = run.ResumeCountdownRemainingSeconds;
                int beforeCountdown = publications;
                core.Tick(clock += 0.25f);
                Require(publications > beforeCountdown && run.ResumeCountdownActive
                    && run.ResumeCountdownRemainingSeconds < firstCountdown,
                    "Resume countdown progress must continue publishing, unlike an idle pause.");
                core.Tick(clock += 2f);
                Require(!run.Paused && run.ActivePattern, "Resume must restore the same live trap.");
                Require(run.ElapsedSeconds < pausedElapsed,
                    "A long pause must still restore the pre-trap lead-in, not only skip elapsed distance.");
                CheckFirstLiveStep(pausedDistance, pausedHealth);

                StartPractice();
                for (int trap = 0; trap < 3; trap++)
                {
                    if (trap > 0) core.RequestNextPattern(clock += 2f);
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                    Require(run.LastResult == GreedLastJudgementResult.Success, "Practice result needs three exact inputs.");
                }
                Require(run.Stopped && run.PracticeMode, "Practice completion must publish its result immediately.");
                Idle("practice-result");
                if (enforce) WaitAnHour("practice-result");

                core.EnterInfiniteRun();
                clock = Time.realtimeSinceStartup;
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                core.ForceStopInfiniteRunForTest();
                Require(run.Stopped && run.InfiniteMode, "Infinite termination must publish before idle.");
                Idle("infinite-result");
                if (enforce) WaitAnHour("infinite-result");

                StartPractice();
                typeof(GreedLastRunCore).GetField("health", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(core, 1);
                GreedLastRunChannel wrong = run.Pattern.Channel == GreedLastRunChannel.Left
                    ? GreedLastRunChannel.Right : GreedLastRunChannel.Left;
                core.HandleInput(wrong, clock + run.Pattern.CoreSeconds);
                Require(run.Stopped && run.Staggered, "The terminal-hit fixture must begin with a short stagger.");
                core.Tick(clock += 3f);
                if (enforce) Require(!run.Staggered && run.StaggerRemainingSeconds == 0f,
                    "The final hit's transient state must expire before terminal ticks become quiet.");
                Idle("failed-result");
                if (enforce) WaitAnHour("failed-result");
                StartPractice();
                Require(!run.Stopped && !run.Staggered && run.Score == 0
                    && run.MissCount == 0 && run.GoodCount == 0 && run.SuccessCount == 0,
                    "Restarting after a long-held failure result must clear damage and previous judgement totals.");
                core.Exit();
                Idle("exited-core");
                if (enforce) Debug.Log("Greed Last idle presentation checks passed: six quiet states, simulated one-hour choice/pause/result waits, immediate commands, resume countdown/lead-in, terminal-hit expiry, bounded first live distance and fresh restart. No player settings or saved slots changed.");
            }
            finally
            {
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                DestroyCreated("GreedLastCamera", previousCamera);
                DestroyCreated("GreedLastEventSystem", previousEvents);
            }
        }

        private static T Read<T>(object target, string field)
            => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void DestroyCreated(string name, GameObject existing)
        {
            GameObject created = GameObject.Find(name);
            if (existing == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }
    }
}
