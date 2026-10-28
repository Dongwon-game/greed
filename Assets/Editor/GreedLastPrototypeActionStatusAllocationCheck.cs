using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeActionStatusAllocationCheck
    {
        private const int Samples = 1024;

        [MenuItem("Greed Last/Run Action Status Allocation Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Action status checks require Edit mode.");
            using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                for (int i = 0; i < 16; i++) GC.KeepAlive(new byte[64]);
                calibration.Stop();
                Require(calibration.Valid && calibration.Count >= 16 && calibration.Count < calibration.Capacity,
                    "The action status allocation recorder must detect deliberate allocations.");
            }
            var read = (Func<GreedLastRunSnapshot, string>)Delegate.CreateDelegate(
                typeof(Func<GreedLastRunSnapshot, string>), typeof(GreedLastRuntimeUi)
                    .GetMethod("BuildThreeActionBundleStatus", BindingFlags.Static | BindingFlags.NonPublic));
            FieldInfo maskField = typeof(GreedLastRunCore).GetField("currentBundleAvoidanceMask", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(maskField != null, "The synthetic mask fixture requires the existing core field.");
            string[] expectedLabels = { "", "동작 ↑ 1/3→↓", "동작 ↓ 1/3→↑", "동작 ↑↓ 2/3→↔",
                "동작 ↔ 1/3→↑", "동작 ↑↔ 2/3→↓", "동작 ↓↔ 2/3→↑", "동작 ↑↓↔ 3/3" };
            var failures = new List<string>();
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            int cases = 0;
            float clock = Time.realtimeSinceStartup;
            try
            {
                core.Enter();
                Measure("choice", run, "");
                core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
                Require(run.StartCountdownActive, "An actual start countdown must be checked.");
                Measure("countdown", run, "");
                core.Tick(clock += 2f); core.Tick(clock += 1f);
                Require(run.ActivePattern, "The action status fixture must reach a real first trap.");
                Measure("before-progress", run, "");
                clock += run.Pattern.CoreSeconds - run.ElapsedSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.SuccessCount == 1 && run.ChapterProgress == 1 && run.PerfectBundleIntact,
                    "The live status control must follow an actual first exact input.");
                Measure("actual-first-exact", run, expectedLabels[1]);
                int originalMask = run.CurrentBundleAvoidanceMask;
                var masks = new List<int>();
                for (int i = 0; i < 32; i++) masks.Add(i);
                masks.Add(int.MinValue); masks.Add(int.MaxValue); masks.Add(-1);
                foreach (int mask in masks)
                {
                    maskField.SetValue(core, mask);
                    core.Tick(clock);
                    Require(run.ChapterProgress == 1 && run.SuccessCount == 1 && run.GoodCount == 0,
                        "Synthetic formatting masks must not advance or judge another trap.");
                    Measure("mask-" + mask, run, expectedLabels[(Math.Max(0, mask) & 14) / 2]);
                }
                maskField.SetValue(core, originalMask);
                core.RequestNextPattern(clock += 2f);
                Require(run.ActivePattern, "Pause controls must use the actual next trap.");
                core.TogglePause(clock += 0.1f);
                Require(run.Paused, "An actual pause must be checked.");
                Measure("pause", run, "");
                core.TogglePause(clock += 0.1f);
                Require(run.ResumeCountdownActive, "An actual resume countdown must be checked.");
                Measure("resume-countdown", run, "");
                core.Tick(clock += 2f);
                float goodDelta = (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                clock += run.Pattern.CoreSeconds + goodDelta - run.ElapsedSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Good && !run.PerfectBundleIntact,
                    "An actual Good must provide the broken-bundle control.");
                Measure("broken-bundle", run, "동작 완주 중단");
                for (int step = 0; !run.Stopped && step < 100; step++)
                {
                    if (run.ChoiceActive) { core.HandleInput(GreedLastRunChannel.Right, clock += 0.1f); continue; }
                    if (run.StartCountdownActive) { core.Tick(clock += 2f); core.Tick(clock += 1f); }
                    if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                    Require(run.ActivePattern, "Stopped status must come from actual natural no-input failure.");
                    clock += Mathf.Max(run.Pattern.HitSeconds, run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds)
                        + 0.1f - run.ElapsedSeconds;
                    core.Tick(clock);
                }
                Require(run.Stopped && run.Health == 0, "An actual stopped result must be checked.");
                Measure("stopped", run, "");
                Require(cases == 43, "Every action-status state/mask control must complete.");
                Require(failures.Count == 0, "Three-action status reads must reuse unchanged labels: " + string.Join(" | ", failures));
                Debug.Log("Greed action status allocation passed: 43 states/masks x 1024 warmed reads, zero current-thread GC.Alloc events, "
                    + "independent eight labels and gate controls preserved. Real first exact/Good/pause/failure controls plus 35 synthetic formatting masks; "
                    + "not 43 played runs, whole-frame allocation bytes, rendered/device FPS or audible latency.");
            }
            finally { core.Exit(); }

            void Measure(string name, GreedLastRunSnapshot snapshot, string expected)
            {
                for (int i = 0; i < 128; i++) Require(read(snapshot) == expected, "Action status value mismatch: " + name);
                using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 65536,
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                bool matched = true;
                for (int i = 0; i < Samples; i++) matched &= read(snapshot) == expected;
                recorder.Stop();
                Require(recorder.Valid && recorder.Count < recorder.Capacity && matched, "Action status measurement must stay valid and preserve its label.");
                int events = recorder.Count;
                Debug.Log("Greed action status allocation: " + name + ", reads=" + Samples + ", gcAllocEvents=" + events);
                if (events != 0) failures.Add(name + "=" + events);
                cases++;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
