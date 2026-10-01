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
            VerifyDeferredToolLabels();
            Debug.Log($"Greed live publication passed: {checks} fresh-profile comparisons, stable-string reuse, changing focus duration, twelve targets, mixed outcomes, health/threat commands, terminal settlement and restart. Personal slots and settings unchanged.");
        }

        private static void VerifyDeferredToolLabels()
        {
            GameObject previousCamera = GameObject.Find("GreedLastCamera");
            GameObject previousEvents = GameObject.Find("GreedLastEventSystem");
            var host = new GameObject("GreedLastHiddenToolsCheck");
            GameObject canvas = null;
            try
            {
                GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                    GreedLastScreenState.InfiniteRun, "Hidden tools check", string.Empty, 0));
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => { run = value; ui.RenderRun(value); };
                float clock = Time.realtimeSinceStartup;
                core.EnterInfiniteRun();
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                string hiddenFocus = Label("focusMaxButton");
                string hiddenThreat = Label("threatUpButton");
                core.FillFocusForTest();
                core.RaiseInfiniteThreatForTest();
                Require(ui.UsePlayerRunControls && Label("focusMaxButton") == hiddenFocus
                    && Label("threatUpButton") == hiddenThreat,
                    "Hidden development labels must not rebuild during clean gameplay.");
                ui.ToggleDevCleanView();
                CheckLatest();
                core.Tick(clock += 0.12f);
                CheckLatest();
                ui.ToggleDevCleanView();
                hiddenFocus = Label("focusMaxButton");
                hiddenThreat = Label("threatUpButton");
                core.RaiseInfiniteThreatForTest();
                core.Tick(clock += 0.12f);
                Require(Label("focusMaxButton") == hiddenFocus && Label("threatUpButton") == hiddenThreat,
                    "Returning to clean mode must stop hidden label rebuilding again.");
                core.TogglePause(clock);
                ui.ToggleDevCleanView();
                CheckLatest();
                Require(run.Paused && !Read<UnityEngine.UI.Button>(ui, "focusMaxButton").gameObject.activeSelf,
                    "Refreshing labels in a pause must not expose locked development actions.");
                ui.ToggleDevCleanView();
                core.ForceStopInfiniteRunForTest();
                ui.ToggleDevCleanView();
                CheckLatest();
                Require(run.Stopped && !Read<UnityEngine.UI.Button>(ui, "focusMaxButton").interactable,
                    "Terminal label refresh must preserve the result's command locks.");
                ui.ToggleDevCleanView();
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                    GreedLastScreenState.RunCoreTest, "Calibration label check", string.Empty, 0));
                core.RestartPractice(clock);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                for (int trap = 0; trap < 3; trap++)
                {
                    if (trap > 0) core.RequestNextPattern(clock += 2f);
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + 0.05f;
                    core.HandleInput(run.Pattern.Channel, clock);
                }
                Require(run.Stopped && run.PracticeMode && ui.UsePlayerRunControls,
                    "Calibration fixture must end a real practice run in player controls.");
                var timingButtons = Read<UnityEngine.UI.Button[]>(ui, "timingOffsetButtons");
                string recommendation = (string)typeof(GreedLastRuntimeUi).GetMethod("BuildTimingRecommendationButtonLabel",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });
                Require(timingButtons[1].GetComponentInChildren<UnityEngine.UI.Text>(true).text == recommendation,
                    "Visible clean practice-result calibration must not inherit a deferred live label.");
                VerifyLongRunPresentation(ui);
                Debug.Log("Greed hidden tool labels passed: clean publication deferral, immediate latest F11 focus/threat labels, live detail updates, repeated hiding, paused visibility and terminal locks.");

                string Label(string field) => Read<UnityEngine.UI.Button>(ui, field)
                    .GetComponentInChildren<UnityEngine.UI.Text>(true).text;
                void CheckLatest()
                {
                    foreach (var pair in new[] {
                        ("focusMaxButton", "BuildRunFocusTestButtonLabel"),
                        ("threatUpButton", "BuildRunThreatUpTestButtonLabel"),
                        ("threatDownButton", "BuildRunThreatDownTestButtonLabel") })
                    {
                        string expected = (string)typeof(GreedLastRuntimeUi).GetMethod(pair.Item2,
                            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });
                        Require(Label(pair.Item1).Contains(expected, StringComparison.Ordinal),
                            "Visible development tool must reflect the latest snapshot immediately: " + pair.Item1);
                    }
                }
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

        private static void VerifyLongRunPresentation(GreedLastRuntimeUi ui)
        {
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                GreedLastScreenState.InfiniteRun, "Long presentation check", string.Empty, 0));
            int renderedSnapshots = 0;
            int presentedJudgements = 0;
            int previousCount = 0;
            bool sawTerminal = false;
            bool sawFreshPractice = false;
            GreedLastPrototypeTimelineCheck.VerifyLongInfiniteRun(value =>
            {
                ui.RenderRun(value);
                renderedSnapshots++;
                int count = value.SuccessCount + value.GoodCount + value.MissCount;
                int historyCount = Read<int>(ui, "judgementHistoryCount");
                Require(Read<int>(ui, "lastFeedbackJudgementCount") == count,
                    "The long-run UI must track every real cumulative judgement without replaying old feedback.");
                Require(historyCount == Mathf.Min(count, 5),
                    "The long-run recent judgement surface must hold only the latest five entries.");
                if (count > previousCount) presentedJudgements++;
                previousCount = count;
                if (value.InfiniteMode && value.Stopped)
                {
                    sawTerminal = true;
                    Require(count >= 1200 && historyCount == 5,
                        "Long-run terminal presentation must retain its final bounded history.");
                }
                if (value.PracticeMode && value.StartCountdownActive)
                {
                    sawFreshPractice = true;
                    Require(count == 0 && historyCount == 0,
                        "Fresh practice must clear the long-run UI history and result identity.");
                }
            });
            Require(sawTerminal && sawFreshPractice && presentedJudgements >= 1203 && renderedSnapshots > 30000,
                "Long presentation checks must render a complete real 400-section run, failure and restart.");
            Debug.Log($"Greed long UI presentation passed: {renderedSnapshots} real snapshot renders,"
                + $" {presentedJudgements} distinct judgements, recent history bounded to five, terminal preservation and fresh-practice reset. Synthetic progression only, not full-frame or device performance.");
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
