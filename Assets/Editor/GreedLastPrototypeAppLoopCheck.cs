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
