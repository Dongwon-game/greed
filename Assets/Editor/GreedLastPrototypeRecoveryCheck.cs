using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeRecoveryCheck
    {
        internal readonly struct Sample
        {
            internal readonly GreedLastRunSnapshot Before, After;
            internal Sample(GreedLastRunSnapshot before, GreedLastRunSnapshot after)
            {
                Before = before;
                After = after;
            }
        }

        [MenuItem("Greed Last/Run Recovery Preview Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Recovery checks require Edit mode.");
            int cases = 0, inputs = 0, routeMismatches = 0, objectiveMismatches = 0;
            bool sawFullGaugeRecovery = false, sawCappedRecovery = false, sawStackedRecovery = false;
            bool sawRecoveryTrap = false, sawBothRecoverySources = false, sawGiftUsedLastRoom = false;
            string[] routes = { string.Empty, "안정 루트", "변주 루트", "압박 루트",
                "안정 루트 / 압박 루트", "안정 루트 / 안정 루트 / 압박 루트" };
            foreach (int growth in new[] { 0, 3 })
                for (int gifts = 0; gifts <= 3; gifts++)
                    foreach (string route in routes)
                        foreach (bool damaged in new[] { false, true })
                        {
                            cases++;
                            foreach (Sample sample in CreateSamples(growth, gifts, route, damaged))
                            {
                                var before = sample.Before;
                                var after = sample.After;
                                int gain = after.Health - before.Health;
                                Require(gain == after.LastJudgementHealthChange && gain >= 0,
                                    "Recovery comparisons must use actual Good health changes, not seeded fields.");
                                Require(before.GoodHealthGainPreview == gain,
                                    "The published Good recovery preview must match the next real judgement.");
                                if (before.JudgementText.Contains("루트 예고: 안정 -", StringComparison.Ordinal))
                                    Require(before.JudgementText.Contains(gain > 0 ? "Good 체력 +" + gain : "회복 상한", StringComparison.Ordinal),
                                        "The live stable route cue must not promise recovery after its cap.");
                                string tag = Label("BuildActiveRouteActionCuePayoffTag", before);
                                if (before.ActiveRouteProfile == "안정 루트")
                                {
                                    string expected = gain > 0 ? "보정회복+" + gain : string.Empty;
                                    string recovery = gain > 0 ? "보정 체력 +" + gain : "회복 상한";
                                    Require(Label("BuildActiveRoutePayoffCoach", before).Contains(recovery, StringComparison.Ordinal)
                                        && Label("BuildRoutePatternObjectiveSuffix", before).Contains(recovery, StringComparison.Ordinal),
                                        "The stable coach and objective must show actual recovery, including post-Miss traps and caps.");
                                    if (tag != expected)
                                    {
                                        routeMismatches++;
                                        Debug.Log($"Greed recovery route mismatch: growth={growth}, gifts={gifts}, damaged={damaged},"
                                            + $" health={before.Health}/{before.MaxHealth}, expected={expected}, actual={tag}.");
                                    }
                                }
                                if (before.ActiveChoiceProfile == "안정형")
                                {
                                    string expected = gain > 0 ? " / 안정 회복" : " / 안전 돌파";
                                    string objective = Label("BuildChoiceProfileObjectiveSuffix", before);
                                    if (objective != expected)
                                    {
                                        objectiveMismatches++;
                                        Debug.Log($"Greed recovery objective mismatch: health={before.Health}/{before.MaxHealth},"
                                            + $" gain={gain}, expected={expected}, actual={objective}.");
                                    }
                                }
                                sawFullGaugeRecovery |= before.Health == before.MaxHealth && gain > 0;
                                sawCappedRecovery |= gain == 0 && (gifts > 0 || before.ActiveRouteProfile == "안정 루트");
                                sawStackedRecovery |= gain >= 2 && gifts >= 2;
                                sawRecoveryTrap |= before.Pattern.Prompt.Contains("[복구]", StringComparison.Ordinal);
                                bool giftApplied = after.JudgementText.Contains("생존 축", StringComparison.Ordinal);
                                bool routeApplied = after.JudgementText.Contains("안정 루트 - 보정 회복", StringComparison.Ordinal);
                                sawBothRecoverySources |= giftApplied && routeApplied;
                                sawGiftUsedLastRoom |= giftApplied && !routeApplied && before.ActiveRouteProfile == "안정 루트";
                                inputs++;
                            }
                        }
            Require(sawFullGaugeRecovery && sawCappedRecovery && sawStackedRecovery && sawRecoveryTrap
                && sawBothRecoverySources && sawGiftUsedLastRoom, "Recovery checks must cover real caps, expanded gauges,"
                    + " stacked healing, post-Miss traps and survival-before-stable recovery ordering.");
            Require(routeMismatches == 0 && objectiveMismatches == 0,
                $"Recovery hints must match actual Good gains. Route={routeMismatches}, objective={objectiveMismatches}.");
            VerifyGoldenRecovery();
            Debug.Log($"Greed recovery previews passed: {cases} transient cores, {inputs} real Good judgements, growth 0/3,"
                + " survival stacks 0-3, six route histories, healthy/two-natural-Miss entry, actual recovery cap/gauge expansion"
                + " and survival/stable source ordering. Public core timing/input only; no private gameplay state edits, persistence, physical input or device-performance claim.");
        }

        private static void VerifyGoldenRecovery()
        {
            var stable = CreateSamples(0, 0, "안정 루트", false);
            Require(stable[0].Before.Health == 4 && stable[0].Before.MaxHealth == 4
                && stable[0].After.Health == 5 && stable[0].After.LastJudgementHealthChange == 1
                && stable[0].After.StableRouteRecoveryCount == 1
                && stable[1].After.Health == 5 && stable[1].After.LastJudgementHealthChange == 0
                && stable[1].After.StableRouteRecoveryCount == 1,
                "Stable-only recovery must expand 4/4 to 5/5 once, then stay capped without another recovery count.");
            var both = CreateSamples(0, 1, "안정 루트", true)[0];
            var giftFirst = CreateSamples(0, 2, "안정 루트", true)[0];
            var grown = CreateSamples(3, 2, "안정 루트", true)[0];
            var empty = CreateSamples(0, 0, string.Empty, true)[0];
            Require(both.Before.Health == 3 && both.After.Health == 5 && both.After.StableRouteRecoveryCount == 1
                && giftFirst.Before.Health == 3 && giftFirst.After.Health == 5 && giftFirst.After.StableRouteRecoveryCount == 0
                && grown.Before.Health == 6 && grown.After.Health == 8 && grown.After.StableRouteRecoveryCount == 0
                && empty.Before.Health == 1 && empty.After.Health == 1 && empty.After.LastJudgementHealthChange == 0,
                "Golden controls must retain survival-before-stable order, 5/8 caps, actual damage and no unsupported healing.");
            Debug.Log("Greed golden recovery passed: five separate transient core controls, fixed 4-to-5/3-to-5/6-to-8/1-to-1 health,"
                + " stable recovery counters, source ordering and no-heal controls. Existing recovery rules unchanged.");
        }

        internal static List<Sample> CreateSamples(int growth, int gifts, string route, bool damaged)
        {
            var samples = new List<Sample>();
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.ConfigureGrowth(growth, 0, 0);
            string summary = route;
            for (int i = 0; i < gifts; i++) summary += (summary.Length > 0 ? " / " : string.Empty) + "생존 축";
            core.EnterInfiniteRun(summary.Length == 0 ? GreedLastRunRecord.Empty : new GreedLastRunRecord(
                true, 0, 3, 0, 0, 0, 0, 0, 0, 20f, "recovery-preview", string.Empty,
                gifts > 0 ? "생존 축" : string.Empty, string.Empty, summary, string.Empty));
            float clock = Time.realtimeSinceStartup;
            for (int input = 0; input < (damaged ? 8 : 6); input++)
            {
                for (int wait = 0; !run.ActivePattern && !run.Stopped && wait < 500; wait++) Advance(0.01f);
                Require(run.ActivePattern && !run.CountdownActive && !run.DevInvincible,
                    "Recovery checks need a naturally arriving trap with actual damage rules.");
                bool miss = damaged && input < 2;
                float delta = miss ? 0f : (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds);
                var before = run;
                core.HandleInput(miss ? (run.Pattern.Channel == GreedLastRunChannel.Left
                    ? GreedLastRunChannel.Right : GreedLastRunChannel.Left) : run.Pattern.Channel, clock);
                Require(run.LastResult == (miss ? GreedLastJudgementResult.Miss : GreedLastJudgementResult.Good)
                    && !run.Stopped, "Recovery samples must survive the declared two misses and use actual Good judgements.");
                if (!miss) samples.Add(new Sample(before, run));
            }
            core.Exit();
            return samples;

            void Advance(float seconds)
            {
                Require(seconds >= -0.0002f, "Recovery checks must not rewind a core clock.");
                while (seconds > 0.0001f)
                {
                    float step = Mathf.Min(seconds, 0.01f);
                    core.Tick(clock += step);
                    seconds -= step;
                }
            }
        }

        private static string Label(string name, GreedLastRunSnapshot run)
            => (string)typeof(GreedLastRuntimeUi).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { run });

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
