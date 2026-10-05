using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeJudgementCheck
    {
        [MenuItem("Greed Last/Run Judgement Presentation Check")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Judgement checks require Edit mode.");
            string key = GreedLastRunCore.InputTimingOffsetPrefsKey;
            bool hadOffset = PlayerPrefs.HasKey(key);
            float storedOffset = PlayerPrefs.GetFloat(key);
            GameObject previousCamera = GameObject.Find("GreedLastCamera");
            GameObject previousEvents = GameObject.Find("GreedLastEventSystem");
            var host = new GameObject("GreedLastJudgementCheck");
            GameObject canvas = null;
            try
            {
                GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                bool presentSnapshot = true;
                core.SnapshotChanged += value => { run = value; if (presentSnapshot) ui.RenderRun(value); };
                float clock = Time.realtimeSinceStartup;

                void PublishAtClock()
                {
                    // Settings APIs publish wall time; restore the fixture's synthetic clock before judging.
                    typeof(GreedLastRunCore).GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(float) }, null).Invoke(core, new object[] { clock });
                }

                void FreshPractice()
                {
                    ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                        GreedLastScreenState.RunCoreTest, "Judgement check", string.Empty, 0));
                    core.RestartPractice(clock);
                    Require(run.LastJudgementScoreGain == 0 && run.LastJudgementHealthChange == 0
                        && !run.HasJudgedInputTiming && run.LastInputTimingDeltaSeconds == 0f
                        && run.LastJudgedInputChannel == GreedLastRunChannel.None,
                        "A new run must clear its previous reward and timing payload.");
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                    Require(run.ActivePattern, "The fixture needs a real authored practice trap.");
                }

                void FreshNormal()
                {
                    ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                        GreedLastScreenState.RunCoreTest, "Judgement check", string.Empty, 0));
                    core.Enter();
                    for (int step = 0; step < 32 && !run.ActivePattern; step++)
                    {
                        if (run.ChoiceActive) core.HandleInput(GreedLastRunChannel.Left, clock);
                        else core.Tick(clock += 2f);
                    }
                    Require(run.ActivePattern && !run.PracticeMode && !run.ChoiceActive,
                        "The fixture needs a real normal-run trap after its opening choices.");
                }

                VerifyCalibratedWaitingGuide(ui);
                var capped = GreedLastPrototypeScoreCheck.VerifyJudgement(int.MaxValue - 5, GreedLastJudgementResult.Success);
                Require(capped.after.LastResult == GreedLastJudgementResult.Success && capped.after.LastJudgementScoreGain == 5,
                    "Presentation needs the actual boundary judgement, not its subsequent ignored duplicate input.");
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                    GreedLastScreenState.InfiniteRun, "Score ceiling", string.Empty, 0));
                ui.RenderRun(capped.before);
                Call(ui, "UpdateRunTimingVisuals");
                Require(Read<Text>(ui, "activeActionCueText").text.Contains("정확 +5", StringComparison.Ordinal),
                    "Player score-ceiling action guidance must preview its actual five available points.");
                ui.RenderRun(capped.after);
                Require(Read<Text>(ui, "judgementFeedbackText").text.Contains("+5점", StringComparison.Ordinal),
                    "Player score-ceiling feedback must show its actual five awarded points.");
                foreach (float delta in new[] { -0.012f, 0f, 0.012f, 0.075f })
                {
                    FreshPractice();
                    GreedLastRunSnapshot before = run;
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta;
                    core.HandleInput(run.Pattern.Channel, clock);
                    Require(run.LastResult == GreedLastJudgementResult.Success
                        && run.LastJudgementScoreGain == run.Score - before.Score
                        && run.LastJudgementScoreGain > 0,
                        "Exact feedback must carry the actual reward, including bonuses.");
                    Require(run.HasJudgedInputTiming && Mathf.Abs(run.LastInputTimingDeltaSeconds - delta) < 0.001f,
                        "Small exact offsets must remain available even when the human-readable message says exact.");
                    Require(run.LastJudgedInputChannel == before.Pattern.Channel,
                        "Every exact judgement must retain the actual input channel.");
                    if (Mathf.Abs(delta) <= 0.02f) Require(string.IsNullOrEmpty(Nudge(run)),
                        "Tiny exact timing must not produce unnecessary correction coaching.");
                    Text feedback = Read<Text>(ui, "judgementFeedbackText");
                    Require(feedback.text.Contains("+" + run.LastJudgementScoreGain + "점", StringComparison.Ordinal)
                        && feedback.text.Split('\n').Length == 2,
                        "Player feedback must show the real score in its existing two-line surface.");
                    Require(Mathf.Abs(Read<float>(ui, "inputTimingTraceSignedDeltaSeconds") - delta) < 0.001f
                        && Read<float>(ui, "inputTimingTraceUntil") > Time.unscaledTime,
                        "Exact taps must create a trace from structured timing, not parsed display text.");
                    Require(Read<int>(ui, "judgementHistoryCount") == 1, "A real input must be presented once.");
                    string compact = feedback.text;
                    float feedbackUntil = Read<float>(ui, "judgementFeedbackUntil");
                    float popUntil = Read<float>(ui, "judgementFeedbackPopUntil");
                    ui.ToggleDevCleanView();
                    Require(!ui.UsePlayerRunControls && feedback.text != compact,
                        "F11 must immediately reformat a visible judgement without waiting for another core publication.");
                    ui.ToggleDevCleanView();
                    Require(ui.UsePlayerRunControls && feedback.text == compact
                        && Read<int>(ui, "judgementHistoryCount") == 1
                        && Read<float>(ui, "judgementFeedbackUntil") == feedbackUntil
                        && Read<float>(ui, "judgementFeedbackPopUntil") == popUntil,
                        "Changing feedback view must preserve its original reward, history, lifetime and animation.");
                    ui.CycleDevTrapVisualPreview();
                    string preview = feedback.text;
                    float previewUntil = Read<float>(ui, "judgementFeedbackUntil");
                    ui.ToggleDevCleanView();
                    ui.ToggleDevCleanView();
                    Require(!string.IsNullOrEmpty(preview) && feedback.text == preview
                        && Read<float>(ui, "judgementFeedbackUntil") == previewUntil
                        && Read<int>(ui, "judgementHistoryCount") == 1,
                        "A development preview must not resurrect the previous judgement during F11 changes.");
                    Write(ui, "judgementFeedbackUntil", -10f);
                    Write(ui, "inputTimingTraceUntil", -10f);
                    int count = Read<int>(ui, "judgementHistoryCount");
                    core.AdjustInputTimingOffset(0.02f);
                    core.ResetInputTimingOffset();
                    core.ApplyRecommendedInputTimingOffset();
                    core.ToggleAutoFlow(clock);
                    core.ToggleAutoFlow(clock);
                    core.ToggleDevInvincible();
                    core.ToggleDevInvincible();
                    core.FillFocusForTest();
                    ui.RenderRun(run);
                    Require(Read<int>(ui, "judgementHistoryCount") == count
                        && Read<float>(ui, "judgementFeedbackUntil") == -10f
                        && Read<float>(ui, "inputTimingTraceUntil") == -10f,
                        "Settings, focus and automatic-flow commands must not replay an old judgement.");
                }

                FreshPractice();
                core.ToggleDevInvincible();
                for (int miss = 0; miss < 2; miss++)
                {
                    if (miss > 0) core.RequestNextPattern(clock += 2f);
                    Require(run.ActivePattern && run.DevInvincible, "Repeated misses need a protected live trap.");
                    int before = Read<int>(ui, "judgementHistoryCount");
                    int health = run.Health;
                    clock += Mathf.Max(run.Pattern.HitSeconds,
                        run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds) + 0.1f;
                    core.Tick(clock);
                    Require(run.LastResult == GreedLastJudgementResult.Miss && run.MissReason == "no_input"
                        && !run.HasJudgedInputTiming && run.LastInputTimingDeltaSeconds == 0f
                        && run.LastJudgedInputChannel == GreedLastRunChannel.None
                        && run.LastJudgementScoreGain == 0 && run.LastJudgementHealthChange == 0
                        && run.Health == health,
                        "Timeouts must not fabricate tap timing, score or damage while protected.");
                    Require(string.IsNullOrEmpty(Nudge(run)), "A timeout has no input timing to correct.");
                    Require(!run.JudgementText.Contains("빠름 ", StringComparison.Ordinal)
                        && !run.JudgementText.Contains("늦음 ", StringComparison.Ordinal),
                        "The core's no-input copy must not fabricate a tap's timing in the diagnostic HUD.");
                    string timeoutDetail = Label("BuildJudgementFeedbackText", run);
                    Require(timeoutDetail.Contains("입력 없음", StringComparison.Ordinal)
                        && !timeoutDetail.Contains("빠름 ", StringComparison.Ordinal)
                        && !timeoutDetail.Contains("늦음 ", StringComparison.Ordinal),
                        "Detailed timeout feedback must name the missing input without fabricating a tap's timing.");
                    Require(Read<int>(ui, "judgementHistoryCount") == before + 1,
                        "Each real protected miss must be shown even when score, health and copy are unchanged.");
                }
                core.ToggleDevInvincible();

                FreshPractice();
                float goodDelta = (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                int goodScoreBefore = run.Score;
                clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - goodDelta;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Good && run.HasJudgedInputTiming
                    && Mathf.Abs(run.LastInputTimingDeltaSeconds + goodDelta) < 0.001f
                    && run.LastJudgementScoreGain == run.Score - goodScoreBefore,
                    "Early Good feedback must retain signed timing and its actual score.");
                Require(Read<Text>(ui, "judgementFeedbackText").text.Contains("빠름", StringComparison.Ordinal),
                    "Early Good feedback must not be presented as late.");
                Require(Nudge(run).Contains("빠름", StringComparison.Ordinal),
                    "Timing coaching must use the actual signed input payload.");

                for (int stage = 0; stage < 3; stage++)
                {
                    foreach (GreedLastRunChannel wrong in new[] {
                        GreedLastRunChannel.Left, GreedLastRunChannel.Center, GreedLastRunChannel.Right })
                    {
                        FreshPractice();
                        for (int preceding = 0; preceding < stage; preceding++)
                        {
                            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                            core.HandleInput(run.Pattern.Channel, clock);
                            core.RequestNextPattern(clock += 2f);
                        }
                        GreedLastRunChannel targetChannel = run.Pattern.Channel;
                        if (wrong == targetChannel) continue;
                        int history = Read<int>(ui, "judgementHistoryCount");
                        presentSnapshot = false;
                        clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + 0.012f;
                        core.HandleInput(wrong, clock);
                        typeof(GreedLastRunCore).GetField("judgementText", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(core, "Presentation wording replaced");
                        PublishAtClock();
                        presentSnapshot = true;
                        ui.RenderRun(run);
                        float expectedX = (float)typeof(GreedLastRuntimeUi).GetMethod("GetChannelAnchorX",
                            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { wrong });
                        Require(run.LastResult == GreedLastJudgementResult.Miss && run.MissReason == "wrong_channel"
                            && run.LastJudgedInputChannel == wrong
                            && Read<GreedLastRunChannel>(ui, "runnerResponseChannel") == wrong
                            && Mathf.Abs(Read<float>(ui, "inputTimingTraceX") - expectedX) < 0.001f
                            && Read<GreedLastJudgementResult[]>(ui, "laneJudgementTagResults")[(int)wrong - 1]
                                == GreedLastJudgementResult.Miss
                            && Read<GreedLastJudgementResult[]>(ui, "laneJudgementTagResults")[(int)targetChannel - 1]
                                == GreedLastJudgementResult.None
                            && Read<int>(ui, "judgementHistoryCount") == history + 1,
                            "Wrong-channel runner, trace and distinct correct-lane hint must use the actual input, independent of wording.");
                        string actualInputLabel = wrong == GreedLastRunChannel.Left ? "좌 입력"
                            : wrong == GreedLastRunChannel.Center ? "중 입력" : "우 입력";
                        Require(Label("BuildJudgementFeedbackText", run).Contains(actualInputLabel, StringComparison.Ordinal),
                            "Detailed wrong-channel feedback must retain the actual pressed channel when wording changes.");
                        Text wrongFeedback = Read<Text>(ui, "judgementFeedbackText");
                        if (!run.Stopped)
                            Require(wrongFeedback.fontSize == 36 && wrongFeedback.text.Split('\n').Length == 2
                                && wrongFeedback.rectTransform.rect.height == 124f
                                && wrongFeedback.preferredHeight <= wrongFeedback.rectTransform.rect.height + 1f,
                                "Visible compact wrong-lane copy must fit its fixed two-line surface without enlarging over gauges.");
                    }
                }

                foreach (bool practice in new[] { true, false })
                foreach (float offset in new[] { -0.2f, -0.02f, 0f, 0.02f, 0.2f })
                foreach (float direction in new[] { -1f, 1f })
                {
                    if (practice) FreshPractice();
                    else FreshNormal();
                    core.AdjustInputTimingOffset(offset - core.InputTimingOffsetSeconds);
                    PublishAtClock();
                    float delta = direction * (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                    presentSnapshot = false;
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds + delta;
                    core.HandleInput(run.Pattern.Channel, clock);
                    typeof(GreedLastRunCore).GetField("judgementText", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(core, "Changed Good wording\n" + (direction < 0f ? "늦음 999ms" : "빠름 999ms"));
                    PublishAtClock();
                    presentSnapshot = true;
                    ui.RenderRun(run);
                    string expectedDirection = direction < 0f ? "빠름 " : "늦음 ";
                    string expectedHint = direction < 0f ? "다음은 살짝 늦게" : "다음은 살짝 빠르게";
                    Require(run.LastResult == GreedLastJudgementResult.Good && run.HasJudgedInputTiming
                        && Label("BuildJudgementFeedbackText", run).Contains(expectedDirection, StringComparison.Ordinal)
                        && Label("BuildJudgementFeedbackText", run).Contains(expectedHint, StringComparison.Ordinal)
                        && !Label("BuildJudgementFeedbackText", run).Contains("999ms", StringComparison.Ordinal)
                        && Label("BuildJudgementCorrectionObjectiveSuffix", run).Contains(expectedHint, StringComparison.Ordinal)
                        && (practice || Label("BuildRunObjectiveLine", run).Contains(expectedHint, StringComparison.Ordinal)),
                        "Good timing and the next-input objective must use actual signed input, not contradictory wording."
                            + $" practice={practice}, offset={offset:F3}, delta={delta:F4}, actual={run.LastInputTimingDeltaSeconds:F4},"
                            + $" result={run.LastResult}, reason={run.MissReason}, detail={Label("BuildJudgementFeedbackText", run)},"
                            + $" objective={Label("BuildRunObjectiveLine", run)}.");
                    Text feedback = Read<Text>(ui, "judgementFeedbackText");
                    string compact = feedback.text;
                    float until = Read<float>(ui, "judgementFeedbackUntil");
                    float popUntil = Read<float>(ui, "judgementFeedbackPopUntil");
                    ui.ToggleDevCleanView();
                    Require(feedback.text.Contains(expectedDirection, StringComparison.Ordinal)
                        && feedback.text.Contains(expectedHint, StringComparison.Ordinal)
                        && !feedback.text.Contains("999ms", StringComparison.Ordinal),
                        "F11 detail must agree with compact Good feedback's actual timing direction.");
                    ui.ToggleDevCleanView();
                    Require(feedback.text == compact && Read<int>(ui, "judgementHistoryCount") == 1
                        && Read<float>(ui, "judgementFeedbackUntil") == until
                        && Read<float>(ui, "judgementFeedbackPopUntil") == popUntil,
                        "Timing-consistent mode changes must retain one judgement and its original lifetime.");
                }

                foreach (bool practice in new[] { true, false })
                foreach (float offset in new[] { -0.2f, -0.02f, 0f, 0.02f, 0.2f })
                foreach (float direction in new[] { -1f, 1f })
                {
                    if (practice) FreshPractice();
                    else FreshNormal();
                    core.AdjustInputTimingOffset(offset - core.InputTimingOffsetSeconds);
                    PublishAtClock();
                    float delta = direction * (run.Pattern.GoodWindowSeconds + 0.04f);
                    presentSnapshot = false;
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds + delta;
                    core.HandleInput(run.Pattern.Channel, clock);
                    typeof(GreedLastRunCore).GetField("judgementText", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(core, "Changed Miss wording\n" + (direction < 0f ? "늦음 999ms" : "빠름 999ms"));
                    PublishAtClock();
                    presentSnapshot = true;
                    ui.RenderRun(run);
                    string expectedDirection = direction < 0f ? "빠름 " : "늦음 ";
                    string expectedHint = direction < 0f ? "다음은 조금 늦게" : "다음은 조금 빠르게";
                    string detail = Label("BuildJudgementFeedbackText", run);
                    Require(run.LastResult == GreedLastJudgementResult.Miss && run.HasJudgedInputTiming
                        && run.MissReason == (direction < 0f ? "too_early" : "too_late")
                        && Mathf.Abs(run.LastInputTimingDeltaSeconds - delta) < 0.001f
                        && detail.Contains(expectedDirection, StringComparison.Ordinal)
                        && detail.Contains(expectedHint, StringComparison.Ordinal)
                        && !detail.Contains("999ms", StringComparison.Ordinal),
                        "Timed practice/normal misses must retain actual direction and coaching at every calibration offset.");
                    Text feedback = Read<Text>(ui, "judgementFeedbackText");
                    string compact = feedback.text;
                    float until = Read<float>(ui, "judgementFeedbackUntil");
                    ui.ToggleDevCleanView();
                    Require(feedback.text.Contains(expectedDirection, StringComparison.Ordinal)
                        && feedback.text.Contains(expectedHint, StringComparison.Ordinal)
                        && feedback.preferredHeight <= feedback.rectTransform.rect.height + 1f,
                        "Detailed timed Miss feedback must fit its fixed surface and agree with the actual input.");
                    ui.ToggleDevCleanView();
                    Require(feedback.text == compact && Read<int>(ui, "judgementHistoryCount") == 1
                        && Read<float>(ui, "judgementFeedbackUntil") == until,
                        "A timed Miss view switch must not replay or extend its feedback.");
                }

                FreshNormal();
                core.ToggleDevInvincible();
                core.Tick(clock += Mathf.Max(run.Pattern.HitSeconds,
                    run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds)
                    - run.ElapsedSeconds + 0.1f);
                Require(run.LastResult == GreedLastJudgementResult.Miss && run.MissReason == "no_input"
                    && !run.HasJudgedInputTiming
                    && !run.JudgementText.Contains("빠름 ", StringComparison.Ordinal)
                    && !run.JudgementText.Contains("늦음 ", StringComparison.Ordinal)
                    && !Label("BuildJudgementFeedbackText", run).Contains("빠름 ", StringComparison.Ordinal)
                    && !Label("BuildJudgementFeedbackText", run).Contains("늦음 ", StringComparison.Ordinal),
                    "Normal-run timeout detail must not fabricate a tap's direction from the expiry clock.");
                core.ToggleDevInvincible();

                VerifyStableRecovery(core, ui, ref run, ref clock);
                VerifyExactBundleRecovery(core, ui, ref run, ref clock);
                FreshPractice();
                clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                core.TogglePause(clock);
                core.TogglePause(clock);
                string countdownText = Read<Text>(ui, "judgementFeedbackText").text;
                ui.ToggleDevCleanView();
                ui.ToggleDevCleanView();
                Require(Read<Text>(ui, "judgementFeedbackText").text == countdownText
                    && Read<GreedLastRunSnapshot>(ui, "lastPresentedJudgementSnapshot").LastResult == GreedLastJudgementResult.None,
                    "Resume countdowns must clear the previous live judgement's presentation identity.");
                Debug.Log("Greed judgement presentation passed: actual reward/timing/channel payload, tiny exact traces, 40 early/late Good/Miss cases in practice and normal at five calibration offsets with contradictory wording, six wording-independent wrong-channel pairs, explicit no-input detail, coaching suppression, protected timeout repetition, settings/focus/flow replay isolation, F11 feedback/preview/countdown separation, real Good/exact-bundle recovery and fresh-run reset. Personal slots are not accessed.");
            }
            finally
            {
                if (hadOffset) PlayerPrefs.SetFloat(key, storedOffset);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
                DestroyCreated("GreedLastCamera", previousCamera);
                DestroyCreated("GreedLastEventSystem", previousEvents);
            }
        }

        private static void VerifyStableRecovery(GreedLastRunCore core, GreedLastRuntimeUi ui,
            ref GreedLastRunSnapshot run, ref float clock)
        {
            core.Enter();
            for (int step = 0; step < 32; step++)
            {
                if (run.ChoiceActive) core.HandleInput(GreedLastRunChannel.Left, clock);
                else if (run.CountdownActive) core.Tick(clock += 2f);
                else if (!run.ActivePattern) core.Tick(clock += 0.1f);
                else if (run.ActiveRouteProfile == "안정 루트") break;
                else
                {
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                }
            }
            Require(run.ActivePattern && run.ActiveRouteProfile == "안정 루트", "Recovery needs a real selected stable route.");
            int health = run.Health;
            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
            core.HandleInput(run.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right : GreedLastRunChannel.Left, clock);
            Require(run.LastJudgementHealthChange == -1 && run.Health == health - 1,
                "Wrong-channel payload must match the real damage.");
            core.RequestNextPattern(clock += 2f);
            Require(run.ActivePattern, "Recovery needs the next real breather trap.");
            GreedLastRunSnapshot before = run;
            float delta = (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta;
            core.HandleInput(run.Pattern.Channel, clock);
            Require(run.LastResult == GreedLastJudgementResult.Good
                && run.LastJudgementHealthChange == run.Health - before.Health
                && run.LastJudgementHealthChange > 0,
                "A stable-route recovery must expose only actual gained health.");
            Text text = Read<Text>(ui, "judgementFeedbackText");
            Require(text.text.Contains("체력 +" + run.LastJudgementHealthChange, StringComparison.Ordinal)
                && text.text.Contains("+" + run.LastJudgementScoreGain + "점", StringComparison.Ordinal)
                && text.text.Split('\n').Length == 2,
                "Recovery must replace the combo detail with actual healed health without extra lines.");
        }

        private static void VerifyExactBundleRecovery(GreedLastRunCore core, GreedLastRuntimeUi ui,
            ref GreedLastRunSnapshot run, ref float clock)
        {
            core.Enter();
            for (int step = 0; step < 256 && !run.Stopped; step++)
            {
                if (run.ChoiceActive) core.HandleInput(GreedLastRunChannel.Left, clock);
                else if (run.CountdownActive) core.Tick(clock += 2f);
                else if (!run.ActivePattern) core.Tick(clock += 0.1f);
                else
                {
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                }
            }
            GreedLastRunRecord record = core.CreateRunRecord();
            Require(record.IsValid && run.SaveEligible, "Exact healing needs a real completed stable-route loadout.");
            core.EnterInfiniteRun(record);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
            core.HandleInput(run.Pattern.Channel == GreedLastRunChannel.Left
                ? GreedLastRunChannel.Right : GreedLastRunChannel.Left, clock);
            for (int trap = 0; trap < 5; trap++)
            {
                core.RequestNextPattern(clock += 2f);
                GreedLastRunSnapshot before = run;
                clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                if (trap < 4) continue;
                Text feedback = Read<Text>(ui, "judgementFeedbackText");
                Require(run.LastResult == GreedLastJudgementResult.Success && run.LastJudgementHealthChange > 0
                    && run.LastJudgementHealthChange == run.Health - before.Health
                    && run.LastJudgementScoreGain == run.Score - before.Score
                    && feedback.text.Contains("체력 +" + run.LastJudgementHealthChange, StringComparison.Ordinal)
                    && feedback.text.Split('\n').Length == 2,
                    "A real intact exact bundle must display its actual health and total reward after a damaged bundle.");
            }
        }

        private static void DestroyCreated(string name, GameObject previous)
        {
            GameObject created = GameObject.Find(name);
            if (previous == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }

        private static string Nudge(GreedLastRunSnapshot snapshot) => (string)typeof(GreedLastRuntimeUi)
            .GetMethod("BuildRecentTimingNudgeCoach", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { snapshot });

        private static string Label(string method, GreedLastRunSnapshot snapshot) => (string)typeof(GreedLastRuntimeUi)
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { snapshot });

        private static T Read<T>(GreedLastRuntimeUi ui, string name) => (T)typeof(GreedLastRuntimeUi)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        private static void Write(GreedLastRuntimeUi ui, string name, object value) => typeof(GreedLastRuntimeUi)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, value);
        private static void Call(GreedLastRuntimeUi ui, string name) => typeof(GreedLastRuntimeUi)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);

        private static void VerifyCalibratedWaitingGuide(GreedLastRuntimeUi ui)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var deck = (GreedLastPatternModel[])typeof(GreedLastRunCore).GetField("patternDeck", fields)
                .GetValue(new GreedLastRunCore());
            int cases = 0;
            for (int index = 0; index < 3; index++)
            foreach (float offset in new[] { -0.2f, -0.1f, 0f, 0.1f, 0.2f })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += snapshot => { run = snapshot; ui.RenderRun(snapshot); };
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                    GreedLastScreenState.RunCoreTest, "Calibration waiting check", string.Empty, 0));
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                core.RestartPractice(16f);
                core.Tick(18f);
                typeof(GreedLastRunCore).GetField("inputTimingOffsetSeconds", fields).SetValue(core, offset);
                typeof(GreedLastRunCore).GetField("devNextPatternOverride", fields).SetValue(core, deck[index]);
                core.RequestNextPattern(19f);
                float tapAt = 19f + run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds + 0.05f;
                core.Tick(tapAt);
                Require(run.ActivePattern && run.MissCount == 0, "Waiting copy needs a still-live authored trap.");
                bool waiting = Mathf.Abs(run.CoreDeltaSeconds + offset) <= run.Pattern.GoodWindowSeconds;
                Color32 color = (Color32)typeof(GreedLastRuntimeUi).GetMethod("GetTimingGuideColor",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { run });
                Require(color.Equals(waiting ? new Color32(105, 168, 190, 245) : new Color32(207, 87, 65, 235)),
                    "Pending calibrated input must use a waiting color, not the missed-window red.");
                int historyCount = Read<int>(ui, "judgementHistoryCount");
                string compact = Label("BuildCompactTimingGuideLabel", run);
                string detail = Label("BuildTimingGuideLabel", run);
                string lane = Label("BuildLaneTimingHint", run);
                Require(waiting ? compact == "보정 입력 대기" && detail.Contains("보정 입력 대기", StringComparison.Ordinal)
                    && !detail.Contains("지난 박", StringComparison.Ordinal) && lane == "보정 대기"
                    : compact == "놓침" && detail.Contains("놓침", StringComparison.Ordinal) && lane == "놓침",
                    "Timing copy must distinguish a pending calibrated input from an actually missed input window.");
                Call(ui, "UpdateRunTimingVisuals");
                Require(Read<Text>(ui, "timingGuideText").text == compact,
                    "The actual compact guide must display its calibrated waiting state.");
                ui.ToggleDevCleanView();
                Call(ui, "UpdateRunTimingVisuals");
                Require(Read<Text>(ui, "timingGuideText").text == detail,
                    "F11 detail must agree without inventing a resolved judgement.");
                ui.ToggleDevCleanView();
                Require(Read<int>(ui, "judgementHistoryCount") == historyCount
                    && run.SuccessCount + run.GoodCount + run.MissCount == 0,
                    "Waiting copy and F11 must not create a judgement before an actual input.");
                core.HandleInput(run.Pattern.Channel, tapAt);
                Require(run.LastResult == (waiting ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Miss)
                    && run.SuccessCount + run.GoodCount + run.MissCount == 1,
                    "Waiting/missed guide state must agree with the one actual input outcome.");
                cases++;
            }
            Debug.Log($"Greed calibrated waiting guide passed: {cases} cases, three timing types and five offsets, compact/detail/lane state and actual input agreement. Authored beat/visual travel unchanged; no personal preferences written.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Greed judgement presentation: " + message);
        }
    }
}
