using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeVisualCheck
    {
        private const string MenuPath = "Greed Last/Capture Prototype Visual Checks";
        private static readonly Queue<Frame> Frames = new Queue<Frame>();
        private static Scene captureScene;
        private static GreedLastRuntimeUi ui;
        private static Canvas canvas;
        private static Camera camera;
        private static EventSystem eventSystem;
        private static RenderTexture target;
        private static Frame currentFrame;
        private static double frameStartedAt;
        private static bool running;
        private static int capturedFrameCount;
        private static CaptureReport report;

        [Serializable]
        private sealed class CaptureReport
        {
            public string startedUtc;
            public string completedUtc;
            public string unityVersion;
            public bool succeeded;
            public string error;
            public bool pointerLoopPassed;
            public int pointerLoopCases;
            public int pointerLoopClicks;
            public int pointerLoopCanceledCommands;
            public List<CaptureResult> frames = new List<CaptureResult>();
        }

        [Serializable]
        private sealed class CaptureResult
        {
            public string file;
            public int width;
            public int height;
            public float safeInset;
            public Vector4 safeInsets;
            public int checkedButtons;
            public int checkedModuleClicks;
            public int checkedModuleLaneInputs;
            public int checkedModuleScrolls;
            public int checkedControlPoints;
            public int checkedBlockedLanePoints;
            public int checkedLaneLabels;
            public int checkedFeedbackAreas;
            public int checkedRunnerCueClearances;
            public bool retainedScreen;
            public int brightSamples;
            public int detailSamples;
        }

        private sealed class Frame
        {
            public string Name;
            public int Width;
            public int Height;
            public float SafeInset;
            public Vector4 SafeInsets;
            public double SettleSeconds = 0.5;
            public Action Show;
            public Action BeforeUpdate;
            public bool RetainedScreen;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play mode before capturing prototype frames.");
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException("Visual capture needs a graphics device; do not use -nographics.");
            }

            running = true;
            capturedFrameCount = 0;
            report = new CaptureReport
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
            };
            try
            {
                GreedLastPrototypeAppLoopCheck.RunPointerLoop();
                report.pointerLoopPassed = true;
                report.pointerLoopCases = GreedLastPrototypeAppLoopCheck.LastPointerCaseCount;
                report.pointerLoopClicks = GreedLastPrototypeAppLoopCheck.LastPointerClickCount;
                report.pointerLoopCanceledCommands = GreedLastPrototypeAppLoopCheck.LastCanceledCommandCount;
                captureScene = EditorSceneManager.NewPreviewScene();
                camera = new GameObject("GreedVisualCheckCamera", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(11, 16, 18, 255);
                camera.cameraType = CameraType.Game;
                camera.scene = captureScene;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.enabled = false;
                ui = new GameObject("GreedVisualCheckUi").AddComponent<GreedLastRuntimeUi>();
                GameObject existingEventSystem = GameObject.Find("GreedLastEventSystem");
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                canvas = Read<GameObject>("rootPanel").GetComponentInParent<Canvas>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, captureScene);
                SceneManager.MoveGameObjectToScene(ui.gameObject, captureScene);
                SceneManager.MoveGameObjectToScene(canvas.gameObject, captureScene);
                GameObject createdEventSystem = GameObject.Find("GreedLastEventSystem");
                eventSystem = createdEventSystem == null ? UnityEngine.Object.FindAnyObjectByType<EventSystem>() : createdEventSystem.GetComponent<EventSystem>();
                if (existingEventSystem == null && createdEventSystem != null)
                {
                    SceneManager.MoveGameObjectToScene(createdEventSystem, captureScene);
                }
                canvas.GetComponent<CanvasScaler>().enabled = false;
                // World-space projection preserves the same 1920-unit layout in an isolated preview camera.
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;

                // The fixture owns only a transient core, never the user's saved profile.
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot latest = default;
                core.SnapshotChanged += snapshot => latest = snapshot;
                float now = Time.realtimeSinceStartup;
                core.Enter();
                GreedLastRunSnapshot choice = latest;
                core.RestartPractice(now - 4f);
                core.Tick(now - 1f);
                core.Tick(now);
                if (!latest.ActivePattern) throw new InvalidOperationException("Live visual fixture needs an active trap.");
                GreedLastRunSnapshot live = latest;
                core.ToggleAutoFlow(now + 0.05f);
                GreedLastRunSnapshot flowStopped = latest;
                core.ToggleAutoFlow(now + 0.075f);
                core.TogglePause(now + 0.1f);
                GreedLastRunSnapshot paused = latest;
                core.TogglePause(now + 0.2f);
                GreedLastRunSnapshot resume = latest;
                Add("00-title", 1280, 720, () => ShowScreen(GreedLastScreenState.BootInit, "GREED LAST"));
                Add("01-lobby", 1280, 720, () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"));
                Add("02-start-choice", 1280, 720, () => ShowRun(choice));
                Add("03-live-run", 1280, 720, () => ShowRun(live));
                Add("04-pause", 1280, 720, () => ShowRun(paused));
                Add("05-resume", 1280, 720, () => ShowRun(resume));
                Add("06-lobby-desktop", 1920, 1080, () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"));
                Add("07-live-wide-safe", 2340, 1080, () => ShowRun(live), 96f);
                Add("08-pause-wide-safe", 2340, 1080, () => ShowRun(paused), 96f);
                Add("08-live-small", 960, 540, () => ShowRun(live));
                Add("08-pause-small", 960, 540, () => ShowRun(paused));
                Add("08-resume-tablet", 1024, 768, () => ShowRun(resume));
                AddCalibratedPauseFrames();
                AddSavedRunLoopFrames();
                GreedLastRunSnapshot practiceResult = GreedLastPrototypeSmokeCheck.RunPracticeReviewSample(
                    Time.realtimeSinceStartup,
                    new[] { GreedLastJudgementResult.Good, GreedLastJudgementResult.Good, GreedLastJudgementResult.Good });
                Add("25-practice-calibration", 1280, 720, () => ShowRun(practiceResult));
                Add("26-practice-calibration-tablet", 1024, 768, () => ShowRun(practiceResult));
                AddRetainedViewportFrames("73-practice-result", () => ShowRun(practiceResult));
                Add("27-live-after-practice-result", 1280, 720, () => ShowRun(live));
                AddAvoidanceFrames();
                AddJudgementRewardFrames();
                AddRouteStepFeedbackFrames();
                AddActualInputChannelFrames();
                AddStructuredTimingFrames();
                AddPracticeMissFrames();
                AddLongRunCounterFrames();
                AddGameplayChoiceFrames();
                AddBeatPulseFrames(live, resume);
                AddCombinedFeedbackFrames();
                Add("54-live-notch-left", 2340, 1080, () => ShowRun(live), safeInsets: new Vector4(120f, 36f, 24f, 18f));
                Add("55-live-notch-right", 2340, 1080, () => ShowRun(live), safeInsets: new Vector4(24f, 36f, 120f, 18f));
                Add("56-pause-notch-left", 2340, 1080, () => ShowRun(paused), safeInsets: new Vector4(120f, 36f, 24f, 18f));
                Add("57-resume-notch-right", 2340, 1080, () => ShowRun(resume), safeInsets: new Vector4(24f, 36f, 120f, 18f));
                Add("58-choice-safe-small", 960, 540, () => ShowRun(choice), safeInsets: new Vector4(32f, 16f, 12f, 8f));
                Add("59-pause-safe-small", 960, 540, () => ShowRun(paused), safeInsets: new Vector4(12f, 16f, 32f, 8f));
                AddRetainedViewportFrames("60-lobby", () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"));
                AddRetainedViewportFrames("61-choice", () => ShowRun(choice));
                AddRetainedViewportFrames("62-pause", () => ShowRun(paused));
                AddRetainedViewportFrames("79-player-trap-resume", () => ShowRun(flowStopped));
                AddConnectionFrames();
                Directory.CreateDirectory("Logs/VisualChecks");
                BeginNextFrame();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            }
            catch (Exception exception)
            {
                WriteCaptureReport(false, exception.Message);
                Cleanup();
                throw;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun() => !running && !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Add(string name, int width, int height, Action show, float safeInset = 0f, double settleSeconds = 0.5, Action beforeUpdate = null, Vector4? safeInsets = null, bool retainedScreen = false)
        {
            Frames.Enqueue(new Frame { Name = name, Width = width, Height = height, Show = show, SafeInset = safeInset,
                SafeInsets = safeInsets ?? new Vector4(safeInset, 0f, safeInset, 0f),
                SettleSeconds = settleSeconds, BeforeUpdate = beforeUpdate, RetainedScreen = retainedScreen });
        }

        private static void AddRetainedViewportFrames(string name, Action show)
        {
            string title = null;
            GreedLastRunSnapshot snapshot = default;
            void SameScreen()
            {
                Require(Read<Text>("titleText").text == title && Read<GreedLastRunSnapshot>("latestRunSnapshot").Equals(snapshot),
                    name + ": resize checks must keep the same screen and run snapshot, without another Render call.");
            }
            Add(name + "-initial", 2340, 1080, () =>
            {
                show();
                title = Read<Text>("titleText").text;
                snapshot = Read<GreedLastRunSnapshot>("latestRunSnapshot");
            }, safeInsets: new Vector4(120, 36, 24, 18));
            Add(name + "-flipped", 2340, 1080, SameScreen, safeInsets: new Vector4(24, 36, 120, 18), retainedScreen: true);
            Add(name + "-resized", 960, 540, SameScreen, safeInsets: new Vector4(12, 16, 32, 8), retainedScreen: true);
            Add(name + "-tablet", 1024, 768, SameScreen, retainedScreen: true);
        }

        private static void AddBeatPulseFrames(GreedLastRunSnapshot live, GreedLastRunSnapshot resume)
        {
            // Freeze only transient fixture timing, never gameplay clocks or saved preferences.
            void Prepare(bool peak)
            {
                Write("beatPhase", 0f);
                Write("lastCountdownBeatActive", Read<GreedLastRunSnapshot>("latestRunSnapshot").ResumeCountdownActive);
                Write("rhythmBeatIndex", 1);
                Write("downbeatPulseUntil", 0f);
                Write("rhythmPulseStartedAt", -1f);
                Write("nextBeatSoundAt", Time.unscaledTime + (peak ? -1f : 100f));
            }
            var contactCore = new GreedLastRunCore();
            GreedLastRunSnapshot contact = default;
            contactCore.SnapshotChanged += value => contact = value;
            float contactClock = Time.realtimeSinceStartup;
            contactCore.RestartPractice(contactClock);
            contactCore.Tick(contactClock += 2f);
            contactCore.Tick(contactClock += 1f);
            contactCore.Tick(contactClock + contact.Pattern.CoreSeconds - 0.08f);
            PublishAt(contactCore, contactClock + contact.Pattern.CoreSeconds - 0.08f);
            Require(contact.ActivePattern && Mathf.Abs(contact.CoreDeltaSeconds) < 0.1f,
                "Beat comparison frames must show a trap near its actual contact window.");
            Add("40-beat-rest-small", 960, 540, () => ShowRun(contact), beforeUpdate: () => Prepare(false));
            Add("41-beat-peak-small", 960, 540, () => ShowRun(contact), beforeUpdate: () => Prepare(true));
            Add("42-beat-muted-peak-wide", 2340, 1080, () => ShowRun(live), 96f, beforeUpdate: () =>
            {
                Prepare(true);
                Write("sfxVolume", 0f);
                typeof(GreedLastRuntimeUi).GetMethod("RefreshAudioPlaybackState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            });
            Add("43-beat-resume-tablet", 1024, 768, () => ShowRun(resume), beforeUpdate: () => Prepare(true));
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot infinite = default;
            core.SnapshotChanged += value => infinite = value;
            core.EnterInfiniteRun();
            core.Tick(Time.realtimeSinceStartup + 3f);
            core.Tick(Time.realtimeSinceStartup + 4f);
            for (int i = 1; i < 5; i++) core.RaiseInfiniteThreatForTest();
            Require(infinite.InfiniteThreatLevel == 5 && infinite.ActivePattern && !infinite.Stopped && !infinite.CountdownActive,
                "Beat captures require the live highest-threat fixture.");
            Add("44-beat-fast-rest", 1280, 720, () => ShowRun(infinite), beforeUpdate: () => Prepare(false));
            Add("45-beat-fast-peak", 1280, 720, () => ShowRun(infinite), beforeUpdate: () => Prepare(true));
        }

        private static void ShowScreen(GreedLastScreenState state, string title)
        {
            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(state, title, string.Empty, 0));
        }

        private static void AddConnectionFrames()
        {
            var gate = new GreedLastRequestGate();
            var state = new GreedLastStateMachine(gate);
            var backend = new GreedLastMockBackend(false, false);
            backend.AddSampleSaveLoadoutForTest(out _);
            GreedLastStateSnapshot snapshot = default;
            state.StateChanged += value => snapshot = value;
            void Capture(string name, int width, int height, float safeInset = 0f)
            {
                GreedLastStateSnapshot frame = snapshot;
                Add(name, width, height, () =>
                {
                    ui.Render(frame);
                    Require(Read<Button>("normalRunButton").IsInteractable() == frame.CoreActionsEnabled,
                        name + ": connection state and normal-run interaction must agree.");
                    Require(Read<Button>("retryButton").gameObject.activeSelf
                        == (frame.RetryVisible || frame.State == GreedLastScreenState.ConnectBlocked),
                        name + ": retry visibility must match the current request state.");
                }, safeInset);
            }
            state.SetLobbyReady(backend.LoadLobby());
            Require(gate.TryBegin(GreedLastRequestKind.ConnectCheck, out GreedLastRequestToken token),
                "Connection captures need their actual request gate.");
            state.SetConnectChecking();
            Capture("67-connect-loading", 1280, 720);
            gate.Complete(token);
            state.SetConnectBlocked(GreedLastConnectBlockReason.NetworkUnavailable, "네트워크 연결을 확인할 수 없습니다.");
            Capture("68-connect-blocked-small", 960, 540);
            state.SetReSyncPending("네트워크 연결을 확인할 수 없습니다.", false, GreedLastConnectBlockReason.NetworkUnavailable);
            Capture("69-resync-blocked-tablet", 1024, 768);
            Require(gate.TryBegin(GreedLastRequestKind.ReSync, out token), "Retry capture needs an active request.");
            state.SetReSyncPending("최신 로비 상태를 다시 확인하고 있습니다.", false);
            Capture("70-resync-busy-wide", 2340, 1080, 96f);
            gate.Complete(token);
            state.SetLobbySyncWarning(backend.LoadLobby());
            Capture("71-resync-warning-small", 960, 540);
            state.ShowLocalNotice("저장 정보 확인 완료");
            Capture("71-warning-notice-tablet", 1024, 768);
            state.SetReSyncPending("네트워크 연결을 확인할 수 없습니다.", false, GreedLastConnectBlockReason.NetworkUnavailable);
            state.ShowLocalNotice("연결 상태를 다시 확인해야 합니다.");
            Capture("71-blocked-notice-small", 960, 540);
            state.SetLobbyReady(backend.LoadLobby());
            Capture("72-recovered-lobby", 1280, 720);
        }

        private static void AddCombinedFeedbackFrames()
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.Enter();
            core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            clock += run.Pattern.CoreSeconds - 0.08f;
            core.Tick(clock);
            PublishAt(core, clock);
            GreedLastRunSnapshot healthy = run;
            core.DropHealthForDangerTest();
            core.DropHealthForDangerTest();
            PublishAt(core, clock);
            GreedLastRunSnapshot low = run;
            Require(low.Health == 1 && low.ActivePattern && !low.Stopped,
                "Danger captures need a live low-health trap.");
            Add("46-low-health-small", 960, 540, () => { ShowRun(healthy); ui.RenderRun(low); }, settleSeconds: 0.16);
            Add("47-low-health-wide-safe", 2340, 1080, () => { ShowRun(healthy); ui.RenderRun(low); }, 96f, 0.16);
            core.FillFocusForTest();
            PublishAt(core, clock);
            GreedLastRunSnapshot focused = run;
            Require(focused.FocusStateActive && focused.Health == 1, "Focus/danger must be active together.");
            Add("48-focus-danger-small", 960, 540, () => { ShowRun(low); ui.RenderRun(focused); }, settleSeconds: 0.16);
            Add("49-focus-danger-tablet", 1024, 768, () => { ShowRun(low); ui.RenderRun(focused); }, settleSeconds: 0.16);

            core.JumpToDevShortcut(GreedLastDevShortcut.AfterCore, clock += 3f);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            core.FillFocusForTest();
            PublishAt(core, clock);
            GreedLastRunSnapshot escape = run;
            Require(escape.CoreRetrieved && escape.FocusStateActive && !escape.Stopped,
                "Escape captures must retain the core and active focus.");
            Add("50-escape-focus-small", 960, 540, () => ShowRun(escape));
            Add("51-escape-focus-wide-safe", 2340, 1080, () => ShowRun(escape), 96f);

            core.EnterInfiniteRun();
            core.Tick(clock = Time.realtimeSinceStartup + 3f);
            core.Tick(clock += 1f);
            for (int i = 1; i < 5; i++) core.RaiseInfiniteThreatForTest();
            core.DropHealthForDangerTest();
            core.DropHealthForDangerTest();
            core.FillFocusForTest();
            PublishAt(core, clock);
            GreedLastRunSnapshot pressure = run;
            Require(pressure.InfiniteMode && pressure.ActivePattern && pressure.FocusStateActive
                && pressure.Health == 1 && pressure.InfiniteThreatLevel == 5,
                "Pressure captures must combine a real trap, max threat, low health and focus.");
            Add("52-infinite-pressure-small", 960, 540, () => ShowRun(pressure));
            Add("53-infinite-pressure-tablet", 1024, 768, () => ShowRun(pressure));
        }

        private static void ShowRun(GreedLastRunSnapshot snapshot)
        {
            ShowScreen(snapshot.InfiniteMode ? GreedLastScreenState.InfiniteRun : GreedLastScreenState.RunCoreTest, "피라미드 질주");
            ui.RenderRun(snapshot);
        }

        private static void AddGameplayChoiceFrames()
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.Enter();
            GreedLastRunSnapshot start = run;
            Add("33-start-choice-tablet", 1024, 768, () => ShowRun(start));
            core.JumpToDevShortcut(GreedLastDevShortcut.GiftChoice, clock);
            GreedLastRunSnapshot gift = run;
            Add("34-gift-choice", 1280, 720, () => ShowRun(gift));
            Add("35-gift-choice-tablet", 1024, 768, () => ShowRun(gift));
            core.JumpToDevShortcut(GreedLastDevShortcut.RelicChoice, clock);
            GreedLastRunSnapshot relic = run;
            Add("36-relic-choice", 1280, 720, () => ShowRun(relic));
            Add("37-relic-choice-tablet", 1024, 768, () => ShowRun(relic));
            core.HandleInput(GreedLastRunChannel.Center, clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            GreedLastRunSnapshot live = run;
            Add("38-live-after-choice", 1280, 720, () => ShowRun(live));
        }

        private static void AddAvoidanceFrames()
        {
            string[] names = { "floor", "overhead", "side", "rotate-left", "rotate-right" };
            for (int index = 0; index < names.Length; index++)
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                core.Enter();
                float clock = Time.realtimeSinceStartup;
                for (int cycle = 0; cycle <= index; cycle++)
                    core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(run.ActivePattern, "Avoidance capture requires a live trap.");
                core.Tick(clock + run.Pattern.CoreSeconds - 0.08f);
                PublishAt(core, clock + run.Pattern.CoreSeconds - 0.08f);
                GreedLastRunSnapshot approach = run;
                Require(Mathf.Abs(approach.CoreDeltaSeconds + 0.08f) < 0.001f,
                    "Approach frames must show the trap 80ms before its real judgement point.");
                string name = names[index];
                Add("28-approach-" + name, 1280, 720, () => ShowRun(approach));
                if (index == 3)
                    Add("29-rotation-tablet", 1024, 768, () => ShowRun(approach));
                if (index == 4)
                    Add("29-rotation-wide-safe", 2340, 1080, () => ShowRun(approach), 96f);

                GreedLastRunChannel channel = run.Pattern.Channel;
                float exact = clock + run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(channel, exact);
                Require(run.LastResult == GreedLastJudgementResult.Success, "Exact avoidance captures must resolve successfully.");
                GreedLastRunSnapshot resolved = run;
                Add("30-exact-" + name, 1280, 720, () =>
                {
                    ShowRun(approach);
                    ui.RenderRun(resolved);
                }, settleSeconds: 0.16);

                if (index >= 3) continue;
                foreach (bool miss in new[] { false, true })
                {
                    var outcomeCore = new GreedLastRunCore();
                    GreedLastRunSnapshot outcome = default;
                    outcomeCore.SnapshotChanged += value => outcome = value;
                    outcomeCore.Enter();
                    for (int cycle = 0; cycle <= index; cycle++)
                        outcomeCore.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock);
                    outcomeCore.Tick(clock += 2f);
                    outcomeCore.Tick(clock += 1f);
                    GreedLastRunSnapshot before = outcome;
                    float delta = miss ? 0f : (outcome.Pattern.SuccessWindowSeconds + outcome.Pattern.GoodWindowSeconds) * 0.5f;
                    GreedLastRunChannel input = miss
                        ? (channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left)
                        : channel;
                    outcomeCore.HandleInput(input, clock + outcome.Pattern.CoreSeconds - outcomeCore.InputTimingOffsetSeconds + delta);
                    Require(outcome.LastResult == (miss ? GreedLastJudgementResult.Miss : GreedLastJudgementResult.Good),
                        "Outcome captures must reflect the core's actual judgement.");
                    GreedLastRunSnapshot after = outcome;
                    Add((miss ? "32-miss-" : "31-good-") + name, 1280, 720, () =>
                    {
                        ShowRun(before);
                        ui.RenderRun(after);
                    }, settleSeconds: 0.16);
                }
            }
        }

        private static void AddJudgementRewardFrames()
        {
            var source = new GreedLastRunCore();
            GreedLastRunSnapshot sourceRun = default;
            source.SnapshotChanged += value => sourceRun = value;
            float clock = Time.realtimeSinceStartup;
            source.Enter();
            for (int step = 0; step < 256 && !sourceRun.Stopped; step++)
            {
                if (sourceRun.ChoiceActive) source.HandleInput(GreedLastRunChannel.Left, clock);
                else if (sourceRun.CountdownActive) source.Tick(clock += 2f);
                else if (!sourceRun.ActivePattern) source.Tick(clock += 0.1f);
                else
                {
                    clock += sourceRun.Pattern.CoreSeconds - source.InputTimingOffsetSeconds - sourceRun.ElapsedSeconds;
                    source.HandleInput(sourceRun.Pattern.Channel, clock);
                }
            }
            GreedLastRunRecord record = source.CreateRunRecord();
            Require(record.IsValid && sourceRun.SaveEligible, "Reward captures need a real completed normal run.");
            string[] names = { "early-exact", "late-exact", "early-good", "late-good", "timeout", "good-recovery", "exact-recovery" };
            for (int kind = 0; kind < names.Length; kind++)
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                core.EnterInfiniteRun(record);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(run.ActivePattern, "Reward captures need an active infinite trap.");
                if (kind >= 5)
                {
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                    core.HandleInput(run.Pattern.Channel == GreedLastRunChannel.Left
                        ? GreedLastRunChannel.Right : GreedLastRunChannel.Left, clock);
                    core.RequestNextPattern(clock += 2f);
                    Require(run.ActivePattern, "Healing capture needs a recovery trap after actual damage.");
                    if (kind == 6)
                    {
                        for (int warmup = 0; warmup < 4; warmup++)
                        {
                            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                            core.HandleInput(run.Pattern.Channel, clock);
                            core.RequestNextPattern(clock += 2f);
                        }
                    }
                }
                GreedLastRunSnapshot before = run;
                if (kind == 4) core.Tick(clock += run.Pattern.HitSeconds + 0.1f);
                else
                {
                    float goodDelta = (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                    float delta = kind == 6 ? 0f : kind == 0 ? -0.012f : kind == 1 ? 0.012f
                        : kind == 2 ? -goodDelta : goodDelta;
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + delta;
                    core.HandleInput(run.Pattern.Channel, clock);
                }
                GreedLastRunSnapshot after = run;
                Require(after.LastJudgementScoreGain == after.Score - before.Score
                    && after.LastJudgementHealthChange == after.Health - before.Health,
                    "Visual reward payload must match the core's actual changes.");
                if (kind >= 5) Require(after.LastJudgementHealthChange > 0, "Healing capture must show real recovery.");
                string name = names[kind];
                void Show()
                {
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ShowRun(before);
                    ui.RenderRun(after);
                    string text = Read<Text>("judgementFeedbackText").text;
                    ui.ToggleDevCleanView();
                    ui.ToggleDevCleanView();
                    Require(text == Read<Text>("judgementFeedbackText").text,
                        "A live F11 round trip must restore the exact original reward copy.");
                }
                Add("81-reward-" + name + "-small", 960, 540, Show, settleSeconds: 0.16);
                Add("81-reward-" + name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                Add("81-reward-" + name + "-wide", 2340, 1080, Show, 96f, 0.16);
                void ShowDetailed()
                {
                    Show();
                    ui.ToggleDevCleanView();
                }
                Add("81-reward-" + name + "-detail-small", 960, 540, ShowDetailed, settleSeconds: 0.16);
                Add("81-reward-" + name + "-detail-tablet", 1024, 768, ShowDetailed, settleSeconds: 0.16);
                Add("81-reward-" + name + "-detail-wide", 2340, 1080, ShowDetailed, 96f, 0.16);
            }
        }

        private static void AddRouteStepFeedbackFrames()
        {
            string[] routes = { "안정", "변주", "압박" };
            for (int route = 0; route < routes.Length; route++)
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.Enter();
                for (int cycle = 0; cycle <= route; cycle++)
                    core.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, clock += 0.1f);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                for (int step = 0; step < 2; step++)
                {
                    Require(run.ActivePattern && run.ActiveRouteProfile == routes[route] + " 루트",
                        "Route feedback captures need the actual authored route trap.");
                    GreedLastRunSnapshot before = run;
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                    GreedLastRunSnapshot after = run;
                    Require(after.LastResult == GreedLastJudgementResult.Success && !after.ChoiceActive,
                        "Route-step captures must stop before the final bundle choice.");
                    string name = "86-route-" + route + "-step-" + step;
                    // Frame callbacks run after this loop, so capture the authored values too.
                    string routeName = routes[route];
                    int stepNumber = step + 1;
                    void ShowCapturedDetailed()
                    {
                        if (ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(before);
                        ui.RenderRun(after);
                        string text = Read<Text>("judgementFeedbackText").text;
                        Require(text.Contains("경로 " + routeName + " " + stepNumber + "/3", StringComparison.Ordinal)
                            && text.Split('\n').Length <= 3,
                            "Detailed route feedback must retain its next action within three lines.");
                    }
                    Add(name + "-detail-small", 960, 540, ShowCapturedDetailed, settleSeconds: 0.16);
                    Add(name + "-detail-tablet", 1024, 768, ShowCapturedDetailed, settleSeconds: 0.16);
                    Add(name + "-detail-wide", 2340, 1080, ShowCapturedDetailed, 96f, 0.16);
                    core.RequestNextPattern(clock += 2f);
                }

                foreach (string outcome in new[] { "good", "wrong", "no-input" })
                {
                    var breakCore = new GreedLastRunCore();
                    GreedLastRunSnapshot broken = default;
                    breakCore.SnapshotChanged += value => broken = value;
                    float breakClock = Time.realtimeSinceStartup;
                    breakCore.Enter();
                    for (int cycle = 0; cycle <= route; cycle++)
                        breakCore.JumpToDevShortcut(GreedLastDevShortcut.RouteCycle, breakClock += 0.1f);
                    breakCore.Tick(breakClock += 2f);
                    breakCore.Tick(breakClock += 1f);
                    GreedLastRunSnapshot before = broken;
                    if (outcome == "no-input") breakCore.Tick(breakClock + broken.Pattern.HitSeconds + 0.02f);
                    else
                    {
                        float delta = outcome == "good"
                            ? (broken.Pattern.SuccessWindowSeconds + broken.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                        GreedLastRunChannel channel = outcome == "wrong"
                            ? (broken.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left)
                            : broken.Pattern.Channel;
                        breakCore.HandleInput(channel,
                            breakClock + broken.Pattern.CoreSeconds - breakCore.InputTimingOffsetSeconds + delta);
                    }
                    GreedLastRunSnapshot after = broken;
                    Require(after.LastResult == (outcome == "good" ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Miss)
                        && !after.PerfectBundleIntact && !after.Stopped,
                        "Route-break captures must retain a live Good or Miss without a clean finisher.");
                    string routeName = routes[route];
                    void ShowBrokenDetailed()
                    {
                        if (ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(before);
                        ui.RenderRun(after);
                        string text = Read<Text>("judgementFeedbackText").text;
                        Require(text.Contains("경로 " + routeName + " 중단", StringComparison.Ordinal)
                            && text.Split('\n').Length <= 3,
                            "Broken-route detail must retain its next practice action within three rows.");
                    }
                    string name = "86-route-" + route + "-break-" + outcome;
                    Add(name + "-detail-small", 960, 540, ShowBrokenDetailed, settleSeconds: 0.16);
                    Add(name + "-detail-tablet", 1024, 768, ShowBrokenDetailed, settleSeconds: 0.16);
                    Add(name + "-detail-wide", 2340, 1080, ShowBrokenDetailed, 96f, 0.16);
                }
            }
        }

        private static void AddActualInputChannelFrames()
        {
            foreach (GreedLastRunChannel required in new[] {
                GreedLastRunChannel.Left, GreedLastRunChannel.Center, GreedLastRunChannel.Right })
            {
                foreach (GreedLastRunChannel pressed in new[] {
                    GreedLastRunChannel.Left, GreedLastRunChannel.Center, GreedLastRunChannel.Right })
                {
                    if (pressed == required) continue;
                    var core = new GreedLastRunCore();
                    GreedLastRunSnapshot run = default;
                    core.SnapshotChanged += value => run = value;
                    float clock = Time.realtimeSinceStartup;
                    core.EnterInfiniteRun();
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                    for (int search = 0; search < 24 && run.Pattern.Channel != required; search++)
                    {
                        clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                        core.HandleInput(run.Pattern.Channel, clock);
                        core.RequestNextPattern(clock += 2f);
                    }
                    Require(run.ActivePattern && run.Pattern.Channel == required,
                        "Channel capture must reach the required lane through actual authored traps.");
                    GreedLastRunSnapshot before = run;
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds + 0.012f;
                    core.HandleInput(pressed, clock);
                    typeof(GreedLastRunCore).GetField("judgementText", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(core, "Presentation wording replaced");
                    PublishAt(core, clock);
                    GreedLastRunSnapshot after = run;
                    Require(after.MissReason == "wrong_channel" && after.LastJudgedInputChannel == pressed
                        && after.HasJudgedInputTiming, "Channel capture must retain the actual input payload.");
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(before);
                        ui.RenderRun(after);
                        Require(Read<GreedLastRunChannel>("runnerResponseChannel") == pressed
                            && Read<GreedLastJudgementResult[]>("laneJudgementTagResults")[(int)pressed - 1]
                                == GreedLastJudgementResult.Miss
                            && Read<GreedLastJudgementResult[]>("laneJudgementTagResults")[(int)required - 1]
                                == GreedLastJudgementResult.None,
                            "Rendered wrong-lane response must separate pressed and required lanes without parsing copy.");
                    }
                    string name = "82-input-" + required.ToString().ToLowerInvariant()
                        + "-pressed-" + pressed.ToString().ToLowerInvariant();
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                    void ShowDetailed()
                    {
                        Show();
                        ui.ToggleDevCleanView();
                        string actualInput = pressed == GreedLastRunChannel.Left ? "좌 입력"
                            : pressed == GreedLastRunChannel.Center ? "중 입력" : "우 입력";
                        Require(Read<Text>("judgementFeedbackText").text.Contains(actualInput, StringComparison.Ordinal),
                            "Detailed wrong-lane capture must name the actual input independently of display wording.");
                    }
                    Add(name + "-detail-small", 960, 540, ShowDetailed, settleSeconds: 0.16);
                    Add(name + "-detail-tablet", 1024, 768, ShowDetailed, settleSeconds: 0.16);
                    Add(name + "-detail-wide", 2340, 1080, ShowDetailed, 96f, 0.16);
                }
            }
        }

        private static void AddStructuredTimingFrames()
        {
            foreach (string mode in new[] { "practice", "normal", "infinite" })
            foreach (float direction in new[] { -1f, 1f })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                if (mode == "practice") core.RestartPractice(clock);
                else if (mode == "infinite") core.EnterInfiniteRun();
                else core.Enter();
                for (int step = 0; step < 32 && !run.ActivePattern; step++)
                {
                    if (run.ChoiceActive) core.HandleInput(GreedLastRunChannel.Left, clock);
                    else core.Tick(clock += 2f);
                }
                Require(run.ActivePattern, "Structured timing capture needs an authored live trap: " + mode);
                GreedLastRunSnapshot before = run;
                float delta = direction * (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds + delta;
                core.HandleInput(run.Pattern.Channel, clock);
                typeof(GreedLastRunCore).GetField("judgementText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(core, "Changed Good wording\n" + (direction < 0f ? "늦음 999ms" : "빠름 999ms"));
                PublishAt(core, clock);
                GreedLastRunSnapshot after = run;
                Require(after.LastResult == GreedLastJudgementResult.Good && after.HasJudgedInputTiming,
                    "Structured timing capture must use a real signed Good input: " + mode);
                string expectedDirection = direction < 0f ? "빠름 " : "늦음 ";
                string expectedHint = direction < 0f ? "다음은 살짝 늦게" : "다음은 살짝 빠르게";
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(before);
                        ui.RenderRun(after);
                        int count = Read<int>("judgementHistoryCount");
                        float until = Read<float>("judgementFeedbackUntil");
                        float popUntil = Read<float>("judgementFeedbackPopUntil");
                        if (detailed) ui.ToggleDevCleanView();
                        string text = Read<Text>("judgementFeedbackText").text;
                        Require(text.Contains(expectedDirection, StringComparison.Ordinal)
                            && !text.Contains("999ms", StringComparison.Ordinal)
                            && (!detailed || text.Contains(expectedHint, StringComparison.Ordinal)),
                            "Basic/detail timing must follow the actual input, not contradictory copy: " + mode);
                        Require(Read<int>("judgementHistoryCount") == count
                            && Read<float>("judgementFeedbackUntil") == until
                            && Read<float>("judgementFeedbackPopUntil") == popUntil,
                            "Detailed timing capture must not replay or extend its judgement.");
                    }
                    string name = "84-timing-" + mode + (direction < 0f ? "-early" : "-late")
                        + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddLongRunCounterFrames()
        {
            var frames = GreedLastPrototypeTimelineCheck.VerifyLongInfiniteRun();
            void Show()
            {
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                ShowRun(frames.before);
                ui.RenderRun(frames.after);
            }
            Add("83-long-infinite-small", 960, 540, Show, settleSeconds: 0.16);
            Add("83-long-infinite-tablet", 1024, 768, Show, settleSeconds: 0.16);
            Add("83-long-infinite-wide", 2340, 1080, Show, 96f, 0.16);
        }

        private static void AddPracticeMissFrames()
        {
            foreach (int kind in new[] { -1, 0, 1 })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.RestartPractice(clock);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(run.ActivePattern, "Practice Miss captures need an authored live trap.");
                GreedLastRunSnapshot before = run;
                if (kind == 0) core.Tick(clock += run.Pattern.HitSeconds - run.ElapsedSeconds + 0.1f);
                else
                {
                    float delta = kind * (run.Pattern.GoodWindowSeconds + 0.04f);
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds + delta;
                    core.HandleInput(run.Pattern.Channel, clock);
                }
                typeof(GreedLastRunCore).GetField("judgementText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(core, "Changed Miss wording\n늦음 999ms");
                PublishAt(core, clock);
                GreedLastRunSnapshot after = run;
                Require(after.LastResult == GreedLastJudgementResult.Miss && after.HasJudgedInputTiming == (kind != 0),
                    "Practice Miss captures must distinguish real taps from timeouts.");
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(before);
                        ui.RenderRun(after);
                        if (detailed) ui.ToggleDevCleanView();
                        string text = Read<Text>("judgementFeedbackText").text;
                        Require(!text.Contains("999ms", StringComparison.Ordinal)
                            && (kind != 0 || text.Contains("입력 없음", StringComparison.Ordinal)),
                            "Practice timeout must be explicit and must not invent a tap's timing.");
                        if (detailed && kind != 0)
                            Require(text.Contains(kind < 0 ? "빠름 " : "늦음 ", StringComparison.Ordinal),
                                "Detailed practice Miss must preserve the actual timing direction.");
                    }
                    string name = "85-practice-miss-" + (kind < 0 ? "early" : kind > 0 ? "late" : "no-input")
                        + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddCalibratedPauseFrames()
        {
            string key = GreedLastRunCore.InputTimingOffsetPrefsKey;
            bool hadOffset = PlayerPrefs.HasKey(key);
            float storedOffset = PlayerPrefs.GetFloat(key);
            try
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.RestartPractice(clock);
                core.Tick(clock += 3f);
                core.TogglePause(clock += 0.1f);
                foreach (int milliseconds in new[] { 20, -20, 200, -200 })
                {
                    core.AdjustInputTimingOffset(milliseconds / 1000f - core.InputTimingOffsetSeconds);
                    Require(run.Paused && Mathf.RoundToInt(run.InputTimingOffsetSeconds * 1000f) == milliseconds,
                        "Calibration captures need the actual paused core's positive and negative offset limits.");
                    GreedLastRunSnapshot paused = run;
                    string suffix = milliseconds < 0 ? "minus" + -milliseconds : "plus" + milliseconds;
                    Add("80-pause-calibration-" + suffix, 960, 540, () => ShowRun(paused));
                }
            }
            finally
            {
                if (hadOffset) PlayerPrefs.SetFloat(key, storedOffset);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static void PublishAt(GreedLastRunCore core, float time)
        {
            // The fixture advances a synthetic clock; publish using that same clock, not wall time.
            typeof(GreedLastRunCore).GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(float) }, null).Invoke(core, new object[] { time });
        }

        private static void AddSavedRunLoopFrames()
        {
            var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            var stateMachine = new GreedLastStateMachine(new GreedLastRequestGate());
            GreedLastStateSnapshot screen = default;
            stateMachine.StateChanged += value => screen = value;
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.Enter();
            // Drive the real run core to completion without changing stored timing or profile data.
            for (int step = 0; step < 100 && !run.Stopped; step++)
            {
                if (run.ChoiceActive)
                {
                    core.HandleInput(run.ChoiceKind == GreedLastChoiceKind.Node
                        ? GreedLastRunChannel.Left : GreedLastRunChannel.Center, clock += 0.2f);
                }
                else if (run.StartCountdownActive)
                {
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                }
                else if (!run.ActivePattern)
                {
                    core.RequestNextPattern(clock += 2f);
                }
                else
                {
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                    Require(run.LastResult == GreedLastJudgementResult.Success, "The visual loop must resolve exact inputs.");
                }
            }
            GreedLastRunRecord record = core.CreateRunRecord();
            Require(run.EscapeSucceeded && record.IsValid, "The visual loop must finish a real normal run.");
            GreedLastRunSnapshot clear = run;
            Add("09-escape-result", 1280, 720, () => ShowRun(clear));
            AddRetainedViewportFrames("63-clear", () => ShowRun(clear));
            backend.RegisterClear(record);
            stateMachine.SetSaveLoadoutDraft(backend.LoadLobby(),
                backend.BuildRunClearSaveDraftNoticeMessage(record, false) + "\n\n" + backend.BuildSaveLoadoutClearSlotChoiceText(),
                saveSlotChoiceRequired: true, saveLoadoutCandidateAvailable: true);
            AddScreen("10-clear-slot-choice", screen);
            GreedLastStateSnapshot candidate = screen;
            AddRetainedViewportFrames("64-candidate", () => ui.Render(candidate));

            backend.SelectSaveLoadoutSlot(2);
            stateMachine.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSaveLoadoutDraftText(),
                saveLoadoutCandidateAvailable: true);
            AddScreen("11-right-slot-selected", screen);
            GreedLastLobbySnapshot saved = backend.ConfirmSaveLoadout();
            Require(!backend.HasSaveLoadoutCandidate && saved.SaveSlotOccupied[2]
                && !saved.SaveSlotOccupied[0] && !saved.SaveSlotOccupied[1], "Only the explicitly selected right slot may receive the candidate.");
            stateMachine.SetLobbyReady(saved);
            stateMachine.ShowLocalNotice(backend.BuildSaveConfirmMessage());
            AddScreen("12-save-complete", screen);
            backend.ConfirmSaveLoadout();
            Require(!backend.HasSaveLoadoutCandidate, "A second confirmation must not recreate a consumed candidate.");
            for (int step = 0; step < 3; step++)
            {
                stateMachine.SetInfiniteStartReady(backend.LoadLobby(),
                    GreedLastApp.BuildInfinitePreparationDetailForStep(step, "집중 축", "황금 박동").TrimStart()
                    + "\n\n" + backend.BuildInfiniteStartReadyText());
                AddScreen("13-infinite-ready-step-" + step, screen);
                if (step == 2)
                {
                    AddScreen("13-infinite-ready-small", screen, 960, 540);
                    AddScreen("13-infinite-ready-tablet", screen, 1024, 768);
                }
            }
            Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord loadout, out _, out _), "The first entry must consume one use.");
            core.EnterInfiniteRun(loadout);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            for (int pattern = 0; pattern < 3; pattern++)
            {
                if (pattern > 0) core.RequestNextPattern(clock += 2f);
                Require(run.ActivePattern, "The infinite visual loop must contain three live traps.");
                clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                core.HandleInput(run.Pattern.Channel, clock);
                Require(run.LastResult == GreedLastJudgementResult.Success, "The saved loadout must remain playable.");
            }
            core.ForceStopInfiniteRunForTest();
            GreedLastRunSnapshot infiniteResult = run;
            Require(backend.RegisterInfiniteRun(core.CreateInfiniteRunRecord()), "The visual loop must register a real infinite record.");
            Add("14-infinite-result", 1280, 720, () => ShowRun(infiniteResult));
            AddRetainedViewportFrames("74-infinite-result", () => ShowRun(infiniteResult));
            stateMachine.SetInfiniteStartReady(backend.LoadLobby(),
                GreedLastApp.BuildInfinitePreparationDetailForStep(2, "집중 축", "황금 박동").TrimStart()
                + "\n\n" + backend.BuildInfiniteStartReadyText());
            AddScreen("15-final-use-ready", screen);
            Require(backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord finalLoadout, out _, out _)
                && finalLoadout.IsValid, "The second entry must retain a playable loadout after clearing its slot.");
            GreedLastLobbySnapshot empty = backend.LoadLobby();
            Require(!empty.SaveSlotOccupied[2] && !empty.SaveSlotUsable[2]
                && !backend.TryConsumeSelectedSaveLoadout(out _, out _, out _), "The exhausted slot must be empty and reject a third entry.");
            stateMachine.SetInfiniteLoadoutSelect(empty, backend.BuildInfiniteLoadoutSelectText());
            AddScreen("16-exhausted-slot", screen);
            for (int page = 0; page < 4; page++)
            {
                stateMachine.SetInfiniteRecordBoard(backend.LoadLobby(), backend.BuildRecordBoardText(page), page);
                AddScreen("17-record-page-" + page, screen);
                if (page == 2)
                {
                    GreedLastStateSnapshot recordScreen = screen;
                    AddRetainedViewportFrames("65-record", () => ui.Render(recordScreen));
                }
            }

            // A new clear can replace an occupied slot only through the existing confirmation view.
            backend.RegisterClear(record);
            backend.SelectSaveLoadoutSlot(1);
            backend.ConfirmSaveLoadout();
            backend.RegisterClear(record);
            backend.SelectSaveLoadoutSlot(1);
            stateMachine.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSelectedSaveLoadoutOverwritePreviewText(),
                saveSlotOverwriteConfirmationPending: true, saveLoadoutCandidateAvailable: true);
            AddScreen("18-overwrite-confirm", screen);
            AddScreen("19-overwrite-wide-safe", screen, 2340, 1080, 96f);
            AddScreen("19-overwrite-tablet", screen, 1024, 768);
            GreedLastStateSnapshot overwrite = screen;
            AddRetainedViewportFrames("66-overwrite", () => ui.Render(overwrite));
            Add("20-overwrite-drag", 1280, 720, () =>
            {
                ui.Render(overwrite);
                DragMenuDetail();
            });
            Add("20-overwrite-scrolled", 1280, 720, () =>
            {
                ui.Render(overwrite);
                Read<ScrollRect>("menuDetailScroll").verticalNormalizedPosition = 0f;
            });
            stateMachine.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSelectedSaveLoadoutDeletePreviewText(),
                saveSlotDeleteConfirmationPending: true, saveLoadoutCandidateAvailable: true);
            AddScreen("20-delete-confirm-small", screen, 960, 540);
            stateMachine.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSelectedSaveLoadoutRenamePreviewText(),
                saveSlotRenameConfirmationPending: true, saveLoadoutCandidateAvailable: true);
            AddScreen("20-rename-confirm-tablet", screen, 1024, 768);
            stateMachine.SetInfiniteLoadoutSelect(backend.LoadSelectedSaveSlotView(), backend.BuildSelectedSaveLoadoutDeletePreviewText(),
                saveSlotDeleteConfirmationPending: true);
            AddScreen("20-infinite-delete-tablet", screen, 1024, 768);
            stateMachine.SetInfiniteLoadoutSelect(backend.LoadSelectedSaveSlotView(), backend.BuildSelectedSaveLoadoutRenamePreviewText(),
                saveSlotRenameConfirmationPending: true);
            AddScreen("20-infinite-rename-small", screen, 960, 540);
            core.RestartPractice(Time.realtimeSinceStartup - 4f);
            core.Tick(Time.realtimeSinceStartup - 1f);
            core.Tick(Time.realtimeSinceStartup);
            GreedLastRunSnapshot restoredLive = run;
            Add("21-run-after-menus", 1280, 720, () => ShowRun(restoredLive));
            Add("22-run-tablet", 1024, 768, () => ShowRun(restoredLive));
            core.TogglePause(Time.realtimeSinceStartup + 0.1f);
            GreedLastRunSnapshot tabletPause = run;
            Add("23-pause-tablet", 1024, 768, () => ShowRun(tabletPause));
            backend.DiscardSaveLoadoutCandidate();
            stateMachine.SetLobbyReady(backend.LoadLobby());
            stateMachine.ShowLocalNotice(backend.BuildRunClearSkipSaveMessage(record));
            AddScreen("24-skip-save-lobby", screen);
        }

        private static void AddScreen(string name, GreedLastStateSnapshot snapshot, int width = 1280, int height = 720, float inset = 0f)
        {
            Add(name, width, height, () => ui.Render(snapshot), inset);
        }

        private static void DragMenuDetail()
        {
            ScrollRect scroll = Read<ScrollRect>("menuDetailScroll");
            Canvas.ForceUpdateCanvases();
            Vector3 start = camera.WorldToScreenPoint(scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            var pointer = new PointerEventData(eventSystem) { position = start, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(scroll.transform),
                "Comparison dragging must start on the scroll viewport, not an underlying slot.");
            pointer.pointerPressRaycast = hits[0];
            scroll.OnInitializePotentialDrag(pointer);
            scroll.OnBeginDrag(pointer);
            pointer.position += Vector2.up * 100f;
            scroll.OnDrag(pointer);
            scroll.OnEndDrag(pointer);
            scroll.StopMovement();
            Require(scroll.content.anchoredPosition.y > 1f,
                "Pointer dragging must move long comparison text without using the scrollbar.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void BeginNextFrame()
        {
            currentFrame = Frames.Dequeue();
            ReleaseTarget();
            target = new RenderTexture(currentFrame.Width, currentFrame.Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.scaleFactor = currentFrame.Height / 1920f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f * currentFrame.Width / currentFrame.Height, 1920f);
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * (10f / 1920f);
            currentFrame.Show();
            ApplyCaptureSafeArea();
            Canvas.ForceUpdateCanvases();
            frameStartedAt = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            try
            {
                currentFrame.BeforeUpdate?.Invoke();
                typeof(GreedLastRuntimeUi).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                if (EditorApplication.timeSinceStartup - frameStartedAt < currentFrame.SettleSeconds)
                {
                    return;
                }
                CaptureFrame();
                if (Frames.Count > 0)
                {
                    BeginNextFrame();
                    return;
                }
                Debug.Log("Greed Last visual checks captured: " + capturedFrameCount + " nonblank frames in Logs/VisualChecks. These are isolated UI fixtures, not real-device input tests.");
                WriteCaptureReport(true, string.Empty);
                Cleanup();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                WriteCaptureReport(false, exception.Message);
                Cleanup();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void ApplyCaptureSafeArea()
        {
            Vector4 inset = currentFrame.SafeInsets; // Left, bottom, right, top in output pixels.
            Require(inset.x >= 0f && inset.y >= 0f && inset.z >= 0f && inset.w >= 0f
                && inset.x + inset.z < currentFrame.Width && inset.y + inset.w < currentFrame.Height,
                "Capture safe area must leave a positive visible region.");
            // Exercise the production normalization/cache path, not a separate capture-only formula.
            typeof(GreedLastRuntimeUi).GetMethod("ApplySafeAreaSample", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, new object[] { new Rect(inset.x, inset.y, currentFrame.Width - inset.x - inset.z,
                    currentFrame.Height - inset.y - inset.w), currentFrame.Width, currentFrame.Height, false });
        }

        private static void CaptureFrame()
        {
            RectTransform root = Read<RectTransform>("rootPanelRect");
            ApplyCaptureSafeArea();
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            camera.Render();
            int checkedButtons = VerifyVisibleControlRaycasts(out int checkedControlPoints);
            int checkedBlockedLanePoints = VerifyPausedLaneRaycasts();
            int checkedFeedbackAreas = VerifyVisibleFeedbackAreas();
            int checkedRunnerCueClearances = VerifyRunnerCueClearance();
            int checkedLaneLabels = VerifyLanePresentation();
            int checkedModuleClicks = VerifyActionModuleClick();
            int checkedModuleLaneInputs = VerifyLaneModuleInputs();
            int checkedModuleScrolls = VerifyMenuModuleScroll();
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(currentFrame.Width, currentFrame.Height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int brightSamples = 0;
                int detailSamples = 0;
                Color32 first = pixels[0];
                for (int i = 0; i < pixels.Length; i += 97)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.r + pixel.g + pixel.b > 150) brightSamples++;
                    if (Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 35) detailSamples++;
                }
                // The authored canvas is height-scaled; sparse record screens contain fewer pixels at 540p.
                float sampleAreaScale = Mathf.Pow(target.height / 720f, 2f);
                int minimumBrightSamples = Mathf.Max(10, Mathf.CeilToInt(30f * sampleAreaScale));
                int minimumDetailSamples = Mathf.Max(30, Mathf.CeilToInt(100f * sampleAreaScale));
                if (brightSamples < minimumBrightSamples || detailSamples < minimumDetailSamples)
                {
                    File.WriteAllBytes("Logs/VisualChecks/" + currentFrame.Name + ".failed.png", image.EncodeToPNG());
                    throw new InvalidOperationException("Blank or incomplete visual capture: " + currentFrame.Name
                        + " / bright " + brightSamples + " / detail " + detailSamples
                        + " / canvas " + canvas.GetComponent<RectTransform>().rect
                        + " / root " + root.rect
                        + " / title vertices " + Read<Text>("titleText").cachedTextGenerator.vertexCount
                        + " / title culled " + Read<Text>("titleText").canvasRenderer.cull
                        + " / camera rect " + camera.pixelRect);
                }
                string path = "Logs/VisualChecks/" + currentFrame.Name + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                capturedFrameCount++;
                report.frames.Add(new CaptureResult
                {
                    file = currentFrame.Name + ".png",
                    width = target.width,
                    height = target.height,
                    safeInset = currentFrame.SafeInset,
                    safeInsets = currentFrame.SafeInsets,
                    checkedButtons = checkedButtons,
                    checkedModuleClicks = checkedModuleClicks,
                    checkedModuleLaneInputs = checkedModuleLaneInputs,
                    checkedModuleScrolls = checkedModuleScrolls,
                    checkedControlPoints = checkedControlPoints,
                    checkedBlockedLanePoints = checkedBlockedLanePoints,
                    checkedLaneLabels = checkedLaneLabels,
                    checkedFeedbackAreas = checkedFeedbackAreas,
                    checkedRunnerCueClearances = checkedRunnerCueClearances,
                    retainedScreen = currentFrame.RetainedScreen,
                    brightSamples = brightSamples,
                    detailSamples = detailSamples,
                });
                Debug.Log("Visual frame: " + path + " / " + target.width + "x" + target.height);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static int VerifyVisibleControlRaycasts(out int checkedPoints)
        {
            checkedPoints = 0;
            bool paused = Read<GameObject>("pauseMenuOverlay").activeInHierarchy;
            bool menu = Read<ScrollRect>("menuDetailScroll").gameObject.activeInHierarchy;
            bool result = Read<Text>("detailText").enabled && Read<Text>("runHudText").gameObject.activeSelf == false
                && Read<Button>("nextPatternButton").gameObject.activeInHierarchy;
            bool choice = Read<bool>("runModeActive") && Read<bool>("hasRunSnapshot")
                && Read<GreedLastRunSnapshot>("latestRunSnapshot").ChoiceActive;
            bool live = Read<bool>("runModeActive") && Read<bool>("hasRunSnapshot") && !paused
                && !Read<GreedLastRunSnapshot>("latestRunSnapshot").Stopped;
            bool lobby = Read<Button>("practiceRunButton").gameObject.activeInHierarchy;
            if (!paused && !menu && !result && !choice && !live && !lobby) return 0;
            Require(eventSystem != null, "Visible-control checks require the fixture's event system.");
            var controls = new List<Button>
            {
                Read<Button>("nextPatternButton"), Read<Button>("returnLobbyButton"),
                Read<Button>("pauseButton"),
            };
            if (paused)
            {
                controls.Add(Read<Button>("hapticsButton"));
                controls.Add(Read<Button>("rotationTiltButton"));
                controls.Add(Read<Button>("motionComfortButton"));
                controls.AddRange(Read<Button[]>("sfxVolumeButtons"));
                controls.AddRange(Read<Button[]>("timingOffsetButtons"));
            }
            else
            {
                if (result) controls.Add(Read<Button[]>("timingOffsetButtons")[1]);
                controls.AddRange(Read<Button[]>("laneButtons"));
                if (live) controls.AddRange(Read<Button[]>("laneHitButtons"));
                controls.Add(Read<Button>("saveSlotDetailButton"));
                controls.Add(Read<Button>("saveSlotRenameButton"));
                controls.Add(Read<Button>("saveSlotDeleteButton"));
                controls.Add(Read<Button>("saveLoadoutButton"));
                controls.Add(Read<Button>("normalRunButton"));
                controls.Add(Read<Button>("infiniteButton"));
                controls.Add(Read<Button>("practiceRunButton"));
            }
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            int checkedButtons = 0;
            foreach (Button button in controls)
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable()) continue;
                RectTransform rect = button.GetComponent<RectTransform>();
                VerifyControlSafeBounds(rect);
                Vector2[] samples = { new Vector2(0.5f, 0.5f), new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.06f),
                    new Vector2(0.06f, 0.94f), new Vector2(0.94f, 0.94f) };
                foreach (Vector2 sample in samples)
                {
                    Vector2 localPoint = rect.rect.min + Vector2.Scale(rect.rect.size, sample);
                    Vector3 screenPoint = camera.WorldToScreenPoint(rect.TransformPoint(localPoint));
                    var pointer = new PointerEventData(eventSystem) { position = screenPoint };
                    var hits = new List<RaycastResult>();
                    raycaster.Raycast(pointer, hits);
                    Require(hits.Count > 0 && IsExpectedControlHit(button, hits[0].gameObject),
                        currentFrame.Name + ": visible button is blocked at " + sample + ": " + button.name
                        + " / first hit " + (hits.Count == 0 ? "none" : hits[0].gameObject.name)
                        + " / button " + (hits.Count == 0 ? "none" : hits[0].gameObject.GetComponentInParent<Button>()?.name));
                    checkedPoints++;
                }
                VerifyButtonTextBounds(button);
                checkedButtons++;
            }
            return checkedButtons;
        }

        private static void VerifyControlSafeBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector4 safe = currentFrame.SafeInsets;
            foreach (Vector3 corner in corners)
            {
                Vector3 point = camera.WorldToScreenPoint(corner);
                Require(point.x >= safe.x - 1f && point.x <= currentFrame.Width - safe.z + 1f
                    && point.y >= safe.y - 1f && point.y <= currentFrame.Height - safe.w + 1f,
                    currentFrame.Name + ": control extends outside the safe area: " + rect.name + " at " + point);
            }
        }

        private static int VerifyActionModuleClick()
        {
            int count = 0;
            foreach (GreedLastCommandButton button in Read<GameObject>("rootPanel").GetComponentsInChildren<GreedLastCommandButton>())
            {
                if (!button.gameObject.activeInHierarchy || !button.IsInteractable()) continue;
                Vector2 point = camera.WorldToScreenPoint(button.transform.position);
                Button.ButtonClickedEvent originalClick = button.onClick;
                try
                {
                    // Settings persist preferences directly. Probe their real input surface without changing user settings.
                    button.onClick = new Button.ButtonClickedEvent();
                    Action redraw = null;
                    if (currentFrame.Width == Screen.width && currentFrame.Height == Screen.height
                        && currentFrame.SafeInsets == Vector4.zero && Read<bool>("runModeActive") && Read<bool>("hasRunSnapshot"))
                    {
                        GreedLastRunSnapshot run = Read<GreedLastRunSnapshot>("latestRunSnapshot");
                        redraw = () =>
                        {
                            ui.RenderRun(run);
                            Canvas.ForceUpdateCanvases();
                            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
                            {
                                graphic.SetAllDirty();
                                graphic.Rebuild(CanvasUpdate.PreRender);
                            }
                            camera.Render();
                        };
                    }
                    GreedLastPrototypeKeyboardCheck.VerifyActionClick(ui, eventSystem, button, point, redraw);
                }
                finally
                {
                    button.onClick = originalClick;
                }
                count++;
            }
            return count;
        }

        private static int VerifyLaneModuleInputs()
        {
            if (!Read<bool>("runModeActive") || !Read<bool>("hasRunSnapshot")
                || Read<GameObject>("pauseMenuOverlay").activeInHierarchy) return 0;
            GreedLastRunSnapshot run = Read<GreedLastRunSnapshot>("latestRunSnapshot");
            if (run.Stopped) return 0;
            if (!run.Paused && !run.ChoiceActive && !run.CountdownActive)
            {
                bool clean = ui.UsePlayerRunControls;
                Require(Read<Button>("nextPatternButton").GetComponentInChildren<Text>().text
                    == (clean ? (run.AutoFlow ? "일시정지" : "함정 재개") : (run.AutoFlow ? "자동 정지" : "자동 시작"))
                    && Read<Button>("returnLobbyButton").gameObject.activeInHierarchy == !clean,
                    currentFrame.Name + ": gameplay controls must match the active player or development view.");
            }
            Button[] lanes = Read<Button[]>("laneButtons");
            Button[] margins = Read<Button[]>("laneHitButtons");
            int count = 0;
            for (int i = 0; i < lanes.Length; i++)
            {
                Button lane = lanes[i];
                if (!lane.gameObject.activeInHierarchy || !lane.IsInteractable()) continue;
                GreedLastPrototypeKeyboardCheck.VerifyLaneInput(ui, eventSystem, lane,
                    camera.WorldToScreenPoint(lane.transform.position), i);
                count++;
                Button margin = margins[i];
                if (run.ChoiceActive || !margin.gameObject.activeInHierarchy || !margin.IsInteractable()) continue;
                RectTransform rect = margin.GetComponent<RectTransform>();
                foreach (float edge in new[] { 0.06f, 0.94f })
                {
                    Vector2 local = rect.rect.min + rect.rect.size * edge;
                    GreedLastPrototypeKeyboardCheck.VerifyLaneInput(ui, eventSystem, margin,
                        camera.WorldToScreenPoint(rect.TransformPoint(local)), i);
                    count++;
                }
            }
            return count;
        }

        private static int VerifyMenuModuleScroll()
        {
            ScrollRect scroll = Read<ScrollRect>("menuDetailScroll");
            if (!scroll.gameObject.activeInHierarchy || !scroll.vertical || scroll.content == null) return 0;
            RectTransform rect = scroll.viewport;
            Vector2 start = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            Vector2 top = camera.WorldToScreenPoint(rect.TransformPoint(new Vector2(rect.rect.center.x, rect.rect.yMax)));
            GreedLastPrototypeKeyboardCheck.VerifyScrollInput(ui, eventSystem, scroll, start,
                Mathf.Min(80f, Mathf.Abs(top.y - start.y) * 0.75f));
            return 1;
        }

        private static bool IsLaneControl(Button button)
        {
            return Array.IndexOf(Read<Button[]>("laneButtons"), button) >= 0
                || Array.IndexOf(Read<Button[]>("laneHitButtons"), button) >= 0;
        }

        private static bool IsExpectedControlHit(Button button, GameObject hit)
        {
            if (hit == button.gameObject || hit.transform.IsChildOf(button.transform)) return true;
            // The transparent touch margin and visible lane share one channel and one pointer owner set.
            int index = Array.IndexOf(Read<Button[]>("laneHitButtons"), button);
            if (index < 0) return false;
            Button lane = Read<Button[]>("laneButtons")[index];
            return hit == lane.gameObject || hit.transform.IsChildOf(lane.transform);
        }

        private static int VerifyPausedLaneRaycasts()
        {
            GameObject overlay = Read<GameObject>("pauseMenuOverlay");
            if (!overlay.activeInHierarchy) return 0;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            int count = 0;
            foreach (Button lane in Read<Button[]>("laneHitButtons"))
            {
                RectTransform rect = lane.GetComponent<RectTransform>();
                foreach (float x in new[] { 0.06f, 0.5f, 0.94f })
                {
                    Vector2 local = rect.rect.min + Vector2.Scale(rect.rect.size, new Vector2(x, 0.5f));
                    var pointer = new PointerEventData(eventSystem) { position = camera.WorldToScreenPoint(rect.TransformPoint(local)) };
                    var hits = new List<RaycastResult>();
                    raycaster.Raycast(pointer, hits);
                    Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(overlay.transform),
                        currentFrame.Name + ": pause or countdown input leaks through to a lane: " + lane.name);
                    count++;
                }
            }
            return count;
        }

        private static int VerifyLanePresentation()
        {
            if (!Read<bool>("runModeActive") || !Read<bool>("hasRunSnapshot")
                || Read<GameObject>("pauseMenuOverlay").activeInHierarchy) return 0;
            GreedLastRunSnapshot run = Read<GreedLastRunSnapshot>("latestRunSnapshot");
            if (run.Stopped) return 0;
            if (run.ChoiceActive)
                Require(!Read<GameObject>("runnerAvatarRoot").activeInHierarchy,
                    currentFrame.Name + ": the runner covers the choice surface.");
            bool liveHeader = !run.ChoiceActive && !run.CountdownActive;
            bool cleanHeader = ui.UsePlayerRunControls;
            Text title = Read<Text>("titleText");
            RectTransform root = Read<RectTransform>("rootPanelRect");
            if (liveHeader)
            {
                VerifyTextBounds(title);
                VerifyControlSafeBounds(title.rectTransform);
                Require(title.preferredHeight <= title.rectTransform.rect.height + 1f,
                    currentFrame.Name + ": live title needs enough height for all lines.");
                Text progress = Read<Text>("runProgressText");
                VerifyTextBounds(progress);
                Require(progress.fontSize >= (cleanHeader ? 32 : 20)
                    && progress.preferredHeight <= progress.rectTransform.rect.height + 1f,
                    currentFrame.Name + ": stage/count/threat must fit its player or diagnostic surface.");
                Bounds progressBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, progress.rectTransform);
                Bounds progressBar = RectTransformUtility.CalculateRelativeRectTransformBounds(root,
                    Read<Image>("runProgressFill").transform.parent);
                Require(progressBounds.min.y >= progressBar.max.y + 4f,
                    currentFrame.Name + ": progress text overlaps its fill bar.");
            }
            int count = 0;
            foreach (Text label in Read<Text[]>("laneLabelTexts"))
            {
                VerifyTextBounds(label);
                Require(label.preferredHeight <= label.rectTransform.rect.height + 1f,
                    currentFrame.Name + ": lane text is clipped: " + label.text);
                var corners = new Vector3[4];
                label.rectTransform.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 screen = camera.WorldToScreenPoint(corner);
                    Require(screen.x >= 0 && screen.x <= target.width && screen.y >= 0 && screen.y <= target.height,
                        currentFrame.Name + ": lane label lies outside the viewport.");
                }
                Require(run.ChoiceActive || (cleanHeader ? label.rectTransform.anchorMin.y > 1f
                        : label.rectTransform.anchorMin == Vector2.zero && label.rectTransform.anchorMax == Vector2.one),
                    currentFrame.Name + ": input labels must use the correct player header or diagnostic lane placement.");
                if (liveHeader && cleanHeader)
                {
                    Bounds titleBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, title.rectTransform);
                    Bounds labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, label.rectTransform);
                    bool sharesWidth = titleBounds.max.x > labelBounds.min.x && titleBounds.min.x < labelBounds.max.x;
                    Require(!sharesWidth || titleBounds.min.y >= labelBounds.max.y + 8f,
                        currentFrame.Name + ": live title overlaps the lane input header: " + label.text);
                    Bounds progressBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root,
                        Read<Text>("runProgressText").rectTransform);
                    bool sharesProgressWidth = progressBounds.max.x > labelBounds.min.x
                        && progressBounds.min.x < labelBounds.max.x;
                    Require(!sharesProgressWidth || labelBounds.min.y >= progressBounds.max.y + 4f,
                        currentFrame.Name + ": input header overlaps compact progress: " + label.text);
                }
                count++;
            }
            return count;
        }

        private static int VerifyVisibleFeedbackAreas()
        {
            if (Read<GameObject>("pauseMenuOverlay").activeInHierarchy)
            {
                Text title = Read<Text>("pauseMenuTitleText");
                Text body = Read<Text>("pauseMenuBodyText");
                foreach (Text label in new[] { title, body })
                {
                    VerifyTextBounds(label);
                    VerifyControlSafeBounds(label.rectTransform);
                    Require(label.preferredHeight <= label.rectTransform.rect.height + 1f,
                        currentFrame.Name + ": pause text must not clip its calibration or countdown value.");
                }
                Transform panel = Read<Image>("pauseMenuPanel").transform;
                Bounds bodyArea = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, body.rectTransform);
                Bounds titleArea = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, title.rectTransform);
                Bounds resumeArea = RectTransformUtility.CalculateRelativeRectTransformBounds(panel,
                    Read<Button>("nextPatternButton").transform);
                Require(!bodyArea.Intersects(titleArea) && !bodyArea.Intersects(resumeArea),
                    currentFrame.Name + ": pause summary must stay between its heading and resume command.");
                return 2;
            }
            RectTransform root = Read<RectTransform>("rootPanelRect");
            GameObject gauges = Read<GameObject>("runGaugeRoot");
            int count = 0;
            foreach (string field in new[] { "activeActionCueText", "judgementFeedbackText", "timingGuideText" })
            {
                Text label = Read<Text>(field);
                if (!label.gameObject.activeInHierarchy || !label.enabled || label.color.a < 0.01f
                    || string.IsNullOrWhiteSpace(label.text)) continue;
                VerifyTextBounds(label);
                Require(label.preferredHeight <= label.rectTransform.rect.height + 1f,
                    currentFrame.Name + ": feedback text is vertically clipped: " + label.name);
                if (gauges.activeInHierarchy)
                {
                    Bounds textBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, label.rectTransform);
                    Bounds gaugeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, gauges.transform);
                    Require(!textBounds.Intersects(gaugeBounds),
                        currentFrame.Name + ": feedback overlaps the health/focus gauges: " + label.name);
                }
                count++;
            }
            return count;
        }

        private static int VerifyRunnerCueClearance()
        {
            if (Read<GameObject>("pauseMenuOverlay").activeInHierarchy) return 0;
            Text guide = Read<Text>("timingGuideText");
            if (!guide.gameObject.activeInHierarchy || !guide.enabled || guide.color.a < 0.01f
                || string.IsNullOrWhiteSpace(guide.text)) return 0;
            Transform parent = guide.transform.parent;
            Bounds text = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, guide.rectTransform);
            var cues = new List<Graphic> { Read<Image>("runnerPreparationCue"), Read<Text>("runnerPreparationSymbolText") };
            cues.AddRange(Read<Image[]>("runnerFootfallPips"));
            int count = 0;
            foreach (Graphic cue in cues)
            {
                if (cue == null || !cue.gameObject.activeInHierarchy || !cue.enabled || cue.color.a < 0.01f) continue;
                Bounds area = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, cue.rectTransform);
                bool sharesWidth = area.max.x > text.min.x && area.min.x < text.max.x;
                Require(!sharesWidth || area.min.y >= text.max.y + 7f,
                    currentFrame.Name + ": runner cue crosses the timing guide: " + cue.name);
                count++;
            }
            return count;
        }

        private static void VerifyButtonTextBounds(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label == null || !label.enabled || string.IsNullOrWhiteSpace(label.text)) return;
            VerifyTextBounds(label);
        }

        private static void VerifyTextBounds(Text label)
        {
            IList<UIVertex> vertices = label.cachedTextGenerator.verts;
            Require(vertices.Count > 0, currentFrame.Name + ": missing text: " + label.name);
            Rect rect = label.rectTransform.rect;
            const float tolerance = 3f;
            foreach (UIVertex vertex in vertices)
            {
                Vector3 point = vertex.position / label.pixelsPerUnit;
                Require(point.x >= rect.xMin - tolerance && point.x <= rect.xMax + tolerance
                    && point.y >= rect.yMin - tolerance && point.y <= rect.yMax + tolerance,
                    currentFrame.Name + ": text exceeds its area: " + label.name
                        + " / bounds " + rect + " / glyph " + point + " / text " + label.text.Replace('\n', '|'));
            }
        }

        private static void WriteCaptureReport(bool succeeded, string error)
        {
            Directory.CreateDirectory("Logs/VisualChecks");
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.succeeded = succeeded;
            report.error = error;
            File.WriteAllText("Logs/VisualChecks/latest-report.json", JsonUtility.ToJson(report, true));
        }

        private static T Read<T>(string name)
        {
            return (T)typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        }

        private static void Write(string name, object value)
        {
            typeof(GreedLastRuntimeUi).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, value);
        }

        private static void ReleaseTarget()
        {
            if (camera != null) camera.targetTexture = null;
            if (target == null) return;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            target = null;
        }

        private static void Cleanup()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            ReleaseTarget();
            if (ui != null) GreedLastPrototypeAudioCheck.ReleaseEditorAudio(ui.gameObject);
            if (captureScene.IsValid()) EditorSceneManager.ClosePreviewScene(captureScene);
            if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            ui = null;
            canvas = null;
            camera = null;
            eventSystem = null;
            Frames.Clear();
            running = false;
        }
    }
}
