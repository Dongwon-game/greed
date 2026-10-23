using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
        private static int textCompletenessCalls;
        private static string captureDirectory;
        private static bool currentFrameRendered;
        private static CaptureReport report;
        private static string CaptureRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "VisualChecks");

        [Serializable]
        private sealed class CaptureReport
        {
            public string startedUtc;
            public string completedUtc;
            public string unityVersion;
            public bool succeeded;
            public string error;
            public string archivedReport;
            public string captureDirectory, failedFrameFile, failureCaptureError, expectedRejectionMessage;
            public string scope = "Isolated preview-camera canvas rendering with synthetic core snapshots, manual UI Update and per-frame synthetic pointer probes, including possible held-input redraws. Not native screen capture, actual engine gameplay, hardware input, audible output, device FPS or latency testing.";
            public bool pointerLoopPassed;
            public bool pointerLoopIncluded;
            public bool scoreBoundaryOnly;
            public bool progressBoundaryOnly;
            public bool preparationPreviewOnly;
            public bool rewardPreviewOnly;
            public bool recoveryPreviewOnly;
            public bool choicePreviewOnly;
            public bool liveFocusPreviewOnly;
            public bool resolvedBonusPreviewOnly;
            public bool goodFocusPreviewOnly;
            public bool textClippingRejectionOnly, expectedClippingRejected, lowHealthInputOnly, menuScrollInputOnly;
            public bool infiniteResultOnly;
            public int wrongChannelAlternatesRejected;
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
            public int checkedModuleMarginInputs, checkedSameChannelOverlapInputs;
            public int checkedModuleScrolls;
            public int checkedSuspendedScrolls;
            public int checkedControlPoints;
            public int checkedBlockedLanePoints;
            public int checkedLaneLabels;
            public int checkedFeedbackAreas;
            public int checkedProgressAreas;
            public int checkedRunnerCueClearances;
            public int checkedTextCompletenessCalls;
            public bool retainedScreen;
            public bool forcedLaneOverlap;
            public int brightSamples;
            public int detailSamples;
        }

        [Serializable]
        private sealed class PublicationCheckReport
        {
            public string startedUtc, completedUtc, unityVersion, error;
            public bool succeeded;
            public string scope = "Real filesystem operations in an isolated evidence directory. Not disk-full, power-loss, actual scene cleanup, gameplay or hardware testing.";
            public int transientLockRetries;
            public List<string> cases = new List<string>();
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
            public Action BeforeCapture;
            public bool ForcedLaneOverlap;
            public bool RetainedScreen;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            Begin(true);
        }

        [MenuItem("Greed Last/Capture Prototype Layout Only")]
        public static void RunRenderOnly()
        {
            Begin(false);
        }

        [MenuItem("Greed Last/Capture Score Boundary Checks")]
        public static void RunScoreBoundaryOnly()
        {
            Begin(false, scoreBoundaryOnly: true);
        }

        [MenuItem("Greed Last/Capture Progress Boundary Checks")]
        public static void RunProgressBoundaryOnly()
        {
            Begin(false, progressBoundaryOnly: true);
        }

        [MenuItem("Greed Last/Capture Infinite Preparation Previews")]
        public static void RunPreparationPreviewOnly()
        {
            Begin(false, preparationPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Live Reward Previews")]
        public static void RunRewardPreviewOnly()
        {
            Begin(false, rewardPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Recovery Previews")]
        public static void RunRecoveryPreviewOnly()
        {
            Begin(false, recoveryPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Choice Reward Previews")]
        public static void RunChoicePreviewOnly()
        {
            Begin(false, choicePreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Live Focus Feedback")]
        public static void RunLiveFocusPreviewOnly()
        {
            Begin(false, liveFocusPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Reward Guidance")]
        public static void RunRewardGuidanceOnly()
        {
            Begin(false, choicePreviewOnly: true, liveFocusPreviewOnly: true, resolvedBonusPreviewOnly: true, goodFocusPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Resolved Bonus Feedback")]
        public static void RunResolvedBonusPreviewOnly()
        {
            Begin(false, resolvedBonusPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Good Focus Feedback")]
        public static void RunGoodFocusPreviewOnly()
        {
            Begin(false, goodFocusPreviewOnly: true);
        }

        [MenuItem("Greed Last/Capture Text Clipping Rejection Check")]
        public static void RunTextClippingRejection()
        {
            Begin(false, textClippingRejectionOnly: true);
        }

        [MenuItem("Greed Last/Capture Low Health Input Checks")]
        public static void RunLowHealthInput()
        {
            Begin(false, lowHealthInputOnly: true);
        }

        public static void RunMenuScrollInput()
        {
            Begin(false, menuScrollInputOnly: true);
        }

        [MenuItem("Greed Last/Capture Infinite Result Actions")]
        public static void RunInfiniteResultOnly()
        {
            Begin(false, infiniteResultOnly: true);
        }

        private static void Begin(bool includePointerLoop, bool scoreBoundaryOnly = false, bool progressBoundaryOnly = false,
            bool textClippingRejectionOnly = false, bool lowHealthInputOnly = false, bool preparationPreviewOnly = false,
            bool rewardPreviewOnly = false, bool recoveryPreviewOnly = false, bool choicePreviewOnly = false, bool liveFocusPreviewOnly = false,
            bool resolvedBonusPreviewOnly = false, bool goodFocusPreviewOnly = false, bool menuScrollInputOnly = false,
            bool infiniteResultOnly = false)
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play mode before capturing prototype frames.");
            }

            GreedLastPrototypePlayModeCheck.RequireAsyncBatchLifetime();

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
                pointerLoopIncluded = includePointerLoop,
                scoreBoundaryOnly = scoreBoundaryOnly,
                progressBoundaryOnly = progressBoundaryOnly,
                preparationPreviewOnly = preparationPreviewOnly,
                rewardPreviewOnly = rewardPreviewOnly,
                recoveryPreviewOnly = recoveryPreviewOnly,
                choicePreviewOnly = choicePreviewOnly,
                liveFocusPreviewOnly = liveFocusPreviewOnly,
                resolvedBonusPreviewOnly = resolvedBonusPreviewOnly,
                goodFocusPreviewOnly = goodFocusPreviewOnly,
                textClippingRejectionOnly = textClippingRejectionOnly,
                lowHealthInputOnly = lowHealthInputOnly,
                menuScrollInputOnly = menuScrollInputOnly,
                infiniteResultOnly = infiniteResultOnly,
            };
            try
            {
                report.captureDirectory = "capture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff",
                    System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                captureDirectory = Path.Combine(CaptureRoot, report.captureDirectory);
                Require(!Directory.Exists(captureDirectory), "Visual captures require a new evidence directory.");
                Directory.CreateDirectory(captureDirectory);
                if (includePointerLoop)
                {
                    GreedLastPrototypeAppLoopCheck.RunPointerLoop();
                    report.pointerLoopPassed = true;
                    report.pointerLoopCases = GreedLastPrototypeAppLoopCheck.LastPointerCaseCount;
                    report.pointerLoopClicks = GreedLastPrototypeAppLoopCheck.LastPointerClickCount;
                    report.pointerLoopCanceledCommands = GreedLastPrototypeAppLoopCheck.LastCanceledCommandCount;
                }
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
                AddNaturalInfiniteResultFrames();
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
                AddReviewRecommendationFrames();
                AddActualInputChannelFrames();
                AddStructuredTimingFrames();
                AddCalibratedWaitingFrames();
                AddInterruptedFramePauseFrames();
                AddPracticeMissFrames();
                AddFocusedPracticeFrames();
                AddCountdownPulseFrames();
                AddFocusedPracticeOutcomeFrames();
                AddRefillReviewFrames();
                AddRefillClearSaveFrames();
                AddLongRunCounterFrames();
                AddScoreBoundaryFrames();
                AddProgressBoundaryFrames();
                AddPreparationPreviewFrames();
                AddDirectRetryReviewFrames();
                AddPracticeCandidateFrames();
                AddResumeInputBufferFrames();
                AddRewardPreviewFrames();
                AddRecoveryPreviewFrames();
                AddFinisherPreviewFrames();
                AddChoiceRewardPreviewFrames();
                AddLiveFocusFeedbackFrames();
                AddResolvedBonusFeedbackFrames();
                AddGoodFocusFeedbackFrames();
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
                if (scoreBoundaryOnly || progressBoundaryOnly || preparationPreviewOnly || rewardPreviewOnly || recoveryPreviewOnly || choicePreviewOnly || liveFocusPreviewOnly || resolvedBonusPreviewOnly || goodFocusPreviewOnly)
                {
                    string prefix = goodFocusPreviewOnly ? "103-good-focus-" : resolvedBonusPreviewOnly ? "102-resolved-bonus-" : liveFocusPreviewOnly ? "101-live-focus-" : choicePreviewOnly ? "100-choice-reward-" : recoveryPreviewOnly ? "99-recovery-" : rewardPreviewOnly ? "98-live-reward-" : preparationPreviewOnly ? "96-preparation-"
                        : progressBoundaryOnly ? "93-progress-" : "92-score-ceiling-";
                    int count = Frames.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Frame frame = Frames.Dequeue();
                        if (frame.Name.StartsWith(prefix, StringComparison.Ordinal)
                            || (choicePreviewOnly && frame.Name.StartsWith("100-choice-reward-", StringComparison.Ordinal))
                            || (liveFocusPreviewOnly && frame.Name.StartsWith("101-live-focus-", StringComparison.Ordinal))
                            || (resolvedBonusPreviewOnly && frame.Name.StartsWith("102-resolved-bonus-", StringComparison.Ordinal))
                            || (goodFocusPreviewOnly && frame.Name.StartsWith("103-good-focus-", StringComparison.Ordinal))
                            || (preparationPreviewOnly && frame.Name.StartsWith("104-direct-retry-", StringComparison.Ordinal))
                            || (preparationPreviewOnly && frame.Name.StartsWith("105-practice-candidate-", StringComparison.Ordinal))
                            || (preparationPreviewOnly && frame.Name.StartsWith("106-resume-input-buffer-", StringComparison.Ordinal))
                            || (preparationPreviewOnly && frame.Name.StartsWith("97-growth-start-", StringComparison.Ordinal)))
                            Frames.Enqueue(frame);
                    }
                    int guidanceFrames = (choicePreviewOnly ? 30 : 0) + (liveFocusPreviewOnly ? 24 : 0) + (resolvedBonusPreviewOnly ? 12 : 0) + (goodFocusPreviewOnly ? 18 : 0);
                    Require(Frames.Count == (guidanceFrames > 0 ? guidanceFrames : recoveryPreviewOnly ? 36 : rewardPreviewOnly ? 18 : preparationPreviewOnly ? 78 : progressBoundaryOnly ? 12 : 24),
                        "The focused capture must include only its assigned frames.");
                }
                if (infiniteResultOnly)
                {
                    int count = Frames.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Frame frame = Frames.Dequeue();
                        if (frame.Name.StartsWith("107-infinite-terminal-", StringComparison.Ordinal)) Frames.Enqueue(frame);
                    }
                    Require(Frames.Count == 12, "Natural infinite result captures require both phases, modes and three sizes.");
                }
                if (textClippingRejectionOnly)
                {
                    Frames.Clear();
                    Add("94-text-rejection-valid", 1280, 720,
                        () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"));
                    Add("94-text-rejection-clipped", 1280, 720,
                        () => ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구"), beforeUpdate: () =>
                        {
                            Text label = Read<Button>("practiceRunButton").GetComponentInChildren<Text>();
                            label.text = "SAVE\nAGAIN\nEXIT";
                            label.fontSize = 44;
                            label.resizeTextForBestFit = false;
                            label.verticalOverflow = VerticalWrapMode.Truncate;
                            label.alignment = TextAnchor.UpperLeft;
                            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                            label.rectTransform.sizeDelta = new Vector2(420f, 65f);
                        });
                }
                if (lowHealthInputOnly)
                {
                    Frame[] candidates = Frames.ToArray();
                    Frames.Clear();
                    foreach (Frame frame in candidates)
                    {
                        if (!frame.Name.StartsWith("46-", StringComparison.Ordinal)
                            && !frame.Name.StartsWith("47-", StringComparison.Ordinal)
                            && !frame.Name.StartsWith("48-", StringComparison.Ordinal)
                            && !frame.Name.StartsWith("49-", StringComparison.Ordinal)) continue;
                        int phase = 0;
                        foreach (double settle in new[] { 0.03, 0.12, 0.22 })
                        {
                            Frames.Enqueue(new Frame { Name = frame.Name + "-phase-" + phase++, Width = frame.Width,
                                Height = frame.Height, SafeInset = frame.SafeInset, SafeInsets = frame.SafeInsets,
                                Show = frame.Show, BeforeUpdate = frame.BeforeUpdate, SettleSeconds = settle });
                        }
                        Frames.Enqueue(new Frame { Name = frame.Name + "-forced-overlap", Width = frame.Width,
                            Height = frame.Height, SafeInset = frame.SafeInset, SafeInsets = frame.SafeInsets,
                            Show = frame.Show, SettleSeconds = 0.12, ForcedLaneOverlap = true,
                            BeforeCapture = () =>
                            {
                                foreach (Button lane in Read<Button[]>("laneButtons"))
                                    lane.transform.localScale = new Vector3(1.2f, 1f, 1f);
                            } });
                    }
                    Require(Frames.Count == 16, "Low-health input capture must cover four views at three feedback phases and a forced overlap each.");
                }
                if (menuScrollInputOnly)
                {
                    Frames.Clear();
                    string detail = string.Join("\n", new string[40]).Replace("\n", "비교 기록 12345 / 마지막 1회\n");
                    void ShowMenu() => ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                        GreedLastScreenState.InfiniteRecordBoard, "기록", detail, 0));
                    void ShowNearOverflow()
                    {
                        for (int lines = 1; lines <= 40; lines++)
                        {
                            string text = string.Join("\n", new string[lines]).Replace("\n", "비교 기록 12345 / 마지막 1회\n");
                            ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(
                                GreedLastScreenState.InfiniteRecordBoard, "기록", text, 0));
                            if (Read<ScrollRect>("menuDetailScroll").vertical) return;
                        }
                        throw new InvalidOperationException("Near-overflow probes must find the first real overflowing text layout.");
                    }
                    Add("107-menu-scroll-desktop", 1280, 720, ShowMenu);
                    Add("107-menu-scroll-small", 960, 540, ShowMenu);
                    Add("107-menu-scroll-tablet", 1024, 768, ShowMenu);
                    Add("107-menu-scroll-notch-left", 2340, 1080, ShowMenu, safeInsets: new Vector4(120f, 36f, 24f, 18f));
                    Add("107-menu-scroll-notch-right", 2340, 1080, ShowMenu, safeInsets: new Vector4(24f, 36f, 120f, 18f));
                    Add("108-menu-scroll-near-desktop", 1280, 720, ShowNearOverflow);
                    Add("108-menu-scroll-near-small", 960, 540, ShowNearOverflow);
                    Add("108-menu-scroll-near-tablet", 1024, 768, ShowNearOverflow);
                    Add("108-menu-scroll-near-notch-left", 2340, 1080, ShowNearOverflow, safeInsets: new Vector4(120f, 36f, 24f, 18f));
                    Add("108-menu-scroll-near-notch-right", 2340, 1080, ShowNearOverflow, safeInsets: new Vector4(24f, 36f, 120f, 18f));
                }
                BeginNextFrame();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            }
            catch (Exception exception)
            {
                FinishCapture(false, exception.Message);
                throw;
            }
        }

        [MenuItem(MenuPath, true)]
        [MenuItem("Greed Last/Capture Text Clipping Rejection Check", true)]
        [MenuItem("Greed Last/Capture Low Health Input Checks", true)]
        [MenuItem("Greed Last/Run Capture Finish Policy Check", true)]
        [MenuItem("Greed Last/Run Capture Publication Check", true)]
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

        private static void AddNaturalInfiniteResultFrames()
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            core.EnterInfiniteRun();
            float clock = Time.realtimeSinceStartup;
            for (int tick = 0; !run.Stopped && tick < 10000; tick++) core.Tick(clock += 0.05f);
            Require(run.Stopped && run.Health == 0 && !run.DevInvincible,
                "Natural infinite result frames need a real no-input failure without shortcuts.");
            AddResult(run, "end");
            core.HandleInput(GreedLastRunChannel.Left, clock += 0.05f);
            Require(run.Stopped && run.JudgementText.Contains("정산 후 재준비", StringComparison.Ordinal),
                "Ignored result input must preserve the current preparation action.");
            AddResult(run, "ignored");
            core.Exit();

            void AddResult(GreedLastRunSnapshot snapshot, string phase)
            {
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                        ShowRun(snapshot);
                        Require(Read<Button>("nextPatternButton").GetComponentInChildren<Text>().text == "정산 후 재준비",
                            "Every natural infinite result frame must retain its actual preparation button.");
                    }
                    string name = "107-infinite-terminal-" + phase + (detailed ? "-detail" : "-brief");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
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
            AddDetailedChoice("start", start);
            core.JumpToDevShortcut(GreedLastDevShortcut.GiftChoice, clock);
            GreedLastRunSnapshot gift = run;
            Add("34-gift-choice", 1280, 720, () => ShowRun(gift));
            Add("35-gift-choice-tablet", 1024, 768, () => ShowRun(gift));
            AddDetailedChoice("gift", gift);
            core.JumpToDevShortcut(GreedLastDevShortcut.RelicChoice, clock);
            GreedLastRunSnapshot relic = run;
            Add("36-relic-choice", 1280, 720, () => ShowRun(relic));
            Add("37-relic-choice-tablet", 1024, 768, () => ShowRun(relic));
            AddDetailedChoice("relic", relic);
            core.HandleInput(GreedLastRunChannel.Center, clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            GreedLastRunSnapshot live = run;
            Add("38-live-after-choice", 1280, 720, () => ShowRun(live));

            core.Enter();
            clock = Time.realtimeSinceStartup;
            for (int step = 0; step < 16 && run.ChoiceKind != GreedLastChoiceKind.Node; step++)
            {
                if (run.ChoiceActive) core.HandleInput(GreedLastRunChannel.Center, clock += 0.2f);
                else if (run.StartCountdownActive)
                {
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                }
                else if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                else core.HandleInput(run.Pattern.Channel,
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds);
            }
            Require(run.ChoiceActive && run.ChoiceKind == GreedLastChoiceKind.Node,
                "Detailed route captures must follow a real completed chapter.");
            AddDetailedChoice("route", run);

            void AddDetailedChoice(string name, GreedLastRunSnapshot choice)
            {
                Require(choice.ChoiceActive, "Detailed choice captures need an actual core choice.");
                void Show()
                {
                    if (ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ShowRun(choice);
                    Button[] shortcuts = Read<Button[]>("devShortcutButtons");
                    for (int i = 0; i < shortcuts.Length; i++)
                        Require(shortcuts[i].gameObject.activeInHierarchy == (i < (int)GreedLastDevShortcut.ReviewTargetCheck)
                            && shortcuts[i].IsInteractable() == (i < (int)GreedLastDevShortcut.ReviewTargetCheck),
                            "Choice lanes must hide only the overlapping inner tools and retain outer section shortcuts.");
                }
                Add("92-choice-tools-" + name + "-detail-small", 960, 540, Show);
                Add("92-choice-tools-" + name + "-detail-tablet", 1024, 768, Show);
                Add("92-choice-tools-" + name + "-detail-wide", 2340, 1080, Show, 96f);
            }
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
                if (kind == 4) core.Tick(clock += Mathf.Max(run.Pattern.HitSeconds,
                    run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds) + 0.1f);
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
                    if (outcome == "no-input") breakCore.Tick(breakClock + Mathf.Max(broken.Pattern.HitSeconds,
                        broken.Pattern.CoreSeconds + broken.Pattern.GoodWindowSeconds - breakCore.InputTimingOffsetSeconds) + 0.02f);
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

        private static void AddReviewRecommendationFrames()
        {
            foreach (bool infinite in new[] { false, true })
            foreach (bool good in new[] { false, true })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup + 100f;
                if (infinite) core.EnterInfiniteRun(GreedLastRunRecord.Empty, "재도전 목표: 보정 0회 / 현재 1");
                else
                {
                    core.ConfigureGrowth(3, 0, 0);
                    core.Enter("복기 재도전\n재도전 목표: 1장 보정 0회 / 현재 1");
                }
                int consumed = 0;
                for (int guard = 0; guard < 40; guard++)
                {
                    if (run.ActivePattern && !run.CountdownActive
                        && (infinite ? consumed == 2 : run.ChapterIndex == 2 && run.ChapterProgress == 1)) break;
                    if (run.ChoiceActive) core.HandleInput(GreedLastRunChannel.Left, clock += 0.1f);
                    else if (run.CountdownActive || !run.ActivePattern) core.Tick(clock += 2f);
                    else
                    {
                        if (!infinite && consumed < 2)
                        {
                            clock += Mathf.Max(run.Pattern.HitSeconds,
                                run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds)
                                - run.ElapsedSeconds + 0.02f;
                            core.Tick(clock);
                        }
                        else
                        {
                            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                            core.HandleInput(run.Pattern.Channel, clock);
                        }
                        consumed++;
                    }
                }
                Require(run.ActivePattern && !run.CountdownActive && !run.Stopped
                    && run.TimingRecommendationSampleCount == 2 && !run.HasTimingRecommendation
                    && !string.IsNullOrEmpty(run.ReviewTargetText),
                    "Review captures need two real samples immediately before a third live judgement.");
                GreedLastRunSnapshot before = run;
                float delta = good ? (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds + delta;
                core.HandleInput(run.Pattern.Channel, clock);
                GreedLastRunSnapshot after = run;
                Require(after.HasTimingRecommendation && !after.Stopped && !after.ChoiceActive
                    && after.LastResult == (good ? GreedLastJudgementResult.Good : GreedLastJudgementResult.Success),
                    "The third real sample must expose its live recommendation with the review outcome.");
                string review = GreedLastRuntimeUi.BuildReviewTargetFeedbackLine(after);
                Require(!string.IsNullOrEmpty(review), "A live review capture needs its actual current target result.");
                void ShowDetailed()
                {
                    if (ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ShowRun(before);
                    ui.RenderRun(after);
                    string text = Read<Text>("judgementFeedbackText").text;
                    Require(text.Contains(review, StringComparison.Ordinal)
                        && text.Contains("보정 추천 준비", StringComparison.Ordinal),
                        "Detail must preserve both the real review outcome and newly available recommendation. Actual: "
                            + text.Replace('\n', '|'));
                    float until = Read<float>("judgementFeedbackUntil");
                    float popUntil = Read<float>("judgementFeedbackPopUntil");
                    int history = Read<int>("judgementHistoryCount");
                    ui.ToggleDevCleanView();
                    ui.ToggleDevCleanView();
                    Require(Read<Text>("judgementFeedbackText").text == text
                        && Read<float>("judgementFeedbackUntil") == until
                        && Read<float>("judgementFeedbackPopUntil") == popUntil
                        && Read<int>("judgementHistoryCount") == history,
                        "F11 must retain the existing review/recommendation without replaying its judgement.");
                }
                string name = "87-review-recommendation-" + (infinite ? "infinite" : "normal") + (good ? "-good" : "-exact");
                Add(name + "-detail-small", 960, 540, ShowDetailed, settleSeconds: 0.16);
                Add(name + "-detail-tablet", 1024, 768, ShowDetailed, settleSeconds: 0.16);
                Add(name + "-detail-wide", 2340, 1080, ShowDetailed, 96f, 0.16);
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

        private static void AddCalibratedWaitingFrames()
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var deck = (GreedLastPatternModel[])typeof(GreedLastRunCore).GetField("patternDeck", fields)
                .GetValue(new GreedLastRunCore());
            for (int index = 0; index < 3; index++)
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.RestartPractice(clock);
                core.Tick(clock += 2f);
                typeof(GreedLastRunCore).GetField("inputTimingOffsetSeconds", fields).SetValue(core, -0.2f);
                typeof(GreedLastRunCore).GetField("devNextPatternOverride", fields).SetValue(core, deck[index]);
                core.RequestNextPattern(clock += 1f);
                core.Tick(clock + run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds + 0.05f);
                GreedLastRunSnapshot pending = run;
                Require(pending.ActivePattern && pending.CoreDeltaSeconds > pending.Pattern.GoodWindowSeconds
                    && Mathf.Abs(pending.CoreDeltaSeconds + pending.InputTimingOffsetSeconds) <= pending.Pattern.GoodWindowSeconds
                    && pending.SuccessCount + pending.GoodCount + pending.MissCount == 0,
                    "Waiting capture requires a genuine pending calibrated input, not a resolved judgement.");
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(pending);
                        if (detailed) ui.ToggleDevCleanView();
                        typeof(GreedLastRuntimeUi).GetMethod("UpdateRunTimingVisuals", fields).Invoke(ui, null);
                        Require(Read<Text>("timingGuideText").text.Contains("보정 입력 대기", StringComparison.Ordinal),
                            "The captured guide must display pending input in basic and detailed views.");
                    }
                    string name = "90-calibrated-wait-" + pending.Pattern.TimingType.ToString().ToLowerInvariant()
                        + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddInterruptedFramePauseFrames()
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            core.Tick(clock += 0.25f);
            GreedLastRunSnapshot before = run;
            Require(core.PauseForInterruptedFrame(clock + 0.75f) && run.Paused && !run.ResumeCountdownActive
                && run.Distance == before.Distance && run.Health == before.Health,
                "Interrupted frame capture must use a real frozen core snapshot.");
            GreedLastRunSnapshot paused = run;
            void Show()
            {
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                ShowRun(paused);
                Require(Read<GameObject>("pauseMenuOverlay").activeInHierarchy,
                    "Frame interruption must display the real pause menu, not an unclickable frozen run.");
            }
            Add("91-frame-interruption-small", 960, 540, Show);
            Add("91-frame-interruption-tablet", 1024, 768, Show);
            Add("91-frame-interruption-wide", 2340, 1080, Show, 96f);
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

        private static void AddPreparationPreviewFrames()
        {
            string[] gifts = { "집중 축", "연쇄 축", "생존 축" };
            string[] relics = { string.Empty, "황금 박동", "사막의 잔상" };
            for (int sample = 0; sample < 4; sample++)
            {
                int slot = sample % 3;
                bool growth = sample == 3;
                string preparationGift = gifts[slot];
                string preparationRelic = growth ? "황금 박동" : relics[slot];
                var backend = new GreedLastMockBackend(false, false);
                var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 2, 2, 2, 1, 80f,
                    "기프트 방향", growth ? string.Empty : "박자 감각", growth ? "연쇄 축" : "생존 축",
                    string.Empty, growth ? "연쇄 축" : "안정 루트 / 생존 축", string.Empty);
                if (growth)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    foreach (string field in new[] { "growthSurvivalLevel", "growthFocusLevel", "growthTimingLevel" })
                        typeof(GreedLastMockBackend).GetField(field, flags).SetValue(backend, 3);
                }
                backend.RegisterClear(record);
                backend.SelectSaveLoadoutSlot(slot);
                backend.ConfirmSaveLoadout();
                var gate = new GreedLastRequestGate();
                var state = new GreedLastStateMachine(gate);
                GreedLastStateSnapshot screen = default;
                state.StateChanged += value => screen = value;
                state.SetLobbyReady(backend.LoadLobby());
                GreedLastApp app = CreateIsolatedCaptureApp(backend, state, gate);
                try
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(GreedLastApp).GetField("infinitePreparationStep", flags).SetValue(app, sample == 0 ? 1 : 2);
                    typeof(GreedLastApp).GetField("infinitePreparationGift", flags).SetValue(app, preparationGift);
                    typeof(GreedLastApp).GetField("infinitePreparationRelic", flags).SetValue(app, preparationRelic);
                    typeof(GreedLastApp).GetMethod("OpenInfinitePreparation", flags)
                        .Invoke(app, new object[] { null, false });
                }
                finally { UnityEngine.Object.DestroyImmediate(app.gameObject); }
                var prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(record, preparationGift, preparationRelic);
                string expectedBonus = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(prepared);
                string expectedStart = GreedLastRunCore.BuildInfiniteLoadoutStartStatePreview(prepared,
                    backend.GrowthSurvivalLevel, backend.GrowthFocusLevel, backend.GrowthTimingLevel);
                Require(screen.State == GreedLastScreenState.InfiniteStartReady
                    && screen.Detail.Contains(expectedBonus, StringComparison.Ordinal)
                    && screen.Detail.Contains(expectedStart, StringComparison.Ordinal),
                    "Preparation captures must use the actual app's current prepared preview.");
                void Show()
                {
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ui.Render(screen);
                    Require(Read<Text>("detailText").text.Contains(expectedBonus, StringComparison.Ordinal)
                        && Read<Text>("detailText").text.Contains(expectedStart, StringComparison.Ordinal),
                        "The visible preparation body must retain its applied bonus and starting state.");
                }
                string name = growth ? "97-growth-start-complete"
                    : "96-preparation-S" + (slot + 1) + (slot == 0 ? "-gift" : "-complete");
                Add(name + "-small", 960, 540, Show);
                Add(name + "-tablet", 1024, 768, Show);
                Add(name + "-wide", 2340, 1080, Show, 96f);
            }
        }

        private static void AddDirectRetryReviewFrames()
        {
            var backend = new GreedLastMockBackend(false, false);
            backend.AddSampleSaveLoadoutForTest(out _);
            var gate = new GreedLastRequestGate();
            var state = new GreedLastStateMachine(gate);
            GreedLastStateSnapshot screen = default;
            state.StateChanged += value => screen = value;
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            GreedLastApp app = CreateIsolatedCaptureApp(backend, state, gate, core);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Command(string name, params object[] args) => typeof(GreedLastApp).GetMethod(name, flags).Invoke(app, args);
                void Capture(string phase, bool live)
                {
                    GreedLastStateSnapshot captured = screen;
                    GreedLastRunSnapshot capturedRun = run;
                    foreach (bool detailed in new[] { false, true })
                    {
                        void Show()
                        {
                            if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                            ui.Render(captured);
                            if (live)
                            {
                                ui.RenderRun(capturedRun);
                                if (phase == "start-pause")
                                    Require(Read<Button>("returnLobbyButton").GetComponentInChildren<Text>().text == "기록 없이 로비"
                                        && Read<Text>("pauseMenuBodyText").text.Contains("사용권 차감 유지", StringComparison.Ordinal),
                                        "An unplayed pause must describe no record and the already-spent use in both views.");
                            }
                            else if (phase == "unplayed")
                                Require(Read<Text>("detailText").text.Contains("주행 전 종료 / 기록·성장 보상 없음", StringComparison.Ordinal)
                                    && Read<Text>("detailText").text.Contains("다음: 무한모드 다시 준비", StringComparison.Ordinal),
                                    "The visible unplayed-abandonment receipt must explain the absent record and reward.");
                            else
                            {
                                string body = Read<Text>("detailText").text;
                                int result = body.IndexOf("무한 기록 저장", StringComparison.Ordinal);
                                Require(result >= 0 && body.IndexOf("시작 정비", StringComparison.Ordinal) < result
                                    && body.IndexOf("시작 상태:", StringComparison.Ordinal) >= 0
                                    && body.IndexOf("시작 상태:", StringComparison.Ordinal) < result,
                                    "Direct retry preparation must show current choices/start before the previous result summary.");
                            }
                        }
                        string name = "104-direct-retry-" + phase + (detailed ? "-detail" : "-basic");
                        Add(name + "-small", 960, 540, Show);
                        Add(name + "-tablet", 1024, 768, Show);
                        Add(name + "-wide", 2340, 1080, Show, 96f);
                    }
                }
                state.SetInfiniteRun(string.Empty);
                core.EnterInfiniteRun(((GreedLastRunRecord[])typeof(GreedLastMockBackend)
                    .GetField("saveLoadoutSlots", flags).GetValue(backend))[0]);
                GreedLastPrototypeAppLoopCheck.RunToFailure(core, () => run);
                Command("RequestNextRunPattern");
                string review = (string)typeof(GreedLastApp)
                    .GetField("pendingInfinitePreparationReviewContext", flags).GetValue(app);
                Require(!string.IsNullOrEmpty(review) && screen.Detail.Contains(review, StringComparison.Ordinal),
                    "Retry captures must contain the natural result's actual transferred review.");
                Capture("entry", false);
                foreach (GreedLastRunChannel channel in new[] { GreedLastRunChannel.Center, GreedLastRunChannel.Center })
                {
                    typeof(GreedLastApp).GetField("infinitePreparationInputBlockUntil", flags).SetValue(app, 0f);
                    Command("HandleRunChannelInput", channel);
                    if ((int)typeof(GreedLastApp).GetField("infinitePreparationStep", flags).GetValue(app) == 1)
                        Capture("gift", false);
                }
                Capture("ready", false);
                typeof(GreedLastApp).GetField("infinitePreparationInputBlockUntil", flags).SetValue(app, 0f);
                Command("RequestNextRunPattern");
                Require(run.InfiniteMode && !string.IsNullOrEmpty(run.ReviewTargetText)
                    && run.JudgementText.Contains(review, StringComparison.Ordinal),
                    "Retry-start captures must retain the prepared current goal in the actual core snapshot.");
                Capture("start", true);
                Command("RequestToggleRunPause");
                Capture("start-pause", true);
                Command("RequestReturnToLobby");
                Require(screen.Detail.Contains("주행 전 종료 / 기록·성장 보상 없음", StringComparison.Ordinal)
                    && (int)typeof(GreedLastMockBackend).GetField("infiniteRunCount", flags).GetValue(backend) == 1,
                    "Abandoning an unplayed seeded-score start must preserve the prior record, not settle another one.");
                Capture("unplayed", false);
                Command("RequestInfiniteRun");
                Require(string.IsNullOrEmpty((string)typeof(GreedLastApp)
                    .GetField("pendingInfinitePreparationResultNotice", flags).GetValue(app)),
                    "A new preparation after unplayed abandonment must not restore the old saved-result summary.");
            }
            finally { UnityEngine.Object.DestroyImmediate(app.gameObject); }
        }

        private static void AddPracticeCandidateFrames()
        {
            const string practiceKey = "GreedLast.Onboarding.PracticeCompleted";
            bool hadPractice = PlayerPrefs.HasKey(practiceKey);
            int practiceValue = PlayerPrefs.GetInt(practiceKey);
            var backend = new GreedLastMockBackend(false, false);
            backend.RegisterClear(new GreedLastRunRecord(true, 900, 3, 0, 0, 5, 3, 0, 0, 80f,
                "before-practice", "박자 감각", "집중 축", string.Empty, "변주 루트 / 집중 축", string.Empty));
            var gate = new GreedLastRequestGate();
            var state = new GreedLastStateMachine(gate);
            GreedLastStateSnapshot screen = default;
            state.StateChanged += value => screen = value;
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            GreedLastApp app = CreateIsolatedCaptureApp(backend, state, gate, core);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                core.SnapshotChanged += (Action<GreedLastRunSnapshot>)typeof(GreedLastApp)
                    .GetMethod("HandleRunSnapshotChanged", flags).CreateDelegate(typeof(Action<GreedLastRunSnapshot>), app);
                void Command(string name, params object[] args) => typeof(GreedLastApp).GetMethod(name, flags).Invoke(app, args);
                void Capture(string phase, bool live)
                {
                    GreedLastStateSnapshot captured = screen;
                    GreedLastRunSnapshot capturedRun = run;
                    foreach (bool detailed in new[] { false, true })
                    {
                        void Show()
                        {
                            bool hasKey = PlayerPrefs.HasKey(practiceKey);
                            int previous = PlayerPrefs.GetInt(practiceKey);
                            try
                            {
                                PlayerPrefs.SetInt(practiceKey, 0);
                                if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                                ui.Render(captured);
                                if (live) ui.RenderRun(capturedRun);
                                Require(!live || Read<Button>("nextPatternButton").GetComponentInChildren<Text>().text == "저장 후보 확인",
                                    "The first-practice result must visibly name the pending candidate handoff.");
                                Require(phase != "draft" || !Read<Button>("nextPatternButton").IsInteractable(),
                                    "A captured practice handoff must not accept the recommended slot without a choice.");
                            }
                            finally
                            {
                                if (hasKey) PlayerPrefs.SetInt(practiceKey, previous); else PlayerPrefs.DeleteKey(practiceKey);
                            }
                        }
                        string name = "105-practice-candidate-" + phase + (detailed ? "-detail" : "-basic");
                        Add(name + "-small", 960, 540, Show);
                        Add(name + "-tablet", 1024, 768, Show);
                        Add(name + "-wide", 2340, 1080, Show, 96f);
                    }
                }
                PlayerPrefs.SetInt(practiceKey, 0);
                state.SetLobbyReady(backend.LoadLobby());
                state.SetRunCoreTest();
                core.EnterPractice();
                GreedLastPrototypeAppLoopCheck.RunToCompletion(core, () => run);
                Capture("result", true);
                Command("RequestNextRunPattern");
                Require(screen.State == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "The practice capture must follow the actual protected candidate handoff.");
                Capture("draft", false);
                Command("HandleRunChannelInput", GreedLastRunChannel.Right);
                Command("RequestNextRunPattern");
                Require(!backend.HasSaveLoadoutCandidate && backend.LoadLobby().SaveSlotOccupied[2],
                    "The practice capture must save only to the explicitly selected S3.");
                Capture("saved", false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(app.gameObject);
                if (hadPractice) PlayerPrefs.SetInt(practiceKey, practiceValue); else PlayerPrefs.DeleteKey(practiceKey);
                PlayerPrefs.Save();
            }
        }

        private static void AddResumeInputBufferFrames()
        {
            foreach (bool practice in new[] { true, false })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                if (practice) core.RestartPractice(clock);
                else
                {
                    core.Enter();
                    core.HandleInput(GreedLastRunChannel.Center, clock);
                }
                clock = Mathf.Max(clock, Time.realtimeSinceStartup);
                for (int wait = 0; !run.ActivePattern && wait < 500; wait++) core.Tick(clock += 0.01f);
                Require(run.ActivePattern && !run.CountdownActive && !run.DevInvincible,
                    "Resume preview requires a real opening trap without shortcuts.");
                core.Tick(clock += 0.04f);
                core.TogglePause(clock);
                core.TogglePause(clock += 0.1f);
                core.Tick(clock += 1.5f);
                core.HandleInput(run.Pattern.Channel, clock += 0.01f);
                Require(!run.Paused && !run.CountdownActive && run.ActivePattern && run.ElapsedSeconds < -0.5f
                    && run.IgnoreReason == "resume_buffer" && run.MissCount == 0,
                    "Resume preview must capture a genuinely ignored pre-start tap, not a seeded message.");
                GreedLastRunSnapshot captured = run;
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                        ShowRun(captured);
                    }
                    string name = "106-resume-input-buffer-" + (practice ? "practice" : "normal")
                        + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show);
                    Add(name + "-tablet", 1024, 768, Show);
                    Add(name + "-wide", 2340, 1080, Show, 96f);
                }
                core.Exit();
            }
        }

        private static void AddRewardPreviewFrames()
        {
            for (int sample = 0; sample < 3; sample++)
            {
                var pair = GreedLastPrototypeScoreCheck.BuildRewardPreviewSample(sample);
                bool good = sample == 2;
                string name = "98-live-reward-" + (sample == 0 ? "normal-high" : sample == 1 ? "pressure-bundle" : "good-section");
                void ShowPreview()
                {
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                    ShowRun(pair.before);
                    typeof(GreedLastRuntimeUi).GetMethod("UpdateRunTimingVisuals", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ui, null);
                    string cue = Read<Text>("activeActionCueText").text;
                    Require(cue.Contains("정박 +" + pair.before.CenterHitScoreGainPreview, StringComparison.Ordinal)
                        && !cue.Contains("정확 +", StringComparison.Ordinal),
                        "Reward captures must identify the central preview, not promise it for every outer Success.");
                }
                void ShowJudgement()
                {
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                    ShowRun(pair.before);
                    ui.RenderRun(pair.after);
                    Require(Read<Text>("judgementFeedbackText").text.Contains("+" + pair.after.LastJudgementScoreGain + "점", StringComparison.Ordinal),
                        "Reward captures must retain actual chain/bundle/section-inclusive feedback.");
                    Require((good ? pair.before.GoodScoreGainPreview : pair.before.CenterHitScoreGainPreview)
                        == pair.after.LastJudgementScoreGain, "Rendered preview and judgement gain must agree.");
                }
                foreach (bool before in new[] { true, false })
                {
                    Action show = before ? ShowPreview : ShowJudgement;
                    string suffix = before ? "-preview" : "-judgement";
                    Add(name + suffix + "-small", 960, 540, show, settleSeconds: 0.16);
                    Add(name + suffix + "-tablet", 1024, 768, show, settleSeconds: 0.16);
                    Add(name + suffix + "-wide", 2340, 1080, show, 96f, 0.16);
                }
            }
        }

        private static void AddRecoveryPreviewFrames()
        {
            var stable = GreedLastPrototypeRecoveryCheck.CreateSamples(0, 0, "안정 루트", false);
            var stacked = GreedLastPrototypeRecoveryCheck.CreateSamples(0, 2, "안정 루트", true);
            var samples = new[] { stable[0], stable[1], stacked[0] };
            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                int expected = i == 0 ? 1 : i == 1 ? 0 : 2;
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                        ShowRun(sample.Before);
                        if (detailed) ui.ToggleDevCleanView();
                        typeof(GreedLastRuntimeUi).GetMethod("UpdateRunTimingVisuals", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(ui, null);
                        Require(sample.Before.GoodHealthGainPreview == expected
                            && sample.After.LastJudgementHealthChange == expected,
                            "Recovery captures must use real +1/+0/+2 Good snapshots with unchanged recovery rules.");
                        string cue = Read<Text>("activeActionCueText").text;
                        if (detailed)
                            Require(expected > 0 ? cue.Contains("보정회복+" + expected, StringComparison.Ordinal)
                                : !cue.Contains("보정회복+", StringComparison.Ordinal),
                                "Detailed recovery captures must not promise healing after its actual cap.");
                    }
                    string name = "99-recovery-" + (expected == 0 ? "capped" : expected == 1 ? "expand" : "stacked")
                        + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddFinisherPreviewFrames()
        {
            var normal = GreedLastPrototypeRecoveryCheck.CreateBundleSamples(0, 0, 0, 0);
            var broken = GreedLastPrototypeRecoveryCheck.CreateBundleSamples(0, 0, 0, 0, true);
            var samples = new[] { normal[0], normal[1], broken[0] };
            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                string expected = i == 0 ? "회복 대기" : i == 1 ? "집중 보상 대기" : "중단";
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                        ShowRun(sample.AnticipationBefore);
                        ui.RenderRun(sample.Anticipation);
                        if (detailed) ui.ToggleDevCleanView();
                        if (detailed)
                            Require(Read<Text>("judgementFeedbackText").text.Contains(expected, StringComparison.Ordinal),
                                "Visible penultimate-judgement feedback must distinguish recovery, capped focus and broken bundles. Actual: "
                                    + Read<Text>("judgementFeedbackText").text);
                    }
                    string name = "99-recovery-finisher-" + (i == 0 ? "heal" : i == 1 ? "focus" : "broken")
                        + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddChoiceRewardPreviewFrames()
        {
            var samples = GreedLastPrototypeRecoveryCheck.CreateChoicePreviewSamples();
            foreach (string name in new[] { "start", "health-cap", "route-cap", "focus-cap", "activation" })
            {
                var snapshot = samples[name];
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                        ShowRun(snapshot);
                        if (detailed) ui.ToggleDevCleanView();
                        Text[] labels = Read<Text[]>("laneLabelTexts");
                        if (name == "health-cap") Require(labels[1].text.Contains("체력 상한 유지", StringComparison.Ordinal), "Gift choices must show actual capped healing.");
                        if (name == "route-cap") Require(labels[0].text.Contains("체력 상한 유지", StringComparison.Ordinal), "Stable choices must show immediate capped healing separately from their ongoing rule.");
                        if (name == "focus-cap") Require(labels[1].text.Contains("집중 상태 상한 유지", StringComparison.Ordinal)
                            && labels[2].text.Contains("집중 상태 상한 유지", StringComparison.Ordinal), "Both capped relics must stop promising new focus.");
                        if (name == "activation") Require(labels[0].text.Contains("집중 상태 발동", StringComparison.Ordinal), "Focus choices must preview the real activation.");
                    }
                    string prefix = "100-choice-reward-" + name + (detailed ? "-detail" : "-basic");
                    Add(prefix + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(prefix + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(prefix + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddLiveFocusFeedbackFrames()
        {
            foreach (var pair in GreedLastPrototypeRecoveryCheck.CreateLiveFocusSamples())
                foreach (bool resolved in new[] { false, true })
                    foreach (bool detailed in new[] { false, true })
                    {
                        string kind = pair.Key;
                        var sample = pair.Value;
                        void Show()
                        {
                            if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                            ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                            ShowRun(sample.Before);
                            if (resolved) ui.RenderRun(sample.After);
                            if (detailed) ui.ToggleDevCleanView();
                            if (resolved && detailed)
                            {
                                string feedback = Read<Text>("judgementFeedbackText").text;
                                Require(!feedback.Contains("정박 충전 +", StringComparison.Ordinal)
                                    && !feedback.Contains("루트 집중 +", StringComparison.Ordinal)
                                    && feedback.Contains("집중 연장", StringComparison.Ordinal),
                                    "Detailed focus feedback must show actual extension rather than an ignored charge. Actual: " + feedback);
                            }
                            if (resolved && !detailed && kind == "perfect")
                                Require(Read<Text>("judgementFeedbackText").text.Contains("집중 유지", StringComparison.Ordinal),
                                    "Basic active-focus Perfect feedback must not claim another gauge charge.");
                        }
                        string name = "101-live-focus-" + kind + (resolved ? "-result" : "-preview") + (detailed ? "-detail" : "-basic");
                        Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                        Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                        Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                    }
        }

        private static void AddResolvedBonusFeedbackFrames()
        {
            var samples = GreedLastPrototypeScoreCheck.CreateResolvedBonusSamples(0);
            var heal = samples.Find(sample => sample.profile == 8 && sample.after.Health > sample.before.Health);
            var threat = samples.Find(sample => sample.before.InfiniteThreatLevel == 2 && sample.after.InfiniteThreatLevel == 3);
            Require(heal.after.LastResult == GreedLastJudgementResult.Success && threat.after.LastResult == GreedLastJudgementResult.Success,
                "Bonus captures must use actual pre-heal and pre-rise scoring, not edited snapshots.");
            for (int index = 0; index < 2; index++)
            {
                var sample = index == 0 ? heal : threat;
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                        ShowRun(sample.before);
                        ui.RenderRun(sample.after);
                        if (detailed) ui.ToggleDevCleanView();
                        if (!detailed) return;
                        string feedback = Read<Text>("judgementFeedbackText").text;
                        Require(feedback.Contains("조합 +" + sample.profile, StringComparison.Ordinal),
                            "Visible profile bonus must retain its scored amount across recovery. Actual: " + feedback);
                        Require(sample.threat == 0 ? !feedback.Contains("위협 +", StringComparison.Ordinal)
                            : feedback.Contains("위협 +" + sample.threat, StringComparison.Ordinal),
                            "Visible threat bonus must belong to the completed trap, not the next level. Actual: " + feedback);
                    }
                    string name = "102-resolved-bonus-" + (index == 0 ? "profile-heal" : "threat-rise") + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddGoodFocusFeedbackFrames()
        {
            var samples = GreedLastPrototypeRecoveryCheck.CreateGoodFocusSamples();
            var activation = samples.Find(sample => sample.After.LastResult == GreedLastJudgementResult.Good
                && !sample.Before.FocusStateActive && sample.After.FocusStateActive && !sample.After.ChoiceActive);
            var empty = samples.Find(sample => sample.After.LastResult == GreedLastJudgementResult.Good
                && sample.Before.FocusStateActive && sample.Before.Combo == 0 && !sample.After.ChoiceActive);
            var retained = samples.Find(sample => sample.After.LastResult == GreedLastJudgementResult.Good
                && sample.Before.FocusStateActive && sample.After.Combo > 0 && !sample.After.ChoiceActive);
            Require(activation.After.LastResult == GreedLastJudgementResult.Good && empty.After.LastResult == GreedLastJudgementResult.Good
                && retained.After.LastResult == GreedLastJudgementResult.Good, "Good captures need natural activation, empty and retained chain results.");
            for (int index = 0; index < 3; index++)
            {
                var sample = index == 0 ? activation : index == 1 ? empty : retained;
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowScreen(GreedLastScreenState.LobbyReady, "피라미드 입구");
                        ShowRun(sample.Before);
                        ui.RenderRun(sample.After);
                        if (detailed) ui.ToggleDevCleanView();
                        if (!detailed) return;
                        string feedback = Read<Text>("judgementFeedbackText").text;
                        Require(feedback.Contains("집중 발동", StringComparison.Ordinal) == !sample.Before.FocusStateActive
                            && feedback.Contains("집중 연쇄 완충", StringComparison.Ordinal) == (sample.After.Combo > 0),
                            "Good feedback must separate activation and genuinely retained chains. Actual: " + feedback);
                    }
                    string name = "103-good-focus-" + (index == 0 ? "activation" : index == 1 ? "empty" : "retained") + (detailed ? "-detail" : "-basic");
                    Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                    Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                    Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
                }
            }
        }

        private static void AddScoreBoundaryFrames()
        {
            var capped = GreedLastPrototypeScoreCheck.VerifyJudgement(int.MaxValue - 5, GreedLastJudgementResult.Success);
            void ShowPreview()
            {
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                ShowRun(capped.before);
                typeof(GreedLastRuntimeUi).GetMethod("UpdateRunTimingVisuals", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui, null);
                Require(Read<Text>("activeActionCueText").text.Contains("정박 +5", StringComparison.Ordinal),
                    "The visible action cue must preview only the five points still available at the score ceiling.");
            }
            Add("92-score-ceiling-preview-small", 960, 540, ShowPreview, settleSeconds: 0.16);
            Add("92-score-ceiling-preview-tablet", 1024, 768, ShowPreview, settleSeconds: 0.16);
            Add("92-score-ceiling-preview-wide", 2340, 1080, ShowPreview, 96f, 0.16);
            foreach (bool detailed in new[] { false, true })
            {
                void Show()
                {
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    ShowRun(capped.before);
                    ui.RenderRun(capped.after);
                    if (detailed) ui.ToggleDevCleanView();
                    GreedLastRunSnapshot presented = Read<GreedLastRunSnapshot>("lastPresentedJudgementSnapshot");
                    string feedback = Read<Text>("judgementFeedbackText").text;
                    Require(presented.Score == int.MaxValue && presented.LastJudgementScoreGain == 5
                        && feedback.Contains("점수 상한", StringComparison.Ordinal)
                        && (detailed || feedback.Contains("+5점", StringComparison.Ordinal)),
                        "Capped feedback must retain five awarded points in both views and show +5 points in the player format.");
                }
                string name = "92-score-ceiling-" + (detailed ? "detail" : "basic");
                Add(name + "-small", 960, 540, Show, settleSeconds: 0.16);
                Add(name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                Add(name + "-wide", 2340, 1080, Show, 96f, 0.16);
            }
            var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            backend.RegisterInfiniteRun(new GreedLastInfiniteRunRecord(true, int.MaxValue, 10000f,
                400, 5, "score-boundary", "연쇄형", 1200, 0, 3, 2000, "no_input", string.Empty));
            var machine = new GreedLastStateMachine(new GreedLastRequestGate());
            GreedLastStateSnapshot board = default;
            machine.StateChanged += value => board = value;
            machine.SetInfiniteRecordBoard(backend.LoadLobby(), backend.BuildRecordBoardText(2), 2);
            GreedLastStateSnapshot recordBoard = board;
            void ShowRecordBoard()
            {
                ui.Render(recordBoard);
                Require(recordBoard.State == GreedLastScreenState.InfiniteRecordBoard
                    && Read<Text>("titleText").text == recordBoard.Headline
                    && Read<Text>("detailText").text.Contains(int.MaxValue.ToString(), StringComparison.Ordinal),
                    "Deferred score-ceiling record captures must retain the board and its actual capped record, not a later save comparison.");
            }
            Add("92-score-ceiling-record-small", 960, 540, ShowRecordBoard);
            Add("92-score-ceiling-record-tablet", 1024, 768, ShowRecordBoard);
            Add("92-score-ceiling-record-wide", 2340, 1080, ShowRecordBoard, 96f);

            var high = new GreedLastRunRecord(true, int.MaxValue, 3, 0, 1, int.MaxValue, 15, 0, 0,
                90f, "quality-boundary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            var qualityBackend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            qualityBackend.RegisterClear(high);
            double quality = Convert.ToDouble(typeof(GreedLastMockBackend).GetMethod("BuildSaveLoadoutQualityScore",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { high }));
            Require(quality > int.MaxValue, "The quality capture must cover a sum above the stored score ceiling.");
            machine.SetSaveLoadoutDraft(qualityBackend.LoadLobby(),
                qualityBackend.BuildSaveLoadoutDraftText(showSelectedSlot: false)
                    + "\n선택 전 저장 잠김 / 저장 포기는 후보 소모",
                saveSlotChoiceRequired: true, saveLoadoutCandidateAvailable: true);
            AddQualityState("choice", board, "선택 전 저장 잠김");
            qualityBackend.SelectSaveLoadoutSlot(2);
            machine.SetSaveLoadoutDraft(qualityBackend.LoadSelectedSaveSlotView(), qualityBackend.BuildSaveLoadoutDraftText(),
                saveLoadoutCandidateAvailable: true);
            AddQualityState("selected", board, "품질 " + quality);
            machine.SetLobbyReady(qualityBackend.ConfirmSaveLoadout());
            machine.ShowLocalNotice(qualityBackend.BuildSaveConfirmMessage());
            AddQualityState("saved", board, "품질 " + quality);

            var comparisonBackend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            comparisonBackend.RegisterClear(new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                "ordinary-quality", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
            comparisonBackend.SelectSaveLoadoutSlot(1);
            comparisonBackend.ConfirmSaveLoadout();
            comparisonBackend.RegisterClear(high);
            comparisonBackend.SelectSaveLoadoutSlot(1);
            string comparison = comparisonBackend.BuildSelectedSaveLoadoutOverwritePreviewText();
            Require(comparison.Contains("품질 +", StringComparison.Ordinal), "A stronger candidate must retain a positive comparison.");
            machine.SetSaveLoadoutDraft(comparisonBackend.LoadSelectedSaveSlotView(), comparison,
                saveSlotOverwriteConfirmationPending: true, saveLoadoutCandidateAvailable: true);
            AddQualityState("comparison", board, "품질 +");

            void AddQualityState(string name, GreedLastStateSnapshot state, string expected)
            {
                void Show()
                {
                    ui.Render(state);
                    Require(Read<Text>("detailText").text.Contains(expected, StringComparison.Ordinal),
                        "The actual quality body must retain its wide amount/sign: " + expected);
                    Require(state.State != GreedLastScreenState.SaveLoadoutDraft
                        || Read<Button>("nextPatternButton").IsInteractable() == !state.SaveSlotChoiceRequired,
                        "The captured candidate must retain its actual explicit-choice save lock.");
                }
                Add("92-score-ceiling-quality-" + name + "-small", 960, 540, Show, settleSeconds: 0.16);
                Add("92-score-ceiling-quality-" + name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                Add("92-score-ceiling-quality-" + name + "-wide", 2340, 1080, Show, 96f, 0.16);
            }
        }

        private static void AddProgressBoundaryFrames()
        {
            var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
            var machine = new GreedLastStateMachine(new GreedLastRequestGate());
            GreedLastStateSnapshot state = default;
            machine.StateChanged += value => state = value;
            backend.AddGrowthPointsForTest(3);
            machine.SetGrowthBoard(backend.LoadLobby(), backend.BuildGrowthBoardText());
            AddState("growth-normal", state, "성장 포인트 3");
            foreach (string field in new[] { "growthPoints", "normalClearCount", "normalFailCount", "normalAbandonCount", "infiniteRunCount" })
                typeof(GreedLastMockBackend).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(backend, int.MaxValue);
            machine.SetGrowthBoard(backend.LoadLobby(), backend.BuildGrowthBoardText());
            AddState("growth-ceiling", state, "성장 포인트 " + int.MaxValue);
            backend.UpgradeGrowth(GreedLastRunChannel.Center, out _);
            int balance = backend.LoadLobby().GrowthPoints;
            Require(balance > 0 && balance < int.MaxValue && backend.GrowthFocusLevel == 1,
                "The progress capture needs an actual existing-cost purchase from a capped balance.");
            machine.SetGrowthBoard(backend.LoadLobby(), backend.BuildGrowthBoardText());
            AddState("growth-purchased", state, "성장 포인트 " + balance);
            machine.SetProfileBoard(backend.LoadLobby(), backend.BuildProfileBoardText(0), 0);
            AddState("profile", state, "일반 6442450941회");

            void AddState(string name, GreedLastStateSnapshot snapshot, string expected)
            {
                void Show()
                {
                    ui.Render(snapshot);
                    Require(Read<Text>("detailText").text.Contains(expected, StringComparison.Ordinal),
                        "The visible progress body must retain its wide value after rendering: " + expected);
                }
                Add("93-progress-" + name + "-small", 960, 540, Show, settleSeconds: 0.16);
                Add("93-progress-" + name + "-tablet", 1024, 768, Show, settleSeconds: 0.16);
                Add("93-progress-" + name + "-wide", 2340, 1080, Show, 96f, 0.16);
            }
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
                if (kind == 0) core.Tick(clock += Mathf.Max(run.Pattern.HitSeconds,
                    run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds)
                    - run.ElapsedSeconds + 0.1f);
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

        private static void AddFocusedPracticeFrames()
        {
            foreach (var lesson in new[] {
                (Step: 1, Context: "탈출 실패 기록 저장\n복기: 숙이기 실수 1회 / 기초연습 숙이기 단계"),
                (Step: 2, Context: "탈출 실패 기록 저장\n복기: 비틀기 실수 2회 / 기초연습 비틀기 단계\n재도전 목표: 2장 무실수 / 현재 1"),
                (Step: 1, Context: "무한모드 종료 / 기록 저장\n복기: 무한 1구간 / 숙이기 실수 1회\n힌트: 다음 구간 정확권 전환 / 기초연습 숙이기 단계\n목표: 다음 구간 무실수 / 직전 2") })
            {
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                string context = GreedLastApp.BuildPracticeOpeningContextFromLobbyDetail(lesson.Context);
                core.EnterPractice(context);
                GreedLastRunSnapshot countdown = run;
                float clock = Time.realtimeSinceStartup;
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(run.ActivePattern && run.ChapterProgress == lesson.Step,
                    "Focused practice captures must start at the actual requested lesson.");
                GreedLastRunSnapshot live = run;
                for (int attempt = 0; attempt < 3 - lesson.Step; attempt++)
                {
                    if (attempt > 0) core.RequestNextPattern(clock += 2f);
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                }
                GreedLastRunSnapshot result = run;
                Require(result.Stopped && result.SuccessCount == 3 - lesson.Step
                    && !result.JudgementText.Contains("3축 정확", StringComparison.Ordinal),
                    "Focused results must show only the attempted remaining axes.");
                string name = lesson.Context.StartsWith("무한", StringComparison.Ordinal)
                    ? "infinite-slide" : lesson.Step == 1 ? "normal-slide" : "normal-twist";
                foreach (var phase in new[] { (Name: "countdown", Run: countdown), (Name: "live", Run: live), (Name: "result", Run: result) })
                {
                    void Show()
                    {
                        if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                        ShowRun(phase.Run);
                        if (phase.Name == "result")
                        {
                            Require(Read<Button>("nextPatternButton").GetComponentInChildren<Text>().text == "복기 다시 확인",
                                "Focused completion must expose the matching focused restart action.");
                            Require(Read<Text>("titleText").text == "복기 연습 완료",
                                "Focused exact completion must display its actual partial-course title.");
                        }
                    }
                    string key = "88-focused-practice-" + name + "-" + phase.Name;
                    Add(key + "-small", 960, 540, Show);
                    Add(key + "-tablet", 1024, 768, Show);
                    Add(key + "-wide", 2340, 1080, Show, 96f);
                }
            }
        }

        private static void AddCountdownPulseFrames()
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot run = default;
            core.SnapshotChanged += value => run = value;
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            Capture("start-2", run, "2");
            core.Tick(clock += 0.65f);
            Capture("start-1", run, "1");
            core.Tick(clock += 0.65f);
            Capture("start-go", run, "GO");
            core.Tick(clock += 0.7f);
            core.Tick(clock += 1f);
            Require(run.ActivePattern && !run.CountdownActive,
                "Countdown captures need a real released practice before pausing.");
            core.TogglePause(clock += 0.1f);
            core.TogglePause(clock += 0.1f);
            Capture("resume-2", run, "2");
            core.Tick(clock += 0.65f);
            Capture("resume-1", run, "1");
            core.Tick(clock += 0.65f);
            Capture("resume-go", run, "GO");

            void Capture(string name, GreedLastRunSnapshot snapshot, string expectedText)
            {
                Require(snapshot.CountdownActive, "Each countdown pulse must come from an active core countdown.");
                foreach (bool detailed in new[] { false, true })
                {
                    void Show()
                    {
                        if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                        ShowRun(snapshot);
                        Require(Read<Text>("judgementFeedbackText").text == expectedText,
                            "Countdown feedback data must match its actual remaining time.");
                        VerifyForeground();
                    }
                    void VerifyForeground()
                    {
                        Text visible = Read<Text>(snapshot.ResumeCountdownActive
                            ? "pauseMenuTitleText" : "judgementFeedbackText");
                        Require(visible.isActiveAndEnabled && visible.color.a > 0f
                            && visible.text == (snapshot.ResumeCountdownActive ? "재개 " : string.Empty) + expectedText,
                            "The foreground countdown, not a covered label, must show " + expectedText);
                    }
                    string key = "89-countdown-" + name + (detailed ? "-detail" : "-basic");
                    Add(key + "-small", 960, 540, Show, beforeUpdate: VerifyForeground);
                    Add(key + "-tablet", 1024, 768, Show, beforeUpdate: VerifyForeground);
                    Add(key + "-wide", 2340, 1080, Show, 96f, beforeUpdate: VerifyForeground);
                }
            }
        }

        private static void AddFocusedPracticeOutcomeFrames()
        {
            foreach (int step in new[] { 1, 2 })
            {
                foreach (string outcome in new[] { "good", "wrong", "no-input" })
                {
                    var core = new GreedLastRunCore();
                    GreedLastRunSnapshot run = default;
                    core.SnapshotChanged += value => run = value;
                    core.EnterPractice("복기 연습\n복기: 기초연습 " + (step == 1 ? "숙이기 단계" : "비틀기 단계"));
                    float clock = Time.realtimeSinceStartup;
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                    for (int attempt = 0; attempt < 3 - step; attempt++)
                    {
                        if (attempt > 0) core.RequestNextPattern(clock += 2f);
                        Require(run.ActivePattern, "Focused result captures need their actual authored remaining trap.");
                        if (outcome == "no-input")
                            core.Tick(clock += Mathf.Max(run.Pattern.HitSeconds,
                                run.Pattern.CoreSeconds + run.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds)
                                + 0.02f - run.ElapsedSeconds);
                        else
                        {
                            clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - run.ElapsedSeconds;
                            if (outcome == "good")
                                clock += (run.Pattern.SuccessWindowSeconds + run.Pattern.GoodWindowSeconds) * 0.5f;
                            GreedLastRunChannel channel = outcome == "good" ? run.Pattern.Channel
                                : run.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left;
                            core.HandleInput(channel, clock);
                        }
                    }
                    GreedLastRunSnapshot result = run;
                    Require(core.IsPracticeComplete && result.SuccessCount == 0
                        && result.GoodCount + result.MissCount == 3 - step && !result.SaveEligible,
                        "Focused captures must keep Good and Miss outcomes without full-course or save claims.");
                    foreach (bool detailed in new[] { false, true })
                    {
                        void Show()
                        {
                            if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                            ShowRun(result);
                            string expected = outcome == "good" ? "정확권 다시" : "실수 구간 다시";
                            Require(Read<Button>("nextPatternButton").GetComponentInChildren<Text>().text == expected,
                                "Focused result action must match its real Good or Miss result.");
                            Require(Read<Text>("titleText").text == "복기 연습 완료",
                                "Focused Good and Miss outcomes must not claim full-course completion.");
                        }
                        string key = "90-focused-outcome-" + step + "-" + outcome + (detailed ? "-detail" : "-basic");
                        Add(key + "-small", 960, 540, Show);
                        Add(key + "-tablet", 1024, 768, Show);
                        Add(key + "-wide", 2340, 1080, Show, 96f);
                    }
                }
            }
        }

        private static void AddRefillReviewFrames()
        {
            foreach (GreedLastRunAttemptOutcome outcome in new[] {
                GreedLastRunAttemptOutcome.Failed, GreedLastRunAttemptOutcome.Abandoned })
            {
                var backend = new GreedLastMockBackend(false, false);
                backend.AddSampleSaveLoadoutForTest(out _);
                Require(backend.TryConsumeSelectedSaveLoadout(out _, out _, out _)
                    && backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "Refill captures need legitimately depleted transient slots.");
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                float clock = Time.realtimeSinceStartup;
                core.Enter(string.Empty, refillRun: true);
                for (int step = 0; step < 100 && !run.Stopped; step++)
                {
                    if (run.ChoiceActive)
                        core.HandleInput(run.ChoiceKind == GreedLastChoiceKind.Node ? GreedLastRunChannel.Left : GreedLastRunChannel.Center, clock += 0.2f);
                    else if (run.StartCountdownActive)
                    {
                        core.Tick(clock += 2f);
                        core.Tick(clock += 1f);
                    }
                    else if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                    else
                    {
                        core.Tick(clock += run.Pattern.HitSeconds + 0.2f - run.ElapsedSeconds);
                        if (outcome == GreedLastRunAttemptOutcome.Abandoned) break;
                    }
                }
                Require(outcome == GreedLastRunAttemptOutcome.Failed ? run.Stopped && run.Health == 0 : !run.Stopped && run.Health > 0,
                    "Refill failure and live abandonment captures must retain their distinct outcomes.");
                GreedLastRunAttemptRecord record = core.CreateNormalAttemptRecord(outcome);
                Require(backend.RegisterNormalAttempt(record), "Refill captures must use the actual recorded attempt.");
                string notice = (string)typeof(GreedLastApp).GetMethod("BuildRefillAttemptResultMessage",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { backend.BuildRunAttemptResultMessage(record), outcome });
                string review = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(notice);
                var gate = new GreedLastRequestGate();
                var state = new GreedLastStateMachine(gate);
                GreedLastStateSnapshot screen = default;
                state.StateChanged += value => screen = value;
                state.SetLobbyReady(backend.LoadLobby());
                state.ShowLocalNotice(notice);
                GreedLastStateSnapshot lobby = screen;
                GreedLastStateSnapshot management;
                GreedLastApp captureApp = CreateIsolatedCaptureApp(backend, state, gate);
                try
                {
                    typeof(GreedLastApp).GetMethod("RequestSaveLoadout", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(captureApp, null);
                    management = screen;
                    Require(management.State == GreedLastScreenState.SaveLoadoutDraft
                        && management.Detail.Contains(review, StringComparison.Ordinal),
                        "Refill management captures must use the actual app entry and its preserved result goal.");
                }
                finally { UnityEngine.Object.DestroyImmediate(captureApp.gameObject); }
                state.SetInfiniteStartReady(backend.LoadLobby(), review + "\n\n" + backend.BuildInfiniteStartReadyText());
                GreedLastStateSnapshot preparation = screen;
                string opening = (string)typeof(GreedLastApp).GetMethod("BuildRefillRunOpeningContext",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { notice });
                core.Enter(opening, refillRun: true);
                Require(run.ChoiceActive && !string.IsNullOrEmpty(run.ReviewTargetText),
                    "Refill captures must carry the real result goal into the fresh starting choice.");
                GreedLastRunSnapshot choice = run;
                clock = Time.realtimeSinceStartup;
                core.HandleInput(GreedLastRunChannel.Center, clock += 0.2f);
                GreedLastRunSnapshot countdown = run;
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(run.ActivePattern && run.ReviewTargetText == choice.ReviewTargetText,
                    "Refill review must remain active after its actual opening countdown.");
                GreedLastRunSnapshot live = run;
                core.TogglePause(clock += 0.1f);
                GreedLastRunSnapshot paused = run;
                foreach (bool detailed in new[] { false, true })
                {
                    void Mode() { if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView(); }
                    void AddViews(string phase, Action show)
                    {
                        string name = "91-refill-review-" + outcome.ToString().ToLowerInvariant() + "-" + phase
                            + (detailed ? "-detail" : "-basic");
                        void Show() { Mode(); show(); }
                        Add(name + "-small", 960, 540, Show);
                        Add(name + "-tablet", 1024, 768, Show);
                        Add(name + "-wide", 2340, 1080, Show, 96f);
                    }
                    AddViews("lobby", () => ui.Render(lobby));
                    AddViews("preparation", () => ui.Render(preparation));
                    AddViews("management", () => ui.Render(management));
                    AddViews("choice", () =>
                    {
                        ShowRun(choice);
                        if (detailed)
                        {
                            Button[] shortcuts = Read<Button[]>("devShortcutButtons");
                            for (int i = (int)GreedLastDevShortcut.ReviewTargetCheck; i < shortcuts.Length; i++)
                                Require(!shortcuts[i].gameObject.activeInHierarchy && !shortcuts[i].IsInteractable(),
                                    "Inner-column development commands must not block the wide choice lanes.");
                        }
                    });
                    AddViews("countdown", () => ShowRun(countdown));
                    AddViews("live", () =>
                    {
                        ShowRun(live);
                        if (detailed)
                            Require(Read<Button[]>("devShortcutButtons")[(int)GreedLastDevShortcut.VisualDropPreview].gameObject.activeInHierarchy,
                                "The live development preview command must return after the choice ends.");
                    });
                    AddViews("pause", () =>
                    {
                        ShowRun(paused);
                        if (detailed)
                        {
                            Text body = Read<Text>("pauseMenuBodyText");
                            Require(body.fontSize >= 18 && body.text.Contains("재개 목표", StringComparison.Ordinal),
                                "The detailed refill pause must fit its real review goal without dropping the target or shrinking below its readable limit.");
                        }
                    });
                }
            }
        }

        private static GreedLastApp CreateIsolatedCaptureApp(GreedLastMockBackend backend,
            GreedLastStateMachine state, GreedLastRequestGate gate, GreedLastRunCore core = null)
        {
            GreedLastApp app = new GameObject("GreedRefillCaptureApp").AddComponent<GreedLastApp>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Require(typeof(GreedLastApp).GetField("backend", flags).GetValue(app) == null,
                "A capture app must remain isolated from the persistent runtime bootstrap.");
            typeof(GreedLastApp).GetField("backend", flags).SetValue(app, backend);
            typeof(GreedLastApp).GetField("stateMachine", flags).SetValue(app, state);
            typeof(GreedLastApp).GetField("requestGate", flags).SetValue(app, gate);
            typeof(GreedLastApp).GetField("runCore", flags).SetValue(app, core);
            GreedLastRuntimeUi captureUi = app.gameObject.AddComponent<GreedLastRuntimeUi>();
            GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(captureUi, playerControls: true);
            typeof(GreedLastApp).GetField("runtimeUi", flags).SetValue(app, captureUi);
            GameObject panel = (GameObject)typeof(GreedLastRuntimeUi).GetField("rootPanel", flags).GetValue(captureUi);
            // Edit-mode fixtures own their Canvas through the host, unlike the persistent player UI.
            panel.GetComponentInParent<Canvas>().transform.SetParent(app.transform, false);
            Require(captureUi.AcceptsPlayerInput,
                "A capture app command needs its own live foreground presentation.");
            return app;
        }

        private static void AddRefillClearSaveFrames()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var backend = new GreedLastMockBackend(false, false);
                backend.AddSampleSaveLoadoutForTest(out _);
                Require(backend.TryConsumeSelectedSaveLoadout(out _, out _, out _)
                    && backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "Refill clear captures must begin after legitimate source-slot depletion.");
                var gate = new GreedLastRequestGate();
                var state = new GreedLastStateMachine(gate);
                GreedLastStateSnapshot screen = default;
                state.StateChanged += value => screen = value;
                var core = new GreedLastRunCore();
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                GreedLastApp app = CreateIsolatedCaptureApp(backend, state, gate, core);
                try
                {
                    void Command(string name, params object[] args) => typeof(GreedLastApp)
                        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, args);
                    void Capture(string phase)
                    {
                        GreedLastStateSnapshot captured = screen;
                        string name = "93-refill-save-s" + (slot + 1) + "-" + phase;
                        void Show()
                        {
                            if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                            ui.Render(captured);
                        }
                        Add(name + "-small", 960, 540, Show);
                        Add(name + "-tablet", 1024, 768, Show);
                        Add(name + "-wide", 2340, 1080, Show, 96f);
                    }
                    state.SetLobbyReady(backend.LoadLobby());
                    Command("RequestNormalRun");
                    Require(run.RefillRun && run.ChoiceActive, "Refill clear captures must use the actual lobby entry.");
                    GreedLastPrototypeAppLoopCheck.RunToCompletion(core, () => run);
                    Require(run.EscapeSucceeded && run.SaveEligible, "Refill captures must reach a real saveable escape.");
                    if (slot == 0)
                    {
                        GreedLastRunSnapshot clear = run;
                        foreach (bool detailed in new[] { false, true })
                        {
                            void ShowClear()
                            {
                                if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                                ShowRun(clear);
                                if (detailed)
                                {
                                    Button[] shortcuts = Read<Button[]>("devShortcutButtons");
                                    for (int i = (int)GreedLastDevShortcut.ReviewTargetCheck; i < shortcuts.Length; i++)
                                        Require(!shortcuts[i].gameObject.activeInHierarchy && !shortcuts[i].IsInteractable(),
                                            "Result detail must not leave inner development controls blocked behind its text.");
                                }
                            }
                            string name = "93-refill-clear-" + (detailed ? "detail" : "basic");
                            Add(name + "-small", 960, 540, ShowClear);
                            Add(name + "-tablet", 1024, 768, ShowClear);
                            Add(name + "-wide", 2340, 1080, ShowClear, 96f);
                        }
                    }
                    Command("RequestNextRunPattern");
                    Require(screen.State == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                        "Refill capture result commands must create an explicit one-shot candidate.");
                    if (slot == 0) Capture("candidate");
                    Command("HandleRunChannelInput", (GreedLastRunChannel)(slot + 1));
                    Capture("selected");
                    Command("RequestNextRunPattern");
                    Require(screen.State == GreedLastScreenState.InfiniteStartReady
                        && !backend.HasSaveLoadoutCandidate && backend.LoadLobby().SelectedSaveSlotIndex == slot,
                        "Chosen-slot refill captures must use the actual save-to-preparation transition.");
                    Capture("preparation");
                }
                finally { UnityEngine.Object.DestroyImmediate(app.gameObject); }
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
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            scroll.OnInitializePotentialDrag(pointer);
            scroll.OnBeginDrag(pointer);
            pointer.position += Vector2.up * 100f;
            scroll.OnDrag(pointer);
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.pointerUpHandler);
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
            currentFrameRendered = false;
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
                currentFrame.BeforeCapture?.Invoke();
                CaptureFrame();
                if (Frames.Count > 0)
                {
                    BeginNextFrame();
                    return;
                }
                Debug.Log("Greed Last visual checks captured: " + capturedFrameCount + " nonblank frames in Logs/VisualChecks. These are isolated UI fixtures, not real-device input tests.");
                Require(!report.textClippingRejectionOnly, "The clipped text fixture must be rejected, not captured as valid.");
                if (report.lowHealthInputOnly)
                {
                    int forced = 0, overlaps = 0, margins = 0;
                    foreach (CaptureResult frame in report.frames)
                    {
                        if (frame.forcedLaneOverlap) forced++;
                        overlaps += frame.checkedSameChannelOverlapInputs;
                        margins += frame.checkedModuleMarginInputs;
                        Require(frame.checkedModuleLaneInputs == 9 && frame.checkedTextCompletenessCalls > 0,
                            "Every low-health view must actually exercise all channel points and text checks.");
                    }
                    Require(report.frames.Count == 16 && forced == 4 && report.wrongChannelAlternatesRejected == 16
                        && overlaps >= 24 && margins > 0, "Low-health coverage must include real same-channel overlaps and wrong-channel rejection.");
                }
                FinishCapture(true, string.Empty);
            }
            catch (Exception exception)
            {
                PreserveFailedFrame();
                bool expected = report.textClippingRejectionOnly && currentFrame.Name == "94-text-rejection-clipped"
                    && exception.Message.Contains("text is vertically truncated", StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(report.failedFrameFile) && string.IsNullOrEmpty(report.failureCaptureError)
                    && File.Exists(Path.Combine(CaptureRoot, report.failedFrameFile)) && capturedFrameCount == 1;
                if (expected)
                {
                    report.expectedClippingRejected = true;
                    report.expectedRejectionMessage = exception.Message;
                    Debug.Log("Greed visual clipping rejection passed: valid positive control, clipped button rejected and failed preview preserved in its immutable run directory.");
                    FinishCapture(true, string.Empty);
                    return;
                }
                Debug.LogException(exception);
                FinishCapture(false, exception.Message);
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
            textCompletenessCalls = 0;
            currentFrameRendered = false;
            RectTransform root = Read<RectTransform>("rootPanelRect");
            ApplyCaptureSafeArea();
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            camera.Render();
            currentFrameRendered = true;
            bool growthPreview = currentFrame.Name.StartsWith("97-growth-start-", StringComparison.Ordinal);
            if (growthPreview || currentFrame.Name.StartsWith("96-preparation-", StringComparison.Ordinal))
            {
                Text body = Read<Text>("detailText");
                Require(body.text.IndexOf("시작 상태:", StringComparison.Ordinal) >= 0
                    && body.text.IndexOf("시작 상태:", StringComparison.Ordinal) < body.text.IndexOf("기록 기준", StringComparison.Ordinal)
                    && body.text.Contains(growthPreview ? "기프트 누적: 연쇄 x1" : "기프트 누적: 생존 x1", StringComparison.Ordinal),
                    "The rendered preparation must prioritize its applied start without replacing the stored gift summary.");
            }
            int checkedProgressAreas = 0;
            if (currentFrame.Name.StartsWith("93-progress-", StringComparison.Ordinal))
            {
                Text body = Read<Text>("detailText");
                Require(HasVisibleText(body), currentFrame.Name + ": progress body must be visible.");
                ScrollRect scroll = Read<ScrollRect>("menuDetailScroll");
                Require(scroll.gameObject.activeInHierarchy && scroll.content == body.rectTransform,
                    currentFrame.Name + ": progress data must use its separate scrolling area.");
                Bounds glyphs = GetTextGlyphBounds(scroll.viewport, body);
                Rect viewport = scroll.viewport.rect;
                Require(body.cachedTextGenerator.verts.Count > 4
                    && glyphs.min.x >= viewport.xMin && glyphs.max.x <= viewport.xMax,
                    currentFrame.Name + ": progress glyph overhangs must stay inside the clipping viewport.");
                Require(body.preferredHeight <= body.rectTransform.rect.height + 1f,
                    currentFrame.Name + ": progress body must retain every line without clipping.");
                checkedProgressAreas = 1;
            }
            int checkedButtons = VerifyVisibleControlRaycasts(out int checkedControlPoints);
            int checkedBlockedLanePoints = VerifyPausedLaneRaycasts();
            int checkedFeedbackAreas = VerifyVisibleFeedbackAreas();
            int checkedRunnerCueClearances = VerifyRunnerCueClearance();
            int checkedLaneLabels = VerifyLanePresentation();
            int checkedModuleClicks = VerifyActionModuleClick();
            int checkedModuleLaneInputs = VerifyLaneModuleInputs(out int checkedModuleMarginInputs,
                out int checkedSameChannelOverlapInputs);
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
                    report.failedFrameFile = Path.Combine(report.captureDirectory, currentFrame.Name + ".failed.png").Replace('\\', '/');
                    WriteNewEvidence(Path.Combine(CaptureRoot, report.failedFrameFile), image.EncodeToPNG());
                    throw new InvalidOperationException("Blank or incomplete visual capture: " + currentFrame.Name
                        + " / bright " + brightSamples + " / detail " + detailSamples
                        + " / canvas " + canvas.GetComponent<RectTransform>().rect
                        + " / root " + root.rect
                        + " / title vertices " + Read<Text>("titleText").cachedTextGenerator.vertexCount
                        + " / title culled " + Read<Text>("titleText").canvasRenderer.cull
                        + " / camera rect " + camera.pixelRect);
                }
                string relativeFile = Path.Combine(report.captureDirectory, currentFrame.Name + ".png").Replace('\\', '/');
                string path = Path.Combine(CaptureRoot, relativeFile);
                WriteNewEvidence(path, image.EncodeToPNG());
                capturedFrameCount++;
                report.frames.Add(new CaptureResult
                {
                    file = relativeFile,
                    width = target.width,
                    height = target.height,
                    safeInset = currentFrame.SafeInset,
                    safeInsets = currentFrame.SafeInsets,
                    checkedButtons = checkedButtons,
                    checkedModuleClicks = checkedModuleClicks,
                    checkedModuleLaneInputs = checkedModuleLaneInputs,
                    checkedModuleMarginInputs = checkedModuleMarginInputs,
                    checkedSameChannelOverlapInputs = checkedSameChannelOverlapInputs,
                    checkedModuleScrolls = checkedModuleScrolls,
                    checkedSuspendedScrolls = checkedModuleScrolls * 8,
                    checkedControlPoints = checkedControlPoints,
                    checkedBlockedLanePoints = checkedBlockedLanePoints,
                    checkedLaneLabels = checkedLaneLabels,
                    checkedFeedbackAreas = checkedFeedbackAreas,
                    checkedProgressAreas = checkedProgressAreas,
                    checkedRunnerCueClearances = checkedRunnerCueClearances,
                    checkedTextCompletenessCalls = textCompletenessCalls,
                    retainedScreen = currentFrame.RetainedScreen,
                    forcedLaneOverlap = currentFrame.ForcedLaneOverlap,
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

        private static bool HasVisibleText(Text label) => label.isActiveAndEnabled
            && label.color.a >= 0.01f && !string.IsNullOrWhiteSpace(label.text);

        private static Bounds GetTextGlyphBounds(RectTransform root, Text label)
        {
            TextGenerator generator = label.cachedTextGenerator;
            generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
            Bounds bounds = default;
            // UI Text appends a trailing empty quad, which is not a rendered glyph.
            for (int i = 0; i < generator.verts.Count - 4; i++)
            {
                Vector3 point = root.InverseTransformPoint(label.rectTransform.TransformPoint(
                    generator.verts[i].position / label.pixelsPerUnit));
                if (i == 0) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
            }
            return bounds;
        }

        private static int VerifyLaneModuleInputs(out int marginInputs, out int overlapInputs)
        {
            marginInputs = overlapInputs = 0;
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
            if (report.lowHealthInputOnly)
            {
                object callback = typeof(GreedLastRuntimeUi).GetField("runChannelRequested", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                bool rejected = false;
                try
                {
                    GreedLastPrototypeKeyboardCheck.VerifyLaneInput(ui, eventSystem, margins[0],
                        camera.WorldToScreenPoint(margins[0].transform.position), 0, alternateSurface: lanes[1]);
                }
                catch (InvalidOperationException error)
                {
                    rejected = error.Message == "An expanded lane probe may accept only its own visible channel as an alternate surface.";
                }
                Require(rejected && ReferenceEquals(callback,
                    typeof(GreedLastRuntimeUi).GetField("runChannelRequested", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui)),
                    "Wrong-channel alternate surfaces must be rejected before mutating the input callback.");
                report.wrongChannelAlternatesRejected++;
            }
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
                    Vector2 point = camera.WorldToScreenPoint(rect.TransformPoint(local));
                    // The visible lane can grow over its own transparent margin during feedback.
                    bool overlap = GreedLastPrototypeKeyboardCheck.VerifyLaneInput(ui, eventSystem, margin, point, i,
                        alternateSurface: lane);
                    if (overlap) overlapInputs++;
                    else marginInputs++;
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
            bool runActive = Read<bool>("runModeActive");
            GreedLastRunSnapshot snapshot = Read<GreedLastRunSnapshot>("latestRunSnapshot");
            if (!runActive || snapshot.Paused || snapshot.CountdownActive || snapshot.ChoiceActive || snapshot.Stopped)
            {
                foreach (string field in new[] { "trapMarker", "trapIllustration", "laneDangerBand", "trapGlow",
                    "trapCorridorShadow", "trapStrike", "trapContactCue", "goodTimingBand", "successTimingBand",
                    "centerTimingBand", "rhythmBreakLine", "inputTimingTraceMarker",
                    "resolvedTrapAfterimage", "resolvedTrapImpact", "feedbackFlash" })
                {
                    Graphic part = Read<Graphic>(field);
                    Require(part == null || !part.isActiveAndEnabled || part.color.a == 0f,
                        currentFrame.Name + ": non-live screens must not retain timing feedback: " + field);
                }
                foreach (string field in new[] { "runMotionLines", "runnerFootfallPips", "runnerActionTrails" })
                    foreach (Image part in Read<Image[]>(field))
                        Require(part == null || !part.isActiveAndEnabled || part.color.a == 0f,
                            currentFrame.Name + ": non-live screens must not retain motion feedback: " + field);
            }
            if (!runActive)
            {
                foreach (Image ripple in Read<Image[]>("laneInputRipples"))
                {
                    Require(ripple.color.a == 0f,
                        currentFrame.Name + ": non-run screens must not retain lane input ripples.");
                }
            }
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
                    currentFrame.Name + ": feedback text is vertically clipped: " + label.name
                        + " / needed " + label.preferredHeight + " / available " + label.rectTransform.rect.height);
                if (gauges.activeInHierarchy)
                {
                    Bounds textBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, label.rectTransform);
                    Bounds gaugeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, gauges.transform);
                    Require(!textBounds.Intersects(gaugeBounds),
                        currentFrame.Name + ": feedback overlaps the health/focus gauges: " + label.name
                            + " / text " + textBounds + " / gauges " + gaugeBounds);
                }
                count++;
            }
            Text guide = Read<Text>("timingGuideText");
            foreach (string field in new[] { "activeActionCueText", "judgementFeedbackText" })
            {
                Text label = Read<Text>(field);
                if (!HasVisibleText(guide) || !HasVisibleText(label)) continue;
                Bounds guideGlyphs = GetTextGlyphBounds(root, guide);
                Bounds feedbackGlyphs = GetTextGlyphBounds(root, label);
                Require(!guideGlyphs.Intersects(feedbackGlyphs),
                    currentFrame.Name + ": timing and feedback glyphs overlap: " + label.name
                        + " / guide " + guideGlyphs + " / feedback " + feedbackGlyphs);
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
            Require(GreedLastPrototypeFontCheck.IsTextComplete(label, out int visible, out int full, out int fontSize),
                currentFrame.Name + ": text is vertically truncated: " + label.name + " / visible " + visible
                    + " / full " + full + " / fitted font " + fontSize + " / text " + label.text.Replace('\n', '|'));
            textCompletenessCalls++;
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
                        + " / bounds " + rect + " / glyph " + point + " / font " + label.fontSize
                        + " / text " + label.text.Replace('\n', '|'));
            }
        }

        private static void WriteCaptureReport(bool succeeded, string error)
        {
            Directory.CreateDirectory(CaptureRoot);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.succeeded = succeeded;
            report.error = error;
            report.archivedReport = "report-" + (report.menuScrollInputOnly ? "menu-scroll-input-" : report.choicePreviewOnly && report.liveFocusPreviewOnly ? "reward-guidance-" : report.goodFocusPreviewOnly ? "good-focus-" : report.resolvedBonusPreviewOnly ? "resolved-bonus-" : report.liveFocusPreviewOnly ? "live-focus-" : report.choicePreviewOnly ? "choice-reward-preview-" : report.recoveryPreviewOnly ? "recovery-preview-" : report.rewardPreviewOnly ? "live-reward-preview-" : report.preparationPreviewOnly ? "preparation-preview-" : report.lowHealthInputOnly ? "low-health-input-" : report.textClippingRejectionOnly ? "text-rejection-" : report.progressBoundaryOnly ? "progress-boundary-"
                : report.scoreBoundaryOnly ? "score-boundary-" : report.pointerLoopIncluded ? "full-" : "layout-")
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";
            string json = JsonUtility.ToJson(report, true);
            PublishReportFiles(CaptureRoot, report.archivedReport, json);
        }

        private static void PublishReportFiles(string directory, string archiveName, string json)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            WriteNewEvidence(Path.Combine(directory, archiveName), bytes);
            ReplaceLatestReport(Path.Combine(directory, "latest-report.json"), bytes);
        }

        private static void ReplaceLatestReport(string path, byte[] bytes, Action<int> beforeRetry = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            bool temporaryOwned = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    temporaryOwned = true;
                    stream.Write(bytes, 0, bytes.Length);
                }
                // Never truncate the old index before the replacement is fully written and closed.
                if (File.Exists(path))
                {
                    for (int attempt = 0; ; attempt++)
                    {
                        try { File.Replace(temporary, path, null); break; }
                        catch (IOException error) when (attempt < 2 && IsRetryableIndexLock(error)
                            && File.Exists(temporary) && File.Exists(path))
                        {
                            if (beforeRetry != null) beforeRetry(attempt + 1);
                            else System.Threading.Thread.Sleep(25 * (attempt + 1));
                        }
                    }
                }
                else File.Move(temporary, path);
                temporaryOwned = false;
            }
            finally
            {
                if (temporaryOwned) File.Delete(temporary);
            }
        }

        private static bool IsRetryableIndexLock(IOException error)
        {
            int code = error.HResult & 0xffff;
            return code == 32 || code == 33 || code == 1175;
        }

        private static void FinishCapture(bool succeeded, string error)
        {
            bool completed = TryWriteAndCleanup(() => WriteCaptureReport(succeeded, error), Cleanup, Debug.LogException);
            if (Application.isBatchMode) EditorApplication.Exit(succeeded && completed ? 0 : 1);
        }

        private static bool TryWriteAndCleanup(Action write, Action cleanup, Action<Exception> reportError)
        {
            bool completed = true;
            try { write(); }
            catch (Exception error) { completed = false; reportError(error); }
            finally
            {
                try { cleanup(); }
                catch (Exception error) { completed = false; reportError(error); }
            }
            return completed;
        }

        [MenuItem("Greed Last/Run Capture Finish Policy Check")]
        public static void VerifyCaptureFinishPolicy()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Capture finish checks require Edit mode.");
            foreach (bool writeFails in new[] { false, true })
            foreach (bool cleanupFails in new[] { false, true })
            {
                var order = new List<string>();
                int failures = 0;
                bool result = TryWriteAndCleanup(() =>
                {
                    order.Add("write");
                    if (writeFails) throw new IOException("Synthetic report-write failure.");
                }, () =>
                {
                    order.Add("cleanup");
                    if (cleanupFails) throw new InvalidOperationException("Synthetic cleanup failure.");
                }, _ => failures++);
                Require(string.Join(",", order) == "write,cleanup" && result == (!writeFails && !cleanupFails)
                    && failures == (writeFails ? 1 : 0) + (cleanupFails ? 1 : 0),
                    "Capture finish must always attempt cleanup and retain failed status for either failure.");
            }
            Debug.Log("Greed capture finish policy passed: four in-memory report/cleanup combinations, cleanup always attempted and either failure retained. No filesystem failure, actual scene teardown or hardware test was simulated by this policy check.");
        }

        [MenuItem("Greed Last/Run Capture Publication Check")]
        public static void VerifyCapturePublication()
        {
            Require(CanRun(), "Capture publication checks require idle Edit mode.");
            string directory = Path.Combine(CaptureRoot, "publication-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff",
                System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(directory);
            var check = new PublicationCheckReport { startedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            string latest = Path.Combine(directory, "latest-report.json");
            const string first = "{\"case\":\"first\",\"caption\":\"확인\"}";
            const string second = "{\"case\":\"second\",\"caption\":\"보존\"}";
            const string third = "{\"case\":\"third\",\"caption\":\"복구\"}";
            bool IsIoRejected(Action operation)
            {
                try { operation(); return false; }
                catch (IOException) { return true; }
                catch (UnauthorizedAccessException) { return true; }
            }
            void RequireLatest(string expected)
            {
                Require(File.ReadAllText(latest) == expected, "Capture publication must retain the complete expected index.");
                Require(Directory.GetFiles(directory, "*.tmp-*", SearchOption.AllDirectories).Length == 0,
                    "Capture publication must remove only its owned temporary file after failure.");
            }
            try
            {
                PublishReportFiles(directory, "report-first.json", first);
                RequireLatest(first);
                Require(File.ReadAllText(Path.Combine(directory, "report-first.json")) == first, "Fresh archive must contain complete UTF-8 text.");
                check.cases.Add("first-publication");

                Require(IsIoRejected(() => PublishReportFiles(directory, "report-first.json", second)), "Duplicate archives must be rejected.");
                RequireLatest(first);
                Require(File.ReadAllText(Path.Combine(directory, "report-first.json")) == first, "Duplicate publication must not overwrite evidence.");
                check.cases.Add("archive-collision");

                PublishReportFiles(directory, "report-second.json", second);
                RequireLatest(second);
                Require(File.ReadAllText(Path.Combine(directory, "report-first.json")) == first, "Replacing the index must preserve earlier archives.");
                check.cases.Add("index-replacement");

                string payload = Path.Combine(directory, "payload.bin");
                WriteNewEvidence(payload, new byte[] { 1, 2, 3 });
                Require(IsIoRejected(() => WriteNewEvidence(payload, new byte[] { 4, 5 }))
                    && Convert.ToBase64String(File.ReadAllBytes(payload)) == "AQID", "Payload collision must retain its original bytes.");
                check.cases.Add("payload-collision");

                bool preparationRejected = false;
                try { ReplaceLatestReport(latest, null); }
                catch (ArgumentNullException) { preparationRejected = true; }
                Require(preparationRejected, "Invalid payload must fail before replacing the previous index.");
                RequireLatest(second);
                check.cases.Add("invalid-payload-preparation");

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    using (var held = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.None))
                        Require(IsIoRejected(() => PublishReportFiles(directory, "report-locked.json", third)), "A Windows exclusive reader must reject index replacement.");
                    RequireLatest(second);
                    Require(File.ReadAllText(Path.Combine(directory, "report-locked.json")) == third, "A failed index update must retain its completed new archive.");
                    check.cases.Add("windows-locked-index");

                    FileStream transient = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.None);
                    try
                    {
                        ReplaceLatestReport(latest, new UTF8Encoding(false).GetBytes(second), retry =>
                        {
                            Require(retry == 1, "Releasing the real exclusive reader must allow the first retry.");
                            check.transientLockRetries++;
                            transient.Dispose();
                            transient = null;
                        });
                    }
                    finally { transient?.Dispose(); }
                    RequireLatest(second);
                    Require(check.transientLockRetries == 1
                        && File.ReadAllText(Path.Combine(directory, "report-first.json")) == first,
                        "Transient lock recovery must preserve the complete index and earlier archive.");
                    check.cases.Add("transient-index-lock-recovery");
                }

                string blocked = Path.Combine(directory, "blocked");
                string obstruction = Path.Combine(blocked, "latest-report.json");
                Directory.CreateDirectory(obstruction);
                string marker = Path.Combine(obstruction, "marker.bin");
                WriteNewEvidence(marker, new byte[] { 7 });
                Require(IsIoRejected(() => PublishReportFiles(blocked, "report-blocked.json", third))
                    && Directory.Exists(obstruction) && File.ReadAllBytes(marker)[0] == 7
                    && File.ReadAllText(Path.Combine(blocked, "report-blocked.json")) == third,
                    "A directory obstruction must be preserved while the completed archive remains available.");
                RequireLatest(second);
                check.cases.Add("directory-obstruction");

                PublishReportFiles(directory, "report-recovered.json", third);
                RequireLatest(third);
                Require(File.ReadAllText(Path.Combine(directory, "report-first.json")) == first
                    && File.ReadAllText(Path.Combine(directory, "report-second.json")) == second, "Recovery must preserve all previous archives.");
                check.cases.Add("fresh-publication-recovery");
                Require(check.cases.Count == (Application.platform == RuntimePlatform.WindowsEditor ? 9 : 7), "All capture publication cases must run.");
                check.succeeded = true;
            }
            catch (Exception error) { check.error = error.ToString(); throw; }
            finally
            {
                check.completedUtc = DateTime.UtcNow.ToString("O");
                WriteNewEvidence(Path.Combine(directory, "publication-check.json"), new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(check, true)));
            }
            Debug.Log("Greed capture publication passed: " + check.cases.Count + " real isolated filesystem cases, preserved archives/index and removed owned temporaries. Evidence: " + directory);
        }

        private static void WriteNewEvidence(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
        }

        private static void PreserveFailedFrame()
        {
            if (!currentFrameRendered || currentFrame == null || target == null) return;
            try
            {
                if (!string.IsNullOrEmpty(report.failedFrameFile)) return;
                report.failedFrameFile = Path.Combine(report.captureDirectory, currentFrame.Name + ".failed.png").Replace('\\', '/');
                RenderTexture previous = RenderTexture.active;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    WriteNewEvidence(Path.Combine(CaptureRoot, report.failedFrameFile), image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            catch (Exception error) { report.failureCaptureError = error.Message; }
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
