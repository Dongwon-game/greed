using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastPrototypeSettingsCheck
    {
        private const string VolumeKey = "GreedLast.RuntimeUi.SfxVolume";
        private static readonly float[] Samples = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -2f, 2f, 0f, 0.075f };

        [MenuItem("Greed Last/Run Settings Recovery Check")]
        public static void Run()
        {
            VerifyTiming();
            VerifyAudio();
            Debug.Log("Greed settings recovery passed: finite and non-finite saved values, read-only fallback, invalid command rejection, normal adjustments and exact practice judgement. Original preferences restored.");
        }

        [MenuItem("Greed Last/Run Settings Recovery Check", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Greed Last/Run Presentation Settings Ownership Check")]
        public static void VerifyPresentationLifetime()
        {
            Require(CanRun(), "Presentation ownership checks require Edit mode.");
            int originalQuality = QualitySettings.GetQualityLevel();
            int originalVSync = QualitySettings.vSyncCount;
            int originalFrameRate = Application.targetFrameRate;
            int originalSleep = Screen.sleepTimeout;
            bool originalIsolation = SessionState.GetBool(GreedLastApp.EditorPlayModeCheckKey, false);
            string preferences = CapturePreferences();
            GameObject cameraBefore = GameObject.Find("GreedLastCamera");
            GameObject eventsBefore = GameObject.Find("GreedLastEventSystem");
            GameObject host = null, canvas = null;
            int alternateQuality = originalQuality == 0 ? 1 : 0;
            int alternateVSync = 0;
            bool alternateCaptured = false;
            try
            {
                Require(QualitySettings.names.Length > 1, "Quality ownership checks need two configured levels.");
                QualitySettings.SetQualityLevel(alternateQuality, false);
                alternateVSync = QualitySettings.vSyncCount;
                alternateCaptured = true;
                QualitySettings.SetQualityLevel(originalQuality, false);
                SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, true);
                for (int scenario = 0; scenario < 6; scenario++)
                {
                    QualitySettings.SetQualityLevel(originalQuality, false);
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = 37;
                    Screen.sleepTimeout = SleepTimeout.SystemSetting;
                    int priorSleep = Screen.sleepTimeout;
                    host = new GameObject("GreedPresentationOwnershipCheck");
                    GreedLastApp app = host.AddComponent<GreedLastApp>();
                    Require(Read<GreedLastMockBackend>(app, "backend") == null,
                        "The Edit-mode fixture must not start a personal backend automatically.");
                    Call(app, "Awake");
                    var ui = Read<GreedLastRuntimeUi>(app, "runtimeUi");
                    canvas = Read<GameObject>(ui, "rootPanel").GetComponentInParent<Canvas>().gameObject;
                    Require(!Read<bool>(Read<GreedLastMockBackend>(app, "backend"), "persistData")
                        && QualitySettings.vSyncCount == 0 && Application.targetFrameRate == 60,
                        "Actual isolated Awake must apply the existing prototype pacing without persistent data.");
                    bool changedVSync = scenario == 1 || scenario == 2;
                    bool changedFrameRate = scenario == 1 || scenario == 3;
                    if (changedVSync) QualitySettings.vSyncCount = 2;
                    if (changedFrameRate) Application.targetFrameRate = 41;
                    if (scenario == 1) Screen.sleepTimeout = SleepTimeout.SystemSetting;
                    bool changedQuality = scenario >= 4;
                    if (changedQuality)
                    {
                        QualitySettings.SetQualityLevel(alternateQuality, false);
                        QualitySettings.vSyncCount = scenario == 4 ? 0 : 2;
                    }
                    int expectedQuality = changedQuality ? alternateQuality : originalQuality;
                    int expectedVSync = changedQuality ? (scenario == 4 ? 0 : 2) : (changedVSync ? 2 : 1);
                    Call(app, "OnDestroy");
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        Require(QualitySettings.GetQualityLevel() == expectedQuality
                            && QualitySettings.vSyncCount == expectedVSync
                            && Application.targetFrameRate == (changedFrameRate ? 41 : 37)
                            && Screen.sleepTimeout == priorSleep,
                            "App removal must restore owned pacing and preserve independently changed values: "
                                + $"case={scenario}, quality={QualitySettings.GetQualityLevel()}, vSync={QualitySettings.vSyncCount}, target={Application.targetFrameRate}.");
                        Call(app, "OnDestroy");
                    }
                    GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                    UnityEngine.Object.DestroyImmediate(host);
                    host = null;
                    UnityEngine.Object.DestroyImmediate(canvas);
                    canvas = null;
                    DestroyCreated("GreedLastCamera", cameraBefore);
                    DestroyCreated("GreedLastEventSystem", eventsBefore);
                }
                host = new GameObject("GreedUninitializedPresentationCheck");
                GreedLastApp uninitialized = host.AddComponent<GreedLastApp>();
                int beforeVSync = QualitySettings.vSyncCount, beforeFrameRate = Application.targetFrameRate;
                Call(uninitialized, "OnDestroy");
                Require(QualitySettings.vSyncCount == beforeVSync && Application.targetFrameRate == beforeFrameRate,
                    "An uninitialized app must not restore settings it never captured.");
            }
            finally
            {
                if (host != null)
                {
                    GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                DestroyCreated("GreedLastCamera", cameraBefore);
                DestroyCreated("GreedLastEventSystem", eventsBefore);
                if (alternateCaptured)
                {
                    QualitySettings.SetQualityLevel(alternateQuality, false);
                    QualitySettings.vSyncCount = alternateVSync;
                }
                QualitySettings.SetQualityLevel(originalQuality, false);
                QualitySettings.vSyncCount = originalVSync;
                Application.targetFrameRate = originalFrameRate;
                Screen.sleepTimeout = originalSleep;
                SessionState.SetBool(GreedLastApp.EditorPlayModeCheckKey, originalIsolation);
            }
            Require(QualitySettings.GetQualityLevel() == originalQuality && QualitySettings.vSyncCount == originalVSync
                && Application.targetFrameRate == originalFrameRate && Screen.sleepTimeout == originalSleep,
                "Presentation checks must restore the original selected quality and global settings.");
            Require(CapturePreferences() == preferences, "Presentation ownership must not change personal preferences.");
            Debug.Log("Greed presentation ownership passed: six actual isolated Awake/OnDestroy paths, mixed external "
                + "pacing changes, quality-level changes, repeated release and uninitialized removal. Original quality levels, global settings and personal values preserved; no device FPS measurement.");
        }

        [MenuItem("Greed Last/Run Presentation Settings Ownership Check", true)]
        private static bool CanRunPresentationLifetime() => CanRun();

        private static string CapturePreferences() => JsonUtility.ToJson(typeof(GreedLastPrototypePlayModeCheck)
            .GetMethod("CapturePreferences", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));

        private static void DestroyCreated(string name, GameObject previous)
        {
            GameObject created = GameObject.Find(name);
            if (previous == null && created != null) UnityEngine.Object.DestroyImmediate(created);
        }

        public static void VerifyTiming()
        {
            Require(CanRun(), "Settings checks require Edit mode.");
            string key = GreedLastRunCore.InputTimingOffsetPrefsKey;
            bool existed = PlayerPrefs.HasKey(key);
            float saved = PlayerPrefs.GetFloat(key);
            try
            {
                foreach (float sample in Samples)
                {
                    PlayerPrefs.SetFloat(key, sample);
                    float expected = Finite(sample) ? Mathf.Clamp(sample, -0.2f, 0.2f) : 0f;
                    var core = new GreedLastRunCore();
                    Require(core.InputTimingOffsetSeconds == expected,
                        "A non-finite stored timing offset must become neutral before judging inputs: " + sample);
                    Require(Same(PlayerPrefs.GetFloat(key), sample), "Loading timing must not rewrite the stored preference.");
                    GreedLastRunSnapshot run = default;
                    int publications = 0;
                    core.SnapshotChanged += value => { run = value; publications++; };
                    float clock = Time.realtimeSinceStartup;
                    core.RestartPractice(clock);
                    core.Tick(clock += 2f);
                    core.Tick(clock += 1f);
                    Require(run.ActivePattern, "Settings recovery needs an active practice trap.");
                    core.HandleInput(run.Pattern.Channel, clock + run.Pattern.CoreSeconds - expected);
                    Require(run.LastResult == GreedLastJudgementResult.Success && run.SuccessCount == 1,
                        "Recovered calibration must still allow an exact real-core judgement.");
                    int before = publications;
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                        core.AdjustInputTimingOffset(invalid);
                    Require(core.InputTimingOffsetSeconds == expected && publications == before
                        && Same(PlayerPrefs.GetFloat(key), sample),
                        "Invalid calibration commands must not alter state, publish or overwrite preferences.");
                    core.AdjustInputTimingOffset(0.02f);
                    float adjusted = Mathf.Clamp(expected + 0.02f, -0.2f, 0.2f);
                    Require(Mathf.Approximately(core.InputTimingOffsetSeconds, adjusted)
                        && Mathf.Approximately(PlayerPrefs.GetFloat(key), adjusted),
                        "A valid adjustment must recover persistence and keep the normal range.");
                    core.ResetInputTimingOffset();
                    Require(PlayerPrefs.GetFloat(key) == 0f, "Reset must still persist neutral calibration.");
                }
            }
            finally { Restore(key, existed, saved); }
        }

        public static void VerifyAudio()
        {
            Require(CanRun(), "Settings checks require Edit mode.");
            bool existed = PlayerPrefs.HasKey(VolumeKey);
            float saved = PlayerPrefs.GetFloat(VolumeKey);
            GameObject host = null;
            try
            {
                foreach (float sample in Samples)
                {
                    PlayerPrefs.SetFloat(VolumeKey, sample);
                    float expected = Finite(sample) ? Mathf.Clamp01(sample) : 1f;
                    host = new GameObject("GreedSettingsRecoveryCheck");
                    GreedLastRuntimeUi ui = host.AddComponent<GreedLastRuntimeUi>();
                    Call(ui, "CreateAudio");
                    AudioSource source = Read<AudioSource>(ui, "rhythmAudio");
                    Require(Read<float>(ui, "sfxVolume") == expected && source.volume == expected,
                        "Stored sound preferences must be finite before reaching the audio source: " + sample);
                    Require(Same(PlayerPrefs.GetFloat(VolumeKey), sample), "Loading audio must not rewrite the stored preference.");
                    string label = GreedLastRuntimeUi.BuildSensorySettingsChecklistTextFromPrefs();
                    Require(label.Contains("소리 " + Mathf.RoundToInt(expected * 100f) + "%", StringComparison.Ordinal),
                        "Settings summary must agree with the recovered runtime volume.");
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                        Call(ui, "SetSfxVolume", invalid);
                    Require(Read<float>(ui, "sfxVolume") == expected && source.volume == expected
                        && Same(PlayerPrefs.GetFloat(VolumeKey), sample),
                        "Invalid volume commands must preserve the current source and stored preference.");
                    Call(ui, "SetSfxVolume", 0.4f);
                    Require(Mathf.Approximately(source.volume, 0.4f) && Mathf.Approximately(PlayerPrefs.GetFloat(VolumeKey), 0.4f),
                        "Normal volume changes must work and persist after recovery.");
                    Call(ui, "SetSfxVolume", 0f);
                    Require(source.mute && source.volume == 0f, "Mute must still work after settings recovery.");
                    GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                    UnityEngine.Object.DestroyImmediate(host);
                    host = null;
                }
            }
            finally
            {
                if (host != null)
                {
                    GreedLastPrototypeAudioCheck.ReleaseEditorAudio(host);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                Restore(VolumeKey, existed, saved);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Same(float left, float right) => left == right || (float.IsNaN(left) && float.IsNaN(right));
        private static T Read<T>(object target, string field)
            => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Restore(string key, bool existed, float value)
        {
            if (existed) PlayerPrefs.SetFloat(key, value);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Greed settings recovery: " + message);
        }
    }
}
