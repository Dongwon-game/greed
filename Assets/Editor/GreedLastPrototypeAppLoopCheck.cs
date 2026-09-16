using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypeAppLoopCheck
    {
        private const string PracticeKey = "GreedLast.Onboarding.PracticeCompleted";

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
                state.StateChanged += ui.Render;
                core.SnapshotChanged += (Action<GreedLastRunSnapshot>)Method(app, "HandleRunSnapshotChanged")
                    .CreateDelegate(typeof(Action<GreedLastRunSnapshot>), app);
                GreedLastRunSnapshot run = default;
                core.SnapshotChanged += value => run = value;
                PlayerPrefs.SetInt(PracticeKey, 0);
                state.SetLobbyReady(backend.LoadLobby());

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
                VerifyRotationDragLifecycle(app, ui, backend, state, core, () => run);
                Debug.Log("Greed Last app callback loop passed: practice -> normal escape -> S3 -> guarded preparation and reselection -> preparation cancel -> two infinite uses -> records -> refill -> guarded skip, rename, delete and overwrite confirmations.");
            }
            finally
            {
                if (hadPracticeKey) PlayerPrefs.SetInt(PracticeKey, practiceValue);
                else PlayerPrefs.DeleteKey(PracticeKey);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(host);
                if (canvasRoot != null) UnityEngine.Object.DestroyImmediate(canvasRoot);
                DestroyCreated("GreedLastCamera", existingCamera);
                DestroyCreated("GreedLastEventSystem", existingEvents);
            }
        }

        private static void FinishPreparationTransition(GreedLastApp app)
        {
            // Advance only the input guard, not wall time or the user's saved profile.
            Write(app, "infinitePreparationInputBlockUntil", 0f);
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
        private static void Click(Button button)
        {
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), "The callback target must be visible and enabled: " + button.name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
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
