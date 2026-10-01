using System;
using System.Reflection;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeLiveCheck
    {
        private const int SampleFrames = 1800;

        [MenuItem("Greed Last/Run Live Publication Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Live checks require Edit mode.");
            var core = new GreedLastRunCore();
            var freshProfile = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), core,
                typeof(GreedLastRunCore).GetMethod("BuildTimingProfileText", BindingFlags.Instance | BindingFlags.NonPublic));
            GreedLastRunSnapshot run = default;
            int checks = 0;
            Action<GreedLastRunSnapshot> observe = value =>
            {
                Require(value.TimingProfile == freshProfile(), "Cached profile must match every current counter and target.");
                run = value;
                checks++;
            };
            core.SnapshotChanged += observe;
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            string profile = run.TimingProfile;
            string flow = run.RecentPatternFlow;
            for (int frame = 0; frame < 3; frame++) core.Tick(clock += 0.01f);
            Require(ReferenceEquals(profile, run.TimingProfile) && ReferenceEquals(flow, run.RecentPatternFlow),
                "Unchanged live descriptions must reuse their original strings.");
            core.SnapshotChanged -= observe;
            core.FillFocusForTest();
            core.SnapshotChanged += observe;
            core.Tick(clock += 0.01f);
            Require(run.FocusStateActivationCount == 1,
                "Commands without subscribers must invalidate the profile before a listener returns.");
            core.Tick(clock += 0.12f);
            Require(!ReferenceEquals(profile, run.TimingProfile) && run.FocusStateActiveSeconds > 0.1f,
                "Focus activation and displayed duration must invalidate the old profile.");
            foreach (string target in new[] { "무실수", "보정 0회", "입력 없음 1회", "추천 보정 적용",
                "콤보 3", "100점", "2구간 생존", "위협 3", "실수 2 이하", "체력 2", "집중 발동 1", "정박 2연속" })
            {
                core.EnterInfiniteRun(GreedLastRunRecord.Empty, "목표: " + target);
                clock = Time.realtimeSinceStartup;
                core.ToggleDevInvincible();
                core.FillFocusForTest();
                for (int frame = 0; frame < 420; frame++)
                {
                    clock += 1f / 60f;
                    core.Tick(clock);
                    if (frame == 120) core.RaiseInfiniteThreatForTest();
                    if (frame == 240) core.DropHealthForDangerTest();
                    if (!run.ActivePattern || run.CountdownActive) continue;
                    int count = run.SuccessCount + run.GoodCount + run.MissCount;
                    if (count % 3 == 2) continue;
                    float delta = count % 3 == 1
                        ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                    if (run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds + delta)
                        core.HandleInput(run.Pattern.Channel, clock);
                }
                core.ForceStopInfiniteRunForTest();
                Require(run.Stopped && run.ReviewTargetText == target, "Each target must retain its terminal profile.");
            }
            core.RestartPractice(clock);
            Require(run.SuccessCount == 0 && run.GoodCount == 0 && run.MissCount == 0
                && run.RecentPatternFlow == string.Empty && !run.TimingProfile.Contains("복기 목표", StringComparison.Ordinal),
                "Restart must discard previous history, counters and target text.");
            Debug.Log($"Greed live publication passed: {checks} fresh-profile comparisons, stable-string reuse, changing focus duration, twelve targets, mixed outcomes, health/threat commands, terminal settlement and restart. Personal slots and settings unchanged.");
        }

        [MenuItem("Greed Last/Measure Live Run Publications")]
        public static void Measure()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Live measurements require Edit mode.");
            GameObject previousCamera = GameObject.Find("GreedLastCamera");
            GameObject previousEvents = GameObject.Find("GreedLastEventSystem");
            var host = new GameObject("GreedLastLiveCheck");
            GameObject canvas = null;
            try
            {
                GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                Sample(null);
                Sample(ui);
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

        private static void Sample(GreedLastRuntimeUi ui)
        {
            if (ui != null) ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                GreedLastScreenState.InfiniteRun, "Live measurement", string.Empty, 0));
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            int publications = 0;
            core.SnapshotChanged += value =>
            {
                publications++;
                run = value;
                if (ui != null) ui.RenderRun(value);
            };
            float clock = Time.realtimeSinceStartup;
            core.EnterInfiniteRun();
            core.ToggleDevInvincible();
            for (int frame = 0; frame < 180; frame++) Step();
            int beforePublications = publications;
            int beforeJudgements = run.SuccessCount + run.GoodCount + run.MissCount;
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc",
                1000000, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            long beforeTime = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int frame = 0; frame < SampleFrames; frame++) Step();
            double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - beforeTime)
                * 1000d / System.Diagnostics.Stopwatch.Frequency;
            allocations.Stop();
            Require(allocations.Valid && allocations.Count > 0 && allocations.Count < allocations.Capacity,
                "Allocation instrumentation must collect real samples without saturating its buffer.");
            int judgements = run.SuccessCount + run.GoodCount + run.MissCount - beforeJudgements;
            Require(!run.Stopped && !run.Paused && run.Distance > 100f && judgements >= 8,
                "Measurement must advance an actual live infinite run, not an idle result.");
            Debug.Log($"Greed live measurement: {(ui == null ? "core" : "core+RenderRun")}, frames={SampleFrames}, publications={publications - beforePublications}, judgements={judgements}, gcAllocEvents={allocations.Count}, eventsPerFrame={(double)allocations.Count / SampleFrames:F1}, instrumentedElapsedMs={milliseconds:F3}. Synthetic 60 Hz core publications only; allocation byte size, UI Update, GPU, full frame time and device performance are not measured. No saved slots or settings changed.");

            void Step()
            {
                clock += 1f / 60f;
                core.Tick(clock);
                if (!run.ActivePattern || run.CountdownActive) return;
                int count = run.SuccessCount + run.GoodCount + run.MissCount;
                float delta = count % 4 == 0
                    ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                if (run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds + delta)
                    core.HandleInput(run.Pattern.Channel, clock);
            }
        }

        private static T Read<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void DestroyCreated(string name, GameObject previous)
        {
            GameObject created = GameObject.Find(name);
            if (previous == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
