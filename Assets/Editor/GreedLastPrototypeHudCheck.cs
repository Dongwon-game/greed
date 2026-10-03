using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeHudCheck
    {
        internal static void VerifyCompactTimingGuideReuse(GreedLastRuntimeUi ui)
        {
            var fresh = (Func<GreedLastRunSnapshot, string>)Delegate.CreateDelegate(
                typeof(Func<GreedLastRunSnapshot, string>), typeof(GreedLastRuntimeUi)
                    .GetMethod("BuildCompactTimingGuideLabel", BindingFlags.Static | BindingFlags.NonPublic));
            var cached = (Func<GreedLastRunSnapshot, string>)Delegate.CreateDelegate(
                typeof(Func<GreedLastRunSnapshot, string>), ui, typeof(GreedLastRuntimeUi)
                    .GetMethod("GetCompactTimingGuideLabel", BindingFlags.Instance | BindingFlags.NonPublic));
            bool originalPlayerControls = ui.UsePlayerRunControls;
            if (!originalPlayerControls) ui.ToggleDevCleanView();
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                GreedLastScreenState.InfiniteRun, "Timing guide cache check", string.Empty, 0));
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            int comparisons = 0;
            int firstWindow = -1;
            bool changedWindow = false;
            bool sawEarly = false;
            bool sawCenter = false;
            bool sawLate = false;
            bool sawMissed = false;
            try
            {
                core.EnterInfiniteRun();
                core.ToggleDevInvincible();
                for (int frame = 0; frame < 2400; frame++)
                {
                    core.Tick(clock += 1f / 60f);
                    if (frame == 600 || frame == 1200) core.RaiseInfiniteThreatForTest();
                    Check();
                    if (!run.ActivePattern || run.CountdownActive) continue;
                    int count = run.SuccessCount + run.GoodCount + run.MissCount;
                    if (count % 3 == 2) continue;
                    float delta = count % 3 == 1
                        ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                    if (run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds + delta)
                    {
                        core.HandleInput(run.Pattern.Channel, clock);
                        Check();
                    }
                    if (frame % 240 == 0)
                    {
                        ui.ToggleDevCleanView();
                        Call(ui, "UpdateRunTimingVisuals");
                        ui.ToggleDevCleanView();
                        Check();
                    }
                }
                core.TogglePause(clock);
                Check();
                Require(cached(run) == string.Empty, "Paused timing must not inherit the last active compact label.");
                core.TogglePause(clock);
                Check();
                core.Tick(clock += 2f);
                Check();
                core.ForceStopInfiniteRunForTest();
                Check();
                Require(cached(run) == string.Empty, "Terminal timing must not revive a cached active label.");
                core.RestartPractice(clock);
                core.Tick(clock += 2f);
                Check();
                Require(changedWindow && sawEarly && sawCenter && sawLate && sawMissed,
                    "Cache checks must cover actual changing windows, approach, exact center, late Good and missed timing.");
                Debug.Log($"Greed compact timing cache passed: {comparisons} real-snapshot comparisons,"
                    + " repeated string reuse, changing threat windows, early/center/late/missed timing, F11 round trips, pause/countdown/terminal and fresh practice. Personal settings and slots unchanged.");
            }
            finally
            {
                if (ui.UsePlayerRunControls != originalPlayerControls) ui.ToggleDevCleanView();
            }

            void Check()
            {
                string expected = fresh(run);
                string label = cached(run);
                Require(label == expected, "Cached compact timing must equal its current uncached label.");
                for (int repeat = 0; repeat < 8; repeat++)
                    Require(ReferenceEquals(label, cached(run)), "Unchanged compact timing must reuse one string.");
                comparisons++;
                ui.RenderRun(run);
                Call(ui, "UpdateRunTimingVisuals");
                if (string.IsNullOrEmpty(expected)) return;
                Require(Read<Text>(ui, "timingGuideText").text == expected,
                    "The actual basic timing graphic must use the current cached label.");
                int window = Mathf.RoundToInt(run.Pattern.SuccessWindowSeconds * 1000f);
                if (firstWindow < 0) firstWindow = window;
                changedWindow |= firstWindow != window;
                sawEarly |= expected.Contains("박 전", StringComparison.Ordinal);
                sawCenter |= expected.StartsWith("정박", StringComparison.Ordinal);
                sawLate |= expected == "늦음 · Good 가능";
                sawMissed |= expected == "놓침";
            }
        }

        internal static void VerifyGaugeLabelReuse(GreedLastRuntimeUi ui)
        {
            Func<GreedLastRunSnapshot, string> Bind(string name, bool instance) =>
                (Func<GreedLastRunSnapshot, string>)(instance
                    ? Delegate.CreateDelegate(typeof(Func<GreedLastRunSnapshot, string>), ui,
                        typeof(GreedLastRuntimeUi).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic))
                    : Delegate.CreateDelegate(typeof(Func<GreedLastRunSnapshot, string>),
                        typeof(GreedLastRuntimeUi).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)));
            var health = Bind("GetHealthGaugeLabel", true);
            var focus = Bind("GetFocusGaugeLabel", true);
            var freshHealth = Bind("FormatHealthValue", false);
            var freshFocus = Bind("FormatFocusStatus", false);
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            bool originalPlayerControls = ui.UsePlayerRunControls;
            if (!originalPlayerControls) ui.ToggleDevCleanView();
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                GreedLastScreenState.InfiniteRun, "Gauge label cache check", string.Empty, 0));
            int checks = 0;
            int initialMaxHealth = -1;
            bool changedMaxHealth = false;
            bool sawActive = false;
            bool sawInactive = false;
            bool sawTimerChange = false;
            float previousRemaining = -1f;
            try
            {
                foreach (int growth in new[] { 0, 1, 3, 5 })
                {
                    core.ConfigureGrowth(growth, Mathf.Min(growth, 3), 0);
                    core.EnterInfiniteRun();
                    if (!run.DevInvincible) core.ToggleDevInvincible();
                    float clock = Time.realtimeSinceStartup;
                    Check();
                    for (int frame = 0; frame < 600; frame++)
                    {
                        core.Tick(clock += 1f / 60f);
                        Require(!run.Stopped, "Every growth configuration must remain live throughout gauge verification.");
                        if (frame == 60) core.FillFocusForTest();
                        if (frame == 180) core.DropHealthForDangerTest();
                        Check();
                        if (frame == 240)
                        {
                            core.TogglePause(clock);
                            Check();
                            Require(run.Paused && !run.ResumeCountdownActive,
                                "Gauge timer stability must be checked in a real paused run.");
                            string pausedFocus = focus(run);
                            for (int wait = 0; wait < 6; wait++)
                            {
                                core.Tick(clock += 0.1f);
                                Check();
                                Require(ReferenceEquals(pausedFocus, focus(run)),
                                    "A paused focus timer must retain its actual frozen label.");
                            }
                            core.TogglePause(clock);
                            Check();
                            Require(run.ResumeCountdownActive, "Gauge resume verification must use the actual countdown.");
                            core.Tick(clock += 1.5f);
                            Check();
                            Require(!run.Paused && !run.CountdownActive,
                                "The gauge fixture must return to live play after the countdown.");
                        }
                        if (frame == 360)
                        {
                            ui.ToggleDevCleanView();
                            Check();
                            ui.ToggleDevCleanView();
                            Check();
                        }
                    }
                    core.ForceStopInfiniteRunForTest();
                    Check();
                }
                core.ConfigureGrowth(0, 0, 0);
                core.RestartPractice(Time.realtimeSinceStartup);
                Check();
                Require(changedMaxHealth && sawActive && sawInactive && sawTimerChange,
                    "Gauge checks must include real maximum changes, activation/expiry and advancing focus time.");
                Debug.Log($"Greed gauge label cache passed: {checks} real-snapshot comparisons,"
                    + " four growth configurations, damage, focus activation/expiry/timer, pause/resume, F11, results and fresh practice; unchanged labels reuse strings without changing value formats. Personal settings and slots unchanged.");
            }
            finally
            {
                if (ui.UsePlayerRunControls != originalPlayerControls) ui.ToggleDevCleanView();
            }

            void Check()
            {
                string healthLabel = health(run);
                string focusLabel = focus(run);
                Require(healthLabel == "체력 " + freshHealth(run) && focusLabel == freshFocus(run),
                    "Cached gauge labels must match the exact original current-value formatting.");
                for (int repeat = 0; repeat < 8; repeat++)
                    Require(ReferenceEquals(healthLabel, health(run)) && ReferenceEquals(focusLabel, focus(run)),
                        "An unchanged gauge snapshot must reuse both label strings.");
                ui.RenderRun(run);
                Require(Read<Text>(ui, "healthGaugeText").text == healthLabel
                    && Read<Text>(ui, "focusGaugeText").text == focusLabel,
                    "The actual gauge graphics must immediately reflect current labels in both views.");
                if (initialMaxHealth < 0) initialMaxHealth = run.MaxHealth;
                changedMaxHealth |= initialMaxHealth != run.MaxHealth;
                sawActive |= run.FocusStateActive;
                sawInactive |= !run.FocusStateActive;
                sawTimerChange |= run.FocusStateActive && previousRemaining >= 0f
                    && previousRemaining != run.FocusStateRemainingSeconds;
                previousRemaining = run.FocusStateActive ? run.FocusStateRemainingSeconds : -1f;
                checks++;
            }
        }

        internal static void VerifyIdleSafeAreaRefresh(GreedLastRuntimeUi ui)
        {
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Viewport check", string.Empty, 0));
            RectTransform root = Read<RectTransform>(ui, "rootPanelRect");
            root.anchorMin = new Vector2(0.2f, 0.2f);
            root.anchorMax = new Vector2(0.8f, 0.8f);
            Set(ui, "lastSafeAreaScreenWidth", -1);
            // No Render or state change: a settled menu still needs a viewport refresh.
            Call(ui, "LateUpdate");
            Require(Read<int>(ui, "lastSafeAreaScreenWidth") == Screen.width,
                "A stationary lobby must refresh the safe area without another Render/state transition.");
            Rect safe = Screen.safeArea;
            Require(Vector2.Distance(root.anchorMin, new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height)) < 0.001f
                && Vector2.Distance(root.anchorMax, new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height)) < 0.001f,
                "The idle frame must apply the current screen safe area, not stale menu anchors.");
            MethodInfo apply = typeof(GreedLastRuntimeUi).GetMethod("ApplySafeAreaSample", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Sample(Rect area, int width, int height, bool force = false)
                => (bool)apply.Invoke(ui, new object[] { area, width, height, force });
            void Check(Rect area, int width, int height, Vector2 min, Vector2 max)
            {
                Require(Sample(area, width, height, true), "A valid viewport must be accepted.");
                Require(Vector2.Distance(root.anchorMin, min) < 0.00001f && Vector2.Distance(root.anchorMax, max) < 0.00001f,
                    "Safe-area anchors must follow the new viewport without changing screens.");
                Require(root.offsetMin == Vector2.zero && root.offsetMax == Vector2.zero,
                    "Safe-area changes must not accumulate pixel offsets.");
                for (int repeat = 0; repeat < 10; repeat++)
                    Require(!Sample(area, width, height), "Unchanged safe areas must not reapply layout each frame.");
            }
            Check(new Rect(120, 36, 2196, 1026), 2340, 1080, new Vector2(120f / 2340f, 36f / 1080f), new Vector2(2316f / 2340f, 1062f / 1080f));
            Check(new Rect(24, 36, 2196, 1026), 2340, 1080, new Vector2(24f / 2340f, 36f / 1080f), new Vector2(2220f / 2340f, 1062f / 1080f));
            Check(new Rect(12, 16, 916, 516), 960, 540, new Vector2(12f / 960f, 16f / 540f), new Vector2(928f / 960f, 532f / 540f));
            Check(new Rect(-20, -30, 1000, 590), 960, 540, Vector2.zero, Vector2.one);
            // Equal pixel rectangles can map differently after a window resize.
            Check(new Rect(12, 16, 916, 516), 1920, 1080, new Vector2(12f / 1920f, 16f / 1080f), new Vector2(928f / 1920f, 532f / 1080f));
            Vector2 lastMin = root.anchorMin;
            Vector2 lastMax = root.anchorMax;
            Rect lastArea = Read<Rect>(ui, "lastSafeArea");
            foreach (Rect invalid in new[] { new Rect(), new Rect(0, 0, -100, 100), new Rect(0, 0, 100, -10),
                new Rect(2000, 0, 10, 100), new Rect(0, 1200, 100, 10), new Rect(0, 0, 0.5f, 100),
                new Rect(float.NaN, 0, 100, 100), new Rect(0, 0, float.PositiveInfinity, 100),
                new Rect(0, float.NegativeInfinity, 100, 100), new Rect(float.MaxValue, 0, float.MaxValue, 100) })
            {
                Require(!Sample(invalid, 1920, 1080, true), "Invalid/empty OS samples must not replace a valid layout.");
                Require(root.anchorMin == lastMin && root.anchorMax == lastMax && Read<Rect>(ui, "lastSafeArea") == lastArea,
                    "Rejected OS samples must preserve both layout and cache for recovery.");
            }
            Require(!Sample(lastArea, 0, 1080) && !Sample(lastArea, 1920, 0)
                && !Sample(lastArea, -1, 1080), "Unavailable/minimized viewports must leave the layout intact.");
            Check(new Rect(0, 0, 1024, 768), 1024, 768, Vector2.zero, Vector2.one);
            Call(ui, "LateUpdate");
            Debug.Log("Greed Last viewport checks passed: idle refresh, opposite notches, resized dimensions, clamped edges, unchanged-sample caching and invalid-sample recovery. Synthetic screen samples only.");
        }

        internal static void VerifyRunTransitions(GreedLastRuntimeUi ui)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            void Draw()
            {
                ui.RenderRun(run);
                Canvas.ForceUpdateCanvases();
                Call(ui, "Update");
            }
            bool Visible(Graphic graphic) => graphic != null && graphic.isActiveAndEnabled && graphic.color.a >= 0.01f;
            void RequireHidden(string context)
            {
                Require(!Visible(Read<Image>(ui, "runnerPreparationCue"))
                    && !Visible(Read<Text>(ui, "runnerPreparationSymbolText")), context + " must hide preparation cues.");
                foreach (Image pip in Read<Image[]>(ui, "runnerFootfallPips"))
                    Require(!Visible(pip), context + " must hide running footfall cues.");
            }
            void BeginLive()
            {
                core.Enter();
                core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock += 3f);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "HUD transition", string.Empty, 0));
                Draw();
                Require(run.ActivePattern && !run.Paused && !run.Stopped, "The transition fixture must restore a live trap.");
                bool hasFootfall = false;
                foreach (Image pip in Read<Image[]>(ui, "runnerFootfallPips")) hasFootfall |= Visible(pip);
                Require(hasFootfall, "Fresh live runs must restore visible footfall cues.");
            }

            BeginLive();
            for (int cycle = 0; cycle < 2; cycle++)
            {
                core.TogglePause(clock += 0.1f);
                Draw();
                Require(run.Paused, "Pause transitions must reach the real paused core state.");
                RequireHidden("Pause");
                core.TogglePause(clock += 0.1f);
                Draw();
                Require(run.ResumeCountdownActive, "Resume transitions must pass through the countdown.");
                RequireHidden("Resume countdown");
                core.Tick(clock += 2f);
                Draw();
                Require(!run.Paused && !run.CountdownActive, "The resume countdown must release live play.");
            }
            core.JumpToDevShortcut(GreedLastDevShortcut.GiftChoice, clock += 1f);
            Draw();
            Require(run.ChoiceActive, "Choice transitions must reach a real gift selection.");
            RequireHidden("Gift choice");
            BeginLive();
            core.EnterInfiniteRun();
            core.Tick(clock = Time.realtimeSinceStartup + 3f);
            core.Tick(clock += 1f);
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.InfiniteRun, "HUD infinite", string.Empty, 0));
            Draw();
            core.ForceStopInfiniteRunForTest();
            Draw();
            Require(run.Stopped, "Result transitions must reach a stopped core state.");
            RequireHidden("Infinite result");
            foreach (GreedLastScreenState state in new[] { GreedLastScreenState.LobbyReady, GreedLastScreenState.SaveLoadoutDraft, GreedLastScreenState.InfiniteRecordBoard })
            {
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(state, "HUD menu", string.Empty, 0));
                Call(ui, "Update");
                RequireHidden(state.ToString());
            }
            BeginLive();
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady, "Lobby", string.Empty, 0));
            Call(ui, "Update");
            Debug.Log("Greed Last HUD transitions passed: repeated pause/countdown/GO, choice, fresh run, infinite result, lobby, save and record screens hide/restore their own runner cues.");
        }

        internal static void VerifyMenuTimingTransitions(GreedLastRuntimeUi ui)
        {
            string[] timingFields = { "trapMarker", "trapIllustration", "laneDangerBand", "trapGlow",
                "trapCorridorShadow", "trapStrike", "trapContactCue", "goodTimingBand", "successTimingBand",
                "centerTimingBand", "rhythmBreakLine", "inputTimingTraceMarker", "inputTimingTraceText",
                "activeActionCueText", "timingGuideText", "resolvedTrapAfterimage", "resolvedTrapImpact", "feedbackFlash",
                "runnerPreparationCue", "runnerPreparationSymbolText" };
            bool Visible(Graphic graphic) => graphic != null && graphic.isActiveAndEnabled && graphic.color.a > 0f
                && (!(graphic is Text label) || !string.IsNullOrEmpty(label.text));
            void RequireHidden(string context)
            {
                foreach (string field in timingFields)
                    Require(!Visible(Read<Graphic>(ui, field)), context + " must immediately hide " + field);
                foreach (string field in new[] { "timingBeatPips", "trapWarningRails", "threeActionExactBurstPips",
                    "runnerFootfallPips", "runnerActionTrails", "runMotionLines" })
                    foreach (Image part in Read<Image[]>(ui, field))
                        Require(!Visible(part), context + " must immediately hide " + field);
            }
            int cases = 0;
            foreach (GreedLastJudgementResult result in new[] { GreedLastJudgementResult.None,
                GreedLastJudgementResult.Success, GreedLastJudgementResult.Good, GreedLastJudgementResult.Miss })
            foreach (GreedLastScreenState destination in new[] { GreedLastScreenState.SaveLoadoutDraft,
                GreedLastScreenState.InfiniteLoadoutSelect, GreedLastScreenState.InfiniteStartReady,
                GreedLastScreenState.LobbyReady, GreedLastScreenState.InfiniteRecordBoard,
                GreedLastScreenState.GrowthBoard, GreedLastScreenState.ProfileBoard })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.EnterInfiniteRun();
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(run.ActivePattern, "Menu timing checks need an authored live trap.");
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.InfiniteRun,
                    "주행", string.Empty, 0));
                ui.RenderRun(run);
                Call(ui, "Update");
                Require(Visible(Read<Image>(ui, "goodTimingBand")), "The source timing band must first be visible.");
                if (result == GreedLastJudgementResult.None)
                {
                    GreedLastRunRecord beforePause = core.CreateRunRecord();
                    GreedLastPatternModel beforePattern = run.Pattern;
                    core.TogglePause(clock += 0.1f);
                    ui.RenderRun(run);
                    Require(run.Paused && !run.CountdownActive, "A real pause must precede the blocked-frame check.");
                    RequireHidden("Pause render before UI Update");
                    core.TogglePause(clock += 0.1f);
                    ui.RenderRun(run);
                    Require(run.ResumeCountdownActive, "Resume must retain its real lead-in countdown.");
                    RequireHidden("Resume countdown render before UI Update");
                    Require(core.CreateRunRecord().Equals(beforePause), "Pause/countdown cleanup must not add gameplay progress.");
                    core.Tick(clock += 2f);
                    ui.RenderRun(run);
                    Call(ui, "Update");
                    Require(run.ActivePattern && !run.CountdownActive && !run.Paused
                        && run.Pattern.Equals(beforePattern) && core.CreateRunRecord().Equals(beforePause)
                        && Visible(Read<Image>(ui, "goodTimingBand")), "The same live trap and unchanged record must restore its timing band after resume.");
                }
                if (result != GreedLastJudgementResult.None)
                {
                    float delta = result == GreedLastJudgementResult.Good
                        ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                    GreedLastRunChannel channel = result == GreedLastJudgementResult.Miss
                        ? (run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Center : GreedLastRunChannel.Left)
                        : run.Pattern.Channel;
                    core.HandleInput(channel, clock += run.Pattern.CoreSeconds - run.ElapsedSeconds
                        - core.InputTimingOffsetSeconds + delta);
                    Require(run.LastResult == result && !run.Stopped,
                        "The source judgement must be produced by actual input: expected=" + result
                        + ", actual=" + run.LastResult + ", stopped=" + run.Stopped
                        + ", delta=" + run.LastInputTimingDeltaSeconds + ", ignore=" + run.IgnoreReason + ", miss=" + run.MissReason);
                    ui.RenderRun(run);
                    Call(ui, "Update");
                    Require(Visible(Read<Image>(ui, result == GreedLastJudgementResult.Miss
                        ? "rhythmBreakLine" : "inputTimingTraceMarker")), "The source feedback must first be visible.");
                }
                GreedLastRunRecord before = core.CreateRunRecord();
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(destination, "메뉴", string.Empty, 0));
                RequireHidden(result + " -> " + destination);
                if (destination != GreedLastScreenState.LobbyReady)
                    Require(!Read<GameObject>(ui, "runnerAvatarRoot").activeInHierarchy
                        && !Read<Image>(ui, "runnerShadow").gameObject.activeInHierarchy,
                        "Non-lobby menus must immediately hide the live runner and shadow: " + destination);
                Call(ui, "Update");
                RequireHidden("Menu update " + result + " -> " + destination);
                Require(core.CreateRunRecord().Equals(before), "Timing cleanup must not change gameplay data.");
                if (result == GreedLastJudgementResult.Miss)
                {
                    ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.InfiniteRun,
                        "주행", string.Empty, 0));
                    ui.RenderRun(run);
                    Call(ui, "Update");
                    Require(run.Staggered && Visible(Read<Image>(ui, "goodTimingBand")),
                        "A real live Miss must still display its recovery band.");
                    core.TogglePause(clock += 0.1f);
                    ui.RenderRun(run);
                    Call(ui, "Update");
                    RequireHidden("Paused Miss recovery update");
                    core.TogglePause(clock += 0.1f);
                    ui.RenderRun(run);
                    Call(ui, "Update");
                    RequireHidden("Countdown Miss recovery update");
                    Require(core.CreateRunRecord().Equals(before), "Pausing a real Miss must not change its recorded outcome.");
                    for (int step = 0; step < 16 && !run.Stopped; step++) core.Tick(clock += 3f);
                    Require(run.Stopped && run.Health == 0, "Recovery checks must reach a natural terminal failure.");
                    ui.RenderRun(run);
                    Call(ui, "Update");
                    RequireHidden("Natural health-zero recovery update");
                }
                cases++;
            }
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady,
                "로비", string.Empty, 0));
            Debug.Log("Greed menu timing transitions passed: " + cases
                + " actual active/exact/Good/Miss sources across seven menus, seven live pause/resume restorations"
                + " and seven stagger pause/countdown/natural health-zero results without changing stored settings or slots.");
        }

        internal static void VerifyCompactProgress(GreedLastRuntimeUi ui)
        {
            bool previousCleanView = Read<bool>(ui, "devCleanViewEnabled");
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            Text progress = Read<Text>(ui, "runProgressText");
            RectTransform progressRoot = Read<GameObject>(ui, "runProgressRoot").GetComponent<RectTransform>();
            try
            {
                if (!previousCleanView) ui.ToggleDevCleanView();
                void Draw(string expected)
                {
                    ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                        run.InfiniteMode ? GreedLastScreenState.InfiniteRun : GreedLastScreenState.RunCoreTest,
                        "Progress check", string.Empty, 0));
                    ui.RenderRun(run);
                    Require(progress.text == expected && progress.fontSize == 34
                        && progress.rectTransform.rect.height == 40f && !progress.raycastTarget,
                        "Clean progress must show a readable single-line stage/count/threat without taking input: " + expected);
                }
                void BeginLive()
                {
                    core.Tick(clock += 4f);
                    core.Tick(clock += 1f);
                    Require(run.ActivePattern && !run.CountdownActive, "Progress checks need a live trap.");
                }

                core.RestartPractice(clock);
                BeginLive();
                Draw("연습 1/3");
                Set(ui, "stageTransitionStartedAt", Time.unscaledTime - 0.25f);
                Set(ui, "stageTransitionUntil", Time.unscaledTime + 1f);
                Set(ui, "stageTransitionCuePending", true);
                Call(ui, "UpdateStageTransitionBanner");
                Require(Read<Text>(ui, "stageTransitionText").color.a == 0f
                    && Read<Image>(ui, "stageTransitionLine").color.a == 0f
                    && !Read<bool>(ui, "stageTransitionCuePending"),
                    "Clean practice must hide its duplicate banner while still consuming the scheduled start cue.");
                RectTransform laneHit = Read<Button[]>(ui, "laneHitButtons")[1].GetComponent<RectTransform>();
                Vector2 laneHitSize = laneHit.sizeDelta;
                for (int toggle = 0; toggle < 3; toggle++)
                {
                    ui.ToggleDevCleanView();
                    Require(progress.text.Contains("기초연습 단계 1/3", StringComparison.Ordinal)
                        && progress.fontSize <= 24 && progressRoot.anchorMin.y == 0.715f
                        && Read<Text>(ui, "stageTransitionText").color.a > 0f,
                        "F11 must restore detailed progress and the practice banner immediately.");
                    ui.ToggleDevCleanView();
                    Require(progress.text == "연습 1/3" && progressRoot.anchorMin.y == 0.705f
                        && Read<Text>(ui, "stageTransitionText").color.a == 0f
                        && !Read<bool>(ui, "stageTransitionCuePending")
                        && laneHit.sizeDelta == laneHitSize,
                        "Repeated clean-view switches must restore compact progress without resizing input targets.");
                }

                core.Enter();
                Draw("시작 기프트 선택");
                for (int choice = 0; run.ChoiceActive && choice < 3; choice++)
                    core.HandleInput(GreedLastRunChannel.Center, clock += 0.3f);
                BeginLive();
                Draw("1장 · 0/3");
                core.JumpToDevShortcut(GreedLastDevShortcut.AfterCore, clock += 1f);
                BeginLive();
                Draw("탈출 · 0/3");
                core.EnterInfiniteRun();
                clock = Time.realtimeSinceStartup;
                BeginLive();
                Draw("1구간 · 0/3 · 위협 1");
                ui.ToggleDevCleanView();
                Require(progress.text.StartsWith("무한 생존", StringComparison.Ordinal)
                    && progress.text.Contains("위협 1/5", StringComparison.Ordinal),
                    "Detailed infinite progress must retain its original pressure/reward description.");
                ui.ToggleDevCleanView();
                Require(progress.text == "1구간 · 0/3 · 위협 1", "Clean infinite progress must restore immediately.");
                core.ForceStopInfiniteRunForTest();
                ui.RenderRun(run);
                Require(!Read<GameObject>(ui, "runProgressRoot").activeSelf,
                    "Terminal results must still hide live progress.");
                Debug.Log("Greed compact progress passed: practice, choice, normal, escape, infinite, repeated clean/detail switches and terminal hiding. Isolated UI/core; no player settings or slots changed.");
            }
            finally
            {
                if (Read<bool>(ui, "devCleanViewEnabled") != previousCleanView) ui.ToggleDevCleanView();
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.LobbyReady,
                    "Lobby", string.Empty, 0));
            }
        }

        internal static void Run()
        {
            var host = new GameObject("GreedHudClearanceCheck", typeof(RectTransform));
            try
            {
                var ui = host.AddComponent<GreedLastRuntimeUi>();
                RectTransform parent = host.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(1200f, 650f);
                Text guide = Create<Text>(parent, "TimingGuide", new Vector2(760f, 64f));
                guide.text = "Timing";
                guide.color = Color.white;
                Image cue = Create<Image>(parent, "RunnerPreparationCue", new Vector2(132f, 16f));
                cue.color = Color.white;
                cue.raycastTarget = false;
                Set(ui, "timingGuideText", guide);
                Set(ui, "runnerPreparationCue", cue);
                MethodInfo keepClear = typeof(GreedLastRuntimeUi).GetMethod("KeepRunnerCuesAboveTimingGuide", BindingFlags.Instance | BindingFlags.NonPublic);
                void Apply() => keepClear.Invoke(ui, null);
                int cases = 0;
                foreach (float parentAngle in new[] { -8f, 0f, 8f })
                foreach (float cueAngle in new[] { -35f, 0f, 35f })
                foreach (float guideScale in new[] { 1f, 1.3f })
                foreach (float cueScale in new[] { 0.8f, 1f, 1.5f })
                {
                    parent.localEulerAngles = new Vector3(0f, 0f, parentAngle);
                    parent.localScale = new Vector3(0.7f, 1.1f, 1f);
                    guide.rectTransform.anchoredPosition = new Vector2(0f, -90f);
                    guide.rectTransform.localScale = Vector3.one * guideScale;
                    cue.rectTransform.anchoredPosition = new Vector2(30f, -95f);
                    cue.rectTransform.localEulerAngles = new Vector3(0f, 0f, cueAngle);
                    cue.rectTransform.localScale = new Vector3(cueScale, 1.2f, 1f);
                    Vector3 scale = cue.rectTransform.localScale;
                    Quaternion rotation = cue.rectTransform.localRotation;
                    Vector3 guidePosition = guide.rectTransform.localPosition;
                    Apply();
                    Bounds text = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, guide.rectTransform);
                    Bounds effect = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, cue.rectTransform);
                    Require(effect.min.y >= text.max.y + 7.99f, "Rotated/scaled cues must clear the pulsing timing text.");
                    Require(Mathf.Abs(cue.rectTransform.anchoredPosition.x - 30f) < 0.001f
                        && cue.rectTransform.localScale == scale && cue.rectTransform.localRotation == rotation
                        && guide.rectTransform.localPosition == guidePosition,
                        "Clearance may lift the effect but must preserve text, horizontal alignment and pose.");
                    Vector3 settled = cue.rectTransform.localPosition;
                    for (int repeat = 0; repeat < 10; repeat++) Apply();
                    Require(Vector3.Distance(settled, cue.rectTransform.localPosition) < 0.001f,
                        "Repeated layout checks must not make a cue drift upward.");
                    cases++;
                }

                void RequireUnmoved(string context)
                {
                    Vector3 before = cue.rectTransform.localPosition;
                    Apply();
                    Require(Vector3.Distance(before, cue.rectTransform.localPosition) < 0.001f, context);
                }
                cue.rectTransform.anchoredPosition = new Vector2(900f, -95f);
                RequireUnmoved("An effect outside the text width must keep its authored pose.");
                cue.rectTransform.anchoredPosition = new Vector2(0f, 200f);
                RequireUnmoved("A cue already above the guide must not move.");
                cue.rectTransform.anchoredPosition = new Vector2(0f, -95f);
                guide.text = string.Empty;
                RequireUnmoved("Empty timing text must not reserve a visible exclusion area.");
                guide.text = "Timing";
                guide.enabled = false;
                RequireUnmoved("Disabled timing text must not affect the runner.");
                guide.enabled = true;
                guide.gameObject.SetActive(false);
                RequireUnmoved("Hidden timing text must not affect the runner.");
                guide.gameObject.SetActive(true);
                guide.color = Color.clear;
                RequireUnmoved("Transparent timing text must not affect the runner.");
                guide.color = Color.white;
                cue.color = Color.clear;
                RequireUnmoved("Transparent cues must not be relocated.");
                cue.color = Color.white;
                cue.enabled = false;
                RequireUnmoved("Disabled cues must not be relocated.");
                cue.enabled = true;
                cue.gameObject.SetActive(false);
                RequireUnmoved("Hidden cues must not be relocated.");
                Set(ui, "timingGuideText", null);
                Apply();
                Debug.Log("Greed Last HUD clearance passed: " + cases + " rotated/scaled cases, repeated-update stability, fixed text/input alignment and hidden/empty guards. Isolated geometry fixture only.");
            }
            finally
            {
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static T Create<T>(Transform parent, string name, Vector2 size) where T : Graphic
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            T graphic = node.AddComponent<T>();
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            graphic.rectTransform.sizeDelta = size;
            return graphic;
        }

        private static void Set(GreedLastRuntimeUi ui, string name, object value)
            => typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, value);
        private static T Read<T>(GreedLastRuntimeUi ui, string name)
            => (T)typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        private static void Call(GreedLastRuntimeUi ui, string name)
            => typeof(GreedLastRuntimeUi).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
