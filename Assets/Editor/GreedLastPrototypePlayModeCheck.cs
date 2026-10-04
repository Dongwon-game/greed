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

        private static void BeginCheck(bool fullLoop)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode
                && !SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false),
                "The engine lifecycle check requires an idle Edit-mode editor.");
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
            SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, true);
            EditorSceneManager.playModeStartScene = scene;
            BeginCycle();
        }

        [MenuItem("Greed Last/Run Engine Play Mode Lifecycle Check", true)]
        [MenuItem("Greed Last/Run Engine Play Mode Full Loop Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode
            && !SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false);

        private static void BeginCycle()
        {
            int cycle = SessionState.GetInt(Prefix + "cycle", 0);
            EditorSettings.enterPlayModeOptions = cycle == 1
                ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
            EditorSettings.enterPlayModeOptionsEnabled = cycle == 1;
            SessionState.SetString(Prefix + "domainBeforeEntry", DomainIdentity);
            SessionState.SetString(Prefix + "deadline", DateTime.UtcNow.AddSeconds(
                SessionState.GetBool(Prefix + "fullLoop", false) ? 240 : 100).ToString("O"));
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
            try
            {
                string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "PlayModeChecks");
                Directory.CreateDirectory(directory);
                results.archivedReport = "report-" + (results.fullLoopIncluded ? "full-loop-" : "lifecycle-")
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + ".json";
                string json = JsonUtility.ToJson(results, true);
                File.WriteAllText(Path.Combine(directory, results.archivedReport), json);
                File.WriteAllText(Path.Combine(directory, "latest-report.json"), json);
            }
            catch (Exception error) { results.succeeded = false; results.error += error; }
            if (results.succeeded) Debug.Log("Greed engine Play-mode check passed: two engine-driven cycles, normal/domain-reload-disabled entry, boot, pause/countdown, scene reload, actual-module synthetic mouse/touch and owned-object teardown. Full clear/save/two-start loop="
                + results.fullLoopIncluded + ". Personal settings and progress unchanged.");
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
            public bool succeeded, personalPreferencesUnchanged, fullLoopIncluded;
            public List<Cycle> cycles = new List<Cycle>();
        }
        [Serializable] private sealed class Cycle
        {
            public bool domainReloadDisabled;
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
                Call(app, "RequestToggleRunPause");
                GreedLastRunSnapshot frozen = run;
                until = Time.realtimeSinceStartup + 0.4f;
                while (Time.realtimeSinceStartup < until) { AssertFrozen(frozen); yield return null; }
                Call(app, "RequestToggleRunPause");
                Require(run.ResumeCountdownActive, "Resume must start the lead-in countdown.");
                deadline = Time.realtimeSinceStartup + 8f;
                while (run.Paused)
                { AssertFrozen(frozen); Require(Time.realtimeSinceStartup < deadline, "Engine-driven resume countdown did not finish."); yield return null; }
                report.pauseCycles++;
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
                    && report.canceledEngineCommands == 1,
                    "The real engine input module must cover mouse, touch and interrupted command release.");
                report.pauseCycles++;
                report.lifecycleJudgements = report.judgements;
                yield return VerifyCalibratedTouchAndAudioRecovery();
                yield return VerifyBlockedEngineFrame();
                yield return VerifyAudioBackgroundOrders();
                if (SessionState.GetBool(Prefix + "fullLoop", false)) yield return VerifyFullLoop();
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
                while (!run.Stopped)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The real-time normal run did not complete within its bounded deadline.");
                    if (run.ChoiceActive && Time.realtimeSinceStartup >= nextChoiceAt)
                    {
                        GreedLastChoiceKind before = run.ChoiceKind;
                        yield return TapTouch(Read<Button[]>(ui, "laneButtons")[0]);
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
                        yield return TapTouch(lane);
                        Require(report.judgements == before + 1, "A full normal-run touch must resolve exactly one actual trap.");
                    }
                    yield return null;
                }
                Require(run.EscapeSucceeded && run.SaveEligible && run.Health > 0 && report.normalChoices >= 4,
                    "The real-time normal loop must recover the core, pass the exit and retain one saveable result.");
                report.fullNormalClear = true;
                report.fullNormalJudgements = report.judgements;
                yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && backend.HasSaveLoadoutCandidate
                    && !Read<Button>(ui, "nextPatternButton").IsInteractable(),
                    "The clear action must open a save window locked until explicit slot selection.");
                report.savedSlotIndex = 1;
                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[report.savedSlotIndex]);
                yield return WaitSeconds(0.4f);
                yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                GreedLastLobbySnapshot saved = backend.LoadLobby();
                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                    && saved.SaveSlotOccupied[1] && !saved.SaveSlotOccupied[0] && !saved.SaveSlotOccupied[2],
                    "One actual save must consume the candidate into selected S2 only.");
                yield return ClickMouse(Read<Button>(ui, "saveLoadoutButton"));
                Require(!backend.HasSaveLoadoutCandidate && Read<int[]>(backend, "saveLoadoutUsesRemaining")[1] == 2,
                    "Opening save management again must not recreate a candidate or duplicate the saved result.");
                report.candidateConsumedOnce = true;
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));

                yield return PrepareInfinite();
                yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                Require(state.CurrentState == GreedLastScreenState.LobbyReady
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[1] == 2,
                    "Canceling complete preparation must preserve both actual starts.");
                report.preparationCancelPreservedUses = true;
                for (int use = 0; use < 2; use++)
                {
                    yield return PrepareInfinite();
                    yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && run.InfiniteMode && !run.Stopped
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[1] == 1 - use
                        && backend.LoadLobby().SaveSlotOccupied[1] == (use == 0),
                        "Only actual infinite start may consume a use, with immediate source auto-clear on its second start.");
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
                    yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                        "Each real-time infinite result must return to the lobby normally.");
                }
                report.infiniteNaturalFailures = true;
                Require(!backend.LoadLobby().SaveSlotOccupied[1] && !backend.TryConsumeSelectedSaveLoadout(out _, out _, out _),
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
                int uses = Read<int[]>(backend, "saveLoadoutUsesRemaining")[1];
                yield return ClickMouse(Read<Button>(ui, "infiniteButton"));
                Require(state.CurrentState == GreedLastScreenState.InfiniteStartReady, "Infinite entry must open preparation, not consume a use.");
                yield return WaitSeconds(0.4f);
                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[0]);
                Require(Read<int>(app, "infinitePreparationStep") == 1, "The engine gift tap must resolve just one preparation step.");
                yield return WaitSeconds(0.4f);
                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[1]);
                Require(Read<int>(app, "infinitePreparationStep") == 2
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[1] == uses,
                    "Gift/relic preparation must remain separate from actual start consumption.");
                yield return WaitSeconds(0.4f);
            }

            private static IEnumerator WaitSeconds(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until) yield return null;
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
            }

            private static Vector2 ScreenPoint(Button button)
            {
                Canvas canvas = button.GetComponentInParent<Canvas>();
                RectTransform rect = button.GetComponent<RectTransform>();
                return RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center));
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
