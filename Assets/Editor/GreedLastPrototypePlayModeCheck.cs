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

        [MenuItem("Greed Last/Run Engine Audio Listener Ownership Check")]
        public static void RunAudioListenerOwnership() => BeginCheck(false, false, true);

        [MenuItem("Greed Last/Run Engine Initial Choice Boundary Check")]
        public static void RunInitialChoiceBoundary() => BeginCheck(false, false, false, true);

        [MenuItem("Greed Last/Run Engine Input System Ownership Check")]
        public static void RunInputSystemOwnership() => BeginCheck(false, false, false, false, true);

        [MenuItem("Greed Last/Run Engine Trap Cue Boundary Check")]
        public static void RunTrapCueBoundary() => BeginCheck(false, false, false, false, false, true);

        private static void BeginCheck(bool fullLoop, bool multiTouchOnly = false, bool audioListenerOnly = false,
            bool initialChoiceOnly = false, bool inputSystemOnly = false, bool trapCueOnly = false)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode
                && !SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false),
                "The engine lifecycle check requires an idle Edit-mode editor.");
            RequireAsyncBatchLifetime();
            VerifyPreferenceCaptureCoverage();
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
            SessionState.SetBool(Prefix + "audioListenerOnly", audioListenerOnly);
            SessionState.SetBool(Prefix + "initialChoiceOnly", initialChoiceOnly);
            SessionState.SetBool(Prefix + "inputSystemOnly", inputSystemOnly);
            SessionState.SetBool(Prefix + "trapCueOnly", trapCueOnly);
            SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, true);
            EditorSceneManager.playModeStartScene = scene;
            BeginCycle();
        }

        [MenuItem("Greed Last/Run Engine Play Mode Lifecycle Check", true)]
        [MenuItem("Greed Last/Run Engine Play Mode Full Loop Check", true)]
        [MenuItem("Greed Last/Run Engine Multi Touch Check", true)]
        [MenuItem("Greed Last/Run Engine Audio Listener Ownership Check", true)]
        [MenuItem("Greed Last/Run Engine Initial Choice Boundary Check", true)]
        [MenuItem("Greed Last/Run Engine Input System Ownership Check", true)]
        [MenuItem("Greed Last/Run Engine Trap Cue Boundary Check", true)]
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

        public static void VerifyPreferenceCaptureCoverage()
        {
            Require(CapturePreferences().values.Exists(value => value.key == GreedLastRunCore.InputTimingOffsetPrefsKey),
                "Personal preference comparisons must include the production input timing correction key.");
        }

        private static void BeginCycle()
        {
            int cycle = SessionState.GetInt(Prefix + "cycle", 0);
            EditorSettings.enterPlayModeOptions = cycle == 1
                ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
            EditorSettings.enterPlayModeOptionsEnabled = cycle == 1;
            SessionState.SetString(Prefix + "domainBeforeEntry", DomainIdentity);
            // Added natural choice courses and resume lead-ins retain their individual state/wait bounds.
            int cycleSeconds = SessionState.GetBool(Prefix + "fullLoop", false) ? 390
                : SessionState.GetBool(Prefix + "multiTouchOnly", false) ? 100 : 240;
            SessionState.SetString(Prefix + "deadline", DateTime.UtcNow.AddSeconds(cycleSeconds).ToString("O"));
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
            results.audioListenerOnly = SessionState.GetBool(Prefix + "audioListenerOnly", false);
            results.initialChoiceOnly = SessionState.GetBool(Prefix + "initialChoiceOnly", false);
            results.inputSystemOnly = SessionState.GetBool(Prefix + "inputSystemOnly", false);
            results.trapCueOnly = SessionState.GetBool(Prefix + "trapCueOnly", false);
            results.multiTouchIncluded = results.fullLoopIncluded || results.multiTouchOnly;
            if (results.multiTouchOnly)
                results.scope = "Actual engine frames and EventSystem synthetic multi-touch: growth command repeat/held-release protection, six save-management held-confirmation paths, six held-command slot-change paths, first-input-only across three practice actions, held fingers across opening choice/countdown, fresh tap recovery and UI cleanup in two domain-reload modes. No manual Tick, clock edits, hardware touch, rendered-image, audio-latency or device FPS testing.";
            if (results.audioListenerOnly)
                results.scope = "Actual engine listener/camera ownership and scene reload in two domain-reload modes: independent enabled/disabled external listeners, borrowed camera without listener, owned fallback, external handoff and cleanup. Isolated non-persisting backend. No audible output, device reset, audio latency or hardware performance measurement.";
            if (results.initialChoiceOnly)
                results.scope = "Actual engine first chapter courses with natural final input omission, two clean/detailed initial node-choice boundaries, four changed-slot/save-to-lobby publication boundaries and fresh touch/keyboard-router recovery in two domain-reload modes. Synthetic EventSystem taps and boolean keyboard callbacks, not OS keyboard input. No manual core Tick, private clock edits, rendered-image or audio latency testing; isolated non-persisting progress.";
            if (results.inputSystemOnly)
                results.scope = "Actual engine input-system ownership in two domain-reload modes: disabled external event system, disabled/missing external module, usable borrowed input, two real scene handoffs, UI re-enable after borrowed input loss and mouse/touch command recovery. External settings and personal preferences preserved; isolated non-persisting progress. No hardware input, rendered-image or latency testing.";
            if (results.trapCueOnly)
                results.scope = "Actual engine Update/LateUpdate in two domain-reload modes: clean/detailed practice, real-time public core input followed by a same-frame next-pattern command after UI Update, entry/target cue-state recovery, unchanged redraw deduplication and pause/resume recovery on the same trap. Synthetic pointer commands and in-memory calibration only. No manual core Tick, clock edits, audible output, hardware input or FPS measurement.";
            results.error = SessionState.GetString(Prefix + "error", string.Empty);
            var before = JsonUtility.FromJson<Preferences>(SessionState.GetString(Prefix + "preferences", "{}"));
            results.personalPreferenceKeys = before.values.ConvertAll(value => value.key);
            results.personalPreferencesUnchanged = JsonUtility.ToJson(before) == JsonUtility.ToJson(CapturePreferences());
            if (!results.personalPreferencesUnchanged) results.error += " Personal PlayerPrefs changed during the check.";
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Prefix + "previousOptions", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Prefix + "previousOptionsEnabled", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(Prefix + "previousScene", string.Empty));
            SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, false);
            results.succeeded = string.IsNullOrEmpty(results.error) && results.cycles.Count == 2;
            if (results.succeeded && results.audioListenerOnly)
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.audioListenerOwnershipCases == 4
                    && cycle.audioListenerSceneHandoffs == 2 && cycle.audioListenerOwnershipPreserved);
                if (!results.succeeded) results.error = "Required audio listener ownership coverage did not complete.";
            }
            if (results.succeeded && results.initialChoiceOnly)
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.initialChoiceBoundaryCases == 2
                    && cycle.initialChoiceFreshInputRecovered && cycle.screenKeyboardTransitionCases == 4
                    && cycle.screenKeyboardFreshInputRecovered);
                if (!results.succeeded) results.error = "Required initial-choice boundary coverage did not complete.";
            }
            if (results.succeeded && (results.inputSystemOnly || results.fullLoopIncluded))
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.inputSystemOwnershipCases == 4
                    && cycle.inputSystemSceneHandoffs == 2 && cycle.inputSystemOwnershipPreserved
                    && cycle.inputSystemReenableRecovered);
                if (!results.succeeded) results.error = "Required input-system ownership coverage did not complete.";
            }
            if (results.succeeded && (results.trapCueOnly || results.fullLoopIncluded))
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.trapCueEngineCases == 2
                    && cycle.trapCueEngineRecovery && cycle.trapCueResumeCases == 2);
                if (!results.succeeded) results.error = "Required engine trap cue coverage did not complete.";
            }
            if (results.succeeded && !results.audioListenerOnly && !results.initialChoiceOnly && !results.inputSystemOnly && !results.trapCueOnly)
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.growthCommandCases == 3
                    && cycle.growthFirstCommandOnly && cycle.growthHeldReleaseCanceled && cycle.growthFreshTapRecovered
                    && cycle.growthEntryHeldTapCanceled && cycle.heldSaveConfirmationCases == 6
                    && cycle.heldSaveConfirmationsPreservedData && cycle.heldSaveConfirmationFreshTapRecovered
                    && cycle.heldConfirmationResolutionCases == 9 && cycle.heldConfirmationResolutionsPreservedData
                    && cycle.heldConfirmationResolutionFreshTapRecovered && cycle.heldConfirmationOccupiedNeighborPreserved
                    && cycle.heldSaveExitConfirmationCases == 2 && cycle.heldSaveExitsPreservedData
                    && cycle.heldSaveExitFreshTapRecovered
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
                        && cycle.registeredHeldCommandPresses == 44
                        && cycle.recoveredCandidateSlotChoiceCases == 9 && cycle.recoveredCandidateSlotsPreserved
                        && cycle.legacyRenamedCandidateRepairCases == 3
                        && cycle.preparedInfinitePreviewCases == 8
                        && cycle.liveRewardPreviewJudgements >= 17
                        && cycle.engineRecoveryGoodPreviews == 3 && cycle.engineRecoveryDamageCases == 2
                        && cycle.engineRecoveryActualHealing == 3 && cycle.engineRecoveryCapPreserved
                        && cycle.engineThreatPreviewBoundaries > 0
                        && cycle.engineStableFinisherPreviews > 0 && cycle.engineStableFocusFallbacks > 0
                        && cycle.engineChoiceHealthPreviews > 0 && cycle.engineCappedChoiceHealthPreviews > 0
                        && cycle.engineChoiceFocusPreviews > 0 && cycle.engineLiveFocusFeedbackCases > 0
                        && cycle.engineResolvedBonusCases > 0 && cycle.engineResolvedBonusThreatBoundaries > 0
                        && cycle.engineGoodFocusFeedbackCases > 0
                        && cycle.engineGoodFocusStateCases == 3
                        && cycle.savedSlotIndex == (cycle.domainReloadDisabled ? 2 : 1));
                if (!results.succeeded) results.error = "Required engine multi-touch coverage did not complete.";
            }
            if (results.succeeded && !results.multiTouchOnly && !results.audioListenerOnly && !results.initialChoiceOnly && !results.inputSystemOnly && !results.trapCueOnly)
            {
                results.succeeded = results.cycles.TrueForAll(cycle => cycle.transitionFrameCommandCases == 4
                    && cycle.transitionFrameCommandsPreservedRun && cycle.transitionFrameFreshTapRecovered
                    && cycle.orphanTouchCommandCases == 6 && cycle.orphanTouchLaneCases == 6
                    && cycle.orphanTouchesIgnored && cycle.orphanTouchFreshTapRecovered && cycle.presentationSettingsRestored
                    && cycle.keyboardTransitionCases == 10 && cycle.keyboardTransitionFreshInputRecovered
                    && cycle.audioListenerOwnershipCases == 4 && cycle.audioListenerSceneHandoffs == 2
                    && cycle.audioListenerOwnershipPreserved && cycle.initialChoiceBoundaryCases == 2
                    && cycle.initialChoiceFreshInputRecovered && cycle.screenKeyboardTransitionCases == 4
                    && cycle.screenKeyboardFreshInputRecovered);
                if (!results.succeeded) results.error = "Required fresh-command, touch-boundary and presentation restoration coverage did not complete.";
            }
            try
            {
                string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "PlayModeChecks");
                Directory.CreateDirectory(directory);
                results.archivedReport = "report-" + (results.trapCueOnly ? "trap-cue-" : results.inputSystemOnly ? "input-system-" : results.audioListenerOnly ? "audio-listener-"
                    : results.initialChoiceOnly ? "initial-choice-" : results.multiTouchOnly ? "multi-touch-"
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
            keys.Add(GreedLastRunCore.InputTimingOffsetPrefsKey);
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
            public bool succeeded, personalPreferencesUnchanged, fullLoopIncluded, multiTouchIncluded, multiTouchOnly, audioListenerOnly, initialChoiceOnly, inputSystemOnly, trapCueOnly;
            public List<Cycle> cycles = new List<Cycle>();
            public List<string> personalPreferenceKeys = new List<string>();
        }
        [Serializable] private sealed class Cycle
        {
            public bool domainReloadDisabled;
            public int growthCommandCases;
            public bool growthFirstCommandOnly, growthHeldReleaseCanceled, growthFreshTapRecovered;
            public bool growthEntryHeldTapCanceled;
            public int heldSaveConfirmationCases, terminalBackSettlements;
            public bool heldSaveConfirmationsPreservedData, heldSaveConfirmationFreshTapRecovered, clearBackSaveProtected;
            public int heldConfirmationResolutionCases;
            public bool heldConfirmationResolutionsPreservedData, heldConfirmationResolutionFreshTapRecovered;
            public bool heldConfirmationOccupiedNeighborPreserved;
            public int heldSaveExitConfirmationCases;
            public bool heldSaveExitsPreservedData, heldSaveExitFreshTapRecovered;
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
            public bool presentationSettingsRestored;
            public int transitionFrameCommandCases;
            public bool transitionFrameCommandsPreservedRun, transitionFrameFreshTapRecovered;
            public int orphanTouchCommandCases, orphanTouchLaneCases;
            public bool orphanTouchesIgnored, orphanTouchFreshTapRecovered;
            public int keyboardTransitionCases;
            public bool keyboardTransitionFreshInputRecovered;
            public int audioListenerOwnershipCases, audioListenerSceneHandoffs;
            public bool audioListenerOwnershipPreserved;
            public int inputSystemOwnershipCases, inputSystemSceneHandoffs;
            public bool inputSystemOwnershipPreserved;
            public bool inputSystemReenableRecovered;
            public int trapCueEngineCases;
            public int trapCueResumeCases;
            public bool trapCueEngineRecovery;
            public int initialChoiceBoundaryCases;
            public bool initialChoiceFreshInputRecovered;
            public int screenKeyboardTransitionCases;
            public int recoveredCandidateSlotChoiceCases;
            public int legacyRenamedCandidateRepairCases;
            public int preparedInfinitePreviewCases;
            public int liveRewardPreviewJudgements;
            public int engineRecoveryGoodPreviews, engineRecoveryDamageCases, engineRecoveryActualHealing;
            public bool engineRecoveryCapPreserved;
            public int engineThreatPreviewBoundaries;
            public int engineStableFinisherPreviews, engineStableFocusFallbacks;
            public int engineChoiceHealthPreviews, engineCappedChoiceHealthPreviews;
            public int engineChoiceFocusPreviews, engineLiveFocusFeedbackCases;
            public int engineResolvedBonusCases, engineResolvedBonusThreatBoundaries;
            public int engineGoodFocusFeedbackCases;
            public int engineGoodFocusStateCases;
            public bool recoveredCandidateSlotsPreserved;
            public bool screenKeyboardFreshInputRecovered;
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
            private Action lateCueAction;

            private void Start()
            {
                startedAt = Time.realtimeSinceStartup;
                report.domainReloadDisabled = SessionState.GetInt(Prefix + "cycle", 0) == 1;
                Application.logMessageReceived += CollectError;
                StartCoroutine(Guard(RunCycle()));
            }

            private void Update() { report.frames++; }

            private void LateUpdate()
            {
                Action action = lateCueAction;
                lateCueAction = null;
                if (action == null) return;
                try { action(); }
                catch (Exception error) { loggedError = error.ToString(); }
            }

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
                if (SessionState.GetBool(Prefix + "audioListenerOnly", false))
                {
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    yield return VerifyAudioListenerOwnership();
                    yield break;
                }
                if (SessionState.GetBool(Prefix + "inputSystemOnly", false))
                {
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    yield return VerifyInputSystemOwnership();
                    yield break;
                }
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
                if (SessionState.GetBool(Prefix + "initialChoiceOnly", false))
                {
                    yield return VerifyInitialRunChoiceBoundary();
                    yield return VerifyScreenKeyboardTransitions();
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    yield break;
                }
                if (SessionState.GetBool(Prefix + "trapCueOnly", false))
                {
                    yield return VerifyEngineTrapCueBoundaries();
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    yield break;
                }
                if (!SessionState.GetBool(Prefix + "multiTouchOnly", false)) yield return VerifyEngineTrapCueBoundaries();
                if (!SessionState.GetBool(Prefix + "multiTouchOnly", false)) yield return VerifyKeyboardTransitionFrames();
                if (!SessionState.GetBool(Prefix + "multiTouchOnly", false)) yield return VerifyInitialRunChoiceBoundary();
                if (!SessionState.GetBool(Prefix + "multiTouchOnly", false)) yield return VerifyScreenKeyboardTransitions();
                if (SessionState.GetBool(Prefix + "fullLoop", false)) yield return VerifyRecoveredCandidateSlotChoices();
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
                yield return VerifyOrphanTouchInput();
                yield return VerifyTransitionFrameCommands();
                yield return VerifyAudioBackgroundOrders();
                yield return VerifySuspendedFreshInput();
                if (SessionState.GetBool(Prefix + "fullLoop", false))
                {
                    yield return VerifyRecoveryPreviews();
                    yield return VerifyGoodFocusStates();
                    yield return VerifyMultiTouch();
                    yield return VerifyFullLoop();
                }
                AudioClip ownedTone = Read<AudioClip>(ui, "beatClip");
                AudioSource ownedSource = Read<AudioSource>(ui, "rhythmAudio");
                GameObject formerHost = app.gameObject;
                AudioSource unrelatedSource = formerHost.AddComponent<AudioSource>();
                unrelatedSource.playOnAwake = false;
                int priorQuality = Read<int>(app, "previousQualityLevel");
                int priorVSync = Read<int>(app, "previousVSyncCount");
                int priorFrameRate = Read<int>(app, "previousTargetFrameRate");
                int priorSleep = Read<int>(app, "previousSleepTimeout");
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
                Require(QualitySettings.GetQualityLevel() == priorQuality && QualitySettings.vSyncCount == priorVSync
                    && Application.targetFrameRate == priorFrameRate
                    && Screen.sleepTimeout == priorSleep,
                    "Actual app removal must restore the presentation settings captured before its Awake.");
                report.presentationSettingsRestored = true;
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
                yield return VerifyAudioListenerOwnership();
                yield return VerifyInputSystemOwnership();
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

            private IEnumerator VerifyOrphanTouchInput()
            {
                bool originalCleanView = Read<bool>(ui, "devCleanViewEnabled");
                TouchPhase[] firstPhases = { TouchPhase.Ended, TouchPhase.Stationary, TouchPhase.Moved };
                try
                {
                    for (int controls = 0; controls < 2; controls++)
                    {
                        if (Read<bool>(ui, "devCleanViewEnabled") != (controls == 0)) ui.ToggleDevCleanView();
                        Require(run.ActivePattern && !run.Paused && !run.Stopped,
                            "Orphan touch input must start on a live trap.");
                        Call(app, "HandlePresentationSuspended");
                        yield return null;
                        yield return null;
                        Button resume = Read<Button>(ui, "nextPatternButton");
                        Vector2 point = ScreenPoint(resume);
                        Require(resume.isActiveAndEnabled && resume.IsInteractable() && RaycastButton(point) == resume,
                            "Orphan command touches must target the already presented, active resume command.");
                        GreedLastRunSnapshot frozen = run;
                        for (int phase = 0; phase < firstPhases.Length; phase++)
                        {
                            yield return TouchFrame(TouchAt(121 + phase, point, firstPhases[phase]));
                            int pending = Read<HashSet<PointerEventData>>(resume, "pendingPresses").Count;
                            if (firstPhases[phase] != TouchPhase.Ended)
                                yield return TouchFrame(TouchAt(121 + phase, point, TouchPhase.Ended));
                            yield return TouchFrame();
                            Require(pending == 0 && run.Paused && !run.ResumeCountdownActive
                                && state.CurrentState == GreedLastScreenState.RunCoreTest
                                && run.Pattern.PatternId == frozen.Pattern.PatternId
                                && Mathf.Abs(run.ElapsedSeconds - frozen.ElapsedSeconds) < 0.0001f
                                && run.MissCount == frozen.MissCount,
                                "A touch first reported without Began must not register or execute resume: "
                                    + $"controls={controls}, phase={firstPhases[phase]}, pending={pending}, "
                                    + $"paused={run.Paused}, countdown={run.ResumeCountdownActive}.");
                            AssertFrozen(frozen);
                            report.orphanTouchCommandCases++;
                        }
                        yield return TapTouch(resume, 121);
                        Require(run.ResumeCountdownActive, "A fresh Began/Ended tap must still resume after orphan contacts.");
                        float deadline = Time.realtimeSinceStartup + 8f;
                        while (run.Paused)
                        { AssertFrozen(frozen); Require(Time.realtimeSinceStartup < deadline, "Orphan touch recovery did not resume."); yield return null; }
                        Require(run.ActivePattern && run.Pattern.PatternId == frozen.Pattern.PatternId
                            && run.ElapsedSeconds < frozen.ElapsedSeconds && run.MissCount == frozen.MissCount,
                            "Orphan contact recovery must preserve the same trap with the normal lead-in.");

                        Button lane = Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1];
                        point = ScreenPoint(lane);
                        Require(lane.isActiveAndEnabled && lane.IsInteractable() && RaycastButton(point) == lane,
                            "Orphan lane contacts must target the active lane through the actual raycast.");
                        int judgements = report.judgements;
                        int score = run.Score, health = run.Health, misses = run.MissCount;
                        for (int phase = 0; phase < firstPhases.Length; phase++)
                        {
                            yield return TouchFrame(TouchAt(121 + phase, point, firstPhases[phase]));
                            Require(report.judgements == judgements
                                && Read<HashSet<int>[]>(ui, "laneHeldPointers")[(int)run.Pattern.Channel - 1].Count == 0,
                                "A lane touch first reported without Began must not judge or register a hold: "
                                    + $"controls={controls}, phase={firstPhases[phase]}, judgements={report.judgements}/{judgements}.");
                            if (firstPhases[phase] != TouchPhase.Ended)
                                yield return TouchFrame(TouchAt(121 + phase, point, TouchPhase.Ended));
                            yield return TouchFrame();
                            Require(report.judgements == judgements && run.Score == score && run.Health == health
                                && run.MissCount == misses && run.ActivePattern,
                                "Orphan lane contacts must not consume the trap or create score/damage.");
                            report.orphanTouchLaneCases++;
                        }
                        yield return WaitForTouchablePattern();
                        yield return TapTouch(lane, 121);
                        Require(report.judgements == judgements + 1 && run.MissCount == misses
                            && (run.LastResult == GreedLastJudgementResult.Success || run.LastResult == GreedLastJudgementResult.Good),
                            "The same finger ID with a fresh Began must judge exactly once after orphan contacts.");
                        if (run.Stopped)
                        {
                            Require(run.PracticeMode && core.CanExitAfterPractice,
                                "A successful fresh practice touch may finish practice, not fail the run.");
                            yield return null;
                            yield return ClickMouse(Read<Button>(ui, "returnLobbyButton"));
                            yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                        }
                        deadline = Time.realtimeSinceStartup + 8f;
                        while (!run.ActivePattern || run.CountdownActive)
                        { Require(!run.Stopped && Time.realtimeSinceStartup < deadline, "Fresh orphan recovery did not reach the next trap."); yield return null; }
                    }
                    report.orphanTouchesIgnored = report.orphanTouchCommandCases == 6 && report.orphanTouchLaneCases == 6;
                    report.orphanTouchFreshTapRecovered = report.orphanTouchesIgnored;
                    Debug.Log("Greed engine orphan touch protection passed: Ended/Stationary/Moved without Began "
                        + "ignored on commands and lanes in clean/detailed controls; fresh taps recovered.");
                }
                finally
                {
                    if (Read<bool>(ui, "devCleanViewEnabled") != originalCleanView) ui.ToggleDevCleanView();
                }
            }

            private IEnumerator VerifyTransitionFrameCommands()
            {
                bool originalCleanView = Read<bool>(ui, "devCleanViewEnabled");
                try
                {
                    for (int controls = 0; controls < 2; controls++)
                    {
                        if (Read<bool>(ui, "devCleanViewEnabled") != (controls == 0))
                            ui.ToggleDevCleanView();
                        for (int device = 0; device < 2; device++)
                        {
                            Require(run.ActivePattern && !run.Paused && !run.Stopped,
                                "Transition-frame input must start on a live, presented trap.");
                            Call(app, "HandlePresentationSuspended");
                            yield return null;
                            Button command = Read<Button>(ui, "nextPatternButton");
                            Vector2 point = ScreenPoint(command);
                            yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                            float deadline = Time.realtimeSinceStartup + 8f;
                            while (run.Paused)
                            { Require(Time.realtimeSinceStartup < deadline, "Fixture resume did not finish."); yield return null; }
                            yield return WaitSeconds(0.15f);
                            Canvas.ForceUpdateCanvases();
                            Require(RaycastButton(point) is not GreedLastCommandButton,
                                "The new menu target must cover previously non-command playfield, not a visible old menu command.");
                            int callbacks = 0;
                            GreedLastRunSnapshot frozen = default;
                            // Interrupt at the actual module's input read, before its new-screen raycast/Down.
                            Action interrupt = () =>
                            {
                                callbacks++;
                                Call(app, "HandlePresentationSuspended");
                                Canvas.ForceUpdateCanvases();
                                frozen = run;
                                Require(RaycastButton(point) == command && command.isActiveAndEnabled
                                    && command.IsInteractable() && run.Paused && !run.ResumeCountdownActive,
                                    "The interruption must expose the intended menu target in the input-read frame: "
                                        + $"controls={controls}, device={device}, "
                                        + $"hit={RaycastButton(point)?.name}, expected={command.name}, "
                                        + $"paused={run.Paused}, countdown={run.ResumeCountdownActive}, stopped={run.Stopped}.");
                            };
                            if (device == 0)
                            {
                                input.BeforeTouchRead = interrupt;
                                yield return TouchFrame(TouchAt(120, point, TouchPhase.Began));
                                yield return TouchFrame(TouchAt(120, point, TouchPhase.Stationary));
                            }
                            else
                            {
                                input.Touches = Array.Empty<Touch>();
                                input.MouseEnabled = input.MouseDown = input.MouseHeld = true;
                                input.MouseUp = false;
                                input.MousePosition = point;
                                input.BeforeMouseRead = interrupt;
                                yield return null;
                                input.MouseDown = false;
                                yield return null;
                            }
                            Require(callbacks == 1, "The input-read interruption must happen exactly once.");
                            int pending = Read<HashSet<PointerEventData>>(command, "pendingPresses").Count;
                            yield return WaitSeconds(0.45f);
                            if (device == 0)
                            {
                                yield return TouchFrame(TouchAt(120, point, TouchPhase.Ended));
                                yield return TouchFrame();
                            }
                            else yield return ReleaseMouse();
                            Require(pending == 0 && state.CurrentState == GreedLastScreenState.RunCoreTest
                                && run.Paused && !run.ResumeCountdownActive && !run.Stopped
                                && run.Pattern.PatternId == frozen.Pattern.PatternId
                                && Mathf.Abs(run.ElapsedSeconds - frozen.ElapsedSeconds) < 0.0001f
                                && run.MissCount == frozen.MissCount,
                                "An input begun before the new pause menu was presented must not resume or abandon on release: "
                                    + $"controls={controls}, device={device}, pending={pending}, "
                                    + $"state={state.CurrentState}, paused={run.Paused}, countdown={run.ResumeCountdownActive}");
                            AssertFrozen(frozen);
                            report.transitionFrameCommandCases++;
                            if (device == 0) yield return TapTouch(Read<Button>(ui, "nextPatternButton"));
                            else yield return ClickMouse(Read<Button>(ui, "nextPatternButton"));
                            Require(run.ResumeCountdownActive,
                                "A fresh input after the transition frame must still start the ordinary resume lead-in.");
                            deadline = Time.realtimeSinceStartup + 8f;
                            while (run.Paused)
                            { AssertFrozen(frozen); Require(Time.realtimeSinceStartup < deadline, "Fresh transition input did not resume."); yield return null; }
                            Require(run.ActivePattern && run.Pattern.PatternId == frozen.Pattern.PatternId
                                && run.ElapsedSeconds < frozen.ElapsedSeconds && run.MissCount == frozen.MissCount,
                                "Fresh input recovery must retain the same trap without charging a miss.");
                        }
                    }
                    report.transitionFrameCommandsPreservedRun = report.transitionFrameCommandCases == 4;
                    report.transitionFrameFreshTapRecovered = report.transitionFrameCommandsPreservedRun;
                    Debug.Log("Greed engine transition-frame protection passed: touch/mouse resume, clean/detailed "
                        + "controls; unseen menu presses canceled, same trap preserved and fresh resume recovered.");
                }
                finally
                {
                    input.BeforeTouchRead = input.BeforeMouseRead = null;
                    if (Read<bool>(ui, "devCleanViewEnabled") != originalCleanView) ui.ToggleDevCleanView();
                }
            }

            private static Button RaycastButton(Vector2 point)
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                return hits.Count == 0 ? null : hits[0].gameObject.GetComponentInParent<Button>();
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

            private IEnumerator VerifyEngineTrapCueBoundaries()
            {
                bool playerControls = ui.UsePlayerRunControls;
                for (int mode = 0; mode < 2; mode++)
                {
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady, "Engine cue boundary must begin in the lobby.");
                    if (ui.UsePlayerRunControls != (mode == 0)) ui.ToggleDevCleanView();
                    typeof(GreedLastRunCore).GetField("inputTimingOffsetSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(core, 0f);
                    yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while (!run.ActivePattern || !Read<bool>(ui, "targetCuePlayed") || run.CoreDeltaSeconds < -0.025f)
                    {
                        Require(!run.Paused && !run.Stopped && Time.realtimeSinceStartup < deadline,
                            "Engine cue fixture must reach its first live target without a clock rewrite.");
                        yield return null;
                    }
                    bool observed = false;
                    lateCueAction = () =>
                    {
                        int frame = Time.frameCount;
                        Require(Read<bool>(ui, "lastActivePatternVisual") && Read<bool>(ui, "targetCuePlayed"),
                            "The first engine trap must have presented both cue states before LateUpdate.");
                        core.HandleInput(run.Pattern.Channel, Time.realtimeSinceStartup);
                        Require(!run.ActivePattern && run.SuccessCount == 1 && !run.Stopped,
                            "Real-time engine input must resolve the first exact judgement.");
                        Require(!Read<bool>(ui, "lastActivePatternVisual") && !Read<bool>(ui, "targetCuePlayed"),
                            "The inactive engine publication must rearm cues before another UI Update.");
                        core.RequestNextPattern(Time.realtimeSinceStartup);
                        Require(Time.frameCount == frame && run.ActivePattern
                            && !Read<bool>(ui, "lastActivePatternVisual") && !Read<bool>(ui, "targetCuePlayed"),
                            "Same-frame next-pattern command must not inherit the previous trap's cue state.");
                        observed = true;
                    };
                    yield return null;
                    Require(observed, "The cue boundary must execute in the real engine LateUpdate phase.");
                    deadline = Time.realtimeSinceStartup + 5f;
                    while (!Read<bool>(ui, "targetCuePlayed"))
                    {
                        Require(run.ActivePattern && !run.Paused && !run.Stopped && Time.realtimeSinceStartup < deadline,
                            "The next engine trap must recover its entry and target cue states.");
                        yield return null;
                    }
                    Require(Read<bool>(ui, "lastActivePatternVisual"), "The next engine entry must also recover.");
                    for (int repeat = 0; repeat < 3; repeat++) ui.RenderRun(run);
                    Require(Read<bool>(ui, "lastActivePatternVisual") && Read<bool>(ui, "targetCuePlayed"),
                        "Unchanged engine redraws must not rearm or replay cues.");
                    Call(app, "RequestToggleRunPause");
                    Require(run.Paused && !Read<bool>(ui, "lastActivePatternVisual") && !Read<bool>(ui, "targetCuePlayed"),
                        "Engine pause publication must rearm cues before a paused UI Update.");
                    GreedLastRunSnapshot pausedRun = run;
                    yield return TapTouch(Read<Button>(ui, "nextPatternButton"));
                    Require(run.ResumeCountdownActive, "A fresh engine resume must start its countdown.");
                    deadline = Time.realtimeSinceStartup + 6f;
                    while (run.Paused || run.CountdownActive)
                    {
                        Require(run.ActivePattern && run.Pattern.PatternId == pausedRun.Pattern.PatternId
                            && run.Score == pausedRun.Score && run.Health == pausedRun.Health
                            && run.SuccessCount == pausedRun.SuccessCount && run.Distance == pausedRun.Distance
                            && !Read<bool>(ui, "lastActivePatternVisual") && !Read<bool>(ui, "targetCuePlayed")
                            && Time.realtimeSinceStartup < deadline,
                            "Resume countdown must preserve the same trap and run values without consuming trap cues.");
                        yield return null;
                    }
                    Require(run.ActivePattern && run.Pattern.PatternId == pausedRun.Pattern.PatternId
                        && run.CoreDeltaSeconds < -0.1f && run.Score == pausedRun.Score && run.Health == pausedRun.Health,
                        "Fresh resume must keep the trap and grant its existing timing buffer.");
                    deadline = Time.realtimeSinceStartup + 5f;
                    while (!Read<bool>(ui, "targetCuePlayed"))
                    {
                        Require(run.ActivePattern && !run.Paused && !run.Stopped && Time.realtimeSinceStartup < deadline,
                            "The resumed engine trap must recover its entry and target cue states.");
                        yield return null;
                    }
                    Require(Read<bool>(ui, "lastActivePatternVisual") && run.Pattern.PatternId == pausedRun.Pattern.PatternId,
                        "The same resumed trap must present its entry and target again.");
                    report.trapCueResumeCases++;
                    Call(app, "RequestToggleRunPause");
                    yield return TapTouch(Read<Button>(ui, "returnLobbyButton"));
                    report.trapCueEngineCases++;
                }
                if (ui.UsePlayerRunControls != playerControls) ui.ToggleDevCleanView();
                report.trapCueEngineRecovery = report.trapCueEngineCases == 2;
                Debug.Log("Greed engine trap cue boundaries passed: two clean/detailed LateUpdate input-to-next-pattern publications, no intermediate UI Update, recovered entry/target states, unchanged redraw deduplication and same-trap pause/countdown/resume recovery with preserved values and timing buffer. Real time with synthetic public core/pointer commands, not hardware audio or FPS measurement.");
            }

            private IEnumerator VerifyInputSystemOwnership()
            {
                for (int scenario = 0; scenario < 4; scenario++)
                {
                    var external = new GameObject("GreedLastInputOwnerCheck", typeof(EventSystem));
                    EventSystem externalEvents = external.GetComponent<EventSystem>();
                    StandaloneInputModule externalModule = scenario == 2 ? null : external.AddComponent<StandaloneInputModule>();
                    externalEvents.enabled = scenario != 0;
                    if (externalModule != null) externalModule.enabled = scenario != 1;
                    externalEvents.sendNavigationEvents = true;
                    UnityEngine.Object.DontDestroyOnLoad(external);
                    InvokeStatic(typeof(GreedLastApp), "Create");
                    Attach(FindApp());
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while (state.CurrentState != GreedLastScreenState.LobbyReady)
                    { Require(Time.realtimeSinceStartup < deadline, "Input ownership app did not reach its lobby."); yield return null; }
                    EventSystem events = EventSystem.current;
                    Require(events != null && events.isActiveAndEnabled
                        && events.GetComponent<StandaloneInputModule>() != null
                        && events.GetComponent<StandaloneInputModule>().isActiveAndEnabled,
                        "The visible UI must have an enabled current pointer input module: scenario=" + scenario);
                    Require((events == externalEvents) == (scenario == 3)
                        && externalEvents.enabled == (scenario != 0)
                        && (externalModule == null || externalModule.enabled == (scenario != 1))
                        && externalEvents.sendNavigationEvents == (scenario != 3),
                        "Input recovery must borrow only usable systems without changing external enabled state.");
                    BindEnginePointer(events);
                    yield return null;
                    yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                    Require(run.PracticeMode && state.CurrentState == GreedLastScreenState.RunCoreTest,
                        "A recovered current input module must dispatch a real engine mouse command.");
                    yield return TapTouch(Read<Button>(ui, "pauseButton"));
                    Require(run.Paused, "A recovered current input module must dispatch a real engine touch command.");
                    if (scenario == 0)
                    {
                        GreedLastRunSnapshot frozen = run;
                        GameObject owned = Read<GameObject>(ui, "ownedEventSystem");
                        yield return PressMouse(Read<Button>(ui, "nextPatternButton"));
                        externalEvents.enabled = true;
                        yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                        yield return null;
                        yield return null;
                        Require(owned == null && Read<GameObject>(ui, "ownedEventSystem") == null
                            && EventSystem.current == externalEvents && !externalEvents.sendNavigationEvents,
                            "A usable external scene input system must replace only the owned fallback.");
                        BindEnginePointer(externalEvents);
                        input.MouseUp = true;
                        yield return null;
                        input.MouseUp = false;
                        yield return null;
                        AssertFrozen(frozen);
                        Require(run.Paused && !run.ResumeCountdownActive,
                            "A release from the old input system must not resume the paused run.");
                        report.inputSystemSceneHandoffs++;
                        yield return TapTouch(Read<Button>(ui, "returnLobbyButton"));
                        Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                            "Fresh touch must remain usable after input-system handoff.");
                        UnityEngine.Object.Destroy(external);
                        yield return null;
                        yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                        yield return null;
                        EventSystem fallback = EventSystem.current;
                        Require(fallback != null && fallback.gameObject == Read<GameObject>(ui, "ownedEventSystem")
                            && !fallback.sendNavigationEvents && Count<EventSystem>() == 1,
                            "After borrowed scene input unloads, the UI must recover one owned fallback.");
                        BindEnginePointer(fallback);
                        yield return null;
                        yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                        Require(run.PracticeMode, "Fresh mouse must work after the external input unloads.");
                        yield return TapTouch(Read<Button>(ui, "pauseButton"));
                        Require(run.Paused, "The newly recovered fallback must also open the pause menu.");
                        report.inputSystemSceneHandoffs++;
                    }
                    if (scenario == 3)
                    {
                        GreedLastRunSnapshot frozen = run;
                        ui.enabled = false;
                        yield return null;
                        externalModule.enabled = false;
                        yield return null;
                        ui.enabled = true;
                        yield return null;
                        EventSystem recovered = EventSystem.current;
                        Require(recovered != null && recovered.gameObject == Read<GameObject>(ui, "ownedEventSystem")
                            && !externalModule.enabled && externalEvents.enabled && externalEvents.sendNavigationEvents,
                            "Re-enabling the UI must recover unavailable borrowed input without changing external settings.");
                        AssertFrozen(frozen);
                        Require(run.Paused && !run.ResumeCountdownActive,
                            "Input-system recovery on UI enable must leave the run explicitly paused.");
                        BindEnginePointer(recovered);
                        yield return null;
                        yield return TapTouch(Read<Button>(ui, "returnLobbyButton"));
                        Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                            "Fresh touch must work after re-enabling an unavailable borrowed input system.");
                        externalModule.enabled = true;
                        report.inputSystemReenableRecovered = true;
                    }
                    else
                    {
                        yield return TapTouch(Read<Button>(ui, "returnLobbyButton"));
                    }
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    Require(CountNamed<EventSystem>("GreedLastEventSystem") == 0
                        && (external == null || (externalEvents.enabled == (scenario != 0)
                            && externalEvents.sendNavigationEvents
                            && (externalModule == null || externalModule.enabled == (scenario != 1)))),
                        "App teardown must release owned input and restore only borrowed navigation.");
                    if (external != null) UnityEngine.Object.Destroy(external);
                    yield return null;
                    Require(Count<EventSystem>() == 0, "The isolated input case must leave no test event systems.");
                    report.inputSystemOwnershipCases++;
                }
                report.inputSystemOwnershipPreserved = report.inputSystemOwnershipCases == 4
                    && report.inputSystemSceneHandoffs == 2;
                Debug.Log("Greed engine input-system ownership passed: four external enabled/module cases, two scene handoffs, UI re-enable recovery, actual synthetic mouse/touch commands, stale release cancellation and external settings preserved.");
            }

            private void BindEnginePointer(EventSystem events)
            {
                StandaloneInputModule module = events.GetComponent<StandaloneInputModule>();
                input = events.gameObject.AddComponent<GreedLastPrototypeKeyboardInput>();
                input.MouseEnabled = true;
                input.MousePosition = new Vector2(-1000f, -1000f);
                module.inputOverride = input;
            }

            private IEnumerator VerifyAudioListenerOwnership()
            {
                foreach (int scenario in new[] { 1, 0, 2, 3 })
                {
                    GameObject externalCamera = scenario == 0 ? null
                        : new GameObject("GreedLastListenerCameraCheck", typeof(Camera));
                    GameObject externalHost = scenario == 1 ? null
                        : new GameObject("GreedLastIndependentListenerCheck", typeof(AudioListener));
                    AudioListener externalListener = externalHost == null ? null : externalHost.GetComponent<AudioListener>();
                    bool externalEnabled = scenario == 0 || scenario == 3;
                    if (externalListener != null) externalListener.enabled = externalEnabled;
                    if (externalCamera != null) UnityEngine.Object.DontDestroyOnLoad(externalCamera);
                    if (externalHost != null) UnityEngine.Object.DontDestroyOnLoad(externalHost);
                    InvokeStatic(typeof(GreedLastApp), "Create");
                    Attach(FindApp());
                    yield return null;
                    Require(ActiveListenerCount() == 1,
                        "An independent listener and camera must provide exactly one enabled listener: scenario=" + scenario
                            + ", active=" + ActiveListenerCount());
                    Require(externalListener == null || externalListener.enabled == externalEnabled,
                        "Initializing the UI must not enable or disable an external listener.");
                    Require(Count<Camera>() == 1, "Camera recovery must not create a second borrowed camera.");
                    report.audioListenerOwnershipCases++;
                    if (scenario == 1)
                    {
                        externalHost = new GameObject("GreedLastListenerHandoffCheck", typeof(AudioListener));
                        externalListener = externalHost.GetComponent<AudioListener>();
                        externalEnabled = true;
                        UnityEngine.Object.DontDestroyOnLoad(externalHost);
                        yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                        yield return null;
                        Require(ActiveListenerCount() == 1 && externalListener.isActiveAndEnabled,
                            "A scene reload must release the owned fallback when an external listener is available.");
                        report.audioListenerSceneHandoffs++;
                        UnityEngine.Object.Destroy(externalHost);
                        yield return null;
                        yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                        yield return null;
                        Require(ActiveListenerCount() == 1 && externalCamera != null,
                            "After the external listener unloads, a borrowed camera must receive one owned fallback.");
                        externalListener = null;
                        report.audioListenerSceneHandoffs++;
                    }
                    UnityEngine.Object.Destroy(app.gameObject);
                    yield return null;
                    yield return null;
                    Require(ActiveListenerCount() == (externalListener != null && externalEnabled ? 1 : 0)
                        && (externalCamera == null ? Count<Camera>() == 0 : externalCamera != null && Count<Camera>() == 1)
                        && (externalListener == null || externalListener.enabled == externalEnabled),
                        "App removal must release only its camera/listener and preserve external enabled/disabled state.");
                    if (externalCamera != null) UnityEngine.Object.Destroy(externalCamera);
                    if (externalHost != null) UnityEngine.Object.Destroy(externalHost);
                    yield return null;
                    Require(Count<AudioListener>() == 0 && Count<Camera>() == 0,
                        "The isolated listener case must leave no owned or test resources.");
                }
                report.audioListenerOwnershipPreserved = report.audioListenerOwnershipCases == 4
                    && report.audioListenerSceneHandoffs == 2;
                Debug.Log("Greed engine audio listener ownership passed: four separate camera/listener cases, two real scene handoffs, one active listener, external settings preserved and owned resources released. Audible output is not measured.");
            }

            private static int ActiveListenerCount()
            {
                int count = 0;
                foreach (AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (listener.isActiveAndEnabled) count++;
                return count;
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
                        Vector2 point = ScreenPoint(command, -0.25f);
                        Vector2 heldPoint = ScreenPoint(command, 0.25f);
                        yield return TouchFrame(TouchAt(91, point, TouchPhase.Began), TouchAt(92, heldPoint, TouchPhase.Began));
                        PointerEventData[] registered = RequireRegisteredCommandPresses(command, 91, 92);
                        yield return TouchFrame(TouchAt(91, point, TouchPhase.Ended), TouchAt(92, heldPoint, TouchPhase.Stationary));
                        Require(Read<bool>(app, scenario.Flag) && backend.HasSaveLoadoutCandidate,
                            "The first engine release must open only the confirmation: " + scenario.Flag);
                        RequireCanceledCommandPresses(command, Array.FindAll(registered, pointer => pointer.pointerId == 92));
                        yield return TouchFrame(TouchAt(92, heldPoint, TouchPhase.Stationary));
                        yield return WaitSeconds(0.45f);
                        yield return TouchFrame(TouchAt(92, heldPoint, TouchPhase.Ended));
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
                    yield return VerifyHeldConfirmationResolutions();
                    yield return VerifyHeldSaveExitConfirmations(pendingFields);
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

            private IEnumerator VerifyHeldConfirmationResolutions()
            {
                foreach (var scenario in new[] {
                    (Infinite: false, Command: "saveSlotDeleteButton", Held: "saveSlotDeleteButton", Flag: "saveSlotDeleteConfirmPending", Commit: false),
                    (Infinite: true, Command: "saveSlotDeleteButton", Held: "saveSlotDeleteButton", Flag: "saveSlotDeleteConfirmPending", Commit: false),
                    (Infinite: false, Command: "saveSlotRenameButton", Held: "saveSlotRenameButton", Flag: "saveSlotRenameConfirmPending", Commit: false),
                    (Infinite: true, Command: "saveSlotRenameButton", Held: "saveSlotRenameButton", Flag: "saveSlotRenameConfirmPending", Commit: false),
                    (Infinite: false, Command: "saveSlotRenameButton", Held: "saveSlotRenameButton", Flag: "saveSlotRenameConfirmPending", Commit: true),
                    (Infinite: true, Command: "saveSlotRenameButton", Held: "saveSlotRenameButton", Flag: "saveSlotRenameConfirmPending", Commit: true),
                    (Infinite: false, Command: "saveSlotDeleteButton", Held: "returnLobbyButton", Flag: "saveSlotDeleteConfirmPending", Commit: true),
                    (Infinite: true, Command: "saveSlotDeleteButton", Held: "returnLobbyButton", Flag: "saveSlotDeleteConfirmPending", Commit: true),
                    (Infinite: false, Command: "returnLobbyButton", Held: "returnLobbyButton", Flag: "saveDraftSkipConfirmPending", Commit: false) })
                {
                    var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                    backend.AddSampleSaveLoadoutForTest(out _);
                    backend.AddSampleSaveLoadoutForTest(out _);
                    backend.SelectSaveLoadoutSlot(1);
                    backend.RenameSelectedSaveLoadoutSlot();
                    backend.AddSampleClearCandidateForTest(out _);
                    backend.SelectSaveLoadoutSlot(0);
                    typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, backend);
                    foreach (string field in new[] { "saveDraftRequiresExplicitSlotChoice", "saveDraftOverwriteConfirmPending",
                        "saveDraftSkipConfirmPending", "saveSlotDeleteConfirmPending", "saveSlotRenameConfirmPending",
                        "saveSlotDetailViewActive", "saveDraftFromRefillRun" })
                        typeof(GreedLastApp).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, false);
                    if (scenario.Infinite)
                        state.SetInfiniteLoadoutSelect(backend.LoadSelectedSaveSlotView(), backend.BuildInfiniteLoadoutSelectText());
                    else state.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSaveLoadoutDraftText(true),
                        saveLoadoutCandidateAvailable: true);
                    yield return null;
                    Button command = Read<Button>(ui, scenario.Command);
                    yield return TapTouch(command);
                    Require(Read<bool>(app, scenario.Flag), "The engine resolution fixture must first open confirmation.");
                    yield return WaitSeconds(0.45f);
                    var records = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
                    var uses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
                    var names = (int[])Read<int[]>(backend, "saveLoadoutRenameSteps").Clone();
                    Require(records[1].IsValid && uses[1] == 2 && names[1] == 1,
                        "The engine resolution fixture must retain an occupied renamed neighbor.");
                    Button heldCommand = Read<Button>(ui, scenario.Held);
                    Vector2 heldPoint = ScreenPoint(heldCommand, -0.25f);
                    Button resolver = scenario.Commit ? command : Read<Button>(ui, "saveSlotDetailButton");
                    Vector2 resolvePoint = ScreenPoint(resolver, 0.25f);
                    yield return TouchFrame(TouchAt(96, heldPoint, TouchPhase.Began));
                    PointerEventData[] held = RequireRegisteredCommandPresses(heldCommand, 96);
                    yield return TouchFrame(TouchAt(96, heldPoint, TouchPhase.Stationary), TouchAt(97, resolvePoint, TouchPhase.Began));
                    yield return TouchFrame(TouchAt(96, heldPoint, TouchPhase.Stationary), TouchAt(97, resolvePoint, TouchPhase.Ended));
                    RequireCanceledCommandPresses(heldCommand, held);
                    Require(!Read<bool>(app, scenario.Flag) && backend.HasSaveLoadoutCandidate,
                        "The fresh engine cancel/confirm must resolve once and retain its candidate.");
                    bool deleted = scenario.Commit && scenario.Command == "saveSlotDeleteButton";
                    for (int slot = 0; slot < records.Length; slot++)
                    {
                        bool deletedSlot = deleted && slot == 0;
                        bool renamedSlot = scenario.Commit && !deleted && slot == 0;
                        Require(Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == (deletedSlot ? 0 : uses[slot])
                            && Read<int[]>(backend, "saveLoadoutRenameSteps")[slot] == (deletedSlot ? 0 : names[slot] + (renamedSlot ? 1 : 0))
                            && (deletedSlot ? !backend.LoadSelectedSaveSlotView().SaveSlotOccupied[slot]
                                : renamedSlot ? Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].LoadoutName != records[slot].LoadoutName
                                : Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(records[slot])),
                            "The engine resolution must apply only its requested rename/delete and preserve other slots and uses.");
                    }
                    var resolved = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
                    var resolvedUses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
                    yield return TouchFrame(TouchAt(96, heldPoint, TouchPhase.Stationary));
                    yield return WaitSeconds(0.45f);
                    yield return TouchFrame(TouchAt(96, heldPoint, TouchPhase.Ended));
                    yield return TouchFrame();
                    Require(!Read<bool>(app, scenario.Flag) && !Read<bool>(app, "saveDraftSkipConfirmPending")
                        && state.CurrentState == (scenario.Infinite ? GreedLastScreenState.InfiniteLoadoutSelect : GreedLastScreenState.SaveLoadoutDraft)
                        && backend.HasSaveLoadoutCandidate,
                        "The delayed engine release must not reopen a resolved confirmation or leave its screen.");
                    for (int slot = 0; slot < resolved.Length; slot++)
                        Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(resolved[slot])
                            && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == resolvedUses[slot],
                            "The canceled delayed engine release must preserve all resolved records and uses.");
                    yield return TapTouch(heldCommand);
                    Require((deleted ? scenario.Infinite ? state.CurrentState == GreedLastScreenState.LobbyReady
                                : Read<bool>(app, "saveDraftSkipConfirmPending") : Read<bool>(app, scenario.Flag))
                        && backend.HasSaveLoadoutCandidate,
                        "A fresh engine press must still perform its own command after resolution.");
                    Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[1].Equals(records[1])
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[1] == uses[1]
                        && Read<int[]>(backend, "saveLoadoutRenameSteps")[1] == names[1],
                        "Engine resolution, delayed release and fresh commands must preserve the occupied neighbor.");
                    report.heldConfirmationResolutionCases++;
                }
                report.heldConfirmationResolutionsPreservedData = report.heldConfirmationResolutionCases == 9;
                report.heldConfirmationResolutionFreshTapRecovered = report.heldConfirmationResolutionsPreservedData;
                report.heldConfirmationOccupiedNeighborPreserved = report.heldConfirmationResolutionsPreservedData;
                Debug.Log("Greed engine held confirmation resolutions passed: nine actual EventSystem cancel/confirm pairs; registered delayed releases cannot reopen or exit, requested changes apply once, occupied renamed neighbors/uses/candidates survive and fresh taps recover. In-memory progress only.");
            }

            private IEnumerator VerifyHeldSaveExitConfirmations(string[] pendingFields)
            {
                foreach (bool overwrite in new[] { true, false })
                {
                    var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                    backend.AddSampleSaveLoadoutForTest(out _);
                    backend.AddSampleSaveLoadoutForTest(out _);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        backend.SelectSaveLoadoutSlot(slot);
                        backend.RenameSelectedSaveLoadoutSlot();
                    }
                    Read<int[]>(backend, "saveLoadoutUsesRemaining")[0] = 1;
                    backend.AddSampleClearCandidateForTest(out _);
                    backend.SelectSaveLoadoutSlot(0);
                    typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, backend);
                    foreach (string field in pendingFields)
                        typeof(GreedLastApp).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, false);
                    state.SetSaveLoadoutDraft(backend.LoadSelectedSaveSlotView(), backend.BuildSaveLoadoutDraftText(true),
                        saveLoadoutCandidateAvailable: true);
                    yield return null;
                    Button command = Read<Button>(ui, overwrite ? "nextPatternButton" : "returnLobbyButton");
                    yield return TapTouch(command);
                    string flag = overwrite ? "saveDraftOverwriteConfirmPending" : "saveDraftSkipConfirmPending";
                    Require(Read<bool>(app, flag) && backend.HasSaveLoadoutCandidate,
                        "Engine exit confirmation must first open without consuming its candidate.");
                    yield return WaitSeconds(0.45f);
                    var records = (GreedLastRunRecord[])Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots").Clone();
                    var uses = (int[])Read<int[]>(backend, "saveLoadoutUsesRemaining").Clone();
                    var names = (int[])Read<int[]>(backend, "saveLoadoutRenameSteps").Clone();
                    var expectedSave = (GreedLastRunRecord)typeof(GreedLastMockBackend)
                        .GetMethod("BuildSaveLoadoutRecord", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { Read<GreedLastRunRecord>(backend, "lastRunRecord") });
                    Vector2 heldPoint = ScreenPoint(command, -0.25f);
                    Vector2 confirmPoint = ScreenPoint(command, 0.25f);
                    yield return TouchFrame(TouchAt(99, heldPoint, TouchPhase.Began));
                    PointerEventData[] held = RequireRegisteredCommandPresses(command, 99);
                    yield return TouchFrame(TouchAt(99, heldPoint, TouchPhase.Stationary), TouchAt(100, confirmPoint, TouchPhase.Began));
                    yield return TouchFrame(TouchAt(99, heldPoint, TouchPhase.Stationary), TouchAt(100, confirmPoint, TouchPhase.Ended));
                    RequireCanceledCommandPresses(command, held);
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady && !Read<bool>(app, flag)
                        && !backend.HasSaveLoadoutCandidate,
                        "Engine overwrite/discard must consume once and return to lobby.");
                    yield return TouchFrame(TouchAt(99, heldPoint, TouchPhase.Stationary));
                    yield return WaitSeconds(0.45f);
                    yield return TouchFrame(TouchAt(99, heldPoint, TouchPhase.Ended));
                    yield return TouchFrame();
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate,
                        "The delayed engine release must not perform a new lobby command.");
                    yield return TapTouch(Read<Button>(ui, "saveLoadoutButton"));
                    Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft && !backend.HasSaveLoadoutCandidate,
                        "Fresh engine management taps must recover without recreating the candidate.");
                    for (int slot = 0; slot < records.Length; slot++)
                    {
                        bool replaced = overwrite && slot == 0;
                        Require(Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[slot].Equals(replaced ? expectedSave : records[slot])
                            && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == (replaced ? 2 : uses[slot])
                            && Read<int[]>(backend, "saveLoadoutRenameSteps")[slot] == (replaced ? 0 : names[slot]),
                            "Engine overwrite must replace only S1; discard and delayed releases must preserve other slots, uses and names.");
                    }
                    report.heldSaveExitConfirmationCases++;
                }
                report.heldSaveExitsPreservedData = report.heldSaveExitConfirmationCases == 2;
                report.heldSaveExitFreshTapRecovered = report.heldSaveExitsPreservedData;
                Debug.Log("Greed engine held save exits passed: two confirmed overwrite/discard exits consume once, delayed registered fingers cannot run lobby commands, named neighbors survive and fresh management taps recover. In-memory progress only.");
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

            private IEnumerator VerifyRecoveredCandidateSlotChoices()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                try
                {
                    core.Exit();
                    foreach (string entry in new[] { "saveLoadoutButton", "normalRunButton", "infiniteButton" })
                    {
                        for (int slot = 0; slot < 3; slot++)
                        {
                            string key = GreedLastMockBackend.PersistenceTestKeyPrefix + "engine-recovered-choice-" + Guid.NewGuid().ToString("N");
                            try
                            {
                                bool detailed = slot == 1;
                                if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                                var record = new GreedLastRunRecord(true, 800, 3, 0, 1, 6, 15, 0, 0, 90f,
                                    "engine-recovered-clear", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                                var backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                                int priorSlot = (slot + 1) % 3;
                                backend.RegisterClear(record);
                                backend.SelectSaveLoadoutSlot(priorSlot);
                                backend.ConfirmSaveLoadout();
                                GreedLastRunRecord prior = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[priorSlot];
                                if (slot == 1)
                                {
                                    backend.RenameSelectedSaveLoadoutSlot();
                                    prior = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[priorSlot];
                                    GreedLastPrototypeCandidateCheck.RemoveCandidateIdentities(key);
                                    backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                                    Require(!backend.HasSaveLoadoutCandidate
                                        && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[priorSlot].Equals(prior)
                                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[priorSlot] == 2,
                                        "Identity-less saved records must remain resolved after their managed rename, without changing the slot or uses.");
                                    report.legacyRenamedCandidateRepairCases++;
                                }
                                backend.RegisterClear(record);
                                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                                Require(backend.HasSaveLoadoutCandidate, "The engine UI must receive the reloaded new equal-stat clear.");
                                backendField.SetValue(app, backend);
                                state.SetLobbyReady(backend.LoadLobby());
                                yield return null;
                                Button entryButton = Read<Button>(ui, entry);
                                if (slot == 1) yield return ClickMouse(entryButton);
                                else yield return TapTouch(entryButton);
                                Require(state.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                                    && Read<bool>(app, "saveDraftRequiresExplicitSlotChoice")
                                    && !Read<Button>(ui, "nextPatternButton").IsInteractable(),
                                    "A real engine lobby entry must not accept its recommended slot as save consent: " + entry);
                                yield return null;
                                Call(app, "TryHandlePrimaryKeyboardFrame", true, false);
                                Require(backend.HasSaveLoadoutCandidate && Read<bool>(app, "saveDraftRequiresExplicitSlotChoice")
                                    && !backend.LoadSelectedSaveSlotView().SaveSlotOccupied[slot],
                                    "Fresh Enter before a slot choice must leave the recovered candidate and empty slot unchanged.");
                                yield return null;
                                Button lane = Read<Button[]>(ui, "laneButtons")[slot];
                                if (slot == 1) yield return ClickMouse(lane);
                                else yield return TapTouch(lane);
                                Require(!Read<bool>(app, "saveDraftRequiresExplicitSlotChoice")
                                    && backend.LoadSelectedSaveSlotView().SelectedSaveSlotIndex == slot,
                                    "A real engine lane press must explicitly choose the requested empty slot.");
                                Button save = Read<Button>(ui, "nextPatternButton");
                                if (slot == 1) yield return ClickMouse(save);
                                else yield return TapTouch(save);
                                Require(state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                                    && backend.LoadLobby().SaveSlotOccupied[slot]
                                    && !backend.LoadLobby().SaveSlotOccupied[(slot + 2) % 3]
                                    && Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[priorSlot].Equals(prior)
                                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[slot] == 2
                                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[priorSlot] == 2,
                                    "Engine confirmation must save only the chosen slot once, retaining the older slot and both uses.");
                                backend = GreedLastMockBackend.CreatePersistenceTestBackendForEditor(key);
                                backendField.SetValue(app, backend);
                                state.SetLobbyReady(backend.LoadLobby());
                                yield return null;
                                yield return TapTouch(Read<Button>(ui, "saveLoadoutButton"));
                                Require(!backend.HasSaveLoadoutCandidate && !Read<bool>(app, "saveDraftRequiresExplicitSlotChoice")
                                    && !Read<Button>(ui, "nextPatternButton").IsInteractable(),
                                    "The real engine management entry after reload must not offer a second save.");
                                report.recoveredCandidateSlotChoiceCases++;
                            }
                            finally
                            {
                                backendField.SetValue(app, originalBackend);
                                state.SetLobbyReady(originalBackend.LoadLobby());
                                PlayerPrefs.DeleteKey(key);
                                PlayerPrefs.Save();
                            }
                        }
                    }
                    report.recoveredCandidateSlotsPreserved = report.recoveredCandidateSlotChoiceCases == 9;
                    Debug.Log("Greed engine recovered candidate choices passed: nine isolated persistence keys, three visible lobby entries x three chosen empty slots, real synthetic mouse/touch frame progression, pre-choice Enter blocked, same-stat new clear reload/one save, older slots and two uses retained, resolved management after reload. Includes three identity-less saved/managed-name repairs before distinct modern equal-stat clears. Not physical input or a full process restart.");
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                }
            }

            private IEnumerator VerifyScreenKeyboardTransitions()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                try
                {
                    foreach (bool detailed in new[] { false, true })
                    {
                        if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                        var backend = new GreedLastMockBackend(loadPersistedData: false, persistData: false);
                        backendField.SetValue(app, backend);
                        backend.AddSampleClearCandidateForTest(out _);
                        state.SetLobbyReady(backend.LoadLobby());
                        Call(app, "RequestSaveLoadout");
                        yield return null;
                        Call(app, "HandleRunChannelInput", GreedLastRunChannel.Right);
                        Require(backend.LoadSelectedSaveSlotView().SelectedSaveSlotIndex == 2 && !Read<bool>(app, "saveDraftRequiresExplicitSlotChoice"),
                            "The screen fixture must select S3 before testing its confirmation boundary: frame=" + Time.frameCount
                                + ", transition=" + Read<int>(app, "screenInputTransitionFrame")
                                + ", input=" + ui.AcceptsPlayerInput + ", state=" + state.CurrentState);
                        Require((bool)Call(app, "TryHandlePrimaryKeyboardFrame", true, false)
                            && state.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                            && backend.HasSaveLoadoutCandidate
                            && Array.TrueForAll(backend.LoadSelectedSaveSlotView().SaveSlotOccupied, occupied => !occupied),
                            "A fresh Enter in the slot-change publication frame must not confirm an unseen changed slot.");
                        report.screenKeyboardTransitionCases++;
                        yield return null;
                        Require((bool)Call(app, "TryHandlePrimaryKeyboardFrame", true, false)
                            && state.CurrentState == GreedLastScreenState.LobbyReady && !backend.HasSaveLoadoutCandidate
                            && backend.LoadLobby().SaveSlotOccupied[2],
                            "The next-frame Enter must still save the displayed S3 candidate once: frame=" + Time.frameCount
                                + ", transition=" + Read<int>(app, "screenInputTransitionFrame")
                                + ", explicit=" + Read<bool>(app, "saveDraftRequiresExplicitSlotChoice")
                                + ", state=" + state.CurrentState);
                        Require((bool)Call(app, "TryHandlePrimaryKeyboardFrame", true, false)
                            && state.CurrentState == GreedLastScreenState.LobbyReady
                            && Read<int[]>(backend, "saveLoadoutUsesRemaining")[2] == 2,
                            "Enter in the new lobby publication frame must not skip its save summary into preparation.");
                        report.screenKeyboardTransitionCases++;
                        yield return null;
                        Require((bool)Call(app, "TryHandlePrimaryKeyboardFrame", true, false)
                            && state.CurrentState == GreedLastScreenState.InfiniteStartReady
                            && Read<int[]>(backend, "saveLoadoutUsesRemaining")[2] == 2,
                            "A later fresh Enter must open preparation without spending a start.");
                        Call(app, "RequestReturnToLobby");
                    }
                }
                finally
                {
                    core.Exit();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                }
                report.screenKeyboardFreshInputRecovered = report.screenKeyboardTransitionCases == 4;
                Debug.Log("Greed engine screen keyboard transitions passed: four clean/detailed changed-slot and save-to-lobby publication cases; displayed S3 single save and next-frame preparation recovered. Router boolean callbacks in actual frames, not OS keyboard input; personal data unchanged.");
            }

            private IEnumerator VerifyInitialRunChoiceBoundary()
            {
                GreedLastMockBackend originalBackend = Read<GreedLastMockBackend>(app, "backend");
                FieldInfo backendField = typeof(GreedLastApp).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                try
                {
                    foreach (bool detailed in new[] { false, true })
                    {
                        backendField.SetValue(app, new GreedLastMockBackend(loadPersistedData: false, persistData: false));
                        if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                        yield return ClickMouse(Read<Button>(ui, "normalRunButton"));
                        Require(run.ChoiceActive && run.ChoiceKind == GreedLastChoiceKind.StartGift,
                            "The initial-node fixture must begin at the actual normal opening choice.");
                        yield return WaitSeconds(0.4f);
                        yield return TapTouch(Read<Button[]>(ui, "laneButtons")[1]);
                        bool observed = false;
                        Action<GreedLastRunSnapshot> onChoice = snapshot =>
                        {
                            if (!snapshot.ChoiceActive || snapshot.ChoiceKind != GreedLastChoiceKind.Node || observed) return;
                            observed = true;
                            Require(snapshot.MissCount > 0, "The choice boundary must follow a natural input omission.");
                            int history = Read<int>(core, "choiceHistoryCount");
                            Call(app, "HandleRunChannelInput", GreedLastRunChannel.Left);
                            Require(run.ChoiceActive && run.ChoiceKind == GreedLastChoiceKind.Node
                                && Read<int>(core, "choiceHistoryCount") == history
                                && run.Score == snapshot.Score && run.Health == snapshot.Health,
                                "A gameplay lane key in the initial node publication frame must not choose an unseen route.");
                            report.initialChoiceBoundaryCases++;
                        };
                        core.SnapshotChanged += onChoice;
                        try
                        {
                            float deadline = Time.realtimeSinceStartup + 30f;
                            while (!observed)
                            {
                                Require(!run.Stopped && Time.realtimeSinceStartup < deadline,
                                    "The real first chapter must reach its node choice without clock edits or shortcuts.");
                                if (run.SuccessCount + run.GoodCount < 2 && run.ActivePattern && !run.CountdownActive
                                    && run.ElapsedSeconds + core.InputTimingOffsetSeconds >= run.Pattern.CoreSeconds)
                                    yield return TapTouch(Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1]);
                                yield return null;
                            }
                        }
                        finally { core.SnapshotChanged -= onChoice; }
                        yield return null;
                        yield return TapTouch(Read<Button[]>(ui, "laneButtons")[0]);
                        Require(run.ChoiceActive && run.ChoiceKind == GreedLastChoiceKind.Gift,
                            "A fresh next-frame node tap must choose the route exactly once.");
                        yield return WaitSeconds(0.2f);
                        yield return TapTouch(Read<Button[]>(ui, "laneButtons")[1]);
                        Require(!run.ChoiceActive && !run.Stopped,
                            "The following gift must remain selectable through its ordinary transition guard.");
                        Call(app, "RequestReturnToLobby");
                    }
                }
                finally
                {
                    core.Exit();
                    backendField.SetValue(app, originalBackend);
                    state.SetLobbyReady(originalBackend.LoadLobby());
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                }
                report.initialChoiceFreshInputRecovered = report.initialChoiceBoundaryCases == 2;
                Debug.Log("Greed engine initial choice protection passed: two real clean/detailed first-chapter node boundaries after natural input omission, unseen lane keys ignored, fresh route/gift selection recovered. Personal progress and choice rewards unchanged.");
            }

            private IEnumerator VerifyKeyboardTransitionFrames()
            {
                foreach (bool detailed in new[] { false, true })
                {
                    if (ui.UsePlayerRunControls == detailed) ui.ToggleDevCleanView();
                    yield return ClickMouse(Read<Button>(ui, "practiceRunButton"));
                    Require((bool)Call(app, "TryHandlePauseOrBackKeyboardInput", false, true) && run.Paused,
                        "A fresh pause key must pause the actual practice opening.");
                    GreedLastRunSnapshot paused = run;
                    Require(!(bool)Call(app, "TryHandlePauseOrBackKeyboardInput", false, false)
                        && !(bool)Call(app, "TryHandlePrimaryKeyboardCommand", false),
                        "No key must not be consumed as a transition command.");
                    foreach (int command in new[] { 0, 1, 2 })
                    {
                        bool handled = command == 2
                            ? (bool)Call(app, "TryHandlePrimaryKeyboardCommand", true)
                            : (bool)Call(app, "TryHandlePauseOrBackKeyboardInput", command == 0, command == 1);
                        Require(handled && run.Paused && !run.ResumeCountdownActive
                            && run.Score == paused.Score && run.Health == paused.Health,
                            "A same-frame Escape/P/Enter must not resume an unseen pause menu: " + command);
                        report.keyboardTransitionCases++;
                    }
                    yield return null;
                    Require((bool)Call(app, "TryHandlePrimaryKeyboardCommand", true) && run.ResumeCountdownActive,
                        "A fresh next-frame Enter must resume through the existing lead-in.");

                    bool terminalObserved = false;
                    Action<GreedLastRunSnapshot> terminal = snapshot =>
                    {
                        if (!snapshot.Stopped || terminalObserved) return;
                        terminalObserved = true;
                        Require((bool)Call(app, "TryHandlePauseOrBackKeyboardInput", true, false)
                            && run.Stopped && state.CurrentState == GreedLastScreenState.RunCoreTest
                            && run.Score == snapshot.Score && run.Health == snapshot.Health,
                            "Escape in the actual terminal publication frame must not dismiss an unseen result.");
                        report.keyboardTransitionCases++;
                        Require((bool)Call(app, "TryHandlePrimaryKeyboardCommand", true)
                            && run.Stopped && state.CurrentState == GreedLastScreenState.RunCoreTest
                            && run.Score == snapshot.Score && run.Health == snapshot.Health,
                            "Enter in the actual terminal publication frame must not restart an unseen result.");
                        report.keyboardTransitionCases++;
                    };
                    core.SnapshotChanged += terminal;
                    try
                    {
                        float deadline = Time.realtimeSinceStartup + 25f;
                        while (!terminalObserved)
                        { Require(Time.realtimeSinceStartup < deadline, "Practice must naturally fail without synthetic clock edits."); yield return null; }
                    }
                    finally { core.SnapshotChanged -= terminal; }
                    yield return null;
                    if (detailed)
                    {
                        Require((bool)Call(app, "TryHandlePrimaryKeyboardCommand", true) && !run.Stopped && run.PracticeMode,
                            "Fresh next-frame Enter must still restart a failed practice.");
                        Call(app, "RequestReturnToLobby");
                    }
                    else Call(app, "TryHandlePauseOrBackKeyboardInput", true, false);
                    Require(state.CurrentState == GreedLastScreenState.LobbyReady,
                        "Fresh result navigation must return to the lobby normally.");
                }
                if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                report.keyboardTransitionFreshInputRecovered = report.keyboardTransitionCases == 10;
                Debug.Log("Greed engine keyboard transition protection passed: clean/detailed pause Escape/P/Enter and natural failure Escape/Enter; next-frame resume, back and restart recovered. Router callbacks, no OS key injection or clock edits.");
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

            private IEnumerator VerifyRecoveryPreviews()
            {
                GreedLastMockBackend backend = Read<GreedLastMockBackend>(app, "backend");
                try
                {
                    core.Exit();
                    if (ui.UsePlayerRunControls == report.domainReloadDisabled) ui.ToggleDevCleanView();
                    foreach (int gifts in new[] { 0, 2 })
                    {
                        core.ConfigureGrowth(0, 0, 0);
                        state.SetInfiniteRun(string.Empty);
                        string summary = gifts == 0 ? "안정 루트" : "안정 루트 / 생존 축 / 생존 축";
                        core.EnterInfiniteRun(new GreedLastRunRecord(true, 0, 3, 0, 0, 0, 0, 0, 0, 20f,
                            "engine-recovery", string.Empty, gifts == 0 ? string.Empty : "생존 축", string.Empty, summary, string.Empty));
                        if (run.DevInvincible) core.ToggleDevInvincible();
                        for (int inputIndex = 0; inputIndex < (gifts == 0 ? 2 : 3); inputIndex++)
                        {
                            bool miss = gifts > 0 && inputIndex < 2;
                            float deadline = Time.realtimeSinceStartup + 9f;
                            while (!run.ActivePattern || run.CountdownActive || run.ElapsedSeconds + core.InputTimingOffsetSeconds
                                < run.Pattern.CoreSeconds + (miss ? 0f : run.Pattern.SuccessWindowSeconds + 0.015f))
                            {
                                Require(!run.Stopped && Time.realtimeSinceStartup < deadline,
                                    "Recovery checks need actual engine-driven trap timing, without clock edits.");
                                yield return null;
                            }
                            var before = run;
                            int expected = miss ? -1 : gifts > 0 ? 2 : inputIndex == 0 ? 1 : 0;
                            int lane = (int)run.Pattern.Channel - 1;
                            if (miss) lane = (lane + 1) % 3;
                            yield return TapTouch(Read<Button[]>(ui, "laneButtons")[lane]);
                            Require(run.LastResult == (miss ? GreedLastJudgementResult.Miss : GreedLastJudgementResult.Good)
                                && run.Health - before.Health == expected && run.LastJudgementHealthChange == expected,
                                "Engine recovery must retain two actual -1 hits, +1/+0 stable and +2 stacked Good recovery.");
                            if (miss) report.engineRecoveryDamageCases++;
                            else
                            {
                                Require(before.GoodHealthGainPreview == expected,
                                    "Engine recovery preview must match actual healing and zero after the cap.");
                                report.engineRecoveryGoodPreviews++;
                                report.engineRecoveryActualHealing += expected;
                                report.engineRecoveryCapPreserved |= expected == 0 && run.Health == 5;
                            }
                        }
                        core.Exit();
                        state.SetLobbyReady(backend.LoadLobby());
                        yield return null;
                    }
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    core.Exit();
                    core.ConfigureGrowth(backend.GrowthSurvivalLevel, backend.GrowthFocusLevel, backend.GrowthTimingLevel);
                    state.SetLobbyReady(backend.LoadLobby());
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                }
                Debug.Log("Greed engine recovery previews passed: three Good predictions (+1/+0/+2), two natural -1 hits,"
                    + " actual Update/EventSystem touch frames, growth/source restored and no persistence or private clocks edited. Not physical input or device-performance testing.");
            }

            private IEnumerator VerifyGoodFocusStates()
            {
                GreedLastMockBackend backend = Read<GreedLastMockBackend>(app, "backend");
                var loadout = new GreedLastRunRecord(true, 0, 3, 0, 0, 0, 0, 0, 0, 20f,
                    "engine-good-focus", "박자 감각", string.Empty, string.Empty, "박자 감각 / 변주 루트", string.Empty);
                try
                {
                    core.Exit();
                    if (ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                    for (int mode = 0; mode < 3; mode++)
                    {
                        core.ConfigureGrowth(0, mode == 0 ? 0 : 3, 0);
                        state.SetInfiniteRun(string.Empty);
                        core.EnterInfiniteRun(loadout);
                        if (run.DevInvincible) core.ToggleDevInvincible();
                        if (mode == 2)
                            for (int hit = 0; hit < 2; hit++)
                            {
                                yield return WaitForTouchablePattern();
                                yield return TapTouch(Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1]);
                                Require(run.LastResult == GreedLastJudgementResult.Success, "Retained-chain setup needs two real successful taps.");
                            }
                        float deadline = Time.realtimeSinceStartup + 9f;
                        while (!run.ActivePattern || run.CountdownActive || run.ElapsedSeconds + core.InputTimingOffsetSeconds
                            < run.Pattern.CoreSeconds + run.Pattern.SuccessWindowSeconds + 0.015f)
                        {
                            Require(!run.Stopped && Time.realtimeSinceStartup < deadline, "Good focus cases need real engine-driven Good timing.");
                            yield return null;
                        }
                        var before = run;
                        yield return TapTouch(Read<Button[]>(ui, "laneButtons")[(int)run.Pattern.Channel - 1]);
                        yield return null;
                        Require(run.LastResult == GreedLastJudgementResult.Good && run.FocusStateActive
                            && run.LastJudgementFocusStateExtensionSeconds == 0f,
                            "Good focus cases must keep their real judgement and must not extend the timer.");
                        bool activates = mode == 0;
                        bool retains = mode == 2;
                        Require(before.FocusStateActive != activates && (activates ? before.Focus == 2 : true)
                            && (retains ? run.Combo > 0 && run.Combo == before.Combo - 1 : run.Combo == 0),
                            "Good states must arise from actual charge/activation/empty or retained combos, not seeded fields.");
                        string feedback = Read<Text>(ui, "judgementFeedbackText").text;
                        Require(feedback.Contains("집중 발동", StringComparison.Ordinal) == activates
                            && feedback.Contains("집중 연쇄 완충", StringComparison.Ordinal) == retains,
                            "Visible engine Good feedback must distinguish activation, empty and retained chains. Actual: " + feedback);
                        report.engineGoodFocusStateCases++;
                        core.Exit();
                        state.SetLobbyReady(backend.LoadLobby());
                        yield return null;
                    }
                }
                finally
                {
                    input.Touches = Array.Empty<Touch>();
                    core.Exit();
                    core.ConfigureGrowth(backend.GrowthSurvivalLevel, backend.GrowthFocusLevel, backend.GrowthTimingLevel);
                    state.SetLobbyReady(backend.LoadLobby());
                    if (!ui.UsePlayerRunControls) ui.ToggleDevCleanView();
                }
                Debug.Log("Greed engine Good focus states passed: new activation, active empty combo and genuinely retained combo,"
                    + " visible detailed feedback after real Update/EventSystem touches, no Good timer extension. Public temporary core/loadout setup,"
                    + " growth/source restored, no private clocks/state or personal persistence; not hardware/device testing.");
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
                    GreedLastRunRecord prepared = GreedLastRunCore.CreatePreparedInfiniteLoadout(
                        Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[report.savedSlotIndex],
                        Read<string>(app, "infinitePreparationGift"), Read<string>(app, "infinitePreparationRelic"));
                    yield return ConfirmWithTwoFingers(Read<Button>(ui, "nextPatternButton"), use != 0);
                    Require(state.CurrentState == GreedLastScreenState.InfiniteRun && run.InfiniteMode && !run.Stopped
                        && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == 1 - use
                        && backend.LoadLobby().SaveSlotOccupied[report.savedSlotIndex] == (use == 0),
                        "Only actual infinite start may consume a use, with immediate source auto-clear on its second start.");
                    VerifyPreparedInfinitePreview(prepared);
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
                    yield return null;
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
                GreedLastRunRecord stored = Read<GreedLastRunRecord[]>(backend, "saveLoadoutSlots")[report.savedSlotIndex];
                VerifyPreparedInfinitePreview(GreedLastRunCore.CreatePreparedInfiniteLoadout(stored,
                    Read<string>(app, "infinitePreparationGift"), Read<string>(app, "infinitePreparationRelic")));
                yield return WaitSeconds(0.4f);
                yield return VerifyPreparationTouches(1, 2, uses);
                Require(Read<int>(app, "infinitePreparationStep") == 2
                    && Read<int[]>(backend, "saveLoadoutUsesRemaining")[report.savedSlotIndex] == uses,
                    "Gift/relic preparation must remain separate from actual start consumption.");
                VerifyPreparedInfinitePreview(GreedLastRunCore.CreatePreparedInfiniteLoadout(stored,
                    Read<string>(app, "infinitePreparationGift"), Read<string>(app, "infinitePreparationRelic")));
                yield return WaitSeconds(0.4f);
                yield return VerifyPreparationTouches(2, 2, uses);
                report.multiTouchPreparationPreservedUses = true;
            }

            private void VerifyPreparedInfinitePreview(GreedLastRunRecord prepared)
            {
                GreedLastMockBackend backend = Read<GreedLastMockBackend>(app, "backend");
                string bonus = GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(prepared);
                string start = GreedLastRunCore.BuildInfiniteLoadoutStartStatePreview(prepared,
                    backend.GrowthSurvivalLevel, backend.GrowthFocusLevel, backend.GrowthTimingLevel);
                Require(state.CurrentDetail.Contains(bonus, StringComparison.Ordinal)
                    && state.CurrentDetail.Contains(start, StringComparison.Ordinal),
                    "Real engine gift/relic preparation and both start notices must describe the applied loadout, not its unprepared source.");
                report.preparedInfinitePreviewCases++;
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
                    if (run.ActivePattern && snapshot.LastJudgedPattern.PatternId == run.Pattern.PatternId
                        && snapshot.SuccessCount + snapshot.GoodCount + snapshot.MissCount
                            == run.SuccessCount + run.GoodCount + run.MissCount + 1
                        && (snapshot.LastCenterHit || snapshot.LastResult == GreedLastJudgementResult.Good))
                    {
                        int expected = snapshot.LastCenterHit ? run.CenterHitScoreGainPreview : run.GoodScoreGainPreview;
                        Require(expected == snapshot.LastJudgementScoreGain,
                            "Engine center/Good reward previews must include the actual next chain and bundle gain: expected="
                                + expected + ", actual=" + snapshot.LastJudgementScoreGain);
                        if (snapshot.LastResult == GreedLastJudgementResult.Good)
                        {
                            Require(run.GoodHealthGainPreview == snapshot.LastJudgementHealthChange,
                                "Every actual engine Good must match its immediately preceding recovery prediction.");
                            string goodEffect = (string)typeof(GreedLastRuntimeUi).GetMethod("BuildGoodEffectLine", BindingFlags.Static | BindingFlags.NonPublic)
                                .Invoke(null, new object[] { snapshot });
                            bool retains = run.FocusStateActive && snapshot.Combo > 0;
                            Require(goodEffect.Contains("집중 연쇄 완충", StringComparison.Ordinal) == retains
                                && (run.FocusStateActive || !snapshot.FocusStateActive || goodEffect.Contains("집중 발동", StringComparison.Ordinal)),
                                "Engine Good feedback must distinguish new focus activation from actually retained combos.");
                            report.engineGoodFocusFeedbackCases++;
                        }
                        report.liveRewardPreviewJudgements++;
                        int expectedThreat = snapshot.LastResult == GreedLastJudgementResult.Success && run.InfiniteMode
                            ? (int)typeof(GreedLastRunCore).GetMethod("BuildThreatPrecisionBonusForLevel", BindingFlags.Static | BindingFlags.NonPublic)
                                .Invoke(null, new object[] { run.InfiniteThreatLevel, run.Pattern.TimingType }) : 0;
                        Require(!snapshot.LastJudgementScoreLimited && snapshot.LastJudgementThreatScoreBonus == expectedThreat
                            && snapshot.LastJudgementProfileScoreBonus >= 0,
                            "Engine resolved bonus accounting must retain the scored threat/profile rather than subsequent progression.");
                        string profileBonus = (string)typeof(GreedLastRuntimeUi).GetMethod("BuildChoiceProfileScoreBonusPreview", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { snapshot });
                        Require(profileBonus == (snapshot.LastJudgementProfileScoreBonus > 0 ? "조합 +" + snapshot.LastJudgementProfileScoreBonus : string.Empty),
                            "Engine profile feedback must use its actual scoring component.");
                        report.engineResolvedBonusCases++;
                        if (snapshot.InfiniteThreatLevel > run.InfiniteThreatLevel) report.engineResolvedBonusThreatBoundaries++;
                        Require(snapshot.LastJudgementStartedInFocusState == run.FocusStateActive,
                            "Engine focus provenance must distinguish activation from prior active state.");
                        if (run.FocusStateActive && snapshot.LastResult == GreedLastJudgementResult.Success)
                        {
                            float gained = Mathf.Max(0f, snapshot.FocusStateRemainingSeconds - run.FocusStateRemainingSeconds);
                            Require(Mathf.Abs(snapshot.LastJudgementFocusStateExtensionSeconds - gained) < 0.0002f,
                                "Engine focus feedback must retain the actual judgement/bundle time extension.");
                            report.engineLiveFocusFeedbackCases++;
                        }
                    }
                    if (run.ActivePattern && run.ActiveRouteProfile == "안정 루트"
                        && snapshot.StableRouteBundleCount == run.StableRouteBundleCount + 1)
                    {
                        bool heals = run.GoodHealthGainPreview > 0;
                        Require(snapshot.LastResult == GreedLastJudgementResult.Success
                            && snapshot.LastJudgementHealthChange == (heals ? 1 : 0)
                            && snapshot.JudgementText.Contains("패턴 묶음 완벽 - 안정 완주 / "
                                + (heals ? "체력 +1" : "집중"), StringComparison.Ordinal),
                            "Actual engine stable finishers must retain recovery or their existing capped focus fallback.");
                        report.engineStableFinisherPreviews++;
                        if (!heals) report.engineStableFocusFallbacks++;
                    }
                    if (run.ChoiceActive && (!snapshot.ChoiceActive || snapshot.ChoiceKind != run.ChoiceKind))
                    {
                        string healthChoice = Array.Find(new[] { "안정 호흡", "안정 루트", "생존 축", "사막의 잔상" },
                            choice => snapshot.JudgementText.StartsWith(choice + " 선택", StringComparison.Ordinal));
                        if (healthChoice != null)
                        {
                            int gain = snapshot.Health - run.Health;
                            Require(run.ChoiceHealthGainPreview == gain && (gain == 0 || gain == 1) && snapshot.JudgementText.Contains(gain == 0
                                ? "효과: 체력 상한 유지" : "효과: 체력 +1", StringComparison.Ordinal),
                                "Actual engine health choices must report their real gain or existing cap.");
                            report.engineChoiceHealthPreviews++;
                            int mismatches = 0;
                            GreedLastPrototypeRecoveryCheck.CheckChoicePreview(run, healthChoice,
                                gain == 0 ? "체력 상한 유지" : "체력 +1", ref mismatches);
                            Require(mismatches == 0, "Engine choice health previews must match the actual result.");
                            if (gain == 0) report.engineCappedChoiceHealthPreviews++;
                        }
                        string focusChoice = Array.Find(new[] { "박자 감각", "변주 루트", "집중 축", "사막의 잔상", "파라오의 각인" },
                            choice => snapshot.JudgementText.StartsWith(choice + " 선택", StringComparison.Ordinal));
                        if (focusChoice != null)
                        {
                            string expected = run.FocusStateActive
                                ? snapshot.FocusStateRemainingSeconds > run.FocusStateRemainingSeconds + 0.001f ? "집중 상태 연장" : "집중 상태 상한 유지"
                                : snapshot.FocusStateActive ? "집중 상태 발동" : "집중 +" + (snapshot.Focus - run.Focus);
                            int mismatches = 0;
                            GreedLastPrototypeRecoveryCheck.CheckChoicePreview(run, focusChoice, expected, ref mismatches);
                            Require(mismatches == 0 && snapshot.JudgementText.Contains(expected, StringComparison.Ordinal),
                                "Engine choice focus previews must match real charge, activation, extension or cap.");
                            report.engineChoiceFocusPreviews++;
                        }
                    }
                    if (run.InfiniteMode && snapshot.InfiniteMode && run.ActivePattern
                        && snapshot.InfiniteSectionsCleared == run.InfiniteSectionsCleared + 1)
                    {
                        Require(run.NextInfiniteThreatLevel == snapshot.InfiniteThreatLevel,
                            "Actual engine section completions must retain their preceding next-threat prediction.");
                        report.engineThreatPreviewBoundaries++;
                    }
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

            private IEnumerator TapTouch(Button button, int fingerId = 71)
            {
                Require(button.isActiveAndEnabled && button.IsInteractable(),
                    "The engine touch target must be visible and interactable: " + button.name);
                input.MouseEnabled = false;
                Vector2 point = ScreenPoint(button);
                input.Touches = new[] { new Touch { fingerId = fingerId, position = point, phase = TouchPhase.Began } };
                yield return null;
                input.Touches = new[] { new Touch { fingerId = fingerId, position = point, phase = TouchPhase.Stationary } };
                yield return null;
                input.Touches = new[] { new Touch { fingerId = fingerId, position = point, phase = TouchPhase.Ended } };
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
        private static object Call(object target, string name, params object[] args) => target.GetType()
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
