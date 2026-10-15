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
            internal readonly GreedLastRunSnapshot Before, After, Anticipation, AnticipationBefore;
            internal Sample(GreedLastRunSnapshot before, GreedLastRunSnapshot after, GreedLastRunSnapshot anticipation = default,
                GreedLastRunSnapshot anticipationBefore = default)
            {
                Before = before;
                After = after;
                Anticipation = anticipation;
                AnticipationBefore = anticipationBefore;
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
            RunBundlePreviews();
            RunChoiceHealthPreviews();
            RunChoiceFocusPreviews();
            Debug.Log($"Greed recovery previews passed: {cases} transient cores, {inputs} real Good judgements, growth 0/3,"
                + " survival stacks 0-3, six route histories, healthy/two-natural-Miss entry, actual recovery cap/gauge expansion"
                + " and survival/stable source ordering. Public core timing/input only; no private gameplay state edits, persistence, physical input or device-performance claim.");
        }

        [MenuItem("Greed Last/Run Route Finisher Preview Check")]
        public static void RunBundlePreviews()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Finisher checks require Edit mode.");
            int runs = 0, bundles = 0, mismatches = 0, recovery = 0, focus = 0, broken = 0, controls = 0;
            foreach (int growth in new[] { 0, 3 })
            {
                for (int start = 0; start < 3; start++)
                {
                    for (int gift = 0; gift < 3; gift++) Check(growth, start, gift, 0, false);
                    Check(growth, start, 0, 0, true);
                }
                Check(growth, 0, 0, 1, false);
                Check(growth, 0, 0, 2, false);
            }
            Require(runs == 28 && bundles == 112 && recovery > 0 && focus > 0 && broken == 24 && controls == 16,
                "Finisher checks must cover real healthy/capped stable rewards, broken bundles and unchanged variation/pressure controls.");
            Require(mismatches == 0, $"Finisher hints must name the actual reward, not capped recovery. Mismatches={mismatches}.");
            Debug.Log($"Greed route finisher previews passed: {runs} complete transient normal runs/420 actual judgements,"
                + $" {bundles} route bundles, recovery={recovery}, focus={focus}, broken={broken}, other-route controls={controls}."
                + " Public core timing/choice/input APIs only, no private state/clock edits, invincibility or personal saves. Not physical/device testing.");

            void Check(int growth, int start, int gift, int route, bool breakBundles)
            {
                runs++;
                foreach (Sample sample in CreateBundleSamples(growth, start, gift, route, breakBundles))
                {
                    bundles++;
                    var before = sample.Before;
                    var after = sample.After;
                    string objective = Label("BuildStageTransitionObjectiveSuffix", before);
                    string step = Label("BuildRouteStepFeedback", sample.Anticipation);
                    bool valid;
                    if (breakBundles)
                    {
                        broken++;
                        valid = !objective.Contains("안정 완주", StringComparison.Ordinal)
                            && !step.Contains("대기", StringComparison.Ordinal)
                            && !after.JudgementText.Contains("패턴 묶음 완벽 -", StringComparison.Ordinal);
                    }
                    else if (route == 0)
                    {
                        bool healed = after.LastJudgementHealthChange > 0;
                        if (healed) recovery++; else focus++;
                        Require(after.JudgementText.Contains("패턴 묶음 완벽 - 안정 완주 / "
                            + (healed ? "체력 +1" : "집중"), StringComparison.Ordinal),
                            "Stable finishers must actually heal once or retain their existing capped focus fallback.");
                        valid = objective.Contains(healed ? "안정 완주 회복" : "안정 완주 집중", StringComparison.Ordinal)
                            && step.Contains(healed ? "회복 대기" : "집중 보상 대기", StringComparison.Ordinal);
                    }
                    else
                    {
                        controls++;
                        string token = route == 1 ? "변주 완주 집중 +" : "압박 완주 +";
                        valid = objective.Contains(token, StringComparison.Ordinal)
                            && after.JudgementText.Contains(route == 1 ? "변주 완주 / 집중" : "압박 완주 / 점수 +", StringComparison.Ordinal);
                    }
                    if (!valid)
                    {
                        mismatches++;
                        Debug.Log($"Greed finisher mismatch: growth={growth}, start={start}, gift={gift}, route={route},"
                            + $" broken={breakBundles}, chapter={before.ChapterIndex}/{before.Phase}, health={before.Health}/{before.MaxHealth},"
                            + $" actualHealth={after.LastJudgementHealthChange}, objective={objective}, step={step}.");
                    }
                }
            }
        }

        [MenuItem("Greed Last/Run Choice Health Reward Check")]
        public static void RunChoiceHealthPreviews()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Choice health checks require Edit mode.");
            int choices = 0, healthChoices = 0, capped = 0, recovered = 0, mismatches = 0;
            var types = new HashSet<string>();
            var cappedTypes = new HashSet<string>();
            foreach (int growth in new[] { 0, 3 })
                for (int start = 0; start < 3; start++)
                    for (int gift = 0; gift < 3; gift++)
                        CreateBundleSamples(growth, start, gift, 0, observeChoice: (kind, selected, sample) =>
                        {
                            choices++;
                            if (selected != "안정 호흡" && selected != "안정 루트" && selected != "생존 축" && selected != "사막의 잔상") return;
                            healthChoices++;
                            types.Add(selected);
                            int gain = sample.After.Health - sample.Before.Health;
                            Require(gain == 0 || gain == 1, "Real choice healing must preserve its existing one-point reward and cap.");
                            Require(sample.After.MaxHealth == Mathf.Max(sample.Before.MaxHealth, sample.After.Health),
                                "Choice health feedback must not change the historical maximum gauge.");
                            if (gain == 0) { capped++; cappedTypes.Add(selected); } else recovered++;
                            string expected = gain == 0 ? "효과: 체력 상한 유지" : "효과: 체력 +1";
                            string effect = Array.Find(sample.After.JudgementText.Split('\n'), line => line.StartsWith("효과: ", StringComparison.Ordinal));
                            if (effect == null || !effect.StartsWith(expected, StringComparison.Ordinal))
                            {
                                mismatches++;
                                Debug.Log($"Greed choice health mismatch: growth={growth}, kind={kind}, selected={selected},"
                                    + $" before={sample.Before.Health}/{sample.Before.MaxHealth}, after={sample.After.Health}/{sample.After.MaxHealth}, effect={effect}.");
                            }
                        });
            Require(choices == 144 && healthChoices == 84 && types.Count == 4 && capped > 0 && recovered > 0
                && cappedTypes.Contains("안정 루트") && cappedTypes.Contains("생존 축") && cappedTypes.Contains("사막의 잔상"),
                "Choice controls must cover 18 complete runs, eight real choices each, four healing sources and three capped choice types.");
            Require(mismatches == 0, $"Choice health notices must show actual gains or caps. Mismatches={mismatches}.");
            int relicControls = 0;
            foreach (int growth in new[] { 0, 3 })
                CreateBundleSamples(growth, 0, 0, 1, observeChoice: (kind, selected, sample) =>
                {
                    if (selected != "사막의 잔상") return;
                    Require(kind == GreedLastChoiceKind.Relic && sample.After.Health - sample.Before.Health == 1
                        && sample.After.JudgementText.Contains("효과: 체력 +1 / ", StringComparison.Ordinal),
                        "Separate non-stable route controls must retain an actual positive Sand Afterimage health reward.");
                    relicControls++;
                }, relic: 1);
            Require(relicControls == 2, "Both growth levels need a separate positive relic health control.");
            Debug.Log($"Greed choice health rewards passed: 18 matrix runs/270 actual judgements plus two complete relic controls/30 judgements,"
                + $" {choices} choices/{healthChoices} healing choices, recovered={recovered}, capped={capped}."
                + " Existing health rewards/caps/gauges unchanged, public choice/input/timing only; no personal data or private state edits, not physical/device testing.");
        }

        [MenuItem("Greed Last/Run Choice Focus Reward Check")]
        public static void RunChoiceFocusPreviews()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Focus choice checks require Edit mode.");
            int runs = 0, choices = 0, charges = 0, activations = 0, extensions = 0, capped = 0, mismatches = 0;
            float maxBeforeSeconds = 0f;
            var sources = new HashSet<string>();
            foreach (int growth in new[] { 0, 3 })
                for (int start = 0; start < 3; start++)
                    for (int gift = 0; gift < 3; gift++)
                        for (int route = 0; route < 3; route++)
                            Check(growth, start, gift, route);
            foreach (int growth in new[] { 0, 3 })
                for (int start = 0; start < 3; start++)
                    for (int relic = 1; relic <= 2; relic++) Check(growth, start, 0, 1, relic);
            Check(0, 2, 0, 1, 2, true);
            Debug.Log($"Greed choice focus audit: {runs} complete normal runs/{runs * 15} actual judgements, {choices} focus choices,"
                + $" charges={charges}, activations={activations}, extensions={extensions}, capped={capped},"
                + $" maximum before={maxBeforeSeconds:0.000}s, sources={string.Join(",", sources)}, mismatches={mismatches}."
                + " Public choice/input/timing only, no private state, shortcuts, persistence or physical/device claim.");
            Require(runs == 67 && choices == 257 && sources.Count == 5 && charges > 0 && activations > 0 && extensions > 0 && capped == 4,
                "Real focus choices must cover all five sources and charge/activation/extension/four capped relic choices, not privately seeded states.");
            Require(mismatches == 0, $"Focus choice notices must match actual gauge/state changes. Mismatches={mismatches}.");

            void Check(int growth, int start, int gift, int route, int relic = -1, bool allGood = false)
            {
                runs++;
                CreateBundleSamples(growth, start, gift, route, observeChoice: (kind, selected, sample) =>
                            {
                                int amount = selected == "집중 축" ? 2 : selected == "파라오의 각인" ? 3
                                    : selected == "박자 감각" || selected == "변주 루트" || selected == "사막의 잔상" ? 1 : 0;
                                if (amount == 0) return;
                                choices++;
                                sources.Add(selected);
                                var before = sample.Before;
                                var after = sample.After;
                                maxBeforeSeconds = Mathf.Max(maxBeforeSeconds, before.FocusStateRemainingSeconds);
                                string expected;
                                if (before.FocusStateActive)
                                {
                                    Require(after.Focus == before.Focus && after.FocusStateActive,
                                        "An active-state choice must not also charge a second focus gauge.");
                                    float gain = after.FocusStateRemainingSeconds - before.FocusStateRemainingSeconds;
                                    Require(gain >= -0.0001f && gain <= 0.3501f,
                                        "Active-state choices must preserve the existing capped extension, not multiply it by nominal focus.");
                                    if (gain > 0.001f) { extensions++; expected = "집중 상태 연장"; }
                                    else { capped++; expected = "집중 상태 상한 유지"; }
                                }
                                else if (after.FocusStateActive)
                                {
                                    activations++;
                                    expected = "집중 상태 발동";
                                    Require(after.Focus == 0 && after.FocusStateActivationCount == before.FocusStateActivationCount + 1,
                                        "A real choice activation must consume the charge and register exactly one state.");
                                }
                                else
                                {
                                    charges++;
                                    Require(after.Focus - before.Focus == amount, "Inactive choices must retain their authored charge.");
                                    expected = "집중 +" + amount;
                                }
                                string effect = Array.Find(after.JudgementText.Split('\n'), line => line.StartsWith("효과: ", StringComparison.Ordinal));
                                if (effect == null || !effect.Contains(expected, StringComparison.Ordinal))
                                {
                                    mismatches++;
                                    Debug.Log($"Greed choice focus mismatch: growth={growth}, kind={kind}, selected={selected},"
                                        + $" before={before.Focus}/{before.FocusStateRemainingSeconds:0.000}s,"
                                        + $" after={after.Focus}/{after.FocusStateRemainingSeconds:0.000}s, expected={expected}, effect={effect}.");
                                }
                }, relic: relic, allGood: allGood);
            }
        }

        internal static List<Sample> CreateBundleSamples(int growth, int start, int gift, int route, bool broken = false,
            Action<GreedLastChoiceKind, string, Sample> observeChoice = null, int relic = -1, bool allGood = false)
        {
            var samples = new List<Sample>();
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default, anticipation = default, anticipationBefore = default;
            core.SnapshotChanged += value => run = value;
            core.ConfigureGrowth(growth, growth, 0);
            core.Enter();
            float clock = Time.realtimeSinceStartup;
            int inputs = 0;
            while (!run.Stopped && inputs < 15)
            {
                if (run.ChoiceActive)
                {
                    int lane = run.ChoiceKind == GreedLastChoiceKind.StartGift ? start
                        : run.ChoiceKind == GreedLastChoiceKind.Node ? route
                        : run.ChoiceKind == GreedLastChoiceKind.Relic && relic >= 0 ? relic : gift;
                    var previous = run;
                    string selected = lane == 0 ? run.ChoiceLeft : lane == 1 ? run.ChoiceCenter : run.ChoiceRight;
                    core.HandleInput((GreedLastRunChannel)(lane + 1), clock);
                    observeChoice?.Invoke(previous.ChoiceKind, selected, new Sample(previous, run));
                    continue;
                }
                for (int wait = 0; !run.ActivePattern && !run.Stopped && wait < 500; wait++) Advance(0.01f);
                Require(run.ActivePattern && !run.CountdownActive && !run.DevInvincible,
                    "Finisher samples need naturally arriving normal/escape traps without shortcut or invincibility.");
                bool good = allGood || (broken && run.ChapterProgress == 0 && !string.IsNullOrEmpty(run.ActiveRouteProfile));
                float delta = good ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds);
                var before = run;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == (good ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Success),
                    "Finisher samples must use actual declared exact/Good judgements.");
                if (before.ChapterProgress == before.ChapterTarget - 1 && before.ActiveRouteProfile.EndsWith(" 루트", StringComparison.Ordinal))
                    samples.Add(new Sample(before, run, anticipation, anticipationBefore));
                if (before.ChapterProgress == before.ChapterTarget - 2)
                {
                    anticipationBefore = before;
                    anticipation = run;
                }
                inputs++;
            }
            Require(run.EscapeSucceeded && inputs == 15 && samples.Count == 4,
                "Every finisher sample must naturally clear 15 traps, all choices, core recovery and escape with four route bundles.");
            core.Exit();
            return samples;

            void Advance(float seconds)
            {
                Require(seconds >= -0.0002f, "Finisher checks must not rewind a core clock.");
                while (seconds > 0.0001f)
                {
                    float step = Mathf.Min(seconds, 0.01f);
                    core.Tick(clock += step);
                    seconds -= step;
                }
            }
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
