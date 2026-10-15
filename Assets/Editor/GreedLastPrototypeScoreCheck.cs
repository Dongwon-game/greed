using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeScoreCheck
    {
        [MenuItem("Greed Last/Run Score Boundary Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Score checks require Edit mode.");
            foreach (int initial in new[] { 0, int.MaxValue - 5, int.MaxValue })
            {
                foreach (GreedLastJudgementResult result in new[] { GreedLastJudgementResult.Success,
                    GreedLastJudgementResult.Good, GreedLastJudgementResult.Miss })
                    VerifyJudgement(initial, result);
            }
            VerifyJudgement(int.MaxValue - 5, GreedLastJudgementResult.Success, includeProfile: true);
            VerifyJudgement(int.MaxValue - 5, GreedLastJudgementResult.Good, includeProfile: true);
            VerifyBundleAwards();
            RunGrades();
            RunTargets();
            RunProgress();
            RunRecordStatistics();
            RunLoadoutQuality();
            VerifyRecordReload();
            VerifyLoadoutReload();
            RunRewardPreviews();
            RunResolvedBonusFeedback();
            Debug.Log("Greed score boundaries passed: actual exact/Good/Miss rewards, section/route rewards, grade and review targets, capped recent/best/ranking JSON reload, and all three isolated normal-save slots with one candidate and two starts. Numeric boundary values are injected in isolated editor cores/records; this is not a multi-hour play or device-performance test. Personal settings are preserved and personal slots are not accessed.");
        }

        [MenuItem("Greed Last/Run Live Reward Preview Check")]
        public static void RunRewardPreviews()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Reward preview checks require Edit mode.");
            Require(Invoke<string>(typeof(GreedLastMockBackend), "CompactRecordBoardLine",
                "[S] 1500점 / 정박 +140점 / 정박+240→집중 / 보정 +25 / 콤12") == "[S] 1500점 / 콤12"
                && Invoke<string>(typeof(GreedLastMockBackend), "CompactRecordBoardLine",
                    "정박 +140점 / 정박+240→집중 / 보정 +25 / 콤4") == "기록 없음",
                "Current center-score cues must not become record rows or contaminate a real score row.");
            int comparisons = 0, mismatches = 0, healthComparisons = 0, healthMismatches = 0;
            bool sawChain = false, sawFocus = false, sawRecovery = false, sawPressureBundle = false;
            bool sawThreatRise = false, sawHighGift = false;
            string[] summaries = { string.Empty, "안정 호흡 / 안정 루트 / 생존 축",
                "박자 감각 / 변주 루트 / 집중 축", "고점 본능 / 압박 루트 / 연쇄 축",
                "고점 본능 / 황금 박동", "고점 본능 / 압박 루트 / 연쇄 축 / 연쇄 축 / 파라오의 각인",
                "고점 본능 / 안정 루트 / 압박 루트 / 연쇄 축 / 파라오의 각인",
                "고점 본능 / 변주 루트 / 압박 루트 / 연쇄 축 / 파라오의 각인",
                "고점 본능 / 안정 루트 / 변주 루트 / 압박 루트 / 연쇄 축 / 파라오의 각인" };
            for (int loadout = 0; loadout < summaries.Length; loadout++)
                foreach (int focusGrowth in new[] { 0, 3 })
                    foreach (bool mixed in new[] { false, true })
                        CheckRun(loadout, focusGrowth, mixed, normal: false);
            for (int direction = 0; direction < 3; direction++)
                CheckRun(direction, 0, mixed: false, normal: true);
            Require(sawChain && sawFocus && sawRecovery && sawPressureBundle && sawThreatRise && sawHighGift,
                "Preview checks must cover chain, focus, recovery, pressure bundles, threat rises and high-score start gifts.");
            Require(mismatches == 0, "Live previews must equal the next actual center/Good judgement gain. Mismatches=" + mismatches);
            Require(healthComparisons > 0 && healthMismatches == 0,
                "Judgement health fractions must use the same maximum as the actual gauge. Mismatches=" + healthMismatches);
            VerifyHealthFeedbackControls();
            Require(BuildRewardPreviewSample(0).after.LastJudgementScoreGain == 188
                && BuildRewardPreviewSample(0, peripheral: true).after.LastJudgementScoreGain == 148
                && BuildRewardPreviewSample(0, good: true).after.LastJudgementScoreGain == 48,
                "Existing high-gift center/outer Success/Good rewards must remain 188/148/48, not be raised to the central preview.");
            Debug.Log($"Greed live reward previews passed: {comparisons} actual center/Good judgements,"
                + $" {healthComparisons} health-feedback comparisons, three complete normal directions, nine infinite loadouts including mixed routes,"
                + " focus growth 0/3, clean/mixed bundles, recovery and rising threat."
                + " Transient cores with public timing/input APIs; no private state edits, persistence, physical input or device-performance claim.");

            void CheckRun(int direction, int focusGrowth, bool mixed, bool normal)
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                core.ConfigureGrowth(0, focusGrowth, 0);
                if (normal) core.Enter();
                else
                {
                    string summary = summaries[direction];
                    string start = summary.Contains("고점 본능", StringComparison.Ordinal) ? "고점 본능"
                        : summary.Contains("박자 감각", StringComparison.Ordinal) ? "박자 감각" : "안정 호흡";
                    string gift = summary.Contains("연쇄 축", StringComparison.Ordinal) ? "연쇄 축"
                        : summary.Contains("집중 축", StringComparison.Ordinal) ? "집중 축" : "생존 축";
                    string relic = summary.Contains("황금 박동", StringComparison.Ordinal) ? "황금 박동"
                        : summary.Contains("파라오의 각인", StringComparison.Ordinal) ? "파라오의 각인" : string.Empty;
                    var record = direction == 0 ? GreedLastRunRecord.Empty : new GreedLastRunRecord(
                        true, 800, 3, 0, 0, 0, 0, 0, 0, 20f, "reward-preview", start, gift, relic, summary, string.Empty);
                    core.EnterInfiniteRun(record);
                }
                core.ToggleDevInvincible();
                float clock = Time.realtimeSinceStartup;
                int judged = 0, target = normal ? 15 : 18;
                while (judged < target)
                {
                    if (run.ChoiceActive)
                    {
                        core.HandleInput((GreedLastRunChannel)(direction + 1), clock);
                        continue;
                    }
                    for (int wait = 0; !run.ActivePattern && !run.Stopped && wait < 400; wait++) Advance(0.01f);
                    Require(run.ActivePattern && !run.CountdownActive, "Preview checks need a real approaching trap.");
                    int outcome = mixed ? judged % 6 : 0;
                    bool good = outcome == 2 || outcome == 5, miss = outcome == 3;
                    float delta = good ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                    Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds);
                    var before = run;
                    int profileBefore = miss ? 0 : ReadProfileBonusBeforeInput(core, before, good);
                    int threatBefore = miss || good || normal ? 0
                        : Invoke<int>(typeof(GreedLastRunCore), "BuildThreatPrecisionBonusForLevel", before.InfiniteThreatLevel, before.Pattern.TimingType);
                    int preview = Invoke<int>(typeof(GreedLastRuntimeUi), good ? "BuildActiveGoodScorePreview" : "BuildActiveExactScorePreview", before);
                    var input = miss ? (run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left)
                        : run.Pattern.Channel;
                    core.HandleInput(input, clock);
                    Require(run.LastResult == (miss ? GreedLastJudgementResult.Miss : good ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Success)
                        && (miss || good || run.LastCenterHit), "Preview checks must use the intended actual judgement.");
                    string healthLine = Array.Find(run.JudgementText.Split('\n'), line =>
                        line.StartsWith("결과:", StringComparison.Ordinal) && line.Contains(" / 체력 ", StringComparison.Ordinal));
                    if (healthLine != null)
                    {
                        healthComparisons++;
                        if (!healthLine.Contains(" / 체력 " + run.Health + "/" + run.MaxHealth, StringComparison.Ordinal))
                        {
                            healthMismatches++;
                            Debug.Log("Greed health feedback mismatch: gauge=" + run.Health + "/" + run.MaxHealth
                                + ", text=" + run.JudgementText.Replace("\n", " / "));
                        }
                    }
                    sawRecovery |= before.Pattern.Prompt.Contains("[복구]", StringComparison.Ordinal);
                    sawThreatRise |= run.InfiniteThreatLevel > before.InfiniteThreatLevel;
                    sawPressureBundle |= run.PressureRouteBundleCount > before.PressureRouteBundleCount;
                    sawFocus |= before.FocusStateActive;
                    sawChain |= run.Combo >= 3 && !miss && !good;
                    sawHighGift |= direction >= 3 && !normal || normal && direction == 2;
                    if (!miss)
                    {
                        comparisons++;
                        Require(run.LastJudgementProfileScoreBonus == profileBefore && run.LastJudgementThreatScoreBonus == threatBefore
                            && !run.LastJudgementScoreLimited,
                            "Live bonus accounting must retain the pre-hit profile/threat inputs across every normal/infinite Success and Good.");
                        if (preview != run.LastJudgementScoreGain)
                        {
                            mismatches++;
                            Debug.Log($"Greed reward preview mismatch: normal={normal}, direction={direction}, growth={focusGrowth}, mixed={mixed},"
                                + $" input={judged}, good={good}, combo={before.Combo}, bundle={before.ChapterProgress}, focus={before.FocusStateActive},"
                                + $" preview={preview}, actual={run.LastJudgementScoreGain}.");
                        }
                        Require(run.Score - before.Score == run.LastJudgementScoreGain, "Comparisons must include actual chain and bundle rewards.");
                    }
                    judged++;
                }
                if (normal) Require(run.EscapeSucceeded && run.SaveEligible, "Normal checks must finish the authored escape.");
                Require((run.CenterHitScoreGainPreview == 0 && run.GoodScoreGainPreview == 0) || run.ActivePattern,
                    "No active trap must not retain a reward preview after its judgement.");
                core.Exit();
                Require(run.CenterHitScoreGainPreview == 0 && run.GoodScoreGainPreview == 0,
                    "Leaving a run must clear its previous active score preview.");

                void Advance(float seconds)
                {
                    Require(seconds >= -0.0002f, "Preview checks must not rewind a core clock.");
                    while (seconds > 0.0001f)
                    {
                        float step = Mathf.Min(seconds, 0.01f);
                        core.Tick(clock += step);
                        seconds -= step;
                    }
                }
            }
        }

        [MenuItem("Greed Last/Run Resolved Score Feedback Check")]
        public static void RunResolvedBonusFeedback()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Resolved bonus checks require Edit mode.");
            int comparisons = 0, profileMismatches = 0, threatMismatches = 0, healingBoundaries = 0, threatBoundaries = 0;
            foreach (int growth in new[] { 0, 3 })
                foreach (var sample in CreateResolvedBonusSamples(growth))
                {
                    if (sample.after.LastResult != GreedLastJudgementResult.Success) continue;
                    comparisons++;
                    Require(sample.after.LastJudgementProfileScoreBonus == sample.profile
                        && sample.after.LastJudgementThreatScoreBonus == sample.threat && !sample.after.LastJudgementScoreLimited,
                        "Resolved bonus metadata must retain actual scoring inputs independently of the later recovery/threat state.");
                    if (sample.after.Health > sample.before.Health) healingBoundaries++;
                    if (sample.after.InfiniteThreatLevel > sample.before.InfiniteThreatLevel) threatBoundaries++;
                    string profile = Invoke<string>(typeof(GreedLastRuntimeUi), "BuildChoiceProfileScoreBonusPreview", sample.after);
                    int threat = (int)typeof(GreedLastRuntimeUi).GetMethod("BuildThreatPrecisionBonusPreview",
                        BindingFlags.Static | BindingFlags.NonPublic, null,
                        new[] { typeof(GreedLastRunSnapshot), typeof(GreedLastPatternModel) }, null)
                        .Invoke(null, new object[] { sample.after, sample.before.Pattern });
                    if (profile != "조합 +" + sample.profile)
                    {
                        profileMismatches++;
                        Debug.Log($"Greed resolved profile mismatch: growth={growth}, health={sample.before.Health}/{sample.before.MaxHealth}"
                            + $"->{sample.after.Health}/{sample.after.MaxHealth}, expected={sample.profile}, actual={profile}.");
                    }
                    if (threat != sample.threat)
                    {
                        threatMismatches++;
                        Debug.Log($"Greed resolved threat mismatch: growth={growth}, level={sample.before.InfiniteThreatLevel}"
                            + $"->{sample.after.InfiniteThreatLevel}, timing={sample.before.Pattern.TimingType}, expected={sample.threat}, actual={threat}.");
                    }
                }
            Debug.Log($"Greed resolved bonus audit: two transient infinite runs/36 actual judgements, success comparisons={comparisons},"
                + $" healing boundaries={healingBoundaries}, threat boundaries={threatBoundaries}, profile mismatches={profileMismatches}, threat mismatches={threatMismatches}."
                + " Public monotonic timing/input, natural two-Miss damage and actual stable finishers; pure core calculators read before input,"
                + " no private state edits, invincibility, shortcuts or persistence. Not physical/device testing.");
            Require(comparisons == 32 && healingBoundaries > 0 && threatBoundaries == 6,
                "Resolved bonus controls must naturally heal through stable finishers and cross three threat levels at both growth levels.");
            Require(profileMismatches == 0 && threatMismatches == 0,
                $"Resolved bonus details must use the scored state, not the next state. Profile={profileMismatches}, threat={threatMismatches}.");
        }

        internal static System.Collections.Generic.List<(GreedLastRunSnapshot before, GreedLastRunSnapshot after, int profile, int threat)> CreateResolvedBonusSamples(int growth)
        {
            var samples = new System.Collections.Generic.List<(GreedLastRunSnapshot, GreedLastRunSnapshot, int, int)>();
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.ConfigureGrowth(growth, 0, 0);
            core.EnterInfiniteRun(new GreedLastRunRecord(true, 0, 3, 0, 0, 0, 0, 0, 0, 20f,
                "resolved-bonus", string.Empty, string.Empty, string.Empty, "안정 루트", string.Empty));
            float clock = Time.realtimeSinceStartup;
            for (int input = 0; input < 18; input++)
            {
                for (int wait = 0; !run.ActivePattern && !run.Stopped && wait < 500; wait++) Advance(0.01f);
                Require(run.ActivePattern && !run.CountdownActive && !run.DevInvincible, "Bonus controls need naturally arriving traps with normal damage.");
                Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds);
                var before = run;
                bool miss = input < 2;
                int profile = 0, threat = 0;
                if (!miss)
                {
                    profile = ReadProfileBonusBeforeInput(core, before, false);
                    threat = Invoke<int>(typeof(GreedLastRunCore), "BuildThreatPrecisionBonusForLevel", run.InfiniteThreatLevel, run.Pattern.TimingType);
                }
                core.HandleInput(miss ? (run.Pattern.Channel == GreedLastRunChannel.Left
                    ? GreedLastRunChannel.Right : GreedLastRunChannel.Left) : run.Pattern.Channel, clock);
                Require(!run.Stopped && run.LastResult == (miss ? GreedLastJudgementResult.Miss : GreedLastJudgementResult.Success),
                    "Resolved bonus controls must survive their two real misses and score actual center hits.");
                samples.Add((before, run, profile, threat));
            }
            core.Exit();
            return samples;

            void Advance(float seconds)
            {
                Require(seconds >= -0.0002f, "Resolved bonus checks must not rewind a clock.");
                while (seconds > 0.0001f)
                {
                    float step = Mathf.Min(seconds, 0.01f);
                    core.Tick(clock += step);
                    seconds -= step;
                }
            }
        }

        private static int ReadProfileBonusBeforeInput(GreedLastRunCore core, GreedLastRunSnapshot snapshot, bool good)
        {
            if (good) return (int)typeof(GreedLastRunCore).GetMethod("BuildChoiceProfileGoodScoreBonus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(core, new object[] { null });
            int combo = snapshot.Combo + InvokeInstance<int>(core, "BuildSuccessComboGain", true);
            return (int)typeof(GreedLastRunCore).GetMethod("BuildChoiceProfileSuccessScoreBonus", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(bool), typeof(int), typeof(string).MakeByRefType() }, null)
                .Invoke(core, new object[] { true, combo, null });
        }

        private static void VerifyHealthFeedbackControls()
        {
            foreach (int survivalGrowth in new[] { 0, 3 })
                foreach (bool invincible in new[] { false, true })
                    foreach (bool miss in new[] { false, true })
                    {
                        var core = new GreedLastRunCore();
                        GreedLastRunSnapshot run = default;
                        core.SnapshotChanged += value => run = value;
                        core.ConfigureGrowth(survivalGrowth, 0, 0);
                        core.EnterInfiniteRun();
                        if (invincible) core.ToggleDevInvincible();
                        float clock = Time.realtimeSinceStartup;
                        for (int wait = 0; !run.ActivePattern && wait < 400; wait++) core.Tick(clock += 0.01f);
                        Require(run.ActivePattern, "Health controls require a naturally arriving trap.");
                        float delta = miss ? 0f : (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                        float remaining = run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds;
                        while (remaining > 0.0001f)
                        {
                            float step = Mathf.Min(remaining, 0.01f);
                            core.Tick(clock += step);
                            remaining -= step;
                        }
                        core.HandleInput(miss ? (run.Pattern.Channel == GreedLastRunChannel.Left
                            ? GreedLastRunChannel.Right : GreedLastRunChannel.Left) : run.Pattern.Channel, clock);
                        int max = 3 + survivalGrowth;
                        Require(run.LastResult == (miss ? GreedLastJudgementResult.Miss : GreedLastJudgementResult.Good)
                            && run.MaxHealth == max && run.Health == max - (miss && !invincible ? 1 : 0)
                            && run.JudgementText.Contains(" / 체력 " + run.Health + "/" + run.MaxHealth, StringComparison.Ordinal),
                            "Health feedback controls must preserve real damage, invincibility and current gauge maximum.");
                        core.Exit();
                    }
            Debug.Log("Greed judgement health controls passed: eight transient growth 0/3 x real damage/invincibility x Good/Miss cases; unchanged health/damage and gauge-consistent feedback. No private state edits or personal persistence.");
        }

        internal static (GreedLastRunSnapshot before, GreedLastRunSnapshot after) BuildRewardPreviewSample(
            int sample, bool peripheral = false, bool good = false)
        {
            Require(sample >= 0 && sample <= 2, "Reward samples must use a declared scenario.");
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default, before = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            if (sample == 0)
            {
                core.Enter();
                core.HandleInput(GreedLastRunChannel.Right, clock);
            }
            else
            {
                core.ConfigureGrowth(0, sample == 1 ? 3 : 0, 0);
                core.EnterInfiniteRun(new GreedLastRunRecord(true, 800, 3, 0, 0, 0, 0, 0, 0, 20f,
                    "reward-preview", "고점 본능", "연쇄 축", "파라오의 각인",
                    "고점 본능 / 압박 루트 / 연쇄 축 / 파라오의 각인", string.Empty));
            }
            int target = sample == 0 ? 1 : sample == 1 ? 3 : 6;
            for (int input = 0; input < target; input++)
            {
                for (int wait = 0; !run.ActivePattern && !run.Stopped && wait < 400; wait++) Advance(0.01f);
                Require(run.ActivePattern && !run.CountdownActive, "Reward render samples need a naturally arriving trap.");
                bool isGood = (sample == 2 || good) && input == target - 1;
                float delta = isGood ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f
                    : peripheral ? run.Pattern.SuccessWindowSeconds * 0.75f : 0f;
                Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds);
                before = run;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == (isGood ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Success)
                    && (isGood || run.LastCenterHit == !peripheral), "Render reward samples must use their declared judgement.");
                if (!peripheral) Require((isGood ? before.GoodScoreGainPreview : before.CenterHitScoreGainPreview)
                    == run.LastJudgementScoreGain, "Rendered reward samples must pair a current preview with its actual gain.");
            }
            GreedLastRunSnapshot after = run;
            core.Exit();
            return (before, after);

            void Advance(float seconds)
            {
                Require(seconds >= -0.0002f, "Reward samples must not rewind a core clock.");
                while (seconds > 0.0001f)
                {
                    float step = Mathf.Min(seconds, 0.01f);
                    core.Tick(clock += step);
                    seconds -= step;
                }
            }
        }

        [MenuItem("Greed Last/Run Score Grade Boundary Check")]
        public static void RunGrades()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Grade checks require Edit mode.");
            foreach (var example in new[] { (1940, "S"), (1240, "A"), (640, "B"), (0, "C"), (int.MaxValue, "S") })
                Require(Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteGrade", Record(example.Item1)) == example.Item2,
                    "Infinite grades must retain existing thresholds without overflow at score=" + example.Item1);
            Require(Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteGrade", Record(0, combo: int.MaxValue)) == "S"
                && Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteGrade", Record(int.MaxValue, misses: int.MaxValue)) == "C",
                "Large combo bonuses and Miss penalties must be evaluated before narrowing, not wrap across grades.");
            var normal = new GreedLastRunRecord(true, int.MaxValue, 3, 0, 0, 6, 15, 0, 0,
                90f, "score-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            Require(Invoke<string>(typeof(GreedLastMockBackend), "BuildRunGrade", normal) == "S",
                "A capped normal record must not receive C from grade addition overflow.");
            Require(Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", "2147483647점 갱신", true) == "정박 3연속"
                && Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", "2147483447점 갱신", true) == "2147483647점 갱신"
                && Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", "800점 탈출", false) == "1000점 탈출",
                "An advanced score goal must not wrap to 800 or repeat an unreachable goal at the numeric ceiling.");
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.EnterInfiniteRun();
            Field(core, "score").SetValue(core, int.MaxValue);
            core.Tick(Time.realtimeSinceStartup);
            Require(Invoke<string>(typeof(GreedLastRuntimeUi), "BuildInfiniteSnapshotGrade", run) == "S",
                "The live infinite grade must agree with a capped score rather than wrap to C.");
            Debug.Log("Greed score grade boundaries passed: existing S/A/B/C thresholds, wide intermediate sums/products, live grade and advancing score goal ceiling.");
        }

        [MenuItem("Greed Last/Run Score Target Boundary Check")]
        public static void RunTargets()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Target checks require Edit mode.");
            int failures = 0;
            void Check(string actual, string expected, string source)
            {
                Debug.Log($"Greed score target observation: source={source}, actual={actual}, expected={expected}.");
                if (actual != expected) failures++;
            }
            foreach (var example in new[] { (800, "1000점 갱신"), (int.MaxValue - 200, "2147483647점 갱신"),
                (int.MaxValue - 199, "정박 3연속"), (int.MaxValue, "정박 3연속") })
            {
                string summary = example.Item1 + "점 갱신 / 달성 " + example.Item1 + "점";
                Check(Invoke<string>(typeof(GreedLastMockBackend), "BuildAdvancedReviewTargetText", summary, true),
                    example.Item2, "result/" + example.Item1);
                Check(Invoke<string>(typeof(GreedLastApp), "BuildAdvancedReviewTargetTextForContext", summary, true),
                    example.Item2, "entry/" + example.Item1);
            }
            foreach (var example in new[] { (1650, "무한 2000점 갱신 / 현재 1650점"),
                (int.MaxValue - 500, "무한 2147483500점 갱신 / 현재 2147483147점"),
                (int.MaxValue - 100, "무한 2147483647점 갱신 / 현재 2147483547점"),
                (int.MaxValue, "다음 구간 무실수") })
            {
                var record = new GreedLastInfiniteRunRecord(true, example.Item1, 240f, 5, 5,
                    "score-boundary", string.Empty, 15, 0, 0, 12, "manual_stop",
                    "평균 정확 / 정박 최고 5 / 집중 발동 1회 / 최근 구간 실수 0회 / 최근 구간 보정 0회");
                Check(Invoke<string>(typeof(GreedLastMockBackend), "BuildInfiniteRunNextTargetText", record),
                    example.Item2, "milestone/" + example.Item1);
            }
            Require(failures == 0, "Result, entry and infinite milestone goals must agree without wrapping or exceeding the score ceiling. Failures=" + failures);
            Debug.Log("Greed score target boundaries passed: 12 result/entry/milestone cases; existing targets remain unchanged below the numeric ceiling.");
        }

        internal static (GreedLastRunSnapshot before, GreedLastRunSnapshot after) VerifyJudgement(
            int initial, GreedLastJudgementResult expected, bool includeProfile = false)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.EnterInfiniteRun(includeProfile ? new GreedLastRunRecord(true, 0, 3, 0, 0, 0, 0, 0, 0, 20f,
                "capped-profile", "안정 호흡", "생존 축", string.Empty, "안정 호흡 / 안정 루트 / 생존 축", string.Empty)
                : GreedLastRunRecord.Empty);
            void Advance(float seconds)
            {
                while (seconds > 0.0001f)
                {
                    float step = Mathf.Min(seconds, 0.04f);
                    core.Tick(clock += step);
                    seconds -= step;
                }
            }
            for (int wait = 0; !run.ActivePattern && wait < 200; wait++) Advance(0.04f);
            Require(run.ActivePattern, "Score boundary checks need a real automatically arriving trap.");
            // Only the cumulative score is seeded; clocks, trap timing and resolution remain real core APIs.
            Field(core, "score").SetValue(core, initial);
            core.Tick(clock);
            float delta = expected == GreedLastJudgementResult.Good
                ? -(run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
            Advance(run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta - run.ElapsedSeconds);
            GreedLastRunSnapshot before = run;
            if (initial > 0)
            {
                int available = int.MaxValue - initial;
                Require(Invoke<int>(typeof(GreedLastRuntimeUi), "BuildActiveExactScorePreview", before) == available
                    && Invoke<int>(typeof(GreedLastRuntimeUi), "BuildActiveGoodScorePreview", before) == available,
                    "Before-input exact and Good previews must show only the points still available at the score ceiling.");
            }
            GreedLastRunChannel channel = expected == GreedLastJudgementResult.Miss
                ? (run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left)
                : run.Pattern.Channel;
            core.HandleInput(channel, clock);
            Debug.Log($"Greed score boundary observation: initial={initial}, expected={expected}, actual={run.LastResult}, score={run.Score}, gain={run.LastJudgementScoreGain}.");
            Require(run.LastResult == expected && run.Score >= initial
                && run.LastJudgementScoreGain == (long)run.Score - initial,
                "A real judgement must not wrap a cumulative score or lose its actual reward at the integer boundary.");
            Require(expected == GreedLastJudgementResult.Miss ? run.Score == initial
                : initial == 0 ? run.Score > 0 : run.Score == int.MaxValue,
                "Score rewards must remain unchanged normally and saturate only at the existing storage ceiling.");
            bool limited = initial > 0 && expected != GreedLastJudgementResult.Miss;
            Require(run.LastJudgementScoreLimited == limited,
                "Only a real capped award may mark its judgement as score limited, not normal rewards or Miss.");
            if (limited) Require(Invoke<string>(typeof(GreedLastRuntimeUi), expected == GreedLastJudgementResult.Good
                    ? "BuildGoodEffectLine" : "BuildSuccessEffectLine", run).Contains("점수 상한", StringComparison.Ordinal),
                "Capped feedback must not present every nominal bonus as fully awarded points.");
            if (includeProfile)
            {
                string profile = Invoke<string>(typeof(GreedLastRuntimeUi), "BuildChoiceProfileScoreBonusPreview", run);
                Require(limited && run.LastJudgementProfileScoreBonus > 0 && profile.Contains("점수 상한", StringComparison.Ordinal)
                    && !profile.Contains("조합 +", StringComparison.Ordinal),
                    "A partially paid profile component must not claim its entire nominal amount was awarded.");
            }
            GreedLastRunSnapshot afterJudgement = run;
            int judged = run.SuccessCount + run.GoodCount + run.MissCount;
            core.HandleInput(channel, clock);
            Require(run.SuccessCount + run.GoodCount + run.MissCount == judged && run.Score == before.Score + run.LastJudgementScoreGain,
                "A duplicate input must not re-award a boundary judgement.");
            Require(core.CreateInfiniteRunRecord().Score == run.Score && !run.SaveEligible,
                "Boundary records must preserve the current infinite score without creating a normal-save candidate.");
            return (before, afterJudgement);
        }

        private static void VerifyBundleAwards()
        {
            foreach (bool pressure in new[] { false, true })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                core.EnterInfiniteRun();
                Field(core, "score").SetValue(core, int.MaxValue - 5);
                Field(core, "chapterProgress").SetValue(core, 2);
                Field(core, "activeInfiniteLoadoutDisplayProfile").SetValue(core, pressure ? "압박 루트" : string.Empty);
                typeof(GreedLastRunCore).GetMethod("AdvanceRunAfterPattern", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(core, new object[] { Time.realtimeSinceStartup });
                core.Tick(Time.realtimeSinceStartup);
                Require(run.Score == int.MaxValue && run.InfiniteSectionsCleared == 1,
                    "Section and pressure-route awards must not wrap the cumulative score.");
            }
        }

        private static GreedLastInfiniteRunRecord Record(int score, float distance = 100f, int combo = 0, int misses = 0)
            => new GreedLastInfiniteRunRecord(true, score, distance, 0, 1, "score-boundary", string.Empty,
                3, 0, misses, combo, "no_input", string.Empty);

        [MenuItem("Greed Last/Run Progress Boundary Check")]
        public static void RunProgress()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Progress checks require Edit mode.");
            int failures = 0;
            void Check(bool passed, string observation)
            {
                Debug.Log("Greed progress boundary observation: " + observation + ", passed=" + passed);
                if (!passed) failures++;
            }
            foreach (int initial in new[] { 0, int.MaxValue - 2, int.MaxValue })
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                Field(backend, "growthPoints").SetValue(backend, initial);
                int expected = (int)Math.Min(int.MaxValue, (long)initial + 3);
                int actual = backend.AddGrowthPointsForTest(3).GrowthPoints;
                Check(actual == expected, "growth initial=" + initial + ", actual=" + actual + ", expected=" + expected);
                Check(backend.AddGrowthPointsForTest(-3).GrowthPoints == expected,
                    "negative development reward must not remove earned points at initial=" + initial);
            }
            foreach (int initial in new[] { 0, int.MaxValue - 1, int.MaxValue })
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                string[] counters = { "normalClearCount", "normalFailCount", "normalAbandonCount", "infiniteRunCount" };
                foreach (string counter in counters) Field(backend, counter).SetValue(backend, initial);
                var normal = new GreedLastRunRecord(true, 100, 3, 0, 0, 1, 1, 0, 0, 12f,
                    "progress-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                backend.RegisterClear(normal);
                foreach (GreedLastRunAttemptOutcome outcome in new[] { GreedLastRunAttemptOutcome.Failed, GreedLastRunAttemptOutcome.Abandoned })
                    backend.RegisterNormalAttempt(new GreedLastRunAttemptRecord(true, outcome, 100, 0, 0, 0,
                        1, 1, 0, 1, 12f, 1, 1, 3, false, "no_input", string.Empty, string.Empty));
                backend.RegisterInfiniteRun(Record(100));
                int expected = initial == int.MaxValue ? initial : initial + 1;
                foreach (string counter in counters)
                {
                    int actual = (int)Field(backend, counter).GetValue(backend);
                    Check(actual == expected, counter + " initial=" + initial + ", actual=" + actual + ", expected=" + expected);
                }
                string progress = InvokeInstance<string>(backend, "BuildRunProgressSummaryText");
                Check(progress.Contains("일반 " + ((long)expected * 3) + "회", StringComparison.Ordinal)
                    && progress.Contains("클리어율 50%", StringComparison.Ordinal),
                    "wide totals and clear rate initial=" + initial + ", actual=" + progress);
                string tier = InvokeInstance<string>(backend, "BuildProfileTierLabel");
                Check(tier == (initial == 0 ? "B" : "S"), "profile tier initial=" + initial + ", actual=" + tier);
            }
            var retries = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            Field(retries, "normalFailCount").SetValue(retries, int.MaxValue);
            Field(retries, "normalAbandonCount").SetValue(retries, int.MaxValue);
            Check(InvokeInstance<string>(retries, "BuildProfileBadgeName") == "다시 일어서는 자"
                && InvokeInstance<string>(retries, "BuildPrototypePlaytestChecklistText").Contains("일반 재도전 필요", StringComparison.Ordinal),
                "large retry counters must retain their existing retry badge and checklist");
            foreach (int initial in new[] { 100_000_000, int.MaxValue })
            {
                var sample = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                Field(sample, "normalClearCount").SetValue(sample, initial);
                Field(sample, "infiniteRunCount").SetValue(sample, initial);
                Field(sample, "sampleSaveLoadoutSequence").SetValue(sample, initial);
                sample.AddSampleSaveLoadoutForTest(out _);
                var saved = (GreedLastRunRecord[])Field(sample, "saveLoadoutSlots").GetValue(sample);
                Check(saved[0].IsValid && saved[0].Score == int.MaxValue
                    && saved[0].Distance > 0f && !float.IsInfinity(saved[0].Distance)
                    && (int)Field(sample, "sampleSaveLoadoutSequence").GetValue(sample) >= initial,
                    "development sample must remain bounded at progress seed=" + initial + ", score=" + saved[0].Score);
            }
            var candidate = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            Field(candidate, "sampleSaveLoadoutSequence").SetValue(candidate, int.MaxValue);
            candidate.AddSampleClearCandidateForTest(out _);
            var candidateRecord = (GreedLastRunRecord)Field(candidate, "lastRunRecord").GetValue(candidate);
            Check(candidate.HasSaveLoadoutCandidate && candidateRecord.Score == int.MaxValue
                && candidateRecord.LoadoutName == "테스트 후보 S1-2147483648",
                "development clear candidate must preserve a positive sequence name at the ceiling");
            Require(failures == 0, "Growth, persistent play counts and profile aggregates must not wrap. Failures=" + failures);
            VerifyProgressReload();
            Debug.Log("Greed progress boundaries passed: three growth seeds, three real registration seeds across four counters, wide totals/clear rate/profile tier, retry presence, two development samples, one development candidate, and three isolated JSON reload/spend/reward/two-use cases. Only non-persisting backends and assigned temporary test keys are used; no personal save key or settings are accessed.");
        }

        private static void VerifyProgressReload()
        {
            foreach (int initial in new[] { 0, int.MaxValue - 2, int.MaxValue })
            {
                string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "progress-boundary-" + Guid.NewGuid().ToString("N");
                try
                {
                    var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    string[] counters = { "normalClearCount", "normalFailCount", "normalAbandonCount", "infiniteRunCount" };
                    foreach (string counter in counters) Field(backend, counter).SetValue(backend, initial);
                    Field(backend, "growthPoints").SetValue(backend, initial);
                    backend.RegisterClear(new GreedLastRunRecord(true, 100, 3, 0, 0, 1, 1, 0, 0, 12f,
                        "progress-boundary", "박자 감각", "집중 축", string.Empty, "박자 감각 / 집중 축", string.Empty));
                    backend.SelectSaveLoadoutSlot(1);
                    backend.ConfirmSaveLoadout();
                    foreach (GreedLastRunAttemptOutcome outcome in new[] { GreedLastRunAttemptOutcome.Failed, GreedLastRunAttemptOutcome.Abandoned })
                        backend.RegisterNormalAttempt(new GreedLastRunAttemptRecord(true, outcome, 100, 0, 0, 0,
                            1, 1, 0, 1, 12f, 1, 1, 3, false, "no_input", string.Empty, string.Empty));
                    backend.RegisterInfiniteRun(Record(100));
                    int points = (int)Math.Min(int.MaxValue, (long)initial + 5);
                    int plays = (int)Math.Min(int.MaxValue, (long)initial + 1);
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.LoadLobby().GrowthPoints == points && !backend.HasSaveLoadoutCandidate,
                        "JSON reload must preserve capped growth and the already-consumed clear candidate.");
                    foreach (string counter in counters)
                        Require((int)Field(backend, counter).GetValue(backend) == plays,
                            "Each persistent play counter must retain its capped registration through JSON: " + counter);
                    int cost = Invoke<int>(typeof(GreedLastMockBackend), "BuildGrowthUpgradeCost", 0);
                    backend.UpgradeGrowth(GreedLastRunChannel.Center, out _);
                    points -= cost;
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.GrowthFocusLevel == 1 && backend.LoadLobby().GrowthPoints == points,
                        "An earned growth balance at the ceiling must still be spendable at its existing cost.");
                    backend.RegisterInfiniteRun(Record(200, 120f));
                    points = (int)Math.Min(int.MaxValue, (long)points + 1);
                    int infinitePlays = (int)Math.Min(int.MaxValue, (long)plays + 1);
                    for (int use = 0; use < 2; use++)
                    {
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(backend.TryConsumeSelectedSaveLoadout(out var used, out _, out var error)
                            && used.Score == 100 && string.IsNullOrEmpty(error),
                            "The saved S2 loadout must still permit exactly two real backend starts.");
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        var lobby = backend.LoadLobby();
                        Require(lobby.GrowthPoints == points && !lobby.SaveSlotOccupied[0] && !lobby.SaveSlotOccupied[2]
                            && lobby.SaveSlotOccupied[1] == (use == 0) && !backend.HasSaveLoadoutCandidate,
                            "Use consumption must preserve growth/other slots and auto-clear only S2 after its second start.");
                        foreach (string counter in counters)
                            Require((int)Field(backend, counter).GetValue(backend) == (counter == "infiniteRunCount" ? infinitePlays : plays),
                                "Starting a saved loadout must not register a finished run or alter other counters.");
                    }
                    Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                        "A third backend start must remain unavailable after reload.");
                }
                finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            }
        }

        private static void VerifyRecordReload()
        {
            string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "score-boundary-" + Guid.NewGuid().ToString("N");
            try
            {
                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                Require(backend.RegisterInfiniteRun(Record(int.MaxValue, 100f))
                    && backend.RegisterInfiniteRun(Record(int.MaxValue, 120f)),
                    "Capped scores must still use distance to resolve tied records.");
                string saved = PlayerPrefs.GetString(key);
                string board = backend.BuildRecordBoardText(2);
                var reloaded = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                foreach (string name in new[] { "lastInfiniteRunRecord", "bestInfiniteRunRecord" })
                {
                    var record = (GreedLastInfiniteRunRecord)Field(reloaded, name).GetValue(reloaded);
                    Require(record.Score == int.MaxValue && record.Distance == 120f,
                        "JSON must retain capped recent and best scores and the normal tie-breaker.");
                }
                var ranks = (GreedLastInfiniteRunRecord[])Field(reloaded, "infiniteRunRankings").GetValue(reloaded);
                var lobby = reloaded.LoadLobby();
                Require(ranks[0].Score == int.MaxValue && ranks[0].Distance == 120f && ranks[1].Distance == 100f
                    && reloaded.BuildRecordBoardText(2) == board && board.Contains("[S]", StringComparison.Ordinal)
                    && PlayerPrefs.GetString(key) == saved && !reloaded.HasSaveLoadoutCandidate
                    && !lobby.SaveSlotOccupied[0] && !lobby.SaveSlotOccupied[1] && !lobby.SaveSlotOccupied[2],
                    "Capped record reload must preserve ranking, grade and text without changing slots or persistence.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [MenuItem("Greed Last/Run Record Statistics Boundary Check")]
        public static void RunRecordStatistics()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Record statistics checks require Edit mode.");
            int failures = 0;
            foreach (var example in new[] {
                (0, 0, 0, 0, 0, 0), (8, 1, 1, 80, 10, 10), (1, 1, 1, 33, 34, 33),
                (int.MaxValue, int.MaxValue, 0, 50, 50, 0), (int.MaxValue, 0, int.MaxValue, 50, 0, 50),
                (int.MaxValue, int.MaxValue, int.MaxValue, 33, 34, 33), (-1, 5, 0, 0, 100, 0) })
            {
                string expected = $"정확 {example.Item4}% · 보정 {example.Item5}% · 실수 {example.Item6}%";
                string actual = Invoke<string>(typeof(GreedLastMockBackend), "BuildJudgementRatioSummary",
                    example.Item1, example.Item2, example.Item3);
                int exact = Invoke<int>(typeof(GreedLastMockBackend), "CalculateExactRatePercent",
                    example.Item1, example.Item2, example.Item3);
                string flow = Invoke<string>(typeof(GreedLastMockBackend), "BuildJudgementFlowSummary",
                    example.Item1, example.Item2, example.Item3, string.Empty);
                bool hasData = (long)Math.Max(0, example.Item1) + Math.Max(0, example.Item2) + Math.Max(0, example.Item3) > 0;
                bool passed = actual == expected && exact == example.Item4
                    && (hasData ? flow.Contains(expected, StringComparison.Ordinal) : flow == "판정 기록 없음");
                Debug.Log($"Greed record statistics observation: exact={example.Item1}, Good={example.Item2}, Miss={example.Item3}, actual={actual}, rate={exact}, passed={passed}.");
                if (!passed) failures++;
            }
            var record = new GreedLastRunRecord(true, 100, 3, 0, 0, 1, int.MaxValue, int.MaxValue, 0,
                12f, "statistics-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            if (Invoke<string>(typeof(GreedLastMockBackend), "FormatRunRecordJudgementCounts", record)
                .Contains("기록 없음", StringComparison.Ordinal)) failures++;
            var notClean = new GreedLastRunRecord(true, 100, 3, 0, 0, 1, 1, int.MaxValue, 1,
                12f, "statistics-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            if (Invoke<bool>(typeof(GreedLastMockBackend), "HasCleanRunRecord", notClean)) failures++;
            foreach (var example in new[] { (279.25f, "C"), (279.75f, "B"), (549.25f, "A"), (549.75f, "S"),
                (5_000_000_000f, "S"), (float.MaxValue, "S") })
            {
                var distanceRecord = new GreedLastRunRecord(true, 0, 0, 0, 0, 0, 0, 0, 0, example.Item1,
                    "distance-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                string grade = Invoke<string>(typeof(GreedLastMockBackend), "BuildRunGrade", distanceRecord);
                Debug.Log($"Greed normal distance grade observation: distance={example.Item1}, grade={grade}, expected={example.Item2}.");
                if (grade != example.Item2) failures++;
            }
            foreach (var example in new[] { (8, 1, 1, string.Empty, 1, 0, 40),
                (1, 2, 0, string.Empty, 0, 0, 0), (3, 0, 2, string.Empty, 0, 0, 0),
                (0, 0, 0, "정박 최고 3", 1, 0, 36), (0, 0, 0, "정박 최고 4", 1, 0, 48),
                (0, 0, 0, "정박 최고 5", 1, 1, 100), (0, 0, 0, "정박 최고 7", 1, 1, 120),
                (int.MaxValue, int.MaxValue, 0, string.Empty, 1, 0, 40),
                (int.MaxValue, int.MaxValue, 1, string.Empty, 0, 0, 0),
                (0, 0, 0, "정박 최고 2147483647", 1, 1, 120) })
            {
                var bonusRecord = new GreedLastRunRecord(true, 0, 0, 0, 0, 0, example.Item1, example.Item2, example.Item3,
                    12f, "bonus-statistics", string.Empty, string.Empty, string.Empty, string.Empty, example.Item4);
                var bonus = Invoke<GreedLastInfiniteLoadoutBonus>(typeof(GreedLastRunCore), "CalculateInfiniteLoadoutBonus", bonusRecord);
                bool passed = bonus.FocusBonus == example.Item5 && bonus.ComboBonus == example.Item6 && bonus.ScoreBonus == example.Item7;
                Debug.Log($"Greed infinite statistics bonus observation: focus={bonus.FocusBonus}, combo={bonus.ComboBonus}, score={bonus.ScoreBonus}, passed={passed}.");
                if (!passed) failures++;
            }
            foreach (var example in new[] { (3, 3, 1, false), (1, 1, 3, true),
                (int.MaxValue, int.MaxValue, int.MaxValue, false) })
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                var infinite = new GreedLastInfiniteRunRecord(true, 0, 12f, 0, 1, string.Empty, string.Empty,
                    example.Item1, example.Item2, example.Item3, 0, string.Empty, string.Empty);
                Field(backend, "lastInfiniteRunRecord").SetValue(backend, infinite);
                string hint = InvokeInstance<string>(backend, "BuildInfiniteRunTrainingHint", infinite);
                object[] recommendation = { 0, string.Empty };
                bool hasRecommendation = InvokeInstance<bool>(backend, "TrySelectInfiniteGrowthRecommendation", recommendation);
                var attempt = new GreedLastRunAttemptRecord(true, GreedLastRunAttemptOutcome.Abandoned, 0, 0, 0, 0,
                    1, example.Item1, example.Item2, example.Item3, 12f, 1, 1, 3, false, string.Empty, string.Empty, string.Empty);
                string normalHint = Invoke<string>(typeof(GreedLastMockBackend), "BuildNormalAttemptTrainingHint", attempt);
                bool passed = hint.Contains("위험도 상승 전 보정권 입력 안정화", StringComparison.Ordinal) == example.Item4
                    && hasRecommendation == example.Item4
                    && (!hasRecommendation || (int)recommendation[0] == 2)
                    && normalHint.Contains("타이밍보다 생존 보정 우선", StringComparison.Ordinal) == example.Item4;
                Debug.Log($"Greed record guidance observation: counts={example.Item1}/{example.Item2}/{example.Item3}, needsCorrection={example.Item4}, passed={passed}.");
                if (!passed) failures++;
            }
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.EnterInfiniteRun();
            foreach (string counter in new[] { "successCount", "goodCount", "missCount" })
                Field(core, counter).SetValue(core, int.MaxValue);
            core.Tick(Time.realtimeSinceStartup);
            if (Invoke<string>(typeof(GreedLastRuntimeUi), "BuildInfiniteStoppedHighlight", snapshot) != "기록 확인") failures++;
            Require(failures == 0, "Large judgement totals must retain records, ratios and clean-run requirements. Failures=" + failures);
            Debug.Log("Greed record statistics boundaries passed: seven ratio cases, record presence, clean-run comparison, six normal-distance grades, ten infinite start bonuses, three backend guidance cases and stopped review guidance. Existing half-point thresholds retained. Constructed records only; no personal persistence, performance or multi-hour play measurement.");
        }

        [MenuItem("Greed Last/Run Loadout Quality Boundary Check")]
        public static void RunLoadoutQuality()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Quality checks require Edit mode.");
            int cases = 0, failures = 0;
            void Check(bool passed, string detail)
            {
                cases++;
                if (passed) return;
                failures++;
                Debug.Log("Greed quality boundary mismatch: " + detail);
            }
            GreedLastRunRecord Normal(int score = 800, int health = 3, int focus = 1, int combo = 6,
                int exact = 15, int good = 0, int miss = 0, float distance = 90f, string timing = "")
                => new GreedLastRunRecord(true, score, health, 0, focus, combo, exact, good, miss, distance,
                    "quality-boundary", string.Empty, string.Empty, string.Empty, string.Empty, timing);
            double Reference(GreedLastRunRecord record)
            {
                float scaled = record.Distance * 2.2f;
                double distance = (double)scaled >= int.MinValue && (double)scaled <= int.MaxValue
                    ? Mathf.RoundToInt(scaled) : Math.Round((double)record.Distance * 2.2f);
                int profile = Invoke<string>(typeof(GreedLastMockBackend), "BuildLoadoutProfile", record) == "균형형" ? 15 : 35;
                int clean = record.MissCount == 0 ? 90 : record.MissCount == 1 ? 35 : 0;
                return record.Score + distance + (long)record.Health * 95 + (long)record.Focus * 75
                    + (long)record.MaxCombo * 55 + (long)record.SuccessCount * 16 + (long)record.GoodCount * 5
                    + clean + profile - (long)record.MissCount * 85;
            }
            double Quality(GreedLastRunRecord record)
                => Convert.ToDouble(Invoke<object>(typeof(GreedLastMockBackend), "BuildSaveLoadoutQualityScore", record));

            foreach (int score in new[] { 0, 800, 1240, 1940 })
            foreach (float distance in new[] { 0f, 6.3f, 90f, 145.75f })
            foreach (int health in new[] { 2, 3 })
            {
                GreedLastRunRecord record = Normal(score, health, distance: distance);
                Check(Quality(record) == Reference(record), "ordinary weights/rounding at " + score + "/" + distance + "/" + health);
            }
            var boundaries = new[] { Normal(int.MaxValue), Normal(health: int.MaxValue), Normal(focus: int.MaxValue),
                Normal(combo: int.MaxValue), Normal(exact: int.MaxValue), Normal(good: int.MaxValue),
                Normal(miss: int.MaxValue), Normal(distance: 5_000_000_000f), Normal(distance: float.MaxValue) };
            foreach (GreedLastRunRecord record in boundaries)
            {
                double actual = Quality(record), expected = Reference(record);
                Check(!double.IsNaN(actual) && !double.IsInfinity(actual) && actual == expected,
                    "quality expected=" + expected + ", actual=" + actual + ", score=" + record.Score
                        + ", distance=" + record.Distance + ", health=" + record.Health + ", combo=" + record.MaxCombo);
            }
            GreedLastRunRecord baseline = Normal();
            foreach (GreedLastRunRecord record in boundaries)
            {
                double delta = Reference(record) - Reference(baseline);
                bool actual = Invoke<bool>(typeof(GreedLastMockBackend), "IsBetterSaveLoadoutRecord", record, baseline);
                Check(actual == (delta > 0), "recommendation expected=" + (delta > 0) + ", actual=" + actual + ", expected delta=" + delta);
                string expected = "품질 " + (delta >= 0 ? "+" : string.Empty) + delta;
                string text = Invoke<string>(typeof(GreedLastMockBackend), "FormatSaveLoadoutQualityDelta", record, baseline);
                Check(text == expected, "comparison expected=" + expected + ", actual=" + text);
            }
            foreach (var example in new[] { ("정박 최고 " + int.MaxValue, 70),
                ("집중 발동 " + int.MaxValue + "회", 75), ("집중 성공 " + int.MaxValue + "회", 20) })
            {
                GreedLastRunRecord record = Normal(timing: example.Item1);
                Check(Quality(record) == Reference(record) + (long)int.MaxValue * example.Item2,
                    "profile counter multiplier " + example.Item2 + ", actual=" + Quality(record));
            }

            string AllSegments(string kind)
            {
                string text = kind == "정확" ? string.Empty : "1장 정확 0회";
                foreach (string segment in new[] { "1장", "2장", "3장", "4장", "탈출" })
                    text += (text.Length == 0 ? string.Empty : " / ") + segment + " " + kind + " " + int.MaxValue + "회";
                return text;
            }
            foreach (var example in new[] { ("1장 정확 " + int.MaxValue + "회", (long)int.MaxValue * 4 + 60),
                (AllSegments("정확"), (long)int.MaxValue * 28 + 60),
                (AllSegments("보정"), (long)int.MaxValue * 7 + 25),
                (AllSegments("실수"), -(long)int.MaxValue * 126) })
            {
                long actual = Convert.ToInt64(Invoke<object>(typeof(GreedLastMockBackend), "BuildSegmentStabilityQualityBonus", example.Item1));
                Check(actual == example.Item2, "segment quality expected=" + example.Item2 + ", actual=" + actual);
                GreedLastRunRecord record = Normal(timing: example.Item1);
                Check(Quality(record) == Reference(record) + example.Item2, "wide segment bonus must remain in total quality");
                string summary = Invoke<string>(typeof(GreedLastMockBackend), "BuildSaveLoadoutQualitySummary", record);
                Check(summary.Contains(" / 구간 " + (example.Item2 >= 0 ? "+" : string.Empty) + example.Item2, StringComparison.Ordinal),
                    "wide segment text must retain its integer amount and sign: " + summary);
            }
            for (int strongest = 0; strongest < 3; strongest++)
            {
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                int weakest = (strongest + 1) % 3;
                for (int slot = 0; slot < 3; slot++)
                {
                    backend.RegisterClear(Normal(score: slot == strongest ? int.MaxValue : slot == weakest ? 800 : 1200));
                    backend.SelectSaveLoadoutSlot(slot);
                    backend.ConfirmSaveLoadout();
                }
                Check(InvokeInstance<int>(backend, "FindBestUsableSaveLoadoutSlot") == strongest, "best slot must retain the capped record at S" + (strongest + 1));
                Check(InvokeInstance<int>(backend, "FindWeakestSaveLoadoutSlot") == weakest, "weakest slot must remain S" + (weakest + 1));
                backend.RegisterClear(Normal(score: 1240));
                Check(InvokeInstance<int>(backend, "FindRecommendedSaveLoadoutSlotForCandidate") == weakest,
                    "a moderate new candidate must recommend the weakest slot, not the capped one");
            }
            Require(failures == 0, "Loadout quality must preserve ordinary weights and wide recommendation/comparison sums. Cases=" + cases + ", failures=" + failures);
            Debug.Log("Greed loadout quality passed: " + cases + " constructed arithmetic/ordering/text cases, ordinary weights/rounding retained. No personal persistence or device/full-play measurement.");
        }

        private static void VerifyLoadoutReload()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                foreach (int score in new[] { int.MaxValue - 5, int.MaxValue })
                {
                    string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "normal-score-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        var record = new GreedLastRunRecord(true, score, 3, 0, 0, 6, 15, 0, 0, 90f,
                            "score-boundary", "박자 감각", "집중 축", "황금 박동",
                            "박자 감각 / 안정 루트 / 집중 축 / 황금 박동", string.Empty);
                        var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(backend.RegisterClear(record).SaveLoadoutCandidateAvailable,
                            "A capped normal clear must still offer its one save candidate.");
                        backend.SelectSaveLoadoutSlot(slot);
                        backend.ConfirmSaveLoadout();
                        var justSaved = ((GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend))[slot];
                        double quality = Convert.ToDouble(Invoke<object>(typeof(GreedLastMockBackend), "BuildSaveLoadoutQualityScore", justSaved));
                        Require(quality > int.MaxValue && backend.BuildSaveConfirmMessage().Contains("품질 " + quality, StringComparison.Ordinal),
                            "A capped saved record must retain its positive wide quality in the actual confirmation.");
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(!backend.HasSaveLoadoutCandidate, "Saving a capped normal record must consume its candidate across reload.");
                        var saved = (GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend);
                        Require(saved[slot].IsValid && saved[slot].Score == score
                            && saved[slot].PrimaryGift == record.PrimaryGift && string.IsNullOrEmpty(saved[slot].Relic)
                            && !saved[slot].ChoiceSummary.Contains(record.Relic, StringComparison.Ordinal),
                            "The chosen slot must retain capped score/gift while excluding the normal-run-only relic through JSON reload.");
                        Require(Convert.ToDouble(Invoke<object>(typeof(GreedLastMockBackend), "BuildSaveLoadoutQualityScore", saved[slot])) == quality,
                            "Wide quality must be reconstructed unchanged from the existing JSON record fields.");
                        backend.SelectSaveLoadoutSlot((slot + 1) % 3);
                        backend.ConfirmSaveLoadout();
                        Require(!backend.HasSaveLoadoutCandidate
                            && !((GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend))[(slot + 1) % 3].IsValid,
                            "A second confirmation must not copy the consumed candidate into another slot.");
                        backend.SelectSaveLoadoutSlot(slot);
                        for (int use = 0; use < 2; use++)
                        {
                            backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                            string beforePreparation = PlayerPrefs.GetString(key);
                            Require(!string.IsNullOrEmpty(backend.BuildActiveLoadoutText())
                                && !string.IsNullOrEmpty(backend.BuildInfiniteStartReadyText())
                                && PlayerPrefs.GetString(key) == beforePreparation
                                && ((int[])Field(backend, "saveLoadoutUsesRemaining").GetValue(backend))[slot] == 2 - use,
                                "Preparation previews must not consume or rewrite a capped loadout.");
                            Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord consumed, out _, out string error)
                                && consumed.Score == score && string.IsNullOrEmpty(error),
                                "Each actual start must retain the capped source record and consume exactly one use.");
                            var core = new GreedLastRunCore();
                            GreedLastRunSnapshot run = default;
                            core.SnapshotChanged += value => run = value;
                            var prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(consumed, "집중 축", "황금 박동");
                            core.EnterInfiniteRun(prepared);
                            var bonus = Invoke<GreedLastInfiniteLoadoutBonus>(typeof(GreedLastRunCore), "CalculateInfiniteLoadoutBonus", prepared);
                            Require(run.InfiniteMode && !run.Stopped && !run.SaveEligible
                                && run.Score == bonus.ScoreBonus && run.Score < consumed.Score
                                && string.IsNullOrEmpty(consumed.Relic) && prepared.Relic == "황금 박동",
                                "The live infinite run must receive only its existing start bonus, not inherit the normal cumulative score.");
                            backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                            saved = (GreedLastRunRecord[])Field(backend, "saveLoadoutSlots").GetValue(backend);
                            Require(saved[slot].IsValid == (use == 0)
                                && ((int[])Field(backend, "saveLoadoutUsesRemaining").GetValue(backend))[slot] == 1 - use,
                                "The first start retains one use; the second auto-clears only its source slot across reload.");
                            for (int other = 0; other < 3; other++)
                                if (other != slot) Require(!saved[other].IsValid,
                                    "A boundary start must not populate another slot.");
                        }
                        Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _)
                            && !backend.HasSaveLoadoutCandidate && backend.BuildRecordBoardText(0).Contains("[S]", StringComparison.Ordinal),
                            "A third start must fail while the capped normal record and its grade remain available.");
                    }
                    finally
                    {
                        PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.Save();
                    }
                }
            }
            Debug.Log("Greed capped loadout reload passed: six isolated score/slot cases, candidate once, preparation without consumption, two starts with existing bonus, auto-clear/reload and third-start rejection. Constructed boundary records, not a full normal-play or device-performance measurement; personal slots/settings unchanged.");
        }

        private static FieldInfo Field(object instance, string name)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static T Invoke<T>(Type type, string name, params object[] args)
            => (T)type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        private static T InvokeInstance<T>(object instance, string name, params object[] args)
            => (T)instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
