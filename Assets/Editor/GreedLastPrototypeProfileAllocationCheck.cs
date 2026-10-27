using System;
using System.Collections.Generic;
using System.Reflection;
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

            Require(failures.Count == 0, "Passive run presentation profile reads must not allocate temporary arrays: "
                + string.Join(" | ", failures));
            Debug.Log("Greed live profile allocation passed: " + measured + " warmed pure paths x " + Samples
                + " reads allocate zero managed bytes; " + controls
                + " independent score/tie controls preserve profile results. Synthetic core inputs and thread allocation counters only, not whole-frame/device FPS or audible latency.");
        }

        private static void CheckAllocation(string name, Func<string> read, string expected, List<string> failures)
        {
            for (int i = 0; i < 128; i++) Require(read() == expected, "Profile warmup returned a different value.");
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool matched = true;
            for (int i = 0; i < Samples; i++) matched &= read() == expected;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(matched, "Allocation measurement must preserve its exact profile value.");
            Debug.Log("Greed profile allocation: " + name + ", reads=" + Samples + ", managedBytes=" + allocated);
            if (allocated != 0) failures.Add(name + "=" + allocated);
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
