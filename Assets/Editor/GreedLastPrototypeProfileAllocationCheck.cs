using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeProfileAllocationCheck
    {
        private const int Samples = 1024;

        [MenuItem("Greed Last/Run Live Profile Allocation Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Profile allocation checks require idle Edit mode.");
            var failures = new List<string>();
            CheckRecorderCalibration();
            int controls = CheckProfileValues();
            int measured = 0;
            foreach (GreedLastRunChannel? channel in new GreedLastRunChannel?[]
                { null, GreedLastRunChannel.Left, GreedLastRunChannel.Center, GreedLastRunChannel.Right })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot snapshot = default;
                int publications = 0;
                core.SnapshotChanged += value => { snapshot = value; publications++; };
                core.Enter();
                if (channel.HasValue) core.HandleInput(channel.Value, Time.realtimeSinceStartup);
                GreedLastRunSnapshot before = snapshot;
                int count = publications;
                var profile = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), core,
                    typeof(GreedLastRunCore).GetMethod("BuildActiveChoiceProfile", BindingFlags.Instance | BindingFlags.NonPublic));
                CheckAllocation("normal-" + (channel.HasValue ? channel.Value.ToString() : "unselected"),
                    profile, snapshot.ActiveChoiceProfile, failures);
                Require(publications == count && snapshot.Equals(before), "Reading a live profile must not publish or change the run.");
                core.Exit();
                measured++;
            }

            var scores = (Func<int, int, int, int, string>)Delegate.CreateDelegate(
                typeof(Func<int, int, int, int, string>), typeof(GreedLastRunCore)
                    .GetMethod("BuildChoiceProfileFromScores", BindingFlags.Static | BindingFlags.NonPublic));
            CheckAllocation("score-profile", () => scores(1, 3, 2, 0), "집중형", failures);
            measured++;

            var infiniteCore = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            infiniteCore.SnapshotChanged += value => run = value;
            infiniteCore.EnterInfiniteRun(new GreedLastRunRecord(true, 100, 3, 0, 0, 3, 3, 0, 0, 20f,
                "allocation-check", "박자 감각", "집중 축", string.Empty, "집중 축", string.Empty));
            var cached = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), infiniteCore,
                typeof(GreedLastRunCore).GetMethod("BuildActiveChoiceProfile", BindingFlags.Instance | BindingFlags.NonPublic));
            CheckAllocation("cached-infinite", cached, run.ActiveChoiceProfile, failures);
            measured++;
            float clock = Time.realtimeSinceStartup;
            infiniteCore.Tick(clock += 2f);
            infiniteCore.Tick(clock += 1f);
            for (int action = 0; action < 4; action++)
            {
                if (action > 0) infiniteCore.RequestNextPattern(clock += 2f);
                Require(run.ActivePattern, "HUD allocation coverage requires a live infinite trap.");
                clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - infiniteCore.InputTimingOffsetSeconds;
                infiniteCore.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Success, "HUD coverage requires four actual exact inputs.");
            }
            Require(run.SuccessCount == 4, "The HUD probe must reach its nontrivial timing-axis branch.");
            var axis = (Func<GreedLastRunSnapshot, string>)Delegate.CreateDelegate(
                typeof(Func<GreedLastRunSnapshot, string>), typeof(GreedLastRuntimeUi)
                    .GetMethod("BuildDominantTimingAxisFlowTag", BindingFlags.Static | BindingFlags.NonPublic));
            GreedLastRunSnapshot axisSnapshot = run;
            CheckAllocation("hud-timing-axis", () => axis(axisSnapshot), axis(axisSnapshot), failures);
            measured++;
            infiniteCore.Exit();
            measured += CheckCadencePresentation(failures, out int cadenceControls);

            Require(failures.Count == 0, "Passive run presentation profile reads must not allocate temporary arrays: "
                + string.Join(" | ", failures));
            Debug.Log("Greed live profile allocation passed: " + measured + " warmed pure paths x " + Samples
                + " reads produce zero GC.Alloc events; " + controls
                + " independent score/tie controls and " + cadenceControls
                + " cadence controls preserve results. Synthetic core inputs and current-thread Unity profiler events only, not allocation byte sizes, whole-frame/device FPS or audible latency.");
        }

        private static int CheckCadencePresentation(List<string> failures, out int controls)
        {
            var state = (Func<GreedLastRunSnapshot, int, string>)Delegate.CreateDelegate(
                typeof(Func<GreedLastRunSnapshot, int, string>), typeof(GreedLastRuntimeUi)
                    .GetMethod("BuildRouteCadencePipState", BindingFlags.Static | BindingFlags.NonPublic));
            var action = (Func<GreedLastRunSnapshot, int, string>)Delegate.CreateDelegate(
                typeof(Func<GreedLastRunSnapshot, int, string>), typeof(GreedLastRuntimeUi)
                    .GetMethod("BuildRouteCadencePipActionSymbol", BindingFlags.Static | BindingFlags.NonPublic));
            var measured = new HashSet<string>(StringComparer.Ordinal);
            int checks = 0;
            Verify(default);
            var practice = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            practice.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            practice.RestartPractice(clock);
            Verify(run);
            practice.Tick(clock += 2f);
            practice.Tick(clock += 1f);
            Require(run.ActivePattern && !run.CountdownActive, "Cadence checks require actual active practice.");
            Verify(run);
            Measure(run);
            practice.TogglePause(clock);
            Verify(run);
            practice.TogglePause(clock);
            Verify(run);
            practice.Exit();
            Verify(run);

            for (int route = 0; route < 3; route++)
            for (int relic = 0; relic < 3; relic++)
            {
                var core = new GreedLastRunCore();
                core.SnapshotChanged += value => run = value;
                clock = Time.realtimeSinceStartup;
                core.Enter();
                int judgements = 0, choices = 0;
                for (int step = 0; !run.Stopped && step < 100; step++)
                {
                    Verify(run);
                    if (run.ChoiceActive)
                    {
                        int lane = run.ChoiceKind == GreedLastChoiceKind.Node ? route
                            : run.ChoiceKind == GreedLastChoiceKind.Relic ? relic : 1;
                        core.HandleInput((GreedLastRunChannel)(lane + 1), clock += 0.1f);
                        choices++;
                        continue;
                    }
                    if (run.StartCountdownActive)
                    {
                        core.Tick(clock += 2f);
                        core.Tick(clock += 1f);
                    }
                    if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                    Require(run.ActivePattern && !run.CountdownActive, "Cadence value controls must use actual authored traps.");
                    Verify(run);
                    Measure(run);
                    Require(run.Pattern.TimingType == ExpectedTiming(run, run.ChapterProgress),
                        "The independent cadence table must match the actual current trap, including relic/final steps.");
                    clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - core.InputTimingOffsetSeconds;
                    bool miss = run.ChapterIndex == 2 && run.ChapterProgress == 0;
                    GreedLastRunChannel channel = miss
                        ? run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left
                        : run.Pattern.Channel;
                    core.HandleInput(channel, clock);
                    judgements++;
                    Verify(run);
                }
                Require(run.Stopped && run.EscapeSucceeded && judgements == 15 && choices == 8 && run.MissCount == 1,
                    "All nine route/relic combinations must actually clear, including broken and completed cadence states.");
                core.Exit();
                Verify(run);
            }
            var infinite = new GreedLastRunCore();
            infinite.SnapshotChanged += value => run = value;
            infinite.EnterInfiniteRun();
            Verify(run);
            infinite.Tick(clock = Time.realtimeSinceStartup + 2f);
            infinite.Tick(clock += 1f);
            Verify(run);
            infinite.Exit();
            controls = checks;
            return measured.Count * 2;

            void Verify(GreedLastRunSnapshot snapshot)
            {
                bool available = snapshot.PracticeMode || !snapshot.InfiniteMode
                    && (snapshot.ChapterIndex == 1 || snapshot.ChapterIndex >= 2
                        && (snapshot.ActiveRouteProfile == "안정 루트" || snapshot.ActiveRouteProfile == "변주 루트"
                            || snapshot.ActiveRouteProfile == "압박 루트"));
                bool visible = available && !snapshot.InfiniteMode && !snapshot.ChoiceActive
                    && !snapshot.Stopped && !snapshot.Paused && !snapshot.CountdownActive;
                for (int index = -1; index <= 3; index++)
                {
                    string expectedState = !visible || index < 0 || index > 2 ? "hidden"
                        : index < Mathf.Clamp(snapshot.ChapterProgress, 0, 3)
                            ? snapshot.PracticeMode || snapshot.PerfectBundleIntact ? "done" : "broken"
                            : index == snapshot.ChapterProgress ? "active" : "pending";
                    string expectedAction = !available || index < 0 || index > 2 ? string.Empty : ExpectedAction(snapshot, index);
                    Require(state(snapshot, index) == expectedState && action(snapshot, index) == expectedAction,
                        "Cadence state/action must match the independent authored table without mutating progress.");
                    checks++;
                }
            }

            void Measure(GreedLastRunSnapshot snapshot)
            {
                if (snapshot.ChapterProgress != 0) return;
                string name = snapshot.PracticeMode ? "practice" : snapshot.Phase + "/" + snapshot.ChapterIndex
                    + "/" + snapshot.ActiveRouteProfile + (snapshot.ChapterIndex == 3 ? "/" + snapshot.ActiveRelic : string.Empty);
                if (!measured.Add(name)) return;
                CheckAllocation("cadence-state/" + name, () => state(snapshot, 0), "active", failures);
                CheckAllocation("cadence-action/" + name, () => action(snapshot, 0), ExpectedAction(snapshot, 0), failures);
            }
        }

        // Independent authored tables keep the UI implementation out of its own oracle.
        private static string ExpectedAction(GreedLastRunSnapshot run, int index)
        {
            if (run.PracticeMode || run.ChapterIndex <= 1) return new[] { "↑", "↓", "↔" }[index];
            if (run.Phase == GreedLastRunPhase.EscapeRunning || run.ChapterIndex >= 4 || run.ActiveRouteProfile == "압박 루트")
                return new[] { "↓", "↔", "↑" }[index];
            return run.ActiveRouteProfile == "변주 루트" ? new[] { "↔", "↓", "↑" }[index] : new[] { "↑", "↓", "↔" }[index];
        }

        private static GreedLastTimingType ExpectedTiming(GreedLastRunSnapshot run, int index)
        {
            if (run.PracticeMode || run.ChapterIndex <= 1)
                return new[] { GreedLastTimingType.Perfect, GreedLastTimingType.Clutch, GreedLastTimingType.Offbeat }[index];
            if (run.Phase == GreedLastRunPhase.EscapeRunning || run.ChapterIndex >= 4)
                return new[] { GreedLastTimingType.Clutch, GreedLastTimingType.Offbeat, GreedLastTimingType.Perfect }[index];
            if (run.ChapterIndex == 3 && index == 1)
            {
                if (run.ActiveRelic == "황금 박동") return GreedLastTimingType.Perfect;
                if (run.ActiveRelic == "사막의 잔상") return GreedLastTimingType.Offbeat;
                if (run.ActiveRelic == "파라오의 각인") return GreedLastTimingType.Clutch;
            }
            return run.ActiveRouteProfile == "안정 루트"
                ? new[] { GreedLastTimingType.Perfect, GreedLastTimingType.Clutch, GreedLastTimingType.Perfect }[index]
                : run.ActiveRouteProfile == "변주 루트"
                    ? new[] { GreedLastTimingType.Offbeat, GreedLastTimingType.Perfect, GreedLastTimingType.Offbeat }[index]
                    : new[] { GreedLastTimingType.Clutch, GreedLastTimingType.Offbeat, GreedLastTimingType.Clutch }[index];
        }

        private static void CheckAllocation(string name, Func<string> read, string expected, List<string> failures)
        {
            for (int i = 0; i < 128; i++) Require(read() == expected, "Profile warmup returned a different value.");
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc",
                65536, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            bool matched = true;
            for (int i = 0; i < Samples; i++) matched &= read() == expected;
            allocations.Stop();
            Require(allocations.Valid && allocations.Count < allocations.Capacity,
                "Profile allocation recording must remain valid and unsaturated.");
            Require(matched, "Allocation measurement must preserve its exact profile value.");
            int events = allocations.Count;
            Debug.Log("Greed profile allocation: " + name + ", reads=" + Samples + ", gcAllocEvents=" + events);
            if (events != 0) failures.Add(name + "=" + events);
        }

        private static void CheckRecorderCalibration()
        {
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc",
                4096, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 16; i++) GC.KeepAlive(new byte[64]);
            allocations.Stop();
            Require(allocations.Valid && allocations.Count >= 16 && allocations.Count < allocations.Capacity,
                "An allocation recorder must detect deliberate allocations before zero-event results are trusted.");
        }

        private static int CheckProfileValues()
        {
            var profile = (Func<int, int, int, int, string>)Delegate.CreateDelegate(
                typeof(Func<int, int, int, int, string>), typeof(GreedLastRunCore)
                    .GetMethod("BuildChoiceProfileFromScores", BindingFlags.Static | BindingFlags.NonPublic));
            int cases = 0;
            for (int survival = 0; survival <= 4; survival++)
            for (int focus = 0; focus <= 4; focus++)
            for (int combo = 0; combo <= 4; combo++)
            for (int score = 0; score <= 4; score++)
            {
                int top = survival;
                if (focus > top) top = focus;
                if (combo > top) top = combo;
                if (score > top) top = score;
                int ties = (survival == top ? 1 : 0) + (focus == top ? 1 : 0)
                    + (combo == top ? 1 : 0) + (score == top ? 1 : 0);
                string expected = top == 0 ? string.Empty : ties > 1 ? "균형형"
                    : survival == top ? "안정형" : focus == top ? "집중형" : combo == top ? "연쇄형" : "고점형";
                Require(profile(survival, focus, combo, score) == expected, "Profile dominance and ties must retain existing results.");
                cases++;
            }
            return cases;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
