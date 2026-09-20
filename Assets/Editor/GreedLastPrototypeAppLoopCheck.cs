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
        private static Camera pointerCamera;
        private static RenderTexture pointerTarget;
        private static GreedLastRuntimeUi pointerUi;
        private static Vector2Int pointerSize;
        private static Vector4 pointerInsets;
        public static int LastPointerClickCount => pointerClicks;
        public static int LastPointerCaseCount { get; private set; }

        [MenuItem(PointerMenuPath)]
        public static void RunPointerLoop()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !usePointerModule,
                "Pointer-loop checks require an isolated Edit-mode fixture.");
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                "The rendered pointer loop requires a graphics device; do not use -nographics.");
            pointerClicks = 0;
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
                + " clean-view layouts across practice, clear, save, preparation, two-use depletion, records and confirmations. Synthetic input and isolated backend only.");
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
                GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui);
                canvasRoot = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                if (usePointerModule)
                {
                    pointerUi = ui;
                    CreatePointerViewport(canvasRoot.GetComponent<Canvas>());
                    ui.ToggleDevCleanView();
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
                ui.ViewportChanged += (Action)Method(app, "HandleViewportChanged").CreateDelegate(typeof(Action), app);
                state.StateChanged += ui.Render;
                core.SnapshotChanged += (Action<GreedLastRunSnapshot>)Method(app, "HandleRunSnapshotChanged")
                    .CreateDelegate(typeof(Action<GreedLastRunSnapshot>), app);
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                PlayerPrefs.SetInt(PracticeKey, 0);
                VerifyBootAndReconnect(app, ui, backend, state, gate);

                Click(ui, "practiceRunButton");
                Require(run.PracticeMode && state.CurrentState == GreedLastScreenState.RunCoreTest,
                    "The actual lobby practice callback must open a practice run.");
                RunToCompletion(core, () => run);
                Require(run.Stopped && run.SuccessCount == 3 && !backend.HasSaveLoadoutCandidate,
                    "A perfect practice must complete three axes without creating a save candidate.");
                Require(Read<Button>(ui, "nextPatternButton").GetComponentInChildren<Text>().text == "일반 런으로",
                    "The first perfect practice result must offer direct normal-run entry.");
                Click(ui, "nextPatternButton");
                Require(!run.PracticeMode && run.ChoiceActive && PlayerPrefs.GetInt(PracticeKey) == 1,
                    "The practice result callback must mark onboarding and open normal starting choices.");
                Require(!Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "The former result button must not remain actionable over the new starting choice.");

                RunToCompletion(core, () => run);
                Require(run.EscapeSucceeded && run.SaveEligible, "The app-connected normal run must reach a saveable escape.");
                Click(ui, "nextPatternButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "The actual clear action must open explicit slot selection with one candidate.");
                Require(!Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "Saving must remain locked until a slot is explicitly chosen.");
                Method(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.None });
                Require(Read<bool>(app, "saveDraftRequiresExplicitSlotChoice"),
                    "An invalid channel must not implicitly select S3 and unlock saving.");
                Click(Read<Button[]>(ui, "laneButtons")[2]);
                Click(ui, "nextPatternButton");
                GreedLastLobbySnapshot saved = backend.LoadLobby();
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                    && saved.SaveSlotOccupied[2] && !saved.SaveSlotOccupied[0] && !saved.SaveSlotOccupied[1],
                    "The actual S3 button and confirmation must consume the candidate only into S3.");
                VerifyBootAndReconnect(app, ui, backend, state, gate);

                for (int use = 0; use < 2; use++)
                {
                    Click(ui, "infiniteButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady, "The lobby infinite action must open preparation.");
                    string before = backend.LoadLobby().SaveSlotLabels[2];
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Require(Read<int>(app, "infinitePreparationStep") == 0,
                        "The entry tap must not carry through into a preparation choice.");
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
                    Click(ui, "nextPatternButton");
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && run.InfiniteMode,
                        "Start confirmation must enter infinite mode through the app, not merely render its screen.");
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
                Click(ui, "returnLobbyButton");
                Click(ui, "normalRunButton");
                Require(!run.InfiniteMode && !run.Stopped && run.ChoiceActive,
                    "After depletion and record review the real lobby action must start the refill normal run.");
                RunToCompletion(core, () => run);
                Click(ui, "nextPatternButton");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "The first skip-save action must request confirmation while preserving the candidate.");
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate,
                    "A rapid second skip-save tap must preserve the candidate while confirmation appears.");
                FinishSaveConfirmation(app);
                Click(ui, "returnLobbyButton");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                    "Confirming skip-save must discard the candidate and return to the lobby.");
                VerifySavedSlotConfirmations(app, ui, backend, state);
                VerifyAppPauseLifecycle(app, ui, backend, state, core, () => run);
                VerifyGameplayLaneOutcomes(app, ui, backend, state, core, () => run);
                VerifyRotationDragLifecycle(app, ui, backend, state, core, () => run);
                Debug.Log("Greed Last app callback loop passed: practice -> normal escape -> S3 -> guarded preparation and reselection -> preparation cancel -> two infinite uses -> records -> refill -> guarded skip, rename, delete and overwrite confirmations.");
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

        private static void RunToCompletion(GreedLastRunCore core, Func<GreedLastRunSnapshot> latest)
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

        private static void Click(GreedLastRuntimeUi ui, string field) => Click(Read<Button>(ui, field));
        private static void Click(Button button, Action beforePress = null)
        {
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), "The callback target must be visible and enabled: " + button.name);
            if (usePointerModule)
            {
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
                EventSystem system = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                GreedLastPrototypeKeyboardCheck.ClickOnce(system, button, useTouch, beforePress);
                pointerClicks++;
                return;
            }
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            beforePress?.Invoke();
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
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
