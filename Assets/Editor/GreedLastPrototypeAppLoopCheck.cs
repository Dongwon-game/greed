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
                    if (use == 0)
                    {
                        Click(Read<Button[]>(ui, "laneButtons")[0]);
                        Click(ui, "returnLobbyButton");
                        Require(backend.LoadLobby().SaveSlotLabels[2] == before,
                            "Leaving gift preparation must not consume the selected loadout.");
                        Click(ui, "infiniteButton");
                    }
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Click(Read<Button[]>(ui, "laneButtons")[1]);
                    Require(Read<int>(app, "infinitePreparationStep") == 2,
                        "The gift and relic lane presses must both resolve before start confirmation.");
                    Require(backend.LoadLobby().SaveSlotLabels[2] == before,
                        "Gift and relic preparation callbacks must not consume a use before start confirmation.");
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
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                    "Confirming skip-save must discard the candidate and return to the lobby.");
                Debug.Log("Greed Last app callback loop passed: practice -> normal escape -> S3 -> preparation cancel -> two infinite uses -> records -> refill -> skip-save confirmation.");
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
