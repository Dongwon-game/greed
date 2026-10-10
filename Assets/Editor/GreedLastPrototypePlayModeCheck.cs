using System;
using System.Collections;
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
    [InitializeOnLoad]
    public static class GreedLastPrototypePlayModeCheck
    {
        private const string Prefix = "GreedLast.EditorTests.PlayMode.";
        private const string BootScene = "Assets/Scenes/GreedLast_Boot.unity";
        private static readonly string DomainIdentity = Guid.NewGuid().ToString("N");
        private static readonly string[] PersonalKeys =
        {
            "GreedLast.Onboarding.PracticeCompleted"
        };

        static GreedLastPrototypePlayModeCheck()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.update += Watchdog;
        }

        [MenuItem("Greed Last/Run Engine Play Mode Lifecycle Check")]
        public static void Run()
        {
            BeginCheck(false);
        }

        [MenuItem("Greed Last/Run Engine Play Mode Full Loop Check")]
        public static void RunFullLoop()
        {
            BeginCheck(true);
        }

        [MenuItem("Greed Last/Run Engine Multi Touch Check")]
        public static void RunMultiTouch()
        {
            BeginCheck(false, true);
        }

        private static void BeginCheck(bool fullLoop, bool multiTouchOnly = false)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode
                && !SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false),
                "The engine lifecycle check requires an idle Edit-mode editor.");
            RequireAsyncBatchLifetime();
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene);
            Require(scene != null, "The existing prototype boot scene is required.");
            VerifyCanceledGate();
            SessionState.SetString(Prefix + "previousScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Prefix + "previousOptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Prefix + "previousOptions", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Prefix + "preferences", JsonUtility.ToJson(CapturePreferences()));
            SessionState.SetString(Prefix + "startedUtc", DateTime.UtcNow.ToString("O"));
            SessionState.SetString(Prefix + "error", string.Empty);
            SessionState.SetInt(Prefix + "cycle", 0);
            SessionState.SetString(Prefix + "reports", JsonUtility.ToJson(new Results()));
            SessionState.SetBool(Prefix + "fullLoop", fullLoop);
            SessionState.SetBool(Prefix + "multiTouchOnly", multiTouchOnly);
            SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, true);
            EditorSceneManager.playModeStartScene = scene;
            BeginCycle();
        }

        [MenuItem("Greed Last/Run Engine Play Mode Lifecycle Check", true)]
        [MenuItem("Greed Last/Run Engine Play Mode Full Loop Check", true)]
        [MenuItem("Greed Last/Run Engine Multi Touch Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode
            && !SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false);

        internal static void RequireAsyncBatchLifetime()
        {
            Require(!HasImmediateBatchQuit(Application.isBatchMode, Environment.GetCommandLineArgs()),
                "Asynchronous prototype checks must run without -quit; the check exits after writing its final report.");
        }

        private static bool HasImmediateBatchQuit(bool batchMode, string[] arguments)
            => batchMode && Array.Exists(arguments,
                value => string.Equals(value, "-quit", StringComparison.OrdinalIgnoreCase));

        internal static void VerifyAsyncBatchArguments()
        {
            Require(!HasImmediateBatchQuit(false, new[] { "-quit" })
                && !HasImmediateBatchQuit(true, Array.Empty<string>())
                && !HasImmediateBatchQuit(true, new[] { "-batchmode", "Logs/quit-check.log" }),
                "Interactive editors and self-exiting batch checks must remain usable.");
            Require(HasImmediateBatchQuit(true, new[] { "-batchmode", "-quit" })
                && HasImmediateBatchQuit(true, new[] { "-QUIT" }),
                "An immediate batch quit must be rejected before creating test resources or changing play settings.");
        }

        private static void BeginCycle()
        {
            int cycle = SessionState.GetInt(Prefix + "cycle", 0);
            EditorSettings.enterPlayModeOptions = cycle == 1
                ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
            EditorSettings.enterPlayModeOptionsEnabled = cycle == 1;
            SessionState.SetString(Prefix + "domainBeforeEntry", DomainIdentity);
            SessionState.SetString(Prefix + "deadline", DateTime.UtcNow.AddSeconds(
                SessionState.GetBool(Prefix + "fullLoop", false) ? 300 : 100).ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        internal static void VerifyCanceledGate()
        {
            var gate = new GreedLastRequestGate();
            foreach (GreedLastRequestKind kind in new[] { GreedLastRequestKind.BootInit,
                GreedLastRequestKind.ConnectCheck, GreedLastRequestKind.LobbySync, GreedLastRequestKind.ReSync })
            {
                Require(gate.TryBegin(kind, out GreedLastRequestToken canceled), "The cancellation check must acquire a request.");
                gate.CancelPending();
                gate.CancelPending();
                Require(!gate.Busy && !gate.IsCurrent(canceled), "Canceled requests must release their gate idempotently.");
                Require(gate.TryBegin(kind, out GreedLastRequestToken restarted) && restarted.RequestId > canceled.RequestId,
                    "A restarted request must retain a new monotonic identity.");
                gate.Complete(canceled);
                Require(gate.Busy && gate.IsCurrent(restarted), "A stale coroutine must not complete a restarted request.");
                gate.Complete(restarted);
                Require(!gate.Busy, "The restarted request must complete normally.");
            }
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var driver = new GameObject("GreedLastEngineLifecycleCheck").AddComponent<Driver>();
                UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                string error = SessionState.GetString(Prefix + "error", string.Empty);
                if (string.IsNullOrEmpty(error) && SessionState.GetInt(Prefix + "cycle", 0) == 0)
                {
                    SessionState.SetInt(Prefix + "cycle", 1);
                    EditorApplication.delayCall += BeginCycle;
                }
                else EditorApplication.delayCall += Finish;
            }
        }

        private static void Watchdog()
        {
            if (!SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false)) return;
            if (!DateTime.TryParse(SessionState.GetString(Prefix + "deadline", string.Empty),
                null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime deadline)
                || DateTime.UtcNow < deadline) return;
            SessionState.SetString(Prefix + "error", "Engine check exceeded its bounded cycle deadline.");
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish();
        }

        private static void Finish()
        {
            if (!SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false)) return;
            var results = JsonUtility.FromJson<Results>(SessionState.GetString(Prefix + "reports", "{}"));
            results.startedUtc = SessionState.GetString(Prefix + "startedUtc", string.Empty);
            results.completedUtc = DateTime.UtcNow.ToString("O");
            results.unityVersion = Application.unityVersion;
            results.fullLoopIncluded = SessionState.GetBool(Prefix + "fullLoop", false);
            results.multiTouchOnly = SessionState.GetBool(Prefix + "multiTouchOnly", false);
            results.multiTouchIncluded = results.fullLoopIncluded || results.multiTouchOnly;
            if (results.multiTouchOnly)
                results.scope = "Actual engine frames and EventSystem synthetic multi-touch: growth command repeat/held-release protection, six save-management held-confirmation paths, six held-command slot-change paths, first-input-only across three practice actions, held fingers across opening choice/countdown, fresh tap recovery and UI cleanup in two domain-reload modes. No manual Tick, clock edits, hardware touch, rendered-image, audio-latency or device FPS testing.";
            results.error = SessionState.GetString(Prefix + "error", string.Empty);
            var before = JsonUtility.FromJson<Preferences>(SessionState.GetString(Prefix + "preferences", "{}"));
            results.personalPreferencesUnchanged = JsonUtility.ToJson(before) == JsonUtility.ToJson(CapturePreferences());
            if (!results.personalPreferencesUnchanged) results.error += " Personal PlayerPrefs changed during the check.";
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Prefix + "previousOptions", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Prefix + "previousOptionsEnabled", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(Prefix + "previousScene", string.Empty));
            SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, false);
            results.succeeded = string.IsNullOrEmpty(results.error) && results.cycles.Count == 2;
            if (results.succeeded)
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.growthCommandCases == 3
                    && cycle.growthFirstCommandOnly && cycle.growthHeldReleaseCanceled && cycle.growthFreshTapRecovered
                    && cycle.growthEntryHeldTapCanceled && cycle.heldSaveConfirmationCases == 6
                    && cycle.heldSaveConfirmationsPreservedData && cycle.heldSaveConfirmationFreshTapRecovered
                    && cycle.heldSlotChangeCases == 6 && cycle.heldSlotChangesPreservedData && cycle.heldSlotChangeFreshTapRecovered);
                if (!results.succeeded) results.error = "Required engine input protection coverage did not complete.";
            }
            if (results.succeeded && results.multiTouchIncluded)
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.multiTouchPatternCases == 6
                    && cycle.multiTouchChoiceCases == 2 && cycle.multiTouchFirstInputOnly
                    && cycle.heldChoiceCannotAdvance && cycle.multiTouchFreshTapRecovered);
                if (results.succeeded && results.fullLoopIncluded)
                    results.succeeded = results.cycles.TrueForAll(cycle => cycle.consecutiveMultiTouchChoiceCases == 4
                        && cycle.multiTouchPreparationCases == 9 && cycle.multiTouchPreparationPreservedUses
                        && cycle.multiTouchConfirmationCases == 3 && cycle.multiTouchConfirmationsConsumedOnce
                        && cycle.liveBackPauseResume && cycle.heldPauseCommandCases == 2
                        && cycle.heldPauseCommandsPreservedRun && cycle.heldNormalClearPreservedResult
                        && cycle.heldClearDetailedControls == cycle.domainReloadDisabled
                        && cycle.clearBackSaveProtected && cycle.terminalBackSettlements == 2
                        && cycle.heldRunResultCases == 6 && cycle.heldRunResultsPreservedData && cycle.heldRunResultFreshTapRecovered
                        && cycle.heldPreparationStartCases == 3 && cycle.heldPreparationStartsPreservedUses
                        && cycle.registeredHeldCommandPresses == 21
                        && cycle.savedSlotIndex == (cycle.domainReloadDisabled ? 2 : 1));
                if (!results.succeeded) results.error = "Required engine multi-touch coverage did not complete.";
            }
            try
            {
                string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "PlayModeChecks");
                Directory.CreateDirectory(directory);
                results.archivedReport = "report-" + (results.multiTouchOnly ? "multi-touch-"
                    : results.fullLoopIncluded ? "full-loop-" : "lifecycle-")
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + ".json";
                string json = JsonUtility.ToJson(results, true);
                File.WriteAllText(Path.Combine(directory, results.archivedReport), json);
                File.WriteAllText(Path.Combine(directory, "latest-report.json"), json);
            }
            catch (Exception error) { results.succeeded = false; results.error += error; }
            if (results.succeeded) Debug.Log("Greed engine Play-mode check passed: two engine-driven cycles. Full clear/save/two-start loop="
                + results.fullLoopIncluded + ", multi-touch included=" + results.multiTouchIncluded
                + ", multi-touch only=" + results.multiTouchOnly + ". Personal settings and progress unchanged. See report scope for actual coverage.");
            else Debug.LogError("Greed engine Play-mode lifecycle check failed: " + results.error);
            if (Application.isBatchMode) EditorApplication.Exit(results.succeeded ? 0 : 1);
        }

        private static Preferences CapturePreferences()
        {
            var preferences = new Preferences();
            // Resolve actual production keys rather than trusting fixture names.
            var keys = new List<string>(PersonalKeys);
            string backendKey = (string)typeof(GreedLastMockBackend).GetField("PlayerPrefsKey",
                BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            keys.Add(backendKey);
            foreach (FieldInfo field in typeof(GreedLastRuntimeUi).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("PrefsKey", StringComparison.Ordinal))
                    keys.Add((string)field.GetRawConstantValue());
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
                preferences.values.Add(new Preference
                {
                    key = key, exists = PlayerPrefs.HasKey(key),
                    stringValue = PlayerPrefs.GetString(key, string.Empty),
                    intValue = PlayerPrefs.GetInt(key, int.MinValue),
                    floatValue = PlayerPrefs.GetFloat(key, float.MinValue)
                });
            return preferences;
        }

        [Serializable] private sealed class Preferences { public List<Preference> values = new List<Preference>(); }
        [Serializable] private sealed class Preference
        {
            public string key, stringValue;
            public bool exists;
            public int intValue;
            public float floatValue;
        }
        [Serializable] private sealed class Results
        {
            public string startedUtc, completedUtc, unityVersion, error, archivedReport;
            public string scope = "Actual engine lifecycle, core/UI/EventSystem frame progression and optional full game loop. Synthetic mouse/touch, focus/audio-callback simulation and a bounded 750ms main-thread stall; no manual core Tick, UI Update, request-draining or private clock edits. Calibration changes only in-memory fixture state. No OS audio reset, hardware input/capture, rendered-image, device FPS or audio-latency testing.";
            public bool succeeded, personalPreferencesUnchanged, fullLoopIncluded, multiTouchIncluded, multiTouchOnly;
            public List<Cycle> cycles = new List<Cycle>();
        }
        [Serializable] private sealed class Cycle
        {
            public bool domainReloadDisabled;
            public int growthCommandCases;
            public bool growthFirstCommandOnly, growthHeldReleaseCanceled, growthFreshTapRecovered;
            public bool growthEntryHeldTapCanceled;
            public int heldSaveConfirmationCases, terminalBackSettlements;
            public bool heldSaveConfirmationsPreservedData, heldSaveConfirmationFreshTapRecovered, clearBackSaveProtected;
            public bool liveBackPauseResume;
            public bool heldPauseCommandsPreservedRun;
            public int heldPauseCommandCases;
            public bool heldNormalClearPreservedResult, heldClearDetailedControls;
            public int heldSlotChangeCases;
            public bool heldSlotChangesPreservedData, heldSlotChangeFreshTapRecovered;
            public int heldRunResultCases;
            public bool heldRunResultsPreservedData, heldRunResultFreshTapRecovered;
            public int heldPreparationStartCases;
            public bool heldPreparationStartsPreservedUses;
            public int registeredHeldCommandPresses;
            public int frames, publications, judgements, lifecycleJudgements, pauseCycles;
            public float elapsedSeconds;
            public bool sceneReloadPreservedApp, restartAfterDestroy, uiDisabledHidesCanvas;
            public bool domainReloadObserved, externalResourcesPreserved, disabledAudioStopped;
            public bool inactiveHostPausedRun, interruptedBootRecovered;
            public bool disabledUiPausedRun, componentRemovalReleasedUi;
            public bool generatedAudioSourceReleased, externalAudioSourcePreserved, externalNavigationRestored;
            public int externalNavigationCases, externalNavigationSceneReloadCases;
            public int audioSourcesAfterComponentRemoval;
            public bool borrowedSceneReloadRecovered;
            public bool calibratedLateTouchAccepted, audioResetPreservedRun, audioResetCanceledHeldCommand;
            public bool audioResetInterruptedCountdown, disabledAudioResetRecovered;
            public int simulatedAudioConfigurationCallbacks;
            public bool blockedEngineFramePreservedRun;
            public int audioBackgroundOrderCases;
            public bool audioBackgroundOrdersPreservedRun;
            public int suspendedFreshInputCases, partialForegroundInputCases;
            public bool suspendedFreshInputPreservedRun, disabledAppBlockedNewInput;
            public bool releasedUiBlockedCommands, releasedUiDetachedCallbacks, releasedUiRestartRecovered;
            public bool releasedUiBootSafe;
            public int multiTouchPatternCases, multiTouchChoiceCases;
            public bool multiTouchFirstInputOnly, heldChoiceCannotAdvance, multiTouchFreshTapRecovered;
            public int consecutiveMultiTouchChoiceCases, multiTouchPreparationCases;
            public bool multiTouchPreparationPreservedUses;
            public int multiTouchConfirmationCases;
            public bool multiTouchConfirmationsConsumedOnce;
            public int engineMultiTouchBatches, engineTouchContacts;
            public int engineMouseClicks, engineTouchTaps, canceledEngineCommands;
            public int fullNormalJudgements, normalChoices, savedSlotIndex, actualInfiniteStarts;
            public bool fullNormalClear, candidateConsumedOnce, preparationCancelPreservedUses;
            public bool exhaustedSlotAutoCleared, infiniteNaturalFailures, emptySlotRefillReentered;
            public float fullLoopSeconds;
        }

        public sealed class Driver : MonoBehaviour
        {
            private readonly Cycle report = new Cycle();
            private string loggedError;
            private float startedAt;
            private GreedLastApp app;
            private GreedLastRuntimeUi ui;
            private GreedLastStateMachine state;
            private GreedLastRunCore core;
            private GreedLastRunSnapshot run;
            private bool completed;
            private GreedLastPrototypeKeyboardInput input;

            private void Start()
            {
                startedAt = Time.realtimeSinceStartup;
                report.domainReloadDisabled = SessionState.GetInt(Prefix + "cycle", 0) == 1;
                Application.logMessageReceived += CollectError;
                StartCoroutine(Guard(RunCycle()));
            }

            private void Update() { report.frames++; }

            private IEnumerator Guard(IEnumerator routine)
            {
                var routines = new Stack<IEnumerator>();
                routines.Push(routine);
                while (routines.Count > 0)
                {
                    object next;
                    try
                    {
                        Require(string.IsNullOrEmpty(loggedError), "Play mode emitted Unity errors: " + loggedError);
                        IEnumerator current = routines.Peek();
                        if (!current.MoveNext()) { routines.Pop(); continue; }
                        next = current.Current;
                        if (next is IEnumerator nested) { routines.Push(nested); continue; }
                    }
                    catch (Exception error) { Complete(error); yield break; }
                    yield return next;
                }
                Complete(null);
            }

            private IEnumerator RunCycle()
            {
                report.domainReloadObserved = DomainIdentity != SessionState.GetString(Prefix + "domainBeforeEntry", string.Empty);
                Require(report.domainReloadObserved != report.domainReloadDisabled,
                    "The check must observe a real domain reload on its first cycle and retained statics on its second.");
                float deadline = Time.realtimeSinceStartup + 10f;
                while (FindApp() == null) { Require(Time.realtimeSinceStartup < deadline, "Runtime initializer did not create the app."); yield return null; }
                Attach(FindApp());
                while (state.CurrentState != GreedLastScreenState.LobbyReady)
                { Require(Time.realtimeSinceStartup < deadline, "Real Start coroutine did not reach the lobby."); yield return null; }
                Require(!Read<bool>(Read<GreedLastMockBackend>(app, "backend"), "persistData"),
                    "The engine check must use a non-persisting backend before any commands.");
                GameObject canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                InvokeStatic(typeof(GreedLastApp), "Create");
                yield return null;
                Require(Count<GreedLastApp>() == 1 && CountNamed<Canvas>("GreedLastCanvas") == 1,
                    "Repeated runtime initialization must not create a second app or canvas.");

                var duplicate = new GameObject("GreedLastDuplicateAppCheck");
                duplicate.AddComponent<GreedLastApp>();
                yield return null;
                Require(Count<GreedLastApp>() == 1 && CountNamed<Canvas>("GreedLastCanvas") == 1,
                    "A duplicate scene app must not run a second Awake/backend/UI initialization.");

                EventSystem events = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                StandaloneInputModule module = events.GetComponent<StandaloneInputModule>();
                input = events.gameObject.AddComponent<GreedLastPrototypeKeyboardInput>();
                module.inputOverride = input;
                input.MouseEnabled = true;
                input.MousePosition = new Vector2(-1000f, -1000f);
                yield return null;
                yield return null;
                Require(module.IsActive() && !events.sendNavigationEvents,
                    "The actual engine EventSystem must own the test input without Submit/Move activation.");
                yield return VerifyGrowthCommands();
                yield return VerifyHeldSaveConfirmations();
                yield return VerifyHeldSlotChanges();
                if (SessionState.GetBool(Prefix + "fullLoop", false)) yield return VerifyHeldRunResults();
                if (SessionState.GetBool(Prefix + "multiTouchOnly", false))
                {
                    yield return VerifyMultiTouch();
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    Require(CountNamed<Canvas>("GreedLastCanvas") == 0 && Count<GreedLastApp>() == 0,
                        "The isolated multi-touch check must release its runtime UI and app.");
                    yield break;
                }
                Button practice = Read<Button>(ui, "practiceRunButton");
                yield return PressMouse(practice);
                Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                    "A command must wait for pointer release before opening practice.");
                ui.enabled = false;
                yield return null;
                ui.enabled = true;
                yield return ReleaseMouse();
                Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                    "A command held across actual UI disable/re-enable must not execute on release.");
                report.canceledEngineCommands++;
                yield return ClickMouse(practice);
                Require(state.CurrentState == GreedLastScreenState.RunCoreTest && run.PracticeMode,
                    "The real lobby practice callback must initialize the course.");
                deadline = Time.realtimeSinceStartup + 10f;
                while (run.CountdownActive || !run.ActivePattern)
                { Require(Time.realtimeSinceStartup < deadline, "Practice countdown did not advance through engine Update."); yield return null; }
                float before = run.ElapsedSeconds;
                float until = Time.realtimeSinceStartup + 0.25f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Require(run.ElapsedSeconds > before, "The actual app Update must advance the practice clock.");
                yield return VerifyHeldPauseCommands();
                yield return VerifyHeldPauseCommands(detailedControls: true);
                Call(app, "TryHandlePauseOrBackKeyboardInput", false, true);
                GreedLastRunSnapshot frozen = run;
                until = Time.realtimeSinceStartup + 0.4f;
                while (Time.realtimeSinceStartup < until) { AssertFrozen(frozen); yield return null; }
                Call(app, "TryHandlePauseOrBackKeyboardInput", true, false);
                Require(run.ResumeCountdownActive, "Resume must start the lead-in countdown.");
                deadline = Time.realtimeSinceStartup + 8f;
                while (run.Paused)
                { AssertFrozen(frozen); Require(Time.realtimeSinceStartup < deadline, "Engine-driven resume countdown did not finish."); yield return null; }
                report.pauseCycles++;
                report.liveBackPauseResume = true;
                Call(app, "RequestReturnToLobby");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                    "Leaving unfinished practice must return to the lobby without saving onboarding.");

                ui.enabled = false;
                yield return null;
                Require(!canvas.activeInHierarchy, "Disabling the UI component must hide its separately rooted canvas.");
                AudioSource audio = Read<AudioSource>(ui, "rhythmAudio");
                Require(audio.mute && !audio.isPlaying,
                    "The actual UI OnDisable callback must mute and stop its source.");
                report.disabledAudioStopped = true;
                ui.enabled = true;
                yield return null;
                Require(canvas.activeInHierarchy && CountNamed<Canvas>("GreedLastCanvas") == 1,
                    "Re-enabling UI must restore its original canvas without reconstruction.");
                report.uiDisabledHidesCanvas = true;

                GreedLastApp beforeReload = app;
                AsyncOperation reload = SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                while (!reload.isDone) yield return null;
                yield return null;
                Require(FindApp() == beforeReload && Count<GreedLastApp>() == 1
                    && canvas != null && canvas.activeInHierarchy && CountNamed<Canvas>("GreedLastCanvas") == 1,
                    "Loading the boot scene again must preserve one persistent app and its visible UI.");
                report.sceneReloadPreservedApp = true;

                state.SetInfiniteRun(string.Empty);
                core.EnterInfiniteRun();
                core.ToggleDevInvincible();
                deadline = Time.realtimeSinceStartup + 24f;
                bool focusedPause = false;
                bool hostPause = false;
                bool uiPause = false;
                float resumeAt = float.PositiveInfinity;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (!uiPause && run.ActivePattern && !run.Paused && report.judgements >= 1)
                    {
                        ui.enabled = false;
                        Require(run.Paused && !canvas.activeInHierarchy,
                            "Disabling only the live presentation must pause the app before hiding all playable controls.");
                        frozen = run;
                        until = Time.realtimeSinceStartup + 0.35f;
                        while (Time.realtimeSinceStartup < until) { AssertFrozen(frozen); yield return null; }
                        ui.enabled = true;
                        yield return null;
                        Require(run.Paused && canvas.activeInHierarchy,
                            "Restoring only the UI must reopen a pause menu, not lose health behind an invisible screen.");
                        Call(app, "RequestToggleRunPause");
                        Require(run.ResumeCountdownActive, "UI restoration must preserve the normal resume lead-in.");
                        uiPause = true;
                        report.disabledUiPausedRun = true;
                        report.pauseCycles++;
                    }
                    if (!hostPause && run.ActivePattern && !run.Paused && report.judgements >= 2)
                    {
                        app.gameObject.SetActive(false);
                        Require(run.Paused && !canvas.activeInHierarchy,
                            "Deactivating the app host must pause the run before its engine Update stops.");
                        frozen = run;
                        until = Time.realtimeSinceStartup + 0.35f;
                        while (Time.realtimeSinceStartup < until) { AssertFrozen(frozen); yield return null; }
                        app.gameObject.SetActive(true);
                        yield return null;
                        Require(run.Paused && canvas.activeInHierarchy,
                            "Reactivating the host must show its pause menu without consuming inactive time.");
                        Call(app, "RequestToggleRunPause");
                        Require(run.ResumeCountdownActive, "Host reactivation must retain the normal resume lead-in.");
                        hostPause = true;
                        resumeAt = float.PositiveInfinity;
                        report.inactiveHostPausedRun = true;
                        report.pauseCycles++;
                    }
                    if (!focusedPause && run.ActivePattern && !run.Paused && report.judgements >= 3)
                    {
                        Call(app, "OnApplicationFocus", false);
                        Call(ui, "OnApplicationFocus", false);
                        Require(run.Paused, "A focus-loss callback must pause the actual engine-driven app.");
                        frozen = run;
                        resumeAt = Time.realtimeSinceStartup + 0.35f;
                        focusedPause = true;
                    }
                    if (run.Paused)
                    {
                        AssertFrozen(frozen);
                        if (!run.ResumeCountdownActive && Time.realtimeSinceStartup >= resumeAt)
                        {
                            Call(ui, "OnApplicationFocus", true);
                            Call(app, "OnApplicationFocus", true);
                            Call(app, "RequestToggleRunPause");
                            Require(run.ResumeCountdownActive, "App focus return must not skip the resume lead-in.");
                            resumeAt = float.PositiveInfinity;
                        }
                    }
                    else if (run.ActivePattern && !run.CountdownActive
                        && run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds)
                    {
                        Button lane = Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1];
                        int beforeInput = report.judgements;
                        if (beforeInput % 2 == 0) yield return ClickMouse(lane);
                        else yield return TapTouch(lane);
                        Require(report.judgements == beforeInput + 1,
                            "One engine-processed lane input must create exactly one actual judgement, including release.");
                    }
                    Require(!run.Stopped, "The isolated invincible engine run must not terminate unexpectedly.");
                    yield return null;
                }
                Require(focusedPause && !run.Paused && report.judgements >= 5,
                    "Engine progression must cover focus pause/resume and multiple real-time judgements.");
                Require(report.engineMouseClicks >= 3 && report.engineTouchTaps >= 2
                    && report.canceledEngineCommands == 2 + report.heldPauseCommandCases * 2
                        + report.heldRunResultCases * 2 && report.growthEntryHeldTapCanceled,
                    "The real engine input module must cover mouse, touch, canceled growth entry and interrupted command release.");
                report.pauseCycles++;
                report.lifecycleJudgements = report.judgements;
                yield return VerifyCalibratedTouchAndAudioRecovery();
                yield return VerifyBlockedEngineFrame();
                yield return VerifyAudioBackgroundOrders();
                yield return VerifySuspendedFreshInput();
                if (SessionState.GetBool(Prefix + "fullLoop", false))
                {
                    yield return VerifyMultiTouch();
                    yield return VerifyFullLoop();
                }
                AudioClip ownedTone = Read<AudioClip>(ui, "beatClip");
                AudioSource ownedSource = Read<AudioSource>(ui, "rhythmAudio");
                GameObject formerHost = app.gameObject;
                AudioSource unrelatedSource = formerHost.AddComponent<AudioSource>();
                unrelatedSource.playOnAwake = false;
                UnityEngine.Object.Destroy(app);
                yield return null;
                yield return null;
                yield return null;
                Require(Count<GreedLastApp>() == 0 && CountNamed<Canvas>("GreedLastCanvas") == 0
                    && CountNamed<Camera>("GreedLastCamera") == 0
                    && CountNamed<UnityEngine.EventSystems.EventSystem>("GreedLastEventSystem") == 0
                    && ownedTone == null,
                    "Removing just the app component must also release its owned UI, camera, event system and audio.");
                Require(formerHost != null && formerHost.GetComponent<GreedLastRuntimeUi>() == null,
                    "Removing an app component must not destroy its host or leave a live orphaned UI component.");
                report.componentRemovalReleasedUi = true;
                report.audioSourcesAfterComponentRemoval = formerHost.GetComponents<AudioSource>().Length;
                report.generatedAudioSourceReleased = ownedSource == null && report.audioSourcesAfterComponentRemoval == 1;
                report.externalAudioSourcePreserved = unrelatedSource != null && unrelatedSource.gameObject == formerHost;
                UnityEngine.Object.Destroy(formerHost);
                yield return null;
                InvokeStatic(typeof(GreedLastApp), "Create");
                Attach(FindApp());
                deadline = Time.realtimeSinceStartup + 10f;
                while (!Read<GreedLastRequestGate>(app, "requestGate").Busy)
                { Require(Time.realtimeSinceStartup < deadline, "Restart boot did not acquire its request gate."); yield return null; }
                app.gameObject.SetActive(false);
                until = Time.realtimeSinceStartup + 0.15f;
                while (Time.realtimeSinceStartup < until) yield return null;
                app.gameObject.SetActive(true);
                while (state.CurrentState != GreedLastScreenState.LobbyReady)
                { Require(Time.realtimeSinceStartup < deadline, "An interrupted restart must not remain stuck on a canceled boot coroutine."); yield return null; }
                Require(Count<GreedLastApp>() == 1 && CountNamed<Canvas>("GreedLastCanvas") == 1,
                    "A clean restart must create exactly one new UI.");
                report.restartAfterDestroy = true;
                report.interruptedBootRecovered = true;

                yield return VerifyReleasedPresentation();

                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                yield return null;
                report.externalNavigationRestored = true;
                for (int settingsCase = 0; settingsCase < 3; settingsCase++)
                {
                    var externalCamera = new GameObject("GreedLastExternalCameraCheck", typeof(Camera), typeof(AudioListener));
                    var externalEvents = new GameObject("GreedLastExternalEventsCheck",
                        typeof(EventSystem), typeof(StandaloneInputModule));
                    UnityEngine.Object.DontDestroyOnLoad(externalCamera);
                    UnityEngine.Object.DontDestroyOnLoad(externalEvents);
                    EventSystem borrowedEvents = externalEvents.GetComponent<EventSystem>();
                    bool originalNavigation = settingsCase == 0;
                    borrowedEvents.sendNavigationEvents = originalNavigation;
                    InvokeStatic(typeof(GreedLastApp), "Create");
                    Attach(FindApp());
                    yield return null;
                    Require(Read<GameObject>(ui, "ownedCamera") == null && Read<GameObject>(ui, "ownedEventSystem") == null
                        && !borrowedEvents.sendNavigationEvents,
                        "An app must preserve external objects and isolate its keyboard commands while active.");
                    yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                    yield return null;
                    Require(externalCamera != null && externalEvents != null && !borrowedEvents.sendNavigationEvents
                        && Read<GameObject>(ui, "ownedCamera") == null && Read<GameObject>(ui, "ownedEventSystem") == null
                        && Count<Camera>() == 1 && Count<EventSystem>() == 1,
                        "Retained borrowed resources must remain external and unique after a scene reload.");
                    report.externalNavigationSceneReloadCases++;
                    if (settingsCase == 2) borrowedEvents.sendNavigationEvents = true;
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    Require(externalCamera != null && externalEvents != null && CountNamed<Canvas>("GreedLastCanvas") == 0,
                        "App teardown must preserve external camera/input objects and release only its own UI.");
                    report.externalResourcesPreserved = true;
                    report.externalNavigationRestored &= borrowedEvents.sendNavigationEvents == (settingsCase == 2 || originalNavigation);
                    report.externalNavigationCases++;
                    UnityEngine.Object.Destroy(externalCamera);
                    UnityEngine.Object.Destroy(externalEvents);
                    yield return null;
                }
                Require(report.generatedAudioSourceReleased && report.externalAudioSourcePreserved
                    && report.externalNavigationRestored,
                    "Teardown ownership mismatch: generated audio source released=" + report.generatedAudioSourceReleased
                        + ", unrelated host audio preserved=" + report.externalAudioSourcePreserved
                        + ", external navigation restored=" + report.externalNavigationRestored);
                yield return VerifyBorrowedSceneReload();
            }

            private IEnumerator VerifyCalibratedTouchAndAudioRecovery()
            {
                if (!run.Paused && !run.Stopped) Call(app, "RequestToggleRunPause");
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                float originalOffset = core.InputTimingOffsetSeconds;
                typeof(GreedLastRunCore).GetField("inputTimingOffsetSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(core, -0.2f);
                float deadline = Time.realtimeSinceStartup + 10f;
                while (!run.ActivePattern || run.ElapsedSeconds <= run.Pattern.HitSeconds + 0.004f)
                {
                    Require(!run.Stopped && run.MissCount == 0 && Time.realtimeSinceStartup < deadline,
                        "A calibrated engine touch must retain its real trap after the original impact deadline.");
                    yield return null;
                }
                GreedLastPatternModel pattern = run.Pattern;
                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[(int)pattern.Channel - 1]);
                Require(run.GoodCount == 1 && run.MissCount == 0 && run.LastResult == GreedLastJudgementResult.Good
                    && run.HasJudgedInputTiming && run.LastInputTimingDeltaSeconds > pattern.SuccessWindowSeconds
                    && run.LastInputTimingDeltaSeconds <= pattern.GoodWindowSeconds,
                    "The actual input module must accept a late calibrated Good that preceding Update ticks used to expire.");
                report.calibratedLateTouchAccepted = true;
                typeof(GreedLastRunCore).GetField("inputTimingOffsetSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(core, originalOffset);
                deadline = Time.realtimeSinceStartup + 10f;
                while (!run.ActivePattern)
                { Require(Time.realtimeSinceStartup < deadline, "The practice must continue after its calibrated Good."); yield return null; }

                AudioSource source = Read<AudioSource>(ui, "rhythmAudio");
                float volume = source.volume;
                var oldClips = Read<List<AudioClip>>(ui, "ownedAudioClips").ToArray();
                yield return PressMouse(Read<Button>(ui, "pauseButton"));
                Require(!run.Paused, "A held pause command must remain pending before audio interruption.");
                Call(ui, "HandleAudioConfigurationChanged", true);
                report.simulatedAudioConfigurationCallbacks++;
                Require(run.Paused && !run.ResumeCountdownActive && !source.isPlaying,
                    "An active audio reconfiguration must freeze the current trap and stop its obsolete voices.");
                GreedLastRunSnapshot frozen = run;
                yield return ReleaseMouse();
                yield return null;
                AssertFrozen(frozen);
                Require(run.Paused && !run.ResumeCountdownActive,
                    "The command held across audio interruption must not reopen gameplay on release.");
                report.audioResetCanceledHeldCommand = true;
                report.canceledEngineCommands++;
                foreach (AudioClip clip in oldClips) Require(clip == null, "Reconfigured Play-mode tones must be destroyed after the engine frame.");
                Require(Read<AudioSource>(ui, "rhythmAudio") == source && Mathf.Approximately(source.volume, volume)
                    && Read<List<AudioClip>>(ui, "ownedAudioClips").Count == 30,
                    "Actual Play-mode recovery must reuse its source and regenerate only its 30 tones.");
                yield return WaitSeconds(0.3f);
                AssertFrozen(frozen);
                report.audioResetPreservedRun = true;

                yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                Require(run.ResumeCountdownActive, "Audio recovery must use the ordinary resume countdown.");
                yield return WaitSeconds(0.2f);
                Call(ui, "HandleAudioConfigurationChanged", false);
                report.simulatedAudioConfigurationCallbacks++;
                Require(run.Paused && !run.ResumeCountdownActive,
                    "A second audio configuration callback must interrupt, not skip, an unfinished resume.");
                AssertFrozen(frozen);
                report.audioResetInterruptedCountdown = true;

                ui.enabled = false;
                oldClips = Read<List<AudioClip>>(ui, "ownedAudioClips").ToArray();
                Call(ui, "HandleAudioConfigurationChanged", true);
                Call(ui, "HandleAudioConfigurationChanged", false);
                report.simulatedAudioConfigurationCallbacks += 2;
                Require(Read<bool>(ui, "audioConfigurationPending") && source.mute,
                    "A disabled presentation must remember audio resets without rebuilding partial sounds.");
                yield return WaitSeconds(0.2f);
                AssertFrozen(frozen);
                foreach (AudioClip clip in oldClips) Require(clip != null, "Disabled audio resets must coalesce until re-enable.");
                ui.enabled = true;
                yield return null;
                yield return null;
                Require(!Read<bool>(ui, "audioConfigurationPending")
                    && Read<List<AudioClip>>(ui, "ownedAudioClips").Count == 30 && run.Paused,
                    "Actual OnEnable must restore one fresh tone set and keep its preserved pause menu.");
                foreach (AudioClip clip in oldClips) Require(clip == null, "Deferred obsolete tones must release on engine re-enable.");
                report.disabledAudioResetRecovered = true;
                yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                deadline = Time.realtimeSinceStartup + 8f;
                while (run.Paused)
                { AssertFrozen(frozen); Require(Time.realtimeSinceStartup < deadline, "Recovered countdown did not finish."); yield return null; }
                Require(run.Pattern.PatternId == frozen.Pattern.PatternId && run.ActivePattern && run.MissCount == frozen.MissCount
                    && run.ElapsedSeconds < frozen.ElapsedSeconds,
                    "Audio reconfiguration must resume the same trap with its existing lead-in, not timeout or replace it.");
            }

            private IEnumerator VerifyBlockedEngineFrame()
            {
                GreedLastRunSnapshot frozen = run;
                // A bounded main-thread stall, not a private clock edit or an OS/device test.
                System.Threading.Thread.Sleep(750);
                yield return null;
                Require(run.Paused && !run.ResumeCountdownActive && run.ActivePattern
                    && run.Pattern.PatternId == frozen.Pattern.PatternId
                    && Mathf.Abs(run.ElapsedSeconds - frozen.ElapsedSeconds) < 0.0001f
                    && run.MissCount == frozen.MissCount,
                    "A blocked engine frame must pause at its last presented trap time, not charge an unseen miss.");
                AssertFrozen(frozen);
                yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                float deadline = Time.realtimeSinceStartup + 8f;
                while (run.Paused)
                { AssertFrozen(frozen); Require(Time.realtimeSinceStartup < deadline, "Frame-interruption resume did not finish."); yield return null; }
                Require(run.ActivePattern && run.Pattern.PatternId == frozen.Pattern.PatternId
                    && run.ElapsedSeconds < frozen.ElapsedSeconds && run.MissCount == frozen.MissCount,
                    "A frame interruption must preserve the same trap and ordinary resume lead-in.");
                report.blockedEngineFramePreservedRun = true;
            }

            private IEnumerator VerifyAudioBackgroundOrders()
            {
                int[][] orders =
                {
                    new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                    new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
                };
                AudioSource source = Read<AudioSource>(ui, "rhythmAudio");
                float volume = source.volume;
                for (int phase = 0; phase < 2; phase++)
                {
                    for (int caseIndex = 0; caseIndex < orders.Length; caseIndex++)
                    {
                        Require(run.ActivePattern && !run.Paused && !run.Stopped,
                            "Combined interruptions must start from a real live engine trap.");
                        bool interruptCountdown = phase == 1;
                        if (interruptCountdown)
                        {
                            yield return ClickMouse(Read<Button>(ui, "pauseButton"));
                            yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                            yield return WaitSeconds(0.08f);
                            Require(run.ResumeCountdownActive, "The combined callback fixture must interrupt an actual countdown.");
                        }
                        else yield return PressMouse(Read<Button>(ui, "pauseButton"));
                        GreedLastRunSnapshot frozen = run;
                        AudioClip[] obsolete = Read<List<AudioClip>>(ui, "ownedAudioClips").ToArray();
                        bool uiFirst = caseIndex % 2 == 0;
                        foreach (int kind in orders[caseIndex])
                        {
                            if (kind == 0)
                            {
                                Call(ui, "HandleAudioConfigurationChanged", caseIndex % 2 == 0);
                                report.simulatedAudioConfigurationCallbacks++;
                            }
                            else
                            {
                                string method = kind == 1 ? "OnApplicationFocus" : "OnApplicationPause";
                                bool value = kind != 1;
                                if (uiFirst) { Call(ui, method, value); Call(app, method, value); }
                                else { Call(app, method, value); Call(ui, method, value); }
                            }
                        }
                        Require(run.Paused && !run.ResumeCountdownActive && source.mute && !source.isPlaying,
                            "Audio/focus/background ordering must cancel countdowns and keep background voices blocked.");
                        if (!interruptCountdown)
                        {
                            yield return ReleaseMouse();
                            report.canceledEngineCommands++;
                        }
                        yield return null;
                        yield return null;
                        AssertFrozen(frozen);
                        foreach (AudioClip clip in obsolete) Require(clip == null,
                            "Every combined reset must release its obsolete engine-owned tones.");
                        Require(Read<AudioSource>(ui, "rhythmAudio") == source
                            && Read<List<AudioClip>>(ui, "ownedAudioClips").Count == 30
                            && Mathf.Approximately(source.volume, volume),
                            "Combined recovery must retain one source, 30 fresh tones and the selected volume.");

                        // Foreground notifications are duplicated and reversed without an automatic resume.
                        for (int repeat = 0; repeat < 2; repeat++)
                        {
                            if (uiFirst)
                            {
                                Call(app, "OnApplicationPause", false);
                                Call(ui, "OnApplicationPause", false);
                                Call(ui, "OnApplicationFocus", true);
                                Call(app, "OnApplicationFocus", true);
                            }
                            else
                            {
                                Call(app, "OnApplicationFocus", true);
                                Call(ui, "OnApplicationFocus", true);
                                Call(ui, "OnApplicationPause", false);
                                Call(app, "OnApplicationPause", false);
                            }
                        }
                        yield return WaitSeconds(0.08f);
                        Require(run.Paused && !run.ResumeCountdownActive
                            && run.Pattern.PatternId == frozen.Pattern.PatternId && run.MissCount == frozen.MissCount,
                            "Duplicate foreground notifications must preserve the pause and the same trap, not resume a held command.");
                        AssertFrozen(frozen);
                        yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                        float deadline = Time.realtimeSinceStartup + 8f;
                        while (run.Paused)
                        {
                            AssertFrozen(frozen);
                            Require(Time.realtimeSinceStartup < deadline, "Combined recovery countdown did not finish.");
                            yield return null;
                        }
                        Require(run.ActivePattern && run.Pattern.PatternId == frozen.Pattern.PatternId
                            && run.MissCount == frozen.MissCount && run.ElapsedSeconds < frozen.ElapsedSeconds,
                            "Explicit recovery must retain the same trap with the ordinary lead-in after every callback order.");
                        report.audioBackgroundOrderCases++;
                    }
                }
                report.audioBackgroundOrdersPreservedRun = report.audioBackgroundOrderCases == 12;
            }

            private IEnumerator VerifySuspendedFreshInput()
            {
                GameObject canvas = Read<GameObject>(ui, "ownedCanvas");
                for (int phase = 0; phase < 2; phase++)
                {
                    if (phase == 0) yield return ClickMouse(Read<Button>(ui, "pauseButton"));
                    for (int source = 0; source < 4; source++)
                    {
                        if (phase == 1)
                        {
                            if (!run.Paused && !run.ChoiceActive && !run.Stopped)
                                yield return ClickMouse(Read<Button>(ui, "pauseButton"));
                            yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                            yield return ClickMouse(Read<Button>(ui, "normalRunButton"));
                            yield return WaitSeconds(0.4f);
                            Require(run.ChoiceActive, "Suspended choice input requires the actual normal-run choice screen.");
                        }
                        GreedLastRunSnapshot frozen = run;
                        GreedLastScreenState screen = state.CurrentState;
                        Button targetButton = phase == 0 ? Read<Button>(ui, "nextPatternButton")
                            : Read<Button[]>(ui, "laneButtons")[0];
                        object target = source % 2 == 0 ? (object)app : ui;
                        string callback = source < 2 ? "OnApplicationFocus" : "OnApplicationPause";
                        bool background = source >= 2;
                        Call(target, callback, background);
                        Require(!ui.AcceptsPlayerInput && !targetButton.IsInteractable()
                            && Read<AudioSource>(ui, "rhythmAudio").mute,
                            "Either app or UI suspension must lock the engine-owned controls immediately.");
                        yield return BlockedPointerPress(targetButton, source % 2 != 0, null);
                        Call(app, "RequestNextRunPattern");
                        Call(app, "RequestToggleRunPause");
                        Call(app, "HandleRunChannelInput", GreedLastRunChannel.Left);
                        Call(app, "RequestReturnToLobby");
                        AssertSuspendedCommandState(frozen, screen);
                        yield return BlockedPointerPress(targetButton, source % 2 != 0,
                            () => Call(target, callback, !background));
                        Require(ui.AcceptsPlayerInput, "A fully restored foreground must unlock the same controls.");
                        AssertSuspendedCommandState(frozen, screen);
                        if (phase == 1)
                        {
                            yield return TapTouch(targetButton);
                            Require(run.ChoicePrompt != frozen.ChoicePrompt || !run.ChoiceActive,
                                "A fresh foreground touch must still resolve a readable normal-run choice.");
                        }
                        report.suspendedFreshInputCases++;
                    }
                }

                if (!run.Paused && !run.ChoiceActive && !run.Stopped)
                    yield return ClickMouse(Read<Button>(ui, "pauseButton"));
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                yield return ClickMouse(Read<Button>(ui, "pauseButton"));
                GreedLastRunSnapshot paused = run;
                Button resume = Read<Button>(ui, "nextPatternButton");
                for (int order = 0; order < 4; order++)
                {
                    Call(app, "OnApplicationFocus", false);
                    Call(ui, "OnApplicationFocus", false);
                    Call(app, "OnApplicationPause", true);
                    Call(ui, "OnApplicationPause", true);
                    if (order % 2 == 0) Call(app, "OnApplicationFocus", true);
                    else Call(ui, "OnApplicationPause", false);
                    Require(!ui.AcceptsPlayerInput, "One foreground notification must not unlock another suspended source.");
                    yield return BlockedPointerPress(resume, order < 2, null);
                    AssertSuspendedCommandState(paused, state.CurrentState);
                    Call(app, "OnApplicationFocus", true);
                    Call(ui, "OnApplicationFocus", true);
                    Call(app, "OnApplicationPause", false);
                    Call(ui, "OnApplicationPause", false);
                    Require(ui.AcceptsPlayerInput && run.Paused && !run.ResumeCountdownActive,
                        "All foreground sources must restore input without automatically resuming the run.");
                    report.partialForegroundInputCases++;
                }
                app.enabled = false;
                yield return null;
                Require(!ui.AcceptsPlayerInput && canvas.activeInHierarchy
                    && Read<AudioSource>(ui, "rhythmAudio").mute,
                    "A disabled app component must lock its still-visible UI.");
                yield return BlockedPointerPress(resume, false, null);
                Call(app, "RequestToggleRunPause");
                AssertSuspendedCommandState(paused, state.CurrentState);
                app.enabled = true;
                yield return null;
                Require(ui.AcceptsPlayerInput, "Re-enabling the app must restore input on its existing UI.");
                yield return ClickMouse(resume);
                Require(run.ResumeCountdownActive, "A fresh post-disable click must start the normal countdown.");
                report.disabledAppBlockedNewInput = true;
                report.suspendedFreshInputPreservedRun = report.suspendedFreshInputCases == 8
                    && report.partialForegroundInputCases == 4;
                Call(app, "RequestReturnToLobby");
                state.SetInfiniteRun(string.Empty);
                core.EnterInfiniteRun();
                if (!run.DevInvincible) core.ToggleDevInvincible();
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!run.ActivePattern || run.CountdownActive)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Suspended-input checks must restore a live engine run.");
                    yield return null;
                }
            }

            private void AssertSuspendedCommandState(GreedLastRunSnapshot frozen, GreedLastScreenState screen)
            {
                AssertFrozen(frozen);
                Require(run.Paused == frozen.Paused && !run.ResumeCountdownActive
                    && run.ChoiceActive == frozen.ChoiceActive && run.ChoicePrompt == frozen.ChoicePrompt
                    && state.CurrentState == screen,
                    "Blocked new input must not resume, choose, navigate, or alter the engine run: paused="
                    + run.Paused + "/" + frozen.Paused + ", countdown=" + run.ResumeCountdownActive
                    + ", choice=" + run.ChoiceActive + "/" + frozen.ChoiceActive
                    + ", screen=" + state.CurrentState + "/" + screen);
            }

            private IEnumerator BlockedPointerPress(Button button, bool touch, Action restoreWhileHeld)
            {
                Require(button.isActiveAndEnabled && !button.IsInteractable(),
                    "The negative engine-pointer fixture must target a visible suspended control.");
                Vector2 point = ScreenPoint(button);
                input.MouseEnabled = !touch;
                input.Touches = touch ? new[] { new Touch { fingerId = 72, position = point, phase = TouchPhase.Began } }
                    : Array.Empty<Touch>();
                input.MousePosition = point;
                input.MouseDown = input.MouseHeld = !touch;
                yield return null;
                input.MouseDown = false;
                if (touch) input.Touches = new[] { new Touch { fingerId = 72, position = point, phase = TouchPhase.Stationary } };
                restoreWhileHeld?.Invoke();
                yield return null;
                if (touch) input.Touches = new[] { new Touch { fingerId = 72, position = point, phase = TouchPhase.Ended } };
                else { input.MouseHeld = false; input.MouseUp = true; }
                yield return null;
                input.Touches = Array.Empty<Touch>();
                input.MouseUp = false;
                yield return null;
                report.canceledEngineCommands++;
            }

            private IEnumerator VerifyBorrowedSceneReload()
            {
                var sceneCamera = new GameObject("GreedLastBorrowedSceneCamera", typeof(Camera), typeof(AudioListener));
                var sceneEvents = new GameObject("GreedLastBorrowedSceneEvents", typeof(EventSystem), typeof(StandaloneInputModule));
                InvokeStatic(typeof(GreedLastApp), "Create");
                Attach(FindApp());
                float deadline = Time.realtimeSinceStartup + 10f;
                while (state.CurrentState != GreedLastScreenState.LobbyReady)
                { Require(Time.realtimeSinceStartup < deadline, "Borrowed-scene app did not reach its lobby."); yield return null; }
                Require(Read<GameObject>(ui, "ownedCamera") == null && Read<GameObject>(ui, "ownedEventSystem") == null,
                    "The scene-reload fixture must initially borrow scene-owned resources.");
                GreedLastApp originalApp = app;
                GreedLastRuntimeUi originalUi = ui;
                yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                yield return null;
                yield return null;
                Require(sceneCamera == null && sceneEvents == null && FindApp() == originalApp && ui == originalUi,
                    "A scene reload must remove borrowed scene resources but preserve the app and its UI.");
                Require(Count<Camera>() == 1 && Count<AudioListener>() == 1 && Count<EventSystem>() == 1,
                    "Persistent UI must recover one camera/listener/input system after its borrowed scene resources unload.");
                EventSystem events = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                StandaloneInputModule module = events.GetComponent<StandaloneInputModule>();
                input = events.gameObject.AddComponent<GreedLastPrototypeKeyboardInput>();
                module.inputOverride = input;
                input.MouseEnabled = true;
                input.MousePosition = new Vector2(-1000f, -1000f);
                yield return null;
                yield return null;
                Require(module.IsActive() && !events.sendNavigationEvents,
                    "Recovered scene input must retain engine-owned pointer dispatch without duplicate keyboard navigation.");
                yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                Require(state.CurrentState == GreedLastScreenState.RunCoreTest && run.PracticeMode,
                    "The recovered EventSystem must open practice through an actual engine mouse click.");
                yield return TapTouch(Read<Button>(ui, "pauseButton"));
                Require(run.Paused, "Recovered engine touch must open the real practice pause menu.");
                GreedLastRunSnapshot frozen = run;
                for (int reloadIndex = 0; reloadIndex < 2; reloadIndex++)
                {
                    yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                    yield return null;
                    AssertFrozen(frozen);
                    Require(FindApp() == originalApp && ui == originalUi
                        && Count<Camera>() == 1 && Count<AudioListener>() == 1 && Count<EventSystem>() == 1,
                        "Repeated scene loads must preserve one paused app and one set of recovered resources.");
                }
                yield return TapTouch(Read<Button>(ui, "returnLobbyButton"));
                Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                    "An actual engine touch must still return from practice after borrowed resources unload.");
                report.borrowedSceneReloadRecovered = true;
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                yield return null;
                Require(Count<Camera>() == 0 && Count<AudioListener>() == 0 && Count<EventSystem>() == 0,
                    "Recovered resources must be owned by the app and released on final teardown.");
            }

            private IEnumerator VerifyGrowthCommands()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                try
                {
                    for (int scenario = 0; scenario < 3; scenario++)
                    {
                        var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                        backend.AddGrowthPointsForTest(9);
                        if (scenario == 0) backend.AddSampleSaveLoadoutForTest(out _);
                        backendField.SetValue(app, backend);
                        state.SetLobbyReady(backend.LoadLobby());
                        if (scenario == 0)
                        {
                            yield return null;
                            Vector2 entry = ScreenPoint(Read<Button>(ui, "growthButton"));
                            yield return TouchFrame(TouchAt(83, entry, TouchPhase.Began), TouchAt(84, entry, TouchPhase.Began));
                            yield return TouchFrame(TouchAt(83, entry, TouchPhase.Stationary), TouchAt(84, entry, TouchPhase.Stationary));
                            yield return TouchFrame(TouchAt(83, entry, TouchPhase.Ended), TouchAt(84, entry, TouchPhase.Stationary));
                            Require(state.CurrentState == GreedLastScreenState.GrowthBoard,
                                "The first entry finger must open the actual growth screen.");
                            Vector2 command = ScreenPoint(Read<Button>(ui, "nextPatternButton"));
                            yield return TouchFrame(TouchAt(84, command, TouchPhase.Moved));
                            yield return TouchFrame(TouchAt(84, command, TouchPhase.Stationary));
                            yield return WaitSeconds(0.45f);
                            yield return TouchFrame(TouchAt(84, command, TouchPhase.Ended));
                            yield return TouchFrame();
                            GreedLastLobbySnapshot entered = backend.LoadLobby();
                            Require(entered.GrowthPoints == 9
                                && entered.GrowthSurvivalLevel + entered.GrowthFocusLevel + entered.GrowthTimingLevel == 0,
                                "Moving and releasing an old entry finger over growth must not purchase anything.");
                            report.growthEntryHeldTapCanceled = true;
                            report.canceledEngineCommands++;
                        }
                        else Call(app, "RequestGrowthBoard");
                        yield return null;
                        Button primary = Read<Button>(ui, "nextPatternButton");
                        Button[] lanes = Read<Button[]>(ui, "laneButtons");
                        Vector2 firstPoint = ScreenPoint(scenario == 2 ? lanes[0] : primary);
                        Vector2 secondPoint = ScreenPoint(scenario == 2 ? lanes[2] : primary);
                        if (scenario == 0) yield return ClickMouse(primary);
                        else
                        {
                            yield return TouchFrame(TouchAt(73, firstPoint, TouchPhase.Began), TouchAt(74, secondPoint, TouchPhase.Began));
                            if (scenario == 1)
                                yield return TouchFrame(TouchAt(73, firstPoint, TouchPhase.Ended), TouchAt(74, secondPoint, TouchPhase.Stationary));
                        }
                        GreedLastLobbySnapshot first = backend.LoadLobby();
                        Require(first.GrowthPoints == 8
                            && first.GrowthSurvivalLevel + first.GrowthFocusLevel + first.GrowthTimingLevel == 1,
                            "The first engine growth command must spend the existing one-point cost once.");
                        if (scenario == 0)
                        {
                            Require(Time.realtimeSinceStartup - Read<float>(app, "lastGrowthUpgradeAt") < 0.2f,
                                "The engine rapid-repeat fixture must still be inside the existing growth guard.");
                            yield return ClickMouse(primary);
                        }
                        // Began and Ended last one frame; only held fingers remain during the wait.
                        if (scenario == 1)
                            yield return TouchFrame(TouchAt(74, secondPoint, TouchPhase.Stationary));
                        else if (scenario == 2)
                            yield return TouchFrame(TouchAt(73, firstPoint, TouchPhase.Stationary), TouchAt(74, secondPoint, TouchPhase.Stationary));
                        yield return WaitSeconds(0.45f);
                        if (scenario == 1) yield return TouchFrame(TouchAt(74, secondPoint, TouchPhase.Ended));
                        else if (scenario == 2)
                            yield return TouchFrame(TouchAt(73, firstPoint, TouchPhase.Ended), TouchAt(74, secondPoint, TouchPhase.Ended));
                        if (scenario != 0) yield return TouchFrame();
                        GreedLastLobbySnapshot repeated = backend.LoadLobby();
                        Require(repeated.GrowthPoints == first.GrowthPoints
                            && repeated.GrowthSurvivalLevel == first.GrowthSurvivalLevel
                            && repeated.GrowthFocusLevel == first.GrowthFocusLevel
                            && repeated.GrowthTimingLevel == first.GrowthTimingLevel,
                            "A rapid repeat or old held finger released after the guard must not purchase again.");
                        yield return ClickMouse(scenario == 2 ? lanes[2] : primary);
                        GreedLastLobbySnapshot fresh = backend.LoadLobby();
                        Require(fresh.GrowthPoints == 7
                            && fresh.GrowthSurvivalLevel + fresh.GrowthFocusLevel + fresh.GrowthTimingLevel == 2,
                            "A fresh engine growth tap after release and the guard must still purchase once.");
                        report.growthCommandCases++;
                    }
                    report.growthFirstCommandOnly = report.growthCommandCases == 3;
                    report.growthHeldReleaseCanceled = report.growthFirstCommandOnly;
                    report.growthFreshTapRecovered = report.growthFirstCommandOnly;
                    Debug.Log("Greed engine growth protection passed: rapid changed recommendations, two held command fingers and two lane fingers; stale releases cannot spend and fresh taps recover. In-memory points only.");
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                }
            }

            private IEnumerator VerifyHeldSaveConfirmations()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                string[] pendingFields = { "saveDraftRequiresExplicitSlotChoice", "saveDraftOverwriteConfirmPending",
                    "saveDraftSkipConfirmPending", "saveSlotDeleteConfirmPending", "saveSlotRenameConfirmPending",
                    "saveSlotDetailViewActive", "saveDraftFromRefillRun" };
                void ResetPending()
                {
                    foreach (string field in pendingFields)
                        typeof(GreedLastApp).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, false);
                }
                try
                {
                    foreach (var scenario in new[] {
                        (Infinite: false, Button: "nextPatternButton", Flag: "saveDraftOverwriteConfirmPending"),
                        (Infinite: false, Button: "saveSlotDeleteButton", Flag: "saveSlotDeleteConfirmPending"),
                        (Infinite: true, Button: "saveSlotDeleteButton", Flag: "saveSlotDeleteConfirmPending"),
                        (Infinite: false, Button: "saveSlotRenameButton", Flag: "saveSlotRenameConfirmPending"),
                        (Infinite: true, Button: "saveSlotRenameButton", Flag: "saveSlotRenameConfirmPending"),
                        (Infinite: false, Button: "returnLobbyButton", Flag: "saveDraftSkipConfirmPending") })
                    {
                        var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                        backend.AddSampleSaveLoadoutForTest(out _);
                        backend.AddSampleClearCandidateForTest(out _);
                        backend.SelectSaveLoadoutSlot(0);
                        backendField.SetValue(app, backend);
                        ResetPending();
                        if (scenario.Infinite)
                            state.SetInfiniteLoadoutSelect(backend.LoadSelectedSaveSlotView(), backend.BuildInfiniteLoadoutSelectText());
                        else state.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSaveLoadoutDraftText(true),
                            saveLoadoutCandidateAvailable: true);
                        yield return null;
                        Button command = Read<Button>(ui, scenario.Button);
                        Require(command.isActiveAndEnabled && command.IsInteractable(),
                            "Engine held confirmation requires a visible usable command: " + scenario.Button);
                        var records = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
                        var uses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
                        var names = (int[])Read<int[]>(backend, "saveLoadoutRenameSteps").Clone();
                        Vector2 point = ScreenPoint(command);
                        yield return TouchFrame(TouchAt(91, point, TouchPhase.Began), TouchAt(92, point, TouchPhase.Began));
                        yield return TouchFrame(TouchAt(91, point, TouchPhase.Ended), TouchAt(92, point, TouchPhase.Stationary));
                        Require(Read<bool>(app, scenario.Flag) && backend.HasSaveLoadoutCandidate,
                            "The first engine release must open only the confirmation: " + scenario.Flag);
                        yield return TouchFrame(TouchAt(92, point, TouchPhase.Stationary));
                        yield return WaitSeconds(0.45f);
                        yield return TouchFrame(TouchAt(92, point, TouchPhase.Ended));
                        yield return TouchFrame();
                        Require(Read<bool>(app, scenario.Flag) && backend.HasSaveLoadoutCandidate,
                            "The old engine finger released after the guard must not confirm: " + scenario.Flag);
                        for (int slot = 0; slot < records.Length; slot++)
                            Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(records[slot])
                                && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == uses[slot]
                                && Read<int[]>(backend, "saveLoadoutRenameSteps")[slot] == names[slot],
                                "The canceled engine confirmation must preserve every record, use and name: " + scenario.Flag);
                        yield return TapTouch(command);
                        Require(!Read<bool>(app, scenario.Flag),
                            "A fresh engine tap must confirm normally after the canceled old finger: " + scenario.Flag);
                        if (scenario.Flag == "saveSlotDeleteConfirmPending")
                            Require(!backend.LoadLobby().SaveSlotOccupied[0] && backend.HasSaveLoadoutCandidate,
                                "Fresh engine delete must clear only its saved slot.");
                        else if (scenario.Flag == "saveSlotRenameConfirmPending")
                            Require(Read<int[]>(backend, "saveLoadoutRenameSteps")[0] == names[0] + 1
                                && Read<int[]>(backend, "saveLoadoutUsesRemaining")[0] == uses[0]
                                && backend.HasSaveLoadoutCandidate, "Fresh engine rename must rename once without spending a use.");
                        else Require(!backend.HasSaveLoadoutCandidate,
                            "Fresh engine overwrite/discard must consume its candidate once.");
                        report.heldSaveConfirmationCases++;
                    }
                    report.heldSaveConfirmationsPreservedData = report.heldSaveConfirmationCases == 6;
                    report.heldSaveConfirmationFreshTapRecovered = report.heldSaveConfirmationsPreservedData;
                    Debug.Log("Greed engine held save confirmations passed: six actual EventSystem two-finger openings, delayed old releases blocked, all records/uses/names preserved and fresh taps accepted. In-memory progress only.");
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    ResetPending();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                }
            }

            private IEnumerator VerifyHeldSlotChanges()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                string[] pendingFields = { "saveDraftRequiresExplicitSlotChoice", "saveDraftOverwriteConfirmPending",
                    "saveDraftSkipConfirmPending", "saveSlotDeleteConfirmPending", "saveSlotRenameConfirmPending",
                    "saveSlotDetailViewActive", "saveDraftFromRefillRun" };
                void ResetPending()
                {
                    foreach (string field in pendingFields)
                        typeof(GreedLastApp).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, false);
                }
                try
                {
                    foreach (var scenario in new[] {
                        (Infinite: false, Button: "nextPatternButton"),
                        (Infinite: false, Button: "saveSlotDeleteButton"),
                        (Infinite: true, Button: "saveSlotDeleteButton"),
                        (Infinite: false, Button: "saveSlotRenameButton"),
                        (Infinite: true, Button: "saveSlotRenameButton"),
                        (Infinite: true, Button: "nextPatternButton") })
                    {
                        var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                        backend.AddSampleSaveLoadoutForTest(out _);
                        backend.AddSampleSaveLoadoutForTest(out _);
                        backend.AddSampleClearCandidateForTest(out _);
                        backend.SelectSaveLoadoutSlot(0);
                        backendField.SetValue(app, backend);
                        ResetPending();
                        if (scenario.Infinite)
                            state.SetInfiniteLoadoutSelect(backend.LoadSelectedSaveSlotView(), backend.BuildInfiniteLoadoutSelectText());
                        else state.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSaveLoadoutDraftText(true),
                            saveLoadoutCandidateAvailable: true);
                        yield return null;
                        Button command = Read<Button>(ui, scenario.Button);
                        Require(command.isActiveAndEnabled && command.IsInteractable(),
                            "Engine slot change requires a usable command: " + scenario.Button);
                        var records = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
                        var uses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
                        var names = (int[])Read<int[]>(backend, "saveLoadoutRenameSteps").Clone();
                        int target = !scenario.Infinite && scenario.Button == "nextPatternButton" ? 2 : 1;
                        Vector2 commandPoint = ScreenPoint(command);
                        Vector2 slotPoint = ScreenPoint(Read<Button[]>(ui, "laneButtons")[target]);
                        yield return TouchFrame(TouchAt(93, commandPoint, TouchPhase.Began), TouchAt(94, slotPoint, TouchPhase.Began));
                        yield return TouchFrame(TouchAt(93, commandPoint, TouchPhase.Stationary), TouchAt(94, slotPoint, TouchPhase.Ended));
                        yield return TouchFrame(TouchAt(93, commandPoint, TouchPhase.Stationary));
                        yield return WaitSeconds(0.45f);
                        yield return TouchFrame(TouchAt(93, commandPoint, TouchPhase.Ended));
                        yield return TouchFrame();
                        Require(state.CurrentState == (scenario.Infinite
                                ? GreedLastScreenState.InfiniteLoadoutSelect : GreedLastScreenState.SaveLoadoutDraft)
                            && backend.HasSaveLoadoutCandidate && backend.LoadSelectedSaveSlotView().SelectedSaveSlotIndex == target,
                            "The old engine command must not save or enter preparation for the changed slot: " + scenario.Button);
                        Require(!Read<bool>(app, "saveSlotDeleteConfirmPending") && !Read<bool>(app, "saveSlotRenameConfirmPending")
                            && !Read<bool>(app, "saveDraftOverwriteConfirmPending"),
                            "The old engine command must not open confirmation for the changed slot.");
                        for (int slot = 0; slot < records.Length; slot++)
                            Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(records[slot])
                                && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == uses[slot]
                                && Read<int[]>(backend, "saveLoadoutRenameSteps")[slot] == names[slot],
                                "Changing slots and releasing an old engine command must preserve all data.");
                        yield return TapTouch(command);
                        if (scenario.Button == "saveSlotDeleteButton")
                            Require(Read<bool>(app, "saveSlotDeleteConfirmPending") && backend.HasSaveLoadoutCandidate,
                                "Fresh engine delete must open only the changed-slot confirmation.");
                        else if (scenario.Button == "saveSlotRenameButton")
                            Require(Read<bool>(app, "saveSlotRenameConfirmPending") && backend.HasSaveLoadoutCandidate,
                                "Fresh engine rename must open only the changed-slot confirmation.");
                        else if (scenario.Infinite)
                            Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                                && Read<int[]>(backend, "saveLoadoutUsesRemaining")[target] == uses[target],
                                "Fresh engine preparation must retain the changed-slot use count.");
                        else Require(state.CurrentState == GreedLastScreenState.LobbyReady
                                && !backend.HasSaveLoadoutCandidate && backend.LoadSelectedSaveSlotView().SaveSlotOccupied[target],
                                "Fresh engine save must consume its candidate into the explicitly changed slot.");
                        report.heldSlotChangeCases++;
                    }
                    report.heldSlotChangesPreservedData = report.heldSlotChangeCases == 6;
                    report.heldSlotChangeFreshTapRecovered = report.heldSlotChangesPreservedData;
                    Debug.Log("Greed engine held slot changes passed: six two-finger command/slot pairs, delayed old releases blocked, all records/uses/names preserved and fresh actions accepted. In-memory progress only.");
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    Call(app, "ResetInfinitePreparation");
                    Call(app, "ClearInfinitePreparationContext");
                    ResetPending();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                }
            }

            private IEnumerator VerifyHeldPauseCommands(bool detailedControls = false)
            {
                if (ui.UsePlayerRunControls == detailedControls) ui.ToggleDevCleanView();
                Button primary = Read<Button>(ui, "nextPatternButton");
                Button back = Read<Button>(ui, "returnLobbyButton");
                Button pause = Read<Button>(ui, "pauseButton");
                Vector2 primaryPoint = ScreenPoint(primary, detailedControls ? 0f : -0.25f);
                Vector2 backPoint = ScreenPoint(detailedControls ? back : primary, detailedControls ? 0f : 0.25f);
                Vector2 pausePoint = ScreenPoint(pause);
                Require(!run.Paused && primary.isActiveAndEnabled && primary.IsInteractable()
                    && pause.isActiveAndEnabled && pause.IsInteractable(),
                    "Held pause commands must begin at real live controls.");
                yield return TouchFrame(TouchAt(101, primaryPoint, TouchPhase.Began), TouchAt(102, backPoint, TouchPhase.Began));
                PointerEventData[] primaryPresses = detailedControls
                    ? RequireRegisteredCommandPresses(primary, 101) : RequireRegisteredCommandPresses(primary, 101, 102);
                PointerEventData[] backPresses = detailedControls
                    ? RequireRegisteredCommandPresses(back, 102) : Array.Empty<PointerEventData>();
                yield return TouchFrame(TouchAt(101, primaryPoint, TouchPhase.Stationary), TouchAt(102, backPoint, TouchPhase.Stationary),
                    TouchAt(103, pausePoint, TouchPhase.Began));
                yield return TouchFrame(TouchAt(101, primaryPoint, TouchPhase.Stationary), TouchAt(102, backPoint, TouchPhase.Stationary),
                    TouchAt(103, pausePoint, TouchPhase.Ended));
                Require(run.Paused && !run.ResumeCountdownActive, "A fresh third-finger pause must open only the pause menu.");
                RequireCanceledCommandPresses(primary, primaryPresses);
                if (detailedControls) RequireCanceledCommandPresses(back, backPresses);
                GreedLastRunSnapshot frozen = run;
                primaryPoint = ScreenPoint(primary);
                backPoint = ScreenPoint(back);
                yield return TouchFrame(TouchAt(101, primaryPoint, TouchPhase.Moved), TouchAt(102, backPoint, TouchPhase.Moved));
                yield return TouchFrame(TouchAt(101, primaryPoint, TouchPhase.Stationary), TouchAt(102, backPoint, TouchPhase.Stationary));
                yield return WaitSeconds(0.4f);
                yield return TouchFrame(TouchAt(101, primaryPoint, TouchPhase.Ended), TouchAt(102, backPoint, TouchPhase.Ended));
                yield return TouchFrame();
                AssertFrozen(frozen);
                Require(run.Paused && !run.ResumeCountdownActive && state.CurrentState == GreedLastScreenState.RunCoreTest,
                    "Old live fingers moved onto pause-menu commands must not resume or abandon.");
                yield return TapTouch(primary);
                Require(run.ResumeCountdownActive, "Only a fresh pause-menu tap must start the original resume countdown.");
                float deadline = Time.realtimeSinceStartup + 8f;
                while (run.Paused)
                {
                    AssertFrozen(frozen);
                    Require(Time.realtimeSinceStartup < deadline, "Fresh held-command recovery must finish the engine resume lead-in.");
                    yield return null;
                }
                report.canceledEngineCommands += 2;
                report.pauseCycles++;
                report.heldPauseCommandCases++;
                report.heldPauseCommandsPreservedRun = report.heldPauseCommandCases == 2;
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                Debug.Log("Greed engine held pause commands passed: two registered visible command fingers in "
                    + (detailedControls ? "detailed primary/back" : "clean primary/primary")
                    + ", third-finger pause, old press eligibility cleared before moved releases, fresh resume/countdown recovered without changing run values.");
            }

            private IEnumerator VerifyHeldRunResults()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                try
                {
                    for (int scenario = 0; scenario < 6; scenario++)
                    {
                        int mode = scenario % 3;
                        bool detailedControls = scenario >= 3;
                        if (ui.UsePlayerRunControls == detailedControls) ui.ToggleDevCleanView();
                        var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                        backendField.SetValue(app, backend);
                        core.ConfigureGrowth(0, 0, 0);
                        state.SetLobbyReady(backend.LoadLobby());
                        if (mode == 2)
                        {
                            state.SetInfiniteRun("Held engine result check");
                            core.EnterInfiniteRun();
                        }
                        else
                        {
                            yield return ClickMouse(Read<Button>(ui, mode == 0 ? "practiceRunButton" : "normalRunButton"));
                            if (mode == 1)
                            {
                                yield return WaitSeconds(0.4f);
                                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[0]);
                            }
                        }
                        float deadline = Time.realtimeSinceStartup + 25f;
                        while (!run.ActivePattern || run.CountdownActive || run.Health != 1)
                        {
                            Require(Time.realtimeSinceStartup < deadline && !run.Stopped && !run.ChoiceActive,
                                "The held engine result fixture must naturally reach its last live health point. Mode " + mode);
                            yield return null;
                        }
                        Button primary = Read<Button>(ui, "nextPatternButton");
                        Button back = Read<Button>(ui, "returnLobbyButton");
                        Require(primary.isActiveAndEnabled && primary.IsInteractable(), "Held result fingers must begin on the visible live command.");
                        Vector2 primaryPoint = ScreenPoint(primary, detailedControls ? 0f : -0.25f);
                        Vector2 backPoint = ScreenPoint(detailedControls ? back : primary, detailedControls ? 0f : 0.25f);
                        yield return TouchFrame(TouchAt(95, primaryPoint, TouchPhase.Began), TouchAt(96, backPoint, TouchPhase.Began));
                        yield return TouchFrame(TouchAt(95, primaryPoint, TouchPhase.Stationary), TouchAt(96, backPoint, TouchPhase.Stationary));
                        PointerEventData[] primaryPresses = detailedControls
                            ? RequireRegisteredCommandPresses(primary, 95) : RequireRegisteredCommandPresses(primary, 95, 96);
                        PointerEventData[] backPresses = detailedControls
                            ? RequireRegisteredCommandPresses(back, 96) : Array.Empty<PointerEventData>();
                        while (!run.Stopped)
                        {
                            Require(Time.realtimeSinceStartup < deadline, "The held result fixture must fail through actual Update.");
                            yield return null;
                        }
                        GreedLastRunSnapshot terminal = run;
                        RequireCanceledCommandPresses(primary, primaryPresses);
                        if (detailedControls) RequireCanceledCommandPresses(back, backPresses);
                        yield return WaitSeconds(0.45f);
                        yield return TouchFrame(TouchAt(95, primaryPoint, TouchPhase.Ended), TouchAt(96, backPoint, TouchPhase.Ended));
                        yield return TouchFrame();
                        Require(run.Stopped && run.Phase == terminal.Phase && run.Health == terminal.Health
                            && run.Score == terminal.Score && run.Distance == terminal.Distance && run.Focus == terminal.Focus
                            && run.SuccessCount == terminal.SuccessCount && run.GoodCount == terminal.GoodCount
                            && run.MissCount == terminal.MissCount && run.MaxCombo == terminal.MaxCombo
                            && state.CurrentState == (mode == 2 ? GreedLastScreenState.InfiniteRun : GreedLastScreenState.RunCoreTest)
                            && Read<int>(backend, "normalFailCount") == 0 && Read<int>(backend, "infiniteRunCount") == 0
                            && !backend.HasSaveLoadoutCandidate,
                            "Old engine live presses must leave the result visible without restart, settlement or discard. Mode " + mode
                                + $" stopped={run.Stopped}, same={run.Equals(terminal)}, state={state.CurrentState},"
                                + $" normal={Read<int>(backend, "normalFailCount")}, infinite={Read<int>(backend, "infiniteRunCount")},"
                                + $" candidate={backend.HasSaveLoadoutCandidate}, score={run.Score}/{terminal.Score},"
                                + $" distance={run.Distance}/{terminal.Distance}");
                        yield return TapTouch(back);
                        Require(state.CurrentState == GreedLastScreenState.LobbyReady
                            && Read<int>(backend, "normalFailCount") == (mode == 1 ? 1 : 0)
                            && Read<int>(backend, "infiniteRunCount") == (mode == 2 ? 1 : 0),
                            "A fresh engine result tap must settle exactly once. Mode " + mode);
                        report.heldRunResultCases++;
                        report.canceledEngineCommands += 2;
                    }
                    report.heldRunResultsPreservedData = report.heldRunResultCases == 6;
                    report.heldRunResultFreshTapRecovered = report.heldRunResultsPreservedData;
                    Debug.Log("Greed engine held run results passed: three natural failures in clean primary/primary and detailed primary/back views; both pointer identities registered, old press eligibility cleared before delayed releases, fresh back settlement recovered. In-memory progress only.");
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    core.Exit();
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                }
            }

            private IEnumerator VerifyMultiTouch()
            {
                string[] contexts = { string.Empty, "기초연습 숙이기 단계", "기초연습 비틀기 단계" };
                for (int action = 0; action < contexts.Length; action++)
                {
                    for (int order = 0; order < 2; order++)
                    {
                        Call(app, "RequestReturnToLobby");
                        yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                        // Only select the real course starting point; clocks still advance in engine Update.
                        if (action != 0) core.EnterPractice(contexts[action]);
                        yield return WaitForTouchablePattern();
                        Require((int)run.Pattern.AvoidanceType == action + 1,
                            "Each multi-touch case must exercise a different authored avoidance action.");
                        int correct = (int)run.Pattern.Channel - 1;
                        int wrong = (correct + 1) % 3;
                        Button[] lanes = Read<Button[]>(ui, "laneButtons");
                        bool correctFirst = order == 0;
                        Vector2 first = ScreenPoint(lanes[correctFirst ? correct : wrong]);
                        Vector2 second = ScreenPoint(lanes[correctFirst ? wrong : correct]);
                        yield return TouchFrame(TouchAt(73, first, TouchPhase.Began), TouchAt(74, second, TouchPhase.Began));
                        GreedLastRunSnapshot judged = run;
                        Require(report.judgements == 1
                            && run.LastResult == (correctFirst ? GreedLastJudgementResult.Success : GreedLastJudgementResult.Miss)
                            && run.LastJudgedInputChannel == (GreedLastRunChannel)((correctFirst ? correct : wrong) + 1)
                            && (correctFirst || run.MissReason == "wrong_channel"),
                            "Two simultaneous channels must resolve once using the first submitted finger, not the later correct finger.");
                        yield return TouchFrame(TouchAt(73, first, TouchPhase.Ended), TouchAt(74, second, TouchPhase.Stationary));
                        yield return TouchFrame(TouchAt(74, second, TouchPhase.Stationary));
                        yield return WaitSeconds(0.15f);
                        Require(report.judgements == 1 && run.Score == judged.Score && run.Health == judged.Health,
                            "Releasing one finger and holding the other must not create another judgement or reward.");
                        yield return TouchFrame(TouchAt(74, second, TouchPhase.Canceled));
                        yield return TouchFrame();
                        Require(Array.TrueForAll(Read<bool[]>(ui, "laneHoldActive"), held => !held),
                            "Ending and canceling distinct lane fingers must release every hold.");
                        report.multiTouchPatternCases++;
                    }
                }
                report.multiTouchFirstInputOnly = report.multiTouchPatternCases == 6;

                foreach (int chosenLane in new[] { 0, 2 })
                {
                    Call(app, "RequestReturnToLobby");
                    yield return ClickMouse(Read<Button>(ui, "normalRunButton"));
                    yield return WaitSeconds(0.4f);
                    Require(run.ChoiceActive && run.ChoiceKind == GreedLastChoiceKind.StartGift,
                        "Held choice fingers must begin on a real opening gift screen.");
                    string chosenGift = chosenLane == 0 ? run.ChoiceLeft : run.ChoiceRight;
                    Button[] lanes = Read<Button[]>(ui, "laneButtons");
                    Vector2 first = ScreenPoint(lanes[chosenLane]);
                    Vector2 second = ScreenPoint(lanes[1]);
                    yield return TouchFrame(TouchAt(73, first, TouchPhase.Began), TouchAt(74, second, TouchPhase.Began));
                    yield return TouchFrame(TouchAt(73, first, TouchPhase.Stationary), TouchAt(74, second, TouchPhase.Stationary));
                    float deadline = Time.realtimeSinceStartup + 8f;
                    while (run.ChoiceActive || run.CountdownActive || !run.ActivePattern)
                    {
                        Require(Time.realtimeSinceStartup < deadline && report.judgements == 0,
                            "Held choice touches must not select again or resolve the first countdown trap.");
                        yield return null;
                    }
                    Require(Read<string>(core, "startGift") == chosenGift && Read<int>(core, "choiceHistoryCount") == 1,
                        "The first finger alone must determine the opening gift across the countdown.");
                    yield return TouchFrame(TouchAt(73, first, TouchPhase.Ended), TouchAt(74, second, TouchPhase.Canceled));
                    yield return TouchFrame();
                    Require(report.judgements == 0
                        && Array.TrueForAll(Read<bool[]>(ui, "laneHoldActive"), held => !held),
                        "Releasing choice fingers over a live course must not submit a rhythm input.");
                    yield return WaitForTouchablePattern();
                    Vector2 target = ScreenPoint(lanes[(int)run.Pattern.Channel - 1]);
                    yield return TouchFrame(TouchAt(73, target, TouchPhase.Began));
                    Require(report.judgements == 1 && run.LastResult == GreedLastJudgementResult.Success,
                        "A fresh tap using a released choice finger ID must still judge the real first trap once.");
                    yield return TouchFrame(TouchAt(73, target, TouchPhase.Ended));
                    yield return TouchFrame();
                    Require(report.judgements == 1, "The fresh tap's release must not create a second reward.");
                    report.multiTouchChoiceCases++;
                }
                report.heldChoiceCannotAdvance = report.multiTouchChoiceCases == 2;
                report.multiTouchFreshTapRecovered = report.multiTouchFirstInputOnly && report.heldChoiceCannotAdvance;
                Debug.Log("Greed engine multi-touch checks passed: six action/order cases, two held opening choices and fresh same-ID trap taps.");
            }

            private IEnumerator WaitForTouchablePattern()
            {
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!run.ActivePattern || run.CountdownActive
                    || run.ElapsedSeconds + core.InputTimingOffsetSeconds < run.Pattern.CoreSeconds)
                {
                    Require(Time.realtimeSinceStartup < deadline && !run.Stopped,
                        "The real engine course must reach its calibrated input target before timing out.");
                    yield return null;
                }
            }

            private IEnumerator TouchFrame(params Touch[] touches)
            {
                input.MouseEnabled = input.MouseDown = input.MouseHeld = input.MouseUp = false;
                input.Touches = touches;
                int began = 0;
                foreach (Touch touch in touches) if (touch.phase == TouchPhase.Began) began++;
                report.engineTouchContacts += began;
                if (began > 1) report.engineMultiTouchBatches++;
                yield return null;
            }

            private static Touch TouchAt(int id, Vector2 point, TouchPhase phase)
                => new Touch { fingerId = id, position = point, phase = phase };

            private IEnumerator VerifyFullLoop()
            {
                float began = Time.realtimeSinceStartup;
                GreedLastMockBackend backend = Read<GreedLastMockBackend>(app, "backend");
                if (!run.Stopped && !run.Paused) yield return ClickMouse(Read<Button>(ui, "pauseButton"));
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                Require(state.CurrentState == GreedLastScreenState.LobbyReady, "The engine result action must return to the lobby.");
                yield return ClickMouse(Read<Button>(ui, "normalRunButton"));
                Require(run.ChoiceActive && !run.InfiniteMode, "The engine normal-run command must open its first choice.");
                if (run.DevInvincible) core.ToggleDevInvincible();
                Require(!run.DevInvincible, "The full normal loop must use real health and natural failure rules.");
                float nextChoiceAt = Time.realtimeSinceStartup + 0.4f;
                float deadline = Time.realtimeSinceStartup + 120f;
                bool heldForClear = false;
                Vector2 heldPrimaryPoint = default, heldBackPoint = default;
                PointerEventData[] heldClearPrimary = null, heldClearBack = null;
                int clearsBefore = Read<int>(backend, "normalClearCount");
                while (!run.Stopped)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The real-time normal run did not complete within its bounded deadline.");
                    if (!heldForClear && run.ActivePattern && !run.CountdownActive && !run.Paused
                        && run.Phase == GreedLastRunPhase.EscapeRunning && run.ChapterProgress == 2)
                    {
                        report.heldClearDetailedControls = report.domainReloadDisabled;
                        if (ui.UsePlayerRunControls == report.heldClearDetailedControls) ui.ToggleDevCleanView();
                        Button primary = Read<Button>(ui, "nextPatternButton"), back = Read<Button>(ui, "returnLobbyButton");
                        Require(primary.isActiveAndEnabled && primary.IsInteractable(), "Clear fingers must be held on a visible command while the last trap is still live.");
                        heldPrimaryPoint = ScreenPoint(primary, report.heldClearDetailedControls ? 0f : -0.25f);
                        heldBackPoint = ScreenPoint(report.heldClearDetailedControls ? back : primary,
                            report.heldClearDetailedControls ? 0f : 0.25f);
                        yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Began), TouchAt(119, heldBackPoint, TouchPhase.Began));
                        yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Stationary), TouchAt(119, heldBackPoint, TouchPhase.Stationary));
                        heldClearPrimary = report.heldClearDetailedControls
                            ? RequireRegisteredCommandPresses(primary, 118) : RequireRegisteredCommandPresses(primary, 118, 119);
                        heldClearBack = report.heldClearDetailedControls
                            ? RequireRegisteredCommandPresses(back, 119) : Array.Empty<PointerEventData>();
                        heldForClear = true;
                    }
                    if (run.ChoiceActive && Time.realtimeSinceStartup >= nextChoiceAt)
                    {
                        GreedLastChoiceKind before = run.ChoiceKind;
                        if (before == GreedLastChoiceKind.Node
                            || (before == GreedLastChoiceKind.Gift && run.ChapterIndex == 2))
                            yield return VerifyConsecutiveChoiceTouches();
                        else yield return TapTouch(Read<Button[]>(ui, "laneButtons")[0]);
                        Require(!run.ChoiceActive || run.ChoiceKind != before,
                            "One engine touch must resolve exactly the current readable choice.");
                        report.normalChoices++;
                        nextChoiceAt = Time.realtimeSinceStartup + 0.4f;
                    }
                    else if (run.ActivePattern && !run.CountdownActive && !run.Paused
                        && run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds)
                    {
                        Button lane = Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1];
                        int before = report.judgements;
                        if (heldForClear)
                        {
                            Vector2 point = ScreenPoint(lane);
                            foreach (TouchPhase phase in new[] { TouchPhase.Began, TouchPhase.Stationary, TouchPhase.Ended })
                                yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Stationary),
                                    TouchAt(119, heldBackPoint, TouchPhase.Stationary), TouchAt(71, point, phase));
                            yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Stationary), TouchAt(119, heldBackPoint, TouchPhase.Stationary));
                            report.engineTouchTaps++;
                        }
                        else yield return TapTouch(lane);
                        Require(report.judgements == before + 1, "A full normal-run touch must resolve exactly one actual trap.");
                    }
                    yield return null;
                }
                Require(run.EscapeSucceeded && run.SaveEligible && run.Health > 0 && report.normalChoices >= 4,
                    "The real-time normal loop must recover the core, pass the exit and retain one saveable result.");
                Require(heldForClear, "The full clear must include commands held across its final trap.");
                GreedLastRunSnapshot cleared = run;
                RequireCanceledCommandPresses(Read<Button>(ui, "nextPatternButton"), heldClearPrimary);
                if (report.heldClearDetailedControls)
                    RequireCanceledCommandPresses(Read<Button>(ui, "returnLobbyButton"), heldClearBack);
                heldPrimaryPoint = ScreenPoint(Read<Button>(ui, "nextPatternButton"));
                heldBackPoint = ScreenPoint(Read<Button>(ui, "returnLobbyButton"));
                yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Moved), TouchAt(119, heldBackPoint, TouchPhase.Moved));
                yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Stationary), TouchAt(119, heldBackPoint, TouchPhase.Stationary));
                yield return WaitSeconds(0.45f);
                yield return TouchFrame(TouchAt(118, heldPrimaryPoint, TouchPhase.Ended), TouchAt(119, heldBackPoint, TouchPhase.Ended));
                yield return TouchFrame();
                Require(run.Stopped && run.EscapeSucceeded && run.SaveEligible && run.Health == cleared.Health
                    && run.Score == cleared.Score && run.Distance == cleared.Distance && run.Focus == cleared.Focus
                    && state.CurrentState == GreedLastScreenState.RunCoreTest && !backend.HasSaveLoadoutCandidate
                    && Read<int>(backend, "normalClearCount") == clearsBefore,
                    "Old live commands must preserve the clear result without settlement, saving or discard.");
                report.heldNormalClearPreservedResult = true;
                report.canceledEngineCommands += 2;
                Debug.Log("Greed engine held normal clear passed: two registered fingers in "
                    + (report.heldClearDetailedControls ? "detailed primary/back" : "clean primary/primary")
                    + " through the last natural success, old eligibility cleared before moved releases, result/save opportunity preserved; no settlement before fresh input.");
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                report.fullNormalClear = true;
                report.fullNormalJudgements = report.judgements;
                Call(app, "TryHandlePauseOrBackKeyboardInput", true, false);
                yield return null;
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate
                    && !Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "The clear action must open a save window locked until explicit slot selection.");
                report.clearBackSaveProtected = true;
                report.savedSlotIndex = report.domainReloadDisabled ? 2 : 1;
                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[report.savedSlotIndex]);
                yield return WaitSeconds(0.4f);
                yield return ConfirmWithTwoFingers(Read<Button>(ui, "nextPatternButton"), false);
                GreedLastLobbySnapshot saved = backend.LoadLobby();
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                    && saved.SaveSlotOccupied[report.savedSlotIndex]
                    && !saved.SaveSlotOccupied[(report.savedSlotIndex + 1) % 3]
                    && !saved.SaveSlotOccupied[(report.savedSlotIndex + 2) % 3]
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == 2,
                    "Two confirmation fingers must save exactly once into the explicitly chosen slot, with other slots empty and both starts intact.");
                report.multiTouchConfirmationCases++;
                yield return ClickMouse(Read<Button>(ui, "saveLoadoutButton"));
                Require(!backend.HasSaveLoadoutCandidate && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == 2,
                    "Opening save management again must not recreate a candidate or duplicate the saved result.");
                report.candidateConsumedOnce = true;
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));

                yield return PrepareInfinite();
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                Require(state.CurrentState == GreedLastScreenState.LobbyReady
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == 2,
                    "Canceling complete preparation must preserve both actual starts.");
                report.preparationCancelPreservedUses = true;
                for (int use = 0; use < 2; use++)
                {
                    yield return PrepareInfinite();
                    yield return ConfirmWithTwoFingers(Read<Button>(ui, "nextPatternButton"), use != 0);
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && run.InfiniteMode && !run.Stopped
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == 1 - use
                        && backend.LoadLobby().SaveSlotOccupied[report.savedSlotIndex] == (use == 0),
                        "Only actual infinite start may consume a use, with immediate source auto-clear on its second start.");
                    report.multiTouchConfirmationCases++;
                    report.actualInfiniteStarts++;
                    if (run.DevInvincible) core.ToggleDevInvincible();
                    bool inputOnce = false;
                    deadline = Time.realtimeSinceStartup + 35f;
                    while (!run.Stopped)
                    {
                        Require(Time.realtimeSinceStartup < deadline, "The real-time infinite run must reach natural health-zero failure.");
                        if (!inputOnce && run.ActivePattern && !run.CountdownActive
                            && run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds)
                        {
                            yield return TapTouch(Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1]);
                            inputOnce = true;
                        }
                        yield return null;
                    }
                    Require(inputOnce && run.Health == 0 && !run.DevInvincible && !backend.HasSaveLoadoutCandidate,
                        "A consumed source must support its live run until actual natural failure, without a new normal-save opportunity.");
                    int settledBefore = Read<int>(backend, "infiniteRunCount");
                    Call(app, "TryHandlePauseOrBackKeyboardInput", true, false);
                    yield return null;
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                        "Each real-time infinite result must return to the lobby normally.");
                    Require(Read<int>(backend, "infiniteRunCount") == settledBefore + 1,
                        "The result back callback must settle one infinite record, not duplicate it.");
                    Call(app, "TryHandlePauseOrBackKeyboardInput", true, false);
                    Require(Read<int>(backend, "infiniteRunCount") == settledBefore + 1,
                        "Another back callback in the lobby must not settle the same result again.");
                    report.terminalBackSettlements++;
                }
                report.infiniteNaturalFailures = true;
                report.multiTouchConfirmationsConsumedOnce = report.multiTouchConfirmationCases == 3;
                Require(!backend.LoadLobby().SaveSlotOccupied[report.savedSlotIndex] && !backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
                    "The twice-used slot must be empty and reject a third start.");
                report.exhaustedSlotAutoCleared = true;
                yield return ClickMouse(Read<Button>(ui, "infiniteRecordButton"));
                Require(state.CurrentState == GreedLastScreenState.InfiniteRecordBoard,
                    "The engine loop must expose actual recorded infinite results.");
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                yield return ClickMouse(Read<Button>(ui, "infiniteButton"));
                yield return WaitSeconds(0.4f);
                yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                Require(run.RefillRun && run.ChoiceActive && run.Score == 0 && !backend.HasSaveLoadoutCandidate,
                    "After natural failures and source depletion the actual empty preparation must re-enter a fresh refill choice.");
                report.emptySlotRefillReentered = true;
                report.fullLoopSeconds = Time.realtimeSinceStartup - began;
            }

            private IEnumerator PrepareInfinite()
            {
                GreedLastMockBackend backend = Read<GreedLastMockBackend>(app, "backend");
                int uses = Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex];
                yield return ClickMouse(Read<Button>(ui, "infiniteButton"));
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady, "Infinite entry must open preparation, not consume a use.");
                yield return WaitSeconds(0.4f);
                yield return VerifyPreparationTouches(0, 1, uses);
                Require(Read<int>(app, "infinitePreparationStep") == 1, "The engine gift tap must resolve just one preparation step.");
                yield return WaitSeconds(0.4f);
                yield return VerifyPreparationTouches(1, 2, uses);
                Require(Read<int>(app, "infinitePreparationStep") == 2
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == uses,
                    "Gift/relic preparation must remain separate from actual start consumption.");
                yield return WaitSeconds(0.4f);
                yield return VerifyPreparationTouches(2, 2, uses);
                report.multiTouchPreparationPreservedUses = true;
            }

            private IEnumerator ConfirmWithTwoFingers(Button button, bool releaseSecondFirst)
            {
                Vector2 point = ScreenPoint(button);
                yield return TouchFrame(TouchAt(81, point, TouchPhase.Began), TouchAt(82, point, TouchPhase.Began));
                yield return TouchFrame(TouchAt(81, point, TouchPhase.Stationary), TouchAt(82, point, TouchPhase.Stationary));
                int released = releaseSecondFirst ? 82 : 81;
                int held = releaseSecondFirst ? 81 : 82;
                yield return TouchFrame(TouchAt(released, point, TouchPhase.Ended), TouchAt(held, point, TouchPhase.Stationary));
                GreedLastScreenState confirmedState = state.CurrentState;
                yield return TouchFrame(TouchAt(held, point, TouchPhase.Stationary));
                yield return WaitSeconds(0.4f);
                yield return TouchFrame(TouchAt(held, point, TouchPhase.Ended));
                yield return TouchFrame();
                Require(state.CurrentState == confirmedState,
                    "Releasing the second confirmation finger after transition must not invoke a new screen command.");
            }

            private IEnumerator VerifyConsecutiveChoiceTouches()
            {
                GreedLastChoiceKind from = run.ChoiceKind;
                GreedLastChoiceKind next = from == GreedLastChoiceKind.Node
                    ? GreedLastChoiceKind.Gift : GreedLastChoiceKind.Relic;
                int history = Read<int>(core, "choiceHistoryCount");
                Button[] lanes = Read<Button[]>(ui, "laneButtons");
                Vector2 first = ScreenPoint(lanes[0]);
                Vector2 second = ScreenPoint(lanes[2]);
                yield return TouchFrame(TouchAt(73, first, TouchPhase.Began), TouchAt(74, second, TouchPhase.Began));
                yield return TouchFrame(TouchAt(73, first, TouchPhase.Stationary), TouchAt(74, second, TouchPhase.Stationary));
                yield return WaitSeconds(0.4f);
                Require(run.ChoiceActive && run.ChoiceKind == next && Read<int>(core, "choiceHistoryCount") == history + 1,
                    "Two opening fingers must advance only one consecutive choice, even after the transition guard expires: " + from);
                GreedLastRunSnapshot frozen = run;
                yield return TouchFrame(TouchAt(73, first, TouchPhase.Ended), TouchAt(74, second, TouchPhase.Canceled));
                yield return TouchFrame();
                Require(run.ChoiceKind == next && Read<int>(core, "choiceHistoryCount") == history + 1,
                    "Releasing fingers from the previous choice must not choose the new gift or relic.");
                AssertFrozen(frozen);
                report.consecutiveMultiTouchChoiceCases++;
            }

            private IEnumerator VerifyPreparationTouches(int chosenLane, int expectedStep, int uses)
            {
                GreedLastMockBackend backend = Read<GreedLastMockBackend>(app, "backend");
                Button[] lanes = Read<Button[]>(ui, "laneButtons");
                Vector2 first = ScreenPoint(lanes[chosenLane]);
                Vector2 second = ScreenPoint(lanes[(chosenLane + 1) % 3]);
                bool holdStart = expectedStep == 2 && Read<int>(app, "infinitePreparationStep") == 1;
                Button primary = Read<Button>(ui, "nextPatternButton");
                Vector2 start = ScreenPoint(primary);
                PointerEventData[] heldStart = null;
                if (holdStart)
                {
                    // Establish the held start before the accepted relic choice cancels it.
                    yield return TouchFrame(TouchAt(98, start, TouchPhase.Began));
                    yield return TouchFrame(TouchAt(98, start, TouchPhase.Stationary));
                    heldStart = RequireRegisteredCommandPresses(primary, 98);
                }
                yield return TouchFrame(holdStart
                    ? new[] { TouchAt(98, start, TouchPhase.Stationary), TouchAt(73, first, TouchPhase.Began), TouchAt(74, second, TouchPhase.Began) }
                    : new[] { TouchAt(73, first, TouchPhase.Began), TouchAt(74, second, TouchPhase.Began) });
                yield return TouchFrame(holdStart
                    ? new[] { TouchAt(98, start, TouchPhase.Stationary), TouchAt(73, first, TouchPhase.Stationary), TouchAt(74, second, TouchPhase.Stationary) }
                    : new[] { TouchAt(73, first, TouchPhase.Stationary), TouchAt(74, second, TouchPhase.Stationary) });
                if (holdStart) RequireCanceledCommandPresses(primary, heldStart);
                yield return WaitSeconds(0.4f);
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                    && Read<int>(app, "infinitePreparationStep") == expectedStep
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == uses,
                    "Held multi-touch must change only one preparation step and never consume or start a saved loadout.");
                string gift = Read<string>(app, "infinitePreparationGift");
                string relic = Read<string>(app, "infinitePreparationRelic");
                yield return TouchFrame(holdStart
                    ? new[] { TouchAt(98, start, TouchPhase.Ended), TouchAt(73, first, TouchPhase.Ended), TouchAt(74, second, TouchPhase.Canceled) }
                    : new[] { TouchAt(73, first, TouchPhase.Ended), TouchAt(74, second, TouchPhase.Canceled) });
                yield return TouchFrame();
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady
                    && Read<int>(app, "infinitePreparationStep") == expectedStep
                    && Read<string>(app, "infinitePreparationGift") == gift
                    && Read<string>(app, "infinitePreparationRelic") == relic
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == uses,
                    "Releasing preparation fingers must preserve the selected gift, relic and remaining starts.");
                report.multiTouchPreparationCases++;
                if (holdStart)
                {
                    report.heldPreparationStartCases++;
                    report.heldPreparationStartsPreservedUses = report.heldPreparationStartCases == 3;
                    report.canceledEngineCommands++;
                }
            }

            private static IEnumerator WaitSeconds(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until) yield return null;
            }

            private IEnumerator VerifyReleasedPresentation()
            {
                state.SetRunCoreTest();
                core.RestartPractice(Time.realtimeSinceStartup);
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!run.ActivePattern || run.CountdownActive)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Released-screen check needs a live engine practice trap.");
                    yield return null;
                }
                GameObject canvas = Read<GameObject>(ui, "ownedCanvas");
                UnityEngine.Object.Destroy(ui);
                yield return null;
                yield return null;
                Require(run.Paused && Count<GreedLastRuntimeUi>() == 0 && canvas == null,
                    "Removing presentation must first pause the live run and release its owned canvas.");
                GreedLastRunSnapshot frozen = run;
                GreedLastScreenState screen = state.CurrentState;
                Call(app, "RequestToggleRunPause");
                Require(run.Paused && !run.ResumeCountdownActive,
                    "A released screen must not resume an invisible run through keyboard commands.");
                Call(app, "RequestNextRunPattern");
                Call(app, "HandleRunChannelInput", GreedLastRunChannel.Left);
                Call(app, "RequestReturnToLobby");
                Require(run.Paused && !run.ResumeCountdownActive && state.CurrentState == screen,
                    "A released screen must not accept hidden resume, lane, primary or lobby commands.");
                AssertFrozen(frozen);
                report.releasedUiBlockedCommands = true;
                state.ShowLocalNotice("격리 화면 해제 확인");
                yield return WaitSeconds(0.15f);
                AssertFrozen(frozen);
                Require(CountNamed<Canvas>("GreedLastCanvas") == 0,
                    "A released presentation callback must not recreate an orphaned canvas.");
                report.releasedUiDetachedCallbacks = true;

                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                yield return null;
                InvokeStatic(typeof(GreedLastApp), "Create");
                Attach(FindApp());
                deadline = Time.realtimeSinceStartup + 10f;
                while (state.CurrentState != GreedLastScreenState.LobbyReady)
                {
                    Require(Time.realtimeSinceStartup < deadline, "A normal restart must recover after presentation removal.");
                    yield return null;
                }
                Require(Count<GreedLastApp>() == 1 && CountNamed<Canvas>("GreedLastCanvas") == 1
                    && ui.AcceptsPlayerInput,
                    "Restart must recover one playable UI, not leave the released callback or a duplicate canvas.");
                EventSystem events = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                input = events.gameObject.AddComponent<GreedLastPrototypeKeyboardInput>();
                events.GetComponent<StandaloneInputModule>().inputOverride = input;
                input.MouseEnabled = true;
                input.MousePosition = new Vector2(-1000f, -1000f);
                yield return null;
                yield return null;
                yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                Require(state.CurrentState == GreedLastScreenState.RunCoreTest && run.PracticeMode,
                    "A fresh real-module click must still start practice after presentation removal and restart.");
                report.releasedUiRestartRecovered = true;

                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                yield return null;
                InvokeStatic(typeof(GreedLastApp), "Create");
                Attach(FindApp());
                deadline = Time.realtimeSinceStartup + 10f;
                while (!Read<GreedLastRequestGate>(app, "requestGate").Busy)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Released boot-screen check needs an active boot request.");
                    yield return null;
                }
                UnityEngine.Object.Destroy(ui);
                yield return null;
                yield return null;
                while (state.CurrentState != GreedLastScreenState.LobbyReady)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Boot must release its request after presentation removal.");
                    yield return null;
                }
                Call(app, "RequestNormalRun");
                state.ShowLocalNotice("격리 부팅 화면 해제 확인");
                Require(state.CurrentState == GreedLastScreenState.LobbyReady
                    && !Read<GreedLastRequestGate>(app, "requestGate").Busy
                    && !Read<bool>(app, "hasLatestAppRunSnapshot")
                    && Count<GreedLastRuntimeUi>() == 0 && CountNamed<Canvas>("GreedLastCanvas") == 0,
                    "A removed boot screen must finish its request without recreating UI or starting a hidden run.");
                report.releasedUiBootSafe = true;
            }

            private void Attach(GreedLastApp value)
            {
                app = value;
                Require(app != null, "The runtime app is missing.");
                ui = Read<GreedLastRuntimeUi>(app, "runtimeUi");
                state = Read<GreedLastStateMachine>(app, "stateMachine");
                core = Read<GreedLastRunCore>(app, "runCore");
                core.SnapshotChanged += snapshot =>
                {
                    run = snapshot;
                    report.publications++;
                    report.judgements = snapshot.SuccessCount + snapshot.GoodCount + snapshot.MissCount;
                };
            }

            private IEnumerator PressMouse(Button button)
            {
                Require(button.isActiveAndEnabled && button.IsInteractable(),
                    "The engine input target must be visible and interactable: " + button.name);
                input.Touches = Array.Empty<Touch>();
                input.MouseEnabled = true;
                input.MousePosition = ScreenPoint(button);
                input.MouseDown = input.MouseHeld = true;
                yield return null;
                input.MouseDown = false;
            }

            private IEnumerator ReleaseMouse()
            {
                input.MouseHeld = false;
                input.MouseUp = true;
                yield return null;
                input.MouseUp = false;
                yield return null;
            }

            private IEnumerator ClickMouse(Button button)
            {
                yield return PressMouse(button);
                yield return ReleaseMouse();
                report.engineMouseClicks++;
            }

            private IEnumerator TapTouch(Button button)
            {
                Require(button.isActiveAndEnabled && button.IsInteractable(),
                    "The engine touch target must be visible and interactable: " + button.name);
                input.MouseEnabled = false;
                Vector2 point = ScreenPoint(button);
                input.Touches = new[] { new Touch { fingerId = 71, position = point, phase = TouchPhase.Began } };
                yield return null;
                input.Touches = new[] { new Touch { fingerId = 71, position = point, phase = TouchPhase.Stationary } };
                yield return null;
                input.Touches = new[] { new Touch { fingerId = 71, position = point, phase = TouchPhase.Ended } };
                yield return null;
                input.Touches = Array.Empty<Touch>();
                yield return null;
                report.engineTouchTaps++;
                report.engineTouchContacts++;
            }

            private PointerEventData[] RequireRegisteredCommandPresses(Button button, params int[] pointerIds)
            {
                var pending = Read<HashSet<PointerEventData>>(button, "pendingPresses");
                Require(button.isActiveAndEnabled && button.IsInteractable() && pending.Count == pointerIds.Length,
                    "Held command fingers must be registered on an active control: " + button.name
                        + $" active={button.isActiveAndEnabled}, pending={pending.Count}, expected={pointerIds.Length}");
                foreach (int pointerId in pointerIds)
                {
                    bool registered = false;
                    foreach (PointerEventData pointer in pending)
                        registered |= pointer.pointerId == pointerId && pointer.eligibleForClick;
                    Require(registered,
                        "The actual EventSystem must register the held command finger: " + pointerId);
                }
                report.registeredHeldCommandPresses += pointerIds.Length;
                var presses = new PointerEventData[pending.Count];
                pending.CopyTo(presses);
                return presses;
            }

            private static void RequireCanceledCommandPresses(Button button, PointerEventData[] presses)
            {
                Require(presses != null && presses.Length > 0
                    && Read<HashSet<PointerEventData>>(button, "pendingPresses").Count == 0
                    && Array.TrueForAll(presses, pointer => !pointer.eligibleForClick),
                    "The transition must cancel registered press eligibility before release: " + button.name);
            }

            private static Vector2 ScreenPoint(Button button, float horizontalFraction = 0f)
            {
                Canvas canvas = button.GetComponentInParent<Canvas>();
                RectTransform rect = button.GetComponent<RectTransform>();
                return RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center + new Vector2(rect.rect.width * horizontalFraction, 0f)));
            }

            private void AssertFrozen(GreedLastRunSnapshot anchor) => Require(
                run.Health == anchor.Health && run.Score == anchor.Score && run.Distance == anchor.Distance
                && run.FocusStateActiveSeconds == anchor.FocusStateActiveSeconds,
                "Engine pause/countdown must freeze health, score, distance and focus time.");

            private void CollectError(string message, string stack, LogType type)
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) loggedError = message; }

            private void Complete(Exception error)
            {
                if (completed) return;
                completed = true;
                Application.logMessageReceived -= CollectError;
                report.elapsedSeconds = Time.realtimeSinceStartup - startedAt;
                if (error == null && !string.IsNullOrEmpty(loggedError)) error = new InvalidOperationException(loggedError);
                var results = JsonUtility.FromJson<Results>(SessionState.GetString(Prefix + "reports", "{}"));
                results.cycles.Add(report);
                SessionState.SetString(Prefix + "reports", JsonUtility.ToJson(results));
                if (error != null) SessionState.SetString(Prefix + "error", error.ToString());
                EditorApplication.ExitPlaymode();
            }

            private void OnDestroy()
            {
                Application.logMessageReceived -= CollectError;
                if (!completed && SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false))
                    SessionState.SetString(Prefix + "error", "The engine check was interrupted before its lifecycle cycle completed.");
            }
        }

        private static T Read<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void InvokeStatic(Type type, string name) => type
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static GreedLastApp FindApp() => UnityEngine.Object.FindAnyObjectByType<GreedLastApp>();
        private static int Count<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>().Length;
        private static int CountNamed<T>(string name) where T : Component
        {
            int count = 0;
            foreach (T component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                if (component.gameObject.name == name) count++;
            return count;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
