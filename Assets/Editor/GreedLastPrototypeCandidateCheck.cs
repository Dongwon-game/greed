using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeCandidateCheck
    {
        [MenuItem("Greed Last/Run Repeated Clear Save Check")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Candidate checks require Edit mode.");
            int failures = 0;
            for (int slot = 0; slot < 3; slot++)
            {
                string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "repeated-clear-" + Guid.NewGuid().ToString("N");
                try
                {
                    var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                        "repeat-clear", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                    var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    backend.RegisterClear(record);
                    string firstToken = ReadToken(key);
                    Require(Guid.TryParseExact(firstToken, "N", out _), "A fresh clear must persist its canonical identity.");
                    backend.SelectSaveLoadoutSlot(slot);
                    backend.ConfirmSaveLoadout();
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate, "The first clear's saved candidate must remain consumed.");
                    backend.RegisterClear(record);
                    string secondToken = ReadToken(key);
                    Require(secondToken != firstToken, "A new clear must have a new identity even with the same statistics.");
                    Require(backend.HasSaveLoadoutCandidate, "A distinct new clear must offer its own one-time save.");
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    if (!backend.HasSaveLoadoutCandidate)
                    {
                        Debug.Log("Greed repeated clear mismatch: new clear with identical statistics lost its pending candidate after reload; first slot=" + slot);
                        failures++;
                        continue;
                    }
                    int next = (slot + 1) % 3;
                    backend.SelectSaveLoadoutSlot(next);
                    backend.ConfirmSaveLoadout();
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate && backend.LoadLobby().SaveSlotOccupied[slot]
                        && backend.LoadLobby().SaveSlotOccupied[next], "Two distinct clears may each save once without deleting the prior slot.");
                    backend.SelectSaveLoadoutSlot((slot + 2) % 3);
                    backend.ConfirmSaveLoadout();
                    Require(!backend.LoadLobby().SaveSlotOccupied[(slot + 2) % 3], "A consumed second clear must not produce a third copy.");
                    Require(ReadToken(key) == ReadField<string>(ReadData(key), "resolvedSaveLoadoutCandidateToken"),
                        "Saving must persist the same clear's resolved identity.");
                    foreach (int saved in new[] { slot, next })
                    {
                        backend.SelectSaveLoadoutSlot(saved);
                        backend.DeleteSelectedSaveLoadoutSlot();
                    }
                    MutateData(key, data => WriteField(data, "saveLoadoutCandidateAvailable", true));
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate, "An old available flag must not revive a resolved clear after its slots disappear.");
                    backend.RegisterClear(record);
                    backend.DiscardSaveLoadoutCandidate();
                    MutateData(key, data => WriteField(data, "saveLoadoutCandidateAvailable", true));
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(!backend.HasSaveLoadoutCandidate, "Discard must resolve its clear independently of occupied slots.");
                    backend.RegisterClear(record);
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.HasSaveLoadoutCandidate, "A later identical clear must remain eligible after the prior one was discarded.");
                }
                finally
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }
            Require(failures == 0, "Repeated clear candidates must survive reload independently of equal statistics. Failures=" + failures);
            VerifyCompatibility();
            RunRecoveredSlotChoice();
            Debug.Log("Greed repeated clear saves passed: nine isolated keys, three slot rotations/new identities, equal-stat reload/one save, preserved prior slots, resolved flag replay after deletion/discard, six legacy/malformed/case controls. Personal save keys untouched; not full-play, security or hardware testing.");
        }

        [MenuItem("Greed Last/Run Recovered Candidate Slot Choice Check")]
        public static void RunRecoveredSlotChoice()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Recovered candidate checks require Edit mode.");
            int failures = 0;
            foreach (string entry in new[] { "RequestSaveLoadout", "RequestNormalRun", "RequestInfiniteRun" })
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "recovered-choice-" + Guid.NewGuid().ToString("N");
                    GameObject host = new GameObject("GreedLastRecoveredChoiceCheck");
                    GameObject canvas = null;
                    GameObject existingCamera = GameObject.Find("GreedLastCamera");
                    GameObject existingEvents = GameObject.Find("GreedLastEventSystem");
                    try
                    {
                        var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                            "recovered-clear", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                        var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        int priorSlot = (slot + 1) % 3;
                        backend.RegisterClear(record);
                        backend.SelectSaveLoadoutSlot(priorSlot);
                        backend.ConfirmSaveLoadout();
                        backend.RegisterClear(record);
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        Require(backend.HasSaveLoadoutCandidate, "The distinct equal-stat clear must survive reload before checking its UI entry.");

                        var app = host.AddComponent<GreedLastApp>();
                        Require(ReadApp<GreedLastMockBackend>(app, "backend") == null, "The isolated app must not boot personal persistence.");
                        var ui = host.AddComponent<GreedLastRuntimeUi>();
                        GreedLastPrototypeSmokeCheck.InitializeRuntimeUiSmoke(ui, playerControls: true);
                        canvas = ReadApp<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                        var gate = new GreedLastRequestGate();
                        var state = new GreedLastStateMachine(gate);
                        var core = new GreedLastRunCore();
                        WriteApp(app, "backend", backend);
                        WriteApp(app, "requestGate", gate);
                        WriteApp(app, "stateMachine", state);
                        WriteApp(app, "runCore", core);
                        WriteApp(app, "runtimeUi", ui);
                        WriteApp(ui, "nextPatternRequested", AppMethod(app, "RequestNextRunPattern").CreateDelegate(typeof(Action), app));
                        WriteApp(ui, "runChannelRequested", AppMethod(app, "HandleRunChannelInput").CreateDelegate(typeof(Action<GreedLastRunChannel>), app));
                        state.StateChanged += (Action<GreedLastStateSnapshot>)AppMethod(app, "HandleScreenStateChanged")
                            .CreateDelegate(typeof(Action<GreedLastStateSnapshot>), app);
                        GreedLastStateSnapshot screen = default;
                        state.StateChanged += value => screen = value;
                        state.SetLobbyReady(backend.LoadLobby());
                        AppMethod(app, entry).Invoke(app, null);
                        Require(screen.State == GreedLastScreenState.SaveLoadoutDraft && screen.SaveLoadoutCandidateAvailable,
                            entry + ": a pending recovered candidate must open saving, not a new run or preparation.");
                        Button primary = ReadApp<Button>(ui, "nextPatternButton");
                        if (!screen.SaveSlotChoiceRequired || primary.IsInteractable())
                        {
                            Debug.Log("Greed recovered candidate mismatch: saving is unlocked before an explicit slot choice; entry=" + entry + ", requestedSlot=" + slot);
                            failures++;
                            continue;
                        }
                        AppMethod(app, "ConfirmSaveLoadoutAndReturn").Invoke(app, null);
                        AppMethod(app, "HandleRunChannelInput").Invoke(app, new object[] { GreedLastRunChannel.None });
                        Require(screen.SaveSlotChoiceRequired && backend.HasSaveLoadoutCandidate
                            && !backend.LoadSelectedSaveSlotView().SaveSlotOccupied[slot],
                            "A direct confirmation or invalid channel must not choose or save an automatic slot.");
                        Click(ReadApp<Button[]>(ui, "laneButtons")[slot]);
                        Require(!screen.SaveSlotChoiceRequired && primary.IsInteractable(), "A fresh explicit slot choice must enable saving.");
                        Click(primary);
                        GreedLastLobbySnapshot saved = backend.LoadLobby();
                        Require(screen.State == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                            && saved.SaveSlotOccupied[slot] && saved.SaveSlotOccupied[priorSlot]
                            && !saved.SaveSlotOccupied[(slot + 2) % 3], "The actual callbacks must save once into the chosen empty slot, preserving the older clear.");
                        Require(Array.TrueForAll(ReadField<int[]>(ReadData(key), "saveLoadoutUsesRemaining"), uses => uses == 0 || uses == 2),
                            "Opening, choosing and saving must not consume infinite-run uses.");
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        WriteApp(app, "backend", backend);
                        state.SetLobbyReady(backend.LoadLobby());
                        AppMethod(app, "RequestSaveLoadout").Invoke(app, null);
                        Require(!screen.SaveSlotChoiceRequired && !screen.SaveLoadoutCandidateAvailable
                            && !primary.IsInteractable(), "Reloading consumed storage must open management without a new save opportunity.");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(host);
                        if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                        DestroyCreated("GreedLastCamera", existingCamera);
                        DestroyCreated("GreedLastEventSystem", existingEvents);
                        PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.Save();
                    }
                }
            }
            Require(failures == 0, "Recovered candidates must require a fresh explicit choice across all three lobby entries. Failures=" + failures);
            Debug.Log("Greed recovered candidate slot choices passed: nine isolated reload/UI callback cases, three lobby entry routes x three chosen slots, pre-choice confirmation/invalid input blocked, chosen one-time save and older slot/use preservation, management after reload. No personal saves, full-play, physical input or hardware testing.");
        }

        [MenuItem("Greed Last/Run Recovered Candidate Slot Choice Check", true)]
        private static bool CanRunRecoveredSlotChoice() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static MethodInfo AppMethod(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static T ReadApp<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void WriteApp(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Click(Button button)
        {
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), "The callback target must be visible and enabled.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void DestroyCreated(string name, GameObject existing)
        {
            GameObject created = GameObject.Find(name);
            if (existing == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }

        private static void VerifyCompatibility()
        {
            foreach (string scenario in new[] { "legacy-pending", "legacy-saved", "malformed-pending", "malformed-saved", "case-pending", "case-saved" })
            {
                string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "candidate-compat-" + Guid.NewGuid().ToString("N");
                try
                {
                    bool saved = scenario.EndsWith("saved", StringComparison.Ordinal);
                    var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                        "compatible-clear", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                    var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    backend.RegisterClear(record);
                    string token = ReadToken(key);
                    if (saved) backend.ConfirmSaveLoadout();
                    MutateData(key, data =>
                    {
                        WriteField(data, "saveLoadoutCandidateAvailable", true);
                        if (scenario.StartsWith("case", StringComparison.Ordinal))
                        {
                            WriteField(data, "saveLoadoutCandidateToken", token.ToUpperInvariant());
                            WriteField(data, "resolvedSaveLoadoutCandidateToken", saved ? token.ToLowerInvariant() : string.Empty);
                        }
                        else
                        {
                            string value = scenario.StartsWith("legacy", StringComparison.Ordinal) ? string.Empty : "invalid-token";
                            WriteField(data, "saveLoadoutCandidateToken", value);
                            WriteField(data, "resolvedSaveLoadoutCandidateToken", value);
                        }
                    });
                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                    Require(backend.HasSaveLoadoutCandidate == !saved, scenario + ": pending legacy clear must survive, already saved clear must stay resolved.");
                    Require(ReadField<int>(ReadData(key), "version") == 1, "Optional identities must retain the existing data version.");
                    if (saved)
                    {
                        GreedLastLobbySnapshot lobby = backend.LoadLobby();
                        Require(lobby.SaveSlotOccupied[0] && lobby.SaveSlotUsable[0]
                            && ReadField<int[]>(ReadData(key), "saveLoadoutUsesRemaining")[0] == 2,
                            scenario + ": legacy duplicate repair must not delete the stored record or consume its uses.");
                    }
                    else
                    {
                        backend.SelectSaveLoadoutSlot(2);
                        backend.ConfirmSaveLoadout();
                        backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                        string current = ReadToken(key);
                        Require(!backend.HasSaveLoadoutCandidate && backend.LoadLobby().SaveSlotOccupied[2]
                            && Guid.TryParseExact(current, "N", out _) && current == current.ToLowerInvariant()
                            && current == ReadField<string>(ReadData(key), "resolvedSaveLoadoutCandidateToken"),
                            scenario + ": confirming an old pending candidate must persist one canonical resolved identity.");
                    }
                }
                finally
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }
        }

        private static object ReadData(string key)
            => JsonUtility.FromJson(PlayerPrefs.GetString(key), typeof(GreedLastMockBackend).GetNestedType("BackendSaveData", BindingFlags.NonPublic));

        private static T ReadField<T>(object data, string name) => (T)data.GetType().GetField(name).GetValue(data);
        private static void WriteField(object data, string name, object value) => data.GetType().GetField(name).SetValue(data, value);
        private static string ReadToken(string key) => ReadField<string>(ReadData(key), "saveLoadoutCandidateToken");

        private static void MutateData(string key, Action<object> change)
        {
            object data = ReadData(key);
            change(data);
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        [MenuItem("Greed Last/Run Repeated Clear Save Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
