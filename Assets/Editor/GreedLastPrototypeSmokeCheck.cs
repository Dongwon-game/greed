using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeSmokeCheck
    {
        [MenuItem("Greed Last/Run Prototype Smoke Check")]
        public static void Run()
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;

            float now = Time.realtimeSinceStartup;
            core.Enter("prototype smoke", refillRun: false);
            Require(snapshot.ChoiceActive, "Normal run must open with a start gift choice.");

            core.HandleInput(GreedLastRunChannel.Center, now + 0.01f);
            Require(snapshot.StartCountdownActive, "Choosing a start gift must prepare the run countdown.");

            core.Tick(now + 2f);
            core.Tick(now + 3f);
            Require(snapshot.ActivePattern && snapshot.Pattern.IsValid, "The first trap pattern must become active.");

            core.FillFocusForTest();
            Require(snapshot.FocusStateActive, "The focus test command must activate focus state.");
            Require(snapshot.FocusStateActivationCount == 1, "Focus activation must be counted for the result summary.");
            core.Tick(now + 3.25f);
            Require(snapshot.FocusStateActiveSeconds > 0.2f, "Active focus time must accumulate only while the run is moving.");
            int scoreBeforeFocusHit = snapshot.Score;
            core.HandleInput(
                snapshot.Pattern.Channel,
                now + 3f + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success, "Exact channel input at core time must succeed.");
            Require(snapshot.Score > scoreBeforeFocusHit, "Focus success must award score.");
            Require(snapshot.FocusStateSuccessCount == 1, "An exact input during focus state must be counted separately.");
            Require(snapshot.JudgementText.Contains("집중 상태 점수 배율", StringComparison.Ordinal), "Focus success must report its score multiplier.");

            core.RequestNextPattern(now + 5f);
            Require(snapshot.ActivePattern && snapshot.Pattern.IsValid, "A second trap pattern must be startable.");
            core.FillFocusForTest();
            int healthBeforeMiss = snapshot.Health;
            GreedLastRunChannel wrongChannel = snapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            core.HandleInput(wrongChannel, now + 5f + snapshot.Pattern.CoreSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Miss, "Wrong channel must remain a miss during focus state.");
            Require(snapshot.Health == healthBeforeMiss - 1, "Focus state must not prevent miss damage.");
            Require(!snapshot.FocusStateActive, "A miss must end focus state.");
            GreedLastRunAttemptRecord chapterMissAttempt = core.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(chapterMissAttempt.IsValid, "An abandoned started run must create an attempt record.");
            Require(chapterMissAttempt.TimingProfile.Contains("1장 정확 1회", StringComparison.Ordinal), "A chapter-one exact input must remain attached to chapter one.");
            Require(chapterMissAttempt.TimingProfile.Contains("1장 보정 0회", StringComparison.Ordinal), "An untouched chapter-one Good count must remain zero.");
            Require(chapterMissAttempt.TimingProfile.Contains("1장 실수 1회", StringComparison.Ordinal), "A chapter-one miss must remain attached to chapter one in the attempt record.");
            Require(chapterMissAttempt.TimingProfile.Contains("4장 실수 0회", StringComparison.Ordinal), "Untouched later chapters must remain zero in the segment record.");
            Require(
                chapterMissAttempt.TimingProfile.Contains("점프 정확 1회", StringComparison.Ordinal)
                    && chapterMissAttempt.TimingProfile.Contains("숙이기 실수 1회", StringComparison.Ordinal),
                "Normal attempt records must preserve exact jump and failed slide avoidance separately.");
            Require(
                InvokeMockBackendStaticLabel("BuildAvoidanceWeaknessSummary", chapterMissAttempt.TimingProfile)
                    .Contains("상단→↓숙이기 실수 1회", StringComparison.Ordinal),
                "A failed slide must become the run's actionable avoidance review.");
            GreedLastRunAttemptRecord chapterGoodAttempt = BuildChapterGoodAttempt(now + 7f);

            var refillCore = new GreedLastRunCore();
            GreedLastRunSnapshot refillSnapshot = default;
            refillCore.SnapshotChanged += value => refillSnapshot = value;
            float refillNow = now + 8f;
            refillCore.Enter("보충 런 - 저장 조합을 다시 만들기 위한 일반 런입니다.", refillRun: true);
            Require(refillSnapshot.RefillRun, "A refill entry must publish the refill-run flag immediately.");
            Require(refillSnapshot.ChoiceActive, "A refill run must still begin with the normal start-gift choice.");
            Require(
                InvokeRuntimeUiLabel("BuildRunReturnButtonLabel", refillSnapshot, false) == "보충 포기",
                "An active refill run must label the lobby return action as refill abandonment.");
            refillCore.HandleInput(GreedLastRunChannel.Center, refillNow + 0.01f);
            Require(refillSnapshot.StartCountdownActive, "A refill run must buffer its first trap after the start gift.");
            Require(
                InvokeRuntimeUiLabel("BuildStartCountdownContext", refillSnapshot).Contains("보충 런", StringComparison.Ordinal),
                "A refill countdown must preserve why the player is re-entering the pyramid.");

            var cleanBundleCore = new GreedLastRunCore();
            GreedLastRunSnapshot cleanSnapshot = default;
            cleanBundleCore.SnapshotChanged += value => cleanSnapshot = value;
            float cleanNow = now + 10f;
            cleanBundleCore.Enter("clean bundle smoke", refillRun: false);
            cleanBundleCore.HandleInput(GreedLastRunChannel.Center, cleanNow + 0.01f);
            cleanBundleCore.Tick(cleanNow + 2f);
            cleanBundleCore.Tick(cleanNow + 3f);

            float patternStart = cleanNow + 3f;
            GreedLastRunChannel[] expectedLearningChannels =
            {
                GreedLastRunChannel.Left,
                GreedLastRunChannel.Center,
                GreedLastRunChannel.Right,
            };
            GreedLastTimingType[] expectedLearningTimings =
            {
                GreedLastTimingType.Perfect,
                GreedLastTimingType.Clutch,
                GreedLastTimingType.Offbeat,
            };
            string[] expectedPracticeHazards = { "바닥", "상단", "측면" };
            string[] expectedPracticeResponses = { "점프", "숙이기", "비틀기" };
            string[] expectedPracticeSymbols = { "↑", "↓", "↔" };
            GreedLastAvoidanceType[] expectedPracticeAvoidanceTypes =
            {
                GreedLastAvoidanceType.Jump,
                GreedLastAvoidanceType.Slide,
                GreedLastAvoidanceType.Twist,
            };
            for (int i = 0; i < 3; i += 1)
            {
                if (i > 0)
                {
                    patternStart += 2f;
                    cleanBundleCore.RequestNextPattern(patternStart);
                }

                Require(cleanSnapshot.ActivePattern, "Every clean bundle step must start an active trap.");
                Require(cleanSnapshot.Pattern.Channel == expectedLearningChannels[i], "Chapter one must teach left, center, and right in order.");
                Require(cleanSnapshot.Pattern.TimingType == expectedLearningTimings[i], "Chapter one must teach Perfect, Clutch, and Offbeat in order.");
                Require(
                    cleanSnapshot.Pattern.AvoidanceType == expectedPracticeAvoidanceTypes[i],
                    "Chapter one must teach jump, slide, and twist in order.");
                Require(
                    InvokeRuntimeUiLabel("BuildRouteCadencePipState", cleanSnapshot, i) == "active"
                        && InvokeRuntimeUiLabel("BuildRouteCadencePipActionSymbol", cleanSnapshot, i)
                            == (i == 0 ? "↑" : i == 1 ? "↓" : "↔"),
                    "Chapter one must expose the active jump-slide-twist step in the progress rail.");
                cleanBundleCore.HandleInput(
                    cleanSnapshot.Pattern.Channel,
                    patternStart + cleanSnapshot.Pattern.CoreSeconds - cleanBundleCore.InputTimingOffsetSeconds);
                Require(cleanSnapshot.LastResult == GreedLastJudgementResult.Success, "Every clean bundle trap must resolve as success.");
                if (i < 2)
                {
                    string threeActionStatus = InvokeRuntimeUiLabel("BuildThreeActionBundleStatus", cleanSnapshot);
                    string expectedThreeActionStatus = i == 0
                        ? "동작 ↑ 1/3→↓"
                        : "동작 ↑↓ 2/3→↔";
                    Require(
                        threeActionStatus == expectedThreeActionStatus,
                        "The live HUD must show exact jump-slide-twist bundle progress. Actual: " + threeActionStatus);
                }
            }

            Require(cleanSnapshot.ChoiceActive, "Completing a normal chapter must open its route choice.");
            Require(
                cleanSnapshot.PerfectSuccessCount == 1
                    && cleanSnapshot.ClutchSuccessCount == 1
                    && cleanSnapshot.OffbeatSuccessCount == 1,
                "The learning bundle must preserve Perfect, Clutch, and Offbeat successes separately.");
            Require(cleanSnapshot.JudgementText.Contains("패턴 묶음 완벽", StringComparison.Ordinal), "Three clean successes must grant the pattern bundle reward.");
            Require(
                cleanSnapshot.JudgementText.Contains("3동작 정확", StringComparison.Ordinal)
                    && cleanSnapshot.TimingProfile.Contains("3동작 정확 1회", StringComparison.Ordinal),
                "A clean jump-slide-twist learning bundle must record one three-action exact section.");
            Require(cleanSnapshot.ThreeActionExactBundleCount == 1, "The live snapshot must expose the accumulated three-action exact bundle count.");
            Require(
                InvokeRuntimeUiLabel("BuildPatternBundleBannerLabel", cleanSnapshot)
                    == "3 패턴 완벽 · 3동작 정확",
                "The learning bundle banner must celebrate its exact three-action clear.");
            Require(
                InvokeRuntimeUiLabel("BuildRunFlowLine", cleanSnapshot).Contains("3동작 정확", StringComparison.Ordinal),
                "The live flow tag must surface a completed exact three-action bundle.");
            Require(
                InvokeRuntimeUiFloat("BuildPatternBundleCompletionVolume", false, true)
                    > InvokeRuntimeUiFloat("BuildPatternBundleCompletionVolume", false, false),
                "A three-action exact bundle must play a stronger completion sound.");
            Require(
                InvokeRuntimeUiFloat("BuildPatternBundleCompletionHapticDuration", false, false, false, true)
                    > InvokeRuntimeUiFloat("BuildPatternBundleCompletionHapticDuration", false, false, false, false),
                "A three-action exact bundle must extend the completion haptic.");
            Require(
                InvokeRuntimeUiFloat("BuildPatternBundleHistoryPulseDuration", true)
                    > InvokeRuntimeUiFloat("BuildPatternBundleHistoryPulseDuration", false)
                    && InvokeRuntimeUiFloat("BuildPatternBundleFeedbackFlashDuration", true)
                        > InvokeRuntimeUiFloat("BuildPatternBundleFeedbackFlashDuration", false),
                "A three-action exact bundle must keep the judgement-history and flash feedback alive longer.");
            Require(cleanSnapshot.FocusStateActive, "The clean bundle sequence must reach focus state.");
            Require(cleanSnapshot.LastCompletedSegment == "1장", "The first completed bundle must expose chapter one as the latest segment.");
            Require(
                cleanSnapshot.LastCompletedSegmentExactCount == 3
                    && cleanSnapshot.LastCompletedSegmentGoodCount == 0
                    && cleanSnapshot.LastCompletedSegmentMissCount == 0,
                "The chapter transition snapshot must expose a clean 3/0/0 segment summary.");
            Require(cleanSnapshot.TimingProfile.Contains("1장 정확 3회", StringComparison.Ordinal), "A clean learning bundle must report three chapter-one exact judgements.");
            Require(cleanSnapshot.TimingProfile.Contains("1장 보정 0회", StringComparison.Ordinal), "A clean learning bundle must keep chapter-one Good at zero.");
            string cleanNodeRecommendation = GreedLastRuntimeUi.BuildChoiceRecommendationLine(cleanSnapshot);
            Require(cleanNodeRecommendation.Contains("추천: 압박 루트", StringComparison.Ordinal), "A clean 3/0/0 chapter must recommend the higher-pressure node.");
            Require(cleanNodeRecommendation.Contains("완전 정확 고점", StringComparison.Ordinal), "A clean node recommendation must explain its segment-performance reason.");
            Require(cleanNodeRecommendation.Contains("압-엇-압", StringComparison.Ordinal), "A clean node recommendation must preview the pressure cadence it is recommending.");
            float distanceAtChoice = cleanSnapshot.Distance;
            float focusTimeAtChoice = cleanSnapshot.FocusStateRemainingSeconds;
            cleanBundleCore.Tick(cleanNow + 30f);
            Require(Mathf.Approximately(cleanSnapshot.Distance, distanceAtChoice), "Distance must stop while a route choice is open.");
            Require(Mathf.Approximately(cleanSnapshot.FocusStateRemainingSeconds, focusTimeAtChoice), "Focus time must stop while a route choice is open.");
            VerifySegmentDrivenNodeRecommendation(cleanNow + 32f);

            var practiceCore = new GreedLastRunCore();
            GreedLastRunSnapshot practiceSnapshot = default;
            practiceCore.SnapshotChanged += value => practiceSnapshot = value;
            float practiceNow = cleanNow + 40f;
            practiceCore.EnterPractice();
            Require(practiceSnapshot.PracticeMode, "Practice entry must publish an explicit practice mode snapshot.");
            Require(!practiceSnapshot.ChoiceActive, "Practice must skip gifts and route choices.");
            Require(practiceSnapshot.StartCountdownActive, "Practice must provide the same start buffer as a real run.");

            practiceCore.Tick(practiceNow + 2f);
            practiceCore.Tick(practiceNow + 3f);
            float practicePatternStart = practiceNow + 3f;
            Require(
                Mathf.Approximately(InvokeRuntimeUiFloat("BuildStageMotionScale", practiceSnapshot), 0.82f)
                    && Mathf.Approximately(InvokeRuntimeUiFloat("BuildStageDepthScale", practiceSnapshot), 0.88f),
                "Practice must keep a calm motion and shallow depth while teaching the three responses.");
            for (int i = 0; i < 3; i += 1)
            {
                if (i > 0)
                {
                    practicePatternStart += 2f;
                    practiceCore.RequestNextPattern(practicePatternStart);
                }

                Require(practiceSnapshot.ActivePattern, "Every practice step must start an active trap.");
                Require(practiceSnapshot.Pattern.Channel == expectedLearningChannels[i], "Practice must keep the left, center, right learning order.");
                Require(practiceSnapshot.Pattern.TimingType == expectedLearningTimings[i], "Practice must keep the three timing-axis learning order.");
                Require(
                    practiceSnapshot.Pattern.AvoidanceType == expectedPracticeAvoidanceTypes[i],
                    "Practice must keep the jump, slide, twist avoidance learning order in structured pattern data.");
                string practiceStepTitle = InvokeRuntimeUiLabel("BuildPracticeStepTitle", practiceSnapshot);
                string practiceObjective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", practiceSnapshot);
                string practiceProgress = InvokeRuntimeUiLabel(
                    "BuildPracticeProgressLabel",
                    practiceSnapshot,
                    practiceSnapshot.ChapterTarget);
                string practiceLesson = InvokeRuntimeUiLabel("BuildPracticeTimingLesson", practiceSnapshot.Pattern);
                Require(
                    practiceStepTitle.Contains((i + 1) + "/3", StringComparison.Ordinal)
                        && practiceObjective.Contains(BuildPracticeTimingKeyword(expectedLearningTimings[i]), StringComparison.Ordinal)
                        && practiceProgress.Contains("단계 " + (i + 1) + "/3", StringComparison.Ordinal)
                        && practiceStepTitle.Contains(expectedPracticeHazards[i], StringComparison.Ordinal)
                        && practiceStepTitle.Contains(expectedPracticeSymbols[i], StringComparison.Ordinal)
                        && practiceStepTitle.Contains(expectedPracticeResponses[i], StringComparison.Ordinal)
                        && practiceObjective.Contains(expectedPracticeHazards[i], StringComparison.Ordinal)
                        && practiceObjective.Contains(expectedPracticeSymbols[i], StringComparison.Ordinal)
                        && practiceObjective.Contains(expectedPracticeResponses[i], StringComparison.Ordinal)
                        && practiceProgress.Contains(expectedPracticeHazards[i], StringComparison.Ordinal)
                        && practiceProgress.Contains(expectedPracticeSymbols[i], StringComparison.Ordinal)
                        && practiceProgress.Contains(expectedPracticeResponses[i], StringComparison.Ordinal)
                        && practiceLesson.Contains(expectedPracticeHazards[i] + "→" + expectedPracticeSymbols[i] + expectedPracticeResponses[i], StringComparison.Ordinal),
                    "Every practice step must align its title, objective, progress, and action lesson. Step " + (i + 1)
                        + " title=[" + practiceStepTitle + "] objective=[" + practiceObjective.TrimEnd()
                        + "] progress=[" + practiceProgress + "] lesson=[" + practiceLesson + "] pattern=["
                        + practiceSnapshot.Pattern.PatternId + "].");
                Require(
                    InvokeRuntimeUiLabel("BuildRouteCadencePipState", practiceSnapshot, i) == "active"
                        && (i == 0 || InvokeRuntimeUiLabel("BuildRouteCadencePipState", practiceSnapshot, i - 1) == "done"),
                    "Practice cadence pips must mark prior lessons complete and the current lesson active.");
                RequireTextLayoutBudget(practiceObjective.TrimEnd(), 1, 32, "Practice step objective");
                if (i == 0)
                {
                    practiceCore.HandleInput(
                        practiceSnapshot.Pattern.Channel,
                        practicePatternStart + practiceSnapshot.Pattern.CoreSeconds - practiceCore.InputTimingOffsetSeconds);
                    Require(practiceSnapshot.LastResult == GreedLastJudgementResult.Success, "Practice exact input must resolve as success.");
                }
                else if (i == 1)
                {
                    float goodDelta = (practiceSnapshot.Pattern.SuccessWindowSeconds + practiceSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
                    practiceCore.HandleInput(
                        practiceSnapshot.Pattern.Channel,
                        practicePatternStart + practiceSnapshot.Pattern.CoreSeconds + goodDelta - practiceCore.InputTimingOffsetSeconds);
                    Require(practiceSnapshot.LastResult == GreedLastJudgementResult.Good, "Practice must demonstrate the survival-only Good band.");
                }
                else
                {
                    GreedLastRunChannel practiceWrongChannel = practiceSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                        ? GreedLastRunChannel.Right
                        : GreedLastRunChannel.Left;
                    practiceCore.HandleInput(
                        practiceWrongChannel,
                        practicePatternStart + practiceSnapshot.Pattern.CoreSeconds - practiceCore.InputTimingOffsetSeconds);
                    Require(practiceSnapshot.LastResult == GreedLastJudgementResult.Miss, "Practice must demonstrate channel-error Miss handling.");
                }
            }

            Require(practiceSnapshot.Stopped, "Practice must stop after its three learning traps.");
            Require(practiceSnapshot.Phase == GreedLastRunPhase.ClearResolving, "Surviving practice must reach its completion state.");
            Require(!practiceSnapshot.SaveEligible, "Practice completion must never create a save candidate.");
            Require(practiceSnapshot.SuccessCount == 1 && practiceSnapshot.GoodCount == 1 && practiceSnapshot.MissCount == 1, "Practice result counts must preserve Success, Good, and Miss separately.");
            Require(string.IsNullOrEmpty(practiceSnapshot.LastCompletedSegment), "Practice completion must not masquerade as a normal chapter transition.");
            Require(practiceSnapshot.TimingProfile.Contains("1장 정확 0회", StringComparison.Ordinal), "Practice judgements must not pollute normal chapter analytics.");
            Require(practiceSnapshot.TimingProfile.Contains("1장 보정 0회", StringComparison.Ordinal), "Practice Good judgements must stay outside normal chapter analytics.");
            Require(practiceSnapshot.TimingProfile.Contains("1장 실수 0회", StringComparison.Ordinal), "Practice misses must stay outside normal chapter analytics.");
            Require(
                practiceSnapshot.JudgementText.Contains("복기 우선: 실수 1회", StringComparison.Ordinal)
                    && practiceSnapshot.JudgementText.Contains("같은 채널과 입력 순간 재확인", StringComparison.Ordinal)
                    && practiceSnapshot.JudgementText.Contains("회피 복기: 측면→↔비틀기 실패 / 3단계 다시", StringComparison.Ordinal),
                "A mixed practice result must prioritize its miss review.");
            Require(
                practiceSnapshot.TimingRecommendationSampleCount == 2
                    && practiceSnapshot.JudgementText.Contains("입력 보정 샘플 2/3", StringComparison.Ordinal),
                "A mixed practice run must explain that its wrong-channel miss did not complete calibration sampling.");
            Require(!practiceSnapshot.JudgementText.Contains("패턴 묶음 완벽", StringComparison.Ordinal), "Practice completion must not grant a normal-run bundle reward.");
            Require(
                InvokeRuntimeUiLabel("BuildPracticeRestartButtonLabel", practiceSnapshot) == "실수 구간 다시"
                    && InvokeRuntimeUiLabel("BuildPracticeCompletionProgressLabel", practiceSnapshot) == "연습 완료 · 실수 복기",
                "A missed practice run must offer a miss-focused restart action.");
            practiceCore.RestartPractice(practiceNow + 20f);
            Require(practiceSnapshot.PracticeMode && practiceSnapshot.StartCountdownActive, "Practice restart must return to its buffered opening.");
            Require(practiceSnapshot.SuccessCount == 0 && practiceSnapshot.GoodCount == 0 && practiceSnapshot.MissCount == 0, "Practice restart must clear the previous result counts.");
            VerifyPracticeReviewVariants(practiceNow + 30f);
            practiceCore.Enter("normal after practice", refillRun: false);
            Require(!practiceSnapshot.PracticeMode, "A normal run entered after practice must clear practice mode.");
            Require(practiceSnapshot.ChoiceActive, "A normal run entered after practice must restore its start gift choice.");
            VerifyInputBoundarySafety(practiceNow + 60f);
            VerifyStoredGiftStackScaling();
            VerifyStoredGiftStacksRestoreIntoRun();
            VerifyInfiniteRoutePayoffRestoration();
            VerifyInfinitePreparationIsRunLocal();

            var fullRunCore = new GreedLastRunCore();
            GreedLastRunSnapshot fullRunSnapshot = default;
            fullRunCore.SnapshotChanged += value => fullRunSnapshot = value;
            float fullRunClock = practiceNow + 100f;
            fullRunCore.Enter("full run smoke", refillRun: false);
            Require(fullRunSnapshot.ChoiceKind == GreedLastChoiceKind.StartGift, "A full normal run must start at its gift choice.");
            fullRunCore.HandleInput(GreedLastRunChannel.Center, fullRunClock);
            float[] expectedChapterMotion = { 0.86f, 1f, 1.16f, 1.08f };
            float[] expectedChapterDepth = { 0.84f, 0.98f, 1.12f, 1.20f };

            for (int chapter = 1; chapter <= 4; chapter += 1)
            {
                StartBufferedPattern(fullRunCore, ref fullRunSnapshot, ref fullRunClock);
                Require(
                    Mathf.Approximately(InvokeRuntimeUiFloat("BuildStageMotionScale", fullRunSnapshot), expectedChapterMotion[chapter - 1])
                        && Mathf.Approximately(InvokeRuntimeUiFloat("BuildStageDepthScale", fullRunSnapshot), expectedChapterDepth[chapter - 1]),
                    "Every normal chapter must preserve its authored motion-versus-depth role.");
                string chapterProgressLabel = InvokeRuntimeUiLabel(
                    "BuildNormalProgressLabel",
                    fullRunSnapshot,
                    fullRunSnapshot.ChapterTarget);
                Require(
                    chapterProgressLabel.Contains(BuildExpectedChapterRole(chapter), StringComparison.Ordinal),
                    "Every normal chapter progress bar must identify its structural role.");
                RequireTextLayoutBudget(chapterProgressLabel, 1, 42, "Chapter role progress label");
                if (chapter == 4)
                {
                    string coreCadenceCue = InvokeRuntimeUiLabel("BuildRouteCadenceCue", fullRunSnapshot);
                    Require(
                        coreCadenceCue.Contains("공명핵 박자 · 안정", StringComparison.Ordinal)
                            && coreCadenceCue.Contains("[압]", StringComparison.Ordinal),
                        "Chapter four must introduce its fixed final cadence before core retrieval.");
                    Require(
                        GreedLastRuntimeUi.BuildLiveRunCoachLine(fullRunSnapshot).Contains("공명핵 검증", StringComparison.Ordinal),
                        "Chapter four coach must frame its traps as learned-skill validation.");
                }

                ResolveExactBundle(fullRunCore, ref fullRunSnapshot, ref fullRunClock, "normal chapter " + chapter);
                Require(fullRunSnapshot.LastCompletedSegment == chapter + "장", "Every normal chapter must publish its completed-segment identity.");
                Require(
                    fullRunSnapshot.LastCompletedSegmentExactCount == 3
                        && fullRunSnapshot.LastCompletedSegmentGoodCount == 0
                        && fullRunSnapshot.LastCompletedSegmentMissCount == 0,
                    "Every clean normal chapter must publish a 3/0/0 transition summary.");
                if (chapter <= 3)
                {
                    Require(fullRunSnapshot.ChoiceKind == GreedLastChoiceKind.Node, "Every direction-building chapter must open a node choice.");
                    fullRunCore.HandleInput(GreedLastRunChannel.Left, fullRunClock += 0.2f);
                    Require(fullRunSnapshot.ChoiceKind == GreedLastChoiceKind.Gift, "A node choice must lead to its chapter gift.");
                    fullRunCore.HandleInput(GreedLastRunChannel.Center, fullRunClock += 0.2f);
                    Require(fullRunSnapshot.SurvivalGiftStacks == chapter, "Repeated chapter gifts must accumulate instead of replacing the previous gift.");
                    if (chapter == 2)
                    {
                        Require(fullRunSnapshot.ChoiceKind == GreedLastChoiceKind.Relic, "Chapter two gift must lead to the mid-run relic choice.");
                        fullRunCore.HandleInput(GreedLastRunChannel.Right, fullRunClock += 0.2f);
                        Require(fullRunSnapshot.ActiveRelic == "파라오의 각인", "The selected relic must remain active for the rest of the normal run.");
                    }

                    Require(fullRunSnapshot.StartCountdownActive, "Completing a chapter choice sequence must buffer the next chapter.");
                    Require(fullRunSnapshot.ChapterIndex == chapter + 1, "Completing a chapter choice sequence must advance exactly one chapter.");
                }
            }

            Require(fullRunSnapshot.CoreRetrieved, "Finishing chapter four must retrieve the resonance core.");
            Require(fullRunSnapshot.Phase == GreedLastRunPhase.EscapeRunning, "Core retrieval must lead into the escape verification phase.");
            Require(fullRunSnapshot.StartCountdownActive, "Escape verification must receive a fresh start buffer.");
            StartBufferedPattern(fullRunCore, ref fullRunSnapshot, ref fullRunClock);
            Require(
                Mathf.Approximately(InvokeRuntimeUiFloat("BuildStageMotionScale", fullRunSnapshot), 1.30f)
                    && Mathf.Approximately(InvokeRuntimeUiFloat("BuildStageDepthScale", fullRunSnapshot), 1.28f),
                "Escape must increase forward motion after the deeper but calmer chapter-four validation.");
            ResolveExactBundle(fullRunCore, ref fullRunSnapshot, ref fullRunClock, "escape verification");
            Require(fullRunSnapshot.Stopped, "Completing escape verification must stop the normal run.");
            Require(fullRunSnapshot.EscapeSucceeded, "Completing escape verification must mark escape success.");
            Require(fullRunSnapshot.LastCompletedSegment == "탈출", "A completed escape must replace the latest segment identity with escape.");
            Require(fullRunSnapshot.LastCompletedSegmentExactCount == 3, "A clean escape must publish three exact judgements in its transition summary.");
            Require(fullRunSnapshot.SaveEligible && fullRunCore.CanExitAfterClear, "A full clear must expose exactly one save opportunity.");
            Require(
                InvokeRuntimeUiLabel("BuildRunReturnButtonLabel", fullRunSnapshot, true) == "저장 없이 로비",
                "A stopped normal clear must label the lobby button as skipping the one save opportunity.");
            Require(fullRunSnapshot.ThreeActionExactBundleCount > 0, "A clean full clear must preserve at least one three-action exact section.");
            string threeActionResultTag = InvokeRuntimeUiLabel("BuildThreeActionExactHudSuffix", fullRunSnapshot);
            Require(
                threeActionResultTag.Contains("3동작 " + fullRunSnapshot.ThreeActionExactBundleCount, StringComparison.Ordinal),
                "The stopped normal result HUD must expose the accumulated three-action exact count.");
            string threeActionResultLine = InvokeRuntimeUiLabel("BuildThreeActionExactHudResultLine", fullRunSnapshot);
            Require(
                threeActionResultLine.Contains("3동작 " + fullRunSnapshot.ThreeActionExactBundleCount, StringComparison.Ordinal)
                    && threeActionResultLine.EndsWith("\n", StringComparison.Ordinal),
                "The reusable stopped-result line must preserve the three-action exact count for infinite settlement HUDs.");
            string stoppedAvoidanceLine = InvokeRuntimeUiLabel("BuildStoppedAvoidanceHudLine", fullRunSnapshot);
            Require(
                stoppedAvoidanceLine.Contains("회피 점프", StringComparison.Ordinal)
                    && stoppedAvoidanceLine.Contains("숙", StringComparison.Ordinal)
                    && stoppedAvoidanceLine.Contains("틀", StringComparison.Ordinal)
                    && stoppedAvoidanceLine.Contains("(정/보/실)", StringComparison.Ordinal),
                "The stopped normal result HUD must expose compact jump-slide-twist performance.");
            Require(
                InvokeRuntimeUiLabel("BuildStoppedAvoidanceReviewHudLine", fullRunSnapshot) == string.Empty,
                "A clean exact clear must not add an unnecessary avoidance review line.");
            GreedLastRunRecord fullRunRecord = fullRunCore.CreateRunRecord();
            Require(fullRunRecord.IsValid, "A full clear must create a valid normal-run record.");
            Require(fullRunSnapshot.SurvivalGiftStacks == 3, "A full run must preserve all three chapter gift stacks in its result snapshot.");
            Require(
                fullRunSnapshot.PerfectSuccessCount + fullRunSnapshot.ClutchSuccessCount + fullRunSnapshot.OffbeatSuccessCount
                    == fullRunSnapshot.SuccessCount,
                "Every exact success must belong to one of the three timing axes.");
            Require(fullRunRecord.Relic == "파라오의 각인", "The normal result record must retain the relic used during that run.");
            Require(fullRunRecord.ChoiceSummary.Contains(fullRunRecord.Relic, StringComparison.Ordinal), "The normal result history must show its relic choice.");
            Require(fullRunRecord.TimingProfile.Contains("집중 발동", StringComparison.Ordinal), "The normal result must preserve focus activation performance.");
            Require(fullRunRecord.TimingProfile.Contains("집중 성공", StringComparison.Ordinal), "The normal result must preserve focused exact successes.");
            Require(fullRunRecord.TimingProfile.Contains("정박 성공", StringComparison.Ordinal), "The normal result must preserve Perfect-axis successes.");
            Require(fullRunRecord.TimingProfile.Contains("압박 성공", StringComparison.Ordinal), "The normal result must preserve Clutch-axis successes.");
            Require(fullRunRecord.TimingProfile.Contains("엇박 성공", StringComparison.Ordinal), "The normal result must preserve Offbeat-axis successes.");
            Require(!fullRunRecord.TimingProfile.Contains("정박 충전 0회", StringComparison.Ordinal), "A full exact clear must preserve at least one Perfect focus charge.");
            Require(!fullRunRecord.TimingProfile.Contains("압박 반격 0회", StringComparison.Ordinal), "A full exact clear must preserve at least one Clutch counter.");
            Require(!fullRunRecord.TimingProfile.Contains("엇박 연쇄 0회", StringComparison.Ordinal), "A full exact clear must preserve at least one Offbeat chain payoff.");
            Require(fullRunRecord.TimingProfile.Contains("1장 정확 3회", StringComparison.Ordinal), "A clean clear must preserve chapter-one exact performance.");
            Require(fullRunRecord.TimingProfile.Contains("4장 정확 3회", StringComparison.Ordinal), "A clean clear must preserve chapter-four exact performance.");
            Require(fullRunRecord.TimingProfile.Contains("탈출 정확 3회", StringComparison.Ordinal), "A clean clear must preserve escape exact performance.");
            Require(fullRunRecord.TimingProfile.Contains("탈출 실수 0회", StringComparison.Ordinal), "A clean escape must preserve a zero-miss escape segment.");
            Require(
                !fullRunRecord.TimingProfile.Contains("3동작 정확 0회", StringComparison.Ordinal),
                "A full clean run must preserve at least one exact three-action section.");
            string fullRunAvoidanceSummary = InvokeMockBackendStaticLabel(
                "BuildAvoidancePerformanceSummary",
                fullRunRecord.TimingProfile);
            Require(
                fullRunAvoidanceSummary.Contains("점프", StringComparison.Ordinal)
                    && fullRunAvoidanceSummary.Contains("숙이기", StringComparison.Ordinal)
                    && fullRunAvoidanceSummary.Contains("비틀기", StringComparison.Ordinal)
                    && fullRunAvoidanceSummary.Contains("(정/보/실)", StringComparison.Ordinal),
                "A full clear must expose compact mastery for all three avoidance actions.");
            Require(
                string.IsNullOrEmpty(InvokeMockBackendStaticLabel("BuildAvoidanceWeaknessSummary", fullRunRecord.TimingProfile)),
                "A full exact clear must not invent an avoidance weakness.");
            VerifySingleUseSaveCandidateAndTwoRunLifetime(fullRunRecord, chapterMissAttempt, chapterGoodAttempt);
            VerifyLiveRunCoachLines(now + 720f);
            VerifyRecoveryPrecisionPayoff(now + 780f);
            VerifyClutchCounterPayoff(now + 840f);
            VerifyRoutePayoffContracts(now + 900f);
            VerifyRunAtmosphereContracts(now + 960f);
            VerifyLobbyResultActionLabels();
            VerifyDevToolButtonLabels(now + 990f);
            VerifyDevSectionShortcutTargets(now + 995f);
            VerifyRetryOpeningChoiceRecommendations();
            VerifyReviewTargetFeedback(now + 820f);
            VerifyTrapResponseContracts();

            Debug.Log("Greed Last prototype smoke check passed: focus performance, three timing axes, deterministic three-step practice onboarding, result-specific practice review and calibration, floor/overhead/side trap response contracts, exact/Good/Miss avoidance feedback, segment-driven choices, stable/variation/pressure route payoff contracts, route payoff haptic contracts, route cadence rail and break coaching, chapter-role progress labels, chapter-three variation peak, conservative final-validation tempo, final escape cadence guidance, live and terminal infinite-section settlement, live coaching HUD, prototype feel payoff guide, route-action choice hints, beat-count timing guide, hazard-bound active score-preview guide, active action cue visibility, active action cue hidden states, non-action screen cue suppression, dev shortcut labels, section shortcut labels, section shortcut targets, review shortcut labels, route shortcut maps, profile record-board shortcut, board page button labels, lane key hints, mobile touch lane hints, miss key feedback, sensory settings checklist, pause settings line and backdrop input block, live reward-distance coaching, recovery-pattern precision payoff and Good fallback, Perfect focus charge, Clutch counter, Offbeat chain payoffs, transition payoff preservation, timing-axis profile mastery, combo badge reward cue, three-action exact feedback amplification, stage-transition objective cue, transition progress labels, lobby next-action targets, performance-based result next actions, persistent and latched retry targets, achievement/failure feedback, advanced target ladders, target test shortcuts, focus/threat/section/miss-cap/input-omission/timing-apply goals, pause resume coaching, result review lines and actions, retry opening recommendations, review practice drills, infinite review preparation, compact, comparison-delta-free, and strongly sanitized record boards, profile checklist and save-confirm/skip/reload verification order, auto-clear refill labels, auto-clear reload persistence, fractional save-use labels, input boundaries, practice lifecycle, accumulated gifts, relic-only normal results, legacy migration, segment-weighted save recommendations, run-local infinite preparation, restored infinite passives, full normal clear, two-use auto-clear, and infinite handoff verified.");
        }

        private static void VerifyTrapResponseContracts()
        {
            VerifyTrapResponseFamily(
                "점프",
                "바닥→↑점프",
                "↑",
                0.62f,
                0f,
                90f,
                new[] { "floor", "sand", "falsebeat", "crack", "threshold", "weight", "mirror", "echo", "scarab", "vortex" });
            VerifyTrapResponseFamily(
                "숙이기",
                "상단→↓숙이기",
                "↓",
                0.88f,
                0f,
                0f,
                new[] { "drop", "pillar", "beam", "gate", "obelisk", "guard", "drum", "pulse", "chain" });
            VerifyTrapResponseFamily(
                "비틀기",
                "측면→↔비틀기",
                "↔",
                0.72f,
                -0.12f,
                -38f,
                new[] { "blade", "wall", "spear", "hidden" });

            GreedLastPatternModel structuredOverride = new GreedLastPatternModel(
                "left_floor_perfect",
                GreedLastRunChannel.Left,
                GreedLastTimingType.Perfect,
                "structured override",
                0.4f,
                1f,
                0.1f,
                0.3f,
                1.4f,
                GreedLastAvoidanceType.Twist);
            Require(
                InvokeRuntimeUiLabel("BuildRunnerResponseLabel", structuredOverride) == "비틀기",
                "Structured avoidance data must take priority over legacy pattern-id inference.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAvoidancePerformanceSummary",
                    "점프 정확 3회 / 점프 보정 1회 / 점프 실수 0회"
                        + " / 숙이기 정확 2회 / 숙이기 보정 0회 / 숙이기 실수 1회"
                        + " / 비틀기 정확 4회 / 비틀기 보정 2회 / 비틀기 실수 0회")
                    == "점프 3/1/0 · 숙이기 2/0/1 · 비틀기 4/2/0 (정/보/실)",
                "Avoidance result summaries must preserve exact, Good, and miss order without cluttering record lists.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAvoidanceWeaknessSummary",
                    "점프 보정 2회 / 점프 실수 0회 / 숙이기 보정 1회 / 숙이기 실수 1회"
                        + " / 비틀기 보정 0회 / 비틀기 실수 0회")
                    == "상단→↓숙이기 실수 1회·보정 1회 / 기초연습 숙이기 단계",
                "Avoidance review must prioritize misses over a larger Good-only count.");
        }

        private static void VerifyTrapResponseFamily(
            string expectedResponse,
            string expectedCue,
            string expectedSymbol,
            float expectedStartY,
            float expectedSweepX,
            float expectedStrikeAngle,
            string[] patternFamilies)
        {
            for (int i = 0; i < patternFamilies.Length; i += 1)
            {
                GreedLastPatternModel pattern = new GreedLastPatternModel(
                    "left_" + patternFamilies[i] + "_perfect",
                    GreedLastRunChannel.Left,
                    GreedLastTimingType.Perfect,
                    patternFamilies[i],
                    0.4f,
                    1f,
                    0.1f,
                    0.3f,
                    1.4f);
                Require(
                    InvokeRuntimeUiLabel("BuildRunnerResponseLabel", pattern) == expectedResponse,
                    patternFamilies[i] + " must resolve to the authored " + expectedResponse + " response.");
                Require(
                    InvokeRuntimeUiLabel("BuildTrapResponseCueLabel", pattern) == expectedCue,
                    patternFamilies[i] + " must expose its hazard approach and response together.");
                Require(
                    InvokeRuntimeUiLabel("BuildRunnerResponseSymbol", pattern) == expectedSymbol,
                    patternFamilies[i] + " must keep its authored action symbol in judgement history.");
                Require(
                    InvokeRuntimeUiLabel("BuildActiveAvoidanceCoach", pattern) == expectedCue + " 회피",
                    patternFamilies[i] + " must expose its hazard-bound response in the live coach line.");
                Require(
                    InvokeRuntimeUiLabel("BuildRunnerOutcomeLabel", pattern, GreedLastJudgementResult.Success)
                        == expectedCue + " 회피",
                    patternFamilies[i] + " exact response must read as a hazard-bound clean avoidance.");
                Require(
                    InvokeRuntimeUiLabel("BuildRunnerOutcomeLabel", pattern, GreedLastJudgementResult.Good)
                        == expectedCue + " 보정",
                    patternFamilies[i] + " Good response must read as hazard-bound corrected survival.");
                Require(
                    InvokeRuntimeUiLabel("BuildRunnerOutcomeLabel", pattern, GreedLastJudgementResult.Miss)
                        == expectedCue + " 실패",
                    patternFamilies[i] + " miss response must name the failed hazard and action.");
                Require(
                    Mathf.Approximately(InvokeRuntimeUiFloat("BuildTrapApproachStartY", pattern), expectedStartY),
                    patternFamilies[i] + " must enter from its authored hazard height.");
                Require(
                    Mathf.Approximately(InvokeRuntimeUiFloat("BuildTrapApproachSweepX", pattern, 0f), expectedSweepX)
                        && Mathf.Approximately(InvokeRuntimeUiFloat("BuildTrapApproachSweepX", pattern, 1f), 0f),
                    patternFamilies[i] + " must converge onto the judgement lane without changing core timing.");
                Require(
                    Mathf.Approximately(InvokeRuntimeUiFloat("BuildTrapStrikeAngle", pattern), expectedStrikeAngle),
                    patternFamilies[i] + " must expose its authored strike direction.");
            }
        }

        private static void VerifyInputBoundarySafety(float now)
        {
            var pausedCore = new GreedLastRunCore();
            GreedLastRunSnapshot pausedSnapshot = default;
            pausedCore.SnapshotChanged += value => pausedSnapshot = value;
            pausedCore.Enter("복기 재도전\n재도전 목표: 1장 무실수 / 현재 1", refillRun: false);
            pausedCore.HandleInput(GreedLastRunChannel.Center, now);
            pausedCore.HandleInput(GreedLastRunChannel.Left, now + 0.1f);
            Require(pausedSnapshot.IgnoreReason == "start_countdown", "Input during the start countdown must be ignored explicitly.");
            Require(pausedSnapshot.LastResult == GreedLastJudgementResult.None, "Ignored countdown input must not publish a stale judgement.");
            Require(pausedSnapshot.SuccessCount == 0 && pausedSnapshot.GoodCount == 0 && pausedSnapshot.MissCount == 0, "Countdown input must not alter run totals.");
            Require(pausedSnapshot.TimingProfile.Contains("1장 정확 0회", StringComparison.Ordinal), "Countdown input must not alter chapter analytics.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", pausedSnapshot),
                "The active action cue must stay hidden during the start countdown.");

            pausedCore.Tick(now + 2f);
            pausedCore.Tick(now + 3f);
            Require(pausedSnapshot.ActivePattern, "The pause boundary test must expose an active trap.");
            Require(pausedSnapshot.AutoFlow, "The pause boundary test must begin from auto-flow so resume restoration is observable.");
            pausedCore.TogglePause(now + 3.1f);
            Require(pausedSnapshot.Paused, "The pause boundary test must enter pause on an active trap.");
            Require(!pausedSnapshot.AutoFlow, "Pause must suspend auto-flow while keeping its resume intent latched.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", pausedSnapshot),
                "The active action cue must stay hidden behind the pause menu.");
            string pauseCoach = GreedLastRuntimeUi.BuildPauseCoachLine(pausedSnapshot);
            Require(pauseCoach.Contains("재개 목표:", StringComparison.Ordinal), "A paused active trap must expose a resume target line.");
            Require(pauseCoach.Contains("바닥→↑점프 회피", StringComparison.Ordinal), "A paused active trap must preserve the required hazard-bound avoidance action.");
            Require(pauseCoach.Contains("정박 타이밍", StringComparison.Ordinal), "A paused active trap must preserve the live timing coach.");
            Require(pauseCoach.Contains("목표 유지", StringComparison.Ordinal), "A paused review retry must keep its target status visible in the pause coach.");
            string pauseHeader = InvokeRuntimeUiLabel("BuildRunPauseHeaderDetail", pausedSnapshot);
            Require(
                pauseHeader.Contains("카운트 뒤", StringComparison.Ordinal)
                    && pauseHeader.Contains("중단 기록", StringComparison.Ordinal),
                "The pause header must explain both counted resume and lobby-exit consequence.");
            string pauseSettings = InvokeRuntimeUiLabel("BuildPauseSettingsLineText", pausedSnapshot, 0.6f, false);
            Require(
                pauseSettings.Contains("설정: 소리 60%", StringComparison.Ordinal)
                    && pauseSettings.Contains("보정", StringComparison.Ordinal)
                    && pauseSettings.Contains("추천", StringComparison.Ordinal)
                    && pauseSettings.Contains("진동 OFF", StringComparison.Ordinal),
                "The pause settings line must preserve volume, calibration, recommendation, and haptic states.");
            Require(
                RuntimePauseBackdropBlocksInput(),
                "The pause menu backdrop must block touches from reaching the run lanes underneath.");
            pausedCore.HandleInput(pausedSnapshot.Pattern.Channel, now + 3.2f);
            Require(pausedSnapshot.IgnoreReason == "paused", "Input while paused must report the pause boundary before any countdown state.");
            Require(pausedSnapshot.LastResult == GreedLastJudgementResult.None, "Paused input must not replay a stale judgement.");
            Require(pausedSnapshot.ActivePattern, "Paused input must preserve the active trap for resume.");
            Require(pausedSnapshot.SuccessCount == 0 && pausedSnapshot.GoodCount == 0 && pausedSnapshot.MissCount == 0, "Paused input must not alter run totals.");
            pausedCore.TogglePause(now + 3.4f);
            Require(pausedSnapshot.ResumeCountdownActive, "Tapping resume must enter a countdown instead of instantly unpausing.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", pausedSnapshot),
                "The active action cue must stay hidden during the resume countdown.");
            Require(
                GreedLastRuntimeUi.BuildPauseCoachLine(pausedSnapshot).Contains("첫 함정 기준선", StringComparison.Ordinal),
                "A resume countdown must tell the player to read the first trap baseline again.");
            string resumeSettings = InvokeRuntimeUiLabel("BuildPauseSettingsLineText", pausedSnapshot, 0.8f, true);
            Require(
                resumeSettings.Contains("설정: 소리 80%", StringComparison.Ordinal)
                    && resumeSettings.Contains("재개", StringComparison.Ordinal)
                    && resumeSettings.Contains("진동 ON", StringComparison.Ordinal),
                "The resume settings line must keep sound, countdown, and haptic states visible.");
            Require(
                InvokeRuntimeUiLabel("BuildResumeCountdownFeedbackText", pausedSnapshot) == "2",
                "The resume countdown must begin with the rhythm-game 2 count.");
            pausedCore.Tick(now + 4.15f);
            Require(
                pausedSnapshot.ResumeCountdownActive
                    && InvokeRuntimeUiLabel("BuildResumeCountdownFeedbackText", pausedSnapshot) == "1",
                "The resume countdown must expose the 1 count before play resumes.");
            pausedCore.Tick(now + 4.65f);
            Require(
                pausedSnapshot.ResumeCountdownActive
                    && InvokeRuntimeUiLabel("BuildResumeCountdownFeedbackText", pausedSnapshot) == "GO",
                "The resume countdown must expose GO before releasing control.");
            pausedCore.Tick(now + 5.0f);
            Require(!pausedSnapshot.Paused && !pausedSnapshot.ResumeCountdownActive, "The resume countdown must release pause state.");
            Require(pausedSnapshot.AutoFlow, "Resume after a paused auto-flow run must restore automatic progression.");
            Require(pausedSnapshot.JudgementText.Contains("앞 박자 여유", StringComparison.Ordinal), "Resuming must report the pre-beat buffer.");
            Require(pausedSnapshot.ActivePattern, "The active trap must survive resume buffering for the player to re-read it.");

            var staggerCore = new GreedLastRunCore();
            GreedLastRunSnapshot staggerSnapshot = default;
            staggerCore.SnapshotChanged += value => staggerSnapshot = value;
            float staggerClock = now + 20f;
            staggerCore.Enter("stagger boundary smoke", refillRun: false);
            staggerCore.HandleInput(GreedLastRunChannel.Center, staggerClock);
            staggerCore.Tick(staggerClock + 2f);
            staggerCore.Tick(staggerClock + 3f);
            Require(staggerSnapshot.ActivePattern, "The stagger boundary test must expose an active trap.");
            GreedLastRunChannel wrongChannel = staggerSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            float realtimeMiss = Time.realtimeSinceStartup;
            staggerCore.HandleInput(wrongChannel, realtimeMiss);
            Require(staggerSnapshot.LastResult == GreedLastJudgementResult.Miss && staggerSnapshot.MissCount == 1, "The stagger boundary test must begin with one real miss.");
            string retainedMissReason = staggerSnapshot.MissReason;
            staggerCore.HandleInput(staggerSnapshot.Pattern.Channel, realtimeMiss + 0.1f);
            Require(staggerSnapshot.IgnoreReason == "locked_by_stagger", "Input during miss stagger must be ignored.");
            Require(staggerSnapshot.LastResult == GreedLastJudgementResult.None, "Ignored stagger input must clear stale visual judgement state.");
            Require(staggerSnapshot.MissCount == 1, "Stagger input must not add another miss.");
            Require(staggerSnapshot.MissReason == retainedMissReason, "Ignored stagger input must preserve the real miss reason for result coaching.");
            Require(staggerSnapshot.TimingProfile.Contains("1장 실수 1회", StringComparison.Ordinal), "Stagger input must not duplicate chapter misses.");

            var repeatCore = new GreedLastRunCore();
            GreedLastRunSnapshot repeatSnapshot = default;
            repeatCore.SnapshotChanged += value => repeatSnapshot = value;
            float repeatClock = now + 40f;
            repeatCore.Enter("repeat boundary smoke", refillRun: false);
            repeatCore.HandleInput(GreedLastRunChannel.Center, repeatClock);
            repeatCore.Tick(repeatClock + 2f);
            repeatCore.Tick(repeatClock + 3f);
            Require(repeatSnapshot.ActivePattern, "The repeat boundary test must expose an active trap.");
            float exactAt = repeatClock + 3f + repeatSnapshot.Pattern.CoreSeconds - repeatCore.InputTimingOffsetSeconds;
            repeatCore.HandleInput(repeatSnapshot.Pattern.Channel, exactAt);
            Require(repeatSnapshot.LastResult == GreedLastJudgementResult.Success && repeatSnapshot.SuccessCount == 1, "The repeat boundary test must resolve one exact input.");
            repeatCore.HandleInput(repeatSnapshot.Pattern.Channel, exactAt + 0.01f);
            Require(repeatSnapshot.IgnoreReason == "no_active_pattern", "A repeated tap after resolution must not target a finished trap.");
            Require(repeatSnapshot.LastResult == GreedLastJudgementResult.None, "A repeated tap must clear stale visual judgement state.");
            Require(repeatSnapshot.SuccessCount == 1 && repeatSnapshot.GoodCount == 0 && repeatSnapshot.MissCount == 0, "A repeated tap must not duplicate any judgement total.");
            Require(repeatSnapshot.TimingProfile.Contains("1장 정확 1회", StringComparison.Ordinal), "A repeated tap must not duplicate chapter exact counts.");
        }

        private static string BuildExpectedChapterRole(int chapterIndex)
        {
            switch (chapterIndex)
            {
                case 1:
                    return "학습";
                case 2:
                    return "방향 형성";
                case 3:
                    return "변주 정점";
                default:
                    return "공명핵 검증";
            }
        }

        private static string BuildPracticeTimingKeyword(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Perfect:
                    return "정박";
                case GreedLastTimingType.Clutch:
                    return "클러치";
                default:
                    return "엇박";
            }
        }

        private static void VerifyPracticeReviewVariants(float now)
        {
            GreedLastRunSnapshot goodSnapshot = RunPracticeReviewSample(
                now,
                new[]
                {
                    GreedLastJudgementResult.Success,
                    GreedLastJudgementResult.Good,
                    GreedLastJudgementResult.Success,
                });
            Require(
                goodSnapshot.SuccessCount == 2 && goodSnapshot.GoodCount == 1 && goodSnapshot.MissCount == 0,
                "The Good-focused practice sample must retain its 2/1/0 result.");
            Require(
                goodSnapshot.JudgementText.Contains("복기 우선: 보정 1회", StringComparison.Ordinal)
                    && goodSnapshot.JudgementText.Contains("판정선 중앙 재확인", StringComparison.Ordinal)
                    && goodSnapshot.JudgementText.Contains("회피 복기: 상단→↓숙이기 보정 / 2단계 다시", StringComparison.Ordinal),
                "A Good-only practice run must prioritize exact-window review.");
            Require(goodSnapshot.HasTimingRecommendation, "Three valid practice timings must produce a calibration recommendation.");
            bool goodRecommendationActionable = InvokeRuntimeUiBool("CanUseTimingRecommendationButton", goodSnapshot);
            Require(
                goodSnapshot.JudgementText.Contains(
                    goodRecommendationActionable ? "입력 보정 제안:" : "입력 보정: 현재 설정 안정",
                    StringComparison.Ordinal),
                "A Good-only practice result must explain whether its calibration can be applied.");
            Require(
                InvokeRuntimeUiLabel("BuildPracticeRestartButtonLabel", goodSnapshot) == "정확권 다시"
                    && InvokeRuntimeUiLabel("BuildPracticeCompletionProgressLabel", goodSnapshot) == "연습 완료 · 정확권 복기",
                "A Good-only practice run must offer an exact-window restart action.");

            GreedLastRunSnapshot exactSnapshot = RunPracticeReviewSample(
                now + 30f,
                new[]
                {
                    GreedLastJudgementResult.Success,
                    GreedLastJudgementResult.Success,
                    GreedLastJudgementResult.Success,
                });
            Require(
                exactSnapshot.SuccessCount == 3 && exactSnapshot.GoodCount == 0 && exactSnapshot.MissCount == 0,
                "The clean practice sample must retain its 3/0/0 result.");
            Require(
                exactSnapshot.JudgementText.Contains("준비 완료: 3축 정확 / 일반 런 진입 가능", StringComparison.Ordinal)
                    && exactSnapshot.JudgementText.Contains("회피 동작: 바닥→↑점프 · 상단→↓숙이기 · 측면→↔비틀기 정확", StringComparison.Ordinal),
                "A clean practice run must announce normal-run readiness.");
            Require(
                exactSnapshot.HasTimingRecommendation
                    && exactSnapshot.JudgementText.Contains("입력 보정: 현재 설정 안정", StringComparison.Ordinal)
                    && !InvokeRuntimeUiBool("CanUseTimingRecommendationButton", exactSnapshot),
                "A centered clean practice run must report stable calibration without offering a redundant apply action.");
            Require(!exactSnapshot.JudgementText.Contains("패턴 묶음 완벽", StringComparison.Ordinal), "A clean practice run must still suppress normal-run bundle rewards.");
            Require(
                InvokeRuntimeUiLabel("BuildPracticeRestartButtonLabel", exactSnapshot) == "3축 다시 확인"
                    && InvokeRuntimeUiLabel("BuildPracticeCompletionProgressLabel", exactSnapshot) == "연습 완료 · 일반 런 준비",
                "A clean practice run must offer a neutral three-axis replay action.");
        }

        private static GreedLastRunSnapshot RunPracticeReviewSample(
            float now,
            GreedLastJudgementResult[] outcomes)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.RestartPractice(now);
            float clock = now;
            StartBufferedPattern(core, ref snapshot, ref clock);
            GreedLastTimingType[] expectedTimings =
            {
                GreedLastTimingType.Perfect,
                GreedLastTimingType.Clutch,
                GreedLastTimingType.Offbeat,
            };

            for (int i = 0; i < outcomes.Length; i += 1)
            {
                if (i > 0)
                {
                    core.RequestNextPattern(clock += 2f);
                }

                Require(snapshot.ActivePattern && snapshot.Pattern.TimingType == expectedTimings[i], "Practice review samples must preserve the three-axis order.");
                float inputAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                if (outcomes[i] == GreedLastJudgementResult.Good)
                {
                    float goodDelta = (snapshot.Pattern.SuccessWindowSeconds + snapshot.Pattern.GoodWindowSeconds) * 0.5f;
                    inputAt += goodDelta;
                }

                core.HandleInput(snapshot.Pattern.Channel, inputAt);
                clock = inputAt;
                Require(snapshot.LastResult == outcomes[i], "Practice review sample judgement must match its requested outcome.");
            }

            Require(snapshot.Stopped && snapshot.Phase == GreedLastRunPhase.ClearResolving, "Every practice review sample must reach completion.");
            Require(!snapshot.SaveEligible, "Practice review samples must never create save candidates.");
            return snapshot;
        }

        private static void VerifyLiveRunCoachLines(float now)
        {
            now = Time.realtimeSinceStartup;
            var goodCore = new GreedLastRunCore();
            GreedLastRunSnapshot goodSnapshot = default;
            goodCore.SnapshotChanged += value => goodSnapshot = value;
            goodCore.Enter("live coach good smoke", refillRun: false);
            goodCore.HandleInput(GreedLastRunChannel.Center, now);
            goodCore.Tick(now + 2f);
            goodCore.Tick(now + 3f);
            string activeCoach = GreedLastRuntimeUi.BuildLiveRunCoachLine(goodSnapshot);
            Require(activeCoach.Contains("바닥→↑점프 회피", StringComparison.Ordinal), "The first active trap coach must name the required hazard-bound avoidance action.");
            Require(activeCoach.Contains("정박 타이밍", StringComparison.Ordinal), "The first active trap must explain its timing axis before input.");
            Require(activeCoach.Contains("박 전", StringComparison.Ordinal), "The first active trap coach must expose a beat-count cue.");
            string activeGuide = InvokeRuntimeUiLabel("BuildTimingGuideLabel", goodSnapshot);
            Require(activeGuide.Contains("박 전", StringComparison.Ordinal), "The active timing guide must expose how many beats remain.");
            Require(activeGuide.Contains("바닥→↑점프", StringComparison.Ordinal), "The active timing guide must bind hazard, action symbol, and action name.");
            string activeActionCue = InvokeRuntimeUiLabel("BuildActiveActionCueText", goodSnapshot);
            Require(
                activeActionCue.Contains("바닥 위험 → ↑ 점프 회피", StringComparison.Ordinal),
                "The active action cue must bind the hazard shape to the required avoidance action. Actual: "
                    + activeActionCue);
            Require(activeActionCue.Contains("박 전", StringComparison.Ordinal), "The active action cue must reuse the beat-count timing cue.");
            string activeActionKeyHint = InvokeRuntimeUiLabel("BuildRunChannelKeyHint", goodSnapshot.Pattern.Channel);
            Require(
                !string.IsNullOrEmpty(activeActionKeyHint) && activeActionCue.Contains(activeActionKeyHint, StringComparison.Ordinal),
                "The active action cue must expose the editor key hint for its target lane.");
            string mobileActiveActionCue = InvokeRuntimeUiLabel("BuildActiveActionCueTextForMode", goodSnapshot, false);
            Require(
                mobileActiveActionCue.Contains("좌 탭", StringComparison.Ordinal)
                    && !mobileActiveActionCue.Contains("A/←", StringComparison.Ordinal),
                "The mobile active action cue must expose tap hints instead of editor key hints. Actual: "
                    + mobileActiveActionCue);
            RequireTextLayoutBudget(mobileActiveActionCue, 1, 72, "mobile active action cue payoff");
            Require(
                activeActionCue.Contains("정확+", StringComparison.Ordinal)
                    && activeActionCue.Contains("보정+", StringComparison.Ordinal)
                    && activeActionCue.Contains("집중+" + GreedLastRunCore.PerfectFocusChargeBonus, StringComparison.Ordinal),
                "The active action cue must bind the current trap to exact, Good, and timing payoff previews. Actual: "
                    + activeActionCue);
            RequireTextLayoutBudget(activeActionCue, 1, 72, "active action cue payoff");
            Require(
                InvokeRuntimeUiLabel("BuildRunChannelInputHintForMode", GreedLastRunChannel.Left, false) == "좌 탭"
                    && InvokeRuntimeUiLabel("BuildRunChannelInputHintForMode", GreedLastRunChannel.Center, false) == "중 탭"
                    && InvokeRuntimeUiLabel("BuildRunChannelInputHintForMode", GreedLastRunChannel.Right, false) == "우 탭",
                "Mobile input hints must expose touch targets for every lane.");
            Require(
                InvokeRuntimeUiLabel("BuildTargetChannelInputFeedback", GreedLastRunChannel.Left, true) == "정답키 A/←"
                    && InvokeRuntimeUiLabel("BuildTargetChannelInputFeedback", GreedLastRunChannel.Center, true) == "정답키 S/Space"
                    && InvokeRuntimeUiLabel("BuildTargetChannelInputFeedback", GreedLastRunChannel.Right, true) == "정답키 D/→"
                    && InvokeRuntimeUiLabel("BuildTargetChannelInputFeedback", GreedLastRunChannel.Left, false) == "정답 좌 탭"
                    && InvokeRuntimeUiLabel("BuildTargetChannelInputFeedback", GreedLastRunChannel.Center, false) == "정답 중 탭"
                    && InvokeRuntimeUiLabel("BuildTargetChannelInputFeedback", GreedLastRunChannel.Right, false) == "정답 우 탭",
                "Target-channel feedback must switch every lane between editor keys and mobile tap hints.");
            Require(
                InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", goodSnapshot),
                "The active action cue must be visible during an input-ready active trap.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", default(GreedLastRunSnapshot)),
                "The active action cue must stay hidden outside a real active trap.");

            var choiceCueCore = new GreedLastRunCore();
            GreedLastRunSnapshot choiceCueSnapshot = default;
            choiceCueCore.SnapshotChanged += value => choiceCueSnapshot = value;
            choiceCueCore.Enter("choice cue suppression smoke", refillRun: false);
            Require(choiceCueSnapshot.ChoiceActive, "A fresh normal run must open on a choice screen.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", choiceCueSnapshot)
                    && string.IsNullOrEmpty(InvokeRuntimeUiLabel("BuildActiveActionCueText", choiceCueSnapshot)),
                "The active action cue must stay hidden on choice screens.");
            choiceCueCore.JumpToDevShortcut(GreedLastDevShortcut.RelicChoice, now + 0.1f);
            Require(
                choiceCueSnapshot.ChoiceActive
                    && !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", choiceCueSnapshot)
                    && string.IsNullOrEmpty(InvokeRuntimeUiLabel("BuildActiveActionCueText", choiceCueSnapshot)),
                "The active action cue must stay hidden on dev-jumped choice screens.");

            var stoppedCueCore = new GreedLastRunCore();
            GreedLastRunSnapshot stoppedCueSnapshot = default;
            stoppedCueCore.SnapshotChanged += value => stoppedCueSnapshot = value;
            stoppedCueCore.EnterInfiniteRun(BuildStoredGiftRecord("안정 축", "안정 축"));
            stoppedCueCore.ForceStopInfiniteRunForTest();
            Require(
                stoppedCueSnapshot.Stopped
                    && stoppedCueSnapshot.Phase == GreedLastRunPhase.FailResolving
                    && stoppedCueSnapshot.JudgementText.Contains("무한모드 정산 종료", StringComparison.Ordinal)
                    && !stoppedCueSnapshot.JudgementText.Contains("무한모드 테스트 종료", StringComparison.Ordinal)
                    && !InvokeRuntimeUiStaticBool("ShouldShowActiveActionCue", stoppedCueSnapshot)
                    && string.IsNullOrEmpty(InvokeRuntimeUiLabel("BuildActiveActionCueText", stoppedCueSnapshot)),
                "Forced infinite settlement must read like a result state and keep active action cues hidden.");
            Require(
                InvokeRuntimeUiLabel("PrefixDevShortcut", "F6", "집중 발동") == "F6 집중 발동",
                "Run dev-tool labels must expose their matching keyboard shortcut.");
            Require(
                InvokeRuntimeUiLabel("PrefixDevShortcut", "End", "정산 종료") == "End 정산 종료"
                    && InvokeRuntimeUiLabel("PrefixDevShortcut", "PgUp", "위협 +1") == "PgUp 위협 +1"
                    && InvokeRuntimeUiLabel("PrefixDevShortcut", "PgDn", "위협 -1") == "PgDn 위협 -1",
                "Long-playtest shortcut labels must include End/PgUp/PgDn key prefixes.");
            Require(
                InvokeRuntimeUiLabel("BuildDevSectionShortcutLabel", GreedLastDevShortcut.GiftChoice) == "기프트 선택"
                    && InvokeRuntimeUiLabel("BuildDevSectionShortcutLabel", GreedLastDevShortcut.RelicChoice) == "유물 선택"
                    && InvokeRuntimeUiLabel("BuildDevSectionShortcutLabel", GreedLastDevShortcut.AfterCore) == "핵 직후"
                    && InvokeRuntimeUiLabel("BuildDevSectionShortcutLabel", GreedLastDevShortcut.BeforeClear) == "탈출 직전",
                "Section-jump dev-tool labels must expose each target test section.");
            Require(
                InvokeRuntimeUiLabel("BuildRunLaneBaseLabel", 0, true).Contains("A/←", StringComparison.Ordinal)
                    && InvokeRuntimeUiLabel("BuildRunLaneBaseLabel", 1, true).Contains("S/Space", StringComparison.Ordinal)
                    && InvokeRuntimeUiLabel("BuildRunLaneBaseLabel", 2, true).Contains("D/→", StringComparison.Ordinal),
                "Editor run lane labels must expose compact keyboard hints.");
            Require(
                InvokeRuntimeUiLabel("BuildRunLaneBaseLabelForMode", 0, false, false) == "좌\n탭"
                    && InvokeRuntimeUiLabel("BuildRunLaneBaseLabelForMode", 1, true, false) == "중 탭"
                    && InvokeRuntimeUiLabel("BuildRunLaneBaseLabelForMode", 2, false, false) == "우\n탭",
                "Release/mobile run lane labels must keep touch targets obvious without editor key hints.");
            Require(
                InvokeRuntimeUiLabel("BuildSfxVolumeButtonLabel", 0.6f) == "소리 60%"
                    && InvokeRuntimeUiLabel("BuildSfxVolumeButtonLabel", 1.4f) == "소리 100%"
                    && InvokeRuntimeUiLabel("BuildSfxVolumeButtonLabel", -0.5f) == "소리 0%"
                    && InvokeRuntimeUiLabel("BuildHapticsButtonLabel", true) == "진동 ON"
                    && InvokeRuntimeUiLabel("BuildHapticsButtonLabel", false) == "진동 OFF",
                "Run sensory setting labels must expose stable volume and haptic states.");
            Require(
                GreedLastRuntimeUi.BuildSensorySettingsChecklistText(-0.5f, true) == "감각: 소리 0%·진동 ON"
                    && GreedLastRuntimeUi.BuildSensorySettingsChecklistText(1.4f, false) == "감각: 소리 100%·진동 OFF",
                "The sensory checklist must clamp volume while preserving the haptic state.");
            float normalTargetActionVolume = InvokeRuntimeUiFloat("BuildTargetActionCueVolume", goodSnapshot);
            Require(
                normalTargetActionVolume > 0.105f && normalTargetActionVolume < 0.12f,
                "The normal-run target action cue must keep a slight payoff accent while staying quieter than the timing cue.");
            var highThreatCueCore = new GreedLastRunCore();
            GreedLastRunSnapshot highThreatCueSnapshot = default;
            highThreatCueCore.SnapshotChanged += value => highThreatCueSnapshot = value;
            highThreatCueCore.EnterInfiniteRun(BuildStoredGiftRecord("연쇄 축", "연쇄 축"));
            string lowThreatReadLine = InvokeRuntimeUiLabel("BuildInfiniteThreatReadLine", highThreatCueSnapshot);
            Require(
                lowThreatReadLine.Contains("위협 1/5 안정", StringComparison.Ordinal)
                    && lowThreatReadLine.Contains("위험폭 좁음", StringComparison.Ordinal)
                    && lowThreatReadLine.Contains("보상 정확+0", StringComparison.Ordinal),
                "Low-threat infinite mode must expose a readable threat, danger-width, and reward line. Actual: "
                    + lowThreatReadLine);
            string lowThreatProgressLine = InvokeRuntimeUiLabel(
                "BuildInfiniteProgressLabel",
                highThreatCueSnapshot,
                highThreatCueSnapshot.ChapterTarget);
            Require(
                lowThreatProgressLine.Contains("위협 1/5 안정", StringComparison.Ordinal)
                    && lowThreatProgressLine.Contains("폭좁음", StringComparison.Ordinal)
                    && lowThreatProgressLine.Contains("정확+0", StringComparison.Ordinal),
                "Low-threat infinite progress must summarize threat pressure and reward. Actual: "
                    + lowThreatProgressLine);
            highThreatCueCore.RaiseInfiniteThreatForTest();
            string risingThreatReadLine = InvokeRuntimeUiLabel("BuildInfiniteThreatReadLine", highThreatCueSnapshot);
            Require(
                risingThreatReadLine.Contains("위협 2/5 상승", StringComparison.Ordinal)
                    && risingThreatReadLine.Contains("위험폭 보통", StringComparison.Ordinal)
                    && risingThreatReadLine.Contains("보상 정확+12", StringComparison.Ordinal),
                "Rising-threat infinite mode must use the same pressure labels as threat-change feedback. Actual: "
                    + risingThreatReadLine);
            string risingThreatProgressLine = InvokeRuntimeUiLabel(
                "BuildInfiniteProgressLabel",
                highThreatCueSnapshot,
                highThreatCueSnapshot.ChapterTarget);
            Require(
                risingThreatProgressLine.Contains("위협 2/5 상승", StringComparison.Ordinal)
                    && risingThreatProgressLine.Contains("폭보통", StringComparison.Ordinal)
                    && risingThreatProgressLine.Contains("정확+12", StringComparison.Ordinal),
                "Rising-threat progress must keep pressure, width, and reward labels aligned. Actual: "
                    + risingThreatProgressLine);
            for (int i = 0; i < 3; i += 1)
            {
                highThreatCueCore.RaiseInfiniteThreatForTest();
            }

            Require(
                highThreatCueSnapshot.JudgementText.Contains("테스트 위협 상승 - 위협 5/5 최고", StringComparison.Ordinal)
                    && highThreatCueSnapshot.JudgementText.Contains("위험폭 최대", StringComparison.Ordinal)
                    && highThreatCueSnapshot.JudgementText.Contains("정확+48~58", StringComparison.Ordinal)
                    && highThreatCueSnapshot.JudgementText.Contains("폭·보상 확인", StringComparison.Ordinal),
                "Threat-up test feedback must summarize the changed threat width and reward. Actual: "
                    + highThreatCueSnapshot.JudgementText);
            Require(
                InvokeRuntimeUiFloat("BuildTargetActionCueVolume", highThreatCueSnapshot) > normalTargetActionVolume,
                "High-threat infinite mode must make the target action cue more audible.");
            string highThreatReadLine = InvokeRuntimeUiLabel("BuildInfiniteThreatReadLine", highThreatCueSnapshot);
            Require(
                highThreatReadLine.Contains("위협 5/5 최고", StringComparison.Ordinal)
                    && highThreatReadLine.Contains("위험폭 최대", StringComparison.Ordinal)
                    && highThreatReadLine.Contains("보상 정확+48~58", StringComparison.Ordinal)
                    && highThreatReadLine != lowThreatReadLine,
                "High-threat infinite mode must visibly change its threat read line. Actual: "
                    + highThreatReadLine);
            string highThreatGuideLine = InvokeRuntimeUiLabel("BuildRunGuideLine", highThreatCueSnapshot);
            Require(
                highThreatGuideLine.Contains(highThreatReadLine, StringComparison.Ordinal),
                "The active run guide must surface the infinite threat read line.");
            string highThreatProgressLine = InvokeRuntimeUiLabel(
                "BuildInfiniteProgressLabel",
                highThreatCueSnapshot,
                highThreatCueSnapshot.ChapterTarget);
            Require(
                highThreatProgressLine.Contains("위협 5/5 최고", StringComparison.Ordinal)
                    && highThreatProgressLine.Contains("폭최대", StringComparison.Ordinal)
                    && highThreatProgressLine.Contains("정확+48~58", StringComparison.Ordinal)
                    && highThreatProgressLine != lowThreatProgressLine,
                "High-threat infinite progress must visibly change with threat pressure. Actual: "
                    + highThreatProgressLine);
            string activeRunGuide = InvokeRuntimeUiLabel("BuildRunGuideLine", goodSnapshot);
            Require(activeRunGuide.Contains("바닥→↑점프 정확 +", StringComparison.Ordinal), "The active run guide must bind the avoidance action to the exact payoff.");
            Require(activeRunGuide.Contains("정확 +", StringComparison.Ordinal), "The active run guide must preview exact-score payoff.");
            Require(activeRunGuide.Contains("보정 +", StringComparison.Ordinal), "The active run guide must preview Good-score fallback payoff.");
            string activeScorePreview = InvokeRuntimeUiLabel("BuildActiveScoringPreviewStatus", goodSnapshot);
            Require(
                activeScorePreview.Contains("바닥→↑점프 정확 +", StringComparison.Ordinal)
                    && activeScorePreview.Contains("보정 +", StringComparison.Ordinal),
                "The compact active score preview must keep the hazard-bound exact payoff. Actual: "
                    + activeScorePreview);
            RequireTextLayoutBudget(activeScorePreview, 1, 64, "hazard-bound active score preview");
            string activeObjective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", goodSnapshot);
            Require(activeObjective.Contains("바닥→↑점프 회피", StringComparison.Ordinal), "The active objective must bind the hazard to the required avoidance action.");
            float goodDelta = (goodSnapshot.Pattern.SuccessWindowSeconds + goodSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            goodCore.HandleInput(
                goodSnapshot.Pattern.Channel,
                now + 3f + goodSnapshot.Pattern.CoreSeconds + goodDelta - goodCore.InputTimingOffsetSeconds);
            string goodCoach = GreedLastRuntimeUi.BuildLiveRunCoachLine(goodSnapshot);
            Require(goodCoach.Contains("보정권 생존", StringComparison.Ordinal), "A Good judgement must coach the player toward the exact window.");

            var earlyCore = new GreedLastRunCore();
            GreedLastRunSnapshot earlySnapshot = default;
            earlyCore.SnapshotChanged += value => earlySnapshot = value;
            earlyCore.Enter("live coach early smoke", refillRun: false);
            earlyCore.HandleInput(GreedLastRunChannel.Center, now + 20f);
            earlyCore.Tick(now + 22f);
            earlyCore.Tick(now + 23f);
            earlyCore.HandleInput(
                earlySnapshot.Pattern.Channel,
                now + 23f + earlySnapshot.Pattern.CoreSeconds - earlySnapshot.Pattern.GoodWindowSeconds - 0.08f - earlyCore.InputTimingOffsetSeconds);
            string earlyCoach = GreedLastRuntimeUi.BuildLiveRunCoachLine(earlySnapshot);
            Require(earlyCoach.Contains("살짝 늦게", StringComparison.Ordinal), "An early miss must tell the player to input slightly later.");

            var wrongChannelCore = new GreedLastRunCore();
            GreedLastRunSnapshot wrongChannelSnapshot = default;
            wrongChannelCore.SnapshotChanged += value => wrongChannelSnapshot = value;
            wrongChannelCore.Enter("live coach channel smoke", refillRun: false);
            wrongChannelCore.HandleInput(GreedLastRunChannel.Center, now + 40f);
            wrongChannelCore.Tick(now + 42f);
            wrongChannelCore.Tick(now + 43f);
            GreedLastRunChannel wrongChannel = wrongChannelSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            wrongChannelCore.HandleInput(
                wrongChannel,
                now + 43f + wrongChannelSnapshot.Pattern.CoreSeconds - wrongChannelCore.InputTimingOffsetSeconds);
            string wrongChannelCoach = GreedLastRuntimeUi.BuildLiveRunCoachLine(wrongChannelSnapshot);
            Require(wrongChannelCoach.Contains("채널 먼저 확인", StringComparison.Ordinal), "A wrong-channel miss must coach channel confirmation first.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", wrongChannelSnapshot).Contains("정답키", StringComparison.Ordinal),
                "A wrong-channel miss feedback must expose the target editor key.");

            var rewardCore = new GreedLastRunCore();
            GreedLastRunSnapshot rewardSnapshot = default;
            rewardCore.SnapshotChanged += value => rewardSnapshot = value;
            float rewardClock = now + 60f;
            rewardCore.Enter("live reward smoke", refillRun: false);
            rewardCore.HandleInput(GreedLastRunChannel.Center, rewardClock);
            rewardCore.Tick(rewardClock + 2f);
            rewardCore.Tick(rewardClock + 3f);
            Require(rewardSnapshot.ActivePattern, "The live reward test must expose an active trap.");
            rewardCore.HandleInput(
                rewardSnapshot.Pattern.Channel,
                rewardClock + 3f + rewardSnapshot.Pattern.CoreSeconds - rewardCore.InputTimingOffsetSeconds);
            Require(rewardSnapshot.LastResult == GreedLastJudgementResult.Success, "The live reward test must resolve one exact hit.");
            string rewardGuide = InvokeRuntimeUiLabel("BuildRunGuideLine", rewardSnapshot);
            Require(rewardGuide.Contains("정박 보상까지 2", StringComparison.Ordinal), "The live guide must show the next precision reward distance.");
            Require(rewardGuide.Contains("연쇄 보너스 x1까지 2", StringComparison.Ordinal), "The live guide must show the next chain reward distance.");
            string rewardCoach = GreedLastRuntimeUi.BuildLiveRunCoachLine(rewardSnapshot);
            Require(rewardCoach.Contains("정박 보상까지 2회", StringComparison.Ordinal), "An exact hit must coach the next precision reward distance.");
            Require(!rewardCoach.Contains("정박 보상 2회", StringComparison.Ordinal), "The exact-hit reward coach must avoid duplicate reward pressure text.");
            string rewardBadge = InvokeRuntimeUiLabel("BuildPrecisionBadgeSuffix", rewardSnapshot);
            Require(rewardBadge.Contains("집중 2회", StringComparison.Ordinal), "The combo badge must expose the near precision-focus reward distance.");
            Require(
                rewardSnapshot.RecentPatternFlow.Contains("바→점", StringComparison.Ordinal),
                "The recent pattern flow must include the compact hazard-to-avoidance token.");
            string rewardFlowLine = InvokeRuntimeUiLabel("BuildRunFlowLine", rewardSnapshot);
            Require(
                rewardFlowLine.Contains("최근", StringComparison.Ordinal)
                    && rewardFlowLine.Contains("바→점", StringComparison.Ordinal),
                "The live run flow line must show recent lane, timing, hazard, and avoidance action together.");
            RequireTextLayoutBudget(rewardFlowLine.TrimEnd(), 1, 108, "live recent lane-timing-action flow line");

            var chapterTransitionCore = new GreedLastRunCore();
            GreedLastRunSnapshot chapterTransitionSnapshot = default;
            chapterTransitionCore.SnapshotChanged += value => chapterTransitionSnapshot = value;
            float chapterTransitionClock = now + 80f;
            chapterTransitionCore.Enter("chapter transition objective smoke", refillRun: false);
            chapterTransitionCore.HandleInput(GreedLastRunChannel.Center, chapterTransitionClock);
            StartBufferedPattern(chapterTransitionCore, ref chapterTransitionSnapshot, ref chapterTransitionClock);
            for (int i = 0; i < 2; i += 1)
            {
                if (i > 0)
                {
                    chapterTransitionCore.RequestNextPattern(chapterTransitionClock += 2f);
                }

                chapterTransitionCore.HandleInput(
                    chapterTransitionSnapshot.Pattern.Channel,
                    chapterTransitionClock + chapterTransitionSnapshot.Pattern.CoreSeconds - chapterTransitionCore.InputTimingOffsetSeconds);
            }

            chapterTransitionCore.RequestNextPattern(chapterTransitionClock += 2f);
            Require(
                chapterTransitionSnapshot.ActivePattern && chapterTransitionSnapshot.ChapterProgress == 2,
                "The chapter transition objective test must stand one trap before a chapter choice.");
            string chapterTransitionObjective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", chapterTransitionSnapshot);
            Require(
                chapterTransitionObjective.Contains("다음 통과로 선택지", StringComparison.Ordinal),
                "The objective line must announce that one more pass opens a choice.");
            string chapterTransitionProgress = InvokeRuntimeUiLabel(
                "BuildNormalProgressLabel",
                chapterTransitionSnapshot,
                chapterTransitionSnapshot.ChapterTarget);
            Require(
                chapterTransitionProgress.Contains("선택지 직전", StringComparison.Ordinal)
                    && chapterTransitionProgress.Contains("다음 통과", StringComparison.Ordinal),
                "The progress label must show that a chapter choice is one pass away.");
        }

        private static void VerifyRecoveryPrecisionPayoff(float now)
        {
            now = Time.realtimeSinceStartup;
            var exactCore = new GreedLastRunCore();
            GreedLastRunSnapshot exactSnapshot = default;
            exactCore.SnapshotChanged += value => exactSnapshot = value;
            exactCore.Enter("recovery precision smoke", refillRun: false);
            exactCore.HandleInput(GreedLastRunChannel.Center, now);
            exactCore.Tick(now + 2f);
            exactCore.Tick(now + 3f);
            Require(exactSnapshot.ActivePattern, "The recovery precision test must expose its opening trap.");

            GreedLastRunChannel wrongChannel = exactSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            exactCore.HandleInput(
                wrongChannel,
                now + 3f + exactSnapshot.Pattern.CoreSeconds - exactCore.InputTimingOffsetSeconds);
            Require(exactSnapshot.LastResult == GreedLastJudgementResult.Miss, "The recovery precision test must begin with a real miss.");

            float recoveryPatternStart = now + 5f;
            exactCore.RequestNextPattern(recoveryPatternStart);
            Require(
                exactSnapshot.ActivePattern && exactSnapshot.Pattern.Prompt.Contains("[복구]", StringComparison.Ordinal),
                "A survivable miss must arm one readable recovery pattern.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(exactSnapshot).Contains("복구 패턴", StringComparison.Ordinal),
                "An active recovery pattern must explain its recovery purpose before input.");
            Require(
                InvokeRuntimeUiLabel("BuildRunObjectiveLine", exactSnapshot)
                    .Contains("복구 정박 +" + GreedLastRunCore.RecoveryPrecisionScoreBonus, StringComparison.Ordinal)
                    && InvokeRuntimeUiLabel("BuildRunObjectiveLine", exactSnapshot)
                        .Contains("연쇄 +" + GreedLastRunCore.RecoveryPrecisionComboBonus, StringComparison.Ordinal),
                "The run objective must preview the recovery precision payoff.");
            Require(
                InvokeRuntimeUiLabel("BuildRunGuideLine", exactSnapshot)
                    .Contains("정확 +", StringComparison.Ordinal),
                "The recovery guide must keep its exact-score preview visible.");

            int scoreBeforeRecovery = exactSnapshot.Score;
            int focusBeforeRecovery = exactSnapshot.Focus;
            exactCore.HandleInput(
                exactSnapshot.Pattern.Channel,
                recoveryPatternStart + exactSnapshot.Pattern.CoreSeconds - exactCore.InputTimingOffsetSeconds);
            Require(exactSnapshot.LastResult == GreedLastJudgementResult.Success && exactSnapshot.LastCenterHit, "The center of a recovery pattern must resolve as a recovery precision success.");
            Require(exactSnapshot.RecoveryPrecisionCount == 1, "A recovery center hit must increment its dedicated run count exactly once.");
            Require(exactSnapshot.Combo >= 1 + GreedLastRunCore.RecoveryPrecisionComboBonus, "A recovery center hit must restart the chain with one extra combo step.");
            Require(
                exactSnapshot.Score >= scoreBeforeRecovery + GreedLastRunCore.RecoveryPrecisionScoreBonus,
                "A recovery center hit must include its dedicated score payoff.");
            Require(
                exactSnapshot.Focus >= focusBeforeRecovery + GreedLastRunCore.RecoveryPrecisionFocusBonus || exactSnapshot.FocusStateActive,
                "A recovery center hit must include its focus payoff or activate focus state.");
            Require(exactSnapshot.JudgementText.Contains("복구 정박 - 흐름 재점화", StringComparison.Ordinal), "The core result must name a successful recovery precision payoff.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(exactSnapshot).Contains("흐름 회복 완료", StringComparison.Ordinal),
                "Recovery precision must coach that the run flow is restored.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", exactSnapshot).Contains("흐름 회복 +" + GreedLastRunCore.RecoveryPrecisionScoreBonus, StringComparison.Ordinal),
                "Recovery precision must have distinct central judgement feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildPracticeSuccessPayoffLine", exactSnapshot).Contains("복구 정박", StringComparison.Ordinal),
                "Practice feedback must be able to explain the recovery payoff directly.");
            Require(exactSnapshot.TimingProfile.Contains("복구 정박 1회", StringComparison.Ordinal), "Recovery precision count must survive into the timing profile.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildJudgementFlowSummary",
                    exactSnapshot.SuccessCount,
                    exactSnapshot.GoodCount,
                    exactSnapshot.MissCount,
                    exactSnapshot.TimingProfile).Contains("복구 정박 1", StringComparison.Ordinal),
                "Run result flow summaries must preserve the recovery precision count.");
            GreedLastRunAttemptRecord recoveryAttempt = exactCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(
                InvokeMockBackendStaticLabel("BuildNormalAttemptReviewText", recoveryAttempt)
                    .Contains("복구 정박 1", StringComparison.Ordinal),
                "An interrupted run review must preserve a successful recovery after its miss.");
            var recoveryRecord = new GreedLastRunRecord(
                true,
                exactSnapshot.Score,
                exactSnapshot.Health,
                exactSnapshot.Combo,
                exactSnapshot.Focus,
                exactSnapshot.MaxCombo,
                exactSnapshot.SuccessCount,
                exactSnapshot.GoodCount,
                exactSnapshot.MissCount,
                exactSnapshot.Distance,
                "복구 검증 조합",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                exactSnapshot.TimingProfile);
            Require(
                InvokeMockBackendStaticLabel("BuildRunClearStrengthText", recoveryRecord)
                    .Contains("실수 후 복구 정박 1회", StringComparison.Ordinal),
                "A clear result must recognize a full recovery as a run strength.");
            Require(
                InvokeMockBackendStaticLabel("BuildRunClearReviewText", recoveryRecord)
                    .Contains("복구 정박 1", StringComparison.Ordinal),
                "A clear review must carry recovery precision into its next-action summary.");

            var goodCore = new GreedLastRunCore();
            GreedLastRunSnapshot goodSnapshot = default;
            goodCore.SnapshotChanged += value => goodSnapshot = value;
            float goodNow = now + 20f;
            goodCore.Enter("recovery Good smoke", refillRun: false);
            goodCore.HandleInput(GreedLastRunChannel.Center, goodNow);
            goodCore.Tick(goodNow + 2f);
            goodCore.Tick(goodNow + 3f);
            GreedLastRunChannel goodWrongChannel = goodSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            goodCore.HandleInput(
                goodWrongChannel,
                goodNow + 3f + goodSnapshot.Pattern.CoreSeconds - goodCore.InputTimingOffsetSeconds);
            float goodRecoveryStart = goodNow + 5f;
            goodCore.RequestNextPattern(goodRecoveryStart);
            float goodDelta = (goodSnapshot.Pattern.SuccessWindowSeconds + goodSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            goodCore.HandleInput(
                goodSnapshot.Pattern.Channel,
                goodRecoveryStart + goodSnapshot.Pattern.CoreSeconds + goodDelta - goodCore.InputTimingOffsetSeconds);
            Require(goodSnapshot.LastResult == GreedLastJudgementResult.Good, "The outer recovery band must remain a survivable Good judgement.");
            Require(goodSnapshot.RecoveryPrecisionCount == 0, "A recovery Good must not grant the center precision payoff.");
            Require(goodSnapshot.JudgementText.Contains("복구 보정 - 생존 흐름 유지", StringComparison.Ordinal), "Recovery Good must name its survival-only fallback.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(goodSnapshot).Contains("복구 성공", StringComparison.Ordinal),
                "Recovery Good must coach the next precision attempt without treating it as failure.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", goodSnapshot).Contains("복구 성공", StringComparison.Ordinal),
                "Recovery Good must keep distinct central feedback.");
        }

        private static void VerifyClutchCounterPayoff(float now)
        {
            now = Time.realtimeSinceStartup;
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter("Clutch counter smoke", refillRun: false);
            core.HandleInput(GreedLastRunChannel.Center, now);
            core.Tick(now + 2f);
            core.Tick(now + 3f);
            Require(snapshot.ActivePattern && snapshot.Pattern.TimingType == GreedLastTimingType.Perfect, "The counter test must begin with the chapter-one Perfect lesson.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot)
                    .Contains("중앙 집중 +" + GreedLastRunCore.PerfectFocusChargeBonus, StringComparison.Ordinal),
                "An active Perfect trap must preview its center focus payoff.");
            Require(
                InvokeRuntimeUiLabel("BuildRunObjectiveLine", snapshot)
                    .Contains("정박 중앙 집중 +" + GreedLastRunCore.PerfectFocusChargeBonus, StringComparison.Ordinal),
                "The run objective must prioritize the Perfect focus opportunity.");
            int focusBeforePerfect = snapshot.Focus;
            core.HandleInput(
                snapshot.Pattern.Channel,
                now + 3f + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success, "The counter test setup must clear its first Perfect trap.");
            Require(snapshot.PerfectFocusChargeCount == 1, "A center Perfect input must increment its focus-charge count exactly once.");
            Require(
                snapshot.Focus >= focusBeforePerfect + 1 + GreedLastRunCore.PerfectFocusChargeBonus || snapshot.FocusStateActive,
                "A center Perfect input must include its extra focus charge or activate focus state.");
            Require(snapshot.JudgementText.Contains("정박 중앙 - 집중 충전", StringComparison.Ordinal), "The core result must name the Perfect focus charge.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("정박 충전", StringComparison.Ordinal),
                "A successful Perfect focus input must provide immediate coaching confirmation.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot)
                    .Contains("정박 충전 +" + GreedLastRunCore.PerfectFocusChargeBonus, StringComparison.Ordinal),
                "A successful Perfect focus input must have distinct central feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildPracticeSuccessPayoffLine", snapshot).Contains("정박 충전", StringComparison.Ordinal),
                "Practice feedback must be able to explain the Perfect focus payoff directly.");
            Require(snapshot.TimingProfile.Contains("정박 충전 1회", StringComparison.Ordinal), "Perfect focus-charge count must survive into the timing profile.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildJudgementFlowSummary",
                    snapshot.SuccessCount,
                    snapshot.GoodCount,
                    snapshot.MissCount,
                    snapshot.TimingProfile).Contains("충전 1", StringComparison.Ordinal),
                "Result flow summaries must preserve the Perfect focus-charge count.");

            float clutchStart = now + 5f;
            core.RequestNextPattern(clutchStart);
            Require(snapshot.ActivePattern && snapshot.Pattern.TimingType == GreedLastTimingType.Clutch, "The second chapter-one lesson must expose a Clutch trap.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot)
                    .Contains("정박 반격 +" + GreedLastRunCore.ClutchCounterScoreBonus, StringComparison.Ordinal),
                "An active Clutch trap must preview its center counter payoff.");
            Require(
                InvokeRuntimeUiLabel("BuildRunObjectiveLine", snapshot)
                    .Contains("압박 정박 반격 +" + GreedLastRunCore.ClutchCounterScoreBonus, StringComparison.Ordinal),
                "The run objective must prioritize the Clutch counter opportunity.");
            Require(
                InvokeRuntimeUiLabel("BuildActiveActionCueText", snapshot)
                    .Contains("반격+" + GreedLastRunCore.ClutchCounterScoreBonus, StringComparison.Ordinal),
                "The active Clutch action cue must preview the counter payoff.");

            int scoreBeforeCounter = snapshot.Score;
            core.HandleInput(
                snapshot.Pattern.Channel,
                clutchStart + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success && snapshot.LastCenterHit, "A center Clutch input must resolve as an exact counter.");
            Require(snapshot.ClutchCounterCount == 1, "A center Clutch input must increment its counter count exactly once.");
            Require(
                snapshot.Score >= scoreBeforeCounter + GreedLastRunCore.ClutchCounterScoreBonus,
                "A center Clutch input must include its dedicated counter score.");
            Require(snapshot.JudgementText.Contains("압박 정박 - 반격", StringComparison.Ordinal), "The core result must name the Clutch counter.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("압박 반격 성공", StringComparison.Ordinal),
                "A successful Clutch counter must provide immediate coaching confirmation.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot)
                    .Contains("압박 반격 +" + GreedLastRunCore.ClutchCounterScoreBonus, StringComparison.Ordinal),
                "A successful Clutch counter must have distinct central feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildPracticeSuccessPayoffLine", snapshot).Contains("압박 반격", StringComparison.Ordinal),
                "Practice feedback must be able to explain the Clutch payoff directly.");
            Require(snapshot.TimingProfile.Contains("압박 반격 1회", StringComparison.Ordinal), "Clutch counter count must survive into the timing profile.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildJudgementFlowSummary",
                    snapshot.SuccessCount,
                    snapshot.GoodCount,
                    snapshot.MissCount,
                    snapshot.TimingProfile).Contains("반격 1", StringComparison.Ordinal),
                "Result flow summaries must preserve the Clutch counter count.");

            float offbeatStart = now + 7f;
            core.RequestNextPattern(offbeatStart);
            Require(snapshot.ActivePattern && snapshot.Pattern.TimingType == GreedLastTimingType.Offbeat, "The third chapter-one lesson must expose an Offbeat trap.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot)
                    .Contains("정박 연쇄 +" + GreedLastRunCore.OffbeatFlowComboBonus, StringComparison.Ordinal),
                "An active Offbeat trap must preview its center chain payoff.");
            string offbeatObjective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", snapshot);
            Require(
                offbeatObjective.Contains("엇박 정박 연쇄 +" + GreedLastRunCore.OffbeatFlowComboBonus, StringComparison.Ordinal)
                    && offbeatObjective.Contains("다음 통과로 선택지", StringComparison.Ordinal),
                "The final chapter trap must combine its Offbeat payoff with the upcoming transition.");
            Require(
                InvokeRuntimeUiLabel("BuildActiveActionCueText", snapshot)
                    .Contains("연쇄+" + GreedLastRunCore.OffbeatFlowComboBonus, StringComparison.Ordinal),
                "The active Offbeat action cue must preview the chain payoff.");

            int comboBeforeOffbeat = snapshot.Combo;
            core.HandleInput(
                snapshot.Pattern.Channel,
                offbeatStart + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success && snapshot.LastCenterHit, "A center Offbeat input must resolve as an exact flow success.");
            Require(snapshot.OffbeatFlowCount == 1, "A center Offbeat input must increment its flow count exactly once.");
            Require(
                snapshot.Combo >= comboBeforeOffbeat + 1 + GreedLastRunCore.OffbeatFlowComboBonus,
                "A center Offbeat input must include its extra chain step.");
            Require(snapshot.JudgementText.Contains("엇박 정박 - 연쇄 가속", StringComparison.Ordinal), "The core result must name the Offbeat flow payoff.");
            Require(snapshot.ChoiceActive, "The final Offbeat lesson must continue into its chapter choice after settling feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot)
                    .Contains("엇박 정박 - 연쇄 가속", StringComparison.Ordinal),
                "A successful Offbeat flow input must have distinct central feedback.");
            Require(snapshot.TimingProfile.Contains("엇박 연쇄 1회", StringComparison.Ordinal), "Offbeat flow count must survive into the timing profile.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildJudgementFlowSummary",
                    snapshot.SuccessCount,
                    snapshot.GoodCount,
                    snapshot.MissCount,
                    snapshot.TimingProfile).Contains("엇박 연쇄 1", StringComparison.Ordinal),
                "Result flow summaries must preserve the Offbeat flow count.");
        }

        private static void VerifyLobbyResultActionLabels()
        {
            GreedLastStateSnapshot failedSnapshot = BuildLobbyLabelSnapshot(
                "탈출 실패 기록 저장\n복기: 채널 확인 / 1장 실수 1회",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyNormalRunButtonLabel", failedSnapshot) == "복기 재도전",
                "A failed normal result must turn the normal-run button into an explicit retry action.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", failedSnapshot) == "복기 연습",
                "A failed normal result must turn the practice button into a review drill.");
            GreedLastStateSnapshot failedAvoidanceSnapshot = BuildLobbyLabelSnapshot(
                "탈출 실패 기록 저장\n복기: 채널 확인 / 숙이기 실수 1회 / 기초연습 숙이기 단계",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", failedAvoidanceSnapshot) == "숙이기 복기",
                "A failed normal result with a slide weakness must label the practice button as slide review.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", failedSnapshot.Detail) == "복기 기록",
                "A failed normal result must route the record button toward review.");
            GreedLastStateSnapshot failedTargetSnapshot = BuildLobbyLabelSnapshot(
                "탈출 실패 기록 저장\n복기: 채널 확인 / 1장 실수 1회\n복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1\n재도전 목표: 1장 무실수 / 현재 1",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyNormalRunButtonLabel", failedTargetSnapshot) == "목표 재도전",
                "A failed review target must turn the normal-run button into a target retry action.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", failedTargetSnapshot) == "목표 연습",
                "A failed review target must turn the practice button into a target drill.");
            GreedLastStateSnapshot achievedTargetSnapshot = BuildLobbyLabelSnapshot(
                "탈출 중단 기록 저장\n복기: 정박 흐름\n복기 목표: 1장 무실수 / 현재 1 / 달성 무실수\n재도전 목표: 1장 보정 0회",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyNormalRunButtonLabel", achievedTargetSnapshot) == "상향 재도전",
                "An achieved review target must distinguish its advanced retry action in the lobby.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", achievedTargetSnapshot) == "상향 목표 연습",
                "An achieved review target must distinguish its advanced practice action in the lobby.");
            string retryContext = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(failedSnapshot.Detail + "\n재도전 목표: 1장 무실수 / 현재 1");
            Require(retryContext.Contains("복기 재도전", StringComparison.Ordinal), "A lobby retry after failure must carry a review retry prefix.");
            Require(retryContext.Contains("복기: 채널 확인 / 1장 실수 1회", StringComparison.Ordinal), "A lobby retry after failure must preserve the review cause in the opening context.");
            Require(retryContext.Contains("재도전 목표: 1장 무실수 / 현재 1", StringComparison.Ordinal), "A lobby retry after failure must preserve the retry target in the new run opening context.");
            string retryContextWithTargetResult = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(
                failedSnapshot.Detail + "\n복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1\n재도전 목표: 1장 무실수 / 현재 1");
            Require(
                retryContextWithTargetResult.Contains("복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1", StringComparison.Ordinal),
                "A lobby retry after a review-target attempt must preserve the previous target result.");
            string practiceContext = GreedLastApp.BuildPracticeOpeningContextFromLobbyDetail(failedSnapshot.Detail + "\n재도전 목표: 1장 무실수 / 현재 1");
            Require(practiceContext.Contains("복기 연습", StringComparison.Ordinal), "A lobby practice drill after failure must carry a review-practice prefix.");
            Require(practiceContext.Contains("복기: 채널 확인 / 1장 실수 1회", StringComparison.Ordinal), "A lobby practice drill after failure must preserve the review cause.");
            string avoidancePracticeContext = GreedLastApp.BuildPracticeOpeningContextFromLobbyDetail(
                "탈출 실패 기록 저장\n복기: 2장 실수 1회 / 비틀기 실수 2회 / 기초연습 비틀기 단계\n재도전 목표: 2장 무실수 / 현재 1");
            Require(
                avoidancePracticeContext.Contains("기초연습 비틀기 단계", StringComparison.Ordinal),
                "A practice drill from an avoidance weakness must carry the target avoidance step.");
            GreedLastStateSnapshot twistAvoidanceSnapshot = BuildLobbyLabelSnapshot(
                "탈출 실패 기록 저장\n복기: 2장 실수 1회 / 비틀기 실수 2회 / 기초연습 비틀기 단계\n재도전 목표: 2장 무실수 / 현재 1",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", twistAvoidanceSnapshot) == "비틀기 복기",
                "A failed normal result with a twist weakness must label the practice button as twist review.");
            GreedLastStateSnapshot jumpCorrectionSnapshot = BuildLobbyLabelSnapshot(
                "탈출 중단 기록 저장\n복기: 1장 보정 1회 / 점프 보정 1회 / 기초연습 점프 단계\n재도전 목표: 1장 보정 0회 / 현재 1",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", jumpCorrectionSnapshot) == "점프 정확권",
                "A Good-only jump weakness must label the practice button as an exact-band jump drill.");
            var avoidancePracticeCore = new GreedLastRunCore();
            GreedLastRunSnapshot avoidancePracticeSnapshot = default;
            avoidancePracticeCore.SnapshotChanged += value => avoidancePracticeSnapshot = value;
            float avoidancePracticeClock = Time.realtimeSinceStartup + 540f;
            avoidancePracticeCore.EnterPractice(avoidancePracticeContext);
            Require(
                avoidancePracticeSnapshot.ChapterProgress == 2
                    && avoidancePracticeSnapshot.JudgementText.Contains("엇박 측면→↔비틀기 단계를 바로 확인", StringComparison.Ordinal),
                "An avoidance-specific practice drill must start on the requested practice step.");
            string avoidanceCountdownContext = InvokeRuntimeUiLabel("BuildStartCountdownContext", avoidancePracticeSnapshot);
            Require(
                avoidanceCountdownContext.Contains("복기:", StringComparison.Ordinal)
                    && avoidanceCountdownContext.Contains("비틀기 실수", StringComparison.Ordinal)
                    && avoidanceCountdownContext.Contains("기초연습 비틀기 단계", StringComparison.Ordinal),
                "An avoidance-specific practice countdown must prioritize the review cause.");
            StartBufferedPattern(avoidancePracticeCore, ref avoidancePracticeSnapshot, ref avoidancePracticeClock);
            Require(
                avoidancePracticeSnapshot.Pattern.AvoidanceType == GreedLastAvoidanceType.Twist
                    && avoidancePracticeSnapshot.Pattern.TimingType == GreedLastTimingType.Offbeat,
                "A twist review drill must begin with the offbeat twist trap.");
            float avoidancePracticeInput = avoidancePracticeClock
                + avoidancePracticeSnapshot.Pattern.CoreSeconds
                - avoidancePracticeCore.InputTimingOffsetSeconds;
            avoidancePracticeCore.HandleInput(avoidancePracticeSnapshot.Pattern.Channel, avoidancePracticeInput);
            Require(avoidancePracticeSnapshot.Stopped, "A final-step avoidance drill must complete after its focused trap.");
            avoidancePracticeCore.RestartPractice(avoidancePracticeInput + 1f);
            Require(
                avoidancePracticeSnapshot.StartCountdownActive
                    && avoidancePracticeSnapshot.ChapterProgress == 2
                    && avoidancePracticeSnapshot.JudgementText.Contains("엇박 측면→↔비틀기 단계를 바로 확인", StringComparison.Ordinal),
                "Restarting an avoidance-specific drill must preserve its focused practice step.");

            GreedLastStateSnapshot candidateSnapshot = BuildLobbyLabelSnapshot(
                "탈출 성공 / 기록 저장\n복기: 전 구간 정확 흐름",
                saveCandidateAvailable: true,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbySaveLoadoutButtonLabel", candidateSnapshot, "추천 S1") == "저장 슬롯 선택 추천 S1",
                "A pending save candidate must expose slot selection as the primary save action.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyNormalRunButtonLabel", candidateSnapshot) == "후보 먼저 저장",
                "A pending save candidate must keep the player from starting a new normal run first.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyInfiniteButtonLabel", candidateSnapshot, "추천 S1") == "무한 후보 저장 필요",
                "A pending save candidate must keep the infinite button pointed at saving the candidate first.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", "보충 런 클리어 / 기록 저장\n점수 200 / 거리 30.0m") == "보충 기록",
                "A refill-run result must distinguish its record review button from normal clear review.");

            GreedLastStateSnapshot infiniteSnapshot = BuildLobbyLabelSnapshot(
                "무한모드 종료 / 기록 저장\n복기: 무한 1구간",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyInfiniteButtonLabel", infiniteSnapshot, "S1 마지막 A").StartsWith("무한 재도전", StringComparison.Ordinal),
                "An infinite result must make the infinite button read as a retry action.");
            GreedLastStateSnapshot settlementReentrySnapshot = BuildLobbyLabelSnapshot(
                "무한모드 종료 / 기록 저장\n힌트: 정산 종료 기록\n다음: 정산 종료 확인 후 무한모드 실전 재개",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyInfiniteButtonLabel", settlementReentrySnapshot, "S1 마지막 A").StartsWith("무한 실전 재개", StringComparison.Ordinal),
                "A forced settlement real-reentry result must label the infinite button as real play resumption.");
            string settlementPracticeLabel = InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", settlementReentrySnapshot);
            Require(
                !settlementPracticeLabel.Contains("복기", StringComparison.Ordinal)
                    && !settlementPracticeLabel.Contains("목표", StringComparison.Ordinal),
                "A forced settlement real-reentry result must not make the practice button look like a review target.");
            GreedLastStateSnapshot emptyLoadoutInfiniteSnapshot = BuildLobbyLabelSnapshot(
                "저장 조합이 비었습니다.",
                saveCandidateAvailable: false,
                infiniteUnlocked: true,
                hasUsableSaveSlot: false,
                hasOccupiedSaveSlot: false);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyInfiniteButtonLabel", emptyLoadoutInfiniteSnapshot, "없음") == "무한 조합 보충",
                "An unlocked infinite lobby with no usable save loadout must label the infinite action as refill.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyNormalRunButtonLabel", emptyLoadoutInfiniteSnapshot) == "보충 런",
                "An unlocked lobby with no usable save loadout must label the normal run action as a refill run.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbySaveLoadoutButtonLabel", emptyLoadoutInfiniteSnapshot, "없음") == "보충 안내",
                "An unlocked lobby with no usable save loadout must label the save-loadout action as refill guidance.");
            Require(
                InvokeRuntimeUiLabel("BuildInfiniteLoadoutSelectActionLabel", emptyLoadoutInfiniteSnapshot, false) == "보충 런",
                "An infinite loadout selection screen with no usable slots must offer the same refill-run action.");
            Require(
                InvokeRuntimeUiLabel("BuildInfinitePrepSaveLoadoutButtonLabel", emptyLoadoutInfiniteSnapshot) == "보충 안내",
                "An infinite preparation screen with no usable save loadout must label its save-loadout side button as refill guidance.");
            Require(
                InvokeRuntimeUiLabel("BuildSaveSlotDetailButtonLabel", emptyLoadoutInfiniteSnapshot, true, false) == "보충 안내",
                "The auxiliary detail button must read as refill guidance when every infinite loadout slot is empty.");
            Require(
                InvokeRuntimeUiLabel("BuildSaveSlotModeText", emptyLoadoutInfiniteSnapshot, false, false, false) == "보충 안내\n보충 런"
                    && InvokeRuntimeUiLabel("BuildSaveSlotModeText", emptyLoadoutInfiniteSnapshot, true, false, false) == "보충 안내\n보충 런",
                "The save-slot side mode label must point to refill instead of empty-slot management when every slot is empty.");
            Require(
                InvokeRuntimeUiStaticBool("ShouldSaveDraftOfferRefill", emptyLoadoutInfiniteSnapshot, false, false),
                "A save-loadout management screen with no candidate and no usable slots must let the primary button start a refill run.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldSaveDraftOfferRefill", emptyLoadoutInfiniteSnapshot, true, false),
                "The refill primary shortcut must stay out of the infinite loadout selection action path.");
            GreedLastLobbySnapshot emptyLoadoutLobbySnapshot = BuildAppLobbySnapshot(
                saveCandidateAvailable: false,
                infiniteUnlocked: true,
                hasUsableSaveSlot: false,
                hasOccupiedSaveSlot: false);
            string emptyInfiniteSlotNotice = InvokeAppStaticLabel(
                "BuildInfiniteLoadoutSlotSelectionNotice",
                emptyLoadoutLobbySnapshot,
                0);
            string emptySaveDraftSlotNotice = InvokeAppStaticLabel(
                "BuildSaveDraftSlotSelectionNotice",
                emptyLoadoutLobbySnapshot,
                false,
                0);
            Require(
                emptyInfiniteSlotNotice.Contains("기본 버튼으로 보충 런", StringComparison.Ordinal)
                    && emptySaveDraftSlotNotice.Contains("기본 버튼으로 보충 런", StringComparison.Ordinal)
                    && !emptyInfiniteSlotNotice.Contains("선택 완료", StringComparison.Ordinal)
                    && !emptySaveDraftSlotNotice.Contains("상세 보기", StringComparison.Ordinal),
                "Empty save-loadout slot taps must keep refill guidance instead of implying a usable selection or slot management.");
            Require(
                InvokeRuntimeUiLabel("BuildCompactSaveSlotLaneState", "빈 슬롯", true).Contains("보충", StringComparison.Ordinal)
                    && InvokeRuntimeUiLabel("BuildCompactSaveSlotLaneState", "빈 슬롯", false) == "빈 슬롯",
                "Empty save-loadout lane labels must switch from empty-slot copy to refill-needed copy only when every slot is empty.");
            Require(
                InvokeRuntimeUiLabel(
                    "BuildSaveSlotLaneActionHint",
                    true,
                    true,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true) == "보충 런"
                    && InvokeRuntimeUiLabel(
                        "BuildSaveSlotLaneActionHint",
                        true,
                        true,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false) == "사용 불가",
                "Empty save-loadout lane action hints must point to refill-run only on the all-empty refill path.");
            Require(
                InvokeRuntimeUiLabel(
                    "BuildSaveSlotLaneActionHint",
                    true,
                    true,
                    false,
                    true,
                    true,
                    true,
                    false,
                    false) == "자동 비움"
                    && InvokeRuntimeUiLabel(
                        "BuildSaveSlotLaneActionHint",
                        false,
                        true,
                        false,
                        true,
                        true,
                        true,
                        false,
                        false) == "시작 후 비움",
                "The final-use save-loadout lane action hint must expose auto-clear before and after slot selection.");
            GreedLastLobbySnapshot usableLoadoutLobbySnapshot = BuildAppLobbySnapshot(
                saveCandidateAvailable: false,
                infiniteUnlocked: true,
                hasUsableSaveSlot: true,
                hasOccupiedSaveSlot: true);
            Require(
                InvokeAppStaticLabel("BuildInfiniteLoadoutSlotSelectionNotice", usableLoadoutLobbySnapshot, 0)
                    .Contains("선택 완료", StringComparison.Ordinal)
                    && InvokeAppStaticLabel("BuildSaveDraftSlotSelectionNotice", usableLoadoutLobbySnapshot, true, 0)
                        .Contains("저장을 누르면", StringComparison.Ordinal),
                "Usable slots and pending save candidates must keep their regular selection/save guidance.");
            GreedLastStateSnapshot expiredLoadoutInfiniteSnapshot = BuildLobbyLabelSnapshot(
                "저장 조합 사용 완료",
                saveCandidateAvailable: false,
                infiniteUnlocked: true,
                hasUsableSaveSlot: false,
                hasOccupiedSaveSlot: true);
            Require(
                InvokeRuntimeUiLabel("BuildSaveSlotDetailButtonLabel", expiredLoadoutInfiniteSnapshot, true, false) == "보충 안내",
                "The auxiliary detail button must still read as refill guidance when legacy slots are occupied but no slot is usable.");
            Require(
                InvokeRuntimeUiLabel("BuildInfinitePrepSaveLoadoutButtonLabel", infiniteSnapshot) == "저장 조합 변경",
                "An infinite preparation screen with a usable save loadout must keep the regular save-loadout-change label.");
            Require(
                !InvokeRuntimeUiStaticBool("ShouldSaveDraftOfferRefill", infiniteSnapshot, false, false),
                "A save-loadout management screen with at least one usable slot must stay in slot-management mode.");
            GreedLastStateSnapshot infiniteTargetSnapshot = BuildLobbyLabelSnapshot(
                "무한모드 종료 / 기록 저장\n복기: 무한 1구간\n복기 목표: 다음 구간 무실수 / 직전 2 / 실패 실수 2\n목표: 다음 구간 무실수 / 직전 2",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyInfiniteButtonLabel", infiniteTargetSnapshot, "S1 마지막 A").StartsWith("무한 목표 재도전", StringComparison.Ordinal),
                "An infinite review-target result must mark the infinite retry button as target-driven.");
            GreedLastStateSnapshot achievedInfiniteTargetSnapshot = BuildLobbyLabelSnapshot(
                "무한모드 종료 / 기록 저장\n복기: 무한 2구간\n복기 목표: 다음 구간 무실수 / 직전 2 / 달성 무실수\n목표: 다음 구간 보정 0회",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyInfiniteButtonLabel", achievedInfiniteTargetSnapshot, "S1 마지막 A").StartsWith("무한 상향 재도전", StringComparison.Ordinal),
                "An achieved infinite review target must distinguish its advanced retry action in the lobby.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", infiniteSnapshot.Detail) == "무한 복기",
                "An infinite result must route the record button toward infinite review.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", "흔적\n다음 확인: 완성 루프 기록 확인") == "완성 기록",
                "A completed prototype-loop record check must label the record button as final completion records.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 복기 재도전으로 실수 구간 보완") == "일반",
                "A normal review retry next-action must target the normal-run button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 복기 연습으로 정확권 전환") == "연습",
                "A review-practice next-action must target the practice button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 무한 복기 재도전으로 목표 진행") == "무한",
                "An infinite review retry next-action must target the infinite button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 무한 복기 전 기초연습 숙이기 단계 확인") == "연습",
                "An infinite pre-retry avoidance drill must target practice before the next infinite run.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 무한 조합 보충") == "무한",
                "An infinite refill next-action must target the infinite/refill button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 무한 버튼으로 보충 런 진입") == "무한",
                "A direct refill-run next-action must target the infinite/refill button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 보충 런 클리어 후 새 조합 저장") == "무한",
                "A direct refill-run clear next-action must still target the infinite/refill route.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 정산 종료 확인 후 무한모드 실전 재개") == "무한",
                "A forced infinite settlement next-action must target the infinite button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 보충 후 무한 재진입") == "무한",
                "A prototype refill/re-entry check phrasing must target the infinite/refill button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 보충 런 클리어 후 새 조합 저장") == "무한",
                "A prototype refill-run checkpoint phrasing must target the infinite/refill button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 저장 후보 저장/포기") == "저장",
                "A prototype save-candidate checkpoint phrasing must target the save-loadout button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 저장 조합으로 무한 시작") == "무한",
                "A prototype save-loadout infinite-start checkpoint must target the infinite button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 저장 조합 2회 소진") == "무한",
                "A prototype two-use consumption checkpoint must keep Enter on the infinite/refill route.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 무한 기록 생성") == "무한",
                "A prototype infinite-record checkpoint must target the infinite button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 무한 위협 폭·보상 확인") == "무한",
                "A prototype infinite threat-width/reward checkpoint must target the infinite button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 일반 런 첫 함정") == "일반",
                "A prototype normal-run checkpoint must target the normal-run button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 복기 연습 후 탈출") == "연습",
                "A prototype review-practice checkpoint must target practice.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 복기 재도전으로 실수 구간 보완") == "일반",
                "A prototype normal review-retry checkpoint must target the normal-run button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 무한 복기 재도전으로 목표 진행") == "무한",
                "A prototype infinite review-retry checkpoint must target the infinite button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 무한 복기 전 기초연습 숙이기 단계 확인") == "연습",
                "A prototype infinite pre-retry drill checkpoint must target practice.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 기록 보드 확인") == "기록",
                "A prototype record-board checkpoint must target records.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음 확인: 완성 루프 기록 확인") == "기록",
                "A completed prototype-loop checkpoint must target records.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 기록 보기에서 최근 기록 확인") == "기록",
                "A record-review next-action must target the record button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 기록 보드에서 최근 기록 확인") == "기록",
                "A record-board next-action phrasing must still target the record button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 성장 보정에서 집중 Lv.2 적용") == "성장",
                "A growth-correction next-action must target the growth button.");
            Require(
                InvokeRuntimeUiLabel("ResolveLobbyNextActionTarget", "결과\n다음: 흔적에서 검증 순서 확인") == "흔적",
                "A profile next-action must target the profile button.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "무한 조합 보충으로 보충 런 진입") == "무한 조합 보충",
                "Profile next-action refill phrasing must highlight the refill/infinite action.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "보충 후 무한 재진입") == "무한 조합 보충",
                "Profile next-check refill/re-entry phrasing must highlight the refill/infinite action.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "보충 런 클리어 후 새 조합 저장") == "무한 조합 보충",
                "Profile next-check direct refill-run phrasing must highlight the refill/infinite action.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "저장 후보 저장/포기") == "저장 조합",
                "Profile next-check save-candidate phrasing must highlight save loadouts.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "저장 조합으로 무한 시작") == "무한모드",
                "Profile next-check save-loadout start phrasing must highlight infinite mode.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "정산 종료 확인 완료 / 실제 무한 런으로 생존 흐름 확인") == "무한모드",
                "Profile next-check settlement phrasing must highlight infinite mode.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "저장 조합 2회 소진") == "무한모드",
                "Profile next-check two-use phrasing must highlight infinite mode.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "무한 위협 폭·보상 확인") == "무한모드",
                "Profile next-check infinite threat-width/reward phrasing must highlight infinite mode.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "무한 복기 전 기초연습 숙이기 단계 확인") == "복기 연습",
                "Profile next-check infinite pre-retry drill phrasing must highlight practice, not infinite mode.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "일반 런 첫 함정") == "일반 런",
                "Profile next-check normal-run phrasing must highlight the normal run.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "기록 보드에서 최근 기록 확인") == "기록 보기",
                "Profile next-action record-board phrasing must highlight the record action.");
            Require(
                InvokeMockBackendStaticLabel("ResolveProfileHighlightActionFromNextAction", "완성 루프 기록 확인") == "기록 보기",
                "Profile completed-loop phrasing must highlight the record action.");
            var prototypeNextActionBackend = new GreedLastMockBackend();
            Require(
                InvokeMockBackendLabel(prototypeNextActionBackend, "BuildPrototypeNextCheckActionText", "무한 복기 전 기초연습 숙이기 단계 확인") == "복기 연습",
                "The profile checklist action label must send infinite pre-retry drills to practice.");
            Require(
                InvokeMockBackendLabel(prototypeNextActionBackend, "BuildPrototypeNextCheckActionText", "무한 복기 재도전으로 목표 진행") == "무한모드",
                "The profile checklist action label must send infinite review retries to infinite mode.");
            Require(
                InvokeMockBackendLabel(prototypeNextActionBackend, "BuildPrototypeNextCheckActionText", "무한 위협 폭·보상 확인") == "무한모드",
                "The profile checklist action label must send infinite threat-width/reward checks to infinite mode.");
            Require(
                InvokeMockBackendLabel(prototypeNextActionBackend, "BuildPrototypeNextCheckActionText", "복기 재도전으로 실수 구간 보완") == "일반 런",
                "The profile checklist action label must send normal review retries to the normal run.");
            Require(
                InvokeMockBackendLabel(prototypeNextActionBackend, "BuildPrototypeNextCheckActionText", "완성 루프 기록 확인") == "기록 보기",
                "The profile checklist action label must send completed-loop verification to records.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음: 복기 연습으로 정확권 전환") == "연습",
                "The lobby Enter action must follow the highlighted review-practice target.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음: 복기 재도전으로 실수 구간 보완") == "일반",
                "The lobby Enter action must follow the highlighted normal retry target.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음: 무한 복기 재도전으로 목표 진행") == "무한",
                "The lobby Enter action must follow the highlighted infinite retry target.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음 확인: 보충 후 무한 재진입") == "무한",
                "The lobby Enter action must follow prototype refill/re-entry phrasing.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음 확인: 보충 런 클리어 후 새 조합 저장") == "무한",
                "The lobby Enter action must follow prototype direct refill-run phrasing.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음 확인: 저장 후보 저장/포기") == "저장",
                "The lobby Enter action must follow prototype save-candidate phrasing.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음 확인: 저장 조합으로 무한 시작") == "무한",
                "The lobby Enter action must follow prototype save-loadout infinite-start phrasing.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음 확인: 기록 보드 확인") == "기록",
                "The lobby Enter action must follow prototype record-board phrasing.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음 확인: 완성 루프 기록 확인") == "기록",
                "The lobby Enter action must open records for the completed prototype-loop checkpoint.");
            Require(
                InvokeAppStaticBool("ShouldMarkProfileSeenFromRecordRequest", "결과\n다음 확인: 완성 루프 기록 확인"),
                "Opening the completed-loop record shortcut must clear the profile NEW marker.");
            Require(
                !InvokeAppStaticBool("ShouldMarkProfileSeenFromRecordRequest", "결과\n다음 확인: 기록 보드 확인"),
                "Opening a regular record-board shortcut must not clear unrelated profile NEW markers.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음: 성장 보정에서 집중 Lv.2 적용") == "성장",
                "The lobby Enter action must follow the highlighted growth target.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "결과\n다음: 흔적에서 검증 순서 확인") == "흔적",
                "The lobby Enter action must follow the highlighted profile target.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "탈출 중단 기록 저장\n점수 125 / 거리 18.0m / 콤보 2") == "기록",
                "The lobby Enter action must open records after a normal result without an explicit next-action.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, "무한모드 종료 / 기록 저장\n복기: 무한 1구간") == "기록",
                "The lobby Enter action must open records after an infinite result without an explicit next-action.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, false, string.Empty) == "무한",
                "The lobby Enter action must open infinite preparation as the refill route when every save loadout is empty.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", true, true, true, false, "결과\n다음: 복기 연습으로 정확권 전환") == "저장",
                "A pending save candidate must keep the lobby Enter action on save selection before review shortcuts.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", true, true, true, false, "결과\n다음 확인: 보충 런 클리어 후 새 조합 저장") == "저장",
                "A pending save candidate must keep the lobby Enter action on save selection before refill shortcuts.");
            Require(
                InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", true, true, true, false, "결과\n다음 확인: 보충 후 무한 재진입") == "저장",
                "A pending save candidate must keep the lobby Enter action on save selection before empty-loadout re-entry shortcuts.");
            string infinitePrepReview = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(
                infiniteSnapshot.Detail
                + "\n복기: 무한 1구간 / 정확 0 -> 다음 구간 무실수"
                + "\n복기 목표: 다음 구간 무실수 / 직전 2 / 실패 실수 2"
                + "\n목표: 다음 구간 무실수 / 직전 2");
            Require(infinitePrepReview.Contains("무한 복기 재도전", StringComparison.Ordinal), "An infinite retry must carry a review-preparation prefix.");
            Require(infinitePrepReview.Contains("복기: 무한 1구간", StringComparison.Ordinal), "An infinite retry preparation must preserve the infinite review line.");
            Require(infinitePrepReview.Contains("복기 목표: 다음 구간 무실수 / 직전 2 / 실패 실수 2", StringComparison.Ordinal), "An infinite retry preparation must preserve the previous review target result.");
            Require(infinitePrepReview.Contains("목표: 다음 구간 무실수 / 직전 2", StringComparison.Ordinal), "An infinite retry preparation must preserve the next target.");
            string reopenedInfinitePrepReview = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(
                infinitePrepReview
                + "\n\n선택 슬롯: 슬롯 1"
                + "\n무한 시작을 누르면 사용 횟수 1회가 차감됩니다.");
            Require(
                reopenedInfinitePrepReview == infinitePrepReview,
                "An infinite review retry context must survive after being prepended to the infinite preparation screen.");
            string settlementRealReentryContext = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(
                "무한모드 종료 / 기록 저장"
                + "\n힌트: 정산 종료 기록 / 실제 생존 흐름은 다음 무한 런에서 확인"
                + "\n복기: 정산 종료 -> 실제 플레이로 무한 종료 기록 확인"
                + "\n목표: 실제 플레이로 무한 종료 기록 확인"
                + "\n다음: 정산 종료 확인 후 무한모드 실전 재개");
            Require(
                string.IsNullOrEmpty(settlementRealReentryContext),
                "A forced settlement real-reentry result must not reopen infinite mode with review-retry context.");
            string settlementRealReentryNotice = InvokeAppStaticLabel(
                "BuildInfinitePreparationEntryNoticeFromLobbyDetail",
                "무한모드 종료 / 기록 저장"
                + "\n힌트: 정산 종료 기록"
                + "\n다음: 정산 종료 확인 후 무한모드 실전 재개");
            Require(
                settlementRealReentryNotice.Contains("정산 종료 확인 완료", StringComparison.Ordinal)
                    && settlementRealReentryNotice.Contains("복기 목표 없이", StringComparison.Ordinal)
                    && settlementRealReentryNotice.Contains("실제 무한 런", StringComparison.Ordinal),
                "A forced settlement real-reentry result must show a preparation notice without creating a review-retry target.");
            string infiniteWeaknessDetail = infiniteSnapshot.Detail
                + "\n복기: 무한 1구간 / 숙이기 실수 1회"
                + "\n힌트: 다음 구간 정확권 전환 / 숙이기 실수 1회 / 기초연습 숙이기 단계"
                + "\n목표: 다음 구간 무실수 / 직전 2";
            string infinitePracticeContext = GreedLastApp.BuildPracticeOpeningContextFromLobbyDetail(infiniteWeaknessDetail);
            Require(
                infinitePracticeContext.Contains("무한 복기 연습", StringComparison.Ordinal)
                    && infinitePracticeContext.Contains("힌트:", StringComparison.Ordinal)
                    && infinitePracticeContext.Contains("목표: 다음 구간 무실수", StringComparison.Ordinal)
                    && infinitePracticeContext.Contains("기초연습 숙이기 단계", StringComparison.Ordinal),
                "An infinite result with an avoidance weakness must be able to open focused practice.");
            GreedLastStateSnapshot infiniteWeaknessPracticeSnapshot = BuildLobbyLabelSnapshot(
                infiniteWeaknessDetail,
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyPracticeButtonLabel", infiniteWeaknessPracticeSnapshot) == "숙이기 복기",
                "An infinite result with a slide weakness must label the practice button as slide review.");
            var infinitePracticeCore = new GreedLastRunCore();
            GreedLastRunSnapshot infinitePracticeSnapshot = default;
            infinitePracticeCore.SnapshotChanged += value => infinitePracticeSnapshot = value;
            infinitePracticeCore.EnterPractice(infinitePracticeContext);
            Require(
                infinitePracticeSnapshot.ChapterProgress == 1
                    && infinitePracticeSnapshot.JudgementText.Contains("클러치 상단→↓숙이기부터", StringComparison.Ordinal),
                "An infinite avoidance practice drill must start at the requested slide step.");
            Require(
                InvokeRuntimeUiLabel("BuildStartCountdownContext", infinitePracticeSnapshot).Contains("힌트:", StringComparison.Ordinal),
                "An infinite avoidance practice countdown must preserve the hint that caused the drill.");
            string refillRetryContext = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(
                "보충 런 실패\n탈출 실패 기록 저장\n재도전 목표: 저장 조합 보충");
            Require(refillRetryContext.Contains("보충 복기 재도전", StringComparison.Ordinal), "A refill retry must keep its refill-review prefix.");
            Require(refillRetryContext.Contains("재도전 목표: 저장 조합 보충", StringComparison.Ordinal), "A refill retry must keep the refill target.");
        }

        private static void VerifyDevToolButtonLabels(float now)
        {
            GreedLastStateSnapshot lobbySnapshot = BuildLobbyLabelSnapshot(
                "피라미드 입구",
                saveCandidateAvailable: false,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyFocusTestButtonLabel", lobbySnapshot) == "저장 생성",
                "The lobby focus test button must describe sample loadout injection.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbySaveUseTestButtonLabel", lobbySnapshot) == "저장 소모",
                "The lobby save-use test button must describe consuming a usable loadout.");
            Require(
                InvokeRuntimeUiLabel("BuildLobbyCandidateTestButtonLabel") == "후보 생성",
                "The lobby candidate test button must describe sample clear candidate injection.");

            GreedLastStateSnapshot candidateSnapshot = BuildLobbyLabelSnapshot(
                "탈출 성공 / 기록 저장",
                saveCandidateAvailable: true,
                infiniteUnlocked: true);
            Require(
                InvokeRuntimeUiLabel("BuildLobbyFocusTestButtonLabel", candidateSnapshot) == "추천 저장",
                "A pending save candidate must make the test save label read as a recommendation save.");

            GreedLastStateSnapshot emptyLoadoutSnapshot = BuildLobbyLabelSnapshot(
                "저장 조합이 비었습니다.",
                saveCandidateAvailable: false,
                infiniteUnlocked: true,
                hasUsableSaveSlot: false,
                hasOccupiedSaveSlot: false);
            Require(
                InvokeRuntimeUiLabel("BuildLobbySaveUseTestButtonLabel", emptyLoadoutSnapshot) == "보충 확인",
                "A lobby with no usable loadout must label the save-use test as refill verification.");
            GreedLastStateSnapshot freshLobbySnapshot = BuildLobbyLabelSnapshot(
                "피라미드 입구",
                saveCandidateAvailable: false,
                infiniteUnlocked: false,
                hasUsableSaveSlot: false,
                hasOccupiedSaveSlot: false,
                firstSaveCompleted: false,
                saveLoadoutUnlocked: false);
            Require(
                InvokeRuntimeUiLabel("BuildLobbySaveUseTestButtonLabel", freshLobbySnapshot) == "저장 없음",
                "A fresh lobby before the first save must not imply that refill verification is available.");

            Require(
                InvokeRuntimeUiLabel("BuildGrowthPointTestButtonLabel") == "포인트 +3",
                "The growth-board focus test button must describe adding test points.");
            Require(
                InvokeRuntimeUiLabel("BuildGrowthMaxTestButtonLabel") == "성장 MAX",
                "The growth-board threat-up test button must describe maxing growth.");
            Require(
                InvokeRuntimeUiLabel("BuildGrowthResetTestButtonLabel") == "초기화",
                "The growth-board threat-down test button must describe resetting growth.");

            var runCore = new GreedLastRunCore();
            GreedLastRunSnapshot runSnapshot = default;
            runCore.SnapshotChanged += value => runSnapshot = value;
            runCore.Enter("dev label run", refillRun: false);
            runCore.HandleInput(GreedLastRunChannel.Center, now + 0.01f);
            runCore.Tick(now + 2f);
            Require(
                InvokeRuntimeUiLabel("BuildRunFocusTestButtonLabel", runSnapshot) == "집중 발동",
                "An inactive normal run must label the focus test as activation.");
            runCore.FillFocusForTest();
            Require(
                InvokeRuntimeUiLabel("BuildRunFocusTestButtonLabel", runSnapshot) == "집중 리필",
                "An active focus state must label the focus test as a refill.");

            var infiniteCore = new GreedLastRunCore();
            GreedLastRunSnapshot infiniteSnapshot = default;
            infiniteCore.SnapshotChanged += value => infiniteSnapshot = value;
            infiniteCore.EnterInfiniteRun(BuildStoredGiftRecord("안정 루트", "안정 루트"));
            Require(
                InvokeRuntimeUiLabel("BuildRunThreatUpTestButtonLabel", infiniteSnapshot) == "위협 +1",
                "A low-threat infinite run must label the threat-up test as an increment.");
            Require(
                InvokeRuntimeUiLabel("BuildRunThreatDownTestButtonLabel", infiniteSnapshot) == "위협 MIN",
                "A minimum-threat infinite run must label the threat-down test as already at minimum.");
            infiniteCore.RaiseInfiniteThreatForTest();
            Require(
                infiniteSnapshot.JudgementText.Contains("테스트 위협 상승 - 위협 2/5 상승", StringComparison.Ordinal)
                    && infiniteSnapshot.JudgementText.Contains("위험폭 보통", StringComparison.Ordinal)
                    && infiniteSnapshot.JudgementText.Contains("정확+12", StringComparison.Ordinal)
                    && infiniteSnapshot.JudgementText.Contains("폭·보상 확인", StringComparison.Ordinal),
                "The threat-up test must explain the next readable danger width and precision reward. Actual: "
                    + infiniteSnapshot.JudgementText);
            for (int i = 0; i < 5; i += 1)
            {
                infiniteCore.RaiseInfiniteThreatForTest();
            }

            Require(
                InvokeRuntimeUiLabel("BuildRunThreatUpTestButtonLabel", infiniteSnapshot) == "위협 MAX",
                "A max-threat infinite run must label the threat-up test as capped.");
            Require(
                InvokeRuntimeUiLabel("BuildRunThreatDownTestButtonLabel", infiniteSnapshot) == "위협 -1",
                "A nonzero-threat infinite run must label the threat-down test as decrementing.");
            infiniteCore.LowerInfiniteThreatForTest();
            Require(
                infiniteSnapshot.JudgementText.Contains("테스트 위협 하강 - 위협 4/5 고압", StringComparison.Ordinal)
                    && infiniteSnapshot.JudgementText.Contains("위험폭 넓음", StringComparison.Ordinal)
                    && infiniteSnapshot.JudgementText.Contains("정확+36~46", StringComparison.Ordinal)
                    && infiniteSnapshot.JudgementText.Contains("폭·보상 확인", StringComparison.Ordinal),
                "The threat-down test must explain the reduced danger width and precision reward. Actual: "
                    + infiniteSnapshot.JudgementText);
        }

        private static void VerifyDevSectionShortcutTargets(float now)
        {
            var giftCore = new GreedLastRunCore();
            GreedLastRunSnapshot giftSnapshot = default;
            giftCore.SnapshotChanged += value => giftSnapshot = value;
            giftCore.Enter("gift section shortcut smoke", refillRun: false);
            giftCore.JumpToDevShortcut(GreedLastDevShortcut.GiftChoice, now);
            Require(
                giftSnapshot.ChoiceActive
                    && giftSnapshot.ChoiceKind == GreedLastChoiceKind.Gift
                    && giftSnapshot.ChapterIndex >= 1
                    && giftSnapshot.ChapterIndex <= 3
                    && giftSnapshot.JudgementText.Contains("기프트 선택", StringComparison.Ordinal),
                "F2 must jump to a chapter gift choice test section.");

            var relicCore = new GreedLastRunCore();
            GreedLastRunSnapshot relicSnapshot = default;
            relicCore.SnapshotChanged += value => relicSnapshot = value;
            relicCore.Enter("relic section shortcut smoke", refillRun: false);
            relicCore.JumpToDevShortcut(GreedLastDevShortcut.RelicChoice, now + 1f);
            Require(
                relicSnapshot.ChoiceActive
                    && relicSnapshot.ChoiceKind == GreedLastChoiceKind.Relic
                    && relicSnapshot.ChapterIndex == 2
                    && relicSnapshot.JudgementText.Contains("유물 선택", StringComparison.Ordinal),
                "F3 must jump to the relic choice test section.");

            var afterCore = new GreedLastRunCore();
            GreedLastRunSnapshot afterCoreSnapshot = default;
            afterCore.SnapshotChanged += value => afterCoreSnapshot = value;
            afterCore.Enter("after core shortcut smoke", refillRun: false);
            afterCore.JumpToDevShortcut(GreedLastDevShortcut.AfterCore, now + 2f);
            Require(
                afterCoreSnapshot.CoreRetrieved
                    && afterCoreSnapshot.Phase == GreedLastRunPhase.EscapeRunning
                    && afterCoreSnapshot.StartCountdownActive
                    && afterCoreSnapshot.ChapterProgress == 0
                    && afterCoreSnapshot.JudgementText.Contains("공명핵 회수 직후", StringComparison.Ordinal),
                "F4 must jump to the escape test immediately after core retrieval.");

            var beforeClear = new GreedLastRunCore();
            GreedLastRunSnapshot beforeClearSnapshot = default;
            beforeClear.SnapshotChanged += value => beforeClearSnapshot = value;
            beforeClear.Enter("before clear shortcut smoke", refillRun: false);
            beforeClear.JumpToDevShortcut(GreedLastDevShortcut.BeforeClear, now + 3f);
            Require(
                beforeClearSnapshot.CoreRetrieved
                    && beforeClearSnapshot.Phase == GreedLastRunPhase.EscapeRunning
                    && beforeClearSnapshot.StartCountdownActive
                    && beforeClearSnapshot.ChapterProgress == beforeClearSnapshot.ChapterTarget - 1
                    && beforeClearSnapshot.JudgementText.Contains("탈출 직전", StringComparison.Ordinal),
                "F5 must jump to the final escape trap before clear.");
        }

        private static GreedLastStateSnapshot BuildLobbyLabelSnapshot(
            string detail,
            bool saveCandidateAvailable,
            bool infiniteUnlocked,
            bool hasUsableSaveSlot = true,
            bool hasOccupiedSaveSlot = true,
            bool firstSaveCompleted = true,
            bool saveLoadoutUnlocked = true)
        {
            return new GreedLastStateSnapshot(
                GreedLastScreenState.LobbyReady,
                0,
                "피라미드 입구",
                detail,
                busy: false,
                coreActionsEnabled: true,
                firstSaveCompleted: firstSaveCompleted,
                saveLoadoutUnlocked: saveLoadoutUnlocked,
                infiniteUnlocked: infiniteUnlocked,
                growthPoints: 0,
                growthSurvivalLevel: 0,
                growthFocusLevel: 0,
                growthTimingLevel: 0,
                recommendedGrowthTrackIndex: -1,
                profileButtonSummary: "흔적",
                selectedSaveSlotIndex: 0,
                saveSlotLabels: new[] { "마지막 1회 / 안정형 / 보너스 A", "빈 슬롯", "빈 슬롯" },
                saveSlotUsable: new[] { hasUsableSaveSlot, false, false },
                saveSlotOccupied: new[] { hasOccupiedSaveSlot, false, false },
                saveSlotChoiceRequired: false,
                saveSlotOverwriteConfirmationPending: false,
                saveLoadoutSkipConfirmationPending: false,
                saveSlotDeleteConfirmationPending: false,
                saveSlotDetailViewActive: false,
                saveSlotRenameConfirmationPending: false,
                saveLoadoutCandidateAvailable: saveCandidateAvailable,
                recordBoardPageIndex: 0,
                retryVisible: false,
                blockReason: GreedLastConnectBlockReason.None);
        }

        private static GreedLastLobbySnapshot BuildAppLobbySnapshot(
            bool saveCandidateAvailable,
            bool infiniteUnlocked,
            bool hasUsableSaveSlot = true,
            bool hasOccupiedSaveSlot = true)
        {
            return new GreedLastLobbySnapshot(
                "test",
                firstSaveCompleted: true,
                saveLoadoutUnlocked: true,
                infiniteUnlocked: infiniteUnlocked,
                saveLoadoutCandidateAvailable: saveCandidateAvailable,
                growthPoints: 0,
                growthSurvivalLevel: 0,
                growthFocusLevel: 0,
                growthTimingLevel: 0,
                recommendedGrowthTrackIndex: -1,
                profileButtonSummary: "흔적",
                selectedSaveSlotIndex: 0,
                saveSlotLabels: new[] { "S1 테스트", "빈 슬롯", "빈 슬롯" },
                saveSlotUsable: new[] { hasUsableSaveSlot, false, false },
                saveSlotOccupied: new[] { hasOccupiedSaveSlot, false, false },
                auxiliaryWarning: false,
                message: string.Empty);
        }

        private static string InvokeRuntimeUiLabel(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastRuntimeUi).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Missing runtime UI label helper: " + methodName);
            return method.Invoke(null, args) as string ?? string.Empty;
        }

        private static string InvokeAppStaticLabel(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastApp).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Require(method != null, "Missing app static label helper: " + methodName);
            return method.Invoke(null, args) as string ?? string.Empty;
        }

        private static int InvokeAppStaticInt(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastApp).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Require(method != null, "Missing app static int helper: " + methodName);
            return (int)method.Invoke(null, args);
        }

        private static bool InvokeAppStaticBool(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastApp).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Require(method != null, "Missing app static bool helper: " + methodName);
            return (bool)method.Invoke(null, args);
        }

        private static Color32 InvokeRuntimeUiSnapshotColor(string methodName, GreedLastRunSnapshot snapshot)
        {
            MethodInfo method = typeof(GreedLastRuntimeUi).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(GreedLastRunSnapshot) },
                modifiers: null);
            Require(method != null, "Missing runtime UI color helper: " + methodName);
            return (Color32)method.Invoke(null, new object[] { snapshot });
        }

        private static bool InvokeRuntimeUiBool(string methodName, GreedLastRunSnapshot snapshot)
        {
            MethodInfo method = typeof(GreedLastRuntimeUi).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(GreedLastRunSnapshot) },
                modifiers: null);
            Require(method != null, "Missing runtime UI bool helper: " + methodName);
            return (bool)method.Invoke(null, new object[] { snapshot });
        }

        private static bool InvokeRuntimeUiStaticBool(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastRuntimeUi).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Missing runtime UI static bool helper: " + methodName);
            return (bool)method.Invoke(null, args);
        }

        private static bool RuntimePauseBackdropBlocksInput()
        {
            GameObject existingCamera = GameObject.Find("GreedLastCamera");
            GameObject existingEventSystem = GameObject.Find("GreedLastEventSystem");
            GameObject host = new GameObject("GreedLastRuntimeUiSmokeHost");
            GameObject canvasRoot = null;

            try
            {
                GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                Action action = null;
                Action<int> timingAction = null;
                Action<GreedLastDevShortcut> shortcutAction = null;
                Action<GreedLastRunChannel> channelAction = null;
                ui.Initialize(
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    action,
                    timingAction,
                    shortcutAction,
                    channelAction);

                FieldInfo backdropField = typeof(GreedLastRuntimeUi).GetField(
                    "pauseMenuBackdrop",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(backdropField != null, "Missing pause backdrop field.");
                var backdrop = backdropField.GetValue(ui) as Image;

                FieldInfo rootField = typeof(GreedLastRuntimeUi).GetField(
                    "rootPanel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(rootField != null, "Missing runtime UI root field.");
                var rootPanel = rootField.GetValue(ui) as GameObject;
                if (rootPanel != null)
                {
                    canvasRoot = rootPanel.transform.root.gameObject;
                }

                return backdrop != null && backdrop.raycastTarget;
            }
            finally
            {
                if (canvasRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvasRoot);
                }

                DestroySmokeCreatedObject("GreedLastCamera", existingCamera);
                DestroySmokeCreatedObject("GreedLastEventSystem", existingEventSystem);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void DestroySmokeCreatedObject(string name, GameObject existingBeforeSmoke)
        {
            if (existingBeforeSmoke != null)
            {
                return;
            }

            GameObject created = GameObject.Find(name);
            if (created != null)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }
        }

        private static float InvokeRuntimeUiFloat(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastRuntimeUi).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Missing runtime UI float helper: " + methodName);
            return (float)method.Invoke(null, args);
        }

        private static Vector4 InvokeRuntimeUiVector4(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastRuntimeUi).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Missing runtime UI vector helper: " + methodName);
            return (Vector4)method.Invoke(null, args);
        }

        private static float InvokeRunCoreStaticFloat(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastRunCore).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Missing run-core float helper: " + methodName);
            return (float)method.Invoke(null, args);
        }

        private static string InvokeMockBackendStaticLabel(string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastMockBackend).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Missing mock-backend label helper: " + methodName);
            return method.Invoke(null, args) as string ?? string.Empty;
        }

        private static string InvokeMockBackendLabel(GreedLastMockBackend backend, string methodName, params object[] args)
        {
            MethodInfo method = typeof(GreedLastMockBackend).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "Missing mock-backend instance label helper: " + methodName);
            return method.Invoke(backend, args) as string ?? string.Empty;
        }

        private static void VerifyRetryOpeningChoiceRecommendations()
        {
            string survivalRecommendation = BuildOpeningChoiceRecommendation(
                "복기 재도전\n탈출 실패 기록 저장\n재도전 목표: 1장 무실수 / 현재 1");
            Require(survivalRecommendation.Contains("추천: 안정 호흡", StringComparison.Ordinal), "A no-miss retry target must recommend the survival start gift.");
            Require(survivalRecommendation.Contains("복기 목표", StringComparison.Ordinal), "A retry start gift recommendation must explain that it comes from review.");
            string survivalHeader = BuildOpeningChoiceHeaderDetail(
                "복기 재도전\n탈출 실패 기록 저장\n재도전 목표: 1장 무실수 / 현재 1");
            Require(survivalHeader.Contains("복기 재도전", StringComparison.Ordinal), "A retry start choice must display the review retry context.");
            Require(survivalHeader.Contains("재도전 목표: 1장 무실수 / 현재 1", StringComparison.Ordinal), "A retry start choice must display the retry target.");
            string failedSurvivalRecommendation = BuildOpeningChoiceRecommendation(
                "복기 재도전\n복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1\n재도전 목표: 1장 무실수 / 현재 1");
            Require(failedSurvivalRecommendation.Contains("복기 실패 보완", StringComparison.Ordinal), "A failed review target must explain the recommended start gift as a recovery choice.");
            string failedSurvivalHeader = BuildOpeningChoiceHeaderDetail(
                "복기 재도전\n복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1\n재도전 목표: 1장 무실수 / 현재 1");
            Require(failedSurvivalHeader.Contains("복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1", StringComparison.Ordinal), "A retry start choice must display the previous review target result.");
            string resultOnlyRecommendation = BuildOpeningChoiceRecommendation(
                "복기 재도전\n복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1");
            Require(resultOnlyRecommendation.Contains("추천: 안정 호흡", StringComparison.Ordinal), "A retry context with only a review-target result must restore the target.");

            string timingRecommendation = BuildOpeningChoiceRecommendation(
                "복기 재도전\n탈출 중단 기록 저장\n재도전 목표: 1장 보정 0회 / 현재 1");
            Require(timingRecommendation.Contains("추천: 박자 감각", StringComparison.Ordinal), "An exact-conversion retry target must recommend the timing start gift.");
            string correctionMissRecommendation = BuildOpeningChoiceRecommendation(
                "복기 재도전\n복기 목표: 1장 보정 0회 / 현재 1 / 실패 실수 1\n재도전 목표: 1장 보정 0회 / 현재 1");
            Require(
                correctionMissRecommendation.Contains("추천: 안정 호흡", StringComparison.Ordinal),
                "A correction-free target that failed by miss must prioritize a survival start gift.");
            Require(
                correctionMissRecommendation.Contains("복기 실패 보완", StringComparison.Ordinal),
                "A correction-free target that failed by miss must explain that the recommendation is recovery-oriented.");

            string scoreRecommendation = BuildOpeningChoiceRecommendation(
                "복기 재도전\n탈출 중단 기록 저장\n재도전 목표: 800점 탈출 / 현재 620점");
            Require(scoreRecommendation.Contains("추천: 고점 본능", StringComparison.Ordinal), "A score retry target must recommend the score start gift.");

            var practiceCore = new GreedLastRunCore();
            GreedLastRunSnapshot practiceSnapshot = default;
            practiceCore.SnapshotChanged += value => practiceSnapshot = value;
            practiceCore.EnterPractice("복기 연습\n재도전 목표: 1장 무실수 / 현재 1");
            Require(practiceSnapshot.PracticeMode && practiceSnapshot.StartCountdownActive, "A review drill must still enter practice countdown.");
            Require(practiceSnapshot.JudgementText.Contains("복기 연습", StringComparison.Ordinal), "A review drill must preserve its review context in the practice opening text.");
            Require(practiceSnapshot.JudgementText.Contains("재도전 목표: 1장 무실수 / 현재 1", StringComparison.Ordinal), "A review drill must preserve the retry target in practice.");
            Require(practiceSnapshot.ReviewTargetText == "1장 무실수 / 현재 1", "A review drill must persist the target into its snapshot.");
            float reviewPracticeClock = Time.realtimeSinceStartup + 520f;
            StartBufferedPattern(practiceCore, ref practiceSnapshot, ref reviewPracticeClock);
            ResolveExactBundle(practiceCore, ref practiceSnapshot, ref reviewPracticeClock, "review practice target");
            Require(practiceSnapshot.Stopped, "A review drill exact bundle must stop at practice completion.");
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(practiceSnapshot).Contains("달성 무실수", StringComparison.Ordinal),
                "A completed review drill must keep its achieved target visible in the stopped HUD.");

            var infiniteCore = new GreedLastRunCore();
            GreedLastRunSnapshot infiniteSnapshot = default;
            infiniteCore.SnapshotChanged += value => infiniteSnapshot = value;
            infiniteCore.EnterInfiniteRun(
                BuildStoredGiftRecord("생존 축", "생존 축"),
                "무한 복기 재도전\n목표: 다음 구간 무실수 / 직전 2");
            Require(infiniteSnapshot.InfiniteMode && infiniteSnapshot.StartCountdownActive, "An infinite review retry must enter infinite countdown.");
            Require(infiniteSnapshot.ReviewTargetText == "다음 구간 무실수 / 직전 2", "An infinite review retry must persist its next target.");
            Require(GreedLastRuntimeUi.BuildReviewTargetHudLine(infiniteSnapshot).Contains("다음 구간 무실수", StringComparison.Ordinal), "An infinite review retry target must be available to the HUD.");
        }

        private static void VerifyReviewTargetFeedback(float now)
        {
            var noMissCore = new GreedLastRunCore();
            GreedLastRunSnapshot noMissSnapshot = default;
            noMissCore.SnapshotChanged += value => noMissSnapshot = value;
            float noMissClock = now;
            noMissCore.Enter("복기 재도전\n재도전 목표: 1장 무실수 / 현재 1");
            noMissCore.HandleInput(GreedLastRunChannel.Center, noMissClock);
            StartBufferedPattern(noMissCore, ref noMissSnapshot, ref noMissClock);
            GreedLastRunChannel wrongChannel = noMissSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            noMissCore.HandleInput(
                wrongChannel,
                noMissClock + noMissSnapshot.Pattern.CoreSeconds - noMissCore.InputTimingOffsetSeconds);
            Require(noMissSnapshot.LastResult == GreedLastJudgementResult.Miss, "The no-miss retry feedback test must create a real miss.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(noMissSnapshot).Contains("목표 실패 실수 1", StringComparison.Ordinal),
                "A broken no-miss retry target must show its failure in the HUD.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(noMissSnapshot).Contains("목표 실패: 실수 1", StringComparison.Ordinal),
                "A broken no-miss retry target must show immediate judgement feedback.");
            GreedLastRunAttemptRecord noMissAttempt = noMissCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(noMissAttempt.TimingProfile.Contains("복기 목표 1장 무실수 / 현재 1", StringComparison.Ordinal), "A no-miss retry target must be stored with its attempt timing profile.");
            Require(noMissAttempt.TimingProfile.Contains("복기 결과 실패 실수 1", StringComparison.Ordinal), "A broken no-miss retry target must settle into the attempt timing profile.");
            var noMissBackend = new GreedLastMockBackend();
            Require(noMissBackend.RegisterNormalAttempt(noMissAttempt), "A review-target attempt must register for a result message.");
            string noMissAttemptMessage = noMissBackend.BuildRunAttemptResultMessage(noMissAttempt);
            RequireTextLayoutBudget(noMissAttemptMessage, 16, 120, "no-miss review-target attempt result");
            Require(
                noMissAttemptMessage.Contains("복기 목표: 1장 무실수 / 현재 1 / 실패 실수 1", StringComparison.Ordinal),
                "A broken no-miss retry target must appear in the attempt result summary.");
            Require(
                noMissAttemptMessage.Contains("다음: 복기 재도전으로 실수 구간 보완", StringComparison.Ordinal),
                "A broken no-miss retry target must make the next action point back to target retry.");
            Require(
                noMissBackend.BuildProfileBoardText(0).Contains("복기 재도전으로 실수 구간 보완", StringComparison.Ordinal),
                "The profile board next action must prioritize retrying a broken review target.");

            var goodCore = new GreedLastRunCore();
            GreedLastRunSnapshot goodSnapshot = default;
            goodCore.SnapshotChanged += value => goodSnapshot = value;
            float goodClock = now + 20f;
            goodCore.Enter("복기 재도전\n재도전 목표: 1장 보정 0회 / 현재 1");
            goodCore.HandleInput(GreedLastRunChannel.Center, goodClock);
            StartBufferedPattern(goodCore, ref goodSnapshot, ref goodClock);
            float goodDelta = (goodSnapshot.Pattern.SuccessWindowSeconds + goodSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            goodCore.HandleInput(
                goodSnapshot.Pattern.Channel,
                goodClock + goodSnapshot.Pattern.CoreSeconds + goodDelta - goodCore.InputTimingOffsetSeconds);
            Require(goodSnapshot.LastResult == GreedLastJudgementResult.Good, "The correction-free retry feedback test must create a real Good judgement.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(goodSnapshot).Contains("목표 실패 보정 1", StringComparison.Ordinal),
                "A broken correction-free retry target must show the Good count in the HUD.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(goodSnapshot).Contains("목표 실패: 보정 1", StringComparison.Ordinal),
                "A broken correction-free retry target must show immediate judgement feedback.");
            Color32 targetFailureGoodColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", goodSnapshot);
            Require(
                targetFailureGoodColor.r >= 220 && targetFailureGoodColor.g <= 150 && targetFailureGoodColor.b <= 120,
                "A Good judgement that breaks a correction-free review target must use a failure-tinted feedback color.");
            GreedLastRunAttemptRecord goodAttempt = goodCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(goodAttempt.TimingProfile.Contains("복기 결과 실패 보정 1", StringComparison.Ordinal), "A broken correction-free retry target must settle into the attempt timing profile.");
            var goodBackend = new GreedLastMockBackend();
            Require(goodBackend.RegisterNormalAttempt(goodAttempt), "A correction-free review attempt must register for a result message.");
            string goodAttemptMessage = goodBackend.BuildRunAttemptResultMessage(goodAttempt);
            RequireTextLayoutBudget(goodAttemptMessage, 16, 120, "correction-free review-target attempt result");
            Require(
                goodAttemptMessage.Contains("복기 목표: 1장 보정 0회 / 현재 1 / 실패 보정 1", StringComparison.Ordinal),
                "A broken correction-free retry target must appear in the attempt result summary.");
            Require(
                goodAttemptMessage.Contains("다음: 복기 연습으로 정확권 전환", StringComparison.Ordinal),
                "A broken correction-free retry target must make the next action point back to practice.");
            Require(
                goodBackend.BuildProfileBoardText(0).Contains("복기 연습으로 정확권 전환", StringComparison.Ordinal),
                "The profile board next action must prioritize practicing a broken correction-free target.");
            var practiceFeedbackCore = new GreedLastRunCore();
            GreedLastRunSnapshot practiceFeedbackSnapshot = default;
            practiceFeedbackCore.SnapshotChanged += value => practiceFeedbackSnapshot = value;
            float practiceFeedbackClock = now + 30f;
            practiceFeedbackCore.EnterPractice("복기 연습\n재도전 목표: 1장 보정 0회 / 현재 1");
            StartBufferedPattern(practiceFeedbackCore, ref practiceFeedbackSnapshot, ref practiceFeedbackClock);
            float practiceGoodDelta = (practiceFeedbackSnapshot.Pattern.SuccessWindowSeconds + practiceFeedbackSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            practiceFeedbackCore.HandleInput(
                practiceFeedbackSnapshot.Pattern.Channel,
                practiceFeedbackClock + practiceFeedbackSnapshot.Pattern.CoreSeconds + practiceGoodDelta - practiceFeedbackCore.InputTimingOffsetSeconds);
            Require(practiceFeedbackSnapshot.LastResult == GreedLastJudgementResult.Good, "The review-practice feedback test must create a Good judgement.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", practiceFeedbackSnapshot).Contains("목표 실패: 보정 1", StringComparison.Ordinal),
                "Review-practice judgement feedback must include the target failure line.");

            var scoreCore = new GreedLastRunCore();
            GreedLastRunSnapshot scoreSnapshot = default;
            scoreCore.SnapshotChanged += value => scoreSnapshot = value;
            float scoreClock = now + 40f;
            scoreCore.Enter("복기 재도전\n재도전 목표: 800점 탈출 / 현재 620점");
            scoreCore.HandleInput(GreedLastRunChannel.Center, scoreClock);
            StartBufferedPattern(scoreCore, ref scoreSnapshot, ref scoreClock);
            scoreCore.HandleInput(
                scoreSnapshot.Pattern.Channel,
                scoreClock + scoreSnapshot.Pattern.CoreSeconds - scoreCore.InputTimingOffsetSeconds);
            Require(scoreSnapshot.LastResult == GreedLastJudgementResult.Success, "The score retry feedback test must create a real exact judgement.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(scoreSnapshot).Contains("진행 현재", StringComparison.Ordinal),
                "A score retry target must keep showing live progress after an exact judgement.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(scoreSnapshot).Contains("목표 진행:", StringComparison.Ordinal),
                "A score retry target must show immediate progress feedback.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(scoreSnapshot).Contains("목표까지", StringComparison.Ordinal),
                "A successful review judgement must coach the remaining score gap instead of using generic timing text.");
            GreedLastRunAttemptRecord scoreAttempt = scoreCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(scoreAttempt.TimingProfile.Contains("복기 결과 진행 ", StringComparison.Ordinal), "A score retry target must settle live progress into the attempt timing profile.");

            var achievedScoreCore = new GreedLastRunCore();
            GreedLastRunSnapshot achievedScoreSnapshot = default;
            achievedScoreCore.SnapshotChanged += value => achievedScoreSnapshot = value;
            float achievedScoreClock = now + 50f;
            achievedScoreCore.Enter("복기 재도전\n재도전 목표: 1점 탈출 / 현재 0점");
            achievedScoreCore.HandleInput(GreedLastRunChannel.Center, achievedScoreClock);
            StartBufferedPattern(achievedScoreCore, ref achievedScoreSnapshot, ref achievedScoreClock);
            achievedScoreCore.HandleInput(
                achievedScoreSnapshot.Pattern.Channel,
                achievedScoreClock + achievedScoreSnapshot.Pattern.CoreSeconds - achievedScoreCore.InputTimingOffsetSeconds);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(achievedScoreSnapshot).Contains("목표 달성", StringComparison.Ordinal),
                "A met score retry target must show achieved state in the live HUD.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(achievedScoreSnapshot).Contains("목표 달성", StringComparison.Ordinal),
                "A met score retry target must show achieved state in immediate feedback.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(achievedScoreSnapshot).Contains("점수 목표 달성", StringComparison.Ordinal),
                "A successful judgement that reaches the score target must confirm it in live coaching.");
            Color32 achievedScoreColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", achievedScoreSnapshot);
            Require(
                achievedScoreColor.g > achievedScoreColor.r && achievedScoreColor.g > achievedScoreColor.b,
                "The judgement that reaches a review target must use a distinct achievement color.");
            GreedLastRunAttemptRecord achievedScoreAttempt = achievedScoreCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(achievedScoreAttempt.TimingProfile.Contains("복기 결과 달성 ", StringComparison.Ordinal), "A score retry target must settle as achieved once its threshold is met.");

            var focusTargetCore = new GreedLastRunCore();
            GreedLastRunSnapshot focusTargetSnapshot = default;
            focusTargetCore.SnapshotChanged += value => focusTargetSnapshot = value;
            focusTargetCore.ConfigureGrowth(0, 3, 0);
            float focusTargetClock = now + 55f;
            focusTargetCore.Enter("복기 재도전\n재도전 목표: 집중 3 / 현재 2");
            Require(
                focusTargetSnapshot.FocusStateActive && focusTargetSnapshot.FocusStateActivationCount == 1,
                "A max-focus review test must begin with one real focus activation from growth.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(focusTargetSnapshot).Contains("목표 달성 집중 3", StringComparison.Ordinal),
                "A focus-value target must count an activated focus state as having reached max focus.");
            focusTargetCore.HandleInput(GreedLastRunChannel.Center, focusTargetClock);
            StartBufferedPattern(focusTargetCore, ref focusTargetSnapshot, ref focusTargetClock);
            focusTargetCore.HandleInput(
                focusTargetSnapshot.Pattern.Channel,
                focusTargetClock + focusTargetSnapshot.Pattern.CoreSeconds - focusTargetCore.InputTimingOffsetSeconds);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(focusTargetSnapshot).Contains("목표 달성: 집중 3", StringComparison.Ordinal),
                "An active focus state must keep a max-focus target achieved in immediate feedback.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(focusTargetSnapshot).Contains("집중 목표 달성", StringComparison.Ordinal),
                "A max-focus target must confirm achievement in live coaching.");
            GreedLastRunAttemptRecord focusTargetAttempt = focusTargetCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(
                focusTargetAttempt.TimingProfile.Contains("복기 결과 달성 집중 3", StringComparison.Ordinal),
                "A max-focus target must settle as achieved after focus converts into its active state.");

            var focusActivationCore = new GreedLastRunCore();
            GreedLastRunSnapshot focusActivationSnapshot = default;
            focusActivationCore.SnapshotChanged += value => focusActivationSnapshot = value;
            focusActivationCore.ConfigureGrowth(0, 3, 0);
            focusActivationCore.Enter("복기 재도전\n재도전 목표: 집중 발동 1회 / 현재 0회");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(focusActivationSnapshot).Contains("목표 달성 집중 발동 1회", StringComparison.Ordinal),
                "A focus-activation target must use activation count instead of the reset focus gauge.");
            float focusActivationClock = now + 58f;
            focusActivationCore.HandleInput(GreedLastRunChannel.Center, focusActivationClock);
            StartBufferedPattern(focusActivationCore, ref focusActivationSnapshot, ref focusActivationClock);
            focusActivationCore.HandleInput(
                focusActivationSnapshot.Pattern.Channel,
                focusActivationClock + focusActivationSnapshot.Pattern.CoreSeconds - focusActivationCore.InputTimingOffsetSeconds);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(focusActivationSnapshot).Contains("목표 달성: 집중 발동 1회", StringComparison.Ordinal),
                "A focus-activation target must keep using activation count in immediate feedback.");
            GreedLastRunAttemptRecord focusActivationAttempt = focusActivationCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(
                focusActivationAttempt.TimingProfile.Contains("복기 결과 달성 집중 발동 1회", StringComparison.Ordinal),
                "A focus-activation target must settle with its explicit activation unit.");

            var scopedPrecisionCore = new GreedLastRunCore();
            GreedLastRunSnapshot scopedPrecisionSnapshot = default;
            scopedPrecisionCore.SnapshotChanged += value => scopedPrecisionSnapshot = value;
            float scopedPrecisionClock = now + 60f;
            scopedPrecisionCore.Enter("복기 재도전\n재도전 목표: 1장 정박 3연속 / 현재 0");
            scopedPrecisionCore.HandleInput(GreedLastRunChannel.Center, scopedPrecisionClock);
            StartBufferedPattern(scopedPrecisionCore, ref scopedPrecisionSnapshot, ref scopedPrecisionClock);
            scopedPrecisionClock += scopedPrecisionSnapshot.Pattern.CoreSeconds - scopedPrecisionCore.InputTimingOffsetSeconds;
            scopedPrecisionCore.HandleInput(
                scopedPrecisionSnapshot.Pattern.Channel,
                scopedPrecisionClock);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(scopedPrecisionSnapshot).Contains("진행 정박 1", StringComparison.Ordinal),
                "A chapter-scoped precision target must keep showing progress before the real streak threshold.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(scopedPrecisionSnapshot).Contains("목표까지 정박 2회", StringComparison.Ordinal),
                "A precision review target must coach its exact remaining streak after each success.");

            for (int i = 0; i < 2; i += 1)
            {
                scopedPrecisionCore.RequestNextPattern(scopedPrecisionClock += 2f);
                scopedPrecisionClock += scopedPrecisionSnapshot.Pattern.CoreSeconds - scopedPrecisionCore.InputTimingOffsetSeconds;
                scopedPrecisionCore.HandleInput(scopedPrecisionSnapshot.Pattern.Channel, scopedPrecisionClock);
            }

            Require(scopedPrecisionSnapshot.MaxPrecisionStreak >= 3, "The precision review test must reach its three-exact target.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(scopedPrecisionSnapshot).Contains("목표 달성: 정박 3", StringComparison.Ordinal),
                "Reaching a precision target must show immediate achievement feedback.");
            Color32 achievedPrecisionColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", scopedPrecisionSnapshot);
            Require(
                achievedPrecisionColor.g > achievedPrecisionColor.r && achievedPrecisionColor.g > achievedPrecisionColor.b,
                "The exact judgement that reaches a precision target must use the achievement color.");

            Require(scopedPrecisionSnapshot.ChoiceKind == GreedLastChoiceKind.Node, "Completing the precision target chapter must open its node choice.");
            scopedPrecisionCore.HandleInput(GreedLastRunChannel.Left, scopedPrecisionClock += 0.2f);
            scopedPrecisionCore.HandleInput(GreedLastRunChannel.Center, scopedPrecisionClock += 0.2f);
            StartBufferedPattern(scopedPrecisionCore, ref scopedPrecisionSnapshot, ref scopedPrecisionClock);
            GreedLastRunChannel precisionWrongChannel = scopedPrecisionSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            scopedPrecisionClock += scopedPrecisionSnapshot.Pattern.CoreSeconds - scopedPrecisionCore.InputTimingOffsetSeconds;
            scopedPrecisionCore.HandleInput(precisionWrongChannel, scopedPrecisionClock);
            Require(
                scopedPrecisionSnapshot.PrecisionStreak == 0 && scopedPrecisionSnapshot.MaxPrecisionStreak >= 3,
                "A later miss must reset only the live precision streak, not its achieved best.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(scopedPrecisionSnapshot).Contains("목표 달성 정박 3", StringComparison.Ordinal),
                "A reached precision target must remain achieved in the HUD after a later miss.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(scopedPrecisionSnapshot).Contains("목표 달성 유지: 정박 3", StringComparison.Ordinal),
                "A later miss must describe a reached precision goal as retained instead of regressing to progress.");
            Color32 retainedPrecisionMissColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", scopedPrecisionSnapshot);
            Require(
                retainedPrecisionMissColor.r > retainedPrecisionMissColor.g,
                "A miss after reaching a precision target must keep its miss color even while the goal remains achieved.");
            GreedLastRunAttemptRecord scopedPrecisionAttempt = scopedPrecisionCore.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(
                scopedPrecisionAttempt.TimingProfile.Contains("복기 결과 달성 정박 3", StringComparison.Ordinal),
                "A chapter-scoped precision target must settle from its achieved best instead of the reset live streak.");

            var scoreShortcutCore = new GreedLastRunCore();
            GreedLastRunSnapshot scoreShortcutSnapshot = default;
            scoreShortcutCore.SnapshotChanged += value => scoreShortcutSnapshot = value;
            float scoreShortcutClock = now + 90f;
            scoreShortcutCore.Enter("복기 재도전\n재도전 목표: 800점 탈출 / 현재 620점");
            scoreShortcutCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, scoreShortcutClock);
            Require(
                scoreShortcutSnapshot.StartCountdownActive
                    && scoreShortcutSnapshot.Score == 799
                    && scoreShortcutSnapshot.ReviewTargetText.Contains("800점", StringComparison.Ordinal),
                "The review-target dev shortcut must preserve the target and prepare a score goal one point below threshold.");
            StartBufferedPattern(scoreShortcutCore, ref scoreShortcutSnapshot, ref scoreShortcutClock);
            scoreShortcutCore.HandleInput(
                scoreShortcutSnapshot.Pattern.Channel,
                scoreShortcutClock + scoreShortcutSnapshot.Pattern.CoreSeconds - scoreShortcutCore.InputTimingOffsetSeconds);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(scoreShortcutSnapshot).Contains("목표 달성", StringComparison.Ordinal),
                "One exact input after the review-target shortcut must expose score-goal achievement feedback.");

            var comboShortcutCore = new GreedLastRunCore();
            GreedLastRunSnapshot comboShortcutSnapshot = default;
            comboShortcutCore.SnapshotChanged += value => comboShortcutSnapshot = value;
            float comboShortcutClock = now + 95f;
            comboShortcutCore.Enter("복기 재도전\n재도전 목표: 최대 콤보 3 / 현재 1");
            comboShortcutCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, comboShortcutClock);
            Require(
                comboShortcutSnapshot.Combo == 2 && comboShortcutSnapshot.MaxCombo == 2,
                "The review-target shortcut must prepare a combo goal one hit below threshold.");
            StartBufferedPattern(comboShortcutCore, ref comboShortcutSnapshot, ref comboShortcutClock);
            comboShortcutClock += comboShortcutSnapshot.Pattern.CoreSeconds - comboShortcutCore.InputTimingOffsetSeconds;
            comboShortcutCore.HandleInput(comboShortcutSnapshot.Pattern.Channel, comboShortcutClock);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(comboShortcutSnapshot).Contains("목표 달성: 콤보", StringComparison.Ordinal),
                "One exact input after the combo shortcut must achieve its target.");
            comboShortcutCore.RequestNextPattern(comboShortcutClock += 2f);
            GreedLastRunChannel comboWrongChannel = comboShortcutSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            comboShortcutClock += comboShortcutSnapshot.Pattern.CoreSeconds - comboShortcutCore.InputTimingOffsetSeconds;
            comboShortcutCore.HandleInput(comboWrongChannel, comboShortcutClock);
            Require(
                comboShortcutSnapshot.Combo == 0 && comboShortcutSnapshot.MaxCombo >= 3,
                "A miss after reaching a combo target must reset only the live combo.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(comboShortcutSnapshot).Contains("목표 달성 콤보", StringComparison.Ordinal)
                    && GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(comboShortcutSnapshot).Contains("목표 달성 유지: 콤보", StringComparison.Ordinal),
                "A reached combo target must remain achieved after the live combo breaks.");
            Color32 retainedComboMissColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", comboShortcutSnapshot);
            Require(
                retainedComboMissColor.r > retainedComboMissColor.g,
                "A miss after reaching a combo target must keep its miss color while the best-combo goal remains achieved.");

            var terminalShortcutCore = new GreedLastRunCore();
            GreedLastRunSnapshot terminalShortcutSnapshot = default;
            terminalShortcutCore.SnapshotChanged += value => terminalShortcutSnapshot = value;
            float terminalShortcutClock = now + 100f;
            terminalShortcutCore.Enter("복기 재도전\n재도전 목표: 1장 무실수 / 현재 1");
            terminalShortcutCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, terminalShortcutClock);
            Require(
                terminalShortcutSnapshot.CoreRetrieved
                    && terminalShortcutSnapshot.Phase == GreedLastRunPhase.EscapeRunning
                    && terminalShortcutSnapshot.ChapterProgress == 2,
                "A terminal review goal shortcut must move to the final escape trap without introducing misses.");
            Require(
                InvokeRuntimeUiLabel("BuildReviewTargetShortcutLabel", terminalShortcutSnapshot) == "목표 정산",
                "A terminal review goal must label its shortcut as a target settlement jump.");
            StartBufferedPattern(terminalShortcutCore, ref terminalShortcutSnapshot, ref terminalShortcutClock);
            string terminalObjective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", terminalShortcutSnapshot);
            Require(
                terminalObjective.Contains("다음 통과로 탈출", StringComparison.Ordinal),
                "The objective line must announce that one more pass clears escape.");
            string terminalProgress = InvokeRuntimeUiLabel(
                "BuildEscapeProgressLabel",
                terminalShortcutSnapshot,
                terminalShortcutSnapshot.ChapterTarget);
            Require(
                terminalProgress.Contains("탈출 직전", StringComparison.Ordinal),
                "The progress label must show that escape is one pass away.");
            terminalShortcutCore.HandleInput(
                terminalShortcutSnapshot.Pattern.Channel,
                terminalShortcutClock + terminalShortcutSnapshot.Pattern.CoreSeconds - terminalShortcutCore.InputTimingOffsetSeconds);
            Require(
                terminalShortcutSnapshot.Stopped
                    && terminalShortcutSnapshot.EscapeSucceeded
                    && GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(terminalShortcutSnapshot).Contains("달성 무실수", StringComparison.Ordinal),
                "One exact input after a terminal target shortcut must reach clear settlement with the goal achieved.");
            Color32 terminalTargetAchievementColor = InvokeRuntimeUiSnapshotColor(
                "GetJudgementFeedbackColor",
                terminalShortcutSnapshot);
            Require(
                terminalTargetAchievementColor.g > terminalTargetAchievementColor.r,
                "A clean terminal review target must use the achievement color on its final successful snapshot.");

            var infiniteShortcutCore = new GreedLastRunCore();
            GreedLastRunSnapshot infiniteShortcutSnapshot = default;
            infiniteShortcutCore.SnapshotChanged += value => infiniteShortcutSnapshot = value;
            float infiniteShortcutClock = now + 110f;
            var infiniteHintGoalCore = new GreedLastRunCore();
            GreedLastRunSnapshot infiniteHintGoalSnapshot = default;
            infiniteHintGoalCore.SnapshotChanged += value => infiniteHintGoalSnapshot = value;
            infiniteHintGoalCore.EnterInfiniteRun(
                BuildStoredGiftRecord("안정 축", "생존 축"),
                "무한 복기 재도전\n복기: 무한 1구간 / 숙이기 실수 1회"
                    + "\n힌트: 다음 구간 정확권 전환 / 숙이기 실수 1회 / 기초연습 숙이기 단계"
                    + "\n목표: 다음 구간 무실수 / 직전 2");
            string infiniteHintGoalCountdown = InvokeRuntimeUiLabel("BuildStartCountdownContext", infiniteHintGoalSnapshot);
            Require(
                infiniteHintGoalCountdown.Contains("목표: 다음 구간 무실수", StringComparison.Ordinal)
                    && !infiniteHintGoalCountdown.Contains("힌트:", StringComparison.Ordinal),
                "A real infinite retry countdown must prioritize the target over the practice hint.");
            infiniteShortcutCore.EnterInfiniteRun(
                BuildStoredGiftRecord("안정 축", "생존 축"),
                "무한 복기 재도전\n목표: 400점 돌파 / 직전 320점");
            infiniteShortcutCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, infiniteShortcutClock);
            Require(
                infiniteShortcutSnapshot.InfiniteMode
                    && infiniteShortcutSnapshot.StartCountdownActive
                    && infiniteShortcutSnapshot.Score == 399,
                "The review-target shortcut must preserve infinite mode and prepare its score threshold in place.");
            Require(
                InvokeRuntimeUiLabel("BuildReviewTargetShortcutLabel", infiniteShortcutSnapshot) == "목표 직전",
                "A generic score target must label its shortcut as a target pre-check.");
            StartBufferedPattern(infiniteShortcutCore, ref infiniteShortcutSnapshot, ref infiniteShortcutClock);
            infiniteShortcutCore.HandleInput(
                infiniteShortcutSnapshot.Pattern.Channel,
                infiniteShortcutClock + infiniteShortcutSnapshot.Pattern.CoreSeconds - infiniteShortcutCore.InputTimingOffsetSeconds);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(infiniteShortcutSnapshot).Contains("목표 달성", StringComparison.Ordinal),
                "An infinite review-target shortcut must expose achievement feedback after one exact input.");

            var infiniteTerminalShortcutCore = new GreedLastRunCore();
            GreedLastRunSnapshot infiniteTerminalShortcutSnapshot = default;
            infiniteTerminalShortcutCore.SnapshotChanged += value => infiniteTerminalShortcutSnapshot = value;
            float infiniteTerminalShortcutClock = now + 120f;
            infiniteTerminalShortcutCore.EnterInfiniteRun(
                BuildStoredGiftRecord("안정 축", "생존 축"),
                "무한 복기 재도전\n목표: 다음 구간 무실수 / 직전 2");
            infiniteTerminalShortcutCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, infiniteTerminalShortcutClock);
            Require(
                infiniteTerminalShortcutSnapshot.InfiniteMode
                    && infiniteTerminalShortcutSnapshot.ChapterProgress == 2
                    && infiniteTerminalShortcutSnapshot.MissCount == 0,
                "An infinite terminal-goal shortcut must preserve infinite mode and prepare the final section trap cleanly.");
            StartBufferedPattern(infiniteTerminalShortcutCore, ref infiniteTerminalShortcutSnapshot, ref infiniteTerminalShortcutClock);
            string infiniteTerminalProgress = InvokeRuntimeUiLabel(
                "BuildInfiniteProgressLabel",
                infiniteTerminalShortcutSnapshot,
                infiniteTerminalShortcutSnapshot.ChapterTarget);
            Require(
                infiniteTerminalProgress.Contains("무한 정산 직전", StringComparison.Ordinal),
                "The infinite progress label must show that section settlement is one pass away.");
            infiniteTerminalShortcutCore.HandleInput(
                infiniteTerminalShortcutSnapshot.Pattern.Channel,
                infiniteTerminalShortcutClock + infiniteTerminalShortcutSnapshot.Pattern.CoreSeconds - infiniteTerminalShortcutCore.InputTimingOffsetSeconds);
            Require(
                infiniteTerminalShortcutSnapshot.InfiniteSectionsCleared >= 1 && !infiniteTerminalShortcutSnapshot.Stopped,
                "The infinite terminal-goal shortcut must complete a clean section and leave settlement to the existing stop action.");
            infiniteTerminalShortcutCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(infiniteTerminalShortcutSnapshot).Contains("달성 무실수", StringComparison.Ordinal),
                "Stopping after the infinite terminal-goal shortcut must settle the clean-section target as achieved.");

            var sectionTargetCore = new GreedLastRunCore();
            GreedLastRunSnapshot sectionTargetSnapshot = default;
            sectionTargetCore.SnapshotChanged += value => sectionTargetSnapshot = value;
            float sectionTargetClock = now + 130f;
            sectionTargetCore.EnterInfiniteRun(
                BuildStoredGiftRecord("안정 축", "생존 축"),
                "무한 복기 재도전\n목표: 3구간 생존 / 현재 2");
            sectionTargetCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, sectionTargetClock);
            Require(
                sectionTargetSnapshot.InfiniteSectionsCleared == 2 && sectionTargetSnapshot.ChapterProgress == 2,
                "A section-survival shortcut must prepare exactly one trap before its target section.");
            StartBufferedPattern(sectionTargetCore, ref sectionTargetSnapshot, ref sectionTargetClock);
            sectionTargetCore.HandleInput(
                sectionTargetSnapshot.Pattern.Channel,
                sectionTargetClock + sectionTargetSnapshot.Pattern.CoreSeconds - sectionTargetCore.InputTimingOffsetSeconds);
            Require(
                sectionTargetSnapshot.InfiniteSectionsCleared == 3
                    && GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(sectionTargetSnapshot).Contains("목표 달성: 3구간 생존", StringComparison.Ordinal),
                "Completing the prepared section must achieve a section-survival review target.");
            sectionTargetCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(sectionTargetSnapshot).Contains("달성 3구간 생존", StringComparison.Ordinal),
                "A section-survival target must remain achieved in infinite settlement.");

            var threatTargetCore = new GreedLastRunCore();
            GreedLastRunSnapshot threatTargetSnapshot = default;
            threatTargetCore.SnapshotChanged += value => threatTargetSnapshot = value;
            float threatTargetClock = now + 140f;
            threatTargetCore.EnterInfiniteRun(
                BuildStoredGiftRecord("안정 축", "생존 축"),
                "무한 복기 재도전\n목표: 위협 3 진입 / 현재 2");
            threatTargetCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, threatTargetClock);
            Require(
                threatTargetSnapshot.InfiniteThreatLevel == 2 && threatTargetSnapshot.ChapterProgress == 2,
                "A threat-entry shortcut must prepare the section boundary one threat below target.");
            string threatEntryProgress = InvokeRuntimeUiLabel(
                "BuildInfiniteProgressLabel",
                threatTargetSnapshot,
                threatTargetSnapshot.ChapterTarget);
            Require(
                threatEntryProgress.Contains("다음 위협↑", StringComparison.Ordinal),
                "A threat-entry boundary progress label must explicitly preview the next threat rise. Actual: "
                    + threatEntryProgress);
            Require(
                InvokeRuntimeUiLabel("BuildReviewTargetShortcutLabel", threatTargetSnapshot) == "목표 구간",
                "A threat-entry target must label its shortcut as a target section-boundary jump.");
            StartBufferedPattern(threatTargetCore, ref threatTargetSnapshot, ref threatTargetClock);
            threatTargetCore.HandleInput(
                threatTargetSnapshot.Pattern.Channel,
                threatTargetClock + threatTargetSnapshot.Pattern.CoreSeconds - threatTargetCore.InputTimingOffsetSeconds);
            Require(
                threatTargetSnapshot.InfiniteThreatLevel == 3
                    && threatTargetSnapshot.MaxInfiniteThreatLevel == 3
                    && GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(threatTargetSnapshot).Contains("목표 달성: 위협 3", StringComparison.Ordinal),
                "Completing the prepared section must achieve a threat-entry review target and retain its maximum threat.");
            Require(
                threatTargetSnapshot.JudgementText.Contains("위협 상승 - 위협 3/5 압박", StringComparison.Ordinal)
                    && threatTargetSnapshot.JudgementText.Contains("위험폭 확대", StringComparison.Ordinal)
                    && threatTargetSnapshot.JudgementText.Contains("정확+24~34", StringComparison.Ordinal),
                "Automatic infinite threat rise feedback must show the new width and reward range. Actual: "
                    + threatTargetSnapshot.JudgementText);
            threatTargetCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(threatTargetSnapshot).Contains("달성 위협 3", StringComparison.Ordinal),
                "A threat-entry target must settle from maximum reached threat.");

            var missCapTargetCore = new GreedLastRunCore();
            GreedLastRunSnapshot missCapTargetSnapshot = default;
            missCapTargetCore.SnapshotChanged += value => missCapTargetSnapshot = value;
            float missCapTargetClock = now + 150f;
            missCapTargetCore.EnterInfiniteRun(
                BuildStoredGiftRecord("안정 축", "생존 축"),
                "무한 복기 재도전\n목표: 실수 2 이하 / 현재 3");
            missCapTargetCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, missCapTargetClock);
            Require(
                missCapTargetSnapshot.MissCount == 2
                    && GreedLastRuntimeUi.BuildReviewTargetHudLine(missCapTargetSnapshot).Contains("목표 유지 실수 2/2", StringComparison.Ordinal),
                "A miss-cap shortcut must prepare the allowed boundary without declaring terminal achievement early.");
            StartBufferedPattern(missCapTargetCore, ref missCapTargetSnapshot, ref missCapTargetClock);
            missCapTargetCore.HandleInput(
                missCapTargetSnapshot.Pattern.Channel,
                missCapTargetClock + missCapTargetSnapshot.Pattern.CoreSeconds - missCapTargetCore.InputTimingOffsetSeconds);
            missCapTargetCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(missCapTargetSnapshot).Contains("달성 실수 2/2", StringComparison.Ordinal),
                "A miss-cap target must settle as achieved when the run ends at its allowed boundary.");

            var noInputTargetCore = new GreedLastRunCore();
            GreedLastRunSnapshot noInputTargetSnapshot = default;
            noInputTargetCore.SnapshotChanged += value => noInputTargetSnapshot = value;
            float noInputTargetClock = now + 160f;
            noInputTargetCore.EnterInfiniteRun(
                BuildStoredGiftRecord("변주 축", "집중 축"),
                "무한 복기 재도전\n목표: 변주 조합 입력 없음 0회 목표");
            noInputTargetCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, noInputTargetClock);
            Require(
                noInputTargetSnapshot.NoInputMissCount == 0 && noInputTargetSnapshot.ChapterProgress == 2,
                "An input-omission shortcut must prepare its zero-count boundary before section completion.");
            StartBufferedPattern(noInputTargetCore, ref noInputTargetSnapshot, ref noInputTargetClock);
            noInputTargetCore.HandleInput(
                noInputTargetSnapshot.Pattern.Channel,
                noInputTargetClock + noInputTargetSnapshot.Pattern.CoreSeconds - noInputTargetCore.InputTimingOffsetSeconds);
            noInputTargetCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(noInputTargetSnapshot).Contains("달성 입력 없음 0/0", StringComparison.Ordinal),
                "An input-omission target must settle as achieved after a clean prepared section.");
            Color32 noInputTargetAchievementColor = InvokeRuntimeUiSnapshotColor(
                "GetJudgementFeedbackColor",
                noInputTargetSnapshot);
            Require(
                noInputTargetAchievementColor.g > noInputTargetAchievementColor.r,
                "A clean terminal input-omission target must use the achievement color at settlement.");
            Require(
                noInputTargetCore.CreateInfiniteRunRecord().TimingProfile.Contains("복기 결과 달성 입력 없음 0/0", StringComparison.Ordinal),
                "A clean input-omission target must persist its zero-count result without adding generic record-board clutter.");

            var noInputFailureCore = new GreedLastRunCore();
            GreedLastRunSnapshot noInputFailureSnapshot = default;
            noInputFailureCore.SnapshotChanged += value => noInputFailureSnapshot = value;
            float noInputFailureClock = now + 170f;
            noInputFailureCore.EnterInfiniteRun(
                BuildStoredGiftRecord("변주 축", "집중 축"),
                "무한 복기 재도전\n목표: 변주 조합 입력 없음 0회 목표");
            StartBufferedPattern(noInputFailureCore, ref noInputFailureSnapshot, ref noInputFailureClock);
            noInputFailureCore.Tick(
                noInputFailureClock += noInputFailureSnapshot.Pattern.HitSeconds
                    + noInputFailureSnapshot.Pattern.GoodWindowSeconds
                    + 1f);
            Require(
                noInputFailureSnapshot.LastResult == GreedLastJudgementResult.Miss
                    && noInputFailureSnapshot.MissReason == "no_input"
                    && noInputFailureSnapshot.NoInputMissCount == 1,
                "Letting an infinite trap expire must count one explicit input omission.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", noInputFailureSnapshot).Contains("정답키", StringComparison.Ordinal),
                "An input-omission miss feedback must expose the target editor key.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(noInputFailureSnapshot).Contains("목표 실패 입력 없음 1/0", StringComparison.Ordinal)
                    && GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(noInputFailureSnapshot).Contains("목표 실패: 입력 없음 1/0", StringComparison.Ordinal),
                "An input omission beyond a zero-count target must fail in both HUD and immediate feedback.");
            noInputFailureCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(noInputFailureSnapshot).Contains("실패 입력 없음 1/0", StringComparison.Ordinal),
                "An input-omission target must retain failure in terminal settlement.");

            var timingApplyTargetCore = new GreedLastRunCore();
            GreedLastRunSnapshot timingApplyTargetSnapshot = default;
            timingApplyTargetCore.SnapshotChanged += value => timingApplyTargetSnapshot = value;
            float timingApplyTargetClock = now + 180f;
            timingApplyTargetCore.EnterInfiniteRun(
                BuildStoredGiftRecord("변주 축", "집중 축"),
                "무한 복기 재도전\n목표: 변주 조합 추천 보정 적용");
            timingApplyTargetCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, timingApplyTargetClock);
            Require(
                timingApplyTargetSnapshot.HasTimingRecommendation
                    && !timingApplyTargetSnapshot.TimingRecommendationApplied
                    && GreedLastRuntimeUi.BuildReviewTargetHudLine(timingApplyTargetSnapshot).Contains("적용 대기 추천", StringComparison.Ordinal),
                "A timing-apply shortcut must prepare an actionable recommendation without pre-completing the goal.");
            Require(
                InvokeRuntimeUiLabel("BuildReviewTargetShortcutLabel", timingApplyTargetSnapshot) == "목표 보정",
                "A timing-apply target must label its shortcut as target recommendation setup.");
            StartBufferedPattern(timingApplyTargetCore, ref timingApplyTargetSnapshot, ref timingApplyTargetClock);
            timingApplyTargetClock += timingApplyTargetSnapshot.Pattern.CoreSeconds - timingApplyTargetCore.InputTimingOffsetSeconds;
            timingApplyTargetCore.HandleInput(timingApplyTargetSnapshot.Pattern.Channel, timingApplyTargetClock);
            timingApplyTargetCore.ApplyRecommendedInputTimingOffset();
            Require(
                timingApplyTargetSnapshot.TimingRecommendationApplied
                    && timingApplyTargetSnapshot.AppliedTimingRecommendationMilliseconds != 0
                    && timingApplyTargetSnapshot.TimingRecommendationMilliseconds
                        == timingApplyTargetSnapshot.AppliedTimingRecommendationMilliseconds,
                "Applying a prepared recommendation must latch its applied value for the current run.");
            Require(
                GreedLastRuntimeUi.BuildReviewTargetHudLine(timingApplyTargetSnapshot).Contains("목표 달성 보정", StringComparison.Ordinal)
                    && GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(timingApplyTargetSnapshot).Contains("목표 달성: 추천 보정", StringComparison.Ordinal),
                "Applying the recommendation must complete the review target in HUD and immediate feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildTimingRecommendationButtonLabel", timingApplyTargetSnapshot).StartsWith("적용 완료", StringComparison.Ordinal),
                "The recommendation button must confirm its applied state instead of falling back to a vague stable label.");
            timingApplyTargetCore.RequestNextPattern(timingApplyTargetClock += 2f);
            float shiftedGoodDelta = (timingApplyTargetSnapshot.Pattern.SuccessWindowSeconds
                + timingApplyTargetSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            timingApplyTargetClock += timingApplyTargetSnapshot.Pattern.CoreSeconds
                + shiftedGoodDelta
                - timingApplyTargetCore.InputTimingOffsetSeconds;
            timingApplyTargetCore.HandleInput(timingApplyTargetSnapshot.Pattern.Channel, timingApplyTargetClock);
            Require(
                !timingApplyTargetSnapshot.TimingRecommendationApplied,
                "A newly shifted timing sample must reopen an applied recommendation when the target moves beyond tolerance.");
            timingApplyTargetCore.ApplyRecommendedInputTimingOffset();
            Require(
                timingApplyTargetSnapshot.TimingRecommendationApplied,
                "Applying the shifted recommendation must complete the timing target again.");
            timingApplyTargetCore.AdjustInputTimingOffset(0.04f);
            Require(
                !timingApplyTargetSnapshot.TimingRecommendationApplied
                    && GreedLastRuntimeUi.BuildReviewTargetHudLine(timingApplyTargetSnapshot).Contains("적용 대기 추천", StringComparison.Ordinal),
                "A manual timing adjustment after recommendation must reopen the apply target.");
            timingApplyTargetCore.ApplyRecommendedInputTimingOffset();
            Require(
                timingApplyTargetSnapshot.TimingRecommendationApplied,
                "Reapplying the recommendation after a manual adjustment must complete the target again.");
            timingApplyTargetCore.ForceStopInfiniteRunForTest();
            Require(
                GreedLastRuntimeUi.BuildStoppedReviewTargetResultLine(timingApplyTargetSnapshot).Contains("달성 추천 보정", StringComparison.Ordinal)
                    && timingApplyTargetCore.CreateInfiniteRunRecord().TimingProfile.Contains("추천 보정 적용", StringComparison.Ordinal),
                "A timing-apply target must preserve its applied value in settlement analytics.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "3구간 생존 / 현재 2 / 달성 3구간 생존",
                    true) == "4구간 생존",
                "An achieved section-survival target must advance by one section.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "위협 3 진입 / 현재 2 / 달성 위협 3",
                    true) == "위협 4 진입",
                "An achieved threat-entry target must advance by one threat level.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "위협 5 진입 / 현재 4 / 달성 위협 5",
                    true) == "3구간 생존",
                "A max-threat target must advance into survival instead of repeating threat five.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "실수 2 이하 / 현재 3 / 달성 실수 2/2",
                    true) == "실수 1 이하",
                "An achieved miss-cap target must tighten its allowed miss count.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "실수 0 이하 / 현재 1 / 달성 실수 0/0",
                    true) == "다음 구간 무실수",
                "A zero-miss cap must advance into a clean-section goal instead of repeating itself.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "다음 구간 무실수 / 직전 2 / 달성 무실수",
                    true) == "다음 구간 보정 0회",
                "An achieved infinite clean-section target must advance into a correction-free section target.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "변주 조합 입력 없음 0회 목표 / 달성 입력 없음 0/0",
                    true) == "다음 구간 무실수",
                "An achieved input-omission target must advance into a clean-section goal.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "변주 조합 추천 보정 적용 / 달성 추천 보정 -60ms",
                    true) == "다음 구간 보정 0회",
                "An achieved timing-apply target must advance into a correction-free section goal.");
            var threatReadTargetRecord = new GreedLastInfiniteRunRecord(
                true,
                820,
                110f,
                2,
                2,
                string.Empty,
                string.Empty,
                4,
                0,
                0,
                4,
                "manual_stop",
                "평균 정확 / 정박 최고 3 / 집중 발동 1회"
                    + " / 최근 구간 무한 2구간 / 최근 구간 정확 3회 / 최근 구간 보정 0회 / 최근 구간 실수 0회");
            string threatReadTargetText = InvokeMockBackendStaticLabel("BuildInfiniteRunNextTargetText", threatReadTargetRecord);
            Require(
                threatReadTargetText == "위협 3 진입 / 현재 2 / 폭·보상 확인",
                "An infinite threat-entry next target must remind the player to inspect threat width and reward. Actual: "
                    + threatReadTargetText);
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    threatReadTargetText + " / 달성 위협 3",
                    true) == "위협 4 진입",
                "The threat width/reward hint must not break the advanced threat-entry ladder.");
            var peakInfiniteRecord = new GreedLastInfiniteRunRecord(
                true,
                1650,
                240f,
                5,
                5,
                "테스트 조합",
                string.Empty,
                15,
                0,
                0,
                12,
                "manual_stop",
                "평균 정확 / 정박 최고 5 / 집중 발동 1회"
                    + " / 최근 구간 무한 5구간 / 최근 구간 정확 3회 / 최근 구간 보정 0회 / 최근 구간 실수 0회");
            Require(
                InvokeMockBackendStaticLabel("BuildInfiniteRunNextTargetText", peakInfiniteRecord)
                    == "무한 2000점 갱신 / 현재 1650점",
                "A fully established infinite record must receive a numeric next-score milestone instead of an unmeasurable best-record label.");
            Require(
                InvokeMockBackendStaticLabel(
                    "BuildAdvancedReviewTargetText",
                    "무한 2000점 갱신 / 현재 1650점 / 달성 2050점",
                    true) == "2200점 갱신",
                "An achieved infinite score milestone must retain infinite-record wording when advanced.");

            var peakScoreShortcutCore = new GreedLastRunCore();
            GreedLastRunSnapshot peakScoreShortcutSnapshot = default;
            peakScoreShortcutCore.SnapshotChanged += value => peakScoreShortcutSnapshot = value;
            float peakScoreShortcutClock = now + 190f;
            peakScoreShortcutCore.EnterInfiniteRun(
                BuildStoredGiftRecord("고점 축", "연쇄 축"),
                "무한 복기 재도전\n목표: 무한 2000점 갱신 / 현재 1650점");
            peakScoreShortcutCore.JumpToDevShortcut(GreedLastDevShortcut.ReviewTargetCheck, peakScoreShortcutClock);
            Require(
                peakScoreShortcutSnapshot.Score == 1999,
                "The target shortcut must prepare a numeric infinite score milestone one point below completion.");
            StartBufferedPattern(peakScoreShortcutCore, ref peakScoreShortcutSnapshot, ref peakScoreShortcutClock);
            peakScoreShortcutCore.HandleInput(
                peakScoreShortcutSnapshot.Pattern.Channel,
                peakScoreShortcutClock + peakScoreShortcutSnapshot.Pattern.CoreSeconds - peakScoreShortcutCore.InputTimingOffsetSeconds);
            Require(
                GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(peakScoreShortcutSnapshot).Contains("목표 달성", StringComparison.Ordinal),
                "The numeric infinite best-score milestone must complete through normal live target feedback.");
            string advancedSectionContext = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(
                "무한모드 종료 / 기록 저장"
                + "\n복기 목표: 3구간 생존 / 현재 2 / 달성 3구간 생존"
                + "\n목표: 3구간 생존 / 현재 2");
            Require(
                advancedSectionContext.Contains("목표: 4구간 생존", StringComparison.Ordinal),
                "An achieved section-survival result must seed the advanced target into infinite preparation.");

            var achievedTargetBackend = new GreedLastMockBackend();
            var achievedNoMissAttempt = new GreedLastRunAttemptRecord(
                true,
                GreedLastRunAttemptOutcome.Abandoned,
                120,
                3,
                1,
                0,
                1,
                1,
                0,
                0,
                12f,
                1,
                1,
                3,
                false,
                string.Empty,
                "테스트 루트",
                "타이밍 기록 없음 / 추천 샘플 0/4"
                    + " / 정박 최고 1"
                    + " / 1장 정확 1회 / 1장 보정 0회 / 1장 실수 0회"
                    + " / 복기 목표 1장 무실수 / 현재 1 / 복기 결과 달성 무실수");
            Require(achievedTargetBackend.RegisterNormalAttempt(achievedNoMissAttempt), "An achieved review-target attempt must register for next-target coaching.");
            string achievedNoMissMessage = achievedTargetBackend.BuildRunAttemptResultMessage(achievedNoMissAttempt);
            RequireTextLayoutBudget(achievedNoMissMessage, 16, 120, "achieved review-target attempt result");
            Require(
                achievedNoMissMessage.Contains("다음: 복기 재도전으로 목표 상향 - 1장 보정 0회", StringComparison.Ordinal),
                "An achieved no-miss review target must advance to a correction-free next target.");
            Require(
                achievedTargetBackend.BuildProfileBoardText(0).Contains("목표 상향 - 1장 보정 0회", StringComparison.Ordinal),
                "The profile board must surface the advanced target after a review goal is achieved.");
            string achievedNoMissRetryContext = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(achievedNoMissMessage);
            Require(
                achievedNoMissRetryContext.Contains("재도전 목표: 1장 보정 0회", StringComparison.Ordinal),
                "An achieved review-target result must seed the next retry with its advanced target.");
            string achievedNoMissRecommendation = BuildOpeningChoiceRecommendation(achievedNoMissRetryContext);
            Require(
                achievedNoMissRecommendation.Contains("추천: 박자 감각", StringComparison.Ordinal),
                "An advanced correction-free retry target must drive the start-gift recommendation.");
            Require(
                achievedNoMissRecommendation.Contains("목표 상향", StringComparison.Ordinal),
                "An achieved review-target retry recommendation must explain that the goal was advanced.");

            var achievedCorrectionBackend = new GreedLastMockBackend();
            var achievedCorrectionAttempt = new GreedLastRunAttemptRecord(
                true,
                GreedLastRunAttemptOutcome.Abandoned,
                160,
                3,
                2,
                0,
                2,
                2,
                0,
                0,
                18f,
                1,
                2,
                3,
                false,
                string.Empty,
                "테스트 루트",
                "평균 0.00초 / 오차 0ms / 빠름 0 늦음 0 / 정박 최고 2"
                    + " / 1장 정확 2회 / 1장 보정 0회 / 1장 실수 0회"
                    + " / 복기 목표 1장 보정 0회 / 현재 1 / 복기 결과 달성 보정 0");
            Require(achievedCorrectionBackend.RegisterNormalAttempt(achievedCorrectionAttempt), "An achieved correction-free target must register for next-target coaching.");
            string achievedCorrectionMessage = achievedCorrectionBackend.BuildRunAttemptResultMessage(achievedCorrectionAttempt);
            Require(
                achievedCorrectionMessage.Contains("목표 상향 - 정박 3연속", StringComparison.Ordinal),
                "An achieved correction-free review target must advance to a precision-streak target.");
            string achievedCorrectionContext = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(achievedCorrectionMessage);
            Require(
                achievedCorrectionContext.Contains("재도전 목표: 정박 3연속", StringComparison.Ordinal),
                "An achieved correction-free result must seed the next retry with a precision-streak target.");

            var achievedScopedScoreBackend = new GreedLastMockBackend();
            var achievedScopedScoreAttempt = new GreedLastRunAttemptRecord(
                true,
                GreedLastRunAttemptOutcome.Abandoned,
                840,
                3,
                5,
                0,
                5,
                5,
                0,
                0,
                42f,
                4,
                2,
                3,
                true,
                string.Empty,
                "테스트 루트",
                "평균 0.00초 / 오차 0ms / 빠름 0 늦음 0 / 정박 최고 5"
                    + " / 복기 목표 1장 800점 탈출 / 현재 620점 / 복기 결과 달성 840점");
            Require(achievedScopedScoreBackend.RegisterNormalAttempt(achievedScopedScoreAttempt), "An achieved chapter-scoped score target must register.");
            string achievedScopedScoreMessage = achievedScopedScoreBackend.BuildRunAttemptResultMessage(achievedScopedScoreAttempt);
            Require(
                achievedScopedScoreMessage.Contains("목표 상향 - 1000점 탈출", StringComparison.Ordinal),
                "An achieved chapter-scoped score target must use the score number, not the chapter number, for escalation.");

            var achievedInfiniteBackend = new GreedLastMockBackend();
            var achievedInfiniteRecord = new GreedLastInfiniteRunRecord(
                true,
                400,
                80f,
                2,
                2,
                "S1 안정",
                "안정형",
                3,
                0,
                0,
                3,
                "test_stop",
                "최근 구간 무한 2구간"
                    + " / 최근 구간 진행 3/3"
                    + " / 최근 구간 정확 3회 / 최근 구간 보정 0회 / 최근 구간 실수 0회"
                    + " / 복기 목표 다음 구간 무실수 / 직전 2 / 복기 결과 달성 무실수");
            Require(achievedInfiniteBackend.RegisterInfiniteRun(achievedInfiniteRecord), "An achieved infinite review target must register.");
            string achievedInfiniteMessage = achievedInfiniteBackend.BuildInfiniteRunResultMessage(achievedInfiniteRecord);
            RequireTextLayoutBudget(achievedInfiniteMessage, 18, 120, "achieved infinite review-target result");
            Require(
                achievedInfiniteMessage.Contains("다음: 무한 복기 재도전으로 목표 상향 - 다음 구간 보정 0회", StringComparison.Ordinal),
                "An achieved infinite no-miss target must advance to a correction-free section target.");
            string achievedInfiniteContext = GreedLastApp.BuildInfinitePreparationReviewContextFromLobbyDetail(achievedInfiniteMessage);
            Require(
                achievedInfiniteContext.Contains("목표: 다음 구간 보정 0회", StringComparison.Ordinal),
                "An achieved infinite review-target result must seed the next infinite retry with its advanced target.");
        }

        private static string BuildOpeningChoiceRecommendation(string openingContext)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter(openingContext);
            Require(snapshot.ChoiceActive && snapshot.ChoiceKind == GreedLastChoiceKind.StartGift, "A retry context must begin at the start-gift choice.");
            Require(!string.IsNullOrEmpty(snapshot.ReviewTargetText), "A retry context must persist its target into the run snapshot.");
            Require(GreedLastRuntimeUi.BuildReviewTargetHudLine(snapshot).Contains(snapshot.ReviewTargetText, StringComparison.Ordinal), "A retry target must be available to the run HUD.");
            if (snapshot.ReviewTargetText.Contains("무실수", StringComparison.Ordinal))
            {
                Require(GreedLastRuntimeUi.BuildReviewTargetHudLine(snapshot).Contains("유지", StringComparison.Ordinal), "A no-miss retry target must report that it is still maintained.");
            }

            if (snapshot.ReviewTargetText.Contains("점", StringComparison.Ordinal))
            {
                Require(GreedLastRuntimeUi.BuildReviewTargetHudLine(snapshot).Contains("현재 0점", StringComparison.Ordinal), "A score retry target must report the current score in the HUD.");
            }

            return GreedLastRuntimeUi.BuildChoiceRecommendationLine(snapshot);
        }

        private static string BuildOpeningChoiceHeaderDetail(string openingContext)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter(openingContext);
            Require(snapshot.ChoiceActive && snapshot.ChoiceKind == GreedLastChoiceKind.StartGift, "A retry context must begin at the start-gift choice.");
            return InvokeRuntimeUiLabel("BuildChoiceHeaderDetail", snapshot);
        }

        private static void VerifyRoutePayoffContracts(float now)
        {
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "안정 루트")
                    .Contains("Good 체력+" + GreedLastRunCore.StableRouteGoodHealthBonus, StringComparison.Ordinal),
                "The stable route choice must preview its ongoing recovery contract.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "안정 루트")
                    .Contains("정-압-정", StringComparison.Ordinal),
                "The stable route choice must preview its authored timing cadence.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "안정 루트")
                    .Contains("점-숙-틀", StringComparison.Ordinal),
                "The stable route choice must preview its jump-slide-twist action flow.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "변주 루트")
                    .Contains("엇박 집중+" + GreedLastRunCore.VariationRouteOffbeatFocusBonus, StringComparison.Ordinal),
                "The variation route choice must preview its ongoing focus contract.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "변주 루트")
                    .Contains("엇-정-엇", StringComparison.Ordinal),
                "The variation route choice must preview its authored timing cadence.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "변주 루트")
                    .Contains("틀-숙-점", StringComparison.Ordinal),
                "The variation route choice must preview its twist-slide-jump action flow.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "압박 루트")
                    .Contains("Clutch +" + GreedLastRunCore.PressureRouteClutchScoreBonus, StringComparison.Ordinal),
                "The pressure route choice must preview its ongoing score contract.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "압박 루트")
                    .Contains("압-엇-압", StringComparison.Ordinal),
                "The pressure route choice must preview its authored timing cadence.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceEffectHint", GreedLastChoiceKind.Node, "압박 루트")
                    .Contains("숙-틀-점", StringComparison.Ordinal),
                "The pressure route choice must preview its slide-twist-jump action flow.");
            VerifyStableRouteRecovery(now);
            VerifyVariationRouteFlow(now + 30f);
            VerifyPressureRouteCounter(now + 60f);
            VerifyRouteCycleDevShortcut(now + 90f);
            VerifyAvoidanceCycleDevShortcut(now + 105f);
            VerifyFinalValidationTempoContract();
            VerifyRouteBundleContracts(now + 120f);
            VerifyRouteBundleBreakFeedback(now + 210f);
            VerifyFinalCadenceGuidance(now + 270f);
        }

        private static void VerifyRouteBundleContracts(float now)
        {
            VerifyRouteBundleContract(
                now,
                1,
                "안정 루트",
                new[]
                {
                    GreedLastTimingType.Perfect,
                    GreedLastTimingType.Clutch,
                    GreedLastTimingType.Perfect,
                },
                new[]
                {
                    GreedLastAvoidanceType.Jump,
                    GreedLastAvoidanceType.Slide,
                    GreedLastAvoidanceType.Twist,
                });
            VerifyRouteBundleContract(
                now + 30f,
                2,
                "변주 루트",
                new[]
                {
                    GreedLastTimingType.Offbeat,
                    GreedLastTimingType.Perfect,
                    GreedLastTimingType.Offbeat,
                },
                new[]
                {
                    GreedLastAvoidanceType.Twist,
                    GreedLastAvoidanceType.Slide,
                    GreedLastAvoidanceType.Jump,
                });
            VerifyRouteBundleContract(
                now + 60f,
                3,
                "압박 루트",
                new[]
                {
                    GreedLastTimingType.Clutch,
                    GreedLastTimingType.Offbeat,
                    GreedLastTimingType.Clutch,
                },
                new[]
                {
                    GreedLastAvoidanceType.Slide,
                    GreedLastAvoidanceType.Twist,
                    GreedLastAvoidanceType.Jump,
                });
        }

        private static void VerifyRouteBundleContract(
            float now,
            int routeCycleCount,
            string expectedRoute,
            GreedLastTimingType[] expectedTimings,
            GreedLastAvoidanceType[] expectedAvoidances)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter(expectedRoute + " bundle smoke", refillRun: false);
            for (int i = 0; i < routeCycleCount; i += 1)
            {
                core.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, clock += 0.1f);
            }

            Require(snapshot.ActiveRouteProfile == expectedRoute, expectedRoute + " shortcut must prepare the expected route.");
            int healthBeforeBundle = snapshot.Health;
            int scoreBeforeBundle = snapshot.Score;
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(
                InvokeRuntimeUiLabel("BuildRouteCadencePipState", snapshot, 0) == "active"
                    && InvokeRuntimeUiLabel("BuildRouteCadencePipState", snapshot, 1) == "pending",
                expectedRoute + " cadence rail must begin with one active and two pending steps.");
            for (int i = 0; i < expectedTimings.Length; i += 1)
            {
                if (i > 0)
                {
                    core.RequestNextPattern(clock += 2f);
                }

                Require(snapshot.ActivePattern, expectedRoute + " bundle must expose all three route traps.");
                Require(
                    snapshot.Pattern.TimingType == expectedTimings[i],
                    expectedRoute + " bundle timing step " + (i + 1) + " must match its authored cadence.");
                Require(
                    snapshot.Pattern.AvoidanceType == expectedAvoidances[i],
                    expectedRoute + " bundle avoidance step " + (i + 1) + " must match its authored action flow.");
                string cadenceCue = InvokeRuntimeUiLabel("BuildRouteCadenceCue", snapshot);
                Require(
                    cadenceCue.Replace("[", string.Empty).Replace("]", string.Empty)
                        .Contains(BuildExpectedRouteCadence(expectedRoute), StringComparison.Ordinal),
                    expectedRoute + " live detail must expose its full three-beat cadence.");
                Require(
                    cadenceCue.Contains("[" + BuildCadenceToken(expectedTimings[i]) + "]", StringComparison.Ordinal),
                    expectedRoute + " live detail must highlight the currently active cadence step.");
                Require(
                    cadenceCue.Contains("[" + BuildAvoidanceToken(expectedAvoidances[i]) + "]", StringComparison.Ordinal),
                    expectedRoute + " live detail must highlight the current authored avoidance step.");
                if (i == 2)
                {
                    string objective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", snapshot);
                    Require(
                        objective.Contains(BuildExpectedRouteBundleObjective(expectedRoute), StringComparison.Ordinal)
                            && objective.Contains("다음 통과로 선택지", StringComparison.Ordinal),
                        expectedRoute + " final trap must preview both its route finisher and chapter transition.");
                }

                float exactAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(snapshot.Pattern.Channel, exactAt);
                clock = exactAt;
                Require(snapshot.LastResult == GreedLastJudgementResult.Success, expectedRoute + " bundle inputs must stay exact.");
                if (i < 2)
                {
                    Require(
                        InvokeRuntimeUiLabel("BuildRouteCadencePipState", snapshot, i) == "done"
                            && InvokeRuntimeUiLabel("BuildRouteCadencePipState", snapshot, i + 1) == "active",
                        expectedRoute + " cadence rail must advance its completed and active pips after exact input.");
                    Require(
                        InvokeRuntimeUiLabel("BuildRunGuideLine", snapshot)
                            .Contains(BuildExpectedRouteBundleProgress(expectedRoute) + " " + (i + 1) + "/3", StringComparison.Ordinal),
                        expectedRoute + " live status must expose its clean bundle progress.");
                    string stepFeedback = InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot);
                    Require(
                        stepFeedback.Contains("경로 " + expectedRoute.Replace(" 루트", string.Empty) + " " + (i + 1) + "/3", StringComparison.Ordinal),
                        expectedRoute + " judgement feedback must preserve its clean route step.");
                    Require(
                        stepFeedback.Contains(
                            i == 0
                                ? "다음 " + BuildCadenceToken(expectedTimings[1])
                                : "마지막 " + BuildCadenceToken(expectedTimings[2]),
                            StringComparison.Ordinal),
                        expectedRoute + " judgement feedback must preview the next authored timing.");
                    Require(
                        stepFeedback.Contains(BuildAvoidanceActionLabel(expectedAvoidances[i + 1]), StringComparison.Ordinal),
                        expectedRoute + " judgement feedback must preview the next authored avoidance action.");
                    RequireTextLayoutBudget(
                        stepFeedback,
                        3,
                        96,
                        expectedRoute + " route-step judgement feedback");
                    string stepCoach = GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot);
                    Require(
                        stepCoach.Contains("다음 " + BuildCadenceToken(expectedTimings[i + 1]), StringComparison.Ordinal),
                        expectedRoute + " live coach must bridge the player into the next cadence step.");
                    RequireTextLayoutBudget(stepCoach, 1, 70, expectedRoute + " route-step live coach");
                    if (i == 1)
                    {
                        Require(
                            stepFeedback.Contains(BuildExpectedRouteBundleRewardPreview(expectedRoute), StringComparison.Ordinal),
                            expectedRoute + " penultimate step must preview its clean finisher reward.");
                    }
                }
            }

            string bundleName = expectedRoute.Replace(" 루트", string.Empty) + " 완주";
            Require(snapshot.ChoiceActive, expectedRoute + " clean bundle must continue to the chapter node choice.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceHeaderDetail", snapshot)
                    .Contains("박자 지도 · 안정 정-압-정 / 변주 엇-정-엇 / 압박 압-엇-압", StringComparison.Ordinal),
                "The route choice screen must compare all three authored cadences before selection.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceHeaderDetail", snapshot)
                    .Contains("동작 지도 · 안정 점-숙-틀 / 변주 틀-숙-점 / 압박 숙-틀-점", StringComparison.Ordinal),
                "The route choice screen must compare all three authored avoidance flows before selection.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceHeaderDetail", snapshot)
                    .Contains("좌 안정 정-압-정/점-숙-틀", StringComparison.Ordinal),
                "The route choice header preview must keep the left route timing and action readable without repeating reward text.");
            Require(
                InvokeRuntimeUiLabel("BuildChoiceHeaderDetail", snapshot)
                    .Contains("우 압박 압-엇-압/숙-틀-점", StringComparison.Ordinal),
                "The route choice header preview must keep the right route timing and action readable.");
            string stableLaneLabel = InvokeRuntimeUiLabel("BuildChoiceLaneLabel", snapshot, "안정 루트");
            Require(
                stableLaneLabel.Contains("\nGood 체력+" + GreedLastRunCore.StableRouteGoodHealthBonus + "\n", StringComparison.Ordinal)
                    && stableLaneLabel.Contains("\n정-압-정 / 점-숙-틀", StringComparison.Ordinal),
                "The stable route lane button must split reward and timing/action hints into readable lines.");
            RequireTextLayoutBudget(stableLaneLabel, 5, 32, "stable route choice lane label");
            string pressureLaneLabel = InvokeRuntimeUiLabel("BuildChoiceLaneLabel", snapshot, "압박 루트");
            Require(
                pressureLaneLabel.Contains("\nClutch +" + GreedLastRunCore.PressureRouteClutchScoreBonus + "\n", StringComparison.Ordinal)
                    && pressureLaneLabel.Contains("\n압-엇-압 / 숙-틀-점", StringComparison.Ordinal),
                "The pressure route lane button must split reward and timing/action hints into readable lines.");
            RequireTextLayoutBudget(pressureLaneLabel, 5, 32, "pressure route choice lane label");
            Require(snapshot.JudgementText.Contains(bundleName, StringComparison.Ordinal), expectedRoute + " clean bundle must name its route finisher.");
            Require(snapshot.JudgementText.Contains("3동작 정확", StringComparison.Ordinal), expectedRoute + " clean bundle must name its three-action exact clear.");
            Require(
                InvokeRuntimeUiLabel("BuildPatternBundleBannerLabel", snapshot)
                    == bundleName + " · 3동작 정확",
                expectedRoute + " transition banner must combine route completion and three-action mastery.");
            Require(snapshot.TimingProfile.Contains(bundleName + " 1회", StringComparison.Ordinal), expectedRoute + " bundle count must survive into the timing profile.");
            Require(snapshot.TimingProfile.Contains("3동작 정확 1회", StringComparison.Ordinal), expectedRoute + " must persist one exact three-action section.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot).Contains(bundleName, StringComparison.Ordinal),
                expectedRoute + " bundle finisher must remain visible in central transition feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildRoutePayoffSettlementLine", snapshot).Contains(bundleName.Replace(" 완주", string.Empty) + " 1", StringComparison.Ordinal),
                expectedRoute + " bundle finisher must appear in route settlement.");
            Require(
                InvokeMockBackendStaticLabel("BuildRouteBundleStrengthText", snapshot.TimingProfile) == bundleName + " 1회",
                expectedRoute + " bundle finisher must become a result strength.");

            switch (expectedRoute)
            {
                case "안정 루트":
                    Require(snapshot.StableRouteBundleCount == 1, "Stable bundle completion must increment exactly once.");
                    Require(snapshot.Health == healthBeforeBundle + GreedLastRunCore.StableRouteBundleHealthBonus, "Stable bundle completion must grant its health reward.");
                    break;
                case "변주 루트":
                    Require(snapshot.VariationRouteBundleCount == 1, "Variation bundle completion must increment exactly once.");
                    Require(snapshot.FocusStateActive, "Variation bundle completion must finish with an active focus state.");
                    break;
                case "압박 루트":
                    Require(snapshot.PressureRouteBundleCount == 1, "Pressure bundle completion must increment exactly once.");
                    Require(
                        snapshot.Score >= scoreBeforeBundle + GreedLastRunCore.PressureRouteBundleScoreBonus,
                        "Pressure bundle completion must include its dedicated score reward.");
                    break;
            }

            string flowSummary = InvokeMockBackendStaticLabel(
                "BuildJudgementFlowSummary",
                snapshot.SuccessCount,
                snapshot.GoodCount,
                snapshot.MissCount,
                snapshot.TimingProfile);
            Require(flowSummary.Contains("경로 완주 1", StringComparison.Ordinal), expectedRoute + " result flow must preserve one clean route bundle.");

            GreedLastRunChannel routeChannel = BuildExpectedRouteChannel(expectedRoute);
            core.HandleInput(routeChannel, clock += 0.1f);
            Require(snapshot.ChoiceActive && snapshot.ChoiceKind == GreedLastChoiceKind.Gift, expectedRoute + " selection must continue to the gift choice.");
            core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
            Require(snapshot.ChoiceActive && snapshot.ChoiceKind == GreedLastChoiceKind.Relic, "The chapter-two gift must continue to the relic choice.");
            core.HandleInput(routeChannel, clock += 0.1f);
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(snapshot.ChapterIndex == 3 && snapshot.ActivePattern, "The relic cadence sample must begin chapter three.");
            float firstExactAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
            core.HandleInput(snapshot.Pattern.Channel, firstExactAt);
            clock = firstExactAt;
            core.RequestNextPattern(clock += 2f);
            GreedLastTimingType expectedRelicTiming = BuildExpectedRelicTiming(routeChannel);
            Require(
                snapshot.Pattern.TimingType == expectedRelicTiming,
                expectedRoute + " chapter-three middle step must adopt the selected relic timing.");
            string relicCadenceCue = InvokeRuntimeUiLabel("BuildRouteCadenceCue", snapshot);
            Require(
                relicCadenceCue.Contains("[" + BuildCadenceToken(expectedRelicTiming) + "]", StringComparison.Ordinal)
                    && relicCadenceCue.Contains("유물 변주", StringComparison.Ordinal)
                    && relicCadenceCue.Contains("변주 정점", StringComparison.Ordinal),
                expectedRoute + " chapter-three cadence must highlight and name its relic override.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("변주 정점", StringComparison.Ordinal),
                expectedRoute + " chapter-three coach must identify the run's variation peak.");
        }

        private static void VerifyFinalValidationTempoContract()
        {
            float chapterThreeTempo = InvokeRunCoreStaticFloat("BuildNormalChapterTempoScale", 3, false);
            float finalTempo = InvokeRunCoreStaticFloat("BuildNormalChapterTempoScale", 4, true);
            float chapterThreeWindow = InvokeRunCoreStaticFloat("BuildNormalChapterWindowScale", 3, false);
            float finalWindow = InvokeRunCoreStaticFloat("BuildNormalChapterWindowScale", 4, true);
            Require(
                finalTempo >= chapterThreeTempo,
                "Final validation must not be faster than the chapter-three hand-feel peak.");
            Require(
                finalWindow > chapterThreeWindow,
                "Final validation must use a wider base timing window than chapter three.");
        }

        private static string BuildExpectedRouteBundleObjective(string route)
        {
            switch (route)
            {
                case "안정 루트":
                    return "안정 완주 회복";
                case "변주 루트":
                    return "변주 완주 집중 +" + GreedLastRunCore.VariationRouteBundleFocusBonus;
                default:
                    return "압박 완주 +" + GreedLastRunCore.PressureRouteBundleScoreBonus;
            }
        }

        private static string BuildExpectedRouteBundleProgress(string route)
        {
            return route.Replace(" 루트", string.Empty) + " 완주";
        }

        private static string BuildExpectedRouteCadence(string route)
        {
            switch (route)
            {
                case "안정 루트":
                    return "정 → 압 → 정";
                case "변주 루트":
                    return "엇 → 정 → 엇";
                default:
                    return "압 → 엇 → 압";
            }
        }

        private static string BuildExpectedRouteBundleRewardPreview(string route)
        {
            switch (route)
            {
                case "안정 루트":
                    return "회복 대기";
                case "변주 루트":
                    return "집중 +" + GreedLastRunCore.VariationRouteBundleFocusBonus + " 대기";
                default:
                    return "+" + GreedLastRunCore.PressureRouteBundleScoreBonus + "점 대기";
            }
        }

        private static GreedLastRunChannel BuildExpectedRouteChannel(string route)
        {
            switch (route)
            {
                case "안정 루트":
                    return GreedLastRunChannel.Left;
                case "변주 루트":
                    return GreedLastRunChannel.Center;
                default:
                    return GreedLastRunChannel.Right;
            }
        }

        private static GreedLastTimingType BuildExpectedRelicTiming(GreedLastRunChannel channel)
        {
            switch (channel)
            {
                case GreedLastRunChannel.Left:
                    return GreedLastTimingType.Perfect;
                case GreedLastRunChannel.Center:
                    return GreedLastTimingType.Offbeat;
                default:
                    return GreedLastTimingType.Clutch;
            }
        }

        private static string BuildCadenceToken(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Perfect:
                    return "정";
                case GreedLastTimingType.Clutch:
                    return "압";
                case GreedLastTimingType.Offbeat:
                    return "엇";
                default:
                    return "-";
            }
        }

        private static string BuildAvoidanceToken(GreedLastAvoidanceType avoidanceType)
        {
            switch (avoidanceType)
            {
                case GreedLastAvoidanceType.Jump:
                    return "점";
                case GreedLastAvoidanceType.Slide:
                    return "숙";
                case GreedLastAvoidanceType.Twist:
                    return "틀";
                default:
                    return "-";
            }
        }

        private static string BuildAvoidanceActionLabel(GreedLastAvoidanceType avoidanceType)
        {
            switch (avoidanceType)
            {
                case GreedLastAvoidanceType.Jump:
                    return "점프";
                case GreedLastAvoidanceType.Slide:
                    return "숙이기";
                case GreedLastAvoidanceType.Twist:
                    return "비틀기";
                default:
                    return "대응";
            }
        }

        private static void VerifyRouteBundleBreakFeedback(float now)
        {
            var stableCore = new GreedLastRunCore();
            GreedLastRunSnapshot stableSnapshot = default;
            float stableClock = now;
            stableCore.SnapshotChanged += value => stableSnapshot = value;
            stableCore.Enter("stable bundle break smoke", refillRun: false);
            stableCore.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, stableClock += 0.1f);
            StartBufferedPattern(stableCore, ref stableSnapshot, ref stableClock);
            float goodDelta = (stableSnapshot.Pattern.SuccessWindowSeconds + stableSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            stableCore.HandleInput(
                stableSnapshot.Pattern.Channel,
                stableClock + stableSnapshot.Pattern.CoreSeconds + goodDelta - stableCore.InputTimingOffsetSeconds);
            Require(stableSnapshot.LastResult == GreedLastJudgementResult.Good, "The stable break sample must land in the Good window.");
            Require(!stableSnapshot.PerfectBundleIntact, "A Good judgement must break the clean route bundle.");
            Require(
                stableSnapshot.JudgementText.Contains("안정 완주 중단", StringComparison.Ordinal),
                "The first stable bundle break must explain that its clean finisher was lost.");
            Require(
                InvokeRuntimeUiLabel("BuildRunGuideLine", stableSnapshot).Contains("안정 완주 중단 1/3", StringComparison.Ordinal),
                "The live HUD must retain the broken stable bundle state.");
            Require(
                InvokeRuntimeUiLabel("BuildRunGuideLine", stableSnapshot).Contains("동작 완주 중단", StringComparison.Ordinal),
                "The live HUD must show that the three-action exact bundle was broken.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", stableSnapshot).Contains("완주 중단", StringComparison.Ordinal),
                "A Good bundle break must remain visible in central judgement feedback.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", stableSnapshot)
                    .Contains("경로 안정 중단 · 다음 압 연습", StringComparison.Ordinal),
                "A broken stable bundle must turn its next cadence step into an explicit practice cue.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(stableSnapshot)
                    .Contains("다음 압 연습", StringComparison.Ordinal),
                "The live coach must retain the next practice beat after a clean bundle is broken.");
            RequireTextLayoutBudget(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", stableSnapshot),
                3,
                96,
                "Broken stable-route judgement feedback");
            Require(
                InvokeRuntimeUiLabel("BuildRouteCadencePipState", stableSnapshot, 0) == "broken"
                    && InvokeRuntimeUiLabel("BuildRouteCadencePipState", stableSnapshot, 1) == "active",
                "A broken cadence rail must mark the resolved step red while keeping the next step active.");
            for (int i = 1; i < 3; i += 1)
            {
                stableCore.RequestNextPattern(stableClock += 2f);
                float exactAt = stableClock + stableSnapshot.Pattern.CoreSeconds - stableCore.InputTimingOffsetSeconds;
                stableCore.HandleInput(stableSnapshot.Pattern.Channel, exactAt);
                stableClock = exactAt;
            }

            Require(stableSnapshot.StableRouteBundleCount == 0, "A broken stable bundle must not grant its clean completion count.");
            Require(stableSnapshot.TimingProfile.Contains("안정 완주 0회", StringComparison.Ordinal), "A broken stable bundle must remain zero in the timing profile.");
            Require(stableSnapshot.TimingProfile.Contains("3동작 정확 0회", StringComparison.Ordinal), "A Good judgement must prevent three-action exact credit.");

            var pressureCore = new GreedLastRunCore();
            GreedLastRunSnapshot pressureSnapshot = default;
            float pressureClock = now + 40f;
            pressureCore.SnapshotChanged += value => pressureSnapshot = value;
            pressureCore.Enter("pressure bundle break smoke", refillRun: false);
            pressureCore.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, pressureClock += 0.1f);
            pressureCore.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, pressureClock += 0.1f);
            pressureCore.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, pressureClock += 0.1f);
            pressureCore.ToggleDevInvincible();
            StartBufferedPattern(pressureCore, ref pressureSnapshot, ref pressureClock);
            GreedLastRunChannel wrongChannel = pressureSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            pressureCore.HandleInput(
                wrongChannel,
                pressureClock + pressureSnapshot.Pattern.CoreSeconds - pressureCore.InputTimingOffsetSeconds);
            Require(pressureSnapshot.LastResult == GreedLastJudgementResult.Miss, "The pressure break sample must register a wrong-channel miss.");
            Require(
                pressureSnapshot.JudgementText.Contains("압박 완주 중단", StringComparison.Ordinal),
                "The first pressure miss must explain that its clean finisher was lost.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", pressureSnapshot).Contains("완주 중단", StringComparison.Ordinal),
                "An invincible pressure miss must still expose the lost clean finisher.");
        }

        private static void VerifyFinalCadenceGuidance(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter("final cadence guidance smoke", refillRun: false);
            core.JumpToDevShortcut(GreedLastDevShortcut.AfterCore, clock += 0.1f);
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(
                InvokeRuntimeUiLabel("BuildRouteCadencePipState", snapshot, 0) == "active",
                "Escape cadence rail must activate its first final step.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("탈출 검증", StringComparison.Ordinal),
                "Escape coach must frame its opening as final validation rather than a new rule.");
            string escapeProgressLabel = InvokeRuntimeUiLabel(
                "BuildEscapeProgressLabel",
                snapshot,
                snapshot.ChapterTarget);
            Require(
                escapeProgressLabel.Contains("탈출 검증", StringComparison.Ordinal)
                    && escapeProgressLabel.Contains("공명핵 회수", StringComparison.Ordinal),
                "Escape progress must preserve both its validation role and carried-core objective.");
            RequireTextLayoutBudget(escapeProgressLabel, 1, 42, "Escape role progress label");
            GreedLastTimingType[] expectedTimings =
            {
                GreedLastTimingType.Clutch,
                GreedLastTimingType.Offbeat,
                GreedLastTimingType.Perfect,
            };
            GreedLastAvoidanceType[] expectedAvoidances =
            {
                GreedLastAvoidanceType.Slide,
                GreedLastAvoidanceType.Twist,
                GreedLastAvoidanceType.Jump,
            };

            for (int i = 0; i < expectedTimings.Length; i += 1)
            {
                if (i > 0)
                {
                    core.RequestNextPattern(clock += 2f);
                }

                Require(snapshot.ActivePattern, "Escape cadence guidance must expose every final trap.");
                Require(snapshot.Pattern.TimingType == expectedTimings[i], "Escape must keep its authored pressure-offbeat-perfect cadence.");
                Require(snapshot.Pattern.AvoidanceType == expectedAvoidances[i], "Escape must keep its authored slide-twist-jump action validation.");
                string cadenceCue = InvokeRuntimeUiLabel("BuildRouteCadenceCue", snapshot);
                Require(
                    cadenceCue.Contains("탈출 박자 · 안정", StringComparison.Ordinal)
                        && cadenceCue.Contains("[" + BuildCadenceToken(expectedTimings[i]) + "]", StringComparison.Ordinal),
                    "Escape detail must identify and highlight its active final cadence step.");
                Require(
                    cadenceCue.Contains("[" + BuildAvoidanceToken(expectedAvoidances[i]) + "]", StringComparison.Ordinal),
                    "Escape detail must highlight its active slide-twist-jump validation step.");

                float exactAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(snapshot.Pattern.Channel, exactAt);
                clock = exactAt;
                if (i < 2)
                {
                    string feedback = InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot);
                    Require(
                        feedback.Contains("경로 안정 " + (i + 1) + "/3", StringComparison.Ordinal)
                            && feedback.Contains(
                                (i == 0 ? "다음 " : "마지막 ") + BuildCadenceToken(expectedTimings[i + 1]),
                                StringComparison.Ordinal),
                        "Escape judgement feedback must bridge into the next final cadence step.");
                    Require(
                        feedback.Contains(BuildAvoidanceActionLabel(expectedAvoidances[i + 1]), StringComparison.Ordinal),
                        "Escape judgement feedback must bridge into the next final avoidance action.");
                }
            }

            Require(snapshot.Stopped && snapshot.EscapeSucceeded, "The final cadence guidance sample must still finish the escape.");
            Require(snapshot.StableRouteBundleCount == 1, "A clean final escape cadence must grant its restored stable-route finisher.");
            Require(snapshot.JudgementText.Contains("안정 완주", StringComparison.Ordinal), "The escape settlement must retain its route finisher text.");
        }

        private static void VerifyRouteCycleDevShortcut(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter("route cycle shortcut smoke", refillRun: false);

            core.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, clock);
            Require(
                snapshot.StartCountdownActive
                    && snapshot.ChapterIndex == 2
                    && snapshot.ActiveRouteProfile == "안정 루트",
                "The first route-cycle shortcut press must prepare the stable chapter-two sample.");
            Require(
                InvokeRuntimeUiLabel("BuildRouteTestShortcutLabel", snapshot) == "경로 변주 ↔↓↑",
                "The route-cycle button must announce variation and its response map as the next sample.");
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(snapshot.Pattern.TimingType == GreedLastTimingType.Perfect, "The stable route shortcut must begin with its Perfect representative.");
            core.HandleInput(
                snapshot.Pattern.Channel,
                clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.SuccessCount == 1, "The stable route shortcut sample must remain playable.");

            core.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, clock += 2f);
            Require(
                snapshot.StartCountdownActive
                    && snapshot.ActiveRouteProfile == "변주 루트"
                    && snapshot.SuccessCount == 0
                    && snapshot.Score == 0,
                "The second route-cycle press must reset the comparison baseline and prepare variation.");
            Require(
                InvokeRuntimeUiLabel("BuildRouteTestShortcutLabel", snapshot) == "경로 압박 ↓↔↑",
                "The route-cycle button must announce pressure and its response map as the next sample.");
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(snapshot.Pattern.TimingType == GreedLastTimingType.Offbeat, "The variation route shortcut must begin with its Offbeat representative.");

            core.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, clock += 2f);
            Require(
                snapshot.StartCountdownActive
                    && snapshot.ActiveRouteProfile == "압박 루트"
                    && snapshot.SuccessCount == 0
                    && snapshot.Score == 0,
                "The third route-cycle press must reset the comparison baseline and prepare pressure.");
            Require(
                InvokeRuntimeUiLabel("BuildRouteTestShortcutLabel", snapshot) == "경로 안정 ↑↓↔",
                "The route-cycle button must wrap back to stable with its response map after pressure.");
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(snapshot.Pattern.TimingType == GreedLastTimingType.Clutch, "The pressure route shortcut must begin with its Clutch representative.");
            core.HandleInput(
                snapshot.Pattern.Channel,
                clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.PressureRouteCounterCount == 1, "The pressure route shortcut must expose its amplified counter payoff.");
        }

        private static void VerifyAvoidanceCycleDevShortcut(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter("avoidance cycle shortcut smoke", refillRun: false);

            GreedLastAvoidanceType[] expectedAvoidances =
            {
                GreedLastAvoidanceType.Jump,
                GreedLastAvoidanceType.Slide,
                GreedLastAvoidanceType.Twist,
            };
            GreedLastTimingType[] expectedTimings =
            {
                GreedLastTimingType.Perfect,
                GreedLastTimingType.Clutch,
                GreedLastTimingType.Offbeat,
            };
            string[] expectedActions = { "점프", "숙이기", "비틀기" };
            string[] expectedLaneHazards = { "바닥", "상단", "측면" };
            string[] expectedNextLabels = { "다음 ↓ 숙이기", "다음 ↔ 비틀기", "다음 ↑ 점프" };
            string[] expectedRoles = { "충전", "반격", "연쇄" };
            string[] expectedRolePayoffs =
            {
                "충전+" + GreedLastRunCore.PerfectFocusChargeBonus,
                "반격+" + GreedLastRunCore.ClutchCounterScoreBonus,
                "연쇄+" + GreedLastRunCore.OffbeatFlowComboBonus,
            };
            Require(
                InvokeRuntimeUiLabel("BuildAvoidanceTestShortcutLabel", snapshot) == "동작 ↑ 점프",
                "The avoidance-cycle shortcut button must name jump as the first comparison sample.");

            for (int i = 0; i < expectedAvoidances.Length; i += 1)
            {
                core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock += 2f);
                Require(
                    snapshot.StartCountdownActive
                        && snapshot.ChapterIndex == 1
                        && snapshot.ChapterProgress == i
                        && snapshot.Score == 0
                        && snapshot.Distance <= 0.001f
                        && snapshot.SuccessCount == 0,
                    "Every avoidance-cycle shortcut press must reset the comparison baseline and prepare the matching chapter-one step.");
                Require(
                    snapshot.JudgementText.Contains(expectedActions[i], StringComparison.Ordinal),
                    "The avoidance-cycle countdown must name the response being tested.");
                Require(
                    InvokeRuntimeUiLabel("BuildAvoidanceTestShortcutLabel", snapshot) == expectedNextLabels[i],
                    "The avoidance-cycle shortcut button must show the next comparison sample after each press.");

                StartBufferedPattern(core, ref snapshot, ref clock);
                Require(
                    snapshot.Pattern.AvoidanceType == expectedAvoidances[i]
                        && snapshot.Pattern.TimingType == expectedTimings[i],
                    "The avoidance-cycle shortcut must pair each response with its authored representative timing.");
                string laneResponseHint = InvokeRuntimeUiLabel("BuildLaneResponseHint", snapshot);
                Require(
                    laneResponseHint.Contains(expectedActions[i], StringComparison.Ordinal)
                        && laneResponseHint.Contains(expectedLaneHazards[i], StringComparison.Ordinal)
                        && laneResponseHint.Contains("→", StringComparison.Ordinal)
                        && laneResponseHint.Contains(expectedRoles[i], StringComparison.Ordinal)
                        && laneResponseHint.Contains(expectedRolePayoffs[i], StringComparison.Ordinal)
                        && laneResponseHint.Contains(
                            expectedAvoidances[i] == GreedLastAvoidanceType.Jump
                                ? "↑"
                                : expectedAvoidances[i] == GreedLastAvoidanceType.Slide ? "↓" : "↔",
                            StringComparison.Ordinal),
                    "The active lane hint must pair the hazard, response name, payoff value, payoff role, and action symbol.");
                Vector4 markerShape = InvokeRuntimeUiVector4("BuildTrapMarkerShapeLayout", snapshot.Pattern, 1f);
                if (expectedAvoidances[i] == GreedLastAvoidanceType.Jump)
                {
                    Require(
                        markerShape.x > markerShape.y * 3f
                            && Mathf.Approximately(markerShape.z, 0f),
                        "Jump trap markers must stay low and wide like a floor sweep.");
                }
                else if (expectedAvoidances[i] == GreedLastAvoidanceType.Slide)
                {
                    Require(
                        markerShape.y > 100f
                            && markerShape.x > markerShape.y
                            && Mathf.Approximately(markerShape.z, 0f),
                        "Slide trap markers must stay thick overhead blocks without diagonal ambiguity.");
                }
                else
                {
                    Require(
                        markerShape.y > markerShape.x * 3f
                            && Mathf.Abs(markerShape.z) >= 20f,
                        "Twist trap markers must stay narrow, tall, and angled like a side strike.");
                }
                Vector4 dangerBounds = InvokeRuntimeUiVector4(
                    "BuildTrapDangerBandLayout",
                    snapshot.Pattern,
                    0.5f,
                    0.75f,
                    0f,
                    0f);
                if (expectedAvoidances[i] == GreedLastAvoidanceType.Jump)
                {
                    Require(
                        dangerBounds.y < 0.16f
                            && dangerBounds.w < 0.37f
                            && dangerBounds.z - dangerBounds.x >= 0.24f,
                        "Jump traps must read as a low, broad floor danger zone.");
                }
                else if (expectedAvoidances[i] == GreedLastAvoidanceType.Slide)
                {
                    Require(
                        dangerBounds.y >= 0.30f && dangerBounds.w >= 0.90f,
                        "Slide traps must read as an overhead danger zone descending from above.");
                }
                else
                {
                    Require(
                        dangerBounds.z - dangerBounds.x <= 0.17f
                            && dangerBounds.y <= 0.18f
                            && dangerBounds.w >= 0.88f,
                        "Twist traps must read as a narrow side strike through the lane.");
                }
                Vector4 missImpact = InvokeRuntimeUiVector4("BuildResolvedMissImpactLayout", snapshot.Pattern);
                float missImpactAngle = InvokeRuntimeUiFloat("BuildResolvedMissImpactAngle", snapshot.Pattern);
                float missHaptic = InvokeRuntimeUiFloat("BuildMissHapticDuration", snapshot.Pattern);
                Vector4 successPose = InvokeRuntimeUiVector4("BuildRunnerResponsePoseTransformForPattern", snapshot.Pattern, 1f, 1f);
                Vector4 successMotion = InvokeRuntimeUiVector4("BuildRunnerResponsePoseMotionForPattern", snapshot.Pattern, 1f, 1f);
                Vector4 successLimbs = InvokeRuntimeUiVector4("BuildRunnerResponseLimbAnglesForPattern", snapshot.Pattern, 1f, 1f);
                if (expectedAvoidances[i] == GreedLastAvoidanceType.Jump)
                {
                    Require(
                        missImpact.y < 0.18f
                            && missImpact.z > missImpact.w * 4f
                            && Mathf.Approximately(missImpactAngle, 0f)
                            && Mathf.Approximately(missHaptic, 0.060f),
                        "A failed jump must leave a broad floor impact and a short ground-hit vibration.");
                    Require(
                        successPose.y >= 90f
                            && successPose.w < 0.94f
                            && successMotion.z < 0.62f
                            && successLimbs.x < 0f
                            && successLimbs.y > 0f,
                        "A successful jump must visibly rise, compress, and tuck its limbs.");
                }
                else if (expectedAvoidances[i] == GreedLastAvoidanceType.Slide)
                {
                    Require(
                        missImpact.y >= 0.29f
                            && missImpact.z > missImpact.w * 2f
                            && Mathf.Approximately(missImpactAngle, 0f)
                            && Mathf.Approximately(missHaptic, 0.085f),
                        "A failed slide must leave an overhead compression impact and the longest vibration.");
                    Require(
                        successPose.y <= -29f
                            && successPose.z > 1.20f
                            && successPose.w < 0.58f
                            && Mathf.Abs(successMotion.x) >= 8f
                            && successLimbs.z < 0f,
                        "A successful slide must crouch low, stretch wide, and lean under the overhead trap.");
                }
                else
                {
                    Require(
                        missImpact.w > missImpact.z * 4f
                            && Mathf.Abs(missImpact.x - 0.5f) > 0.05f
                            && Mathf.Abs(missImpactAngle) >= 30f
                            && Mathf.Approximately(missHaptic, 0.070f),
                        "A failed twist must leave an offset side slash and a distinct medium vibration.");
                    Require(
                        Mathf.Abs(successPose.x) >= 46f
                            && successPose.z < 0.86f
                            && Mathf.Abs(successMotion.x) >= 13f
                            && successMotion.z < 0.84f
                            && successLimbs.x < 0f
                            && successLimbs.y > 0f,
                        "A successful twist must sidestep, narrow its silhouette, and rotate around the side strike.");
                }
                float exactAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(snapshot.Pattern.Channel, exactAt);
                clock = exactAt;
                Require(snapshot.LastResult == GreedLastJudgementResult.Success, "Every avoidance-cycle sample must remain playable.");
            }

            core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock += 2f);
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(
                snapshot.Pattern.AvoidanceType == GreedLastAvoidanceType.Jump
                    && snapshot.Pattern.TimingType == GreedLastTimingType.Perfect,
                "The avoidance-cycle shortcut must wrap from twist back to jump.");
        }

        private static void VerifyStableRouteRecovery(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            EnterSecondChapterWithRoute(
                core,
                ref snapshot,
                ref clock,
                GreedLastRunChannel.Left,
                GreedLastRunChannel.Left,
                "stable route");
            Require(snapshot.Pattern.Prompt.Contains("[안정]", StringComparison.Ordinal), "The stable route must mark its active traps.");
            Require(snapshot.ActiveRouteProfile == "안정 루트", "The selected stable route must remain explicit in the live snapshot.");
            Require(
                InvokeRuntimeUiLabel("BuildChapterExperienceTitle", snapshot).StartsWith("안정 · ", StringComparison.Ordinal),
                "The chapter title must retain the selected stable route.");
            Require(
                InvokeRuntimeUiLabel("BuildRunGuideLine", snapshot).Contains("경로 안정 보험", StringComparison.Ordinal),
                "The live guide must retain the stable-route identity.");
            Require(
                snapshot.JudgementText.Contains("Good 체력 +" + GreedLastRunCore.StableRouteGoodHealthBonus, StringComparison.Ordinal),
                "The first stable trap must announce its persistent recovery contract.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("보정 체력 +" + GreedLastRunCore.StableRouteGoodHealthBonus, StringComparison.Ordinal),
                "The stable route coach must preview its Good recovery insurance.");

            GreedLastRunChannel wrongChannel = snapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            float missAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
            core.HandleInput(wrongChannel, missAt);
            clock = missAt;
            Require(snapshot.LastResult == GreedLastJudgementResult.Miss, "The stable route recovery test must take one real hit.");
            int damagedHealth = snapshot.Health;

            core.RequestNextPattern(clock += 2f);
            Require(snapshot.ActivePattern && snapshot.Pattern.Prompt.Contains("[복구]", StringComparison.Ordinal), "The stable route must retain the recovery breather after a miss.");
            float goodDelta = (snapshot.Pattern.SuccessWindowSeconds + snapshot.Pattern.GoodWindowSeconds) * 0.5f;
            core.HandleInput(
                snapshot.Pattern.Channel,
                clock + snapshot.Pattern.CoreSeconds + goodDelta - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Good, "The stable route contract must trigger in the Good band.");
            Require(snapshot.Health == damagedHealth + GreedLastRunCore.StableRouteGoodHealthBonus, "Stable-route Good must restore one damaged health point.");
            Require(snapshot.StableRouteRecoveryCount == 1, "Stable-route recovery must increment its dedicated count exactly once.");
            Require(snapshot.JudgementText.Contains("안정 루트 - 보정 회복", StringComparison.Ordinal), "Stable-route Good must name its recovery payoff.");
            Require(snapshot.TimingProfile.Contains("안정 복구 1회", StringComparison.Ordinal), "Stable-route recovery must survive into the timing profile.");
            Require(
                InvokeRuntimeUiLabel("BuildRoutePayoffSettlementLine", snapshot).Contains("안정 1", StringComparison.Ordinal),
                "The route settlement line must preserve stable recovery.");
            Require(
                InvokeMockBackendStaticLabel("BuildRoutePayoffStrengthText", snapshot.TimingProfile) == "안정 복구 1회",
                "The result strength analysis must recognize stable recovery.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot).Contains("안정 회복 +" + GreedLastRunCore.StableRouteGoodHealthBonus, StringComparison.Ordinal),
                "Stable-route recovery must have distinct central feedback.");
            Color32 stableColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", snapshot);
            Require(
                stableColor.g > stableColor.r && stableColor.g > stableColor.b,
                "Stable-route recovery must use a recovery-green feedback color.");
            float stableHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "안정 루트", false);
            float stableAchievementHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "안정 루트", true);
            Require(
                Mathf.Approximately(stableHaptic, 0.030f)
                    && Mathf.Approximately(stableAchievementHaptic, 0.055f),
                "Stable-route recovery must keep a soft recovery haptic and a stronger target-achievement pulse.");
        }

        private static void VerifyVariationRouteFlow(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            EnterSecondChapterWithRoute(
                core,
                ref snapshot,
                ref clock,
                GreedLastRunChannel.Center,
                GreedLastRunChannel.Center,
                "variation route");
            Require(
                snapshot.Pattern.Prompt.Contains("[변주]", StringComparison.Ordinal)
                    && snapshot.Pattern.TimingType == GreedLastTimingType.Offbeat,
                "The variation route must lead with an Offbeat trap.");
            Require(snapshot.ActiveRouteProfile == "변주 루트", "The selected variation route must remain explicit in the live snapshot.");
            Require(
                InvokeRuntimeUiLabel("BuildChapterExperienceTitle", snapshot).StartsWith("변주 · ", StringComparison.Ordinal),
                "The chapter title must retain the selected variation route.");
            Require(
                snapshot.JudgementText.Contains("엇박 정박 집중 +" + GreedLastRunCore.VariationRouteOffbeatFocusBonus, StringComparison.Ordinal),
                "The first variation trap must announce its persistent focus contract.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("변주 공명", StringComparison.Ordinal)
                    && GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("집중 +" + GreedLastRunCore.VariationRouteOffbeatFocusBonus, StringComparison.Ordinal),
                "The variation route coach must preview its Offbeat focus payoff.");
            string variationObjective = InvokeRuntimeUiLabel("BuildRunObjectiveLine", snapshot);
            Require(
                variationObjective.Contains("변주 집중 +" + GreedLastRunCore.VariationRouteOffbeatFocusBonus, StringComparison.Ordinal),
                "The run objective must expose the variation-route focus contract. Actual: " + variationObjective);
            Require(
                InvokeRuntimeUiLabel("BuildActiveActionCueText", snapshot)
                    .Contains("변주집중+" + GreedLastRunCore.VariationRouteOffbeatFocusBonus, StringComparison.Ordinal),
                "The active variation route cue must preview its focus route payoff.");

            core.HandleInput(
                snapshot.Pattern.Channel,
                clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success && snapshot.LastCenterHit, "Variation-route Offbeat must resolve at the center line.");
            Require(snapshot.VariationRouteFlowCount == 1, "Variation-route flow must increment its dedicated count exactly once.");
            Require(snapshot.JudgementText.Contains("변주 루트 - 엇박 공명", StringComparison.Ordinal), "Variation-route exact input must name its focus payoff.");
            Require(snapshot.TimingProfile.Contains("변주 공명 1회", StringComparison.Ordinal), "Variation-route flow must survive into the timing profile.");
            Require(
                InvokeRuntimeUiLabel("BuildRoutePayoffSettlementLine", snapshot).Contains("변주 1", StringComparison.Ordinal),
                "The route settlement line must preserve variation flow.");
            Require(
                InvokeMockBackendStaticLabel("BuildRoutePayoffStrengthText", snapshot.TimingProfile) == "변주 공명 1회",
                "The result strength analysis must recognize variation flow.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot).Contains("루트 집중 +" + GreedLastRunCore.VariationRouteOffbeatFocusBonus, StringComparison.Ordinal),
                "Variation-route flow must have distinct central feedback.");
            Color32 variationColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", snapshot);
            Require(
                variationColor.b > variationColor.r && variationColor.b > variationColor.g,
                "Variation-route flow must use a distinct syncopation feedback color.");
            float stableHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "안정 루트", false);
            float variationHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "변주 루트", false);
            Require(
                Mathf.Approximately(variationHaptic, 0.040f)
                    && variationHaptic > stableHaptic,
                "Variation-route flow must keep a stronger focus haptic than stable recovery.");
        }

        private static void VerifyPressureRouteCounter(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            float clock = now;
            core.SnapshotChanged += value => snapshot = value;
            EnterSecondChapterWithRoute(
                core,
                ref snapshot,
                ref clock,
                GreedLastRunChannel.Right,
                GreedLastRunChannel.Center,
                "pressure route");
            Require(
                snapshot.Pattern.Prompt.Contains("[압박]", StringComparison.Ordinal)
                    && snapshot.Pattern.TimingType == GreedLastTimingType.Clutch,
                "The pressure route must lead with a Clutch trap.");
            Require(snapshot.ActiveRouteProfile == "압박 루트", "The selected pressure route must remain explicit in the live snapshot.");
            Require(
                InvokeRuntimeUiLabel("BuildChapterExperienceTitle", snapshot).StartsWith("압박 · ", StringComparison.Ordinal),
                "The chapter title must retain the selected pressure route.");
            Require(
                snapshot.JudgementText.Contains("Clutch 정박 +" + GreedLastRunCore.PressureRouteClutchScoreBonus, StringComparison.Ordinal),
                "The first pressure trap must announce its persistent counter contract.");
            Require(
                GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("압박 증폭", StringComparison.Ordinal)
                    && GreedLastRuntimeUi.BuildLiveRunCoachLine(snapshot).Contains("반격 +" + GreedLastRunCore.PressureRouteClutchScoreBonus, StringComparison.Ordinal),
                "The pressure route coach must preview its amplified counter.");
            Require(
                InvokeRuntimeUiLabel("BuildRunObjectiveLine", snapshot).Contains("압박 증폭 +" + GreedLastRunCore.PressureRouteClutchScoreBonus, StringComparison.Ordinal),
                "The run objective must expose the pressure-route counter contract.");
            Require(
                InvokeRuntimeUiLabel("BuildActiveActionCueText", snapshot)
                    .Contains("압박+" + GreedLastRunCore.PressureRouteClutchScoreBonus, StringComparison.Ordinal),
                "The active pressure route cue must preview its amplified counter payoff.");

            int scoreBeforeCounter = snapshot.Score;
            core.HandleInput(
                snapshot.Pattern.Channel,
                clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success && snapshot.LastCenterHit, "Pressure-route Clutch must resolve at the center line.");
            Require(snapshot.PressureRouteCounterCount == 1, "Pressure-route counter must increment its dedicated count exactly once.");
            Require(
                snapshot.Score >= scoreBeforeCounter + GreedLastRunCore.ClutchCounterScoreBonus + GreedLastRunCore.PressureRouteClutchScoreBonus,
                "Pressure-route Clutch must include both its base counter and route amplification scores.");
            Require(snapshot.JudgementText.Contains("압박 루트 - 클러치 증폭", StringComparison.Ordinal), "Pressure-route exact input must name its amplified counter.");
            Require(snapshot.TimingProfile.Contains("압박 증폭 1회", StringComparison.Ordinal), "Pressure-route counter must survive into the timing profile.");
            Require(
                InvokeRuntimeUiLabel("BuildRoutePayoffSettlementLine", snapshot).Contains("압박 1", StringComparison.Ordinal),
                "The route settlement line must preserve pressure counters.");
            Require(
                InvokeMockBackendStaticLabel("BuildRoutePayoffStrengthText", snapshot.TimingProfile) == "압박 증폭 1회",
                "The result strength analysis must recognize pressure counters.");
            Require(
                InvokeRuntimeUiLabel("BuildJudgementFeedbackText", snapshot).Contains("루트 증폭 +" + GreedLastRunCore.PressureRouteClutchScoreBonus, StringComparison.Ordinal),
                "Pressure-route counter must have distinct central feedback.");
            Color32 pressureColor = InvokeRuntimeUiSnapshotColor("GetJudgementFeedbackColor", snapshot);
            Require(
                pressureColor.r > pressureColor.g && pressureColor.g > pressureColor.b,
                "Pressure-route counter must use a distinct high-pressure feedback color.");
            float variationHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "변주 루트", false);
            float pressureHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "압박 루트", false);
            float pressureAchievementHaptic = InvokeRuntimeUiFloat("BuildRoutePayoffHapticDuration", "압박 루트", true);
            Require(
                Mathf.Approximately(pressureHaptic, 0.050f)
                    && pressureHaptic > variationHaptic
                    && pressureAchievementHaptic > pressureHaptic,
                "Pressure-route counter must keep the sharpest route haptic and gain extra weight on target achievement.");

            string resultFlow = InvokeMockBackendStaticLabel(
                "BuildJudgementFlowSummary",
                snapshot.SuccessCount,
                snapshot.GoodCount,
                snapshot.MissCount,
                snapshot.TimingProfile);
            Require(resultFlow.Contains("압박 증폭 1", StringComparison.Ordinal), "Result flow summaries must preserve the active route payoff count.");
        }

        private static void VerifyRunAtmosphereContracts(float now)
        {
            var stableCore = new GreedLastRunCore();
            GreedLastRunSnapshot stableSnapshot = default;
            float stableClock = now;
            stableCore.SnapshotChanged += value => stableSnapshot = value;
            EnterSecondChapterWithRoute(
                stableCore,
                ref stableSnapshot,
                ref stableClock,
                GreedLastRunChannel.Left,
                GreedLastRunChannel.Left,
                "stable atmosphere");
            Require(
                InvokeRuntimeUiLabel("BuildAtmosphereRouteProfile", stableSnapshot) == "안정 루트",
                "The atmosphere helper must extract the selected stable route.");
            Color32 stableAccent = InvokeRuntimeUiSnapshotColor("BuildRunAtmosphereAccent", stableSnapshot);
            Require(
                stableAccent.g >= stableAccent.r && stableAccent.g > stableAccent.b,
                "Stable-route atmosphere must lean toward a readable recovery-green accent.");

            var variationCore = new GreedLastRunCore();
            GreedLastRunSnapshot variationSnapshot = default;
            float variationClock = now + 40f;
            variationCore.SnapshotChanged += value => variationSnapshot = value;
            EnterSecondChapterWithRoute(
                variationCore,
                ref variationSnapshot,
                ref variationClock,
                GreedLastRunChannel.Center,
                GreedLastRunChannel.Center,
                "variation atmosphere");
            Require(
                InvokeRuntimeUiLabel("BuildAtmosphereRouteProfile", variationSnapshot) == "변주 루트",
                "The atmosphere helper must extract the selected variation route.");
            Color32 variationAccent = InvokeRuntimeUiSnapshotColor("BuildRunAtmosphereAccent", variationSnapshot);
            Require(
                variationAccent.b > stableAccent.b,
                "Variation-route atmosphere must carry more cool-blue syncopation than the stable route.");

            var pressureCore = new GreedLastRunCore();
            GreedLastRunSnapshot pressureSnapshot = default;
            float pressureClock = now + 80f;
            pressureCore.SnapshotChanged += value => pressureSnapshot = value;
            EnterSecondChapterWithRoute(
                pressureCore,
                ref pressureSnapshot,
                ref pressureClock,
                GreedLastRunChannel.Right,
                GreedLastRunChannel.Center,
                "pressure atmosphere");
            Require(
                InvokeRuntimeUiLabel("BuildAtmosphereRouteProfile", pressureSnapshot) == "압박 루트",
                "The atmosphere helper must extract the selected pressure route.");
            Color32 pressureAccent = InvokeRuntimeUiSnapshotColor("BuildRunAtmosphereAccent", pressureSnapshot);
            Require(
                pressureAccent.r > stableAccent.r && pressureAccent.r > variationAccent.r,
                "Pressure-route atmosphere must carry the strongest warm warning accent.");

            var relicCore = new GreedLastRunCore();
            GreedLastRunSnapshot relicSnapshot = default;
            float relicClock = now + 120f;
            relicCore.SnapshotChanged += value => relicSnapshot = value;
            EnterSecondChapterWithRoute(
                relicCore,
                ref relicSnapshot,
                ref relicClock,
                GreedLastRunChannel.Center,
                GreedLastRunChannel.Center,
                "relic atmosphere");
            ResolveExactBundle(relicCore, ref relicSnapshot, ref relicClock, "relic atmosphere chapter two");
            Require(relicSnapshot.ChoiceKind == GreedLastChoiceKind.Node, "The relic atmosphere test must reach the chapter-two node choice first.");
            relicCore.HandleInput(GreedLastRunChannel.Center, relicClock += 0.05f);
            Require(relicSnapshot.ChoiceKind == GreedLastChoiceKind.Gift, "The relic atmosphere test must continue through the chapter-two gift choice.");
            relicCore.HandleInput(GreedLastRunChannel.Center, relicClock += 0.05f);
            Require(relicSnapshot.ChoiceKind == GreedLastChoiceKind.Relic, "The relic atmosphere test must reach relic choice.");
            relicCore.HandleInput(GreedLastRunChannel.Right, relicClock += 0.05f);
            Require(relicSnapshot.ActiveRelic == "파라오의 각인", "The relic atmosphere test must select Pharaoh's Seal.");
            Color32 relicAccent = InvokeRuntimeUiSnapshotColor("BuildRunAtmosphereAccent", relicSnapshot);
            Require(
                relicAccent.r >= relicAccent.b + 24,
                "A Pharaoh relic must push the run accent toward warm gold without changing run rules.");

            var infiniteCore = new GreedLastRunCore();
            GreedLastRunSnapshot infiniteSnapshot = default;
            infiniteCore.SnapshotChanged += value => infiniteSnapshot = value;
            infiniteCore.EnterInfiniteRun(BuildStoredGiftRecord(
                "연쇄 축",
                "연쇄 축 / 압박 루트 / 압박 루트"));
            Require(infiniteSnapshot.InfiniteMode, "The infinite atmosphere test must enter infinite mode.");
            string infiniteAtmosphereProfile = InvokeRuntimeUiLabel("BuildAtmosphereChoiceProfile", infiniteSnapshot);
            string infiniteAtmosphereRoute = InvokeRuntimeUiLabel("BuildAtmosphereRouteProfile", infiniteSnapshot);
            Require(
                infiniteAtmosphereProfile == "연쇄형"
                    && infiniteAtmosphereRoute == "압박 루트",
                "Infinite atmosphere must extract both stored profile and stored route from combined loadout text. Actual profile=["
                    + infiniteAtmosphereProfile + "] route=[" + infiniteAtmosphereRoute + "] source=["
                    + infiniteSnapshot.ActiveInfiniteLoadoutProfile + "].");
            Color32 threatOneAccent = InvokeRuntimeUiSnapshotColor("BuildRunAtmosphereAccent", infiniteSnapshot);
            for (int i = 0; i < 4; i += 1)
            {
                infiniteCore.RaiseInfiniteThreatForTest();
            }

            Require(infiniteSnapshot.InfiniteThreatLevel == 5, "The infinite atmosphere test must reach the highest test threat.");
            Color32 threatFiveAccent = InvokeRuntimeUiSnapshotColor("BuildRunAtmosphereAccent", infiniteSnapshot);
            Require(
                threatFiveAccent.r > threatOneAccent.r
                    && threatFiveAccent.g < threatOneAccent.g,
                "Rising infinite threat must warm the atmosphere toward danger.");
        }

        private static void EnterSecondChapterWithRoute(
            GreedLastRunCore core,
            ref GreedLastRunSnapshot snapshot,
            ref float clock,
            GreedLastRunChannel routeChannel,
            GreedLastRunChannel giftChannel,
            string context)
        {
            core.Enter(context, refillRun: false);
            core.HandleInput(GreedLastRunChannel.Center, clock);
            StartBufferedPattern(core, ref snapshot, ref clock);
            ResolveExactBundle(core, ref snapshot, ref clock, context + " chapter one");
            Require(snapshot.ChoiceActive && snapshot.ChoiceKind == GreedLastChoiceKind.Node, context + " must reach its route choice.");
            core.HandleInput(routeChannel, clock += 0.05f);
            Require(snapshot.ChoiceActive && snapshot.ChoiceKind == GreedLastChoiceKind.Gift, context + " must continue to its gift choice.");
            core.HandleInput(giftChannel, clock += 0.05f);
            Require(snapshot.StartCountdownActive && snapshot.ChapterIndex == 2, context + " must prepare chapter two.");
            StartBufferedPattern(core, ref snapshot, ref clock);
            Require(snapshot.ActivePattern && snapshot.ChapterIndex == 2, context + " must start chapter two with its selected route.");
        }

        private static void VerifySegmentDrivenNodeRecommendation(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter("segment recommendation smoke", refillRun: false);
            core.HandleInput(GreedLastRunChannel.Center, now);
            core.Tick(now + 2f);
            core.Tick(now + 3f);
            float clock = now + 3f;
            Require(snapshot.ActivePattern, "The segment recommendation test must start chapter one.");

            float goodDelta = (snapshot.Pattern.SuccessWindowSeconds + snapshot.Pattern.GoodWindowSeconds) * 0.5f;
            core.HandleInput(
                snapshot.Pattern.Channel,
                clock + snapshot.Pattern.CoreSeconds + goodDelta - core.InputTimingOffsetSeconds);
            for (int i = 0; i < 2; i += 1)
            {
                core.RequestNextPattern(clock += 2f);
                Require(snapshot.ActivePattern, "The mixed segment recommendation test must expose all chapter traps.");
                core.HandleInput(
                    snapshot.Pattern.Channel,
                    clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
            }

            Require(snapshot.ChoiceKind == GreedLastChoiceKind.Node, "A completed mixed chapter must open its node choice.");
            Require(
                snapshot.LastCompletedSegmentExactCount == 2
                    && snapshot.LastCompletedSegmentGoodCount == 1
                    && snapshot.LastCompletedSegmentMissCount == 0,
                "A mixed chapter must publish its 2/1/0 segment summary.");
            string recommendation = GreedLastRuntimeUi.BuildChoiceRecommendationLine(snapshot);
            Require(recommendation.Contains("추천: 안정 루트", StringComparison.Ordinal), "A chapter with Good reliance must recommend the wider stable node.");
            Require(recommendation.Contains("보정 정확 전환", StringComparison.Ordinal), "A Good-reliant node recommendation must explain the exact-conversion goal.");
            Require(recommendation.Contains("정-압-정", StringComparison.Ordinal), "A Good-reliant recommendation must preview the stable cadence.");

            core.HandleInput(GreedLastRunChannel.Left, clock += 0.1f);
            Require(snapshot.ChoiceKind == GreedLastChoiceKind.Gift, "The recommendation ladder must be able to select the stable route.");
            core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
            StartBufferedPattern(core, ref snapshot, ref clock);
            ResolveExactBundle(core, ref snapshot, ref clock, "stable graduation chapter");
            Require(snapshot.ChoiceKind == GreedLastChoiceKind.Node && snapshot.ActiveRouteProfile == "안정 루트", "The stable graduation test must return to its next node choice.");
            string variationRecommendation = GreedLastRuntimeUi.BuildChoiceRecommendationLine(snapshot);
            Require(
                variationRecommendation.Contains("추천: 변주 루트", StringComparison.Ordinal)
                    && variationRecommendation.Contains("안정 졸업 변주", StringComparison.Ordinal)
                    && variationRecommendation.Contains("엇-정-엇", StringComparison.Ordinal),
                "A clean chapter after stable recovery must recommend variation as the next skill step.");

            core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
            Require(snapshot.ChoiceKind == GreedLastChoiceKind.Gift, "The recommendation ladder must be able to select the variation route.");
            core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
            Require(snapshot.ChoiceKind == GreedLastChoiceKind.Relic, "The chapter-two variation choice must continue through its relic selection.");
            core.HandleInput(GreedLastRunChannel.Center, clock += 0.1f);
            StartBufferedPattern(core, ref snapshot, ref clock);
            ResolveExactBundle(core, ref snapshot, ref clock, "variation graduation chapter");
            Require(snapshot.ChoiceKind == GreedLastChoiceKind.Node && snapshot.ActiveRouteProfile == "변주 루트", "The variation graduation test must return to its next node choice.");
            string pressureRecommendation = GreedLastRuntimeUi.BuildChoiceRecommendationLine(snapshot);
            Require(
                pressureRecommendation.Contains("추천: 압박 루트", StringComparison.Ordinal)
                    && pressureRecommendation.Contains("완전 정확 고점", StringComparison.Ordinal)
                    && pressureRecommendation.Contains("압-엇-압", StringComparison.Ordinal),
                "A clean variation chapter must recommend pressure as the final high-score step.");
        }

        private static void StartBufferedPattern(
            GreedLastRunCore core,
            ref GreedLastRunSnapshot snapshot,
            ref float clock)
        {
            Require(snapshot.StartCountdownActive, "A buffered segment must begin with a start countdown.");
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            Require(snapshot.ActivePattern && snapshot.Pattern.IsValid, "A buffered segment must start its first trap.");
        }

        private static void ResolveExactBundle(
            GreedLastRunCore core,
            ref GreedLastRunSnapshot snapshot,
            ref float clock,
            string context)
        {
            for (int i = 0; i < 3; i += 1)
            {
                if (i > 0)
                {
                    core.RequestNextPattern(clock += 2f);
                }

                Require(snapshot.ActivePattern && snapshot.Pattern.IsValid, context + " must expose all three traps.");
                float inputAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(snapshot.Pattern.Channel, inputAt);
                clock = inputAt;
                Require(snapshot.LastResult == GreedLastJudgementResult.Success, context + " exact input must succeed.");
            }
        }

        private static GreedLastRunAttemptRecord BuildChapterGoodAttempt(float now)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            core.Enter("chapter Good smoke", refillRun: false);
            core.HandleInput(GreedLastRunChannel.Center, now + 0.01f);
            core.Tick(now + 2f);
            core.Tick(now + 3f);
            Require(snapshot.ActivePattern, "The chapter Good test must start its first trap.");
            float goodDelta = (snapshot.Pattern.SuccessWindowSeconds + snapshot.Pattern.GoodWindowSeconds) * 0.5f;
            core.HandleInput(
                snapshot.Pattern.Channel,
                now + 3f + snapshot.Pattern.CoreSeconds + goodDelta - core.InputTimingOffsetSeconds);
            Require(snapshot.LastResult == GreedLastJudgementResult.Good, "The chapter Good test must resolve inside the Good band.");
            GreedLastRunAttemptRecord record = core.CreateNormalAttemptRecord(GreedLastRunAttemptOutcome.Abandoned);
            Require(record.TimingProfile.Contains("1장 정확 0회", StringComparison.Ordinal), "A Good-only attempt must keep chapter-one exact at zero.");
            Require(record.TimingProfile.Contains("1장 보정 1회", StringComparison.Ordinal), "A Good-only attempt must attach its Good judgement to chapter one.");
            Require(record.TimingProfile.Contains("1장 실수 0회", StringComparison.Ordinal), "A Good-only attempt must keep chapter-one misses at zero.");
            return record;
        }

        private static void VerifySingleUseSaveCandidateAndTwoRunLifetime(
            GreedLastRunRecord record,
            GreedLastRunAttemptRecord chapterMissAttempt,
            GreedLastRunAttemptRecord chapterGoodAttempt)
        {
            const string backendSaveKey = "GreedLast.MockBackend.SaveData.v1";
            bool hadSavedData = PlayerPrefs.HasKey(backendSaveKey);
            string savedData = hadSavedData ? PlayerPrefs.GetString(backendSaveKey, string.Empty) : string.Empty;
            try
            {
                PlayerPrefs.DeleteKey(backendSaveKey);
                PlayerPrefs.Save();
                var backend = new GreedLastMockBackend();
                RequirePrototypeLoopStatus(backend, "루프 0/4", "A fresh profile");
                Require(
                    InvokeRuntimeUiStaticBool("ShouldHideLobbyDetail", InvokeMockBackendLabel(backend, "BuildLobbyStatusMessage", true)),
                    "The default synchronized lobby message must stay hidden.");
                GreedLastLobbySnapshot freshNoSaveConsumeLobby =
                    backend.ConsumeSelectedSaveLoadoutUseForTest(out string freshNoSaveConsumeNotice);
                Require(
                    freshNoSaveConsumeNotice.Contains("먼저 저장 조합을 만들어야 합니다.", StringComparison.Ordinal)
                        && freshNoSaveConsumeNotice.Contains("F6 저장 생성", StringComparison.Ordinal)
                        && !freshNoSaveConsumeNotice.Contains("무한 버튼으로 보충 런", StringComparison.Ordinal)
                        && !freshNoSaveConsumeLobby.InfiniteUnlocked,
                    "Before the first save, PgUp save-consume feedback must point to creating a save instead of refill.");
                string attemptMessage = backend.BuildRunAttemptResultMessage(chapterMissAttempt);
                RequireTextLayoutBudget(attemptMessage, 16, 110, "normal attempt result");
                Require(attemptMessage.Contains("약점 구간: 1장 실수 1회", StringComparison.Ordinal), "An attempt result must identify its most fragile chapter and failure count.");
                Require(attemptMessage.Contains("흐름: 위험 흐름", StringComparison.Ordinal), "A half-miss attempt must be classified as a dangerous judgement flow.");
                Require(attemptMessage.Contains("복기: 채널 확인 / 1장 실수 1회", StringComparison.Ordinal), "An attempt result must connect the miss reason to the retry review line.");
                Require(
                    attemptMessage.Contains("훈련 힌트: 밝아진 목표 레인만 보고 입력 / 상단→↓숙이기 실수 1회 / 기초연습 숙이기 단계", StringComparison.Ordinal),
                    "An attempt result training hint must carry the weakest avoidance drill step.");
                Require(attemptMessage.Contains("재도전 목표: 1장 무실수 / 현재 1", StringComparison.Ordinal), "A chapter miss must become a segment-specific retry target.");
                Require(attemptMessage.Contains("다음: 복기 재도전으로 실수 구간 보완", StringComparison.Ordinal), "A missed attempt should prioritize a review retry as the next action.");
                var reviewChecklistBackend = new GreedLastMockBackend();
                Require(reviewChecklistBackend.RegisterNormalAttempt(chapterMissAttempt), "A review-checklist attempt must register for profile board verification.");
                RequirePrototypeLoopStatus(reviewChecklistBackend, "루프 1/4", "A failed-attempt profile");
                GreedLastLobbySnapshot reviewLobby = reviewChecklistBackend.LoadLobby();
                Require(
                    reviewLobby.ProfileButtonSummary.Contains("복기", StringComparison.Ordinal)
                        && reviewLobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "A profile with an active weakness should label the profile button as review before passive growth.");
                Require(
                    InvokeAppStaticInt("ResolveProfileBoardOpeningPage", reviewLobby) == 0,
                    "A review-priority profile button must open on the summary checklist instead of the storage/growth page.");
                string reviewChecklistText = reviewChecklistBackend.BuildProfileBoardText(0);
                RequireTextLayoutBudget(reviewChecklistText, 12, 92, "profile review checklist");
                Require(
                    reviewChecklistText.Contains("복기 약점 상단→↓숙이기 실1 / 연습 숙이기", StringComparison.Ordinal),
                    "The profile prototype checklist must surface the current avoidance weakness.");
                Require(
                    reviewChecklistText.Contains("다음 행동", StringComparison.Ordinal)
                        && reviewChecklistText.Contains("Enter/강조: 복기 연습", StringComparison.Ordinal)
                        && reviewChecklistText.Contains("복기 연습으로 기초연습 숙이기 단계 반복", StringComparison.Ordinal),
                    "The profile next action must name the highlighted practice action and prioritize the current avoidance drill over passive growth.");
                string goodAttemptMessage = backend.BuildRunAttemptResultMessage(chapterGoodAttempt);
                RequireTextLayoutBudget(goodAttemptMessage, 16, 110, "Good-only attempt result");
                Require(goodAttemptMessage.Contains("약점 구간: 1장 보정 1회", StringComparison.Ordinal), "A Good-only attempt must request an exact-judgement conversion in its weak segment.");
                Require(goodAttemptMessage.Contains("흐름: 보정 의존", StringComparison.Ordinal), "A Good-only attempt must be classified as correction-dependent.");
                Require(
                    goodAttemptMessage.Contains("훈련 힌트: 1장 보정 1회 / 정확 판정 전환 / 바닥→↑점프 보정 1회 / 기초연습 점프 단계 / 판정선 안쪽으로 입력 좁히기", StringComparison.Ordinal),
                    "A Good-only attempt must receive a concrete exact-band training hint with its weakest avoidance step.");
                Require(goodAttemptMessage.Contains("복기: 중단 / 1장 보정 1회", StringComparison.Ordinal), "A Good-only attempt must connect correction reliance to a compact review line.");
                Require(goodAttemptMessage.Contains("재도전 목표: 1장 보정 0회 / 현재 1", StringComparison.Ordinal), "A chapter Good must become a segment-specific exact-conversion target.");
                Require(goodAttemptMessage.Contains("다음: 복기 연습으로 정확권 전환", StringComparison.Ordinal), "A Good-only attempt should prioritize a review drill as the next action.");
                GreedLastRunRecord comparisonBaseline = BuildComparisonBaselineRecord(record);
                Require(
                    InvokeAppStaticInt("ResolveProfileBoardOpeningPage", backend.LoadLobby()) == 0,
                    "A profile board with no actionable trace must open on the summary page.");
                backend.RegisterClear(comparisonBaseline);
                GreedLastLobbySnapshot lobby = backend.RegisterClear(record);
                Require(backend.HasSaveLoadoutCandidate && lobby.SaveLoadoutCandidateAvailable, "A clear must create one pending save candidate.");
                Require(
                    InvokeAppStaticInt("ResolveProfileBoardOpeningPage", lobby) == 1,
                    "A pending save candidate must open profile directly on the storage page.");
                Require(
                    lobby.ProfileButtonSummary.Contains("저장", StringComparison.Ordinal)
                        && lobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "A pending save candidate must label the profile button as a new save action.");
                GreedLastLobbySnapshot seenCandidateProfileLobby = backend.MarkProfileSeen();
                Require(
                    seenCandidateProfileLobby.ProfileButtonSummary.Contains("저장", StringComparison.Ordinal)
                        && !seenCandidateProfileLobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "Opening profile must clear only the NEW marker while keeping the pending save action label.");
                string pendingSaveProfileText = backend.BuildProfileBoardText(1);
                Require(
                    pendingSaveProfileText.Contains("Enter/강조: 저장 조합", StringComparison.Ordinal)
                        && pendingSaveProfileText.Contains("저장 후보를 첫 슬롯에 저장", StringComparison.Ordinal)
                        && pendingSaveProfileText.Contains("포기", StringComparison.Ordinal),
                    "A pending save candidate profile page must name the highlighted save-loadout action and the skip path.");
                string saveDraftText = backend.BuildSaveLoadoutDraftText();
                RequireTextLayoutBudget(saveDraftText, 18, 120, "pending save-loadout draft");
                Require(
                    saveDraftText.Contains("후보 포기", StringComparison.Ordinal)
                        && saveDraftText.Contains("저장 버튼: 이 슬롯 저장", StringComparison.Ordinal)
                        && saveDraftText.Contains("포기 버튼: 후보 소모", StringComparison.Ordinal)
                        && saveDraftText.Contains("빈 슬롯에 새 조합 저장", StringComparison.Ordinal),
                    "A pending save-loadout draft must expose both saving and skipping as valid candidate-consuming actions.");
                Require(saveDraftText.Contains("구간 +144", StringComparison.Ordinal), "A clean full clear must receive the expected segment-stability save quality bonus.");
                string clearMessage = backend.BuildRunClearResultMessage(record);
                RequireTextLayoutBudget(clearMessage, 19, 110, "normal clear result");
                Require(clearMessage.Contains("이번 런 전용", StringComparison.Ordinal), "The clear result must label its relic as a one-run effect.");
                Require(clearMessage.Contains("생존 x3", StringComparison.Ordinal), "The clear result must summarize accumulated gifts for the save candidate.");
                Require(clearMessage.Contains("집중: 발동", StringComparison.Ordinal), "The clear result must expose one compact focus-performance line.");
                Require(clearMessage.Contains("판정: 정박", StringComparison.Ordinal), "The clear result must separate the three timing-success axes.");
                Require(clearMessage.Contains("흐름: 집중 정밀", StringComparison.Ordinal), "A clean focused clear must be classified as focus precision.");
                Require(
                    clearMessage.Contains("회피: 점프", StringComparison.Ordinal)
                        && clearMessage.Contains("숙이기", StringComparison.Ordinal)
                        && clearMessage.Contains("비틀기", StringComparison.Ordinal)
                        && clearMessage.Contains("3동작", StringComparison.Ordinal),
                    "A clean clear result must keep the jump-slide-twist and three-action mastery summary in the result detail.");
                Require(clearMessage.Contains("복기: 전 구간 정확 흐름", StringComparison.Ordinal), "A clean clear must expose a compact success review line.");
                Require(
                    clearMessage.Contains("다음: 저장 조합에서 첫 슬롯 저장 또는 포기", StringComparison.Ordinal),
                    "A first clean clear must send the player toward saving or skipping the new candidate.");
                Require(clearMessage.Contains("정확 100% · 보정 0% · 실수 0%", StringComparison.Ordinal), "A clean clear must expose an exact judgement ratio.");
                Require(clearMessage.Contains("비교: 신기록 / 이전 최고 대비", StringComparison.Ordinal), "A new normal best must compare itself against the previous best instead of only saying current best.");
                Require(clearMessage.Contains("정확률 +20%p", StringComparison.Ordinal), "A new normal best must expose its exact-rate improvement.");
                var reloadedAfterNormalBestBackend = new GreedLastMockBackend();
                string reloadedNormalComparison = InvokeMockBackendLabel(reloadedAfterNormalBestBackend, "BuildLatestNormalComparisonText");
                Require(
                    reloadedNormalComparison.Contains("신기록 / 이전 최고 대비", StringComparison.Ordinal)
                        && reloadedNormalComparison.Contains("정확률 +20%p", StringComparison.Ordinal),
                    "Reloading after a normal-run new best must preserve the previous-best comparison basis.");
                VerifyPrototypeChecklist(
                    backend,
                    "일반 S 완료",
                    "저장 후보 우선",
                    "후보 처리 후 무한",
                    "성장 포인트 보유",
                    "기록 보드 확인 가능");
                Require(
                    InvokeMockBackendLabel(backend, "BuildPrototypeProgressChecklistStatusText") == "체크 4/7",
                    "The prototype progress counter must reach four checks after a clear creates a save candidate and record.");
                RequirePrototypeLoopStatus(
                    backend,
                    "루프 2/4",
                    "A clear with a pending save candidate");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 저장 후보 저장/포기", StringComparison.Ordinal),
                    "The prototype checklist must point a pending clear toward save confirmation or skip.");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 저장 후보 저장/포기 / 실행: 저장 조합", StringComparison.Ordinal),
                    "The prototype checklist must name the save-loadout button as the action for a pending save candidate.");
                var candidateGrowthHintBackend = new GreedLastMockBackend();
                candidateGrowthHintBackend.RegisterClear(record);
                string pendingCandidateGrowthNotice = string.Empty;
                for (int i = 0; i < 3; i += 1)
                {
                    candidateGrowthHintBackend.UpgradeGrowth(GreedLastRunChannel.Left, out pendingCandidateGrowthNotice);
                }

                Require(
                    pendingCandidateGrowthNotice.Contains("성장 포인트 부족", StringComparison.Ordinal)
                        && pendingCandidateGrowthNotice.Contains("저장 후보 처리 후 무한모드 또는 일반 런", StringComparison.Ordinal),
                    "A pending-candidate growth-board refill hint must not imply that only save confirmation can clear the candidate.");
                string saveCandidateLobbyMessage = InvokeMockBackendLabel(backend, "BuildLobbyStatusMessage", true);
                Require(
                    saveCandidateLobbyMessage.Contains("저장 후보 대기", StringComparison.Ordinal)
                        && saveCandidateLobbyMessage.Contains("저장 또는 포기", StringComparison.Ordinal)
                        && !InvokeRuntimeUiStaticBool("ShouldHideLobbyDetail", saveCandidateLobbyMessage),
                    "A pending save candidate must remain visible as an actionable lobby notice.");
                var skipSaveBackend = new GreedLastMockBackend();
                skipSaveBackend.RegisterClear(record);
                Require(skipSaveBackend.HasSaveLoadoutCandidate, "The skip-save test must begin from a pending save candidate.");
                string skipSaveDraftText = skipSaveBackend.BuildSaveLoadoutDraftText(showSelectedSlot: false);
                RequireTextLayoutBudget(skipSaveDraftText, 13, 120, "skip-save slot-choice draft");
                string explicitSlotChoiceDraftText = skipSaveDraftText
                    + "\n선택 전 저장 잠김 / 저장 포기는 후보 소모";
                RequireTextLayoutBudget(explicitSlotChoiceDraftText, 14, 120, "explicit save-slot choice draft");
                Require(
                    skipSaveDraftText.Contains("후보는 저장 또는 포기 뒤 사라집니다.", StringComparison.Ordinal),
                    "The save draft must explain that skipping consumes the candidate.");
                var skipSaveStateMachine = new GreedLastStateMachine(new GreedLastRequestGate());
                GreedLastStateSnapshot skipSaveDraftSnapshot = default;
                skipSaveStateMachine.StateChanged += value => skipSaveDraftSnapshot = value;
                skipSaveStateMachine.SetSaveLoadoutDraft(
                    skipSaveBackend.LoadLobby(),
                    "후보 포기 확인",
                    saveLoadoutSkipConfirmationPending: true,
                    saveLoadoutCandidateAvailable: true);
                Require(
                    skipSaveDraftSnapshot.Headline == "저장 후보 포기"
                        && skipSaveDraftSnapshot.SaveLoadoutSkipConfirmationPending,
                    "The skip-save confirmation state must publish an explicit destructive headline and flag.");
                skipSaveBackend.DiscardSaveLoadoutCandidate();
                GreedLastLobbySnapshot skippedSaveLobby = skipSaveBackend.LoadLobby();
                Require(
                    !skipSaveBackend.HasSaveLoadoutCandidate && !skippedSaveLobby.SaveLoadoutCandidateAvailable,
                    "Skipping save must consume the pending candidate immediately.");
                Require(
                    skippedSaveLobby.ProfileButtonSummary.Contains("보충", StringComparison.Ordinal)
                        && skippedSaveLobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "After skipping save, the profile button must point to refill instead of the consumed save candidate.");
                Require(
                    !Array.Exists(skippedSaveLobby.SaveSlotOccupied, occupied => occupied),
                    "Skipping save must not create or occupy any save-loadout slot.");
                var reloadedAfterSkipSaveBackend = new GreedLastMockBackend();
                GreedLastLobbySnapshot reloadedAfterSkipSaveLobby = reloadedAfterSkipSaveBackend.LoadLobby();
                Require(
                    !reloadedAfterSkipSaveBackend.HasSaveLoadoutCandidate
                        && !reloadedAfterSkipSaveLobby.SaveLoadoutCandidateAvailable
                        && !Array.Exists(reloadedAfterSkipSaveLobby.SaveSlotOccupied, occupied => occupied)
                        && reloadedAfterSkipSaveBackend.BuildSaveLoadoutDraftText().Contains("저장 후보 없음", StringComparison.Ordinal),
                    "A skipped save candidate must not reappear after backend reload or leave a phantom save chance.");
                string skippedSaveMessage = skipSaveBackend.BuildRunClearSkipSaveMessage(record);
                Require(
                    skippedSaveMessage.Contains("저장 조합 슬롯에는 저장하지 않았습니다.", StringComparison.Ordinal)
                        && skippedSaveMessage.Contains("저장 후보는 사라지고 기록 보드에는 남습니다.", StringComparison.Ordinal),
                    "The skip-save result must distinguish record persistence from save-slot storage.");
                Require(
                    !InvokeRuntimeUiStaticBool("ShouldHideLobbyDetail", skippedSaveMessage)
                        && InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", skippedSaveMessage) == "클리어 복기",
                    "The skip-save lobby notice must remain visible and route the record button to clear review.");
                string refillSkipSaveMessage = InvokeAppStaticLabel("BuildRefillClearSkipSaveMessage", skippedSaveMessage);
                RequireTextLayoutBudget(refillSkipSaveMessage, 4, 120, "refill skip-save lobby notice");
                Require(
                    refillSkipSaveMessage.StartsWith("보충 런 저장 안 함", StringComparison.Ordinal)
                        && refillSkipSaveMessage.Contains("기록:", StringComparison.Ordinal)
                        && refillSkipSaveMessage.Contains("다음: 무한 버튼으로 보충 런 재시도", StringComparison.Ordinal)
                        && !refillSkipSaveMessage.Contains("판정:", StringComparison.Ordinal)
                        && !refillSkipSaveMessage.Contains("저장 후보", StringComparison.Ordinal),
                    "A refill skip-save lobby notice must stay compact and record-only after saying the loadout was not updated.");
                GreedLastStateSnapshot skippedSaveSnapshot = BuildLobbyLabelSnapshot(
                    skippedSaveMessage,
                    saveCandidateAvailable: false,
                    infiniteUnlocked: true,
                    hasUsableSaveSlot: false,
                    hasOccupiedSaveSlot: false);
                Require(
                    InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", skippedSaveMessage) == "클리어 복기",
                    "A skipped-save clear must still expose its record button as clear review.");
                Require(
                    InvokeRuntimeUiLabel("BuildLobbyNormalRunButtonLabel", skippedSaveSnapshot) == "새 일반 런",
                    "A skipped-save clear must label the normal button as a new run.");
                string skippedSaveLobbyMessage = InvokeMockBackendLabel(skipSaveBackend, "BuildLobbyStatusMessage", false);
                Require(
                    skippedSaveLobbyMessage.Contains("저장 조합 없음", StringComparison.Ordinal)
                        && !skippedSaveLobbyMessage.Contains("저장 후보 대기", StringComparison.Ordinal),
                    "After skipping save, the lobby must move from candidate handling to refill guidance.");
                string skippedSaveRecordBoard = skipSaveBackend.BuildRecordBoardText(0);
                Require(
                    skippedSaveRecordBoard.Contains("일반 최고", StringComparison.Ordinal)
                        && !skippedSaveRecordBoard.Contains("저장 후보", StringComparison.Ordinal)
                        && !skippedSaveRecordBoard.Contains("다음:", StringComparison.Ordinal),
                    "Skipping save must leave a clean record-board entry without candidate or next-action text.");
                RequirePrototypeLoopStatus(skipSaveBackend, "루프 2/4", "A skipped-save profile");
                Require(
                    skipSaveBackend.BuildProfileBoardText(0).Contains("다음 확인: 무한 조합 보충 / 실행: 무한 조합 보충", StringComparison.Ordinal),
                    "A skipped-save profile must point the tester to refill instead of treating save as complete.");

                backend.SelectSaveLoadoutSlot(0);
                lobby = backend.ConfirmSaveLoadout();
                Require(!backend.HasSaveLoadoutCandidate && !lobby.SaveLoadoutCandidateAvailable, "Confirming a save must consume its candidate immediately.");
                Require(lobby.SaveSlotOccupied[0] && lobby.SaveSlotUsable[0], "The confirmed candidate must occupy its selected slot.");
                Require(
                    lobby.SaveSlotLabels[0].Contains("2/2", StringComparison.Ordinal)
                        && lobby.SaveSlotLabels[0].Contains("2회 남음", StringComparison.Ordinal),
                    "A new save loadout must start with exactly two clearly visible uses.");
                Require(
                    lobby.ProfileButtonSummary.Contains("성장", StringComparison.Ordinal)
                        && lobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "After confirming a save, remaining growth points must keep the profile button action-specific.");
                Require(
                    InvokeAppStaticInt("ResolveProfileBoardOpeningPage", lobby) == 1,
                    "A growth-action profile button must open profile directly on the storage/growth page.");
                string growthProfileText = backend.BuildProfileBoardText(1);
                Require(
                    growthProfileText.Contains("성장 흔적", StringComparison.Ordinal)
                        && growthProfileText.Contains("Enter/강조: 성장 보정", StringComparison.Ordinal),
                    "The growth-action profile page must show growth traces and the highlighted growth action.");
                var partialGrowthBackend = new GreedLastMockBackend();
                partialGrowthBackend.AddGrowthPointsForTest(1);
                partialGrowthBackend.UpgradeGrowth(GreedLastRunChannel.Left, out _);
                GreedLastLobbySnapshot partialGrowthLobby = partialGrowthBackend.AddGrowthPointsForTest(1);
                Require(
                    partialGrowthLobby.ProfileButtonSummary.Contains("성장", StringComparison.Ordinal)
                        && partialGrowthLobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "A profile with leftover growth points must keep the profile button action-specific even before the next upgrade cost is met.");
                Require(
                    partialGrowthBackend.BuildProfileBoardText(1).Contains("Enter/강조: 성장 보정", StringComparison.Ordinal),
                    "A leftover growth-point profile page must still highlight growth correction as the next action.");
                string refillResumeNotice = InvokeAppStaticLabel("BuildRefillResumeNotice", lobby);
                Require(
                    refillResumeNotice.Contains("보충 완료 - S1 2회 사용 가능", StringComparison.Ordinal)
                        && refillResumeNotice.Contains("무한모드", StringComparison.Ordinal),
                    "A refill save confirmation must name the refreshed slot and remaining uses before infinite preparation.");
                string activeLoadoutText = backend.BuildActiveLoadoutText();
                Require(activeLoadoutText.Contains("생존 x3", StringComparison.Ordinal), "A saved loadout must expose its accumulated gift direction.");
                Require(!activeLoadoutText.Contains(record.Relic, StringComparison.Ordinal), "A saved loadout detail must not display the one-run relic.");
                Require(
                    activeLoadoutText.Contains("입장 체크: 위협 폭·보상 / PgUp·PgDn", StringComparison.Ordinal),
                    "A saved loadout detail must remind the player to verify infinite threat width and reward before entry.");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 저장 조합으로 무한 시작", StringComparison.Ordinal),
                    "After confirming a save, the prototype checklist must point to the first infinite entry.");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 저장 조합으로 무한 시작 / 실행: 무한모드", StringComparison.Ordinal),
                    "After confirming a save, the prototype checklist must name the infinite button as the action for the first infinite entry.");
                RequirePrototypeLoopStatus(
                    backend,
                    "루프 3/4",
                    "A profile after confirming the saved loadout");
                string postConfirmDraftText = backend.BuildSaveLoadoutDraftText();
                RequireTextLayoutBudget(postConfirmDraftText, 14, 120, "post-confirm save-loadout management draft");
                Require(
                    postConfirmDraftText.Contains("저장 후보 없음", StringComparison.Ordinal)
                        && postConfirmDraftText.Contains("다음 클리어 후 1회", StringComparison.Ordinal)
                        && !postConfirmDraftText.Contains("후보 처리", StringComparison.Ordinal),
                    "After confirming a save, the save-loadout screen must switch to management mode without a second save chance.");
                var replacementCandidateBackend = new GreedLastMockBackend();
                replacementCandidateBackend.RegisterClear(record);
                replacementCandidateBackend.SelectSaveLoadoutSlot(0);
                replacementCandidateBackend.ConfirmSaveLoadout();
                replacementCandidateBackend.RegisterClear(comparisonBaseline);
                string replacementCandidateClearMessage = replacementCandidateBackend.BuildRunClearResultMessage(comparisonBaseline);
                Require(
                    replacementCandidateClearMessage.Contains("다음: 저장 조합에서 후보 비교 / 저장 또는 포기", StringComparison.Ordinal),
                    "A clear with an existing save slot must route the player to candidate comparison, saving, or skipping.");
                string replacementCandidateProfileText = replacementCandidateBackend.BuildProfileBoardText(1);
                Require(
                    replacementCandidateProfileText.Contains("저장 조합에서 후보 비교 후 저장/포기", StringComparison.Ordinal)
                        && replacementCandidateProfileText.Contains("필요 행동: 저장 후보를 슬롯에 저장 또는 포기", StringComparison.Ordinal),
                    "A replacement candidate profile page must expose compare, save, and skip paths before infinite re-entry.");
                string replacementCandidateDraftText = replacementCandidateBackend.BuildSaveLoadoutDraftText();
                Require(
                    replacementCandidateDraftText.Contains("작업: 후보 비교 / 저장·포기", StringComparison.Ordinal)
                        && replacementCandidateDraftText.Contains("저장 버튼: 덮어쓰기 확인 / 포기 버튼: 후보 소모", StringComparison.Ordinal)
                        && replacementCandidateDraftText.Contains("비교: 후보와 기존 슬롯 차이", StringComparison.Ordinal),
                    "A replacement candidate save draft must separate overwrite confirmation from candidate skipping.");
                var reloadedAfterConfirmBackend = new GreedLastMockBackend();
                GreedLastLobbySnapshot reloadedAfterConfirmLobby = reloadedAfterConfirmBackend.LoadLobby();
                Require(
                    !reloadedAfterConfirmBackend.HasSaveLoadoutCandidate
                        && !reloadedAfterConfirmLobby.SaveLoadoutCandidateAvailable,
                    "A confirmed save candidate must not reappear after backend reload.");
                Require(
                    reloadedAfterConfirmLobby.SaveSlotOccupied[0]
                        && reloadedAfterConfirmBackend.BuildSaveLoadoutDraftText().Contains("저장 후보 없음", StringComparison.Ordinal),
                    "Reloading after save confirmation must preserve the stored slot without creating a second save chance.");
                VerifyPrototypeChecklist(backend, "저장 조합 가능", "무한 대기");
                var firstInfiniteActionBackend = new GreedLastMockBackend();
                firstInfiniteActionBackend.RegisterClear(record);
                firstInfiniteActionBackend.SelectSaveLoadoutSlot(0);
                firstInfiniteActionBackend.ConfirmSaveLoadout();
                firstInfiniteActionBackend.ResetGrowthForTest();
                string firstInfiniteActionProfileText = firstInfiniteActionBackend.BuildProfileBoardText(0);
                Require(
                    firstInfiniteActionProfileText.Contains("저장 조합으로 무한모드 첫 기록 남기기 / 위협 폭·보상 확인", StringComparison.Ordinal),
                    "After growth points are handled, the profile next action must tell the player to read infinite threat width and reward on first entry. Actual: "
                        + firstInfiniteActionProfileText);
                Require(
                    firstInfiniteActionBackend.BuildProfileBoardText(2).Contains("다음 흔적: 저장 조합으로 무한모드 기록 / 위협 폭·보상", StringComparison.Ordinal),
                    "The profile badge page must connect the first infinite record goal to threat width/reward reading.");

                string usageBeforePreparation = lobby.SaveSlotLabels[0];
                string firstUseStartReadyText = backend.BuildInfiniteStartReadyText();
                Require(
                    firstUseStartReadyText.Contains("입장 체크: 위협 폭·보상 / PgUp·PgDn", StringComparison.Ordinal),
                    "The infinite preparation screen must include the threat-width/reward entry check.");
                lobby = backend.LoadLobby();
                Require(lobby.SaveSlotLabels[0] == usageBeforePreparation, "Opening infinite preparation must not consume a save use before start confirmation.");

                string labelAfterFirstConfirm = lobby.SaveSlotLabels[0];
                lobby = backend.ConfirmSaveLoadout();
                Require(lobby.SaveSlotLabels[0] == labelAfterFirstConfirm, "Confirming again without a candidate must not duplicate or refresh the save.");

                Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord firstUseRecord, out _, out _), "The first infinite entry must consume one save use.");
                Require(firstUseRecord.IsValid, "The first save use must return its stored loadout.");
                Require(string.IsNullOrEmpty(firstUseRecord.Relic), "A save loadout must not carry the one-run relic into infinite mode.");
                Require(!firstUseRecord.ChoiceSummary.Contains(record.Relic, StringComparison.Ordinal), "A save loadout summary must remove its one-run relic.");
                Require(!firstUseRecord.LoadoutName.Contains(record.Relic, StringComparison.Ordinal), "A save loadout name must remove its one-run relic.");
                lobby = backend.LoadLobby();
                Require(
                    lobby.SaveSlotLabels[0].Contains("마지막 1회", StringComparison.Ordinal)
                        && lobby.SaveSlotLabels[0].Contains("1/2", StringComparison.Ordinal),
                    "One clearly visible final use must remain after the first infinite entry.");
                string finalUseStartReadyText = backend.BuildInfiniteStartReadyText();
                Require(
                    finalUseStartReadyText.Contains("마지막 1회", StringComparison.Ordinal)
                        && finalUseStartReadyText.Contains("이번 시작 후 이 슬롯은 자동으로 비워집니다.", StringComparison.Ordinal),
                    "The final-use infinite preparation screen must warn that starting will auto-clear the slot.");
                Require(
                    finalUseStartReadyText.Contains("입장 체크: 위협 폭·보상 / PgUp·PgDn", StringComparison.Ordinal),
                    "The final-use infinite preparation screen must keep the threat-width/reward entry check visible.");
                string finalUseBriefText = backend.BuildInfiniteStartReadyBriefText();
                Require(
                    finalUseBriefText.Contains("시작 후 자동 비움", StringComparison.Ordinal),
                    "The final-use brief infinite preparation text must preserve the auto-clear warning.");
                Require(
                    finalUseBriefText.Contains("위협 체크", StringComparison.Ordinal),
                    "The brief infinite preparation text must keep the threat check visible.");

                var infiniteCore = new GreedLastRunCore();
                GreedLastRunSnapshot infiniteSnapshot = default;
                infiniteCore.SnapshotChanged += value => infiniteSnapshot = value;
                float infiniteClock = Time.realtimeSinceStartup + 500f;
                infiniteCore.EnterInfiniteRun(firstUseRecord);
                Require(infiniteSnapshot.InfiniteMode && infiniteSnapshot.StartCountdownActive, "A consumed save loadout must enter buffered infinite mode.");
                Require(infiniteSnapshot.ActiveInfiniteLoadoutName == firstUseRecord.LoadoutName, "Infinite mode must retain the consumed loadout identity.");
                Require(string.IsNullOrEmpty(infiniteSnapshot.ActiveRelic), "Infinite mode must start without a normal-run relic effect.");
                Require(infiniteSnapshot.SurvivalGiftStacks == 3, "Infinite mode must restore all stored survival gift stacks.");
                VerifyInfiniteSurvivalStackEffect(infiniteCore, ref infiniteSnapshot, ref infiniteClock);
                Require(infiniteSnapshot.InfiniteSectionsCleared == 1 && !infiniteSnapshot.Stopped, "Three resolved infinite patterns must clear one section and continue the run.");
                infiniteCore.ForceStopInfiniteRunForTest();
                GreedLastInfiniteRunRecord infiniteRecord = infiniteCore.CreateInfiniteRunRecord();
                Require(infiniteSnapshot.Stopped && infiniteRecord.IsValid, "Stopping infinite mode must create a valid record from the saved direction.");
                Require(
                    InvokeRuntimeUiLabel("BuildStoppedAvoidanceHudLine", infiniteSnapshot).Contains("회피 점프", StringComparison.Ordinal),
                    "The stopped infinite result HUD must expose compact avoidance performance.");
                Require(
                    InvokeRuntimeUiLabel("BuildStoppedAvoidanceReviewHudLine", infiniteSnapshot).Contains("회피 복기", StringComparison.Ordinal)
                        && InvokeRuntimeUiLabel("BuildStoppedAvoidanceReviewHudLine", infiniteSnapshot).Contains("→", StringComparison.Ordinal),
                    "The stopped infinite result HUD must point to the weakest avoidance action.");
                Require(infiniteRecord.TimingProfile.Contains("최근 구간 무한 1구간", StringComparison.Ordinal), "An infinite record must persist its latest completed section summary.");
                Require(backend.RegisterInfiniteRun(infiniteRecord), "The infinite classification test must register its generated record.");
                string infiniteResultMessage = backend.BuildInfiniteRunResultMessage(infiniteRecord);
                RequireTextLayoutBudget(infiniteResultMessage, 17, 120, "infinite result");
                Require(
                    infiniteResultMessage.Contains("정산 종료 기록", StringComparison.Ordinal)
                        && !infiniteResultMessage.Contains("테스트 종료 기록", StringComparison.Ordinal),
                    "Forced infinite result summaries must use settlement wording instead of test-stop wording.");
                Require(
                    infiniteResultMessage.Contains("다음: 정산 종료 확인 후 무한모드 실전 재개", StringComparison.Ordinal),
                    "Forced infinite result summaries must send the next action back to real infinite play.");
                Require(infiniteResultMessage.Contains("흐름: 위험 흐름", StringComparison.Ordinal), "A miss-heavy infinite record must be classified as a dangerous judgement flow.");
                Require(
                    infiniteResultMessage.Contains("회피: 점프", StringComparison.Ordinal)
                        && infiniteResultMessage.Contains("숙이기", StringComparison.Ordinal)
                        && infiniteResultMessage.Contains("비틀기", StringComparison.Ordinal)
                        && infiniteResultMessage.Contains("(정/보/실)", StringComparison.Ordinal),
                    "An infinite result detail must keep the compact jump-slide-twist avoidance summary.");
                Require(infiniteResultMessage.Contains("최근 구간: 무한 1구간 / 정확 0 · 보정 1 · 실수 2", StringComparison.Ordinal), "Infinite settlement must display its persisted latest-section performance.");
                Require(infiniteResultMessage.Contains("복기: 무한 1구간 / 정확 0", StringComparison.Ordinal), "An infinite result must preserve the latest section inside its review line.");
                Require(
                    infiniteResultMessage.Contains("저장 조합 소모:", StringComparison.Ordinal)
                        && infiniteResultMessage.Contains("마지막 1회 (1/2)", StringComparison.Ordinal),
                    "The first infinite result must show that the saved loadout has one final use left.");
                string infiniteAvoidanceWeakness = InvokeMockBackendStaticLabel("BuildAvoidanceWeaknessSummary", infiniteRecord.TimingProfile);
                Require(!string.IsNullOrEmpty(infiniteAvoidanceWeakness), "The infinite result sample must expose an avoidance weakness.");
                VerifyPrototypeChecklist(backend, "무한 1구간 기록", "저장 조합 가능");
                Require(
                    InvokeMockBackendLabel(backend, "BuildPrototypeProgressChecklistStatusText") == "체크 6/7",
                    "The prototype progress counter must reach six checks after the first infinite record but before auto-clear.");
                RequirePrototypeLoopStatus(
                    backend,
                    "루프 4/4",
                    "A profile after the first infinite record");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 저장 조합 2회 소진 / 실행: PgUp 소모", StringComparison.Ordinal),
                    "After the first infinite record, the prototype checklist must guide the tester to consume the final save-loadout use.");
                Require(
                    backend.BuildProfileBoardText(0).Contains("Enter/강조: 무한모드 - 정산 종료 확인 완료 / 실제 무한 런으로 생존 흐름 확인", StringComparison.Ordinal),
                    "After a forced infinite settlement, the profile next action must prefer real infinite play over review noise.");
                GreedLastInfiniteRunRecord coachedInfiniteRecord = WithInfiniteMissReason(infiniteRecord, "too_late");
                Require(backend.RegisterInfiniteRun(coachedInfiniteRecord), "The latest-section coaching test must register a non-test-stop infinite record.");
                string coachedInfiniteMessage = backend.BuildInfiniteRunResultMessage(coachedInfiniteRecord);
                Require(
                    InvokeMockBackendLabel(backend, "BuildInfiniteRunTrainingHint", coachedInfiniteRecord)
                        .Contains(infiniteAvoidanceWeakness, StringComparison.Ordinal),
                    "Infinite result training hints must carry the weakest avoidance drill.");
                Require(coachedInfiniteMessage.Contains("힌트: 무한 1구간 / 정확 0", StringComparison.Ordinal), "A real infinite stop must coach from its latest completed section first.");
                Require(coachedInfiniteMessage.Contains("목표: 다음 구간 무실수 / 직전 2", StringComparison.Ordinal), "A miss-heavy latest section must become the next infinite target.");
                Require(coachedInfiniteMessage.Contains("다음: 무한 복기 재도전으로 실수 구간 보완", StringComparison.Ordinal), "A miss-heavy infinite result should prioritize an infinite review retry.");
                var cleanBestHintBackend = new GreedLastMockBackend();
                var cleanBestHintRecord = new GreedLastInfiniteRunRecord(
                    true,
                    1280,
                    150f,
                    2,
                    2,
                    string.Empty,
                    string.Empty,
                    7,
                    0,
                    0,
                    5,
                    "manual_stop",
                    "평균 정확 / 정박 최고 4 / 집중 발동 1회"
                        + " / 최근 구간 무한 2구간 / 최근 구간 정확 3회 / 최근 구간 보정 0회 / 최근 구간 실수 0회");
                Require(cleanBestHintBackend.RegisterInfiniteRun(cleanBestHintRecord), "The clean infinite best-hint fixture must register.");
                string cleanBestHint = InvokeMockBackendLabel(cleanBestHintBackend, "BuildInfiniteRunTrainingHint", cleanBestHintRecord);
                Require(
                    cleanBestHint.Contains("다음 목표는 위협 3", StringComparison.Ordinal)
                        && cleanBestHint.Contains("폭·보상 확인", StringComparison.Ordinal)
                        && !cleanBestHint.Contains("다음 목표는 위험", StringComparison.Ordinal),
                    "A clean infinite new-best hint must use the threat term and point to width/reward reading. Actual: "
                        + cleanBestHint);
                string infiniteProfileText = backend.BuildProfileBoardText(0);
                Require(
                    infiniteProfileText.Contains("다음 행동", StringComparison.Ordinal)
                        && infiniteProfileText.Contains("무한 복기 전", StringComparison.Ordinal)
                        && infiniteProfileText.Contains("기초연습", StringComparison.Ordinal),
                    "The profile next action must keep the infinite weakness practice visible before retrying infinite mode.");
                VerifyInfiniteTerminalSegmentSettlement(backend);
                VerifyRecordBoardMinimalText(record, chapterMissAttempt, infiniteRecord);

                Require(
                    backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord secondUseRecord, out string secondUseDetail, out _),
                    "The second infinite entry must consume the final save use.");
                Require(secondUseRecord.IsValid, "The final save use must still return its stored loadout.");
                Require(string.IsNullOrEmpty(secondUseRecord.Relic), "A second entry must not inherit the previous run-local preparation relic.");
                Require(!secondUseRecord.ChoiceSummary.Contains("연쇄 축", StringComparison.Ordinal), "A second entry must not inherit the previous run-local preparation gift.");
                Require(secondUseDetail.Contains("사용 횟수 소진 - 슬롯 자동 비움", StringComparison.Ordinal), "The final save use must explain that the slot was cleared.");
                Require(secondUseDetail.Contains("남은 저장 조합 없음", StringComparison.Ordinal), "The final save use must point to replenishment when no slot remains.");
                string finalUseNoRecordMessage = backend.BuildInfiniteRunNoRecordMessage();
                Require(
                    finalUseNoRecordMessage.Contains("저장 조합 소모:", StringComparison.Ordinal)
                        && finalUseNoRecordMessage.Contains("자동 비움", StringComparison.Ordinal)
                        && finalUseNoRecordMessage.Contains("무한 조합 보충", StringComparison.Ordinal),
                    "After the final save use auto-clears, even a no-record infinite result must point to refill.");
                lobby = backend.LoadLobby();
                Require(!lobby.SaveSlotOccupied[0] && !lobby.SaveSlotUsable[0], "A save slot must clear itself immediately after its second use.");
                Require(
                    lobby.SaveSlotLabels[0] == "빈 슬롯",
                    "A two-use auto-cleared save slot must return to an empty visible slot label instead of keeping an expired use label.");
                Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _), "A cleared two-use slot must not be consumable again.");
                string emptyLoadoutLobbyMessage = InvokeMockBackendLabel(backend, "BuildLobbyStatusMessage", false);
                Require(
                    emptyLoadoutLobbyMessage.Contains("저장 조합 없음", StringComparison.Ordinal)
                        && emptyLoadoutLobbyMessage.Contains("보충 런", StringComparison.Ordinal)
                        && !InvokeRuntimeUiStaticBool("ShouldHideLobbyDetail", emptyLoadoutLobbyMessage),
                    "An empty save-loadout lobby must keep the refill-run notice visible.");
                var reloadedAfterAutoClearBackend = new GreedLastMockBackend();
                GreedLastLobbySnapshot reloadedAfterAutoClearLobby = reloadedAfterAutoClearBackend.LoadLobby();
                Require(
                    !Array.Exists(reloadedAfterAutoClearLobby.SaveSlotOccupied, occupied => occupied)
                        && !Array.Exists(reloadedAfterAutoClearLobby.SaveSlotUsable, usable => usable)
                        && !reloadedAfterAutoClearBackend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "A two-use auto-cleared save slot must remain empty and unusable after backend reload.");
                Require(
                    reloadedAfterAutoClearBackend.BuildSaveLoadoutDraftText().Contains("보충 안내 / 저장 조합 없음", StringComparison.Ordinal)
                        && reloadedAfterAutoClearBackend.BuildInfiniteStartReadyText().Contains("보충 런 버튼: 보충 런 진입", StringComparison.Ordinal),
                    "Reloading after two-use auto-clear must keep both save management and infinite entry pointed to refill.");
                Require(
                    InvokeAppStaticInt("ResolveProfileBoardOpeningPage", lobby) == 1,
                    "An empty save-loadout state must open profile directly on the storage/refill page.");
                Require(
                    InvokeAppStaticBool("ShouldStartLobbyNormalAsRefillRun", lobby),
                    "An empty save-loadout lobby must make the normal-run button enter the refill-run flow, not a plain normal run.");
                Require(
                    lobby.ProfileButtonSummary.Contains("보충", StringComparison.Ordinal)
                        && lobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "An auto-cleared empty save-loadout state must label the profile button as a new refill action.");
                string emptyLoadoutVerificationText = backend.BuildProfileBoardText(3);
                Require(
                    emptyLoadoutVerificationText.Contains("2 저장 조합: 보충 클리어 후 저장", StringComparison.Ordinal)
                        && emptyLoadoutVerificationText.Contains("3 무한모드: 조합 보충", StringComparison.Ordinal)
                        && emptyLoadoutVerificationText.Contains("무한 조합 보충 / 무한 위협 전 준비", StringComparison.Ordinal),
                    "The profile verification page must prioritize refill preparation when every save loadout is empty.");
                Require(
                    InvokeMockBackendLabel(backend, "BuildPrototypeProgressChecklistStatusText") == "체크 7/7",
                    "The prototype progress counter must stay complete after the two-use save loadout auto-clears.");
                RequirePrototypeLoopStatus(
                    backend,
                    "루프 4/4",
                    "A profile after the two-use save loadout auto-clears");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 보충 후 무한 재진입", StringComparison.Ordinal),
                    "After the last save slot auto-clears, the prototype checklist must point back to refill and re-entry.");
                Require(
                    backend.BuildProfileBoardText(0).Contains("다음 확인: 보충 후 무한 재진입 / 실행: 무한 조합 보충", StringComparison.Ordinal),
                    "After auto-clear, the prototype checklist must name the refill action needed for infinite re-entry.");
                var autoClearNoInfiniteBackend = new GreedLastMockBackend();
                autoClearNoInfiniteBackend.AddSampleSaveLoadoutForTest(out _);
                GreedLastLobbySnapshot autoClearNoInfiniteLobby =
                    autoClearNoInfiniteBackend.ConsumeSelectedSaveLoadoutUseForTest(out string firstDevConsumeNotice);
                Require(
                    firstDevConsumeNotice.Contains("저장 조합 소모:", StringComparison.Ordinal)
                        && firstDevConsumeNotice.Contains("사용 결과: 2/2", StringComparison.Ordinal)
                        && firstDevConsumeNotice.Contains("마지막 1회 (1/2)", StringComparison.Ordinal)
                        && autoClearNoInfiniteLobby.SaveSlotUsable[0],
                    "The PgUp dev save-consume path must spend the first use and leave one final visible use.");
                autoClearNoInfiniteLobby =
                    autoClearNoInfiniteBackend.ConsumeSelectedSaveLoadoutUseForTest(out string secondDevConsumeNotice);
                Require(
                    secondDevConsumeNotice.Contains("저장 조합 소모:", StringComparison.Ordinal)
                        && secondDevConsumeNotice.Contains("사용 횟수 소진 - 슬롯 자동 비움", StringComparison.Ordinal)
                        && !autoClearNoInfiniteLobby.SaveSlotOccupied[0],
                    "The second PgUp dev save-consume path must auto-clear the slot before any infinite record exists.");
                string autoClearNoInfiniteProfile = autoClearNoInfiniteBackend.BuildProfileBoardText(0);
                Require(
                    autoClearNoInfiniteProfile.Contains("다음 확인: 보충 후 무한 재진입", StringComparison.Ordinal)
                        && !autoClearNoInfiniteProfile.Contains("다음 확인: 무한 기록 생성", StringComparison.Ordinal),
                    "If test-only consumption clears the final slot before an infinite record, the checklist must prioritize refill over impossible infinite-record creation.");
                var completedLoopBackend = new GreedLastMockBackend();
                completedLoopBackend.RegisterClear(record);
                completedLoopBackend.SelectSaveLoadoutSlot(0);
                completedLoopBackend.ConfirmSaveLoadout();
                completedLoopBackend.RegisterClear(record);
                completedLoopBackend.SelectSaveLoadoutSlot(1);
                completedLoopBackend.ConfirmSaveLoadout();
                completedLoopBackend.SelectSaveLoadoutSlot(0);
                completedLoopBackend.ConsumeSelectedSaveLoadoutUseForTest(out _);
                completedLoopBackend.ConsumeSelectedSaveLoadoutUseForTest(out _);
                var cleanCompletedLoopInfiniteRecord = new GreedLastInfiniteRunRecord(
                    true,
                    1500,
                    120f,
                    2,
                    2,
                    "S2 안정",
                    "안정형",
                    9,
                    0,
                    0,
                    9,
                    string.Empty,
                    "최근 구간 무한 2구간 / 최근 구간 진행 3/3 / 최근 구간 정확 3회 / 최근 구간 보정 0회 / 최근 구간 실수 0회");
                Require(completedLoopBackend.RegisterInfiniteRun(cleanCompletedLoopInfiniteRecord), "The completed-loop final record fixture must register clean infinite progress.");
                string completedLoopProfile = completedLoopBackend.BuildProfileBoardText(0);
                Require(
                    completedLoopProfile.Contains("루프 4/4", StringComparison.Ordinal)
                        && completedLoopProfile.Contains("다음 확인: 완성 루프 기록 확인 / 실행: 기록 보기", StringComparison.Ordinal),
                    "A completed loop with another usable slot must guide the tester into the final record summary instead of refill or save-use checks.");
                Require(
                    completedLoopProfile.Contains("Enter/강조: 기록 보기 - 완성 루프 기록 확인", StringComparison.Ordinal),
                    "A completed loop must keep the profile next-action highlight on the final record check even when growth points remain.");
                Require(
                    !completedLoopProfile.Contains("다음 확인: 보충 후 무한 재진입", StringComparison.Ordinal)
                        && !completedLoopProfile.Contains("다음 확인: 저장 조합 2회 소진", StringComparison.Ordinal),
                    "The completed-loop final record check must only appear after refill and two-use save checks are no longer urgent.");
                GreedLastLobbySnapshot completedLoopLobby = completedLoopBackend.LoadLobby();
                Require(
                    completedLoopLobby.ProfileButtonSummary.Contains("완성", StringComparison.Ordinal)
                        && completedLoopLobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "A completed prototype loop must surface the final completion check on the profile button before passive growth.");
                Require(
                    completedLoopLobby.Message.Contains("프로토타입 완성 루프 확인", StringComparison.Ordinal)
                        && completedLoopLobby.Message.Contains("다음 확인: 완성 루프 기록 확인", StringComparison.Ordinal),
                    "A completed prototype loop must surface the final record check directly in the lobby message.");
                Require(
                    !InvokeRuntimeUiStaticBool("ShouldHideLobbyDetail", completedLoopLobby.Message),
                    "A completed prototype-loop lobby message must remain visible instead of being treated as default sync text.");
                Require(
                    InvokeRuntimeUiLabel("BuildLobbyRecordButtonLabel", completedLoopLobby.Message) == "완성 기록",
                    "A completed prototype-loop lobby message must label the record button as final completion records.");
                Require(
                    InvokeAppStaticLabel("ResolveLobbyPrimaryActionTarget", false, true, true, true, completedLoopLobby.Message) == "기록",
                    "A completed prototype-loop lobby message must keep Enter on the final records.");
                Require(
                    InvokeAppStaticInt("ResolveProfileBoardOpeningPage", completedLoopLobby) == 0,
                    "A completed prototype-loop profile button must open on the summary checklist page.");
                Require(
                    completedLoopBackend.ResolveProfileRecordBoardShortcutPageIndex() == 0,
                    "A completed prototype-loop profile center shortcut must open the record summary page.");
                GreedLastLobbySnapshot completedLoopSeenLobby = completedLoopBackend.MarkProfileSeen();
                Require(
                    completedLoopSeenLobby.ProfileButtonSummary.Contains("완성", StringComparison.Ordinal)
                        && !completedLoopSeenLobby.ProfileButtonSummary.Contains("NEW", StringComparison.Ordinal),
                    "After the completed-loop profile notice is seen, the profile button must keep its completion label without the NEW marker.");
                Require(
                    backend.BuildProfileBoardText(1).Contains("필요 행동: 무한 조합 보충", StringComparison.Ordinal)
                        && backend.BuildProfileBoardText(1).Contains("Enter/강조: 무한 조합 보충", StringComparison.Ordinal),
                    "The profile storage page must point empty save-loadout state and Enter highlight to refill.");
                Require(
                    backend.BuildProfileBoardText(1).Contains("무한 조합 보충 필요", StringComparison.Ordinal),
                    "The profile storage page must name the refill action after auto-clearing the last save loadout.");
                GreedLastLobbySnapshot emptyLoadoutConsumeLobby =
                    backend.ConsumeSelectedSaveLoadoutUseForTest(out string emptyLoadoutConsumeNotice);
                Require(
                    emptyLoadoutConsumeNotice.Contains("무한 버튼으로 보충 런 흐름", StringComparison.Ordinal)
                        && !emptyLoadoutConsumeNotice.Contains("먼저 저장 조합을 만들어야 합니다.", StringComparison.Ordinal)
                        && !Array.Exists(emptyLoadoutConsumeLobby.SaveSlotUsable, usable => usable),
                    "After auto-clear, PgUp save-consume feedback must point to refill rather than first-save creation.");
                string emptyLoadoutStartReadyText = backend.BuildInfiniteStartReadyText();
                Require(
                    emptyLoadoutStartReadyText.Contains("보충 런 버튼: 보충 런 진입", StringComparison.Ordinal)
                        && emptyLoadoutStartReadyText.Contains("다음: 보충 런 클리어 후 새 조합 저장", StringComparison.Ordinal),
                    "An empty infinite preparation screen must clearly say that the primary action starts a refill run.");
                string emptySaveLoadoutDraftText = backend.BuildSaveLoadoutDraftText();
                RequireTextLayoutBudget(emptySaveLoadoutDraftText, 12, 110, "empty save-loadout refill guidance draft");
                Require(
                    emptySaveLoadoutDraftText.Contains("보충 안내 / 저장 조합 없음", StringComparison.Ordinal)
                        && emptySaveLoadoutDraftText.Contains("다음: 보충 런 클리어 후 새 조합 저장", StringComparison.Ordinal)
                        && !emptySaveLoadoutDraftText.Contains("기존 슬롯 확인/변경/삭제", StringComparison.Ordinal),
                    "An empty save-loadout management screen must focus on refill guidance instead of stale slot management.");
                string emptySaveLoadoutSlotChoiceText = backend.BuildSaveLoadoutDraftText(showSelectedSlot: false);
                RequireTextLayoutBudget(emptySaveLoadoutSlotChoiceText, 10, 110, "empty save-loadout slot-choice refill guidance");
                Require(
                    emptySaveLoadoutSlotChoiceText.Contains("보충 안내 / 저장 조합 없음", StringComparison.Ordinal)
                        && emptySaveLoadoutSlotChoiceText.Contains("다음: 보충 런 클리어 후 새 조합 저장", StringComparison.Ordinal)
                        && !emptySaveLoadoutSlotChoiceText.Contains("좌 / 중 / 우로 확인할 슬롯 선택", StringComparison.Ordinal),
                    "An empty save-loadout slot-choice view must not ask the player to select unavailable slots.");
                string emptyLoadoutSelectText = backend.BuildInfiniteLoadoutSelectText();
                RequireTextLayoutBudget(emptyLoadoutSelectText, 14, 110, "empty infinite loadout selection refill guidance");
                Require(
                    emptyLoadoutSelectText.Contains("상태: 비어 있음 - 보충 런 필요", StringComparison.Ordinal)
                        && emptyLoadoutSelectText.Contains("보충 런 버튼: 보충 런 진입", StringComparison.Ordinal)
                        && emptyLoadoutSelectText.Contains("다음: 보충 런 클리어 후 새 조합 저장", StringComparison.Ordinal),
                    "An empty infinite loadout selection screen must guide the player into a refill run.");
                Require(
                    !emptyLoadoutSelectText.Contains("추천: 사용 가능한 저장 조합 없음", StringComparison.Ordinal),
                    "An empty infinite loadout selection screen must avoid redundant recommendation text after refill guidance is shown.");
                string emptyLoadoutDetailText = backend.BuildSelectedInfiniteLoadoutDetailText();
                Require(
                    emptyLoadoutDetailText.Contains("상태: 빈 슬롯", StringComparison.Ordinal)
                        && emptyLoadoutDetailText.Contains("보충 런 버튼: 보충 런 진입", StringComparison.Ordinal)
                        && emptyLoadoutDetailText.Contains("다음: 보충 런 클리어 후 새 조합 저장", StringComparison.Ordinal),
                    "An empty infinite loadout detail screen must keep the refill-run guidance instead of a dead selection state.");
                VerifyPrototypeChecklist(backend, "조합 보충 필요", "무한 1구간 기록", "최근 소진 자동 비움");
                for (int slotIndex = 0; slotIndex < 3; slotIndex += 1)
                {
                    backend.RegisterClear(record);
                    backend.SelectSaveLoadoutSlot(slotIndex);
                    backend.ConfirmSaveLoadout();
                }

                lobby = backend.LoadLobby();
                Require(
                    lobby.SaveSlotOccupied[0] && lobby.SaveSlotOccupied[1] && lobby.SaveSlotOccupied[2],
                    "The segment-quality recommendation test must begin with three occupied clean slots.");
                GreedLastRunRecord unstableLateRunRecord = BuildUnstableLateRunRecord(record);
                backend.RegisterClear(unstableLateRunRecord);
                string unstableDraftText = backend.BuildSaveLoadoutDraftText();
                RequireTextLayoutBudget(unstableDraftText, 18, 120, "weak-candidate save-loadout draft");
                Require(unstableDraftText.Contains("구간 -16", StringComparison.Ordinal), "Late chapter and escape instability must reduce save quality with the expected penalty.");
                Require(unstableDraftText.Contains("추천: 보류 / 기존 슬롯 유지", StringComparison.Ordinal), "A weaker late-run candidate must not be recommended over three clean stored loadouts.");
                VerifyLegacyRelicSaveMigration(record, backendSaveKey);
            }
            finally
            {
                if (hadSavedData)
                {
                    PlayerPrefs.SetString(backendSaveKey, savedData);
                }
                else
                {
                    PlayerPrefs.DeleteKey(backendSaveKey);
                }

                PlayerPrefs.Save();
            }
        }

        private static void VerifyStoredGiftStackScaling()
        {
            string singleSurvival = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(
                BuildStoredGiftRecord("생존 축", "생존 축"));
            string stackedSurvival = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(
                BuildStoredGiftRecord("생존 축", "생존 축 / 생존 축 / 생존 축"));
            Require(singleSurvival.Contains("체력 +1", StringComparison.Ordinal), "One survival gift must grant one infinite health bonus.");
            Require(stackedSurvival.Contains("체력 +2", StringComparison.Ordinal), "Accumulated survival gifts must raise the infinite health bonus cap.");

            string singleFocus = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(
                BuildStoredGiftRecord("집중 축", "집중 축"));
            string stackedFocus = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(
                BuildStoredGiftRecord("집중 축", "집중 축 / 집중 축 / 집중 축"));
            Require(singleFocus.Contains("집중 +2", StringComparison.Ordinal), "One focus gift must preserve its base infinite focus bonus.");
            Require(stackedFocus.Contains("집중 +3", StringComparison.Ordinal), "Accumulated focus gifts must reach the infinite focus cap.");

            string singleChain = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(
                BuildStoredGiftRecord("연쇄 축", "연쇄 축"));
            string stackedChain = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(
                BuildStoredGiftRecord("연쇄 축", "연쇄 축 / 연쇄 축 / 연쇄 축"));
            Require(singleChain.Contains("콤보 +2", StringComparison.Ordinal), "One chain gift must preserve its base infinite combo bonus.");
            Require(stackedChain.Contains("콤보 +5", StringComparison.Ordinal), "Accumulated chain gifts must raise the infinite combo bonus cap.");
            Require(stackedChain.Contains("점수 +180", StringComparison.Ordinal), "Accumulated chain gifts must carry their score direction into infinite mode.");
        }

        private static void VerifyStoredGiftStacksRestoreIntoRun()
        {
            var focusCore = new GreedLastRunCore();
            GreedLastRunSnapshot focusSnapshot = default;
            focusCore.SnapshotChanged += value => focusSnapshot = value;
            focusCore.EnterInfiniteRun(BuildStoredGiftRecord("집중 축", "집중 축 / 집중 축 / 집중 축"));
            Require(focusSnapshot.FocusGiftStacks == 3, "Infinite mode must restore three focus stacks from the save summary.");
            Require(focusSnapshot.Focus == 0 && focusSnapshot.FocusStateActive, "Three focus stacks must activate the focus state on infinite entry.");
            Require(focusSnapshot.FocusStateActivationCount == 1, "Infinite entry focus activation must be counted once.");
            Require(
                GreedLastRunCore.BuildInfiniteLoadoutStartStatePreview(
                    BuildStoredGiftRecord("집중 축", "집중 축 / 집중 축 / 집중 축"),
                    0,
                    0,
                    0).Contains("집중 상태", StringComparison.Ordinal),
                "The infinite start preview must show immediate focus-state activation.");

            var chainCore = new GreedLastRunCore();
            GreedLastRunSnapshot chainSnapshot = default;
            chainCore.SnapshotChanged += value => chainSnapshot = value;
            float clock = Time.realtimeSinceStartup + 350f;
            chainCore.EnterInfiniteRun(BuildStoredGiftRecord("연쇄 축", "연쇄 축 / 연쇄 축 / 연쇄 축"));
            Require(chainSnapshot.ChainGiftStacks == 3 && chainSnapshot.Combo == 5, "Three chain stacks must restore their capped entry combo.");
            StartBufferedPattern(chainCore, ref chainSnapshot, ref clock);
            int comboBeforeExact = chainSnapshot.Combo;
            float exactAt = clock + chainSnapshot.Pattern.CoreSeconds - chainCore.InputTimingOffsetSeconds;
            chainCore.HandleInput(chainSnapshot.Pattern.Channel, exactAt);
            Require(chainSnapshot.LastResult == GreedLastJudgementResult.Success, "The restored chain test must resolve an exact input.");
            Require(chainSnapshot.Combo == comboBeforeExact + 3, "Three restored chain stacks must add three combo on exact input.");
        }

        private static void VerifyInfiniteRoutePayoffRestoration()
        {
            var stableCore = new GreedLastRunCore();
            GreedLastRunSnapshot stableSnapshot = default;
            stableCore.SnapshotChanged += value => stableSnapshot = value;
            float stableClock = Time.realtimeSinceStartup + 380f;
            stableCore.EnterInfiniteRun(BuildStoredGiftRecord(
                "집중 축",
                "집중 축 / 안정 루트 / 안정 루트"));
            Require(stableSnapshot.ActiveInfiniteLoadoutProfile.Contains("안정 루트", StringComparison.Ordinal), "Infinite mode must restore the dominant stable route.");
            Require(stableSnapshot.ActiveRouteProfile == "안정 루트", "Infinite mode must expose the restored stable route as live state.");
            StartBufferedPattern(stableCore, ref stableSnapshot, ref stableClock);
            Require(stableSnapshot.Pattern.Prompt.Contains("[안정]", StringComparison.Ordinal), "Restored stable loadouts must mark their infinite traps.");
            GreedLastRunChannel wrongChannel = stableSnapshot.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right
                : GreedLastRunChannel.Left;
            float missAt = stableClock + stableSnapshot.Pattern.CoreSeconds - stableCore.InputTimingOffsetSeconds;
            stableCore.HandleInput(wrongChannel, missAt);
            stableClock = missAt;
            int damagedHealth = stableSnapshot.Health;
            stableCore.RequestNextPattern(stableClock += 2f);
            float goodDelta = (stableSnapshot.Pattern.SuccessWindowSeconds + stableSnapshot.Pattern.GoodWindowSeconds) * 0.5f;
            stableCore.HandleInput(
                stableSnapshot.Pattern.Channel,
                stableClock + stableSnapshot.Pattern.CoreSeconds + goodDelta - stableCore.InputTimingOffsetSeconds);
            Require(
                stableSnapshot.StableRouteRecoveryCount == 1
                    && stableSnapshot.Health == damagedHealth + GreedLastRunCore.StableRouteGoodHealthBonus,
                "A stored stable route must retain its Good recovery contract in infinite mode.");

            var variationCore = new GreedLastRunCore();
            GreedLastRunSnapshot variationSnapshot = default;
            variationCore.SnapshotChanged += value => variationSnapshot = value;
            float variationClock = Time.realtimeSinceStartup + 410f;
            variationCore.EnterInfiniteRun(BuildStoredGiftRecord(
                "생존 축",
                "생존 축 / 변주 루트 / 변주 루트"));
            Require(variationSnapshot.ActiveRouteProfile == "변주 루트", "Infinite mode must expose the restored variation route as live state.");
            ResolveInfiniteRouteCenterHit(
                variationCore,
                ref variationSnapshot,
                ref variationClock,
                GreedLastTimingType.Offbeat,
                "stored variation route");
            Require(
                variationSnapshot.VariationRouteFlowCount == 1
                    && variationSnapshot.JudgementText.Contains("변주 루트 - 엇박 공명", StringComparison.Ordinal),
                "A stored variation route must retain its Offbeat focus contract in infinite mode.");

            var pressureCore = new GreedLastRunCore();
            GreedLastRunSnapshot pressureSnapshot = default;
            pressureCore.SnapshotChanged += value => pressureSnapshot = value;
            float pressureClock = Time.realtimeSinceStartup + 440f;
            pressureCore.EnterInfiniteRun(BuildStoredGiftRecord(
                "생존 축",
                "생존 축 / 압박 루트 / 압박 루트"));
            Require(pressureSnapshot.ActiveRouteProfile == "압박 루트", "Infinite mode must expose the restored pressure route as live state.");
            ResolveInfiniteRouteCenterHit(
                pressureCore,
                ref pressureSnapshot,
                ref pressureClock,
                GreedLastTimingType.Clutch,
                "stored pressure route");
            Require(
                pressureSnapshot.PressureRouteCounterCount == 1
                    && pressureSnapshot.JudgementText.Contains("압박 루트 - 클러치 증폭", StringComparison.Ordinal),
                "A stored pressure route must retain its Clutch score contract in infinite mode.");
        }

        private static void ResolveInfiniteRouteCenterHit(
            GreedLastRunCore core,
            ref GreedLastRunSnapshot snapshot,
            ref float clock,
            GreedLastTimingType targetTiming,
            string context)
        {
            StartBufferedPattern(core, ref snapshot, ref clock);
            for (int i = 0; i < 12; i += 1)
            {
                Require(snapshot.ActivePattern, context + " must keep an active trap while searching for its route axis.");
                bool targetFound = snapshot.Pattern.TimingType == targetTiming;
                float exactAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(snapshot.Pattern.Channel, exactAt);
                clock = exactAt;
                Require(snapshot.LastResult == GreedLastJudgementResult.Success, context + " search inputs must stay exact.");
                if (targetFound)
                {
                    return;
                }

                core.RequestNextPattern(clock += 2f);
            }

            throw new InvalidOperationException(context + " did not expose its route timing axis within twelve traps.");
        }

        private static void VerifyInfinitePreparationIsRunLocal()
        {
            GreedLastRunRecord stored = BuildStoredGiftRecord("생존 축", "생존 축 / 생존 축 / 생존 축");
            GreedLastRunRecord prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(
                stored,
                "연쇄 축",
                "파라오의 각인");
            Require(string.IsNullOrEmpty(stored.Relic), "The stored loadout must remain relic-free before preparation.");
            Require(!stored.ChoiceSummary.Contains("연쇄 축", StringComparison.Ordinal), "Infinite preparation must not mutate the stored gift history.");
            Require(prepared.Relic == "파라오의 각인", "Infinite preparation must attach its selected run-local relic.");
            Require(prepared.ChoiceSummary.Contains("연쇄 축", StringComparison.Ordinal), "Infinite preparation must append one run-local gift adjustment.");

            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            float clock = Time.realtimeSinceStartup + 420f;
            core.EnterInfiniteRun(prepared);
            Require(snapshot.SurvivalGiftStacks == 3 && snapshot.ChainGiftStacks == 1, "Prepared infinite mode must combine stored and run-local gift stacks.");
            Require(snapshot.ActiveRelic == "파라오의 각인", "Prepared infinite mode must expose its run-local relic.");
            StartBufferedPattern(core, ref snapshot, ref clock);
            int comboBeforeExact = snapshot.Combo;
            float exactAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
            core.HandleInput(snapshot.Pattern.Channel, exactAt);
            Require(snapshot.LastResult == GreedLastJudgementResult.Success, "Prepared infinite mode must resolve its first exact input.");
            Require(snapshot.Combo == comboBeforeExact + 3, "The prepared chain gift and Pharaoh relic must amplify exact combo together.");
            Require(snapshot.JudgementText.Contains("파라오의 각인", StringComparison.Ordinal), "Exact feedback must name the run-local relic that amplified the chain.");
        }

        private static void VerifyInfiniteSurvivalStackEffect(
            GreedLastRunCore core,
            ref GreedLastRunSnapshot snapshot,
            ref float clock)
        {
            StartBufferedPattern(core, ref snapshot, ref clock);
            int startingHealth = snapshot.Health;
            Require(startingHealth >= 5, "Three stored survival gifts must enter infinite mode at the health bonus cap.");
            for (int i = 0; i < 2; i += 1)
            {
                GreedLastRunChannel wrongChannel = snapshot.Pattern.Channel == GreedLastRunChannel.Left
                    ? GreedLastRunChannel.Right
                    : GreedLastRunChannel.Left;
                float missAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(wrongChannel, missAt);
                clock = missAt;
                Require(snapshot.LastResult == GreedLastJudgementResult.Miss, "The stored survival test must take two real hits.");
                core.RequestNextPattern(clock += 2f);
                Require(snapshot.ActivePattern, "The stored survival test must continue after each hit.");
            }

            float goodDelta = (snapshot.Pattern.SuccessWindowSeconds + snapshot.Pattern.GoodWindowSeconds) * 0.5f;
            float goodAt = clock + snapshot.Pattern.CoreSeconds + goodDelta - core.InputTimingOffsetSeconds;
            core.HandleInput(snapshot.Pattern.Channel, goodAt);
            clock = goodAt;
            Require(snapshot.LastResult == GreedLastJudgementResult.Good, "The stored survival test must finish inside the Good band.");
            Require(snapshot.Health == startingHealth, "Three survival stacks must recover two health on Good and restore the entry cap.");
            Require(snapshot.JudgementText.Contains("생존 축 x3", StringComparison.Ordinal), "Good feedback must name the stored survival stack that restored health.");
            Require(snapshot.LastCompletedSegment == "무한 1구간", "A completed infinite bundle must expose its section identity.");
            Require(
                snapshot.LastCompletedSegmentExactCount == 0
                    && snapshot.LastCompletedSegmentGoodCount == 1
                    && snapshot.LastCompletedSegmentMissCount == 2,
                "The first infinite section must publish its 0/1/2 judgement summary.");
            Require(snapshot.JudgementText.Contains("무한 1구간 판정 정확 0 / 보정 1 / 실수 2", StringComparison.Ordinal), "Infinite section feedback must expose its compact judgement settlement.");
            Require(
                snapshot.CurrentSegmentExactCount == 0
                    && snapshot.CurrentSegmentGoodCount == 0
                    && snapshot.CurrentSegmentMissCount == 0,
                "Completing an infinite section must clear the live section counters before the next section.");
        }

        private static void VerifyInfiniteTerminalSegmentSettlement(GreedLastMockBackend backend)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot snapshot = default;
            core.SnapshotChanged += value => snapshot = value;
            float clock = Time.realtimeSinceStartup + 600f;
            core.EnterInfiniteRun(BuildStoredGiftRecord("연쇄 축", "연쇄 축"));
            StartBufferedPattern(core, ref snapshot, ref clock);

            for (int i = 0; i < 3; i += 1)
            {
                GreedLastRunChannel wrongChannel = snapshot.Pattern.Channel == GreedLastRunChannel.Left
                    ? GreedLastRunChannel.Right
                    : GreedLastRunChannel.Left;
                float missAt = clock + snapshot.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(wrongChannel, missAt);
                clock = missAt;
                Require(snapshot.LastResult == GreedLastJudgementResult.Miss, "Every terminal-section test input must resolve as a real miss.");
                Require(snapshot.CurrentSegmentMissCount == i + 1, "The live infinite section counter must advance after every real miss.");
                if (i < 2)
                {
                    Require(!snapshot.Stopped, "The terminal-section test must survive until its third hit.");
                    core.RequestNextPattern(clock += 2f);
                    Require(snapshot.ActivePattern, "The terminal-section test must expose its next trap before the fatal hit.");
                }
            }

            Require(snapshot.Stopped && snapshot.Health == 0, "The third terminal-section miss must end infinite mode at zero health.");
            GreedLastInfiniteRunRecord record = core.CreateInfiniteRunRecord();
            Require(record.IsValid, "A real infinite death must create a terminal-section record.");
            Require(record.TimingProfile.Contains("종료 구간 무한 1구간", StringComparison.Ordinal), "A real death must persist the section where the run ended.");
            Require(record.TimingProfile.Contains("종료 구간 진행 3/3", StringComparison.Ordinal), "The terminal section must preserve resolved-pattern progress.");
            Require(record.TimingProfile.Contains("종료 구간 실수 3회", StringComparison.Ordinal), "The terminal section must preserve all fatal-section misses.");
            Require(backend.RegisterInfiniteRun(record), "The terminal-section record must register for result coaching.");

            string resultMessage = backend.BuildInfiniteRunResultMessage(record);
            RequireTextLayoutBudget(resultMessage, 17, 120, "terminal infinite result");
            Require(resultMessage.Contains("종료 구간: 무한 1구간 3/3 / 정확 0 · 보정 0 · 실수 3", StringComparison.Ordinal), "Infinite results must prioritize the section where the player actually died.");
            Require(resultMessage.Contains("힌트: 무한 1구간 3/3 / 정확 0", StringComparison.Ordinal), "Infinite coaching must start from the terminal section instead of an older completed section.");
            Require(resultMessage.Contains("복기: 무한 1구간 3/3 / 정확 0", StringComparison.Ordinal), "Infinite review must prioritize the terminal death section.");
            Require(resultMessage.Contains("목표: 다음 구간 무실수 / 종료 구간 3", StringComparison.Ordinal), "The next target must use terminal-section misses.");
        }

        private static void VerifyRecordBoardMinimalText(
            GreedLastRunRecord clearRecord,
            GreedLastRunAttemptRecord attemptRecord,
            GreedLastInfiniteRunRecord infiniteRecord)
        {
            var boardBackend = new GreedLastMockBackend();
            boardBackend.RegisterClear(clearRecord);
            Require(
                boardBackend.ResolveRecentRecordBoardPageIndex() == 1,
                "After a normal clear, the profile record-board shortcut should open the normal record page.");
            boardBackend.RegisterNormalAttempt(attemptRecord);
            Require(
                boardBackend.ResolveRecentRecordBoardPageIndex() == 1,
                "After a normal attempt, the profile record-board shortcut should keep opening the normal record page.");
            boardBackend.RegisterNormalAttempt(new GreedLastRunAttemptRecord(
                attemptRecord.IsValid,
                attemptRecord.Outcome,
                attemptRecord.Score,
                attemptRecord.Health,
                attemptRecord.Combo,
                attemptRecord.Focus,
                attemptRecord.MaxCombo,
                attemptRecord.SuccessCount,
                attemptRecord.GoodCount,
                attemptRecord.MissCount,
                attemptRecord.Distance,
                attemptRecord.ChapterIndex,
                attemptRecord.ChapterProgress,
                attemptRecord.ChapterTarget,
                attemptRecord.CoreRetrieved,
                attemptRecord.MissReason,
                attemptRecord.ChoiceSummary,
                attemptRecord.TimingProfile + " / 복기 목표 1장 무실수 / 현재 1 / 복기 결과 실패 실수 1"));
            string attemptSummaryText = boardBackend.BuildRecordBoardText(0);
            Require(
                attemptSummaryText.Contains("최근 시도", StringComparison.Ordinal),
                "The record summary must distinguish recent failed or abandoned attempts from recent clears.");
            string profileRepresentativeText = boardBackend.BuildProfileBoardText(2);
            RequireTextLayoutBudget(profileRepresentativeText, 16, 92, "profile representative record with recent attempt");
            Require(
                profileRepresentativeText.Contains("대표 기록", StringComparison.Ordinal)
                    && profileRepresentativeText.Contains("일반 ", StringComparison.Ordinal)
                    && profileRepresentativeText.Contains("최근 시도", StringComparison.Ordinal),
                "The profile representative page must show clean best records plus a distinct recent attempt line.");
            Require(
                !profileRepresentativeText.Contains("복기 목표", StringComparison.Ordinal)
                    && !profileRepresentativeText.Contains("기초연습", StringComparison.Ordinal)
                    && !profileRepresentativeText.Contains("다음 확인", StringComparison.Ordinal),
                "The profile representative page must keep coaching text out of compact record rows.");
            boardBackend.RegisterInfiniteRun(infiniteRecord);
            Require(
                boardBackend.ResolveRecentRecordBoardPageIndex() == 2,
                "After an infinite record, the profile record-board shortcut should open the infinite record page.");
            Require(
                boardBackend.ResolveProfileRecordBoardShortcutPageIndex() == boardBackend.ResolveRecentRecordBoardPageIndex(),
                "Before the completed-loop final check, the profile center shortcut must keep opening the most recent record page.");

            for (int page = 0; page < boardBackend.RecordBoardPages; page += 1)
            {
                string boardText = boardBackend.BuildRecordBoardText(page);
                RequireTextLayoutBudget(boardText, 5, 72, "record board page " + page);
                Require(!boardText.Contains("타이밍:", StringComparison.Ordinal), "Record boards must not show timing-detail labels.");
                Require(!boardText.Contains("후보", StringComparison.Ordinal), "Record boards must not show save-candidate guidance.");
                Require(!boardText.Contains("목표:", StringComparison.Ordinal), "Record boards must not show coaching goals.");
                Require(!boardText.Contains("복기 목표", StringComparison.Ordinal), "Record boards must not show review-target result details.");
                Require(!boardText.Contains("가이드", StringComparison.Ordinal), "Record boards must not show live guide text.");
                Require(!boardText.Contains("코칭", StringComparison.Ordinal), "Record boards must not show live coaching text.");
                Require(!boardText.Contains("상태 ", StringComparison.Ordinal), "Record boards must not show live status fragments.");
                Require(!boardText.Contains("상태:", StringComparison.Ordinal), "Record boards must not show colon status fragments.");
                Require(!boardText.Contains("판정:", StringComparison.Ordinal), "Record boards must not show judgement-detail fragments.");
                Require(!boardText.Contains("작업:", StringComparison.Ordinal), "Record boards must not show save-management action fragments.");
                Require(!boardText.Contains("사용 결과", StringComparison.Ordinal), "Record boards must not show save-use flow fragments.");
                Require(!boardText.Contains("선택:", StringComparison.Ordinal), "Record boards must not show save-slot selection fragments.");
                Require(!boardText.Contains("중앙/레인 큐", StringComparison.Ordinal), "Record boards must not show prototype cue checklist text.");
                Require(!boardText.Contains("1박 전", StringComparison.Ordinal), "Record boards must not show active beat-count cue text.");
                Require(!boardText.Contains("선택 ", StringComparison.Ordinal), "Record boards must not show route-choice detail text.");
                Require(!boardText.Contains("정답키", StringComparison.Ordinal), "Record boards must not show editor key hints.");
                Require(!boardText.Contains("정답 좌 탭", StringComparison.Ordinal), "Record boards must not show mobile miss correction hints.");
                Require(!boardText.Contains("터치 탭", StringComparison.Ordinal), "Record boards must not show mobile touch checklist hints.");
                Require(!boardText.Contains("성장 포인트", StringComparison.Ordinal), "Record boards must not show growth-point result text.");
                Require(!boardText.Contains("입력 보정", StringComparison.Ordinal), "Record boards must not show input-calibration advice.");
                Require(!boardText.Contains("감각:", StringComparison.Ordinal), "Record boards must not show sensory setting text.");
                Require(!boardText.Contains("보정:", StringComparison.Ordinal), "Record boards must not show calibration setting text.");
                Require(!boardText.Contains("소리", StringComparison.Ordinal), "Record boards must not show audio setting text.");
                Require(!boardText.Contains("진동", StringComparison.Ordinal), "Record boards must not show haptic setting text.");
                Require(
                    !boardText.Contains("점프 정확", StringComparison.Ordinal)
                        && !boardText.Contains("숙이기 정확", StringComparison.Ordinal)
                        && !boardText.Contains("비틀기 정확", StringComparison.Ordinal),
                    "Record boards must keep avoidance analytics inside result details instead of ranked list rows.");
                Require(
                    !boardText.Contains("정확 +", StringComparison.Ordinal)
                        && !boardText.Contains("정확+", StringComparison.Ordinal)
                        && !boardText.Contains("보정 +", StringComparison.Ordinal)
                        && !boardText.Contains("충전+", StringComparison.Ordinal)
                        && !boardText.Contains("반격 +", StringComparison.Ordinal)
                        && !boardText.Contains("연쇄 +", StringComparison.Ordinal),
                    "Record boards must not show live reward-delta coaching fragments.");
            }

            string summaryText = boardBackend.BuildRecordBoardText(0);
            Require(summaryText.Contains("일반 최고", StringComparison.Ordinal), "The summary board must show the best normal record.");
            Require(summaryText.Contains("무한 최고", StringComparison.Ordinal), "The summary board must show the best infinite record.");

            string normalText = boardBackend.BuildRecordBoardText(1);
            Require(normalText.Contains("1위", StringComparison.Ordinal), "The normal board must present ranked clear records.");
            Require(!normalText.Contains("시도 기록", StringComparison.Ordinal), "The normal board must show attempts as records without an extra section heading.");
            Require(normalText.Contains("탈출 중단", StringComparison.Ordinal), "The normal board must keep abandoned attempts as compact records.");
            Require(
                normalText.Contains("탈출 중단", StringComparison.Ordinal)
                    && normalText.Contains("콤" + attemptRecord.MaxCombo, StringComparison.Ordinal),
                "The normal attempt board must keep max combo as a compact record metric.");
            Require(normalText.Contains("시도1", StringComparison.Ordinal), "The normal attempt board must label failed or abandoned attempts without adding a noisy section heading.");
            var denseNormalBoardBackend = new GreedLastMockBackend();
            for (int i = 0; i < 4; i += 1)
            {
                denseNormalBoardBackend.RegisterClear(BuildRecordBoardRunVariant(record, i));
            }

            for (int i = 0; i < 3; i += 1)
            {
                denseNormalBoardBackend.RegisterNormalAttempt(BuildRecordBoardAttemptVariant(attemptRecord, i));
            }

            string denseNormalText = denseNormalBoardBackend.BuildRecordBoardText(1);
            RequireTextLayoutBudget(denseNormalText, 4, 72, "dense normal record board");
            Require(
                denseNormalText.Contains("1위", StringComparison.Ordinal)
                    && denseNormalText.Contains("2위", StringComparison.Ordinal)
                    && !denseNormalText.Contains("3위", StringComparison.Ordinal)
                    && denseNormalText.Contains("시도1", StringComparison.Ordinal)
                    && denseNormalText.Contains("탈출 중단", StringComparison.Ordinal),
                "A dense normal record board must keep top clears plus recent attempts without expanding into a noisy long list.");

            string infiniteText = boardBackend.BuildRecordBoardText(2);
            Require(infiniteText.Contains("최고", StringComparison.Ordinal), "The infinite board must show the best infinite run.");
            Require(infiniteText.Contains("콤", StringComparison.Ordinal), "The infinite board must keep max combo as a compact record metric.");

            string rankingText = boardBackend.BuildRecordBoardText(3);
            Require(rankingText.Contains("1위", StringComparison.Ordinal), "The infinite ranking board must present compact ranked records.");
            Require(
                InvokeRuntimeUiLabel("GetRecordBoardPageLabel", 0) == "요약"
                    && InvokeRuntimeUiLabel("GetRecordBoardPageLabel", 1).Contains("일반", StringComparison.Ordinal)
                    && InvokeRuntimeUiLabel("GetRecordBoardPageLabel", 2).Contains("무한", StringComparison.Ordinal)
                    && InvokeRuntimeUiLabel("GetRecordBoardPageLabel", 3).Contains("랭킹", StringComparison.Ordinal),
                "Record-board lane labels must expose previous, summary, and next page navigation.");
            Require(
                InvokeRuntimeUiLabel("GetRecordBoardPageButtonLabel", 1) == "2/4 일반 기록"
                    && InvokeRuntimeUiLabel("GetRecordBoardPageButtonLabel", -1) == "4/4 무한 랭킹"
                    && InvokeRuntimeUiLabel("GetProfileBoardPageButtonLabel", 3) == "4/4 검증",
                "Record/profile board buttons must include page numbers for long playtest navigation.");
            Require(
                InvokeRuntimeUiLabel("BuildProfileBoardCenterLaneLabel") == "기록\n보드",
                "The profile-board center lane must clearly announce its record-board shortcut.");
            string sanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1000점 / 복기: 무한 1구간 / 복기 목표 1장 무실수 / 현재 1 / 복기 결과 실패 실수 1 / 다음: 복기 재도전 / 힌트: 밝아진 목표 레인만 보고 입력 / 훈련 힌트: 밝아진 목표 레인만 보고 입력 / 숙이기 실수 1회 / 기초연습 숙이기 단계 / 다음 행동 / 복기 약점 숙이기 실수 1회 / 콤9 / 후보 저장");
            Require(
                sanitizedBoardLine == "[S] 1000점 / 콤9",
                "The record board compactor must strip coaching, next-action, and save-candidate fragments.");
            string prototypeChecklistSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1120점 / 체크 6/7 / 다음 확인: 저장 조합 2회 소진 / 실행: PgUp 소모 / Enter/강조: 무한모드 / 콤10");
            Require(
                prototypeChecklistSanitizedBoardLine == "[S] 1120점 / 콤10",
                "The record board compactor must strip prototype next-check and action fragments even when they contain numbers.");
            string mobileHintSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 900점 / 정답키 A/← / 정답 좌 탭 / 터치 탭 / 위험→동작·탭·1박·보상 / 위험→동작·키·1박·보상 / 위험 + 동작 + 키 + 1박 전 + 보상 / 바닥 위험 → ↑ 점프 회피 · A/← · 1박 전 · 정확+140 집중+1 보정+25 / 바닥→↑점프 · 충전+1 / 상단→↓숙이기 · 반격+50 / 측면→↔비틀기 · 연쇄+1 / 좌 탭 / 콤5");
            Require(
                mobileHintSanitizedBoardLine == "[S] 900점 / 콤5",
                "The record board compactor must strip editor key, mobile touch, active action cue, and lane payoff fragments.");
            string recentFlowSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 860점 / 최근 좌정·바→점 > 중압·상→숙 / 흐름: 피하기 확인 / 콤4");
            Require(
                recentFlowSanitizedBoardLine == "[A] 860점 / 콤4",
                "The record board compactor must strip live recent hazard-flow fragments.");
            string symbolGuideSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 910점 / 위험→동작 바닥↑점 상단↓숙 측면↔틀 / 바닥→↑점프 / 상단→↓숙이기 / 측면→↔비틀기 / 콤5");
            Require(
                symbolGuideSanitizedBoardLine == "[A] 910점 / 콤5",
                "The record board compactor must strip symbolized hazard-action guide fragments.");
            string hazardWeaknessSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 930점 / 회피 복기 상단→↓숙이기 실수 1회 / 복기 약점 상단→↓숙이기 실수 1회 / 기초연습 숙이기 단계 / 콤7");
            Require(
                hazardWeaknessSanitizedBoardLine == "[A] 930점 / 콤7",
                "The record board compactor must strip hazard-bound avoidance review fragments.");
            string shortActionCueSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 920점 / 바닥 위험 → ↑ 점프 회피 / 상단 위험 → ↓ 숙이기 회피 / 측면 위험 → ↔ 비틀기 회피 / 콤6");
            Require(
                shortActionCueSanitizedBoardLine == "[A] 920점 / 콤6",
                "The record board compactor must strip short active action cue fragments without relying on reward text.");
            string uiHintSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 777점 / 가이드 좌 점프 / 상태 동작 ↑ / 코칭 회피 점프 / 재개 목표 카운트 뒤 첫 함정 / 중앙/레인 큐+상태줄 확인: 위험→동작·키·1박·보상 / 일시정지 배경차단 / Enter/강조: 저장 조합 / 보정: +35ms / 감각: 소리 80%·진동 ON / 콤4");
            Require(
                uiHintSanitizedBoardLine == "[A] 777점 / 콤4",
                "The record board compactor must strip live guide, pause, cue checklist, and sensory fragments.");
            string growthSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1300점 / 성장 포인트 +3 / 보유 6pt / 생존 강화 가능 / 콤8");
            Require(
                growthSanitizedBoardLine == "[S] 1300점 / 콤8",
                "The record board compactor must strip growth-point and upgrade fragments.");
            string calibrationSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 910점 / 입력 보정: 현재 설정 안정 / 입력 보정 제안: 살짝 빠르게 / 콤5");
            Require(
                calibrationSanitizedBoardLine == "[A] 910점 / 콤5",
                "The record board compactor must strip input-calibration advice fragments.");
            string saveManagementSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 777점 / 관리 모드 / 저장 후보 없음 / 기존 슬롯만 확인 / 이름 변경 / 삭제 / 새 저장은 다음 클리어 후 1회만 가능 / 상세 보기: 기존 저장 조합 기록 확인 / 보충 안내 / 보충 런 버튼: 보충 런 진입 / 무한 시작 전 저장 조합 보충 필요 / 콤4");
            Require(
                saveManagementSanitizedBoardLine == "[A] 777점 / 콤4",
                "The record board compactor must strip save-management mode fragments even when they contain numbers.");
            string saveUseSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1180점 / 저장 조합 소모: 슬롯 1 / 사용 결과: 2/2 · 2회 남음 -> 마지막 1회 (1/2) / 남은 사용 1회 / 다음 소모 후 슬롯 자동 비움 / 콤8");
            Require(
                saveUseSanitizedBoardLine == "[S] 1180점 / 콤8",
                "The record board compactor must strip save-use consumption notices from playable record rows.");
            string refillRunSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 다음: 보충 런 클리어 후 새 조합 저장 / 다음 확인: 보충 후 무한 재진입 / 실행: 무한 조합 보충 / 보충 런 버튼: 보충 런 진입 / 콤12");
            Require(
                refillRunSanitizedBoardLine == "[S] 1500점 / 콤12",
                "The record board compactor must strip refill-run next-action and button guidance from playable record rows.");
            string pendingCandidateRouteSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1420점 / 후보 처리 후 무한 / 무한 후보 저장 필요 / 저장 후보 우선 / 콤11");
            Require(
                pendingCandidateRouteSanitizedBoardLine == "[S] 1420점 / 콤11",
                "The record board compactor must strip pending-candidate routing guidance from playable record rows.");
            string saveCandidateButtonGuidanceBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1410점 / 저장 조합에서 후보 비교 후 저장/포기 / 저장 버튼: 덮어쓰기 확인 / 포기 버튼: 후보 소모 / 콤10");
            Require(
                saveCandidateButtonGuidanceBoardLine == "[S] 1410점 / 콤10",
                "The record board compactor must strip save-candidate button guidance from playable record rows.");
            string saveCandidateChoiceGuidanceBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1410점 / 저장 후보 대기 / 저장 기회: 저장하거나 포기하면 사라짐 / 좌 · 중 · 우 슬롯을 고른 뒤 저장 또는 포기를 선택하세요. / 콤10");
            Require(
                saveCandidateChoiceGuidanceBoardLine == "[S] 1410점 / 콤10",
                "The record board compactor must strip save-or-skip candidate guidance from playable record rows.");
            string settlementStopSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1190점 / 힌트: 정산 종료 기록 / 실제 생존 흐름은 다음 무한 런에서 확인 / 복기: 정산 종료 -> 실제 플레이로 무한 종료 기록 확인 / 콤8");
            Require(
                settlementStopSanitizedBoardLine == "[S] 1190점 / 콤8",
                "The record board compactor must strip forced-settlement hints from playable record rows.");
            string settlementReentrySanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1190점 / 다음: 정산 종료 확인 후 무한모드 실전 재개 / Enter/강조: 무한모드 / 콤8");
            Require(
                settlementReentrySanitizedBoardLine == "[S] 1190점 / 콤8",
                "The record board compactor must strip forced-settlement re-entry next actions from playable record rows.");
            string settlementReentryButtonLabelSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1190점 / 무한 실전 재개 S1 마지막 A / 실전 재개 / 콤8");
            Require(
                settlementReentryButtonLabelSanitizedBoardLine == "[S] 1190점 / 콤8",
                "The record board compactor must strip settlement real-run re-entry button fragments from playable record rows.");
            string settlementReentryNoticeSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1190점 / 정산 종료 확인 완료 / 이번 입장은 복기 목표 없이 실제 무한 런 흐름을 확인합니다. / 콤8");
            Require(
                settlementReentryNoticeSanitizedBoardLine == "[S] 1190점 / 콤8",
                "The record board compactor must strip settlement real-run re-entry preparation notices from playable record rows.");
            string saveCandidateSummaryBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "저장 모드 / 1회 후보\n집중 축 S1 / S / 집중형·안정 / 1740점 / 82.1m\n품질 2277 / S / 집중형·안정 / 집중 x2 / 집중 S / 집중 발동 2 / 정박 5 / 조합 시작 감각 + 집중 x2 + 황금 박동");
            Require(
                saveCandidateSummaryBoardLine == "기록 없음",
                "The record board compactor must not treat save-candidate summary rows as playable records.");
            string saveCandidateComparisonBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "후보: 집중 축 S1 / S / 집중형·안정 / 1740점 / 82.1m / 품질 2277 / 조합 시작 감각 + 집중 x2\n현재 품질: 품질 1980 / A / 생존형·변주 / 생존 x1 / 차이: 품질 +297");
            Require(
                saveCandidateComparisonBoardLine == "기록 없음",
                "The record board compactor must strip candidate/current quality comparison rows instead of keeping their score-like numbers.");
            string devShortcutSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 888점 / F1 무적 / F2 기프트·F3 유물·F4 핵·F5 탈출 / F6 집중 / F7 목표·F8 경로·F9 동작 / PgUp·PgDn·End / 콤6");
            Require(
                devShortcutSanitizedBoardLine == "[S] 888점 / 콤6",
                "The record board compactor must strip long-playtest shortcut fragments.");
            string threatReadSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1280점 / 목표: 위협 3 진입 / 현재 2 / 폭·보상 확인 / 다음: 저장 조합으로 무한모드 기록 갱신 / 콤5");
            Require(
                threatReadSanitizedBoardLine == "[S] 1280점 / 콤5",
                "The record board compactor must strip threat-width/reward coaching fragments.");
            string threatChangeSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1300점 / 테스트 위협 상승 - 위협 5/5 최고 / 위험폭 최대 / 정확+48~58 / 첫 함정은 유예 패턴으로 폭·보상 확인 / 콤7");
            Require(
                threatChangeSanitizedBoardLine == "[S] 1300점 / 콤7",
                "The record board compactor must strip threat-change test feedback fragments.");
            string automaticThreatSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 1500점 / 위협 상승 - 위협 3/5 압박 / 위험폭 확대 / 정확+24~34 / 짧은 호흡 뒤 함정 속도와 판정 압박 증가 / 콤8");
            Require(
                automaticThreatSanitizedBoardLine == "[A] 1500점 / 콤8",
                "The record board compactor must strip automatic threat-rise feedback fragments.");
            string multiLineResultSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "탈출 성공 / 기록 저장\n[S] 1234점 / 저장 후보: 집중 축 S1 / 다음: 저장 후보 저장 / 콤8\n판정: 정박 3 / 흐름: 집중 정밀 / 상태: 자동 진행");
            Require(
                multiLineResultSanitizedBoardLine == "[S] 1234점 / 콤8",
                "The record board compactor must scan multiline result messages and keep only the actual record line.");
            string statusSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[B] 640점 / 상태: 자동 진행 / 채널: 중 / 판정: Perfect / 흐름: 집중 정밀 / 집중: 발동 / 회피: 점프 / 사용 결과: 2/2 -> 1/2 / 콤3");
            Require(
                statusSanitizedBoardLine == "[B] 640점 / 콤3",
                "The record board compactor must strip status, judgement, focus, and use-flow fragments.");
            string autoFlowSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 770점 / 자동 진행 / 자동 정지 / 자동 비움 / GO 뒤 자동 진행 복구 / 콤4");
            Require(
                autoFlowSanitizedBoardLine == "[A] 770점 / 콤4",
                "The record board compactor must strip standalone auto-flow and auto-clear status fragments.");
            string saveDraftSanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "관리 모드 / 저장 후보 없음\n슬롯\n> 슬롯 1: 집중 축 S1 / S / 정박+집중 / 1107점 / 2/2 · 2회 남음\n선택: 슬롯 1 / 상태: 2/2 · 2회 남음\n작업: 상세 보기 / 이름 변경 / 삭제\n다음: 상세 보기");
            Require(
                saveDraftSanitizedBoardLine == "기록 없음",
                "The record board compactor must not treat save-slot management rows as score records.");
            string emptySanitizedBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "복기: 무한 1구간 / 힌트: 숙이기 단계 / 복기 목표 1장 무실수 / 다음: 복기 재도전 / 복기 약점 숙이기 실수 1회 / 기초연습 숙이기 단계 / 후보 저장");
            Require(
                emptySanitizedBoardLine == "기록 없음",
                "A record-board line made only of coaching fragments must collapse to an empty record.");
            string nonMetricBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "추천 루트 안정 / 최고 대비 유지 / 검증 순서 확인 / 체감 목표 유지");
            Require(
                nonMetricBoardLine == "기록 없음",
                "A record-board line without score, distance, combo, or count metrics must not survive as a record.");
            string looseNumberBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "2/4 점검 / 완료 1회 / 보정 100ms / 입력 보정 0ms");
            Require(
                looseNumberBoardLine == "기록 없음",
                "Record-board compaction must require record units, not just arbitrary checklist or settings numbers.");
            string settingsInRecordBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 보정 100ms / 입력 보정 0ms / 콤12");
            Require(
                settingsInRecordBoardLine == "[S] 1500점 / 콤12",
                "Record-board compaction must keep score rows but strip input-correction settings fragments.");
            string comparisonDeltaOnlyBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "비교: 최고 대비 점수 +120 / 거리 +8.0m / 구간 +1 / 콤보 +2 / 실수 -1 / 정확률 +20%p");
            Require(
                comparisonDeltaOnlyBoardLine == "기록 없음",
                "Record-board compaction must not keep comparison-delta fragments as standalone records.");
            string scoreWithComparisonDeltaBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 비교: 신기록 / 점수 +120 / 거리 +8.0m / 콤보 +2 / 실수 -1 / 정확률 +20%p / 콤12");
            Require(
                scoreWithComparisonDeltaBoardLine == "[S] 1500점 / 콤12",
                "Record-board compaction must strip comparison deltas while preserving the actual score row.");
            string scoreWithResourceDeltaBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 체력 +1 / 집중 -1 / 콤12");
            Require(
                scoreWithResourceDeltaBoardLine == "[S] 1500점 / 콤12",
                "Record-board compaction must strip health and focus comparison deltas from score rows.");
            string scoreWithRewardDeltaBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 바닥→↑점프 정확 +24 / 보정 +12 / 정박 중앙 집중 +1 / 보정 체력 +1 / 압박 반격 +35 / 엇박 연쇄 +1 / 콤12");
            Require(
                scoreWithRewardDeltaBoardLine == "[S] 1500점 / 콤12",
                "Record-board compaction must strip live reward-delta coaching from score rows.");
            string buttonNavigationBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "2/4 버튼 / 페이지 이동 / 다음 기록 / 이전 기록 / 체크 7/7");
            Require(
                buttonNavigationBoardLine == "기록 없음",
                "Record-board navigation labels with numbers must not survive as record rows.");
            string prototypeOnlyBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "체크 7/7 / 루프 4/4 / 다음 확인: 보충 후 무한 재진입 / 실행: 무한 조합 보충 / Enter/강조: 무한 조합 보충");
            Require(
                prototypeOnlyBoardLine == "기록 없음",
                "Prototype checklist-only lines must not survive as record rows.");
            string profileSummaryBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "성과 요약\n일반 2회  클리어 1 / 실패 1 / 중단 0\n클리어율 50%  무한 기록 1회 / 루프 4/4");
            Require(
                profileSummaryBoardLine == "기록 없음",
                "Profile summary lines with completion-loop metrics must not survive as record rows.");
            string prototypeCompletionLoopBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 완성 루프: 일반→저장→무한→기록 / 프로토 완성 기준 / 완성 기준 확인 / 콤12");
            Require(
                prototypeCompletionLoopBoardLine == "[S] 1500점 / 콤12",
                "The record board compactor must strip prototype completion-loop checklist fragments.");
            string prototypeGoalBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 프로토타입: 기획 체감 검증 / 기획 체감 검증용 / 체감 목표: 피하기 + 정확 타이밍 / 완성 기록 / 콤12");
            Require(
                prototypeGoalBoardLine == "[S] 1500점 / 콤12",
                "The record board compactor must strip prototype-goal fragments from score rows.");
            string prototypeCompletionNoticeBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[S] 1500점 / 프로토타입 완성 루프 확인 / 다음 확인: 완성 루프 기록 확인 / 흔적 완성 NEW / 콤12");
            Require(
                prototypeCompletionNoticeBoardLine == "[S] 1500점 / 콤12",
                "The record board compactor must strip completed-loop lobby/profile notice fragments from score rows.");
            string prototypeLoopProgressBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 900점 / 루프 진행 3/4 / 완성 진행: 저장 확정 전 / 콤6");
            Require(
                prototypeLoopProgressBoardLine == "[A] 900점 / 콤6",
                "The record board compactor must strip prototype loop-progress variants.");
            string statusLinePrototypeBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "상태줄 보상·3동작 HUD / 동작 ↑ 1/3→↓ / 중앙/레인 큐+상태줄 확인: 위험→동작·키·1박·보상");
            Require(
                statusLinePrototypeBoardLine == "기록 없음",
                "Status-line and three-action HUD checklist text must not survive as record rows.");
            string infiniteWeaknessBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[A] 880점 / 무한 약점 바닥→↑점프 실1 / 연습 점프 / 콤5");
            Require(
                infiniteWeaknessBoardLine == "[A] 880점 / 콤5",
                "The record board compactor must strip compressed infinite weakness checklist fragments.");
            string compressedPracticeBoardLine = InvokeMockBackendStaticLabel(
                "CompactRecordBoardLine",
                "[B] 720점 / 복기 약점 측면→↔비틀기 실2 / 연습 비틀기 / 기초연습 비틀기 단계 / 콤3");
            Require(
                compressedPracticeBoardLine == "[B] 720점 / 콤3",
                "The record board compactor must strip compressed practice-step review fragments.");
        }

        private static void VerifyPrototypeChecklist(GreedLastMockBackend backend, params string[] expectedFragments)
        {
            Require(backend.ProfileBoardPages == 4, "The profile board must expose summary, storage, badge, and verification pages.");
            string profileText = backend.BuildProfileBoardText(0);
            RequireTextLayoutBudget(profileText, 12, 92, "profile prototype checklist");
            Require(profileText.Contains("프로토타입 체크", StringComparison.Ordinal), "The profile board must expose a prototype checklist.");
            Require(
                profileText.Contains("기획 체감 검증", StringComparison.Ordinal),
                "The profile checklist must state that this build is validating the planned prototype feel.");
            Require(
                profileText.Contains("체감 목표: 피하기 + 정확 타이밍", StringComparison.Ordinal),
                "The profile checklist must keep the prototype goal visible on the first page.");
            Require(
                profileText.Contains("체크 ", StringComparison.Ordinal)
                    && profileText.Contains("/7", StringComparison.Ordinal),
                "The profile checklist must expose compact prototype progress counting.");
            Require(
                profileText.Contains("루프 ", StringComparison.Ordinal)
                    && profileText.Contains("/4", StringComparison.Ordinal),
                "The profile checklist must expose the four-step prototype completion loop.");
            Require(
                profileText.Contains("클리어율", StringComparison.Ordinal)
                    && profileText.Contains("무한 기록", StringComparison.Ordinal)
                    && profileText.Contains(InvokeMockBackendLabel(backend, "BuildPrototypeCompletionLoopStatusText"), StringComparison.Ordinal),
                "The profile summary must include the current completion-loop status before the checklist.");
            Require(profileText.Contains("다음 확인:", StringComparison.Ordinal), "The profile checklist must name the next concrete playtest checkpoint.");
            Require(profileText.Contains("실행:", StringComparison.Ordinal), "The profile checklist must name the button or test shortcut that runs the next checkpoint.");
            Require(profileText.Contains("Enter/강조:", StringComparison.Ordinal), "The profile checklist must name the currently highlighted lobby action.");
            Require(profileText.Contains("중앙/레인 큐", StringComparison.Ordinal), "The profile checklist must expose active action and lane cue status together.");
            Require(profileText.Contains("상태줄", StringComparison.Ordinal), "The profile checklist must expose status-line payoff readability from the first page.");
            Require(
                profileText.Contains("위험→동작·키·1박·보상", StringComparison.Ordinal),
                "The profile checklist must describe the hazard, active action cue timing, editor key hint, and payoff preview.");
            Require(
                InvokeMockBackendStaticLabel("BuildPrototypeCueContractText", false) == "위험→동작·탭·1박·보상",
                "The profile cue contract must switch to mobile tap wording outside dev/editor mode.");
            Require(profileText.Contains("위협 폭·보상", StringComparison.Ordinal), "The profile checklist must expose the infinite threat read check.");
            Require(profileText.Contains("감각 보정:", StringComparison.Ordinal), "The profile checklist must expose the current calibration and sensory setting check.");
            Require(
                GreedLastRuntimeUi.BuildSensorySettingsChecklistText(0.6f, false) == "감각: 소리 60%·진동 OFF",
                "The sensory checklist helper must expose concrete audio and haptic values.");
            Require(profileText.Contains("소리", StringComparison.Ordinal) && profileText.Contains("진동", StringComparison.Ordinal), "The profile checklist must expose current audio and haptic values.");
            float previousTimingOffset = PlayerPrefs.GetFloat(GreedLastRunCore.InputTimingOffsetPrefsKey, 0f);
            bool hadPreviousTimingOffset = PlayerPrefs.HasKey(GreedLastRunCore.InputTimingOffsetPrefsKey);
            try
            {
                PlayerPrefs.SetFloat(GreedLastRunCore.InputTimingOffsetPrefsKey, 0.035f);
                Require(
                    GreedLastRunCore.BuildStoredInputTimingOffsetChecklistText() == "보정: +35ms",
                    "The stored input calibration checklist must expose the clamped PlayerPrefs value.");
                string calibratedProfileText = backend.BuildProfileBoardText(0);
                Require(
                    calibratedProfileText.Contains("감각 보정:+35ms", StringComparison.Ordinal),
                    "The profile checklist must reflect the stored input calibration value.");
                PlayerPrefs.SetFloat(GreedLastRunCore.InputTimingOffsetPrefsKey, 5f);
                Require(
                    GreedLastRunCore.BuildStoredInputTimingOffsetChecklistText() == "보정: +200ms",
                    "The stored input calibration checklist must clamp overly late calibration values.");
                string maxCalibratedProfileText = backend.BuildProfileBoardText(0);
                RequireTextLayoutBudget(maxCalibratedProfileText, 12, 92, "max-calibrated profile prototype checklist");
                Require(
                    maxCalibratedProfileText.Contains("감각 보정:+200ms", StringComparison.Ordinal),
                    "The profile checklist must keep max late calibration visible without breaking layout.");
                PlayerPrefs.SetFloat(GreedLastRunCore.InputTimingOffsetPrefsKey, -5f);
                Require(
                    GreedLastRunCore.BuildStoredInputTimingOffsetChecklistText() == "보정: -200ms",
                    "The stored input calibration checklist must clamp overly early calibration values.");
                string minCalibratedProfileText = backend.BuildProfileBoardText(0);
                RequireTextLayoutBudget(minCalibratedProfileText, 12, 92, "min-calibrated profile prototype checklist");
                Require(
                    minCalibratedProfileText.Contains("감각 보정:-200ms", StringComparison.Ordinal),
                    "The profile checklist must keep max early calibration visible without breaking layout.");
            }
            finally
            {
                if (hadPreviousTimingOffset)
                {
                    PlayerPrefs.SetFloat(GreedLastRunCore.InputTimingOffsetPrefsKey, previousTimingOffset);
                }
                else
                {
                    PlayerPrefs.DeleteKey(GreedLastRunCore.InputTimingOffsetPrefsKey);
                }

                PlayerPrefs.Save();
            }
            for (int i = 0; i < expectedFragments.Length; i += 1)
            {
                Require(
                    profileText.Contains(expectedFragments[i], StringComparison.Ordinal),
                    "The profile checklist must include: " + expectedFragments[i]);
            }

            string verificationText = backend.BuildProfileBoardText(3);
            RequireTextLayoutBudget(verificationText, 14, 92, "profile prototype verification order");
            Require(verificationText.Contains("검증 순서", StringComparison.Ordinal), "The profile board must expose a verification-order page.");
            Require(verificationText.Contains("Enter/강조:", StringComparison.Ordinal), "The verification page must preserve the highlighted lobby action summary.");
            Require(verificationText.Contains("1 일반 런", StringComparison.Ordinal), "The verification page must start with the normal-run check.");
            Require(verificationText.Contains("위험→동작·박자", StringComparison.Ordinal), "The verification order must start normal-run testing from hazard-action readability.");
            Require(verificationText.Contains("박자·보상", StringComparison.Ordinal), "The verification page must remind the player to check rhythm and score-reward feedback.");
            Require(verificationText.Contains("상태줄", StringComparison.Ordinal), "The verification page must include the bottom status-line reward check.");
            Require(verificationText.Contains("루트 버튼", StringComparison.Ordinal), "The verification page must include the route-button timing/action check.");
            Require(verificationText.Contains("2 저장 조합", StringComparison.Ordinal), "The verification page must include the save-loadout check.");
            Require(
                verificationText.Contains("저장 없이 로비", StringComparison.Ordinal)
                    && verificationText.Contains("클리어 복기", StringComparison.Ordinal),
                "The verification page must include the skip-save lobby summary and clear-review button check.");
            Require(verificationText.Contains("2회 소진", StringComparison.Ordinal), "The verification page must remind the player to test two-use save-loadout clearing.");
            if (backend.HasSaveLoadoutCandidate)
            {
                Require(
                    profileText.Contains("저장 후보 우선", StringComparison.Ordinal),
                    "A pending candidate profile summary must make saving or skipping the candidate the immediate priority.");
                Require(
                    profileText.Contains("후보 처리 후 무한", StringComparison.Ordinal),
                    "A pending candidate profile summary must show that infinite mode waits for save confirmation or skip.");
                Require(
                    verificationText.Contains("후보 저장/포기", StringComparison.Ordinal),
                    "A pending candidate verification page must remind the player to test both save confirmation and skip.");
                Require(
                    verificationText.Contains("3 무한모드: 후보 처리 후 시작", StringComparison.Ordinal),
                    "A pending candidate verification page must prevent the player from entering infinite before save confirmation or skip.");
            }

            Require(verificationText.Contains("3 무한모드", StringComparison.Ordinal), "The verification page must include the infinite-mode check.");
            Require(verificationText.Contains("위협·정산·실전 재개", StringComparison.Ordinal), "The verification page must include the forced-settlement-to-real-run check.");
            Require(verificationText.Contains("4 기록/흔적", StringComparison.Ordinal), "The verification page must finish with records and profile review.");
            Require(verificationText.Contains("기록만", StringComparison.Ordinal), "The verification page must remind the player to check that record boards stay record-only.");
            Require(verificationText.Contains("2/4 버튼", StringComparison.Ordinal), "The verification page must include record/profile page-button navigation.");
            Require(verificationText.Contains("체감 목표", StringComparison.Ordinal), "The verification page must keep the prototype experience goal visible.");
            Require(verificationText.Contains("동작 확인", StringComparison.Ordinal), "The verification page must include the jump-slide-twist action shortcut check.");
            Require(verificationText.Contains("위험→동작", StringComparison.Ordinal), "The verification page must bind hazards to avoidance actions.");
            Require(verificationText.Contains("바닥↑점", StringComparison.Ordinal), "The verification page must expose the compact floor-to-jump cue.");
            Require(verificationText.Contains("3동작 HUD", StringComparison.Ordinal), "The verification page must include the three-action live-HUD check.");
            Require(verificationText.Contains("상태줄 보상", StringComparison.Ordinal), "The verification page must include active status-line payoff readability.");
            Require(verificationText.Contains("최근 흐름", StringComparison.Ordinal), "The verification page must include the recent lane-timing-action flow check.");
            Require(
                verificationText.Contains("중앙/레인 큐", StringComparison.Ordinal),
                "The verification page must remind the player to check active action and lane cues together.");
            Require(verificationText.Contains("F1", StringComparison.Ordinal), "The verification page must include the invincibility dev-test shortcut.");
            Require(
                verificationText.Contains("F2 기프트", StringComparison.Ordinal)
                    && verificationText.Contains("F3 유물", StringComparison.Ordinal)
                    && verificationText.Contains("F4 핵", StringComparison.Ordinal)
                    && verificationText.Contains("F5 탈출", StringComparison.Ordinal),
                "The verification page must include each chapter-jump dev-test shortcut.");
            Require(verificationText.Contains("F7 목표", StringComparison.Ordinal), "The verification page must include the review-target dev-test shortcut.");
            Require(verificationText.Contains("F8 경로", StringComparison.Ordinal), "The verification page must include the route-cycle dev-test shortcut.");
            Require(verificationText.Contains("↑↓↔·↔↓↑·↓↔↑", StringComparison.Ordinal), "The verification page must expose the stable/variation/pressure route response maps.");
            Require(verificationText.Contains("F9 동작", StringComparison.Ordinal), "The verification page must include the avoidance-cycle dev-test shortcut.");
            Require(verificationText.Contains("F6", StringComparison.Ordinal), "The verification page must include the focus/save dev-test shortcut.");
            Require(verificationText.Contains("PgUp", StringComparison.Ordinal), "The verification page must include the threat-up/save-use dev-test shortcut.");
            Require(verificationText.Contains("PgDn", StringComparison.Ordinal), "The verification page must include the threat-down/candidate dev-test shortcut.");
            Require(
                verificationText.Contains("End", StringComparison.Ordinal),
                "The verification page must include the infinite-stop dev-test shortcut.");
            Require(verificationText.Contains("Enter: 강조 다음 행동", StringComparison.Ordinal), "The verification page must include the keyboard primary-action check.");
            Require(verificationText.Contains("일시정지", StringComparison.Ordinal), "The verification page must include the pause-and-resume check.");
            Require(verificationText.Contains("배경차단", StringComparison.Ordinal), "The verification page must include the pause-backdrop input block check.");
            Require(verificationText.Contains("큐 숨김", StringComparison.Ordinal), "The verification page must include the non-action cue suppression check.");
            Require(verificationText.Contains("선택·정산 큐 숨김", StringComparison.Ordinal), "The verification page must include choice/result cue suppression in the checklist.");
            Require(
                verificationText.Contains("완성 루프: 일반→저장→무한→기록", StringComparison.Ordinal),
                "The verification page must expose the prototype completion loop as a single playtest contract.");
            Require(verificationText.Contains("GO", StringComparison.Ordinal), "The verification page must match the resume countdown GO label.");
            Require(verificationText.Contains("첫 함정", StringComparison.Ordinal), "The verification page must remind the player to check the first trap after resume.");
            Require(verificationText.Contains("복기 약점", StringComparison.Ordinal), "The verification page must include the result-to-practice review flow check.");
            Require(verificationText.Contains("약점 반복", StringComparison.Ordinal), "The verification page must remind the player to repeat the weak practice step.");
            Require(verificationText.Contains("보정 빠름·추천·늦음", StringComparison.Ordinal), "The verification page must include the input timing calibration buttons.");
            Require(verificationText.Contains("감각 소리%·진동", StringComparison.Ordinal), "The verification page must include the audio and haptic setting check.");
            Require(verificationText.Contains("터치 탭", StringComparison.Ordinal), "The verification page must include the mobile touch-lane check.");
            Require(verificationText.Contains("무한 위협", StringComparison.Ordinal), "The verification page must include infinite threat controls when a save loadout is usable.");
            Require(verificationText.Contains("폭·보상", StringComparison.Ordinal), "The verification page must remind the player to read threat width and reward changes.");
            Require(verificationText.Contains("자동 비움", StringComparison.Ordinal), "The verification page must preserve the two-use auto-clear confirmation.");
            string masteryText = backend.BuildProfileBoardText(2);
            RequireTextLayoutBudget(masteryText, 16, 92, "profile timing-axis mastery");
            Require(masteryText.Contains("축 숙련", StringComparison.Ordinal), "The profile badge page must expose timing-axis mastery.");
            Require(masteryText.Contains("정박 충전", StringComparison.Ordinal), "The profile badge page must preserve Perfect focus mastery.");
            Require(masteryText.Contains("압박 반격", StringComparison.Ordinal), "The profile badge page must preserve Clutch counter mastery.");
            Require(masteryText.Contains("엇박 연쇄", StringComparison.Ordinal), "The profile badge page must preserve Offbeat chain mastery.");
            Require(masteryText.Contains("경로 숙련", StringComparison.Ordinal), "The profile badge page must expose route-payoff mastery.");
            Require(masteryText.Contains("안정", StringComparison.Ordinal), "The profile badge page must preserve stable-route mastery.");
            Require(masteryText.Contains("변주", StringComparison.Ordinal), "The profile badge page must preserve variation-route mastery.");
            Require(masteryText.Contains("압박", StringComparison.Ordinal), "The profile badge page must preserve pressure-route mastery.");
            Require(masteryText.Contains("회피 숙련", StringComparison.Ordinal), "The profile badge page must expose jump, slide, and twist mastery.");
            Require(
                masteryText.Contains("점프", StringComparison.Ordinal)
                    && masteryText.Contains("숙", StringComparison.Ordinal)
                    && masteryText.Contains("틀", StringComparison.Ordinal),
                "The profile badge page must compactly preserve all three avoidance actions.");
            Require(
                masteryText.Contains("3동작 정확", StringComparison.Ordinal),
                "The profile badge page must preserve the exact jump-slide-twist achievement line.");
            if (InvokeMockBackendLabel(backend, "BuildPrototypeCompletionLoopStatusText") == "루프 4/4")
            {
                Require(
                    masteryText.Contains("완성 루프", StringComparison.Ordinal),
                    "A completed prototype loop must unlock a visible completion-loop badge.");
            }
        }

        private static void RequirePrototypeLoopStatus(GreedLastMockBackend backend, string expectedStatus, string context)
        {
            string actualStatus = InvokeMockBackendLabel(backend, "BuildPrototypeCompletionLoopStatusText");
            Require(
                actualStatus == expectedStatus,
                context + " must expose " + expectedStatus + " but was " + actualStatus + ".");
            Require(
                backend.BuildProfileBoardText(0).Contains(expectedStatus, StringComparison.Ordinal),
                context + " profile checklist must visibly include " + expectedStatus + ".");
        }

        private static void VerifyLegacyRelicSaveMigration(GreedLastRunRecord record, string backendSaveKey)
        {
            var legacyData = new LegacyBackendSaveData
            {
                version = 1,
                firstSaveCompleted = true,
                saveLoadoutUnlocked = true,
                savedLoadoutConfirmed = true,
                selectedSaveSlotIndex = 0,
                lastRunRecord = BuildLegacyRunRecord(record),
                bestRunRecord = BuildLegacyRunRecord(record),
                saveLoadoutSlots = new[] { BuildLegacyRunRecord(record) },
                saveLoadoutUsesRemaining = new[] { 1 },
                saveLoadoutRenameSteps = new[] { 0 },
            };
            PlayerPrefs.SetString(backendSaveKey, JsonUtility.ToJson(legacyData));
            PlayerPrefs.Save();

            var migratedBackend = new GreedLastMockBackend();
            GreedLastLobbySnapshot migratedLobby = migratedBackend.LoadLobby();
            Require(migratedLobby.SaveSlotOccupied[0] && migratedLobby.SaveSlotUsable[0], "A legacy relic-bearing save slot must remain usable after migration.");
            Require(migratedBackend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord migratedRecord, out _, out _), "A migrated legacy slot must still enter infinite mode.");
            Require(string.IsNullOrEmpty(migratedRecord.Relic), "Legacy save migration must remove the stored relic.");
            Require(!migratedRecord.ChoiceSummary.Contains(record.Relic, StringComparison.Ordinal), "Legacy save migration must remove the relic from its choice history.");
            Require(!migratedRecord.LoadoutName.Contains(record.Relic, StringComparison.Ordinal), "Legacy save migration must repair a relic-bearing loadout name.");

            legacyData.saveLoadoutUsesRemaining = new[] { 0 };
            PlayerPrefs.SetString(backendSaveKey, JsonUtility.ToJson(legacyData));
            PlayerPrefs.Save();

            var exhaustedLegacyBackend = new GreedLastMockBackend();
            GreedLastLobbySnapshot exhaustedLegacyLobby = exhaustedLegacyBackend.LoadLobby();
            Require(
                !exhaustedLegacyLobby.SaveSlotOccupied[0]
                    && !exhaustedLegacyLobby.SaveSlotUsable[0]
                    && exhaustedLegacyLobby.Message.Contains("보충 런", StringComparison.Ordinal),
                "A legacy save slot with zero remaining uses must be pruned and routed to refill guidance.");
            Require(
                exhaustedLegacyBackend.BuildInfiniteStartReadyText().Contains("보충 런 버튼: 보충 런 진입", StringComparison.Ordinal),
                "A pruned exhausted legacy save slot must surface refill-run guidance on the infinite preparation screen.");
            Require(
                !exhaustedLegacyBackend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                "A pruned exhausted legacy save slot must not be consumable.");
        }

        private static LegacyRunRecordData BuildLegacyRunRecord(GreedLastRunRecord record)
        {
            return new LegacyRunRecordData
            {
                isValid = record.IsValid,
                score = record.Score,
                health = record.Health,
                combo = record.Combo,
                focus = record.Focus,
                maxCombo = record.MaxCombo,
                successCount = record.SuccessCount,
                goodCount = record.GoodCount,
                missCount = record.MissCount,
                distance = record.Distance,
                loadoutName = record.LoadoutName,
                startGift = record.StartGift,
                primaryGift = record.PrimaryGift,
                relic = record.Relic,
                choiceSummary = record.ChoiceSummary,
                timingProfile = record.TimingProfile,
            };
        }

        private static GreedLastRunRecord BuildStoredGiftRecord(string primaryGift, string choiceSummary)
        {
            return new GreedLastRunRecord(
                true,
                0,
                3,
                0,
                0,
                0,
                0,
                0,
                0,
                0f,
                primaryGift + " 조합",
                string.Empty,
                primaryGift,
                string.Empty,
                choiceSummary,
                string.Empty);
        }

        private static GreedLastRunRecord BuildComparisonBaselineRecord(GreedLastRunRecord record)
        {
            return new GreedLastRunRecord(
                true,
                Mathf.Max(1, record.Score - 100),
                record.Health,
                record.Combo,
                record.Focus,
                record.MaxCombo,
                Mathf.Max(0, record.SuccessCount - 3),
                record.GoodCount + 2,
                record.MissCount + 1,
                record.Distance,
                record.LoadoutName,
                record.StartGift,
                record.PrimaryGift,
                record.Relic,
                record.ChoiceSummary,
                record.TimingProfile);
        }

        private static GreedLastRunRecord BuildRecordBoardRunVariant(GreedLastRunRecord record, int index)
        {
            int offset = Mathf.Max(0, index);
            return new GreedLastRunRecord(
                true,
                record.Score + offset * 37,
                record.Health,
                record.Combo,
                record.Focus,
                record.MaxCombo + offset,
                record.SuccessCount,
                record.GoodCount,
                record.MissCount,
                record.Distance + offset * 4f,
                record.LoadoutName + " R" + offset,
                record.StartGift,
                record.PrimaryGift,
                record.Relic,
                record.ChoiceSummary,
                record.TimingProfile);
        }

        private static GreedLastRunAttemptRecord BuildRecordBoardAttemptVariant(GreedLastRunAttemptRecord record, int index)
        {
            int offset = Mathf.Max(0, index);
            return new GreedLastRunAttemptRecord(
                true,
                record.Outcome,
                record.Score + offset * 19,
                record.Health,
                record.Combo,
                record.Focus,
                record.MaxCombo + offset,
                record.SuccessCount,
                record.GoodCount,
                record.MissCount,
                record.Distance + offset * 3f,
                record.ChapterIndex,
                record.ChapterProgress,
                record.ChapterTarget,
                record.CoreRetrieved,
                record.MissReason,
                record.ChoiceSummary,
                record.TimingProfile + " / 보드 변형 " + offset);
        }

        private static GreedLastRunRecord BuildUnstableLateRunRecord(GreedLastRunRecord record)
        {
            string timingProfile = record.TimingProfile
                .Replace("4장 정확 3회", "4장 정확 1회")
                .Replace("4장 보정 0회", "4장 보정 1회")
                .Replace("4장 실수 0회", "4장 실수 1회")
                .Replace("탈출 정확 3회", "탈출 정확 1회")
                .Replace("탈출 보정 0회", "탈출 보정 1회")
                .Replace("탈출 실수 0회", "탈출 실수 1회");
            return new GreedLastRunRecord(
                true,
                Mathf.Max(1, record.Score - 200),
                Mathf.Max(1, record.Health - 1),
                record.Combo,
                record.Focus,
                Mathf.Max(0, record.MaxCombo - 2),
                Mathf.Max(0, record.SuccessCount - 4),
                record.GoodCount + 2,
                record.MissCount + 2,
                record.Distance,
                record.LoadoutName,
                record.StartGift,
                record.PrimaryGift,
                record.Relic,
                record.ChoiceSummary,
                timingProfile);
        }

        private static GreedLastInfiniteRunRecord WithInfiniteMissReason(
            GreedLastInfiniteRunRecord record,
            string missReason)
        {
            return new GreedLastInfiniteRunRecord(
                record.IsValid,
                record.Score,
                record.Distance,
                record.SectionsCleared,
                record.MaxThreatLevel,
                record.LoadoutName,
                record.LoadoutProfile,
                record.SuccessCount,
                record.GoodCount,
                record.MissCount,
                record.MaxCombo,
                missReason,
                record.TimingProfile);
        }

        private static void RequireTextLayoutBudget(string text, int maxLines, int maxLineLength, string context)
        {
            string[] lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            Require(lines.Length <= maxLines, context + " must stay within " + maxLines + " visible lines.");
            for (int i = 0; i < lines.Length; i += 1)
            {
                Require(
                    lines[i].Length <= maxLineLength,
                    context + " line " + (i + 1) + " exceeds the " + maxLineLength + " character layout budget: "
                        + lines[i].Length + " chars / " + lines[i]);
            }
        }

        [Serializable]
        private sealed class LegacyBackendSaveData
        {
            public int version;
            public bool firstSaveCompleted;
            public bool saveLoadoutUnlocked;
            public bool savedLoadoutConfirmed;
            public int selectedSaveSlotIndex;
            public LegacyRunRecordData lastRunRecord;
            public LegacyRunRecordData bestRunRecord;
            public LegacyRunRecordData[] saveLoadoutSlots;
            public int[] saveLoadoutUsesRemaining;
            public int[] saveLoadoutRenameSteps;
        }

        [Serializable]
        private sealed class LegacyRunRecordData
        {
            public bool isValid;
            public int score;
            public int health;
            public int combo;
            public int focus;
            public int maxCombo;
            public int successCount;
            public int goodCount;
            public int missCount;
            public float distance;
            public string loadoutName;
            public string startGift;
            public string primaryGift;
            public string relic;
            public string choiceSummary;
            public string timingProfile;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Greed Last prototype smoke check failed: " + message);
            }
        }
    }
}
