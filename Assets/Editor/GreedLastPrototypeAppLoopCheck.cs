using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeAppLoopCheck
    {
        private const string PracticeKey = "GreedLast.Onboarding.PracticeCompleted";
        private const string PointerMenuPath = "Greed Last/Run Rendered Input Loop Check";
        private static bool usePointerModule;
        private static bool useTouch;
        private static int pointerClicks;
        private static int canceledCommandPresses;
        private static Camera pointerCamera;
        private static RenderTexture pointerTarget;
        private static GreedLastRuntimeUi pointerUi;
        private static Vector2Int pointerSize;
        private static Vector4 pointerInsets;
        public static int LastPointerClickCount => pointerClicks;
        public static int LastCanceledCommandCount => canceledCommandPresses;
        public static int LastPointerCaseCount { get; private set; }

        [MenuItem(PointerMenuPath)]
        public static void RunPointerLoop()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !usePointerModule,
                "Pointer-loop checks require an isolated Edit-mode fixture.");
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                "The rendered pointer loop requires a graphics device; do not use -nographics.");
            pointerClicks = 0;
            canceledCommandPresses = 0;
            LastPointerCaseCount = 0;
            usePointerModule = true;
            var errors = new List<string>();
            Application.LogCallback collectErrors = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(message);
            };
            Application.logMessageReceived += collectErrors;
            try
            {
                RunPointerCase(false, 1280, 720, Vector4.zero);
                RunPointerCase(true, 960, 540, new Vector4(12f, 16f, 32f, 8f));
                RunPointerCase(true, 2340, 1080, new Vector4(120f, 36f, 24f, 18f));
                RunPointerCase(true, 2340, 1080, new Vector4(24f, 36f, 120f, 18f));
                RunPointerCase(true, 1024, 768, Vector4.zero);
            }
            finally
            {
                Application.logMessageReceived -= collectErrors;
                usePointerModule = false;
                useTouch = false;
            }
            Require(errors.Count == 0, "The rendered input loop emitted Unity errors: " + string.Join(" | ", errors));
            Debug.Log("Greed Last full pointer loop passed: " + pointerClicks
                + " actual-module mouse/touch clicks in " + LastPointerCaseCount
                + " clean-view layouts; " + canceledCommandPresses
                + " interrupted app-bound commands canceled across practice, clear, save, preparation, two-use depletion, records and confirmations. Synthetic input and isolated backend only.");
        }

        [MenuItem(PointerMenuPath, true)]
        private static bool CanRunPointerLoop() => !usePointerModule && !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void RunPointerCase(bool touch, int width, int height, Vector4 insets)
        {
            useTouch = touch;
            pointerSize = new Vector2Int(width, height);
            pointerInsets = insets;
            Run();
            LastPointerCaseCount++;
        }

        [MenuItem("Greed Last/Run Suspended Player Input Check")]
        public static void RunSuspendedPlayerInputCheck()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Suspended-input checks require an idle Edit-mode editor.");
            Run();
        }

        [MenuItem("Greed Last/Run Suspended Player Input Check", true)]
        private static bool CanRunSuspendedPlayerInputCheck() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Run()
        {
            Require(!Application.isPlaying, "App-loop checks require Edit mode.");
            bool hadPracticeKey = PlayerPrefs.HasKey(PracticeKey);
            int practiceValue = PlayerPrefs.GetInt(PracticeKey);
            GameObject existingCamera = GameObject.Find("GreedLastCamera");
            GameObject existingEvents = GameObject.Find("GreedLastEventSystem");
            GameObject host = new GameObject("GreedLastAppLoopCheck");
            GameObject canvasRoot = null;
            try
            {
                // Edit mode leaves Awake/Start inactive; inject a non-persisting backend before exercising callbacks.
                GreedLastApp app = host.AddComponent<GreedLastApp>();
                Require(Read<GreedLastMockBackend>(app, "backend") == null, "The isolated app must not boot its persistent backend.");
                GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                Require(ui.UsePlayerRunControls, "A new runtime UI must default to player controls without F11 or a saved preference.");
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                Require(ui.UsePlayerRunControls, "UI initialization must retain the clean player default.");
                canvasRoot = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                if (usePointerModule)
                {
                    pointerUi = ui;
                    CreatePointerViewport(canvasRoot.GetComponent<Canvas>());
                }
                var gate = new GreedLastRequestGate();
                var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                var state = new GreedLastStateMachine(gate);
                var core = new GreedLastRunCore();
                Write(app, "requestGate", gate);
                Write(app, "backend", backend);
                Write(app, "stateMachine", state);
                Write(app, "runCore", core);
                Write(app, "runtimeUi", ui);
                Bind(ui, app, "practiceRunRequested", "RequestPracticeRun");
                Bind(ui, app, "normalRunRequested", "RequestNormalRun");
                Bind(ui, app, "saveLoadoutRequested", "RequestSaveLoadout");
                Bind(ui, app, "infiniteRunRequested", "RequestInfiniteRun");
                Bind(ui, app, "infiniteRecordRequested", "RequestInfiniteRecordBoard");
                Bind(ui, app, "nextPatternRequested", "RequestNextRunPattern");
                Bind(ui, app, "returnLobbyRequested", "RequestReturnToLobby");
                Bind(ui, app, "pauseRequested", "RequestToggleRunPause");
                Bind(ui, app, "saveSlotDetailRequested", "RequestShowSaveLoadoutDetail");
                Bind(ui, app, "saveSlotRenameRequested", "RequestRenameSaveLoadoutSlot");
                Bind(ui, app, "saveSlotDeleteRequested", "RequestDeleteSaveLoadoutSlot");
                Write(ui, "runChannelRequested", Method(app, "HandleRunChannelInput").CreateDelegate(typeof(Action<GreedLastRunChannel>), app));
                Write(ui, "timingOffsetRequested", Method(app, "RequestTimingOffsetAdjust").CreateDelegate(typeof(Action<int>), app));
                ui.ViewportChanged += (Action)Method(app, "HandleViewportChanged").CreateDelegate(typeof(Action), app);
                ui.RotationTiltPreferenceChanged += (Action<bool>)Method(app, "HandleRotationTiltPreferenceChanged").CreateDelegate(typeof(Action<bool>), app);
                state.StateChanged += ui.Render;
                core.SnapshotChanged += (Action<GreedLastRunSnapshot>)Method(app, "HandleRunSnapshotChanged")
                    .CreateDelegate(typeof(Action<GreedLastRunSnapshot>), app);
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                PlayerPrefs.SetInt(PracticeKey, 0);
                VerifyBootAndReconnect(app, ui, backend, state, gate);
                VerifyDefaultPlayerEntry(ui);
                VerifyMenuInputRippleCleanup(ui, backend, state);

                VerifySlowRunPresentation(app, backend, state, core);
                VerifyInterruptedCommandProtection(app, ui, backend, state, core);
                VerifySuspendedPlayerInput(app, ui, backend, state, core, () => run);

                Click(ui, "practiceRunButton");
                Require(run.PracticeMode && state.CurrentState == GreedLastScreenState.RunCoreTest,
                    "The actual lobby practice callback must open a practice run.");
                AssertSyncCannotLeaveScreen(app, backend, state, gate);
                RunToCompletion(core, () => run);
                Require(run.Stopped && run.SuccessCount == 3 && !backend.HasSaveLoadoutCandidate,
                    "A perfect practice must complete three axes without creating a save candidate.");
                Require(Read<Text>(ui, "titleText").text == "기초연습 완료",
                    "The complete basic course must retain its original completion title.");
                Require(Read<Button>(ui, "nextPatternButton").GetComponentInChildren<Text>().text == "일반 런으로",
                    "The first perfect practice result must offer direct normal-run entry.");
                Click(ui, "nextPatternButton");
                Require(!run.PracticeMode && run.ChoiceActive && PlayerPrefs.GetInt(PracticeKey) == 1,
                    "The practice result callback must mark onboarding and open normal starting choices.");
                AssertSyncCannotLeaveScreen(app, backend, state, gate);
                Require(!Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "The former result button must not remain actionable over the new starting choice.");

                RunToCompletion(core, () => run);
                Require(run.EscapeSucceeded && run.SaveEligible, "The app-connected normal run must reach a saveable escape.");
                AssertSyncCannotLeaveScreen(app, backend, state, gate);
                Click(ui, "nextPatternButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "The actual clear action must open explicit slot selection with one candidate.");
                Require(!Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "Saving must remain locked until a slot is explicitly chosen.");
                Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.None });
                Require(Read<bool>(app, "saveDraftRequiresExplicitSlotChoice"),
                    "An invalid channel must not implicitly select S3 and unlock saving.");
                Click(Read<Button[]>(ui, "laneButtons")[2]);
                VerifyHeldManagementViewSwitch(app, ui, backend, state, "nextPatternButton");
                Click(ui, "nextPatternButton");
                GreedLastLobbySnapshot saved = backend.LoadLobby();
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                    && saved.SaveSlotOccupied[2] && !saved.SaveSlotOccupied[0] && !saved.SaveSlotOccupied[1],
                    "The actual S3 button and confirmation must consume the candidate only into S3.");
                VerifyBootAndReconnect(app, ui, backend, state, gate);
                VerifyDefaultPlayerEntry(ui);

                VerifySlowPreparationPresentation(app, backend, state);
                VerifySlowConfirmationPresentation(app, backend, state);

                for (int use = 0; use < 2; use++)
                {
                    Click(ui, "infiniteButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady, "The lobby infinite action must open preparation.");
                    AssertSyncCannotLeaveScreen(app, backend, state, gate);
                    string before = backend.LoadLobby().SaveSlotLabels[2];
                    float entryGuardDeadline = Read<float>(app, "infinitePreparationInputBlockUntil");
                    float beforeEntryPress = Time.realtimeSinceStartup;
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Require(Read<int>(app, "infinitePreparationStep") == 0,
                        "The entry tap must not carry through into a preparation choice."
                            + $" touch={useTouch}, viewport={pointerSize}, until={entryGuardDeadline:F4},"
                            + $" beforeLayout={beforeEntryPress:F4}, afterPress={Time.realtimeSinceStartup:F4}.");
                    FinishPreparationTransition(app);
                    Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.None });
                    Require(Read<int>(app, "infinitePreparationStep") == 0,
                        "An invalid channel must not silently select the left preparation option.");
                    if (use == 0)
                    {
                        Click(Read<Button[]>(ui, "laneButtons")[0]);
                        Click(ui, "returnLobbyButton");
                        Require(backend.LoadLobby().SaveSlotLabels[2] == before,
                            "Leaving gift preparation must not consume the selected loadout.");
                        RectTransform restoredSave = Read<Button>(ui, "saveLoadoutButton").GetComponent<RectTransform>();
                        Require(Mathf.Approximately(restoredSave.anchorMin.y, 0.29f) && Mathf.Approximately(restoredSave.sizeDelta.y, 92f),
                            "Leaving preparation must restore the save command above infinite entry, not retain the preparation position.");
                        Click(ui, "infiniteButton");
                        FinishPreparationTransition(app);
                    }
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Require(Read<int>(app, "infinitePreparationStep") == 1,
                        "A rapid second tap must not choose a relic before its choices can be read.");
                    FinishPreparationTransition(app);
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Require(Read<int>(app, "infinitePreparationStep") == 2,
                        "The gift and relic lane presses must both resolve before start confirmation.");
                    Require(backend.LoadLobby().SaveSlotLabels[2] == before,
                        "Gift and relic preparation callbacks must not consume a use before start confirmation.");
                    Click(ui, "nextPatternButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                        && backend.LoadLobby().SaveSlotLabels[2] == before,
                        "The relic selection must not spill into start confirmation or consume a use.");
                    FinishPreparationTransition(app);
                    if (use == 0) VerifyPreparationReselection(app, ui, backend, before);
                    VerifyHeldManagementViewSwitch(app, ui, backend, state, "nextPatternButton");
                    Click(ui, "nextPatternButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && run.InfiniteMode,
                        "Start confirmation must enter infinite mode through the app, not merely render its screen.");
                    AssertSyncCannotLeaveScreen(app, backend, state, gate);
                    GreedLastLobbySnapshot afterStart = backend.LoadLobby();
                    Require(afterStart.SaveSlotOccupied[2] == (use == 0),
                        "S3 must remain occupied after the first start and auto-empty on the second start.");
                    float clock = Time.realtimeSinceStartup;
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                    Require(run.ActivePattern && !run.Stopped, "An emptied source slot must not stop its active run.");
                    core.HandleInput(run.Pattern.Channel, clock + run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
                    core.ForceStopInfiniteRunForTest();
                    Click(ui, "nextPatternButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady,
                        "The actual infinite result action must settle its record and return to preparation.");
                    Click(ui, "returnLobbyButton");
                }
                Require(!backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "A twice-used source must reject a third use after the complete callback loop.");
                Click(ui, "infiniteRecordButton");
                Require(state.CurrentState == GreedLastScreenState.InfiniteRecordBoard,
                    "The resulting records must remain reachable through the actual lobby button.");
                AssertSyncCannotLeaveScreen(app, backend, state, gate);
                Click(ui, "returnLobbyButton");
                Click(ui, "normalRunButton");
                Require(!run.InfiniteMode && !run.Stopped && run.ChoiceActive,
                    "After depletion and record review the real lobby action must start the refill normal run.");
                Require(string.IsNullOrEmpty(run.ReviewTargetText),
                    "A fresh refill after unrelated record review must not inherit a stale goal.");
                RunToFailure(core, () => run);
                Require(run.RefillRun && !run.EscapeSucceeded && !run.SaveEligible,
                    "A failed refill must remain a real unsaveable normal attempt.");
                Click(ui, "returnLobbyButton");
                string refillReview = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(state.CurrentDetail);
                Require(refillReview.StartsWith("보충 복기 재도전", StringComparison.Ordinal)
                    && refillReview.Contains("재도전 목표:", StringComparison.Ordinal),
                    "The actual failed refill result must supply a review goal for its next entry.");
                Click(ui, "normalRunButton");
                Require(run.RefillRun && run.ChoiceActive && !run.Stopped
                    && run.JudgementText.Contains(refillReview, StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(run.ReviewTargetText),
                    "The real refill re-entry must retain the failed attempt's review and active goal.");
                RunToFailure(core, () => run);
                Click(ui, "returnLobbyButton");
                refillReview = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(state.CurrentDetail);
                Require(!string.IsNullOrEmpty(refillReview), "A repeated real refill failure must retain review context.");
                Click(ui, "infiniteButton");
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                    && state.CurrentDetail.Contains(refillReview, StringComparison.Ordinal)
                    && !backend.HasSaveLoadoutCandidate,
                    "The recommended infinite-preparation path must present the pending refill review without consuming anything.");
                FinishPreparationTransition(app);
                Click(ui, "nextPatternButton");
                Require(run.RefillRun && run.ChoiceActive && !run.Stopped
                    && run.JudgementText.Contains(refillReview, StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(run.ReviewTargetText)
                    && string.IsNullOrEmpty(Read<string>(app, "pendingRefillRunReviewContext")),
                    "Starting the refill from empty infinite preparation must transfer and clear its pending review.");
                Require(Array.TrueForAll(backend.LoadLobby().SaveSlotOccupied, occupied => !occupied),
                    "Failure and both refill re-entry paths must not recreate or consume any saved slot.");
                VerifyRefillAbandonReentry(app, ui, backend, state, core, () => run);
                VerifyRefillManagementCancellation(app, ui, backend, state, core, () => run);
                VerifyRefillSaveAndReuse(app, ui, backend, state, core, () => run);
                RunToCompletion(core, () => run);
                Click(ui, "nextPatternButton");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "The first skip-save action must request confirmation while preserving the candidate.");
                AssertSyncCannotLeaveScreen(app, backend, state, gate);
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "A rapid second skip-save tap must preserve the candidate while confirmation appears.");
                FinishSaveConfirmation(app);
                VerifyHeldManagementViewSwitch(app, ui, backend, state, "returnLobbyButton");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                    "Confirming skip-save must discard the candidate and return to the lobby.");
                VerifySavedSlotConfirmations(app, ui, backend, state);
                VerifyPracticeReviewEntry(app, ui, backend, state, core, () => run);
                VerifyFocusedPracticeCompletion(ui, backend, state, core, () => run);
                VerifyPracticeCalibrationLoop(ui, backend, state, core, () => run);
                VerifyPlayerPrimaryControls(app, ui, backend, state, core, () => run);
                VerifyAppPauseLifecycle(app, ui, backend, state, core, () => run);
                VerifyGameplayLaneOutcomes(app, ui, backend, state, core, () => run);
                VerifyRotationDragLifecycle(app, ui, backend, state, core, () => run);
                Debug.Log("Greed Last app callback loop passed: clean default entry -> practice -> normal escape -> S3 -> guarded preparation and reselection -> preparation cancel -> two infinite uses -> records -> refill failure/abandon review through both lobby actions -> refill clear/save/reuse across all three slots -> guarded skip, rename, delete and overwrite confirmations. Held F11 transitions cancel management commands without changing candidates, slots or uses.");
            }
            finally
            {
                if (hadPracticeKey) PlayerPrefs.SetInt(PracticeKey, practiceValue);
                else PlayerPrefs.DeleteKey(PracticeKey);
                PlayerPrefs.Save();
                GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                if (pointerCamera != null) pointerCamera.targetTexture = null;
                if (pointerTarget != null)
                {
                    pointerTarget.Release();
                    UnityEngine.Object.DestroyImmediate(pointerTarget);
                }
                if (pointerCamera != null) UnityEngine.Object.DestroyImmediate(pointerCamera.gameObject);
                pointerCamera = null;
                pointerTarget = null;
                pointerUi = null;
                UnityEngine.Object.DestroyImmediate(host);
                if (canvasRoot != null) UnityEngine.Object.DestroyImmediate(canvasRoot);
                DestroyCreated("GreedLastCamera", existingCamera);
                DestroyCreated("GreedLastEventSystem", existingEvents);
            }
        }

        private static void VerifyDefaultPlayerEntry(GreedLastRuntimeUi ui)
        {
            Require(ui.UsePlayerRunControls, "Boot and saved-slot reload must retain the default player view without F11.");
            foreach (CanvasGroup group in Read<List<CanvasGroup>>(ui, "devCleanViewGroups"))
                Require(group.alpha == 0f && !group.interactable && !group.blocksRaycasts,
                    "Default entry must hide and disable development controls: " + group.name);
            Require(Read<Button>(ui, "practiceRunButton").IsInteractable()
                && Read<Button>(ui, "normalRunButton").IsInteractable(),
                "Clean entry must keep the actual practice and normal commands usable.");
        }

        private static void VerifyHeldManagementViewSwitch(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, string field)
        {
            Button button = Read<Button>(ui, field);
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(),
                "Mode-switch checks need an enabled management command: " + field);
            bool originalView = ui.UsePlayerRunControls;
            GreedLastScreenState screen = state.CurrentState;
            GreedLastRunSnapshot run = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
            GreedLastLobbySnapshot lobby = backend.LoadSelectedSaveSlotView();
            bool candidate = backend.HasSaveLoadoutCandidate;
            var slots = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
            var uses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
            EventSystem system = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            Require(system != null, "Held management commands require the isolated event system.");
            int calls = 0;
            UnityEngine.Events.UnityAction countClick = () => calls++;
            button.onClick.AddListener(countClick);
            try
            {
                for (int direction = 0; direction < 2; direction++)
                {
                    bool beforeView = ui.UsePlayerRunControls;
                    void SwitchView()
                    {
                        ui.ToggleDevCleanView();
                        if (usePointerModule) RefreshPointerViewport(button);
                    }
                    if (usePointerModule)
                    {
                        RefreshPointerViewport(button);
                        GreedLastPrototypeKeyboardCheck.ClickOnce(system, button, useTouch, whileHeld: SwitchView);
                        canceledCommandPresses++;
                    }
                    else
                    {
                        var press = new PointerEventData(system)
                            { button = PointerEventData.InputButton.Left, eligibleForClick = true };
                        ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerDownHandler);
                        SwitchView();
                        ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerUpHandler);
                        if (press.eligibleForClick)
                            ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerClickHandler);
                    }
                    Require(calls == 0 && ui.UsePlayerRunControls != beforeView
                        && state.CurrentState == screen && backend.HasSaveLoadoutCandidate == candidate
                        && Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot").Equals(run)
                        && backend.LoadSelectedSaveSlotView().SelectedSaveSlotIndex == lobby.SelectedSaveSlotIndex,
                        "A held F11 transition must not execute or leave its management screen: " + field);
                    for (int slot = 0; slot < slots.Length; slot++)
                        Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(slots[slot])
                            && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == uses[slot],
                            "A held mode switch must preserve every saved record and remaining use: " + field);
                }
            }
            finally
            {
                button.onClick.RemoveListener(countClick);
                if (ui.UsePlayerRunControls != originalView) ui.ToggleDevCleanView();
            }
        }

        private static void VerifyBootAndReconnect(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRequestGate gate)
        {
            GreedLastLobbySnapshot original = backend.LoadLobby();
            var visited = new List<GreedLastScreenState>();
            Action<GreedLastStateSnapshot> observe = snapshot => visited.Add(snapshot.State);
            state.StateChanged += observe;
            try
            {
                AdvanceRoutine(app, "BootThenConnect", () =>
                {
                    Require(gate.Busy, "Every boot wait must retain its request token.");
                    AssertLobbyCommandsLocked(app, ui, state, gate);
                    IEnumerator duplicate = (IEnumerator)Method(app, "BootThenConnect").Invoke(app, null);
                    Require(!duplicate.MoveNext(), "A second boot must not replace an active request.");
                });
                Require(visited.Count == 4 && visited[0] == GreedLastScreenState.BootInit
                    && visited[1] == GreedLastScreenState.ConnectChecking
                    && visited[2] == GreedLastScreenState.LobbyLoading
                    && visited[3] == GreedLastScreenState.LobbyReady && !gate.Busy,
                    "The real boot routines must visit title, connection, lobby loading and ready exactly once.");
                string[] fields = { "online", "maintenance", "versionMatched", "sessionValid" };
                var reasons = new[] { GreedLastConnectBlockReason.NetworkUnavailable, GreedLastConnectBlockReason.Maintenance,
                    GreedLastConnectBlockReason.VersionMismatch, GreedLastConnectBlockReason.SessionInvalid };
                for (int test = 0; test < fields.Length; test++)
                {
                    bool healthy = fields[test] != "maintenance";
                    Write(backend, fields[test], !healthy);
                    try
                    {
                        AdvanceRoutine(app, "ConnectThenLoadLobby");
                        Require(state.CurrentState == GreedLastScreenState.ConnectBlocked && state.BlockReason == reasons[test]
                            && !gate.Busy && Read<Button>(ui, "retryButton").IsInteractable(),
                            "A failed connection must release its token and offer a retry: " + reasons[test]);
                        AssertLobbyCommandsLocked(app, ui, state, gate);
                        state.ShowLocalNotice("연결 상태를 다시 확인해야 합니다.");
                        Require(!state.CoreActionsEnabled && Read<Button>(ui, "retryButton").gameObject.activeSelf,
                            "A notice must preserve a blocked lobby and its retry route.");
                        if (test == 1)
                        {
                            Write(backend, "online", false);
                            try
                            {
                                AdvanceRoutine(app, "RetrySyncRoutine");
                                Require(state.BlockReason == GreedLastConnectBlockReason.NetworkUnavailable,
                                    "A new retry failure must replace the earlier maintenance reason.");
                            }
                            finally { Write(backend, "online", true); }
                        }
                        AdvanceRoutine(app, "RetrySyncRoutine");
                        Require(state.CurrentState == GreedLastScreenState.ReSyncPending && !gate.Busy
                            && state.BlockReason == reasons[test],
                            "A failed retry must remain blocked without leaking its request token.");
                        AssertLobbyCommandsLocked(app, ui, state, gate);
                    }
                    finally { Write(backend, fields[test], healthy); }
                    AdvanceRoutine(app, "RetrySyncRoutine", () => AssertLobbyCommandsLocked(app, ui, state, gate));
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady && !gate.Busy
                        && state.BlockReason == GreedLastConnectBlockReason.None,
                        "Recovery must reopen the lobby without the earlier connection block.");
                }

                // A warning must carry the freshly loaded slots, not the previous screen's cache.
                state.SetLobbyReady(new GreedLastMockBackend(false, false).LoadLobby());
                Write(backend, "auxiliaryWarning", true);
                try
                {
                    AdvanceRoutine(app, "LoadLobby");
                    Require(state.CurrentState == GreedLastScreenState.ReSyncPending && !gate.Busy
                        && Read<Button>(ui, "normalRunButton").IsInteractable(),
                        "An auxiliary-only warning must keep the loaded lobby usable.");
                    Require(state.LobbySnapshot.SaveSlotOccupied.Length == original.SaveSlotOccupied.Length,
                        "A warning lobby must retain its loaded slot snapshot.");
                    for (int slot = 0; slot < original.SaveSlotOccupied.Length; slot++)
                        Require(state.LobbySnapshot.SaveSlotOccupied[slot] == original.SaveSlotOccupied[slot]
                            && state.LobbySnapshot.SaveSlotLabels[slot] == original.SaveSlotLabels[slot],
                            "An auxiliary warning must not replace a loaded saved slot with stale lobby data.");
                    AdvanceRoutine(app, "RetrySyncRoutine", () => AssertLobbyCommandsLocked(app, ui, state, gate));
                    Require(state.CurrentState == GreedLastScreenState.ReSyncPending && state.CoreActionsEnabled
                        && state.BlockReason == GreedLastConnectBlockReason.None,
                        "A repeated auxiliary warning must preserve usable data without retaining an old connection error.");
                    Require(gate.TryBegin(GreedLastRequestKind.LobbyAction, out GreedLastRequestToken warningToken),
                        "The warning-only lobby must permit a fresh action after the request ends.");
                    Require(!state.CoreActionsEnabled, "The action lock must also reflect a newly active request.");
                    gate.Complete(warningToken);
                    Require(state.CoreActionsEnabled, "Completing an action must restore the warning lobby's allowed state.");
                    state.ShowLocalNotice("저장 정보 확인 완료");
                    Require(state.CoreActionsEnabled && Read<Button>(ui, "normalRunButton").IsInteractable()
                        && Read<Button>(ui, "retryButton").gameObject.activeSelf,
                        "A local notice must preserve a usable warning lobby and its retry option.");
                    string body = Read<Text>(ui, "detailText").text;
                    Require(!body.Contains("다시 열립니다"), "A usable warning must not claim its active commands are locked.");
                }
                finally { Write(backend, "auxiliaryWarning", false); }
                AdvanceRoutine(app, "RetrySyncRoutine");
                VerifyLobbyForegroundRecovery(app, ui, backend, state, gate);
                VerifyConnectionLifecycleEdges(app, ui, backend, state, gate);
                for (int slot = 0; slot < original.SaveSlotLabels.Length; slot++)
                    Require(backend.LoadLobby().SaveSlotLabels[slot] == original.SaveSlotLabels[slot],
                        "Boot failures and retries must not consume or change a saved loadout.");
                Debug.Log("Greed Last boot/reconnect checks passed: ordered boot, duplicate request lock, four simulated failure reasons, locked app commands, fresh warning snapshot and preserved saved slots. Coroutine waits are advanced without real network traffic.");
            }
            finally { state.StateChanged -= observe; }
        }

        private static void VerifyLobbyForegroundRecovery(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRequestGate gate)
        {
            GreedLastLobbySnapshot before = backend.LoadLobby();
            GreedLastRunSnapshot runBefore = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Method(app, "OnApplicationPause").Invoke(app, new object[] { true });
                Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                Require(state.CurrentState == GreedLastScreenState.ReSyncPending && !state.CoreActionsEnabled && !gate.Busy,
                    "Returning to an idle lobby must wait for explicit sync without starting a run or leaving a busy token.");
                AssertLobbyCommandsLocked(app, ui, state, gate);
                Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                Require(state.CurrentState == GreedLastScreenState.ReSyncPending && !gate.Busy,
                    "A duplicate resume callback must not invent another lobby request.");
                AdvanceRoutine(app, "RetrySyncRoutine", () =>
                {
                    int request = gate.ActiveRequestId;
                    Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                    Method(app, "OnApplicationFocus").Invoke(app, new object[] { true });
                    Require(gate.ActiveRequestId == request && gate.Busy && !state.CoreActionsEnabled,
                        "Foreground callbacks during sync must not replace its request or reopen actions early.");
                    AssertLobbyCommandsLocked(app, ui, state, gate);
                });
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && state.CoreActionsEnabled,
                    "An explicit successful sync must restore the idle lobby after every resume.");
                Method(app, "OnApplicationFocus").Invoke(app, new object[] { false });
                Method(app, "OnApplicationFocus").Invoke(app, new object[] { true });
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !gate.Busy,
                    "Desktop focus-only changes must not force the mobile suspension sync flow.");
                GreedLastLobbySnapshot after = state.LobbySnapshot;
                Require(after.SelectedSaveSlotIndex == before.SelectedSaveSlotIndex
                    && after.SaveLoadoutCandidateAvailable == before.SaveLoadoutCandidateAvailable,
                    "Resume sync must preserve the selected slot and candidate availability.");
                for (int slot = 0; slot < before.SaveSlotLabels.Length; slot++)
                    Require(after.SaveSlotLabels[slot] == before.SaveSlotLabels[slot]
                        && after.SaveSlotOccupied[slot] == before.SaveSlotOccupied[slot],
                        "Foreground checks must not spend or replace the player's saved slots.");
                Require(Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot").Equals(runBefore),
                    "Lobby background/foreground callbacks must leave the previous run snapshot untouched.");
            }
            Debug.Log("Greed Last lobby foreground checks passed: three suspension/retry cycles, duplicate resume, focus-only return, busy action isolation and selected-slot/run preservation. Callbacks are simulated.");
        }

        private static void VerifyConnectionLifecycleEdges(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRequestGate gate)
        {
            Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
            Require(state.CurrentState == GreedLastScreenState.LobbyReady && state.CoreActionsEnabled,
                "A duplicate foreground notification after successful sync must not lock the lobby again.");
            Write(backend, "auxiliaryWarning", true);
            try
            {
                AdvanceRoutine(app, "LoadLobby");
                Method(app, "OnApplicationPause").Invoke(app, new object[] { true });
                Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                Require(state.CurrentState == GreedLastScreenState.ReSyncPending && !state.CoreActionsEnabled,
                    "An actionable warning lobby must request fresh data after real app suspension, just like a ready lobby.");
                AssertLobbyCommandsLocked(app, ui, state, gate);
                AdvanceRoutine(app, "RetrySyncRoutine");
                Require(state.CurrentState == GreedLastScreenState.ReSyncPending && state.CoreActionsEnabled,
                    "A successful foreground retry must restore the usable warning lobby.");
                Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                Require(state.CoreActionsEnabled, "Duplicate foreground notification must not relock a restored warning lobby.");
                Click(ui, "practiceRunButton");
                Require(state.CurrentState == GreedLastScreenState.RunCoreTest
                    && Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot").PracticeMode,
                    "An actionable warning lobby must still start practice through its real button.");
                AssertSyncCannotLeaveScreen(app, backend, state, gate);
                if (!Read<Button>(ui, "returnLobbyButton").gameObject.activeInHierarchy)
                    Click(ui, "pauseButton");
                Click(ui, "returnLobbyButton");
            }
            finally { Write(backend, "auxiliaryWarning", false); }
            AdvanceRoutine(app, "RetrySyncRoutine");
            AdvanceRoutine(app, "BootThenConnect", () =>
            {
                GreedLastScreenState waitingState = state.CurrentState;
                int request = gate.ActiveRequestId;
                Method(app, "OnApplicationPause").Invoke(app, new object[] { true });
                Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                Method(app, "RequestRetrySync").Invoke(app, null);
                Method(app, "ToggleOfflineForEditorCheck").Invoke(app, null);
                Require(gate.ActiveRequestId == request && gate.Busy && state.CurrentState == waitingState,
                    "Suspension or a stale connection command during boot must not replace its active request.");
                Require(backend.CheckConnect().Success, "A connection toggle during boot must not mutate the backend.");
                AssertLobbyCommandsLocked(app, ui, state, gate);
            });
            Require(state.CurrentState == GreedLastScreenState.LobbyReady && !gate.Busy,
                "Boot must finish once after suspension in each of its waiting stages.");
            Debug.Log("Greed Last connection lifecycle edges passed: duplicate foreground after recovery, actionable warning suspension, and background/connection commands during all boot waits. Simulated callbacks only.");
        }

        private static void AssertSyncCannotLeaveScreen(GreedLastApp app, GreedLastMockBackend backend,
            GreedLastStateMachine state, GreedLastRequestGate gate)
        {
            GreedLastScreenState before = state.CurrentState;
            GreedLastRunSnapshot runBefore = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
            GreedLastLobbySnapshot lobbyBefore = backend.LoadLobby();
            int request = gate.ActiveRequestId;
            IEnumerator retry = (IEnumerator)Method(app, "RetrySyncRoutine").Invoke(app, null);
            try { Require(!retry.MoveNext(), "A stale sync routine must not abandon the current screen: " + before); }
            finally { (retry as IDisposable)?.Dispose(); }
            Method(app, "RequestRetrySync").Invoke(app, null);
            Method(app, "ToggleOfflineForEditorCheck").Invoke(app, null);
            Require(state.CurrentState == before && gate.ActiveRequestId == request && !gate.Busy
                && backend.CheckConnect().Success && Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot").Equals(runBefore),
                "Hidden connection callbacks must preserve the active screen, run and backend: " + before);
            GreedLastLobbySnapshot after = backend.LoadLobby();
            Require(after.SaveLoadoutCandidateAvailable == lobbyBefore.SaveLoadoutCandidateAvailable
                && after.SelectedSaveSlotIndex == lobbyBefore.SelectedSaveSlotIndex,
                "A stale retry must not alter the candidate or selected slot.");
            for (int slot = 0; slot < after.SaveSlotLabels.Length; slot++)
                Require(after.SaveSlotLabels[slot] == lobbyBefore.SaveSlotLabels[slot],
                    "A stale retry must not spend a saved loadout.");
        }

        private static void AssertLobbyCommandsLocked(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastStateMachine state, GreedLastRequestGate gate)
        {
            GreedLastScreenState before = state.CurrentState;
            int requestId = gate.ActiveRequestId;
            Require(!Read<Button>(ui, "normalRunButton").IsInteractable(), "A blocked/loading lobby must disable its normal-run button.");
            foreach (string command in new[] { "RequestPracticeRun", "RequestNormalRun", "RequestSaveLoadout",
                "RequestInfiniteRun", "RequestInfiniteRecordBoard", "RequestGrowthBoard", "RequestProfileBoard" })
            {
                Method(app, command).Invoke(app, null);
                Require(state.CurrentState == before && gate.ActiveRequestId == requestId,
                    "A blocked/loading lobby must also reject the app command, not only disable its button: " + command);
            }
        }

        private static void AdvanceRoutine(GreedLastApp app, string name, Action atWait = null)
        {
            int steps = 0;
            void Drain(IEnumerator routine)
            {
                try
                {
                    while (routine.MoveNext())
                    {
                        Require(++steps <= 24, "Boot/retry fixture exceeded its bounded coroutine steps.");
                        if (routine.Current is WaitForSecondsRealtime) atWait?.Invoke();
                        else if (routine.Current is IEnumerator child) Drain(child);
                        else throw new InvalidOperationException("Unexpected boot/retry yield type.");
                    }
                }
                finally { (routine as IDisposable)?.Dispose(); }
            }
            Drain((IEnumerator)Method(app, name).Invoke(app, null));
        }

        private static void FinishPreparationTransition(GreedLastApp app)
        {
            // Advance only the input guard, not wall time or the user's saved profile.
            Write(app, "infinitePreparationInputBlockUntil", 0f);
        }

        private static void VerifySlowPreparationPresentation(GreedLastApp app,
            GreedLastMockBackend backend, GreedLastStateMachine state)
        {
            string[] slots = backend.LoadLobby().SaveSlotLabels;
            int slowPresentations = 0;
            void SlowPresentation(GreedLastStateSnapshot value)
            {
                if (value.State != GreedLastScreenState.InfiniteStartReady) return;
                // Deliberately exceed the input guard to simulate a slow first layout on a phone.
                System.Threading.Thread.Sleep(240);
                slowPresentations++;
            }
            state.StateChanged += SlowPresentation;
            try
            {
                Method(app, "RequestInfiniteRun").Invoke(app, null);
                for (int expectedStep = 0; expectedStep < 3; expectedStep++)
                {
                    float deadline = Read<float>(app, "infinitePreparationInputBlockUntil");
                    Require(deadline > Time.realtimeSinceStartup,
                        "Preparation protection must begin after the slow presentation, not expire inside it."
                            + $" step={expectedStep}, deadline={deadline:F4}, now={Time.realtimeSinceStartup:F4}.");
                    if (expectedStep < 2)
                        Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Center });
                    else Method(app, "RequestNextRunPattern").Invoke(app, null);
                    Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                        && Read<int>(app, "infinitePreparationStep") == expectedStep,
                        "An immediate input after slow presentation must not select or start the next preparation step.");
                    FinishPreparationTransition(app);
                    if (expectedStep < 2)
                        Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Center });
                }
                Require(slowPresentations == 3, "Slow preparation must exercise entry, gift and relic presentation.");
                Method(app, "RequestReturnToLobby").Invoke(app, null);
                for (int slot = 0; slot < slots.Length; slot++)
                    Require(backend.LoadLobby().SaveSlotLabels[slot] == slots[slot],
                        "Slow preparation and cancellation must not consume or change a loadout.");
                Debug.Log("Greed slow preparation passed: three delayed presentations retain fresh input guards, immediate selection/start rejected and cancellation preserves slots. Isolated callbacks, not device latency measurement.");
            }
            finally { state.StateChanged -= SlowPresentation; }
        }

        private static void VerifyInterruptedCommandProtection(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core)
        {
            int cases = 0;
            foreach (bool opening in new[] { false, true })
            foreach (string command in new[] { "HandleRunChannelInput", "RequestNextRunPattern", "RequestToggleRunPause" })
            {
                state.SetRunCoreTest();
                float lastFrame = Time.realtimeSinceStartup - 0.75f;
                core.RestartPractice(opening ? lastFrame : lastFrame - 2f);
                if (!opening)
                {
                    core.Tick(lastFrame);
                    core.RequestNextPattern(lastFrame);
                    core.Tick(lastFrame);
                }
                GreedLastRunSnapshot before = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                Require(opening ? before.StartCountdownActive : before.ActivePattern,
                    "Interrupted command coverage requires its real pending opening or authored trap.");
                Method(app, command).Invoke(app, command == "HandleRunChannelInput"
                    ? new object[] { before.Pattern.IsValid ? before.Pattern.Channel : GreedLastRunChannel.Left } : null);
                GreedLastRunSnapshot after = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                Require(after.Paused && !after.ResumeCountdownActive && after.ActivePattern == before.ActivePattern
                    && Mathf.Abs(after.ElapsedSeconds - before.ElapsedSeconds) < 0.0001f
                    && Mathf.Abs(after.StartCountdownRemainingSeconds - before.StartCountdownRemainingSeconds) < 0.0001f
                    && after.Distance == before.Distance && after.Health == before.Health && after.Score == before.Score
                    && after.FocusStateRemainingSeconds == before.FocusStateRemainingSeconds && after.MissCount == before.MissCount
                    && Read<GameObject>(ui, "pauseMenuOverlay").activeInHierarchy,
                    "An app-bound command arriving before Update must freeze the last displayed state: " + command + "/" + opening);
                cases++;
            }
            state.SetLobbyReady(backend.LoadLobby());
            Debug.Log($"Greed interrupted app commands passed: {cases} isolated command callbacks before Update across active trap/opening countdown. Synthetic past frame times, not hardware input or engine frame-stall measurement; personal slots/settings unchanged.");
        }

        private static void VerifySlowRunPresentation(GreedLastApp app, GreedLastMockBackend backend,
            GreedLastStateMachine state, GreedLastRunCore core)
        {
            string[] slots = backend.LoadLobby().SaveSlotLabels;
            var failures = new List<string>();
            int delayMilliseconds = 0;
            void SlowPresentation(GreedLastRunSnapshot value)
            {
                if (delayMilliseconds > 0) System.Threading.Thread.Sleep(delayMilliseconds);
            }
            core.SnapshotChanged += SlowPresentation;
            try
            {
                foreach (var entry in new[] {
                    (Method: "RequestNormalRun", Arguments: (object[])null),
                    (Method: "StartNormalRunCore", Arguments: new object[] { "Slow normal entry check", false }),
                    (Method: "StartNormalRunCore", Arguments: new object[] { "Slow refill entry check", true }),
                    (Method: "RequestPracticeRun", Arguments: (object[])null) })
                {
                    state.SetLobbyReady(backend.LoadLobby());
                    Write(app, "runEntryLaneInputBlockUntil", 0f);
                    delayMilliseconds = 360;
                    Method(app, entry.Method).Invoke(app, entry.Arguments);
                    delayMilliseconds = 0;
                    GreedLastRunSnapshot before = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                    if (Read<float>(app, "runEntryLaneInputBlockUntil") <= Time.realtimeSinceStartup)
                        failures.Add("entry protection expired: " + entry.Method + "/" + before.RefillRun);
                    Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Center });
                    GreedLastRunSnapshot after = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                    if (before.ChoiceKind != after.ChoiceKind || before.ActiveChoiceProfile != after.ActiveChoiceProfile)
                        failures.Add("entry tap advanced the opening choice: " + entry.Method);
                }

                Method(app, "StartNormalRunCore").Invoke(app, new object[] { "Slow choice check", false });
                float clock = Time.realtimeSinceStartup;
                for (int step = 0; step < 32; step++)
                {
                    GreedLastRunSnapshot current = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                    if (current.ChoiceActive && current.ChoiceKind == GreedLastChoiceKind.Node) break;
                    if (current.ChoiceActive) core.HandleInput(GreedLastRunChannel.Left, clock);
                    else if (current.CountdownActive || !current.ActivePattern) core.Tick(clock += 2f);
                    else
                    {
                        clock += current.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - current.ElapsedSeconds;
                        core.HandleInput(current.Pattern.Channel, clock);
                    }
                }
                Write(app, "runEntryLaneInputBlockUntil", 0f);
                Write(app, "runChoiceInputBlockUntil", 0f);
                Method(app, "ResetRunLaneInputGuard").Invoke(app, null);
                delayMilliseconds = 240;
                Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Left });
                delayMilliseconds = 0;
                GreedLastRunSnapshot choice = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                Require(choice.ChoiceActive && choice.ChoiceKind == GreedLastChoiceKind.Gift,
                    "Slow normal choice protection needs a real route to chapter-gift transition.");
                if (Read<float>(app, "runChoiceInputBlockUntil") <= Time.realtimeSinceStartup)
                    failures.Add("gift protection expired during its first presentation");
                Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Right });
                GreedLastRunSnapshot remaining = Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot");
                if (remaining.ChoiceKind != choice.ChoiceKind || remaining.ActiveChoiceProfile != choice.ActiveChoiceProfile)
                    failures.Add("a post-layout tap selected the unread gift");
                Require(failures.Count == 0, "Slow run presentation: " + string.Join(" | ", failures));
                Write(app, "runChoiceInputBlockUntil", 0f);
                Method(app, "ResetRunLaneInputGuard").Invoke(app, null);
                Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Right });
                Require(!Read<GreedLastRunSnapshot>(app, "latestAppRunSnapshot").ChoiceActive,
                    "A deliberate gift choice must work after the guard is advanced.");
                for (int slot = 0; slot < slots.Length; slot++)
                    Require(backend.LoadLobby().SaveSlotLabels[slot] == slots[slot],
                        "Slow normal/practice entry and choice checks must not touch saved loadouts.");
                Debug.Log("Greed slow run presentation passed: four entry paths and a real route/gift transition retain post-layout guards; immediate unread choices rejected and deliberate selection accepted. Isolated delayed callbacks, not device latency measurement.");
            }
            finally
            {
                core.SnapshotChanged -= SlowPresentation;
                Write(app, "runEntryLaneInputBlockUntil", 0f);
                Write(app, "runChoiceInputBlockUntil", 0f);
                Method(app, "ResetRunLaneInputGuard").Invoke(app, null);
                state.SetLobbyReady(backend.LoadLobby());
            }
        }

        private static void VerifySlowConfirmationPresentation(GreedLastApp app,
            GreedLastMockBackend originalBackend, GreedLastStateMachine state)
        {
            string[] pendingFields = { "saveDraftRequiresExplicitSlotChoice", "saveDraftOverwriteConfirmPending",
                "saveDraftSkipConfirmPending", "saveSlotDeleteConfirmPending", "saveSlotRenameConfirmPending",
                "saveSlotDetailViewActive", "saveDraftFromRefillRun" };
            bool delayNext = false;
            void SlowPresentation(GreedLastStateSnapshot value)
            {
                if (!delayNext) return;
                delayNext = false;
                System.Threading.Thread.Sleep(420);
            }
            state.StateChanged += SlowPresentation;
            try
            {
                foreach (var scenario in new[] {
                    (Open: "OpenSaveLoadoutOverwriteConfirmation", Confirm: "ConfirmSaveLoadoutAndReturn", Flag: "saveDraftOverwriteConfirmPending"),
                    (Open: "OpenSaveLoadoutDeleteConfirmation", Confirm: "RequestDeleteSaveLoadoutSlot", Flag: "saveSlotDeleteConfirmPending"),
                    (Open: "OpenInfiniteLoadoutDeleteConfirmation", Confirm: "RequestDeleteSaveLoadoutSlot", Flag: "saveSlotDeleteConfirmPending"),
                    (Open: "OpenSaveLoadoutRenameConfirmation", Confirm: "RequestRenameSaveLoadoutSlot", Flag: "saveSlotRenameConfirmPending"),
                    (Open: "OpenInfiniteLoadoutRenameConfirmation", Confirm: "RequestRenameSaveLoadoutSlot", Flag: "saveSlotRenameConfirmPending"),
                    (Open: "RequestReturnToLobby", Confirm: "RequestReturnToLobby", Flag: "saveDraftSkipConfirmPending") })
                {
                    var isolated = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                    isolated.AddSampleSaveLoadoutForTest(out _);
                    isolated.AddSampleClearCandidateForTest(out _);
                    isolated.SelectSaveLoadoutSlot(0);
                    Write(app, "backend", isolated);
                    foreach (string field in pendingFields) Write(app, field, false);
                    state.SetLobbyReady(isolated.LoadLobby());
                    if (scenario.Open == "RequestReturnToLobby") Method(app, "RequestSaveLoadout").Invoke(app, null);
                    GreedLastLobbySnapshot before = isolated.LoadSelectedSaveSlotView();
                    var records = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(isolated, "saveLoadoutSlots").Clone();
                    var uses = (int[])Read<int[]>(isolated, "saveLoadoutUsesRemaining").Clone();
                    delayNext = true;
                    MethodInfo open = Method(app, scenario.Open);
                    open.Invoke(app, open.GetParameters().Length == 0 ? null : new object[] { null });
                    Require(!delayNext && Read<bool>(app, scenario.Flag)
                        && Read<float>(app, "saveConfirmationInputBlockUntil") > Time.realtimeSinceStartup,
                        "Confirmation protection must remain fresh after a slow presentation: " + scenario.Open);
                    Method(app, scenario.Confirm).Invoke(app, null);
                    Require(Read<bool>(app, scenario.Flag) && isolated.HasSaveLoadoutCandidate,
                        "An immediate post-layout confirmation must not consume its pending operation: " + scenario.Open);
                    GreedLastLobbySnapshot after = isolated.LoadSelectedSaveSlotView();
                    for (int slot = 0; slot < records.Length; slot++)
                        Require(after.SaveSlotLabels[slot] == before.SaveSlotLabels[slot]
                            && Read<GreedLastRunRecord[]>(isolated, "saveLoadoutSlots")[slot].Equals(records[slot])
                            && Read<int[]>(isolated, "saveLoadoutUsesRemaining")[slot] == uses[slot],
                            "A slow confirmation must preserve all slot records, names and uses: " + scenario.Open);
                }
                Debug.Log("Greed slow confirmations passed: overwrite, two delete views, two rename views and skip preserve fresh guards, pending candidate and all slot data after delayed presentation. Non-persisting fixtures only.");
            }
            finally
            {
                state.StateChanged -= SlowPresentation;
                Write(app, "backend", originalBackend);
                foreach (string field in pendingFields) Write(app, field, false);
                state.SetLobbyReady(originalBackend.LoadLobby());
            }
        }

        private static void CreatePointerViewport(Canvas canvas)
        {
            pointerCamera = new GameObject("GreedPointerLoopCamera", typeof(Camera)).GetComponent<Camera>();
            pointerCamera.orthographic = true;
            pointerCamera.orthographicSize = 5f;
            pointerCamera.transform.position = new Vector3(0f, 0f, -10f);
            pointerCamera.clearFlags = CameraClearFlags.SolidColor;
            pointerCamera.enabled = false;
            pointerTarget = new RenderTexture(pointerSize.x, pointerSize.y, 24, RenderTextureFormat.ARGB32);
            pointerTarget.Create();
            pointerCamera.targetTexture = pointerTarget;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = pointerCamera;
            canvas.scaleFactor = pointerSize.y / 1920f;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920f * pointerSize.x / pointerSize.y, 1920f);
            rect.position = Vector3.zero;
            rect.localScale = Vector3.one * (10f / 1920f);
        }

        private static void FinishSaveConfirmation(GreedLastApp app)
        {
            Write(app, "saveConfirmationInputBlockUntil", 0f);
        }

        private static void VerifySavedSlotConfirmations(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state)
        {
            for (int screen = 0; screen < 2; screen++)
            {
                backend.AddSampleSaveLoadoutForTest(out _);
                if (!backend.LoadLobby().SaveSlotOccupied[1]) backend.AddSampleSaveLoadoutForTest(out _);
                state.SetLobbyReady(backend.LoadLobby());
                if (screen == 1) Click(ui, "infiniteButton");
                Click(ui, "saveLoadoutButton");
                Require(state.CurrentState == (screen == 0 ? GreedLastScreenState.SaveLoadoutDraft
                        : GreedLastScreenState.InfiniteLoadoutSelect),
                    "Slot confirmation checks must cover both real management entry paths.");
                Button[] slots = Read<Button[]>(ui, "laneButtons");
                Click(slots[0]);
                GreedLastRunRecord original = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0];
                int remaining = Read<int[]>(backend, "saveLoadoutUsesRemaining")[0];
                Click(ui, "saveSlotRenameButton");
                Click(ui, "saveSlotRenameButton");
                Require(Read<bool>(app, "saveSlotRenameConfirmPending")
                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0].Equals(original),
                    "A rename double tap must leave the original slot unchanged in both management screens.");
                FinishSaveConfirmation(app);
                VerifyHeldManagementViewSwitch(app, ui, backend, state, "saveSlotRenameButton");
                Click(ui, "saveSlotRenameButton");
                Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0].LoadoutName != original.LoadoutName
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[0] == remaining,
                    "A deliberate rename confirmation must change only the name, not remaining uses.");
                GreedLastRunRecord renamed = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0];
                Click(ui, "saveSlotDeleteButton");
                Click(ui, "saveSlotDeleteButton");
                Require(Read<bool>(app, "saveSlotDeleteConfirmPending")
                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0].Equals(renamed),
                    "A delete double tap must preserve the slot until a deliberate confirmation.");
                Click(slots[1]);
                Require(!Read<bool>(app, "saveSlotDeleteConfirmPending"),
                    "Selecting another slot must cancel deletion of the previous target immediately.");
                Click(ui, "saveSlotDeleteButton");
                Click(ui, "saveSlotDetailButton");
                Require(!Read<bool>(app, "saveSlotDeleteConfirmPending")
                    && backend.LoadLobby().SaveSlotOccupied[1],
                    "Cancel must remain available even while the delete confirmation guard is active.");
                Click(ui, "saveSlotDeleteButton");
                FinishSaveConfirmation(app);
                VerifyHeldManagementViewSwitch(app, ui, backend, state, "saveSlotDeleteButton");
                Click(ui, "saveSlotDeleteButton");
                Require(!backend.LoadLobby().SaveSlotOccupied[1]
                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0].Equals(renamed),
                    "Confirmed deletion must remove only the newly selected slot, not the earlier target.");
                Click(ui, "returnLobbyButton");
            }

            backend.AddSampleSaveLoadoutForTest(out _);
            backend.AddSampleClearCandidateForTest(out _);
            state.SetLobbyReady(backend.LoadLobby());
            Click(ui, "saveLoadoutButton");
            Button[] saveSlots = Read<Button[]>(ui, "laneButtons");
            GreedLastRunRecord first = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0];
            GreedLastRunRecord second = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[1];
            Click(saveSlots[0]);
            Click(ui, "nextPatternButton");
            Click(ui, "nextPatternButton");
            Require(Read<bool>(app, "saveDraftOverwriteConfirmPending") && backend.HasSaveLoadoutCandidate
                && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0].Equals(first),
                "A rapid overwrite double tap must preserve both the stored slot and the one-shot candidate.");
            FinishSaveConfirmation(app);
            Click(saveSlots[1]);
            Require(!Read<bool>(app, "saveDraftOverwriteConfirmPending"),
                "Changing overwrite targets must require a new confirmation, even after the old guard expired.");
            Click(ui, "nextPatternButton");
            Click(ui, "nextPatternButton");
            Require(backend.HasSaveLoadoutCandidate && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[1].Equals(second),
                "The replacement target must receive its own guarded confirmation.");
            FinishSaveConfirmation(app);
            VerifyHeldManagementViewSwitch(app, ui, backend, state, "nextPatternButton");
            Click(ui, "nextPatternButton");
            Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[0].Equals(first)
                && !Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[1].Equals(second),
                "A deliberate overwrite must replace only S2 and consume the candidate exactly once.");
            Click(ui, "saveLoadoutButton");
            Require(!backend.HasSaveLoadoutCandidate,
                "Reopening saved loadouts after overwrite must not restore the consumed save opportunity.");
            Click(ui, "returnLobbyButton");
        }

        private static void VerifyPreparationReselection(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, string originalSlot)
        {
            Click(Read<Button[]>(ui, "laneButtons")[0]);
            Click(Read<Button[]>(ui, "laneButtons")[0]);
            Require(Read<int>(app, "infinitePreparationStep") == 0
                && Read<string>(app, "infinitePreparationGift") == string.Empty
                && Read<string>(app, "infinitePreparationRelic") == string.Empty,
                "Gift reselection must clear both previous choices and absorb its double tap.");
            FinishPreparationTransition(app);
            Click(Read<Button[]>(ui, "laneButtons")[2]);
            FinishPreparationTransition(app);
            Click(Read<Button[]>(ui, "laneButtons")[0]);
            FinishPreparationTransition(app);
            string gift = Read<string>(app, "infinitePreparationGift");
            Click(Read<Button[]>(ui, "laneButtons")[1]);
            Click(Read<Button[]>(ui, "laneButtons")[1]);
            Require(Read<int>(app, "infinitePreparationStep") == 1
                && Read<string>(app, "infinitePreparationGift") == gift
                && Read<string>(app, "infinitePreparationRelic") == string.Empty,
                "Relic reselection must keep its gift and wait for a fresh relic tap.");
            FinishPreparationTransition(app);
            Click(Read<Button[]>(ui, "laneButtons")[2]);
            Require(backend.LoadLobby().SaveSlotLabels[2] == originalSlot,
                "Reselection must not mutate or consume the stored loadout.");
            FinishPreparationTransition(app);
        }

        private static void VerifyPracticeReviewEntry(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            GreedLastLobbySnapshot lobby = backend.LoadLobby();
            string[] savedLabels = lobby.SaveSlotLabels;
            var records = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
            var uses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
            foreach (var scenario in new[] {
                (Detail: "탈출 실패 기록 저장\n복기: 채널 확인 / 숙이기 실수 1회 / 기초연습 숙이기 단계", Step: 1),
                (Detail: "탈출 실패 기록 저장\n복기: 2장 실수 1회 / 비틀기 실수 2회 / 기초연습 비틀기 단계\n재도전 목표: 2장 무실수 / 현재 1", Step: 2),
                (Detail: "탈출 중단 기록 저장\n복기: 1장 보정 1회 / 점프 보정 1회 / 기초연습 점프 단계\n재도전 목표: 1장 보정 0회 / 현재 1", Step: 0),
                (Detail: "탈출 중단 기록 저장\n복기: 정박 흐름\n복기 목표: 1장 무실수 / 현재 1 / 달성 무실수\n재도전 목표: 1장 보정 0회", Step: 0),
                (Detail: "보충 런 실패\n탈출 실패 기록 저장\n복기: 기초연습 비틀기 단계\n재도전 목표: 저장 조합 보충", Step: 2),
                (Detail: "무한모드 종료 / 기록 저장\n복기: 무한 1구간 / 숙이기 실수 1회\n힌트: 다음 구간 정확권 전환 / 기초연습 숙이기 단계\n목표: 다음 구간 무실수 / 직전 2", Step: 1),
                (Detail: "피라미드 입구", Step: 0) })
            {
                state.SetLobbyReady(backend.LoadLobby());
                state.ShowLocalNotice(scenario.Detail);
                string context = GreedLastApp.BuildPracticeOpeningContextFromLobbyDetail(scenario.Detail);
                Click(ui, "practiceRunButton");
                Require(state.CurrentState == GreedLastScreenState.RunCoreTest && latest().PracticeMode
                    && latest().ChapterProgress == scenario.Step
                    && Read<string>(core, "lastPracticeOpeningContext") == context,
                    "The actual lobby practice button must preserve its displayed review context and starting axis.");
                Require(string.IsNullOrEmpty(context) || latest().JudgementText.Contains(context, StringComparison.Ordinal),
                    "The actual review countdown must retain the weakness, retry goal and previous result.");
                if (context.Contains("재도전 목표:", StringComparison.Ordinal))
                    Require(!string.IsNullOrWhiteSpace(latest().ReviewTargetText),
                        "The preserved context must initialize the core's live review target, not just countdown copy.");
                float clock = Time.realtimeSinceStartup;
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(latest().ActivePattern && latest().ChapterProgress == scenario.Step
                    && (int)latest().Pattern.AvoidanceType == scenario.Step + 1,
                    "The first real practice trap must match the requested avoidance step after countdown.");
                for (int attempt = 0; attempt < 3 - scenario.Step; attempt++)
                {
                    if (attempt > 0) core.RequestNextPattern(clock += 2f);
                    GreedLastRunSnapshot before = latest();
                    clock += before.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - before.ElapsedSeconds;
                    core.HandleInput(before.Pattern.Channel, clock);
                }
                Require(latest().Stopped && latest().SuccessCount == 3 - scenario.Step
                    && !latest().SaveEligible,
                    "Focused practice must resolve only its remaining authored axes without a save candidate.");
                if (scenario.Step > 0)
                    Require(!latest().JudgementText.Contains("3축 정확", StringComparison.Ordinal)
                        && !latest().JudgementText.Contains("3축 대응 확인", StringComparison.Ordinal)
                        && !latest().JudgementText.Contains("바닥→↑점프", StringComparison.Ordinal)
                        && (scenario.Step < 2 || !latest().JudgementText.Contains("상단→↓숙이기", StringComparison.Ordinal))
                        && latest().JudgementText.Contains("측면→↔비틀기 정확", StringComparison.Ordinal)
                        && Read<Button>(ui, "nextPatternButton").GetComponentInChildren<Text>().text == "복기 다시 확인",
                        "Partial review must not claim full three-axis mastery or offer a misleading full-course restart.");
                Click(ui, "nextPatternButton");
                Require(latest().ChapterProgress == scenario.Step && latest().StartCountdownActive
                    && Read<string>(core, "lastPracticeOpeningContext") == context,
                    "Restarting focused practice must preserve its original axis and review context.");
                Click(ui, "pauseButton");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                    "Focused practice must return without a save opportunity or normal-run result.");
                GreedLastLobbySnapshot after = backend.LoadLobby();
                for (int slot = 0; slot < savedLabels.Length; slot++)
                    Require(after.SaveSlotLabels[slot] == savedLabels[slot]
                        && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(records[slot])
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == uses[slot],
                        "Review practice must preserve every saved record and remaining use.");
            }
            Debug.Log("Greed Last practice review entry passed: seven actual lobby button paths preserve slide/twist/jump, failed/advanced targets, refill/infinite review and a clean untargeted entry; all slots and uses unchanged.");
        }

        private static void VerifyFocusedPracticeCompletion(GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            int originalPracticeCompleted = PlayerPrefs.GetInt(PracticeKey, 0);
            string[] slots = backend.LoadLobby().SaveSlotLabels;
            try
            {
                foreach (int step in new[] { 1, 2 })
                {
                    string detail = "탈출 실패 기록 저장\n복기: 기초연습 "
                        + (step == 1 ? "숙이기 단계" : "비틀기 단계");
                    foreach (string outcome in new[] { "exact", "good", "wrong", "no-input" })
                    {
                        PlayerPrefs.SetInt(PracticeKey, 0);
                        state.SetLobbyReady(backend.LoadLobby());
                        state.ShowLocalNotice(detail);
                        string context = GreedLastApp.BuildPracticeOpeningContextFromLobbyDetail(detail);
                        Click(ui, "practiceRunButton");
                        CompleteLesson(outcome, step);
                        string expected = outcome == "exact" ? "복기 다시 확인"
                            : outcome == "good" ? "정확권 다시" : "실수 구간 다시";
                        Require(Read<Button>(ui, "nextPatternButton").GetComponentInChildren<Text>().text == expected,
                            "Focused practice must offer the action matching its actual outcome: " + outcome);
                        Require(Read<Text>(ui, "titleText").text == "복기 연습 완료",
                            "Partial outcomes must not claim the complete basic course in their visible title.");
                        Click(ui, "nextPatternButton");
                        Require(latest().StartCountdownActive && latest().ChapterProgress == step
                            && Read<string>(core, "lastPracticeOpeningContext") == context
                            && PlayerPrefs.GetInt(PracticeKey, 0) == 0,
                            "A partial review restart must preserve its lesson without completing full onboarding.");
                        CompleteLesson(outcome, step);
                        Click(ui, "returnLobbyButton");
                        Require(state.CurrentState == GreedLastScreenState.LobbyReady
                            && PlayerPrefs.GetInt(PracticeKey, 0) == 0
                            && state.CurrentDetail.Contains("복기 연습 완료", StringComparison.Ordinal)
                            && !backend.HasSaveLoadoutCandidate,
                            "Returning from a partial review must not record full three-axis onboarding: " + outcome);
                        string[] after = backend.LoadLobby().SaveSlotLabels;
                        for (int slot = 0; slot < slots.Length; slot++)
                            Require(slots[slot] == after[slot], "Focused completion must preserve every save slot and use.");
                    }
                }
                foreach (string outcome in new[] { "exact", "good", "wrong", "no-input" })
                {
                    PlayerPrefs.SetInt(PracticeKey, 0);
                    state.SetLobbyReady(backend.LoadLobby());
                    state.ShowLocalNotice("피라미드 입구 동기화 완료");
                    Click(ui, "practiceRunButton");
                    Require(latest().ChapterProgress == 0, "The full-course control must begin at its original first axis.");
                    CompleteLesson(outcome, 0);
                    bool completed = outcome == "exact" || outcome == "good";
                    Require(Read<Text>(ui, "titleText").text == (completed ? "기초연습 완료" : "기초연습 중단"),
                        "The complete course must retain its original success or health-zero failure title: " + outcome);
                    Click(ui, "returnLobbyButton");
                    Require(PlayerPrefs.GetInt(PracticeKey, 0) == (completed ? 1 : 0)
                        && state.CurrentState == GreedLastScreenState.LobbyReady
                        && state.CurrentDetail.Contains(completed ? "기초연습 완료" : "종료했습니다", StringComparison.Ordinal),
                        "Explicit return must mark onboarding only for the successfully completed course: " + outcome);
                }
                foreach (int attempts in new[] { 0, 1 })
                {
                    PlayerPrefs.SetInt(PracticeKey, 0);
                    state.SetLobbyReady(backend.LoadLobby());
                    state.ShowLocalNotice("피라미드 입구 동기화 완료");
                    Click(ui, "practiceRunButton");
                    if (attempts > 0)
                    {
                        float clock = Time.realtimeSinceStartup;
                        core.Tick(clock += 2f);
                        core.Tick(clock += 1f);
                        GreedLastRunSnapshot before = latest();
                        core.HandleInput(before.Pattern.Channel,
                            clock + before.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - before.ElapsedSeconds);
                    }
                    Click(ui, "pauseButton");
                    Require(latest().Paused && !core.IsPracticeComplete,
                        "An early exit must originate from an unfinished, actually paused practice.");
                    Click(ui, "returnLobbyButton");
                    Require(PlayerPrefs.GetInt(PracticeKey, 0) == 0
                        && state.CurrentDetail.Contains("종료했습니다", StringComparison.Ordinal),
                        "Exiting before the full course is complete must not mark onboarding.");
                }
                Require(!backend.HasSaveLoadoutCandidate, "Practice completion controls must not create a save candidate.");
                string[] finalSlots = backend.LoadLobby().SaveSlotLabels;
                for (int slot = 0; slot < slots.Length; slot++)
                    Require(slots[slot] == finalSlots[slot], "Full and aborted practice must preserve every slot and use.");
            }
            finally
            {
                PlayerPrefs.SetInt(PracticeKey, originalPracticeCompleted);
            }
            Debug.Log("Greed Last focused practice completion passed: eight partial outcomes with matching restarts, four full-course outcomes and two early exits; actual lobby return, correct onboarding and preserved save use.");

            void CompleteLesson(string outcome, int step)
            {
                float clock = Time.realtimeSinceStartup;
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                for (int attempt = 0; attempt < 3 - step; attempt++)
                {
                    if (attempt > 0) core.RequestNextPattern(clock += 2f);
                    GreedLastRunSnapshot before = latest();
                    Require(before.ActivePattern && (int)before.Pattern.AvoidanceType == step + attempt + 1,
                        "Focused outcomes must use only their real remaining authored traps.");
                    if (outcome == "no-input")
                        core.Tick(clock += Mathf.Max(before.Pattern.HitSeconds,
                            before.Pattern.CoreSeconds + before.Pattern.GoodWindowSeconds - core.InputTimingOffsetSeconds)
                            + 0.02f - before.ElapsedSeconds);
                    else
                    {
                        clock += before.Pattern.CoreSeconds - core.InputTimingOffsetSeconds - before.ElapsedSeconds;
                        if (outcome == "good")
                            clock += (before.Pattern.SuccessWindowSeconds + before.Pattern.GoodWindowSeconds) * 0.5f;
                        GreedLastRunChannel channel = outcome != "wrong" ? before.Pattern.Channel
                            : before.Pattern.Channel == GreedLastRunChannel.Left ? GreedLastRunChannel.Right : GreedLastRunChannel.Left;
                        core.HandleInput(channel, clock);
                    }
                }
                bool expectedComplete = step > 0 || outcome == "exact" || outcome == "good";
                Require(latest().Stopped && core.IsPracticeComplete == expectedComplete
                    && latest().SuccessCount + latest().GoodCount + latest().MissCount == 3 - step
                    && !latest().SaveEligible && PlayerPrefs.GetInt(PracticeKey, 0) == 0,
                    "Attempted axes must preserve partial completion or full health-zero failure before explicit return.");
            }
        }

        private static void VerifyPracticeCalibrationLoop(GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            string key = GreedLastRunCore.InputTimingOffsetPrefsKey;
            bool hadOffset = PlayerPrefs.HasKey(key);
            float storedOffset = PlayerPrefs.GetFloat(key);
            float originalCoreOffset = core.InputTimingOffsetSeconds;
            string[] savedLabels = backend.LoadLobby().SaveSlotLabels;
            try
            {
                core.ResetInputTimingOffset();
                Click(ui, "practiceRunButton");
                float clock = Time.realtimeSinceStartup;
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                for (int trap = 0; trap < 3; trap++)
                {
                    if (trap > 0) core.RequestNextPattern(clock += 2f);
                    GreedLastRunSnapshot sample = latest();
                    Require(sample.ActivePattern, "Calibration needs three active practice traps.");
                    clock += sample.Pattern.CoreSeconds - core.InputTimingOffsetSeconds
                        + (sample.Pattern.SuccessWindowSeconds + sample.Pattern.GoodWindowSeconds) * 0.5f;
                    core.HandleInput(sample.Pattern.Channel, clock);
                    Require(latest().LastResult == GreedLastJudgementResult.Good,
                        "Calibration must use measured late Good inputs, not an injected recommendation.");
                }
                GreedLastRunSnapshot result = latest();
                Button calibration = Read<Button[]>(ui, "timingOffsetButtons")[1];
                Require(result.Stopped && result.PracticeMode && result.HasTimingRecommendation
                    && result.TimingRecommendationMilliseconds < 0 && calibration.IsInteractable(),
                    "Late practice responses must expose an actionable negative recommendation at the result.");
                Click(calibration);
                Require(latest().TimingRecommendationApplied
                    && Mathf.RoundToInt(core.InputTimingOffsetSeconds * 1000f) == result.TimingRecommendationMilliseconds
                    && Mathf.Approximately(PlayerPrefs.GetFloat(key), core.InputTimingOffsetSeconds)
                    && !calibration.IsInteractable()
                    && calibration.GetComponentInChildren<Text>().text.Contains("적용 완료"),
                    "The real result action must persist its recommendation once and show the completed state.");
                float appliedOffset = core.InputTimingOffsetSeconds;
                Click(ui, "returnLobbyButton");
                Click(ui, "practiceRunButton");
                Require(Mathf.Approximately(latest().InputTimingOffsetSeconds, appliedOffset)
                    && !latest().HasTimingRecommendation && !latest().TimingRecommendationApplied,
                    "A fresh practice must keep the saved offset but clear the preceding run's samples and result.");
                Click(ui, "pauseButton");
                Require(latest().Paused && calibration.IsInteractable()
                    && calibration.GetComponentInChildren<Text>().text.Contains("0ms"),
                    "Pause settings must offer a real reset before fresh timing samples are collected.");
                Click(calibration);
                Require(latest().Paused && Mathf.Approximately(core.InputTimingOffsetSeconds, 0f)
                    && Mathf.Approximately(PlayerPrefs.GetFloat(key), 0f) && !calibration.IsInteractable(),
                    "The actual reset button must persist zero and remain paused without a second actionable reset.");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                    "Practice calibration must return to the lobby without creating a normal-run save opportunity.");
                for (int slot = 0; slot < savedLabels.Length; slot++)
                    Require(backend.LoadLobby().SaveSlotLabels[slot] == savedLabels[slot],
                        "Practice calibration must not mutate or consume saved loadouts.");
                Debug.Log("Greed Last app calibration loop passed: three measured Good responses -> actual recommendation click -> persisted offset -> new practice -> paused reset -> preserved save slots. Original preference restored.");
            }
            finally
            {
                Write(core, "inputTimingOffsetSeconds", originalCoreOffset);
                if (hadOffset) PlayerPrefs.SetFloat(key, storedOffset);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static void VerifyPlayerPrimaryControls(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            bool previousCleanView = ui.UsePlayerRunControls;
            string[] savedLabels = backend.LoadLobby().SaveSlotLabels;
            Button primary = Read<Button>(ui, "nextPatternButton");
            string Label() => primary.GetComponentInChildren<Text>().text;
            void VerifyHeldModeChange()
            {
                GreedLastRunSnapshot before = latest();
                bool wasPlayerMode = ui.UsePlayerRunControls;
                int calls = 0;
                UnityEngine.Events.UnityAction countClick = () => calls++;
                primary.onClick.AddListener(countClick);
                void SwitchMode()
                {
                    ui.ToggleDevCleanView();
                    if (usePointerModule) RefreshPointerViewport(primary);
                }
                try
                {
                    if (usePointerModule)
                    {
                        RefreshPointerViewport(primary);
                        GreedLastPrototypeKeyboardCheck.ClickOnce(UnityEngine.Object.FindAnyObjectByType<EventSystem>(),
                            primary, useTouch, whileHeld: SwitchMode);
                        canceledCommandPresses++;
                    }
                    else
                    {
                        var pointer = new PointerEventData(EventSystem.current)
                            { button = PointerEventData.InputButton.Left, eligibleForClick = true };
                        ExecuteEvents.Execute(primary.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                        SwitchMode();
                        ExecuteEvents.Execute(primary.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                        if (pointer.eligibleForClick)
                            ExecuteEvents.Execute(primary.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    }
                    Require(calls == 0 && latest().Equals(before) && ui.UsePlayerRunControls != wasPlayerMode,
                        "A held primary press must be canceled when F11 changes its meaning, without pausing or toggling flow.");
                }
                finally { primary.onClick.RemoveListener(countClick); }
            }
            try
            {
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                Click(ui, "practiceRunButton");
                Require(latest().StartCountdownActive && Label() == "시작 준비" && !primary.IsInteractable(),
                    "The clean primary action must describe the locked opening countdown, not auto-start.");
                GreedLastRunSnapshot opening = latest();
                Method(app, "RequestNextRunPattern").Invoke(app, null);
                Require(latest().Equals(opening),
                    "Enter must not bypass or pause the opening countdown through a disabled clean action.");
                core.Tick(Time.realtimeSinceStartup + 2f);
                core.RequestNextPattern(Time.realtimeSinceStartup);
                Require(latest().ActivePattern && latest().AutoFlow && Label() == "일시정지",
                    "Live clean play must offer a real pause while automatic traps remain enabled.");
                GreedLastRunSnapshot before = latest();
                Require(!Read<Button>(ui, "returnLobbyButton").gameObject.activeInHierarchy,
                    "Clean gameplay must keep abandonment inside the pause menu, not next to the live action.");
                Click(primary);
                Require(latest().Paused && !latest().AutoFlow && Read<bool>(core, "autoFlowBeforePause")
                    && Label() == "재개"
                    && Read<GameObject>(ui, "pauseMenuOverlay").activeInHierarchy,
                    "The app-bound clean primary press must open the separate pause menu, not disable trap spawning.");
                Require(Read<Button>(ui, "returnLobbyButton").gameObject.activeInHierarchy,
                    "The clean pause menu must still offer an explicit exit.");
                core.Tick(Time.realtimeSinceStartup + 60f);
                Require(latest().Health == before.Health && latest().Score == before.Score
                    && Mathf.Approximately(latest().Distance, before.Distance),
                    "The primary pause must freeze distance, health and score during idle time.");
                for (int toggle = 0; toggle < 2; toggle++)
                {
                    ui.ToggleDevCleanView();
                    Require(Label() == "재개" && latest().Paused,
                        "F11 must not change or dismiss the paused primary action.");
                }
                Click(primary);
                float resumeEnd = Read<float>(core, "resumeCountdownEndsAt");
                Require(latest().ResumeCountdownActive && Label() == "재개 준비" && !primary.IsInteractable(),
                    "The same primary action must resume through the locked lead-in countdown.");
                Method(app, "RequestNextRunPattern").Invoke(app, null);
                Require(Read<float>(core, "resumeCountdownEndsAt") == resumeEnd,
                    "Repeated Enter during resume preparation must not alter its deadline.");
                core.Tick(resumeEnd + 0.01f);
                Require(!latest().Paused && latest().ActivePattern && latest().AutoFlow && Label() == "일시정지"
                    && !Read<Button>(ui, "returnLobbyButton").gameObject.activeInHierarchy,
                    "Resuming must restore the same trap and automatic flow, with pause as the clean action.");
                VerifyHeldModeChange();
                Require(!ui.UsePlayerRunControls && Label() == "자동 정지"
                    && Read<Button>(ui, "returnLobbyButton").gameObject.activeInHierarchy,
                    "F11 detailed mode must immediately restore the development auto-flow command.");
                Click(primary);
                Require(!latest().Paused && !latest().AutoFlow && Label() == "자동 시작",
                    "Detailed auto-stop must retain its development behavior without masquerading as pause.");
                GreedLastRunSnapshot beforeFlowRestart = latest();
                VerifyHeldModeChange();
                Require(ui.UsePlayerRunControls && Label() == "함정 재개"
                    && !latest().Paused && !latest().AutoFlow,
                    "Clean view must expose a way to resume development-stopped traps without silently changing run state.");
                Click(primary);
                Require(latest().AutoFlow && !latest().Paused && Label() == "일시정지"
                    && latest().ActivePattern == beforeFlowRestart.ActivePattern
                    && latest().Pattern.PatternId == beforeFlowRestart.Pattern.PatternId
                    && latest().Health == beforeFlowRestart.Health && latest().Score == beforeFlowRestart.Score
                    && Mathf.Approximately(latest().Distance, beforeFlowRestart.Distance),
                    "The clean trap-resume press must restore automatic flow without replacing the trap or altering totals.");
                VerifyHeldModeChange();
                Click(primary);
                Click(primary);
                Require(!latest().Paused && latest().AutoFlow && Label() == "자동 정지",
                    "The development command must still re-enable automatic trap generation.");
                VerifyHeldModeChange();
                Require(ui.UsePlayerRunControls && Label() == "일시정지",
                    "Returning to clean play must immediately restore the matching primary label.");
                GreedLastRunSnapshot active = latest();
                core.HandleInput(active.Pattern.Channel, Read<float>(core, "patternStartedAt")
                    + active.Pattern.CoreSeconds - core.InputTimingOffsetSeconds);
                Require(!latest().ActivePattern && !latest().Stopped,
                    "The recovery fixture must also reach the real gap between practice traps.");
                ui.ToggleDevCleanView();
                Click(primary);
                ui.ToggleDevCleanView();
                GreedLastRunSnapshot gap = latest();
                Require(!gap.AutoFlow && !gap.ActivePattern && Label() == "함정 재개",
                    "Clean controls must recover auto-stop in a gap, not only while a trap remains active.");
                Click(primary);
                Require(latest().AutoFlow && !latest().Paused && !latest().ActivePattern
                    && latest().Health == gap.Health && latest().Score == gap.Score
                    && Mathf.Approximately(latest().Distance, gap.Distance),
                    "Resuming a gap must preserve totals and schedule the next trap instead of starting a pause countdown.");
                core.Tick(Read<float>(core, "nextPatternAt") + 0.01f);
                Require(latest().ActivePattern && latest().AutoFlow && Label() == "일시정지"
                    && latest().Pattern.PatternId != active.Pattern.PatternId,
                    "After the real automatic delay, gap recovery must generate the next practice trap and restore pause.");
                Method(app, "RequestNextRunPattern").Invoke(app, null);
                Require(latest().Paused && Read<bool>(core, "autoFlowBeforePause"),
                    "The Enter callback must use the same clean pause route as the pointer action.");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                    "Exiting the clean pause menu must keep practice isolated from normal save opportunities.");
                for (int slot = 0; slot < savedLabels.Length; slot++)
                    Require(backend.LoadLobby().SaveSlotLabels[slot] == savedLabels[slot],
                        "Primary control checks must not mutate or consume saved loadouts.");
                Debug.Log("Greed Last player primary controls passed: locked opening -> real clean pause -> frozen totals -> F11 held-press cancellation -> guarded resume -> preserved auto-flow -> clean trap recovery while active and between traps -> clean Enter pause -> safe practice exit.");
            }
            finally
            {
                if (ui.UsePlayerRunControls != previousCleanView) ui.ToggleDevCleanView();
            }
        }

        private static void VerifyAppPauseLifecycle(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            string[] savedLabels = backend.LoadLobby().SaveSlotLabels;
            Click(ui, "practiceRunButton");
            Write(core, "startCountdownEndsAt", 0f);
            core.Tick(Time.realtimeSinceStartup);
            core.RequestNextPattern(Time.realtimeSinceStartup);
            Require(latest().ActivePattern && !latest().Paused, "Lifecycle checks need an active practice trap.");
            GreedLastRunSnapshot beforePause = latest();
            AudioSource audio = Read<AudioSource>(ui, "rhythmAudio");
            Method(ui, "OnApplicationFocus").Invoke(ui, new object[] { false });
            Method(app, "OnApplicationFocus").Invoke(app, new object[] { false });
            Require(audio.mute, "UI-first focus loss must mute the app-connected audio source.");
            Require(latest().Paused && !latest().ResumeCountdownActive,
                "Losing app focus must show a paused run, not start a resume countdown.");
            for (int lane = 0; lane < 3; lane++)
            {
                Require(!Read<Button[]>(ui, "laneButtons")[lane].IsInteractable(),
                    "Every gameplay lane must be disabled behind the pause menu.");
                Method(app, "HandleRunChannelInput").Invoke(app, new object[] { (GreedLastRunChannel)(lane + 1) });
            }
            Require(latest().Paused && !latest().ResumeCountdownActive,
                "Gameplay keys, including the center key, must not dismiss the pause menu.");
            core.Tick(Time.realtimeSinceStartup + 60f);
            Method(app, "OnApplicationPause").Invoke(app, new object[] { true });
            Method(ui, "OnApplicationPause").Invoke(ui, new object[] { true });
            Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
            Method(ui, "OnApplicationPause").Invoke(ui, new object[] { false });
            Require(audio.mute, "Resuming the app before focus returns must keep the source muted.");
            Method(app, "OnApplicationFocus").Invoke(app, new object[] { true });
            Method(ui, "OnApplicationFocus").Invoke(ui, new object[] { true });
            Require(audio.mute == (Read<float>(ui, "sfxVolume") <= 0f),
                "Full foreground restoration must respect the user's sound setting.");
            Require(latest().Paused && !latest().ResumeCountdownActive
                && latest().Health == beforePause.Health && latest().Score == beforePause.Score
                && Mathf.Approximately(latest().Distance, beforePause.Distance)
                && state.CurrentState == GreedLastScreenState.RunCoreTest,
                "Background time and foreground callbacks must preserve the paused run without damage or lobby resync.");
            VerifyIdlePauseSettings(ui, core, latest);
            VerifyHeldResumeInterruption(app, ui, latest);
            Click(ui, "nextPatternButton");
            float resumeEnd = Read<float>(core, "resumeCountdownEndsAt");
            Require(latest().ResumeCountdownActive && !Read<Button>(ui, "nextPatternButton").IsInteractable(),
                "The explicit resume button must start its countdown and disable repeated clicks.");
            Method(ui, "Update").Invoke(ui, null);
            Require(Read<int>(ui, "rhythmBeatIndex") == 1, "The actual resume button must start the UI's first countdown beat.");
            Method(app, "RequestToggleRunPause").Invoke(app, null);
            Require(Read<float>(core, "resumeCountdownEndsAt") == resumeEnd,
                "A repeated pause/resume key during countdown must not restart or shorten it.");
            Write(ui, "rhythmBeatIndex", 3);
            Write(ui, "nextBeatSoundAt", Time.unscaledTime + 100f);
            Method(app, "OnApplicationFocus").Invoke(app, new object[] { false });
            Method(ui, "OnApplicationFocus").Invoke(ui, new object[] { false });
            Require(latest().Paused && !latest().ResumeCountdownActive,
                "Losing focus during resume countdown must return to a stable pause menu.");
            Require(audio.mute && Read<int>(ui, "rhythmBeatIndex") == 0,
                "App-first focus loss must synchronously clear countdown audio without waiting for UI Update.");
            Method(app, "OnApplicationFocus").Invoke(app, new object[] { true });
            Method(ui, "OnApplicationFocus").Invoke(ui, new object[] { true });
            Click(ui, "nextPatternButton");
            Method(ui, "Update").Invoke(ui, null);
            Require(Read<int>(ui, "rhythmBeatIndex") == 1, "The second real resume callback must restart at the first beat.");
            Write(core, "resumeCountdownEndsAt", 0f);
            core.Tick(Time.realtimeSinceStartup);
            Method(ui, "Update").Invoke(ui, null);
            Require(Read<int>(ui, "rhythmBeatIndex") == 2, "The app-connected GO transition must emit one release beat.");
            Method(ui, "Update").Invoke(ui, null);
            Require(Read<int>(ui, "rhythmBeatIndex") == 2, "The release beat must not duplicate on the next UI update.");
            Require(!latest().Paused && !latest().CountdownActive && latest().ActivePattern
                && latest().Health == beforePause.Health && latest().Score == beforePause.Score,
                "A fresh explicit resume must restore the same trap and preserve its score and health.");
            Require(latest().ElapsedSeconds < beforePause.ElapsedSeconds,
                "The resumed trap must move back by its lead-in buffer, not land immediately.");
            for (int lane = 0; lane < 3; lane++)
                Require(Read<Button[]>(ui, "laneButtons")[lane].IsInteractable(),
                    "All gameplay lanes must become interactive after the countdown finishes.");
            Click(ui, "pauseButton");
            Click(ui, "returnLobbyButton");
            Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                "Leaving a paused practice must return to the lobby without a save candidate.");
            for (int slot = 0; slot < savedLabels.Length; slot++)
                Require(backend.LoadLobby().SaveSlotLabels[slot] == savedLabels[slot],
                    "Practice pause/resume/exit must not consume existing saved loadouts.");
            Debug.Log("Greed Last app pause lifecycle passed: background audio in both callback orders -> paused input isolation -> real-button first beat -> interrupted countdown reset -> single GO -> lead-in buffer -> lobby with saved slots intact.");
        }

        private static void VerifyIdlePauseSettings(GreedLastRuntimeUi ui, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            var toggles = new[]
            {
                (Field: "hapticsEnabled", Button: "hapticsButton", Key: "GreedLast.RuntimeUi.HapticsEnabled"),
                (Field: "rotationTiltEnabled", Button: "rotationTiltButton", Key: "GreedLast.RuntimeUi.RotationTiltEnabled"),
                (Field: "motionComfortEnabled", Button: "motionComfortButton", Key: "GreedLast.RuntimeUi.MotionComfortEnabled"),
            };
            string volumeKey = "GreedLast.RuntimeUi.SfxVolume";
            string offsetKey = GreedLastRunCore.InputTimingOffsetPrefsKey;
            bool hadVolume = PlayerPrefs.HasKey(volumeKey), hadOffset = PlayerPrefs.HasKey(offsetKey);
            float storedVolume = PlayerPrefs.GetFloat(volumeKey), storedOffset = PlayerPrefs.GetFloat(offsetKey);
            float originalVolume = Read<float>(ui, "sfxVolume"), originalOffset = core.InputTimingOffsetSeconds;
            var originalToggles = new Dictionary<string, (bool HadKey, int Stored, bool Enabled)>();
            foreach (var toggle in toggles)
                originalToggles[toggle.Key] = (PlayerPrefs.HasKey(toggle.Key), PlayerPrefs.GetInt(toggle.Key), Read<bool>(ui, toggle.Field));
            GreedLastRunSnapshot before = latest();
            try
            {
                // Inspect immediately after the real click; no core Tick is allowed to repair a stale setting label.
                foreach (var toggle in toggles)
                {
                    bool original = Read<bool>(ui, toggle.Field);
                    Button button = Read<Button>(ui, toggle.Button);
                    for (int click = 0; click < 2; click++)
                    {
                        Click(button);
                        bool expected = click == 1 ? original : !original;
                        Require(Read<bool>(ui, toggle.Field) == expected
                            && PlayerPrefs.GetInt(toggle.Key) == (expected ? 1 : 0)
                            && button.GetComponentInChildren<Text>().text.EndsWith(expected ? "ON" : "OFF", StringComparison.Ordinal),
                            "A paused toggle must persist and relabel synchronously: " + toggle.Field);
                    }
                }
                Method(ui, "SetSfxVolume").Invoke(ui, new object[] { 0.5f });
                Button[] volume = Read<Button[]>(ui, "sfxVolumeButtons");
                void CheckVolume(int button, float expected)
                {
                    Click(volume[button]);
                    Require(Mathf.Approximately(PlayerPrefs.GetFloat(volumeKey), expected)
                        && Mathf.Approximately(Read<AudioSource>(ui, "rhythmAudio").volume, expected)
                        && volume[1].GetComponentInChildren<Text>().text.Contains(Mathf.RoundToInt(expected * 100f) + "%"),
                        "Paused volume changes must reach the playing source, saved preference and label immediately.");
                }
                CheckVolume(0, 0.4f);
                CheckVolume(2, 0.5f);
                CheckVolume(1, 1f);
                core.ResetInputTimingOffset();
                Button[] timing = Read<Button[]>(ui, "timingOffsetButtons");
                Click(timing[0]);
                Require(Mathf.Approximately(latest().InputTimingOffsetSeconds, 0.02f)
                    && Mathf.Approximately(PlayerPrefs.GetFloat(offsetKey), 0.02f)
                    && timing[1].IsInteractable()
                    && Read<Text>(ui, "pauseMenuBodyText").text.Contains("보정 +20ms", StringComparison.Ordinal),
                    "The paused +20ms command must publish immediately without waiting for Tick.");
                Click(timing[2]);
                Require(Mathf.Approximately(latest().InputTimingOffsetSeconds, 0f)
                    && Mathf.Approximately(PlayerPrefs.GetFloat(offsetKey), 0f) && !timing[1].IsInteractable()
                    && Read<Text>(ui, "pauseMenuBodyText").text.Contains("보정 0ms", StringComparison.Ordinal),
                    "Returning to zero must immediately disable an unnecessary calibration reset.");
                Click(timing[2]);
                Require(Mathf.Approximately(latest().InputTimingOffsetSeconds, -0.02f)
                    && Read<Text>(ui, "pauseMenuBodyText").text.Contains("보정 -20ms", StringComparison.Ordinal),
                    "Negative calibration must also appear immediately in the clean pause summary.");
                Click(timing[1]);
                Require(Mathf.Approximately(latest().InputTimingOffsetSeconds, 0f)
                    && Read<Text>(ui, "pauseMenuBodyText").text.Contains("보정 0ms", StringComparison.Ordinal),
                    "The actual reset command must restore the displayed value without a Tick.");
                Require(latest().Paused && !latest().ResumeCountdownActive
                    && latest().Health == before.Health && latest().Score == before.Score
                    && Mathf.Approximately(latest().Distance, before.Distance),
                    "Pause settings must not resume the run or change gameplay totals.");
                Debug.Log("Greed Last idle pause settings passed: real haptics/tilt/comfort toggles, three volume controls and +/-20ms calibration update immediately without a core tick. Preferences restored.");
            }
            finally
            {
                foreach (var toggle in toggles)
                {
                    var original = originalToggles[toggle.Key];
                    Write(ui, toggle.Field, original.Enabled);
                    if (original.HadKey) PlayerPrefs.SetInt(toggle.Key, original.Stored);
                    else PlayerPrefs.DeleteKey(toggle.Key);
                }
                Write(ui, "sfxVolume", originalVolume);
                Method(ui, "RefreshAudioPlaybackState").Invoke(ui, null);
                core.AdjustInputTimingOffset(originalOffset - core.InputTimingOffsetSeconds);
                if (hadVolume) PlayerPrefs.SetFloat(volumeKey, storedVolume);
                else PlayerPrefs.DeleteKey(volumeKey);
                if (hadOffset) PlayerPrefs.SetFloat(offsetKey, storedOffset);
                else PlayerPrefs.DeleteKey(offsetKey);
                PlayerPrefs.Save();
            }
        }

        private static void VerifySuspendedPlayerInput(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            foreach (bool choice in new[] { false, true })
            foreach (int source in new[] { 0, 1, 2, 3 })
            {
                state.SetLobbyReady(backend.LoadLobby());
                if (choice) Method(app, "StartNormalRunCore").Invoke(app, new object[] { string.Empty, false });
                else
                {
                    Method(app, "RequestPracticeRun").Invoke(app, null);
                    Method(app, "RequestToggleRunPause").Invoke(app, null);
                }
                GreedLastRunSnapshot before = latest();
                Require(choice ? before.ChoiceActive : before.Paused,
                    "Suspended input fixtures must start at a real choice or pause menu.");
                Write(app, "runEntryLaneInputBlockUntil", 0f);
                Write(app, "runChoiceInputBlockUntil", 0f);
                object target = source % 2 == 0 ? (object)app : ui;
                string lifecycle = source < 2 ? "OnApplicationFocus" : "OnApplicationPause";
                bool backgroundValue = source >= 2;
                try
                {
                    Method(target, lifecycle).Invoke(target, new object[] { backgroundValue });
                    Button button = choice ? Read<Button[]>(ui, "laneButtons")[0] : Read<Button>(ui, "nextPatternButton");
                    Require(!ui.AcceptsPlayerInput && !button.IsInteractable()
                        && Read<CanvasGroup>(ui, "playerInputGroup").blocksRaycasts
                        && Read<AudioSource>(ui, "rhythmAudio").mute,
                        "Suspended controls must reject input/feedback without exposing scene clicks.");
                    if (usePointerModule)
                    {
                        RefreshPointerViewport(button);
                        GreedLastPrototypeKeyboardCheck.ClickOnce(UnityEngine.Object.FindAnyObjectByType<EventSystem>(),
                            button, useTouch);
                        canceledCommandPresses++;
                    }
                    else
                    {
                        var pointer = new PointerEventData(EventSystem.current)
                            { button = PointerEventData.InputButton.Left, eligibleForClick = true };
                        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                        if (pointer.eligibleForClick)
                            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    }
                    GreedLastScreenState screenBefore = state.CurrentState;
                    Method(app, "RequestNextRunPattern").Invoke(app, null);
                    Method(app, "RequestToggleRunPause").Invoke(app, null);
                    Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.Left });
                    Method(app, "RequestReturnToLobby").Invoke(app, null);
                    GreedLastRunSnapshot after = latest();
                    Require(after.Paused == before.Paused && !after.ResumeCountdownActive
                        && after.ChoiceActive == before.ChoiceActive && after.ChoicePrompt == before.ChoicePrompt
                        && after.Health == before.Health && after.Score == before.Score && after.Distance == before.Distance
                        && state.CurrentState == screenBefore,
                        "Fresh input while suspended must not resume or choose: choice=" + choice + " / source=" + source);
                    void RestoreForeground()
                    {
                        Method(target, lifecycle).Invoke(target, new object[] { !backgroundValue });
                    }
                    if (usePointerModule)
                    {
                        RefreshPointerViewport(button);
                        GreedLastPrototypeKeyboardCheck.ClickOnce(UnityEngine.Object.FindAnyObjectByType<EventSystem>(),
                            button, useTouch, whileHeld: RestoreForeground);
                        canceledCommandPresses++;
                    }
                    else
                    {
                        var pointer = new PointerEventData(EventSystem.current)
                            { button = PointerEventData.InputButton.Left, eligibleForClick = true };
                        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                        RestoreForeground();
                        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                        if (pointer.eligibleForClick)
                            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    }
                    Require(ui.AcceptsPlayerInput && latest().Paused == before.Paused
                        && !latest().ResumeCountdownActive && latest().ChoicePrompt == before.ChoicePrompt,
                        "A press started while suspended must not become a new foreground command.");
                }
                finally
                {
                    Method(app, "OnApplicationFocus").Invoke(app, new object[] { true });
                    Method(ui, "OnApplicationFocus").Invoke(ui, new object[] { true });
                    Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
                    Method(ui, "OnApplicationPause").Invoke(ui, new object[] { false });
                }
                if (choice)
                {
                    Write(app, "runEntryLaneInputBlockUntil", 0f);
                    Write(app, "runChoiceInputBlockUntil", 0f);
                    Click(Read<Button[]>(ui, "laneButtons")[0]);
                    Require(latest().ChoicePrompt != before.ChoicePrompt || !latest().ChoiceActive,
                        "A genuinely new foreground choice must still work.");
                }
                else
                {
                    Click(ui, "nextPatternButton");
                    Require(latest().ResumeCountdownActive, "A genuinely new foreground resume must still work.");
                }
                core.Exit();
            }
            state.SetLobbyReady(backend.LoadLobby());
            Debug.Log("Greed suspended player input passed: eight app/UI focus/pause sources at real pause/choice menus, fresh blocked clicks, four direct commands, blocked-start presses released after return, and normal foreground retry. Synthetic events/module only; personal slots and settings unchanged.");
        }

        private static void VerifyHeldResumeInterruption(GreedLastApp app, GreedLastRuntimeUi ui,
            Func<GreedLastRunSnapshot> latest)
        {
            Button resume = Read<Button>(ui, "nextPatternButton");
            foreach (string lifecycle in new[] { "OnApplicationFocus", "OnApplicationPause" })
            foreach (bool appFirst in new[] { false, true })
            {
                GreedLastRunSnapshot before = latest();
                int clicks = 0;
                UnityEngine.Events.UnityAction countClick = () => clicks++;
                resume.onClick.AddListener(countClick);
                void InterruptAndReturn()
                {
                    foreach (bool returning in new[] { false, true })
                    {
                        object[] args = { lifecycle == "OnApplicationFocus" ? returning : !returning };
                        Method(appFirst ? (object)app : ui, lifecycle).Invoke(appFirst ? (object)app : ui, args);
                        Method(appFirst ? (object)ui : app, lifecycle).Invoke(appFirst ? (object)ui : app, args);
                    }
                    if (usePointerModule) RefreshPointerViewport(resume);
                }
                try
                {
                    if (usePointerModule)
                    {
                        RefreshPointerViewport(resume);
                        GreedLastPrototypeKeyboardCheck.ClickOnce(UnityEngine.Object.FindAnyObjectByType<EventSystem>(),
                            resume, useTouch, whileHeld: InterruptAndReturn);
                        canceledCommandPresses++;
                    }
                    else
                    {
                        var pointer = new PointerEventData(EventSystem.current)
                            { button = PointerEventData.InputButton.Left, eligibleForClick = true };
                        ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                        InterruptAndReturn();
                        ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                        if (pointer.eligibleForClick)
                            ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    }
                    Require(clicks == 0 && latest().Paused && !latest().ResumeCountdownActive
                        && latest().Health == before.Health && latest().Score == before.Score
                        && Mathf.Approximately(latest().Distance, before.Distance),
                        "A held resume command must cancel without advancing the run: " + lifecycle + " / appFirst=" + appFirst);
                }
                finally
                {
                    resume.onClick.RemoveListener(countClick);
                }
            }
            Debug.Log("Greed Last held-resume checks passed: focus/pause interruption in both app/UI callback orders; release does not resume or advance the run.");
        }

        private static void VerifyGameplayLaneOutcomes(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            string[] savedLabels = backend.LoadLobby().SaveSlotLabels;
            Click(ui, "practiceRunButton");
            Write(core, "startCountdownEndsAt", 0f);
            core.Tick(Time.realtimeSinceStartup);
            var expected = new[] { GreedLastJudgementResult.Success, GreedLastJudgementResult.Good, GreedLastJudgementResult.Miss };
            for (int step = 0; step < expected.Length; step++)
            {
                if (!latest().ActivePattern) core.RequestNextPattern(Time.realtimeSinceStartup);
                GreedLastRunSnapshot before = latest();
                Require(before.ActivePattern && !before.Stopped, "Gameplay input checks need a live practice trap.");
                int lane = (int)before.Pattern.Channel - 1;
                if (step == 2) lane = (lane + 1) % 3;
                float delta = step == 1
                    ? -(before.Pattern.SuccessWindowSeconds + before.Pattern.GoodWindowSeconds) * 0.5f : 0f;
                Click(Read<Button[]>(ui, "laneButtons")[lane], () =>
                {
                    // Place the isolated trap at its test timestamp, after rendering but before module input.
                    Write(core, "patternStartedAt", Time.realtimeSinceStartup + core.InputTimingOffsetSeconds
                        - before.Pattern.CoreSeconds - delta);
                    Write(app, "runEntryLaneInputBlockUntil", 0f);
                    Write(app, "runChoiceInputBlockUntil", 0f);
                    Method(app, "ResetRunLaneInputGuard").Invoke(app, null);
                });
                GreedLastRunSnapshot after = latest();
                Require(after.LastResult == expected[step],
                    "The visible lane must route through the app to its expected judgement: " + expected[step]);
                Require(after.SuccessCount + after.GoodCount + after.MissCount
                    == before.SuccessCount + before.GoodCount + before.MissCount + 1,
                    "Pointer down and release must resolve a gameplay trap exactly once.");
                Require(step == 2 ? after.Health < before.Health : after.Health == before.Health,
                    "Correct and Good lane input must preserve health; wrong-lane input must apply damage.");
                Require(step == 2 || after.Score > before.Score,
                    "A successful real lane callback must also grant its score reward.");
                foreach (bool held in Read<bool[]>(ui, "laneHoldActive"))
                    Require(!held, "Resolved lane release must not leave a pressed highlight behind.");
            }
            Require(latest().Stopped && latest().PracticeMode
                && latest().SuccessCount == 1 && latest().GoodCount == 1 && latest().MissCount == 1,
                "Three real gameplay inputs must finish the practice with one of each judgement.");
            Click(ui, "returnLobbyButton");
            Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                "A mixed practice result must return without creating a save candidate.");
            for (int slot = 0; slot < savedLabels.Length; slot++)
                Require(backend.LoadLobby().SaveSlotLabels[slot] == savedLabels[slot],
                    "Gameplay pointer checks must not consume a saved loadout.");
            Debug.Log("Greed Last gameplay lane loop passed: actual app routing -> exact, Good and wrong lane -> one judgement per press/release -> score, damage, released highlights and saved-slot isolation. Trap timestamps are synthetic.");
        }

        private static void VerifyRotationDragLifecycle(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            Click(ui, "normalRunButton");
            float clock = Time.realtimeSinceStartup;
            for (int cycle = 0; cycle < 4; cycle++)
                core.JumpToDevShortcut(GreedLastDevShortcut.AvoidanceCycle, clock);
            Write(core, "startCountdownEndsAt", 0f);
            core.Tick(clock);
            core.RequestNextPattern(clock);
            Require(latest().ActivePattern && GreedLastRunCore.IsRotationPattern(latest().Pattern),
                "Drag lifecycle checks require a live rotation corridor.");
            ArmRotationDrag(app);
            core.Tick(clock);
            Require(Read<bool>(app, "rotationDragTracking"),
                "A normal live rotation update must preserve an in-progress drag.");
            VerifyRotationViewportLifecycle(app, ui, core, latest);
            core.TogglePause(clock);
            RequireRotationDragReleased(app, "Pausing a rotation must discard the held mouse drag.");
            ArmRotationDrag(app);
            Method(app, "OnApplicationFocus").Invoke(app, new object[] { false });
            RequireRotationDragReleased(app, "Losing focus must discard a drag even when the core was already paused.");
            ArmRotationDrag(app);
            Method(app, "OnApplicationPause").Invoke(app, new object[] { true });
            RequireRotationDragReleased(app, "App suspension must discard its old mouse start position and direction.");
            Method(app, "OnApplicationFocus").Invoke(app, new object[] { true });
            Method(app, "OnApplicationPause").Invoke(app, new object[] { false });
            Require(ui.AcceptsPlayerInput, "The rotation fixture must restore foreground before its next player command.");
            core.TogglePause(clock);
            Write(core, "resumeCountdownEndsAt", 0f);
            core.Tick(clock);
            ArmRotationDrag(app);
            core.JumpToDevShortcut(GreedLastDevShortcut.GiftChoice, clock);
            RequireRotationDragReleased(app, "Leaving rotation for a choice must not carry the old drag into another corridor.");
            ArmRotationDrag(app);
            Click(ui, "returnLobbyButton");
            RequireRotationDragReleased(app, "Returning to the lobby must clear pending rotation feedback.");
            Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                "An interrupted rotation test must not create a successful-clear save candidate.");
            Debug.Log("Greed Last drag lifecycle passed: live tracking -> pause -> focus loss -> app suspension -> choice -> lobby cleanup.");
        }

        private static void VerifyRotationViewportLifecycle(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastRunCore core, Func<GreedLastRunSnapshot> latest)
        {
            MethodInfo sample = Method(ui, "ApplySafeAreaSample");
            var area = new Rect(120, 36, 2196, 1026);
            sample.Invoke(ui, new object[] { area, 2340, 1080, true });
            ArmRotationDrag(app);
            sample.Invoke(ui, new object[] { area, 2340, 1080, false });
            Require(Read<bool>(app, "rotationDragTracking"), "An unchanged viewport must not cancel a live gesture.");
            Write(app, "rotationTiltReady", true);
            Write(app, "rotationTiltAxisInitialized", true);
            Write(app, "rotationTiltFilteredAxis", 0.7f);
            Write(app, "rotationTiltHeldDirection", 1);
            Write(app, "rotationTiltCandidateDirection", 1);
            Write(app, "rotationTiltCandidateSince", 5f);
            GreedLastRunSnapshot before = latest();
            sample.Invoke(ui, new object[] { new Rect(24, 36, 2196, 1026), 2340, 1080, false });
            RequireRotationDragReleased(app, "Moving the safe area must discard the previous drag coordinate system.");
            Require(!Read<bool>(app, "rotationTiltReady") && !Read<bool>(app, "rotationTiltAxisInitialized")
                && Read<int>(app, "rotationTiltHeldDirection") == 0 && Read<int>(app, "rotationTiltCandidateDirection") == 0,
                "A viewport change must require a fresh neutral sensor sample, not confirm the old tilt.");
            Require(latest().Equals(before), "Viewport changes must not judge a trap, pause the core or consume run state.");
            ArmRotationDrag(app);
            sample.Invoke(ui, new object[] { new Rect(), 0, 0, true });
            Require(Read<bool>(app, "rotationDragTracking"), "Rejected viewport samples must not interrupt the gesture repeatedly.");
            sample.Invoke(ui, new object[] { new Rect(0, 0, 960, 540), 960, 540, false });
            RequireRotationDragReleased(app, "A window resize must also discard the old drag start.");
            Method(ui, "LateUpdate").Invoke(ui, null);
            ArmRotationDrag(app);
            core.Tick(Time.realtimeSinceStartup);
            Require(Read<bool>(app, "rotationDragTracking"), "A newly started drag must remain available after resize.");
            Debug.Log("Greed Last rotation viewport checks passed: unchanged gestures retained, resize/notch changes clear drag and tilt candidates, neutral rearm and core state preserved.");
        }

        private static void ArmRotationDrag(GreedLastApp app)
        {
            Write(app, "rotationDragTracking", true);
            Write(app, "rotationDragStartX", 100f);
            Write(app, "rotationDragVisualAxis", 0.5f);
            Write(app, "rotationDragHeldDirection", 1);
        }

        private static void RequireRotationDragReleased(GreedLastApp app, string message)
        {
            Require(!Read<bool>(app, "rotationDragTracking") && Read<float>(app, "rotationDragStartX") == 0f
                && Read<float>(app, "rotationDragVisualAxis") == 0f && Read<int>(app, "rotationDragHeldDirection") == 0,
                message);
        }

        internal static void RunToCompletion(GreedLastRunCore core, Func<GreedLastRunSnapshot> latest)
        {
            float clock = Time.realtimeSinceStartup;
            for (int step = 0; step < 100 && !latest().Stopped; step++)
            {
                GreedLastRunSnapshot run = latest();
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
                    clock += run.Pattern.CoreSeconds - core.InputTimingOffsetSeconds;
                    core.HandleInput(run.Pattern.Channel, clock);
                    Require(latest().LastResult == GreedLastJudgementResult.Success, "The callback fixture needs exact trap responses.");
                }
            }
            Require(latest().Stopped, "The bounded callback fixture must reach a terminal result.");
        }

        private static void RunToFailure(GreedLastRunCore core, Func<GreedLastRunSnapshot> latest)
        {
            float clock = Time.realtimeSinceStartup;
            for (int step = 0; step < 100 && !latest().Stopped; step++)
            {
                GreedLastRunSnapshot run = latest();
                if (run.ChoiceActive)
                    core.HandleInput(run.ChoiceKind == GreedLastChoiceKind.Node ? GreedLastRunChannel.Left : GreedLastRunChannel.Center, clock += 0.2f);
                else if (run.StartCountdownActive)
                {
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                }
                else if (!run.ActivePattern) core.RequestNextPattern(clock += 2f);
                else core.Tick(clock += Mathf.Max(0.02f, run.Pattern.HitSeconds + 0.2f - run.ElapsedSeconds));
            }
            Require(latest().Stopped && latest().Health == 0
                && latest().Phase == GreedLastRunPhase.FailResolving,
                "The bounded missed-input fixture must reach a natural health-zero failure.");
        }

        private static void VerifyMenuInputRippleCleanup(GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state)
        {
            var core = new GreedLastRunCore();
            GreedLastRunSnapshot live = default;
            core.SnapshotChanged += value => live = value;
            float clock = Time.realtimeSinceStartup;
            core.RestartPractice(clock);
            core.Tick(clock += 2f);
            core.Tick(clock += 1f);
            Require(live.ActivePattern, "Menu ripple cleanup needs a real live source snapshot.");
            foreach (GreedLastScreenState destination in new[] { GreedLastScreenState.SaveLoadoutDraft,
                GreedLastScreenState.InfiniteLoadoutSelect, GreedLastScreenState.InfiniteStartReady,
                GreedLastScreenState.LobbyReady, GreedLastScreenState.InfiniteRecordBoard,
                GreedLastScreenState.GrowthBoard, GreedLastScreenState.ProfileBoard })
            {
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(GreedLastScreenState.RunCoreTest, "주행", string.Empty, 0));
                ui.RenderRun(live);
                for (int lane = 0; lane < 3; lane++) ui.RecordLaneInput((GreedLastRunChannel)(lane + 1));
                Method(ui, "Update").Invoke(ui, null);
                Image[] ripples = Read<Image[]>(ui, "laneInputRipples");
                Require(Array.TrueForAll(ripples, ripple => ripple.color.a > 0f),
                    "The cleanup fixture must first display each live lane's actual input ripple.");
                ui.Render(GreedLastPrototypeSmokeCheck.BuildUiScreenSnapshot(destination, "메뉴", string.Empty, 0));
                Require(Array.TrueForAll(ripples, ripple => ripple.color.a == 0f),
                    "Non-run presentation must immediately hide live lane ripples: " + destination);
                Method(ui, "Update").Invoke(ui, null);
                Require(Array.TrueForAll(ripples, ripple => ripple.color.a == 0f),
                    "Menu animation must not restore a prior live lane ripple: " + destination);
            }
            state.SetLobbyReady(backend.LoadLobby());
            Debug.Log("Greed Last menu ripple cleanup passed: live lane presses disappear immediately and remain hidden across seven non-run destinations without changing the source snapshot or gameplay data.");
        }

        private static void VerifyRefillManagementCancellation(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            RunToFailure(core, latest);
            Click(ui, "returnLobbyButton");
            string review = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(state.CurrentDetail);
            Require(!string.IsNullOrEmpty(review), "A naturally failed refill must supply a management retry goal.");
            Click(ui, "saveLoadoutButton");
            Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                && state.CurrentDetail.Contains(review, StringComparison.Ordinal)
                && Read<string>(app, "pendingRefillRunReviewContext") == review,
                "The lobby refill-management entry must present and retain the actual failed-attempt goal.");
            Click(Read<Button[]>(ui, "laneButtons")[0]);
            Click(ui, "saveSlotDetailButton");
            Require(Read<string>(app, "pendingRefillRunReviewContext") == review,
                "Empty-slot detail and selection must not silently lose the pending refill goal.");
            Click(ui, "returnLobbyButton");
            Require(state.CurrentState == GreedLastScreenState.LobbyReady
                && string.IsNullOrEmpty(Read<string>(app, "pendingRefillRunReviewContext")),
                "Canceling refill management must clear its pending goal before returning to the lobby.");
            Click(ui, "saveLoadoutButton");
            Require(string.IsNullOrEmpty(Read<string>(app, "pendingRefillRunReviewContext")),
                "Opening fresh management after cancellation must not restore an unrelated previous result.");
            Click(ui, "nextPatternButton");
            Require(latest().RefillRun && latest().ChoiceActive && string.IsNullOrEmpty(latest().ReviewTargetText)
                && !backend.HasSaveLoadoutCandidate
                && Array.TrueForAll(backend.LoadLobby().SaveSlotOccupied, occupied => !occupied),
                "A canceled management review must return to a fresh refill without creating slots or a save opportunity.");
        }

        private static void VerifyRefillSaveAndReuse(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            int clearCount = Read<int>(backend, "normalClearCount");
            int infiniteCount = Read<int>(backend, "infiniteRunCount");
            for (int slot = 0; slot < 3; slot++)
            {
                Require(latest().RefillRun && latest().ChoiceActive && !backend.HasSaveLoadoutCandidate,
                    "Each refill round trip must begin at fresh authored starting choices.");
                RunToCompletion(core, latest);
                GreedLastRunRecord cleared = core.CreateRunRecord();
                GreedLastRunRecord expectedSaved = (GreedLastRunRecord)typeof(GreedLastMockBackend)
                    .GetMethod("BuildSaveLoadoutRecord", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { cleared });
                Require(latest().EscapeSucceeded && latest().SaveEligible && cleared.IsValid,
                    "The refill retry must finish a real saveable escape before opening its candidate.");
                Click(ui, "nextPatternButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                    && backend.HasSaveLoadoutCandidate && Read<bool>(app, "saveDraftFromRefillRun")
                    && Read<bool>(app, "saveDraftRequiresExplicitSlotChoice")
                    && Read<int>(backend, "normalClearCount") == clearCount + slot + 1,
                    "One refill clear must create one guarded candidate, not implicitly save or register twice.");
                Click(Read<Button[]>(ui, "laneButtons")[slot]);
                Click(ui, "nextPatternButton");
                GreedLastLobbySnapshot saved = backend.LoadLobby();
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                    && state.CurrentDetail.StartsWith("보충 완료 - S" + (slot + 1), StringComparison.Ordinal)
                    && !backend.HasSaveLoadoutCandidate && !Read<bool>(app, "saveDraftFromRefillRun")
                    && saved.SelectedSaveSlotIndex == slot && saved.SaveSlotOccupied[slot]
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2
                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(expectedSaved)
                    && string.IsNullOrEmpty(expectedSaved.Relic)
                    && Read<GreedLastRunRecord>(backend, "lastRunRecord").Equals(cleared)
                    && string.IsNullOrEmpty(Read<string>(app, "pendingRefillRunReviewContext")),
                    "Saving the chosen refill slot must consume its candidate once and open preparation without stale normal-review context.");
                for (int other = 0; other < 3; other++)
                    Require(saved.SaveSlotOccupied[other] == (other == slot),
                        "A refill save must populate only the explicitly selected slot.");

                FinishPreparationTransition(app);
                Click(Read<Button[]>(ui, "laneButtons")[0]);
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2,
                    "Canceling automatic refill preparation must leave both saved uses available.");
                Click(ui, "saveLoadoutButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                    && !backend.HasSaveLoadoutCandidate && !Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "Reopening management after refill saving must not revive the one-shot candidate.");
                Click(ui, "returnLobbyButton");

                for (int use = 0; use < 2; use++)
                {
                    Click(ui, "infiniteButton");
                    FinishPreparationTransition(app);
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    FinishPreparationTransition(app);
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    FinishPreparationTransition(app);
                    Require(Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2 - use,
                        "Refill gift and relic preparation must never spend a stored use.");
                    GreedLastRunRecord prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(expectedSaved,
                        Read<string>(app, "infinitePreparationGift"), Read<string>(app, "infinitePreparationRelic"));
                    GreedLastInfiniteLoadoutBonus bonus = (GreedLastInfiniteLoadoutBonus)typeof(GreedLastRunCore)
                        .GetMethod("CalculateInfiniteLoadoutBonus", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { prepared });
                    Click(ui, "nextPatternButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && latest().InfiniteMode
                        && !latest().Stopped && latest().Score == bonus.ScoreBonus && latest().Distance == 0f
                        && latest().SuccessCount == 0 && latest().GoodCount == 0 && latest().MissCount == 0
                        && !backend.HasSaveLoadoutCandidate
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 1 - use
                        && backend.LoadLobby().SaveSlotOccupied[slot] == (use == 0),
                        "Actual infinite confirmation must spend exactly one use and retain a fresh active run after second-use auto-deletion.");
                    RunToFailure(core, latest);
                    GreedLastInfiniteRunRecord result = core.CreateInfiniteRunRecord();
                    Require(result.IsValid, "A naturally failed refill-funded infinite run must retain its complete result.");
                    Click(ui, "returnLobbyButton");
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady
                        && Read<int>(backend, "infiniteRunCount") == infiniteCount + slot * 2 + use + 1
                        && Read<GreedLastInfiniteRunRecord>(backend, "lastInfiniteRunRecord").Equals(result)
                        && Read<int>(backend, "normalClearCount") == clearCount + slot + 1,
                        "Returning from each refill-funded infinite result must record it once without duplicating the normal clear.");
                }
                Require(Array.TrueForAll(backend.LoadLobby().SaveSlotOccupied, occupied => !occupied)
                    && !backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "Every refill slot must auto-empty after its second actual start and reject a third use.");
                Click(ui, "infiniteButton");
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                    && string.IsNullOrEmpty(Read<string>(app, "pendingRefillRunReviewContext")),
                    "Returning from an infinite result to empty preparation must not resurrect an old failed-refill goal.");
                FinishPreparationTransition(app);
                Click(ui, "nextPatternButton");
                Require(latest().RefillRun && latest().ChoiceActive && !latest().Stopped
                    && string.IsNullOrEmpty(latest().ReviewTargetText),
                    "Empty preparation must open the next fresh refill with no stale clear or failed-review goal.");
            }
            Debug.Log("Greed Last refill save/reuse loop passed: all three chosen slots, one candidate per real clear, automatic preparation cancel, no revived candidate, six naturally failed infinite results, two-use deletion and fresh empty-slot refill entry.");
        }

        private static void VerifyRefillAbandonReentry(GreedLastApp app, GreedLastRuntimeUi ui,
            GreedLastMockBackend backend, GreedLastStateMachine state, GreedLastRunCore core,
            Func<GreedLastRunSnapshot> latest)
        {
            foreach (int entryPath in new[] { 0, 1, 2, 3 })
            {
                bool viaPreparation = entryPath == 1 || entryPath == 2;
                float clock = Time.realtimeSinceStartup;
                core.HandleInput(GreedLastRunChannel.Center, clock += 0.2f);
                core.Tick(clock += 2f);
                core.Tick(clock += 1f);
                Require(latest().ActivePattern && latest().RefillRun,
                    "The abandoned refill fixture must enter an actual authored trap.");
                core.Tick(clock += latest().Pattern.HitSeconds + 0.2f - latest().ElapsedSeconds);
                Require(!latest().Stopped && latest().MissCount == 1,
                    "An abandoned refill must have a real attempt while retaining health.");
                Click(ui, "nextPatternButton");
                Require(latest().Paused, "Abandonment must first open the real clean pause menu.");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentDetail.StartsWith("보충 런 중단", StringComparison.Ordinal),
                    "A live refill exit must retain its abandoned outcome, not become a failed clear.");
                string review = GreedLastApp.BuildNormalRunOpeningContextFromLobbyDetail(state.CurrentDetail);
                Require(review.Contains("재도전 목표:", StringComparison.Ordinal),
                    "The recorded refill abandonment must supply its actual retry goal.");
                Click(ui, entryPath == 3 ? "saveLoadoutButton" : viaPreparation ? "infiniteButton" : "normalRunButton");
                if (entryPath == 3)
                {
                    Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                        && Read<string>(app, "pendingRefillRunReviewContext") == review,
                        "Opening the lobby refill-management action must preserve the abandoned refill goal.");
                    Click(Read<Button[]>(ui, "laneButtons")[2]);
                    Click(ui, "nextPatternButton");
                }
                if (viaPreparation)
                {
                    Require(state.CurrentDetail.Contains(review, StringComparison.Ordinal),
                        "Empty preparation must retain the abandoned refill review before the start action.");
                    FinishPreparationTransition(app);
                    if (entryPath == 2)
                    {
                        Click(ui, "saveLoadoutButton");
                        Require(state.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect
                            && Read<string>(app, "pendingRefillRunReviewContext") == review,
                            "Opening empty-slot management must preserve the pending refill review.");
                        Click(Read<Button[]>(ui, "laneButtons")[1]);
                        Require(Read<string>(app, "pendingRefillRunReviewContext") == review,
                            "Selecting another empty slot must not erase the refill retry goal.");
                    }
                    Click(ui, "nextPatternButton");
                }
                Require(latest().RefillRun && latest().ChoiceActive && !latest().Stopped
                    && latest().JudgementText.Contains(review, StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(latest().ReviewTargetText)
                    && latest().Score == 0 && latest().MissCount == 0
                    && !backend.HasSaveLoadoutCandidate
                    && Array.TrueForAll(backend.LoadLobby().SaveSlotOccupied, occupied => !occupied),
                    "Abandoned refill review must enter a fresh run through either lobby action without duplicating slots or progress.");
            }
            Debug.Log("Greed Last refill review re-entry passed: real health-zero failures and paused live abandonments retain goals through normal, empty-infinite preparation, empty-slot management and direct lobby refill management, fresh stats and no slot/candidate changes.");
        }

        private static void Click(GreedLastRuntimeUi ui, string field) => Click(Read<Button>(ui, field));
        private static void Click(Button button, Action beforePress = null)
        {
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), "The callback target must be visible and enabled: " + button.name);
            if (usePointerModule)
            {
                RefreshPointerViewport(button);
                EventSystem system = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                if (button is GreedLastCommandButton)
                {
                    GreedLastPrototypeKeyboardCheck.VerifyInterruptedAppCommand(pointerUi, system, button, useTouch);
                    canceledCommandPresses++;
                }
                GreedLastPrototypeKeyboardCheck.ClickOnce(system, button, useTouch, beforePress, () =>
                {
                    if (button is GreedLastCommandButton)
                    {
                        // Only the native-size fixture can redraw through Screen.safeArea without a real viewport change.
                        if (pointerSize.x == Screen.width && pointerSize.y == Screen.height && pointerInsets == Vector4.zero
                            && Read<bool>(pointerUi, "runModeActive") && Read<bool>(pointerUi, "hasRunSnapshot"))
                            pointerUi.RenderRun(Read<GreedLastRunSnapshot>(pointerUi, "latestRunSnapshot"));
                        RefreshPointerViewport(button);
                    }
                });
                pointerClicks++;
                return;
            }
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            beforePress?.Invoke();
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void RefreshPointerViewport(Button button)
        {
            // RenderRun samples the host window; restore the isolated target's size before its next input frame.
            Method(pointerUi, "ApplySafeAreaSample").Invoke(pointerUi, new object[]
            {
                new Rect(pointerInsets.x, pointerInsets.y,
                    pointerSize.x - pointerInsets.x - pointerInsets.z,
                    pointerSize.y - pointerInsets.y - pointerInsets.w),
                pointerSize.x, pointerSize.y, false,
            });
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in button.GetComponentInParent<Canvas>().GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            pointerCamera.Render();
        }

        private static void Bind(GreedLastRuntimeUi ui, GreedLastApp app, string field, string method)
            => Write(ui, field, Method(app, method).CreateDelegate(typeof(Action), app));
        private static MethodInfo Method(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Read<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Write(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void DestroyCreated(string name, GameObject existing)
        {
            GameObject created = GameObject.Find(name);
            if (existing == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }
    }
}
