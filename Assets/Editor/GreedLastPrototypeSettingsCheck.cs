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
